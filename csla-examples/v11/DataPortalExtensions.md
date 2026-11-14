# Data Portal Extensions Generator ([DataPortalExtensions])

This document applies to CSLA 11. Starting with CSLA 11, the `Csla` NuGet package includes a source generator that creates strongly typed extension methods on `IDataPortal<T>` and `IChildDataPortal<T>` for the data portal operation methods of your business classes. You opt in with the `[DataPortalExtensions]` attribute.

Instead of calling the untyped `params object[]` methods, where a wrong number, order or type of criteria is only found at runtime:

```csharp
var person = await portal.FetchAsync(42);
```

you call a generated method whose parameters match the operation method, so the compiler checks the call:

```csharp
var person = await portal.GetByIdAsync(42);
```

The generator and the related CSLA0025 analyzer are adapted from [Csla.DataPortalExtensions](https://github.com/StefanOssendorf/Csla.DataPortalExtensions) by Stefan Ossendorf. For CSLA 11 and later, use the generator included in the `Csla` package instead of the `Csla.DataPortalExtensions` package.

## Enabling the generator for a class

Add `[DataPortalExtensions]` (namespace `Csla`) to the business class:

```csharp
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
  [DataPortalExtensions]
  [CslaImplementProperties]
  public partial class PersonEdit : BusinessBase<PersonEdit>
  {
    public partial int Id { get; private set; }
    public partial string Name { get; set; }

    [Create]
    private void New()
    {
      BusinessRules.CheckRules();
    }

    [Fetch]
    private async Task GetById(int id, [Inject] IPersonDal dal)
    {
      var data = await dal.GetAsync(id);
      using (BypassPropertyChecks)
      {
        Id = data.Id;
        Name = data.Name;
      }
      await BusinessRules.CheckRulesAsync();
    }

    [Delete]
    private Task RemoveAsync(int id, [Inject] IPersonDal dal) => dal.DeleteAsync(id);

    [Insert]
    private async Task Insert([Inject] IPersonDal dal) { /* ... */ }

    [Update]
    private async Task Update([Inject] IPersonDal dal) { /* ... */ }
  }
}
```

The generator creates a static class named `PersonEditDataPortalExtensions` in the same namespace as `PersonEdit` (`MyApp.Business`), with these methods:

```csharp
public static partial class PersonEditDataPortalExtensions
{
  public static Task<PersonEdit> NewAsync(this IDataPortal<PersonEdit> portal);
  public static PersonEdit New(this IDataPortal<PersonEdit> portal);

  public static Task<PersonEdit> GetByIdAsync(this IDataPortal<PersonEdit> portal, int id);
  public static PersonEdit GetById(this IDataPortal<PersonEdit> portal, int id);

  public static Task RemoveAsync(this IDataPortal<PersonEdit> portal, int id);
  public static void Remove(this IDataPortal<PersonEdit> portal, int id);
}
```

Callers need a `using` for the business class's namespace to see the extension methods. Use them anywhere you have an `IDataPortal<T>`:

```csharp
public class PersonService(IDataPortal<PersonEdit> portal)
{
  public Task<PersonEdit> NewPersonAsync() => portal.NewAsync();
  public Task<PersonEdit> GetPersonAsync(int id) => portal.GetByIdAsync(id);
  public Task DeletePersonAsync(int id) => portal.RemoveAsync(id);
}
```

Saving is unchanged: call `SaveAsync()` on the object, or `portal.UpdateAsync(obj)`. Update operations (`[Insert]`, `[Update]`, `[DeleteSelf]`) do not take criteria, so no extension methods are generated for them.

The class does not have to be `partial` for the extension methods (they are generated in a separate class), but it should be `partial` anyway so the [operations generator](DataPortalOperationDispatch.md) can dispatch the operations without reflection.

## Which operations get extension methods

| Operation attribute | Receiver | Async return type | Sync return type |
|---|---|---|---|
| `[Create]` | `IDataPortal<T>` | `Task<T>` | `T` |
| `[Fetch]` | `IDataPortal<T>` | `Task<T>` | `T` |
| `[Execute]` | `IDataPortal<T>` | `Task<T>` | `T` |
| `[Delete]` | `IDataPortal<T>` | `Task` | `void` |
| `[CreateChild]` | `IChildDataPortal<T>` | `Task<T>` | `T` |
| `[FetchChild]` | `IChildDataPortal<T>` | `Task<T>` | `T` |

`[Insert]`, `[Update]`, `[DeleteSelf]`, `[InsertChild]`, `[UpdateChild]`, `[DeleteSelfChild]` and `[ExecuteChild]` get no extension methods.

Only operation methods declared in the class itself are used. Operation methods inherited from a base class do not get extension methods for the derived class.

## Generated method names

The name of each generated method is:

```text
Prefix + (operation method name without a trailing "Async") + suffix
```

* The async method uses the suffix `Async` (configurable, see [Build properties](#build-properties)).
* The sync method has no suffix.
* `Prefix` comes from the `[DataPortalExtensions]` attribute (empty by default).

| Operation method | Prefix | Generated async / sync names |
|---|---|---|
| `[Fetch] Task GetById(int id, [Inject] IDal dal)` | (none) | `GetByIdAsync` / `GetById` |
| `[Delete] Task RemoveAsync(int id)` | (none) | `RemoveAsync` / `Remove` |
| `[Create] void Create()` | `Portal` | `PortalCreateAsync` / `PortalCreate` |
| `[Fetch] void Fetch(int id)` | `Portal` | `PortalFetchAsync` / `PortalFetch` |
| `[Fetch] void Fetch(int id)` | (none) | not generated (hidden, CSLADP005) |

### Name hiding and Prefix

`IDataPortal<T>` already has instance methods named `Create`, `CreateAsync`, `Fetch`, `FetchAsync`, `Execute`, `ExecuteAsync`, `Delete`, `DeleteAsync`, `Update` and `UpdateAsync`. `IChildDataPortal<T>` has `CreateChild`, `CreateChildAsync`, `FetchChild`, `FetchChildAsync`, `UpdateChild` and `UpdateChildAsync`. C# always prefers an instance method over an extension method, so an extension method with one of those names would never be called.

The generator does not generate such methods. The analyzer reports each one as **CSLADP005** (warning). This is checked per receiver: a `[FetchChild]` method named `Fetch` is fine, because `IChildDataPortal<T>` has no `Fetch` method.

This is why the common CSLA naming style (operation methods named `Create`, `Fetch`, `Delete`) needs a prefix. Use either:

```csharp
// Option 1: a prefix
[DataPortalExtensions(Prefix = "Portal")]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  [Fetch]
  private void Fetch(int id) { }       // portal.PortalFetchAsync(id), portal.PortalFetch(id)
}

// Option 2: descriptive operation method names
[DataPortalExtensions]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  [Fetch]
  private void FetchById(int id) { }   // portal.FetchByIdAsync(id), portal.FetchById(id)
}
```

The prefix must be a valid C# identifier, otherwise nothing is generated for the class and **CSLADP008** is reported.

## Parameters

* **Only criteria parameters** appear in the generated method. `[Inject]` parameters are omitted and are resolved from DI on the server, as usual. They can appear anywhere in the operation method's parameter list.
* **Default values** are kept, including enum values: `[Fetch] void Find(string name, int page = 1)` produces `FindAsync(this IDataPortal<T> portal, string name, int page = 1)`.
* **`params` arrays** are kept: `[Create] void NewWith(params int[] values)` produces `NewWithAsync(this IDataPortal<T> portal, params int[] values)`.
* **Nullable annotations** are kept (`string? name`).
* Parameter names that collide with the generated names `portal` or `__invoker` are handled automatically.

## Child portals

`[CreateChild]` and `[FetchChild]` methods get extension methods on `IChildDataPortal<T>`:

```csharp
[DataPortalExtensions]
public partial class LineItem : BusinessBase<LineItem>
{
  [CreateChild]
  private void NewChild() { }

  [FetchChild]
  private void LoadChild(LineItemData data) { /* ... */ }
}

// Generated
public static partial class LineItemDataPortalExtensions
{
  public static Task<LineItem> NewChildAsync(this IChildDataPortal<LineItem> portal);
  public static LineItem NewChild(this IChildDataPortal<LineItem> portal);
  public static Task<LineItem> LoadChildAsync(this IChildDataPortal<LineItem> portal, LineItemData data);
  public static LineItem LoadChild(this IChildDataPortal<LineItem> portal, LineItemData data);
}
```

Use them in the parent's operation methods, for example in the `[FetchChild]` method of a `LineItems : BusinessListBase<LineItems, LineItem>` list:

```csharp
[FetchChild]
private void Fetch(int orderId, [Inject] ILineItemDal dal, [Inject] IChildDataPortal<LineItem> childPortal)
{
  using (LoadListMode)
  {
    foreach (var data in dal.FetchForOrder(orderId))
      Add(childPortal.LoadChild(data));
  }
}
```

## Commands

`[Execute]` methods with criteria get extension methods that create and execute the command:

```csharp
[DataPortalExtensions]
public partial class RecalculateCommand : CommandBase<RecalculateCommand>
{
  [Execute]
  private void Run(int count) { /* ... */ }
}

// Generated: RunAsync(this IDataPortal<RecalculateCommand> portal, int count)
//            Run(this IDataPortal<RecalculateCommand> portal, int count)
var result = await portal.RunAsync(10);
```

Executing an existing command object (`portal.ExecuteAsync(command)`) is unchanged.

## Enabling the generator for a whole assembly

Add the attribute at assembly level (for example in any `.cs` file in the business library):

```csharp
[assembly: Csla.DataPortalExtensions(Prefix = "Portal")]
```

This generates extension methods for every class in the assembly that has operation methods and that is:

* non-abstract,
* non-generic (and not nested in a generic type),
* not a record,
* a CSLA type (implements `Csla.Core.ICslaObject`),
* accessible outside its containing type (public or internal, not private or protected nested).

Classes that don't qualify are silently skipped when they are covered only by the assembly attribute. A `Prefix` on a class attribute takes precedence over the assembly `Prefix`:

```csharp
[assembly: DataPortalExtensions(Prefix = "Portal")]

public partial class PersonEdit : BusinessBase<PersonEdit>   // PortalFetchAsync, ...
{
  [Fetch] private void Fetch(int id) { }
}

[DataPortalExtensions(Prefix = "My")]
public partial class OrderEdit : BusinessBase<OrderEdit>     // MyCreateAsync, ...
{
  [Create] private void Create() { }
}
```

When the attribute is on a class that can't have extension methods, the analyzer tells you why: **CSLADP003** (Info) for a generic type, **CSLADP004** (warning) for an abstract type, a type that doesn't implement `ICslaObject`, or a type not accessible outside its containing type.

## Excluding classes and methods

Add `[NoDataPortalExtension]` (namespace `Csla`) to a class or an operation method to exclude it:

```csharp
[DataPortalExtensions]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  [Fetch]
  private void GetById(int id) { }        // GetByIdAsync / GetById generated

  [NoDataPortalExtension]
  [Fetch]
  private void GetByName(string name) { } // nothing generated
}

[NoDataPortalExtension]                    // excluded from [assembly: DataPortalExtensions]
public partial class LegacyEdit : BusinessBase<LegacyEdit> { /* ... */ }
```

## Accessibility

* A public business class gets a `public static partial class` with `public` methods.
* An internal business class gets an `internal` (no modifier) static class with `internal` methods.
* If a criteria parameter type is internal, that one extension method is `internal`.
* If a criteria parameter type is private or protected inside the business class, no extension method can be generated for that operation method and **CSLADP006** (warning) is reported.

For nested business classes, the extension class name includes the containing types: `Outer.PersonEdit` gets `Outer_PersonEditDataPortalExtensions`.

## Build properties

Two MSBuild properties, set in the project file of the business library, control the generated methods:

```xml
<PropertyGroup>
  <!-- Generate the synchronous methods (default true) -->
  <CslaGenerateSyncDataPortalExtensions>false</CslaGenerateSyncDataPortalExtensions>

  <!-- Suffix of the async method names (default Async); "none" for no suffix -->
  <CslaDataPortalExtensionsAsyncSuffix>none</CslaDataPortalExtensionsAsyncSuffix>
</PropertyGroup>
```

| Settings | `[Fetch] void GetById(int id)` generates |
|---|---|
| Defaults | `Task<T> GetByIdAsync(int id)` and `T GetById(int id)` |
| `CslaGenerateSyncDataPortalExtensions` = `false` | `Task<T> GetByIdAsync(int id)` only |
| `CslaDataPortalExtensionsAsyncSuffix` = `Task` | `Task<T> GetByIdTask(int id)` and `T GetById(int id)` |
| `CslaDataPortalExtensionsAsyncSuffix` = `none`, sync = `false` | `Task<T> GetById(int id)` only |
| `CslaDataPortalExtensionsAsyncSuffix` = `none`, sync not set | `Task<T> GetById(int id)` only, plus warning **CSLADP011** |

With no async suffix, the sync methods would have the same names as the async methods, so they are never generated. Set `CslaGenerateSyncDataPortalExtensions` to `false` to make that explicit and silence CSLADP011. A suffix that is not valid in a C# identifier is ignored (the `Async` suffix is used) and **CSLADP010** is reported.

The `Csla` package declares both properties as `CompilerVisibleProperty`, so no extra MSBuild setup is needed.

## How the generated methods work

The generated code passes the precomputed operation name and the `[RunLocal]` setting to the data portal, so the client does not search for the operation method with reflection, and the server can [dispatch by name](DataPortalOperationDispatch.md). The generated `GetByIdAsync` above looks like this:

```csharp
public static Task<PersonEdit> GetByIdAsync(this IDataPortal<PersonEdit> portal, int id)
{
  if (portal is IDataPortalOperationInvoker<PersonEdit> __invoker)
    return __invoker.FetchByOperationAsync("Fetch__Int32", false, new object?[] { id });
  return portal.FetchAsync(new object?[] { id });
}
```

`DataPortal<T>` (the implementation you get from DI or `IDataPortalFactory`) implements `IDataPortalOperationInvoker<T>` and `IChildDataPortalOperationInvoker<T>`. These interfaces are hidden from IntelliSense and are meant for generated code only; don't call them directly.

### Mocks and custom IDataPortal<T> implementations

If the portal does not implement the invoker interface (for example a Moq or NSubstitute mock of `IDataPortal<T>`, or your own implementation), the generated method falls back to the existing `params object[]` method, passing the criteria as an array. Extension methods are static and can't be mocked, so set up the underlying method on the mock:

```csharp
var mock = new Mock<IDataPortal<PersonEdit>>();
mock.Setup(p => p.FetchAsync(It.Is<object[]>(c => c.Length == 1 && (int)c[0] == 42)))
    .ReturnsAsync(testPerson);

var person = await mock.Object.GetByIdAsync(42);   // calls FetchAsync(new object[] { 42 })
```

## Diagnostics

The diagnostics about extension methods are reported by an analyzer in the `Csla` package, so you can suppress them in source or in `.editorconfig`.

| ID | Severity | Meaning |
|---|---|---|
| CSLADP003 | Info | No extension methods for a generic type (or a type nested in a generic type). |
| CSLADP004 | Warning | `[DataPortalExtensions]` on an invalid target: abstract, not an `ICslaObject`, or not accessible outside its containing type. |
| CSLADP005 | Warning | An extension method would be hidden by an `IDataPortal<T>` / `IChildDataPortal<T>` instance method, so it is not generated. Use `Prefix` or rename the operation method. |
| CSLADP006 | Warning | A criteria parameter type is not accessible outside the business class, so the extension method is not generated. |
| CSLADP007 | Warning | Two operation methods would produce extension methods with the same name and parameter types (for example a `[Fetch] Get(int)` and a `[Create] Get(int, [Inject] ...)`); only the first is generated. |
| CSLADP008 | Warning | The `Prefix` (class or assembly) is not a valid C# identifier. |
| CSLADP010 | Warning | `CslaDataPortalExtensionsAsyncSuffix` is not valid in an identifier; `Async` is used. |
| CSLADP011 | Warning | Async suffix is `none` but sync methods were not turned off; sync methods are not generated. |

Operation methods that lose a name collision (CSLADP002, see [DataPortalOperationDispatch.md](DataPortalOperationDispatch.md)) don't get extension methods either.

### CSLA0025: use the generated extension method

When a business type (or its assembly) has `[DataPortalExtensions]`, analyzer **CSLA0025** (warning) reports calls to the untyped methods that a generated extension method can replace:

* On `IDataPortal<T>` and `DataPortal<T>`: `Create`, `CreateAsync`, `Fetch`, `FetchAsync`, `Delete`, `DeleteAsync`, and the criteria overloads of `Execute` and `ExecuteAsync`.
* On `IChildDataPortal<T>` and `DataPortal<T>`: `CreateChild`, `CreateChildAsync`, `FetchChild`, `FetchChildAsync`.

A call is only reported when a generated extension method of the matching kind accepts the number of criteria values passed. `Update`/`UpdateAsync`, `UpdateChild`/`UpdateChildAsync` and `Execute(command)` are never reported. Sync calls are not reported when sync extension methods are not generated.

The code fix replaces the call and adds a `using` for the business class's namespace if needed. If several generated methods accept the arguments, a fix is offered for each:

```csharp
// Before (CSLA0025)
var person = await portal.FetchAsync(id);

// After
var person = await portal.GetByIdAsync(id);
```

This means adding `[DataPortalExtensions]` to an existing class (or assembly) can produce many CSLA0025 warnings in calling code. Use "Fix all occurrences" or migrate gradually.

## Related

* [DataPortalOperationDispatch.md](DataPortalOperationDispatch.md) - the always-on operations generator and operation names
* [Analyzers.md](Analyzers.md) - all CSLA analyzers and CSLADP diagnostics
* [UpgradingToCsla11.md](UpgradingToCsla11.md) - upgrading from CSLA 10
