# Calculated Properties ([CalculatedProperty])

This document applies to CSLA 11. It describes `[CalculatedProperty]` (namespace `Csla`) for managed properties whose value is set by business rules from other property values, such as a total, a tax amount or a derived status.

## Summary for code generation

* Put `[CalculatedProperty]` on a managed property of a `BusinessBase<T>` type whose value is calculated by a business rule.
* Give it no setter or a `private` setter. The rule sets the value with `context.AddOutValue(...)`.
* A calculated property:
  * is not serialized (as if it had `[DoNotSerialize]`);
  * never changes `IsSelfDirty` or `IsDirty`, whether a rule, `SetProperty` or `LoadProperty` changes it. `PropertyChanged` is still raised and dependent rules still run;
  * is recalculated after `MobileFormatter` deserialization (for example after a remote data portal call or `Clone()`) by running the rules whose primary property is the calculated property. This happens after the whole graph is deserialized and after any `Deserialized()` overrides, so don't recalculate it yourself in a `Deserialized()` override.
* Don't call `MarkClean()` or `MarkOld()` after `CheckRules()` to undo the dirty state caused by calculated values. Use `[CalculatedProperty]`.
* Use it for simple values only, not child objects or collections, and only in `BusinessBase<T>` types.
* Works with hand-written properties (`[CalculatedProperty] public decimal Total => GetProperty(TotalProperty);`) and with `[CslaImplementProperties]` generated properties (`[CalculatedProperty] public partial decimal Total { get; private set; }`). Generated properties get a `TotalProperty` field to use in rules.
* Diagnostics: **CSLA0026** (calculated property has a setter that isn't `private`, including `init`), **CSLA0027** (`[CalculatedProperty]` outside a `BusinessBase` type).

## Basic example

```csharp
using Csla;
using Csla.Core;
using Csla.Rules;
using Csla.Rules.CommonRules;

[Serializable]
public partial class LineItemEdit : BusinessBase<LineItemEdit>
{
    public static readonly PropertyInfo<int> QuantityProperty = RegisterProperty<int>(nameof(Quantity));
    public int Quantity
    {
        get => GetProperty(QuantityProperty);
        set => SetProperty(QuantityProperty, value);
    }

    public static readonly PropertyInfo<decimal> PriceProperty = RegisterProperty<decimal>(nameof(Price));
    public decimal Price
    {
        get => GetProperty(PriceProperty);
        set => SetProperty(PriceProperty, value);
    }

    public static readonly PropertyInfo<decimal> LineTotalProperty = RegisterProperty<decimal>(nameof(LineTotal));
    [CalculatedProperty]
    public decimal LineTotal => GetProperty(LineTotalProperty);

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new CalcLineTotal(LineTotalProperty, QuantityProperty, PriceProperty));
        // Rerun the calculation when an input changes
        BusinessRules.AddRule(new Dependency(QuantityProperty, LineTotalProperty));
        BusinessRules.AddRule(new Dependency(PriceProperty, LineTotalProperty));
    }

    [CreateChild]
    private void CreateChild()
    {
        BusinessRules.CheckRules();
    }

    [FetchChild]
    private void FetchChild(LineItemData data)
    {
        using (BypassPropertyChecks)
        {
            Quantity = data.Quantity;
            Price = data.Price;
        }
        BusinessRules.CheckRules();   // calculates LineTotal; the object stays clean
    }
}

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

Setting `Quantity` marks the object dirty (as always) and recalculates `LineTotal`; the `LineTotal` change itself doesn't affect the dirty state. After the object crosses a remote data portal, `LineTotal` isn't in the payload; CSLA runs `CalcLineTotal` on the deserialized object.

## With generated properties

```csharp
[CslaImplementProperties]
public partial class LineItemEdit : BusinessBase<LineItemEdit>
{
    public partial int Quantity { get; set; }
    public partial decimal Price { get; set; }

    [CalculatedProperty]
    public partial decimal LineTotal { get; private set; }   // private setter: allowed (CSLA0026 only reports non-private)

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new CalcLineTotal(LineTotalProperty, QuantityProperty, PriceProperty));
        BusinessRules.AddRule(new Dependency(QuantityProperty, LineTotalProperty));
        BusinessRules.AddRule(new Dependency(PriceProperty, LineTotalProperty));
    }

    // [CreateChild]/[FetchChild] as above
}
```

## Parent totals from child values

A parent's calculated property can use its children's calculated values. After deserialization, children are recalculated before the objects that contain them.

```csharp
[Serializable]
public partial class OrderEdit : BusinessBase<OrderEdit>
{
    public static readonly PropertyInfo<LineItemList> LineItemsProperty = RegisterProperty<LineItemList>(nameof(LineItems));
    public LineItemList LineItems
    {
        get => GetProperty(LineItemsProperty)!;
        private set => LoadProperty(LineItemsProperty, value);
    }

    public static readonly PropertyInfo<decimal> OrderTotalProperty = RegisterProperty<decimal>(nameof(OrderTotal));
    [CalculatedProperty]
    public decimal OrderTotal => GetProperty(OrderTotalProperty);

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new CalcOrderTotal(OrderTotalProperty));
    }

    protected override void OnChildChanged(Csla.Core.ChildChangedEventArgs e)
    {
        base.OnChildChanged(e);
        BusinessRules.CheckRules(OrderTotalProperty);
    }

    private class CalcOrderTotal : BusinessRule
    {
        public CalcOrderTotal(IPropertyInfo primaryProperty) : base(primaryProperty) { }

        protected override void Execute(IRuleContext context)
        {
            var order = (OrderEdit)context.Target!;
            context.AddOutValue(PrimaryProperty!, order.LineItems.Sum(i => i.LineTotal));
        }
    }

    // [Create]/[Fetch]/[Insert]/[Update] omitted
}
```

## Wrong and right

```csharp
// Wrong: public setter (CSLA0026). The value would be lost on serialization and overwritten by rules.
[CalculatedProperty]
public decimal LineTotal
{
    get => GetProperty(LineTotalProperty);
    set => SetProperty(LineTotalProperty, value);
}

// Right
[CalculatedProperty]
public decimal LineTotal => GetProperty(LineTotalProperty);
```

```csharp
// Wrong in CSLA 11: undoing dirty state caused by calculated values
[Fetch]
private void Fetch(int id, [Inject] IOrderDal dal)
{
    // ... load values ...
    BusinessRules.CheckRules();
    MarkClean();
}

// Right: mark the calculated properties [CalculatedProperty] and just check rules
[Fetch]
private void Fetch(int id, [Inject] IOrderDal dal)
{
    // ... load values ...
    BusinessRules.CheckRules();
}
```

```csharp
// Wrong: CSLA0027. ReadOnlyBase (and CommandBase) don't recalculate after deserialization,
// so the attribute would only stop the value from being serialized.
public partial class OrderInfo : ReadOnlyBase<OrderInfo>
{
    [CalculatedProperty]
    public decimal Total => GetProperty(TotalProperty);
}

// Right: in a read-only object, load the value in Fetch (and keep it serialized),
// or use a plain computed property over the public properties
public decimal Total => Subtotal + Tax;
```

## `[CalculatedProperty]` vs. a plain computed property

| | `[CalculatedProperty]` managed property | Plain computed C# property |
|---|---|---|
| Declaration | `PropertyInfo<T>` + `GetProperty`, value set by a rule | `public decimal Total => Quantity * Price;` |
| Raises `PropertyChanged` | Yes, when the rule changes it | No (unless you raise it yourself) |
| Other rules can use it as input | Yes | No |
| Shows broken rules / can have validation rules | Yes | No |
| Serialized | No (recalculated after deserialization) | No (no backing field) |
| Affects dirty state | No | No |

Use a plain computed property for display-only formatting. Use `[CalculatedProperty]` when the value needs rules, change notification or validation.

## Related CSLA 11 changes

* **Async rules.** Async rules for a calculated property are started after deserialization but not awaited.
* **`[DoNotSerialize]` object values.** A managed property marked `[DoNotSerialize]` (or registered with `isSerializable: false`) that holds an object value, such as a `MobileList<T>` or a child object, is no longer serialized. Before, only native-type values were skipped. Values also stay excluded after `BeginEdit`/`CancelEdit`. If you relied on the object value being sent, remove `[DoNotSerialize]`.
* **`IPropertyInfo.IsCalculated`.** Custom `IPropertyInfo` implementations (not derived from `PropertyInfo<T>`) must add `bool IsCalculated { get; }`. Return `false` to keep the CSLA 10 behavior.

## Related

* `BusinessRules.md` - writing rules and `AddOutValue`
* `CheckRulesExecution.md` - how `CheckRules()` runs and reruns rules
* `Properties.md` - property declaration patterns
* `Analyzers.md` - CSLA0026, CSLA0027
