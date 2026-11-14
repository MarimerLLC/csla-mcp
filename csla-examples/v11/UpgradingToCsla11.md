# Upgrading from CSLA 10 to CSLA 11

This document applies to CSLA 11. It is a practical checklist for upgrading code written for CSLA 10 to CSLA 11, covering every breaking change with before and after code. If you are upgrading from a version earlier than CSLA 10, first apply the CSLA 10 upgrade guidance, then this one.

## Upgrade checklist

1. Retarget to .NET 10 or .NET 11 (or keep .NET Framework 4.6.2-4.8 / `netstandard2.0`). [Details](#target-frameworks)
2. Update all `Csla*` package references to 11.x, and remove `Csla.Generator.AutoImplementProperties.CSharp` (build error `CSLABUILD001`). [Details](#retired-generator-package-cslabuild001)
3. Add an operation attribute to every `DataPortal_XYZ` / `Child_XYZ` method that lacks one (CSLA0014 code fix, plus a manual search for `...Async` names). **This is the most important step.** [Details](#attribute-only-data-portal-operations)
4. Declare business classes with operation methods `partial` (CSLA0024 code fix). [Details](#business-classes-should-be-partial-csla0024)
5. Replace `new LazySingleton<T>()` with `new LazySingleton<T>(() => ...)`. [Details](#lazysingletont-parameterless-constructor-removed)
6. Move `AllowNew = false` / `AllowRemove = false` after loading list items, and stop adding or removing items while they are `false`. [Details](#allownew-and-allowremove-are-enforced)
7. Review code that reads `TransactionalAttribute` values, constructs `TransactionalDataPortal`, or changes `DataOptions` transaction defaults. [Details](#transaction-changes)
8. If you use async per-type authorization rules, review `IsSavable` and save logic. [Details](#issavable-and-async-authorization-rules)
9. Optional: adopt strongly typed data portal calls with `[DataPortalExtensions]`. [Details](#optional-strongly-typed-data-portal-calls)
10. Only if the app is trimmed or compiled with NativeAOT: follow the trimming and NativeAOT guide. [Details](#only-if-you-use-trimming-or-nativeaot)
11. If a Blazor app uses state management (`UseInMemoryApplicationContextManager = false`), check `SessionExpiration`: sessions idle for 1 hour are now purged automatically. [Details](#blazor-sessions-are-purged-automatically)
12. Check rules that depend on `CheckRules()` order or rerun behavior: priority now applies across all properties, and reruns only follow output values. `UseLegacyCheckRules()` restores CSLA 10 behavior temporarily. [Details](#checkrules-runs-rules-as-a-batch)
13. Add `IsSequential` to custom `IBusinessRuleBase` implementations that don't inherit `BusinessRuleBase`. Optionally set `IsSequential = true` on async rules that later rules depend on. [Details](#sequential-async-rules)
14. Review handlers for `PropertyChanged`, `ValidationComplete` and `BusyChanged`, code after `await WaitForIdle()`, and custom `IHostRules` implementations (add `PropertyIdle`). [Details](#async-rule-completion-reports-idle-last)
15. Replace `MarkClean()`/`MarkOld()` calls that undo dirtiness from rule-calculated values with `[CalculatedProperty]`; add `IsCalculated` to custom `IPropertyInfo` implementations; check `[DoNotSerialize]` properties that hold objects. [Details](#calculated-properties-and-donotserialize)
16. Pass a factory to `LazyGetPropertyAsync` / `LazyReadPropertyAsync` instead of a `Task<P>` (CS0618), and drop the loaded/loading guard. [Details](#lazygetpropertyasync-and-lazyreadpropertyasync-take-a-factory)
17. Fix new CSLA0007 warnings on getters like `=> GetProperty(P).ToUpper()`, and remove CSLA0007 suppressions on `GetProperty(P)!` getters. [Details](#csla0007-reports-more-property-getters)
18. Optional: declare `MobileFormatter` custom serializers with attributes so CSLA0010 stops reporting their types. [Details](#custom-serializers-can-be-declared-with-attributes)
19. Replace `IsDbNullAsync` with `IsDBNullAsync`, and recompile code that uses `SafeSqlDataReader`. [Details](#async-safedatareader)
20. ASP.NET Core MVC / Razor Pages: check controller code that relies on the old `CslaModelBinder` behavior. [Details](#aspnet-core-cslamodelbinder-binds-whole-object-graphs)
21. Move `OnGetState`/`OnSetState` overrides that take a `StateMode` to the `SerializationInfo`-only overloads. [Details](#statemode-is-obsolete)
22. Remove `MobileReader<T>()` / `MobileWriter<T>()` configuration that names the removed `CslaXml*` readers and writers. [Details](#cslaxml-readers-and-writers-removed)
23. View models: XAML `SaveAsync`/`RefreshAsync` no longer throw; Blazor `OnModelChanged()` overrides take `(T? oldValue, T? newValue)`; `ManageObjectLifetime` defaults to `true` on MAUI and Avalonia. [Details](#view-models)
24. Stop using the CSLA `dotnet new` templates and the Visual Studio template installer; they are removed. [Details](#other-removals)
25. Projects that reference `Csla.Analyzers.dll` directly by path: add `Csla.Analyzers.CodeFixes.dll`. [Details](#analyzer-changes)

## Target frameworks

CSLA 11 targets .NET 10 and .NET 11. It no longer targets .NET 8 or .NET 9. The `netstandard2.0` and .NET Framework 4.6.2, 4.7.2 and 4.8 targets are unchanged.

| Package | CSLA 11 targets |
|---|---|
| `Csla` | `netstandard2.0`, `net462`, `net472`, `net48`, `net10.0`, `net11.0` |
| `Csla.AspNetCore`, `Csla.Blazor`, ... | `net10.0`, `net11.0` |
| `Csla.Maui` | `net10.0-*` and `net11.0-*` (android, ios, maccatalyst, windows) |

Before:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Csla" Version="10.2.0" />
</ItemGroup>
```

After:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Csla" Version="11.0.0" />
</ItemGroup>
```

On .NET 11, Android apps that use `Csla.Maui` need Android API level 24 or later (a .NET 11 for Android requirement). The `net10.0-android` target still supports API level 21.

## Retired generator package (CSLABUILD001)

The source generators that were in `Csla.Generator.AutoImplementProperties.CSharp` are now bundled in the `Csla` package and run automatically in every C# project that references `Csla`. The separate package is retired. While a project still references it, the build fails with:

```text
error CSLABUILD001: The Csla.Generator.AutoImplementProperties.CSharp package is retired. Starting with CSLA 11 its source generators are included in the Csla package. Remove the PackageReference to Csla.Generator.AutoImplementProperties.CSharp (and any matching PackageVersion entry).
```

Before:

```xml
<ItemGroup>
  <PackageReference Include="Csla" Version="10.2.0" />
  <PackageReference Include="Csla.Generator.AutoImplementProperties.CSharp" Version="10.2.0" />
</ItemGroup>
```

After:

```xml
<ItemGroup>
  <PackageReference Include="Csla" Version="11.0.0" />
</ItemGroup>
```

With central package management, also remove the `<PackageVersion Include="Csla.Generator.AutoImplementProperties.CSharp" ... />` line from `Directory.Packages.props`.

No code changes are needed: `[CslaImplementProperties]`, `[CslaImplementPropertiesInterface<T>]` and `[CslaIgnoreProperty]` are now in `Csla.dll`, still in the `Csla` namespace.

## Attribute-only data portal operations

In CSLA 10, if the data portal couldn't find a method with an operation attribute, it fell back to finding a method by name (`DataPortal_Fetch`, `Child_Update`, ...), controlled by `DataPortalOptions.UseLegacyOperationMethods`. **CSLA 11 removes this fallback.** The data portal only calls methods that have an operation attribute, and the `UseLegacyOperationMethods` option is gone (delete any code that sets it).

### Legacy name to attribute mapping

| Legacy method name | Attribute to add |
|---|---|
| `DataPortal_Create` | `[Create]` |
| `DataPortal_Fetch` | `[Fetch]` |
| `DataPortal_Insert` | `[Insert]` |
| `DataPortal_Update` | `[Update]` |
| `DataPortal_Delete` | `[Delete]` |
| `DataPortal_DeleteSelf` | `[DeleteSelf]` |
| `DataPortal_Execute` | `[Execute]` |
| `Child_Create` | `[CreateChild]` |
| `Child_Fetch` | `[FetchChild]` |
| `Child_Insert` | `[InsertChild]` |
| `Child_Update` | `[UpdateChild]` |
| `Child_DeleteSelf` | `[DeleteSelfChild]` |
| `Child_Execute` | `[ExecuteChild]` |

The same mapping applies to the `Async` forms of these names (for example `DataPortal_FetchAsync` needs `[Fetch]`, `Child_UpdateAsync` needs `[UpdateChild]`).

Before (CSLA 10, found by name):

```csharp
public class Customer : BusinessBase<Customer>
{
  private void DataPortal_Fetch(int id)
  {
    // ...
  }

  private void DataPortal_Insert()
  {
    // ...
  }
}
```

After (CSLA 11):

```csharp
public partial class Customer : BusinessBase<Customer>
{
  [Fetch]
  private void DataPortal_Fetch(int id)
  {
    // ...
  }

  [Insert]
  private void DataPortal_Insert()
  {
    // ...
  }
}
```

Once a method has an attribute its name no longer matters, so you can rename it (for example to `Fetch` and `Insert`).

### Finding affected methods (CSLA0014)

Analyzer **CSLA0014** reports methods in CSLA types that have a legacy operation name but no operation attribute. In CSLA 11 it is a **warning** (it was informational in CSLA 10), and its code fix adds the correct attribute. Run the code fix with "Fix all occurrences in Solution".

CSLA0014 matches the exact legacy names only (`DataPortal_Fetch`, `Child_Update`, ...). It does **not** report the `Async` forms such as `DataPortal_FetchAsync` or `Child_UpdateAsync`. Search for those yourself, for example with the regex `(DataPortal|Child)_\w+Async\s*\(` and add the attribute by hand.

A method that overrides a base class method inherits the base method's attribute, so it does not need its own (and CSLA0014 doesn't report it).

### Pitfall: silently skipped methods

A legacy-named method without an attribute is never called. Usually the data portal then throws at runtime because no matching operation exists. But **when a CSLA base class has its own operation method, the base method runs instead and your method is silently skipped**:

**Child create.** `BusinessBase`, `BusinessListBase` and `BusinessBindingListBase` have a `protected virtual void Child_Create()` that is now marked `[CreateChild]`. A child class with its own attribute-less `Child_Create()` gets the base version, which only checks business rules, so your default values are never set:

```csharp
// CSLA 10: called by name. CSLA 11: NEVER called; BusinessBase.Child_Create() runs instead.
private void Child_Create()
{
  LoadProperty(QuantityProperty, 1);
  BusinessRules.CheckRules();
}

// CSLA 11: add the attribute (or override the base method instead)
[CreateChild]
private void Child_Create()
{
  LoadProperty(QuantityProperty, 1);
  BusinessRules.CheckRules();
}

// Also correct: an override inherits [CreateChild] from the base method
protected override void Child_Create()
{
  LoadProperty(QuantityProperty, 1);
  base.Child_Create();   // checks rules
}
```

The base `Child_Create()` is still called when you create a child with no criteria and the class has no better matching `[CreateChild]` method, so a child with no create method of its own still gets its rules checked.

**Child list update.** A list that overrides the synchronous `Child_Update(params object[])` without `[UpdateChild]` is skipped. The data portal calls the base `Child_UpdateAsync` (which is marked `[UpdateChild]`) and updates the child items, but nothing else your override did:

```csharp
// CSLA 11: this override is NOT called by the data portal
protected override void Child_Update(params object[] parameters)
{
  WriteAuditRecord();
  base.Child_Update(parameters);
}

// Fix 1: mark the override
[UpdateChild]
protected override void Child_Update(params object[] parameters)
{
  WriteAuditRecord();
  base.Child_Update(parameters);
}

// Fix 2: override the async method instead (it inherits [UpdateChild])
protected override async Task Child_UpdateAsync(params object[] parameters)
{
  WriteAuditRecord();
  await base.Child_UpdateAsync(parameters);
}
```

### Sync Child_Update helper

The synchronous `Child_Update(params object[])` helper on `BusinessListBase`, `BusinessBindingListBase` and `BusinessDocumentBase` has no operation attribute. You can still call it from your own synchronous `[Update]`, `[Insert]` or `[DeleteSelf]` methods of a root list:

```csharp
[Update]
private void Update()
{
  Child_Update();   // still fine: a normal method call
}
```

The data portal itself calls `Child_UpdateAsync` (marked `[UpdateChild]`) when it updates a child list.

### Other base class changes

* **"Not supported" operations removed.** `CommandBase`, `ReadOnlyBase`, `ReadOnlyListBase`, `ReadOnlyBindingListBase`, `NameValueListBase`, `DynamicListBase` and `DynamicBindingListBase` no longer have private `DataPortal_XYZ` methods that threw `NotSupportedException` (for example when updating a read-only object). Those calls now fail with the data portal's normal "method not found" error. Code that caught `NotSupportedException` for these cases must handle the data portal exception instead.
* **ObjectFactory child create.** Creating a child of a type that uses `[ObjectFactory]` no longer looks for a method named `Child_Create` on the factory or the business class. The data portal calls the business class's `[CreateChild]` method (or the base class default described above).

## Business classes should be partial (CSLA0024)

The `Csla` package now includes a source generator that lets the data portal call operation methods directly instead of through reflection. It can only do that for classes that are `partial`, along with every containing type. This is not a compile-time break: non-partial classes still work through reflection. But analyzer **CSLA0024** (warning) reports them, and its code fix adds `partial`.

Before:

```csharp
[CslaImplementProperties]
public partial class CustomerEdit : BusinessBase<CustomerEdit> { /* already partial */ }

public class OrderEdit : BusinessBase<OrderEdit>
{
  [Fetch]
  private void Fetch(int id) { }
}
```

After:

```csharp
public partial class OrderEdit : BusinessBase<OrderEdit>
{
  [Fetch]
  private void Fetch(int id) { }
}
```

Benefits: no reflection on the server, private operation methods are no longer flagged as unused by IDEs, and the trimmer keeps them. Also watch for **CSLADP002** / **CSLADP009**, which report operation methods that map to the same operation name. See [DataPortalOperationDispatch.md](DataPortalOperationDispatch.md).

## LazySingleton<T> parameterless constructor removed

The parameterless constructor of `Csla.LazySingleton<T>` is removed because it never worked: reading `Value` threw `NullReferenceException`, and DI creation failed. `LazySingleton<T>` also no longer implements `IUseApplicationContext`. The only constructor is `LazySingleton(Func<T>)`.

Before:

```csharp
var lazy = new LazySingleton<Dictionary<string, object>>();
```

After:

```csharp
var lazy = new LazySingleton<Dictionary<string, object>>(() => new Dictionary<string, object>());
```

If the value needs DI services, create it in the delegate, for example `new LazySingleton<MyType>(() => (MyType)applicationContext.CreateInstanceDI(typeof(MyType)))`. Or use `System.Lazy<T>`, which covers the same use case.

## AllowNew and AllowRemove are enforced

`BusinessListBase`, `BusinessBindingListBase`, `DynamicListBase` and `DynamicBindingListBase` now enforce their `AllowNew` and `AllowRemove` properties. Previously these only informed data binding.

* `AllowNew == false`: `AddNew`, `AddNewAsync`, `Add`, `AddRange` and `Insert` throw `NotSupportedException`.
* `AllowRemove == false`: `Remove`, `RemoveAt`, `Clear` and `RemoveItemAsync` throw `NotSupportedException`. For `DynamicBindingListBase` the exception is thrown before the item is deleted from the database.
* Replacing an item through the indexer (`list[i] = item`) is still allowed.
* N-level undo, deserialization and graph merging still add and remove items as needed.
* `AllowRemove` now defaults to `true` for `BusinessListBase` and `DynamicListBase` (as it already did for the binding list types).
* `AllowNew` now keeps its value when the data portal returns the list. In CSLA 10 it was reset to `true` when the list's `ApplicationContext` was set, so setting it in a fetch method had no lasting effect.

The setters are `protected`, so these properties are set inside the list class. If your fetch code sets them to `false` before loading items, it now throws. Move the assignment after the items are loaded.

Before (works in CSLA 10, throws in CSLA 11):

```csharp
[Fetch]
private void Fetch([Inject] IChildDataPortal<LineItem> childPortal, [Inject] ILineItemDal dal)
{
  AllowNew = false;
  AllowRemove = false;
  using (LoadListMode)
  {
    foreach (var data in dal.Fetch())
      Add(childPortal.FetchChild(data));   // NotSupportedException
  }
}
```

After:

```csharp
[Fetch]
private void Fetch([Inject] IChildDataPortal<LineItem> childPortal, [Inject] ILineItemDal dal)
{
  using (LoadListMode)
  {
    foreach (var data in dal.Fetch())
      Add(childPortal.FetchChild(data));
  }
  AllowNew = false;
  AllowRemove = false;
}
```

Also check UI and business code that adds or removes items on such lists (for example a "Delete row" button bound to a read-only grid): it now gets an exception instead of silently succeeding.

## Transaction changes

See [TransactionScopeFactory.md](TransactionScopeFactory.md) for the full transaction documentation. The breaking changes are:

### TransactionalAttribute values are resolved at runtime

In CSLA 10, the `TransactionalAttribute` constructor copied the `DataOptions` defaults into the attribute. Because CSLA caches attributes, later configuration changes were ignored. In CSLA 11 the attribute keeps unspecified values unspecified, and they are resolved from `DataOptions` when the transaction is created.

| Property | CSLA 10 | CSLA 11 |
|---|---|---|
| `TransactionIsolationLevel` | `TransactionIsolationLevel`, copied from `DataOptions` if not given | `TransactionIsolationLevel`, `Unspecified` if not given |
| `TimeoutInSeconds` | `int`, copied from `DataOptions` | `int?`, `null` if not given |
| `AsyncFlowOption` | `TransactionScopeAsyncFlowOption`, copied from `DataOptions` | `TransactionScopeAsyncFlowOption?`, `null` if not given |

Usage of the attribute on methods does not change. Only code that **reads** these properties must handle `null`:

```csharp
// Before
TimeSpan timeout = TimeSpan.FromSeconds(attribute.TimeoutInSeconds);
var flow = attribute.AsyncFlowOption;

// After
TimeSpan timeout = TimeSpan.FromSeconds(attribute.TimeoutInSeconds ?? dataOptions.DefaultTransactionTimeoutInSeconds);
var flow = attribute.AsyncFlowOption ?? dataOptions.DefaultTransactionAsyncFlowOption;
```

### DataOptions defaults are per instance

`DataOptions.DefaultTransactionIsolationLevel`, `DefaultTransactionTimeoutInSeconds` and `DefaultTransactionAsyncFlowOption` were stored in static fields shared by every `CslaOptions` instance. They are now ordinary per-instance properties. Set them through `AddCsla` configuration on each host that needs them; one host's settings no longer leak into another (for example between test hosts).

```csharp
services.AddCsla(o => o
  .Data(d => d.DefaultTransactionIsolationLevel = TransactionIsolationLevel.ReadCommitted));
```

### ITransactionScopeFactory and the TransactionalDataPortal constructor

The server-side data portal now gets its `TransactionScope` from an `ITransactionScopeFactory` service (default `DefaultTransactionScopeFactory`). Register your own with `AddServerSideDataPortal(s => s.RegisterTransactionScopeFactory<T>())` to control timeouts or isolation per operation.

The `TransactionalDataPortal` constructor changed. Only code that constructs it directly (custom data portal servers, some tests) is affected:

```csharp
// Before
var portal = new TransactionalDataPortal(broker, transactionalAttribute);

// After
var context = new TransactionScopeFactoryContext(objectType, DataPortalOperations.Fetch, methodInfo, transactionalAttribute);
var portal = new TransactionalDataPortal(broker, transactionScopeFactory, context);
```

### EnterpriseServices isolation level (.NET Framework only)

With `TransactionalTypes.EnterpriseServices`, an unspecified isolation level now uses `DataOptions.DefaultTransactionIsolationLevel`, and `Serializable` if that is also unspecified. CSLA 10 threw `ArgumentOutOfRangeException` in this case.

## IsSavable and async authorization rules

In CSLA 11, `IsSavable` on `BusinessBase`, `BusinessListBase` and `BusinessBindingListBase` evaluates the per-type authorization rule (`CreateObject`, `EditObject` or `DeleteObject`) through `HasPermissionAsync`, so truly asynchronous rules (`IAuthorizationRuleAsync`, for example a subclass of `AuthorizationRuleAsync` that awaits a service) are supported. This changes behavior only when such a rule does not complete synchronously:

* **Sync rules, or async rules that complete synchronously:** unchanged. They are evaluated on every read of `IsSavable` and never make the object busy.
* **Async rule still running:** the first read of `IsSavable` starts the rule, **marks the object busy** (raising `BusyChanged` synchronously, from inside the `IsSavable` getter) and returns `false`.
* **When the rule completes:** the result is cached for that action and user, the object is marked idle, and a property change for `IsSavable` is raised. Later reads return the cached result. The cache is reset when the action (new, existing or deleted) or the user changes, but not on other property edits.
* **Rule fails:** `IsSavable` is `false` and the object raises `UnhandledAsyncException`.
* **Saving while the check is running** throws `InvalidOperationException` with the `BusyObjectsMayNotBeSaved` message ("Objects that are marked busy may not be saved"), the same as when async business rules are running.

```csharp
public class CanEditCustomerRule() : AuthorizationRuleAsync(AuthorizationActions.EditObject)
{
  protected override async Task ExecuteAsync(IAuthorizationContext context, CancellationToken ct)
  {
    var service = context.ApplicationContext.GetRequiredService<IPermissionService>();
    context.HasPermission = await service.CanEditCustomersAsync(context.ApplicationContext.User, ct);
  }
}
```

What to check in your code:

* Code that handles `BusyChanged` or `PropertyChanged` must tolerate events raised while reading `IsSavable`.
* Code that reads `IsSavable` and then immediately calls `Save()`/`SaveAsync()` should wait for the object to be idle first:

```csharp
if (!customer.IsSavable && customer.IsBusy)
  await customer.WaitForIdle();
if (customer.IsSavable)
  customer = await customer.SaveAsync();
```

* The XAML `ViewModelBase` and `ObjectStatus`, the Blazor `ViewModel` (its `SaveAsync` waits for the object to become idle), and `WaitForIdle()` already handle the busy state, so data-bound UIs need no changes.

Related changes from the same work:

* `ViewModelBase`, `ObjectStatus` and the Blazor `ViewModel` evaluate per-type and per-instance authorization with `HasPermissionAsync`, so async authorization rules no longer crash them. With synchronous rules the values are still set before the calling method returns.
* The synchronous `BusinessRules.HasPermission` now runs async authorization rules that complete synchronously. For an async rule that does not complete synchronously it throws `InvalidOperationException` telling you to use `BusinessRules.HasPermissionAsync` (CSLA 10 threw `ArgumentOutOfRangeException` for any async rule).

## Optional: strongly typed data portal calls

CSLA 11 can generate strongly typed extension methods for `IDataPortal<T>` and `IChildDataPortal<T>`. This is opt-in and not required for the upgrade:

```csharp
[DataPortalExtensions(Prefix = "Portal")]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  [Fetch]
  private async Task Fetch(int id, [Inject] IPersonDal dal) { /* ... */ }
}

// Before
var person = await portal.FetchAsync(42);
// After (compiler-checked criteria)
var person = await portal.PortalFetchAsync(42);
```

Once enabled, analyzer CSLA0025 (warning) flags untyped calls that a generated method can replace, with a code fix. See [DataPortalExtensions.md](DataPortalExtensions.md). If you used Stefan Ossendorf's `Csla.DataPortalExtensions` package, switch to the built-in generator.

## Blazor sessions are purged automatically

When Blazor state management is on (`UseInMemoryApplicationContextManager = false`), the server keeps each user's session in the `ISessionStore` until something calls `ISessionManager.PurgeSessions`. In CSLA 10 the app had to schedule that call itself, and sessions were never removed otherwise. In CSLA 11, `AddServerSideBlazor` registers a hosted background service that does it, on by default, and **removes sessions that haven't been touched for 1 hour**:

```csharp
// Before: app-specific timer or hosted service
sessionManager.PurgeSessions(TimeSpan.FromHours(1));

// After: nothing to write; change the defaults if needed
builder.Services.AddCsla(o => o
  .AddAspNetCore()
  .AddServerSideBlazor(b =>
  {
    b.UseInMemoryApplicationContextManager = false;
    b.SessionPurgeInterval = TimeSpan.FromMinutes(5);  // default: 5 minutes
    b.SessionExpiration = TimeSpan.FromHours(1);       // default: 1 hour
    // b.PurgeExpiredSessions = false;                 // to keep purging yourself
  }));
```

* If users can be idle for more than an hour and should keep their session, raise `SessionExpiration`.
* If the app already calls `PurgeSessions` on a schedule, remove that code or set `PurgeExpiredSessions = false`. Running both is harmless, but the shorter expiration wins.
* `SessionPurgeInterval` must be from 1 millisecond to about 49.7 days, and `SessionExpiration` must be greater than zero, or `AddServerSideBlazor` throws `ArgumentOutOfRangeException`.

See "Purging Expired Sessions" in [BlazorConfiguration.md](../BlazorConfiguration.md#purging-expired-sessions-csla-11) for how the service behaves.

## `CheckRules()` runs rules as a batch

`BusinessRules.CheckRules()` and `CheckRulesAsync()` now:

1. run object-level rules first;
2. run every property rule once, in priority order **across all properties** (CSLA 10 only ordered by priority within one property);
3. rerun only the rules of properties changed through `context.AddOutValue`, and the rules that use those properties as inputs; with `CascadeOnDirtyProperties = true`, repeat until nothing changes, and throw `InvalidOperationException` after `MaxRerunPasses` passes (default 100) instead of overflowing the stack.

What breaks:

* A rule that changes another property by writing to the target object (not `AddOutValue`) no longer triggers the dependent rules during `CheckRules()`. Change it to use `context.AddOutValue`.
* Rules of `AffectedProperties` don't rerun on their own in `CheckRules()`.
* `CascadeIfDirty` has no effect in `CheckRules()` (it still applies to `CheckRules(property)`).
* Tests that assert how many times a rule ran, or in what order across properties, need updating.

`CheckRules(property)`, the rules that run when a property is set, and `CheckObjectRules()` are unchanged.

```csharp
// Configure the batch runner (default)
services.AddCsla(o => o.Rules(r => r.UseBatchCheckRules(b => b.MaxRerunPasses = 200)));

// Temporarily keep the CSLA 10 behavior; will be removed in a future version
services.AddCsla(o => o.Rules(r => r.UseLegacyCheckRules()));
```

See [CheckRulesExecution.md](CheckRulesExecution.md).

## Sequential async rules

`IBusinessRuleBase` has a new `IsSequential` property. Rules that inherit `BusinessRuleBase` (`BusinessRule`, `BusinessRuleAsync`, the built-in rules) already have it. A custom implementation of `IBusinessRuleBase`, `IBusinessRule` or `IBusinessRuleAsync` that doesn't inherit `BusinessRuleBase` must add it:

```csharp
public bool IsSequential => !IsAsync;   // true for a sync rule, false for an async rule
```

Optional: set `IsSequential = true` on a `BusinessRuleAsync` so later rules (for every property) wait for it and short-circuit on its result, like a sync rule:

```csharp
BusinessRules.AddRule(new CustomerExists(CustomerIdProperty) { Priority = 1, IsSequential = true });
```

With the batch runner, `CheckRulesAsync()` and `CheckRulesAsync(property)` also return the properties affected by the async rules they started. See [SequentialAsyncRules.md](SequentialAsyncRules.md).

## Async rule completion reports idle last

When an async rule completes, the object now raises, in this order: `PropertyChanged` for the rule's properties, the `IsSelfValid`/`IsValid`/`IsSavable` changes, `ValidationComplete`, and only then `BusyChanged(property, false)` and the `IsSelfBusy`/`IsBusy` changes.

What to check:

* Handlers of `PropertyChanged`, `ValidationComplete` and `IsValid` changes now see `IsBusy == true` and `IsPropertyBusy(property) == true` for the completing rule.
* Code after `await WaitForIdle()` or `await CheckRulesAsync()` always continues asynchronously, after all completion notifications. It is now safe to dispose a DI scope there. Don't dispose resources the object uses from inside the idle notification handlers.
* Custom `IHostRules` implementations: `RuleComplete(IPropertyInfo)` no longer raises `BusyChanged`. Add `PropertyIdle(IPropertyInfo)` and raise your busy notification there.
* A `CommandBase` returned by a create, fetch or execute waits on the server until the objects in its managed properties are idle (fails with `TimeoutException` after `DefaultWaitForIdleTimeoutInSeconds`). Objects held in private fields aren't covered; await them in the operation method.

## Calculated properties and `[DoNotSerialize]`

Replace this CSLA 10 workaround:

```csharp
// CSLA 10: calculated values made a freshly fetched object dirty
[Fetch]
private void Fetch(int id, [Inject] IOrderDal dal)
{
  // ... load properties
  BusinessRules.CheckRules();
  MarkOld();   // or MarkClean(), only to undo dirtiness from calculated values
}
```

with `[CalculatedProperty]` on the rule-calculated property, and remove the extra `MarkOld()`/`MarkClean()`:

```csharp
[CalculatedProperty]
public partial decimal Total { get; private set; }   // CSLA0026: no setter or a private setter
```

A calculated property is not serialized, never marks the object dirty, and is recalculated by its rules after deserialization. Only in `BusinessBase` types (CSLA0027). See [CalculatedProperties.md](CalculatedProperties.md).

Also:

* `IPropertyInfo` has a new `IsCalculated` member. Custom `IPropertyInfo` implementations (not derived from `PropertyInfo<T>`) must add it; return `false` to keep CSLA 10 behavior.
* A `[DoNotSerialize]` property (or one registered with `isSerializable: false`) that holds an object value, such as a child object or `MobileList<T>`, is no longer serialized, and stays excluded after `CancelEdit`. Remove the attribute if the value must reach the other side of the data portal.

## `LazyGetPropertyAsync` and `LazyReadPropertyAsync` take a factory

The overloads that take a `Task<P>` are obsolete (CS0618). Pass a `Func<Task<P>>`; it only runs when the value isn't loaded and isn't loading, so the `FieldManager.FieldExists` / `PropertyIsLoading` guard and its CSLA0007 suppression go away:

```csharp
// CSLA 10
public ShipmentInfo? Shipment
{
  get
  {
    if (FieldManager.FieldExists(ShipmentProperty) || PropertyIsLoading(ShipmentProperty))
      return GetProperty(ShipmentProperty);
    return LazyGetPropertyAsync(ShipmentProperty,
      ApplicationContext.GetRequiredService<IChildDataPortal<ShipmentInfo>>().FetchChildAsync(Id));
  }
}

// CSLA 11
public ShipmentInfo? Shipment =>
  LazyGetPropertyAsync(ShipmentProperty,
    () => ApplicationContext.GetRequiredService<IChildDataPortal<ShipmentInfo>>().FetchChildAsync(Id));
```

* Passing `null` as the second argument no longer compiles (CS0121).
* A factory (or `Task<P>`) that fails raises `UnhandledAsyncException` and the getter returns the default value; the next read tries again. It no longer throws `InvalidOperationException` from the getter.

See [NullableProperties.md](NullableProperties.md).

## CSLA0007 reports more property getters

* **New warnings:** expression-bodied getters that call a property method inside another call, such as `public string Name => GetProperty(NameProperty).ToUpper();`. Move the logic into a separate plain property or a business rule, or suppress with a justification. Builds with `TreatWarningsAsErrors` fail until fixed.
* **Fewer warnings:** `get => GetProperty(NameProperty)!;` is no longer reported. Remove those suppressions.
* The warning is reported on the accessor's expression or body rather than the whole accessor, so a `[SuppressMessage]` scoped to the old location may need moving.
* Generated `[CslaImplementProperties]` properties are no longer wrapped in `#pragma warning disable CSLA0007`; they don't produce warnings.

## Custom serializers can be declared with attributes

Optional. Existing `CustomSerializers.Add(new TypeMap<T, S>(...))` registration keeps working, and is still required for generic types. To let analyzer CSLA0010 see your serializers, declare them and register each declaring assembly:

```csharp
[MobileSerializer(typeof(MoneySerializer))]        // on a type you own
public class Money { /* ... */ }

[MobileSerializerFor(typeof(ThirdParty.Widget))]    // on a serializer, for a type you don't own
public class WidgetSerializer : IMobileSerializer { /* ... */ }

builder.Services.AddCsla(o => o
  .Serialization(s => s
    .UseMobileFormatter(m => m.AddCustomSerializers(typeof(Money).Assembly))));
```

An assembly that isn't passed to `AddCustomSerializers` satisfies the analyzer but fails at run time. New analyzers: CSLA0028 (invalid declaration), CSLA0029 (more than one serializer for a type). See [CustomSerializers.md](CustomSerializers.md).

## Async `SafeDataReader`

`SafeDataReader` now has async members: `ReadAsync`, `NextResultAsync`, `CloseAsync`, `IsDBNullAsync`, `GetFieldValueAsync<T>` (by ordinal or column name), null-safe getters such as `GetStringAsync`, `GetInt32Async`, `GetDateTimeAsync`, `GetSmartDateAsync`, and `DisposeAsync`:

```csharp
await using var dr = new SafeDataReader(await cm.ExecuteReaderAsync());
while (await dr.ReadAsync())
{
  var name = await dr.GetStringAsync("Name");
  var age = await dr.GetInt32Async("Age");
}
```

* `IsDbNullAsync` is obsolete: use `IsDBNullAsync`.
* `SafeSqlDataReader` inherits `ReadAsync`, `NextResultAsync` and `GetFieldValueAsync<T>` from `SafeDataReader` (same signatures). Recompile code built against CSLA 10. They no longer throw `NotSupportedException` when the wrapped reader isn't a `SqlDataReader`.

## ASP.NET Core `CslaModelBinder` binds whole object graphs

In MVC and Razor Pages, `CslaModelBinder` (registered by `AddAspNetCore()`) now:

* binds editable child objects and child lists at any depth (`Children[0].GrandChildren[1].Name`);
* binds into the object passed to `TryUpdateModelAsync`, setting only posted values (CSLA 10 bound a separate object, so posted values never reached yours);
* honors `[BindNever]`, `[Bind]` and the `TryUpdateModelAsync` include list;
* waits for async business rules in the graph;
* no longer sets unposted child properties to `null`;
* binds a request that isn't a form post to a new object instead of throwing.

Remove workarounds that copied posted values into a fetched object by hand. See [AspNetCoreModelBinding.md](AspNetCoreModelBinding.md).

## `StateMode` is obsolete

Override the `SerializationInfo`-only overloads:

```csharp
// CSLA 10 (now CS0672/CS0618)
protected override void OnGetState(SerializationInfo info, StateMode mode) { /* ... */ }
protected override void OnSetState(SerializationInfo info, StateMode mode) { /* ... */ }

// CSLA 11
protected override void OnGetState(SerializationInfo info)
{
  base.OnGetState(info);
  info.AddValue("_custom", _custom);
}

protected override void OnSetState(SerializationInfo info)
{
  base.OnSetState(info);
  _custom = info.GetValue<string>("_custom");
}
```

Delete any `if (mode == StateMode.Undo)` branch: n-level undo takes snapshots through the serializer and never passes `StateMode.Undo`. Old overrides still run until you move them.

## CslaXml readers and writers removed

`CslaXmlReader`, `CslaXmlWriter`, `CslaXmlBinaryReader`, `CslaXmlBinaryWriter` and `CslaReaderWriterFactory.GetDataContractSerializer()` are removed. Delete configuration that selects them:

```csharp
// Remove
.UseMobileFormatter(m => m
  .MobileReader<CslaXmlBinaryReader>()
  .MobileWriter<CslaXmlBinaryWriter>())
```

The default `CslaBinaryReader`/`CslaBinaryWriter` use a different wire format, so update client and server together.

## View models

The Blazor, WPF, MAUI and Avalonia view models now share `Csla.Mvvm.ViewModel<T>` (in the `Csla` package). See [ViewModels.md](ViewModels.md). Breaking changes:

* **All:** `SaveAsync` returns `Task<T?>` (the saved model). An authorization rule that throws while `CanSave`, `CanEditObject` and the other status properties are calculated is captured in `Exception`/`ViewModelErrorText` and raises `Error`.
* **XAML (WPF, MAUI, Avalonia):** `SaveAsync` and `RefreshAsync` no longer throw. Check `Exception` and `ViewModelErrorText`, or handle `Error`, instead of `try`/`catch`. An invalid model isn't saved and only sets `ViewModelErrorText`:

  ```csharp
  // CSLA 10
  try { await vm.SaveAsync(); await NavigateBack(); }
  catch (Exception ex) { ShowError(ex); }

  // CSLA 11
  await vm.SaveAsync();
  if (vm.Exception is null && string.IsNullOrEmpty(vm.ViewModelErrorText))
    await NavigateBack();
  else
    ShowError(vm.ViewModelErrorText);
  ```

* **MAUI and Avalonia:** `ManageObjectLifetime` defaults to `true` (set `false` to keep CSLA 10 behavior); the public `ManageObjectLifetimeProperty` field is removed (use the `ManageObjectLifetime` property). On MAUI, `BeginAddNew` is obsolete: use `DoAddNew`.
* **XAML:** `RefreshAsync<F>(factory)` is obsolete; call `RefreshAsync(factory)`.
* **Blazor:** replace `protected override void OnModelChanged()` with `protected override void OnModelChanged(T? oldValue, T? newValue)` and call `base.OnModelChanged(oldValue, newValue)`.

## Other removals

* The CSLA `dotnet new` templates (`Support/Templates`) and the Visual Studio template installer are removed. Create projects with the standard .NET templates and add the `Csla` packages.

## Only if you use trimming or NativeAOT

CSLA 11 supports trimmed and NativeAOT apps. This section only applies if your app is published with `PublishTrimmed` or `PublishAot`, which includes Blazor WebAssembly (always trimmed) and .NET MAUI on iOS and Android (trimmed by default). Apps that aren't trimmed need no changes.

CSLA 11 marks APIs that can't work safely after trimming with `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]`, so a trimmed app can show new `IL2026` or `IL3050` warnings where it calls them. To build a trimmed or NativeAOT app correctly, follow [TrimmingAndNativeAot.md](TrimmingAndNativeAot.md). It covers the business class requirements, which list types and APIs to avoid, serialization, generic data portal code, data portal servers, Blazor and MAUI.

## Analyzer changes

* CSLA0014 is now a warning (was info).
* CSLA0007 reports more getters and accepts `GetProperty(P)!` (see [above](#csla0007-reports-more-property-getters)).
* CSLA0010 no longer reports types with a declared custom serializer, including `ClaimsPrincipal`.
* New: CSLA0024 (partial), CSLA0025 (use generated extension method), CSLA0026/CSLA0027 (`[CalculatedProperty]`), CSLA0028/CSLA0029 (serializer declarations), CSLA0030 (properties can't be generated), CSLADP002-CSLADP011 (generator diagnostics).
* New diagnostic suppressors CSLA1001-CSLA1004 hide `IDE0051`, `CA1822`, `IDE0060` and `IDE0058` false positives on data portal operations and CSLA configuration calls. Remove your own suppressions for those cases.
* The code fixes moved to `Csla.Analyzers.CodeFixes.dll` (in the `Csla` package). Only projects that reference `Csla.Analyzers.dll` directly by path need to add it.

See [Analyzers.md](Analyzers.md) for the full list.
