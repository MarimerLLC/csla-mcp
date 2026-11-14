# Sequential Async Rules and Async Rule Completion

This document applies to CSLA 11. It covers:

* `IsSequential`: making an async business rule run in priority order like a sync rule, so its result can stop later rules.
* The order of events when an async rule completes ("idle last"), and what that means for `WaitForIdle()`, `CheckRulesAsync()`, `IHostRules` and `CommandBase`.

## Summary for code generation

* By default an async rule (`BusinessRuleAsync`) starts in priority order and then runs in the background. Later rules don't wait for it, and its result can't short-circuit them.
* Set `IsSequential = true` on a `BusinessRuleAsync` when its result decides whether later rules should run, for example an existence check that is async only because it calls a command object.
* `IsSequential` is honored by the default batch rule runner only. `UseLegacyCheckRules()` ignores it.
* Set `IsSequential` when you create the rule, before it runs. Setting it after the rule has run throws `ArgumentException`.
* Custom rules that implement `IBusinessRuleBase` (or `IBusinessRule` / `IBusinessRuleAsync`) without inheriting `BusinessRuleBase` must add `bool IsSequential { get; }`: `true` for a sync rule, `false` for an async rule.
* After an async rule completes, the object reports idle **last**. Code after `await WaitForIdle()` or `await CheckRulesAsync()` runs after all completion notifications, always asynchronously.

## Declaring a sequential async rule

```csharp
public class CustomerExists : BusinessRuleAsync
{
    public CustomerExists(IPropertyInfo customerIdProperty)
        : base(customerIdProperty)
    {
        InputProperties.Add(customerIdProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        var customerId = (string?)context.InputPropertyValues[PrimaryProperty!];
        if (string.IsNullOrWhiteSpace(customerId))
            return;

        // Rules never call the database directly; use a command object
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<CustomerExistsCommand>>();
        var command = await portal.ExecuteAsync(customerId);
        if (!command.Exists)
            context.AddErrorResult("Customer not found");
    }
}
```

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    BusinessRules.AddRule(new Required(CustomerIdProperty));                                   // priority 0
    BusinessRules.AddRule(new CustomerExists(CustomerIdProperty) { Priority = 1, IsSequential = true });
    BusinessRules.AddRule(new CreditLimitCheck(OrderTotalProperty, CustomerIdProperty) { Priority = 2 });
}
```

`CreditLimitCheck` (any property, priority 2 or higher) waits until `CustomerExists` completes. If `CustomerExists` adds an `Error` result, the remaining `CustomerId` rules above `ProcessThroughPriority` don't run.

## How a sequential rule behaves

During `CheckRules()`, `CheckRules(property)` and `CheckObjectRules()`:

* **Waiting.** Rules after a sequential rule, for every property, wait until it completes. Before it starts, async rules that started earlier in the same run complete, and their results count.
* **Short-circuiting.** Its result short-circuits later rules like a sync rule's: an `Error` broken rule stops the rules of its property above `ProcessThroughPriority`, and `StopProcessing` stops them.
* **Output values.** Its `AddOutValue` values feed the `CheckRules()` rerun pass (see `CheckRulesExecution.md`), and its affected properties cascade in `CheckRules(property)`.
* **Busy state.** While it runs, its affected properties and the properties whose rules wait for it are busy, and the object stays busy until the rules after it have run. `WaitForIdle()`, `CheckRulesAsync()` and `ValidationComplete` wait for the whole run.
* **Sync `CheckRules()` returns early.** The synchronous `CheckRules()` returns when it reaches a sequential rule that is still running, with the properties affected so far. The remaining rules run on the thread that completes the rule and raise `PropertyChanged`. In a data portal operation, use `await BusinessRules.CheckRulesAsync()` so the operation doesn't return before all rules have run.
* **Superseded runs.** If a property's rules run again (the property is set, or `CheckRules()` is called) while a run waits for a sequential rule, the waiting run is discarded and the new run reruns the rules. A discarded `CheckRules()` or `CheckObjectRules()` run starts again after the new run.
* **Exceptions.** A sequential rule that throws completes with a broken rule (`RuleName:message`) that short-circuits like any error, instead of raising an unhandled exception.

An async rule that is not followed by a sequential rule completes in the background, as in CSLA 10.

## `CheckRulesAsync()` return value

With the batch runner, `CheckRulesAsync()` and `CheckRulesAsync(property)` return the affected and output properties of the async rules they started, and of the rules those completions ran, even with no sequential rules. The synchronous `CheckRules()` result is unchanged.

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] IOrderDal dal)
{
    var data = await dal.GetAsync(id);
    using (BypassPropertyChecks)
    {
        CustomerId = data.CustomerId;
        OrderTotal = data.OrderTotal;
    }
    await BusinessRules.CheckRulesAsync();   // waits for async and sequential rules
}
```

## Custom `IBusinessRuleBase` implementations

Rules that inherit `BusinessRule` or `BusinessRuleAsync` need no change. A type that implements the interfaces directly must add the property, or it won't compile against CSLA 11:

```csharp
public class MySyncRule : IBusinessRule
{
    public bool IsSequential => true;   // sync rule
    // ... other IBusinessRule members
}

public class MyAsyncRule : IBusinessRuleAsync
{
    public bool IsSequential => false;  // async rule, background (CSLA 10 behavior)
    // ... other IBusinessRuleAsync members
}
```

## Async rule completion reports idle last

When an async rule completes, the object raises its notifications in this order:

1. `PropertyChanged` for the rule's properties.
2. `PropertyChanged` for `IsSelfValid`, `IsValid` and `IsSavable`.
3. `ValidationComplete`.
4. For each property that is now idle: `PropertyChanged` for `IsSavable`, then `IsSelfBusy` and `IsBusy`, then `BusyChanged(property, false)`.

Rules for your code:

* Handlers for `PropertyChanged`, `ValidationComplete` and the `IsValid`/`IsSavable` changes see `IsBusy == true` and `IsPropertyBusy(property) == true`. Don't treat "busy" inside those handlers as "a rule is still running".
* Code after `await obj.WaitForIdle()` or `await BusinessRules.CheckRulesAsync()` runs after every completion notification, and always continues asynchronously (never inline on the thread that completed the rule). It is safe to dispose a DI scope there.
* Don't dispose services the object uses from inside `BusyChanged`, `IsBusy` or `IsSavable` change handlers; those are raised after `IsBusy` turns `false` but are still part of completion.
* `WaitForIdle()` covers the object's child objects. If a completion handler starts rules on an object outside that graph, wait for that object separately.
* A parent `BusinessBase` raises `PropertyChanged` for `IsSavable` whenever a child's busy state changes.

### `IHostRules`

`IHostRules.RuleComplete(IPropertyInfo)` no longer raises `BusyChanged`. The new `PropertyIdle(IPropertyInfo)` is called when no async rules are running for the property. If you implement `IHostRules` yourself (rare; `BusinessBase` already does), add it and raise your busy notification there:

```csharp
// In a custom IHostRules implementation (BusinessBase does the equivalent)
void IHostRules.PropertyIdle(IPropertyInfo property)
{
    ArgumentNullException.ThrowIfNull(property);
    BusyChanged?.Invoke(this, new BusyChangedEventArgs(property.Name, false));
}

void IHostRules.RuleComplete(IPropertyInfo property)
{
    // Raise PropertyChanged for the property here; don't raise BusyChanged
}
```

### `CommandBase` waits for its child objects

After a create, fetch or execute operation, the server-side data portal waits until the objects a `CommandBase` holds in **managed properties** are idle, then returns the command. A child with a long-running async rule makes the command take that long, and fails with `TimeoutException` after `CslaOptions.DefaultWaitForIdleTimeoutInSeconds` (default 90).

An object the command keeps in a private field isn't covered; await its `WaitForIdle()` in your operation method:

```csharp
[Execute]
private async Task Execute([Inject] IDataPortal<OrderEdit> portal)
{
    _order = await portal.FetchAsync(OrderId);   // private field, not a managed property
    await _order.WaitForIdle();
}
```

## Related

* `CheckRulesExecution.md` - batch `CheckRules()` order and reruns
* `BusinessRules.md` - defining sync and async rules
* `AsyncRuleExceptionHandler.md` - handling exceptions from background async rules
* `UpgradingToCsla11.md` - all CSLA 10 to 11 changes
