# CheckRules Execution Order (Batch Rule Runner)

This document applies to CSLA 11. It describes how `BusinessRules.CheckRules()` and `CheckRulesAsync()` run rules, how priority orders rules across properties, when rules rerun, and how to configure or opt out of the behavior.

## Summary for code generation

* `CheckRules()` runs **every property rule once**, ordered by `Priority` **across all properties** (lowest first), then reruns only rules whose inputs were changed by **rule output values** (`context.AddOutValue`).
* Set values from a rule with `context.AddOutValue(property, value)`. A rule that writes the property some other way (for example by casting `context.Target` and setting a property) does **not** cause dependent rules to rerun during `CheckRules()`.
* `CascadeIfDirty` has no effect in `CheckRules()`. It still applies when a property is set and to `CheckRules(property)`.
* With `CascadeOnDirtyProperties = true`, rules that keep changing each other's values throw `InvalidOperationException` after `MaxRerunPasses` passes (default 100).
* `CheckRules(property)`, the rules that run when a property is set, and `CheckObjectRules()` are unchanged from CSLA 10.
* `UseLegacyCheckRules()` restores the CSLA 10 behavior. It is temporary; don't use it in new code.

## The batch algorithm

When `CheckRules()` (or `CheckRulesAsync()`) is called:

1. Object-level rules (rules with no primary property) run first.
2. Every property rule runs once, in `Priority` order across all properties. Rules with the same priority run by property (in the order each property's first rule was added), then in the order the rules were added.
3. If rules changed property values through output values, the rules for those properties, and the rules that use them as input properties, run again, once.
4. With `CascadeOnDirtyProperties = true`, step 3 repeats until no rule changes a value, up to `MaxRerunPasses`.

Short-circuiting is still per property: a broken `Error` rule stops that property's rules above `ProcessThroughPriority`, and `context.AddSuccessResult(true)` / `StopProcessing` stops the rest of that property's rules.

`CheckRules()` returns a `List<string>` of the property names affected, including each rule's `AffectedProperties`.

## Priority across properties

```csharp
[Serializable]
public partial class OrderEdit : BusinessBase<OrderEdit>
{
    public static readonly PropertyInfo<string> CustomerIdProperty = RegisterProperty<string>(nameof(CustomerId));
    public string CustomerId
    {
        get => GetProperty(CustomerIdProperty)!;
        set => SetProperty(CustomerIdProperty, value);
    }

    public static readonly PropertyInfo<decimal> DiscountProperty = RegisterProperty<decimal>(nameof(Discount));
    public decimal Discount
    {
        get => GetProperty(DiscountProperty);
        set => SetProperty(DiscountProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // MaxDiscount is a custom rule (not built in). Added first, but priority 10:
        // runs after the CustomerId rule below
        BusinessRules.AddRule(new MaxDiscount(DiscountProperty, CustomerIdProperty) { Priority = 10 });

        // Csla.Rules.CommonRules.Required, priority 0 (the default): runs first in
        // CheckRules(), even though it belongs to another property
        BusinessRules.AddRule(new Required(CustomerIdProperty));
    }

    [Create]
    private void Create()
    {
        BusinessRules.CheckRules();
    }
}
```

In CSLA 10, `CheckRules()` ran all of `Discount`'s rules, then all of `CustomerId`'s rules, because priority only ordered rules within one property. In CSLA 11 the `CustomerId` rule (priority 0) runs before the `Discount` rule (priority 10).

Use priority across properties when a rule depends on another property's rules having run: give the rules that must run first a lower number.

## Reruns come only from output values

Write calculated values through `AddOutValue`. That is what tells the batch runner which values changed:

```csharp
// Correct: the runner sees the change and reruns rules that use LineTotal
public class CalcLineTotal : BusinessRule
{
    public CalcLineTotal(IPropertyInfo lineTotal, IPropertyInfo quantity, IPropertyInfo price)
        : base(lineTotal)
    {
        InputProperties.Add(quantity);
        InputProperties.Add(price);
    }

    protected override void Execute(IRuleContext context)
    {
        var quantity = (int)context.InputPropertyValues[InputProperties[0]]!;
        var price = (decimal)context.InputPropertyValues[InputProperties[1]]!;
        context.AddOutValue(PrimaryProperty!, quantity * price);
    }
}
```

```csharp
// Wrong in CSLA 11: rules that depend on LineTotal don't rerun during CheckRules()
protected override void Execute(IRuleContext context)
{
    var target = (LineItemEdit)context.Target!;
    target.LineTotal = target.Quantity * target.Price;
}
```

Other consequences:

* The rules of a rule's `AffectedProperties` don't rerun during `CheckRules()` unless a value changed. They already ran once in the batch. The affected properties are still in the returned list.
* The number of times each rule runs and the order they run in differ from CSLA 10. Don't write rules or tests that depend on a rule running a specific number of times during `CheckRules()`.

## Rules that never settle

With `CascadeOnDirtyProperties = true`, two rules whose output values keep changing each other used to recurse until the process crashed with a `StackOverflowException`. In CSLA 11, `CheckRules()` throws `InvalidOperationException` that names the properties still changing after `MaxRerunPasses` passes.

Fix the rules so they converge (for example, only call `AddOutValue` when the new value differs, or round consistently). Raise `MaxRerunPasses` only when a long chain of dependent calculations legitimately needs more passes.

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    BusinessRules.CascadeOnDirtyProperties = true;
    // ...
}
```

## Configuration

The batch runner is the default; no configuration is needed. To change the rerun limit:

```csharp
builder.Services.AddCsla(o => o
    .Rules(r => r.UseBatchCheckRules(b => b.MaxRerunPasses = 200)));   // default 100, must be >= 1
```

To keep the CSLA 10 behavior temporarily while updating rules that depend on it:

```csharp
builder.Services.AddCsla(o => o
    .Rules(r => r.UseLegacyCheckRules()));
```

* `RuleOptions.UseBatchCheckRules(Action<BatchCheckRulesOptions>?)` and `RuleOptions.UseLegacyCheckRules()` are in the `Csla.Configuration` namespace. If both are called, the last call wins.
* The legacy runner ignores `IsSequential` on async rules (see `SequentialAsyncRules.md`).
* `UseLegacyCheckRules()` will be removed in a future version.

## Upgrading rules from CSLA 10

Check for:

* Rules that depend on another property's rules having run first: set `Priority` so the order is explicit.
* Rules that set property values without `AddOutValue`: switch to `AddOutValue`, or dependent rules won't rerun in `CheckRules()`.
* `CascadeIfDirty = true` used to limit cascading in `CheckRules()`: no longer needed there; reruns already require a changed value.
* Unit tests that assert rule call counts or order after `CheckRules()`.
* Rule cycles with `CascadeOnDirtyProperties = true`: they now throw `InvalidOperationException` instead of overflowing the stack.

## Related

* `BusinessRules.md` - defining rules, priority and short-circuiting
* `SequentialAsyncRules.md` - async rules that run in priority order (`IsSequential`), and async completion order
* `CalculatedProperties.md` - `[CalculatedProperty]` for values set by rules
* `UpgradingToCsla11.md` - all CSLA 10 to 11 changes
