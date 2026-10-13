# Data Portal Operation Dispatch (Operations Generator)

This document applies to CSLA 11. Starting with CSLA 11, the `Csla` NuGet package includes a source generator that lets the server-side data portal call your data portal operation methods (`[Create]`, `[Fetch]`, `[Insert]`, and so on) directly, instead of finding and invoking them through reflection. It runs automatically in every C# project that references `Csla`; there is no package to install and nothing to configure. The only thing you need to do is declare your business classes `partial`.

## What you write

Write operation methods exactly as before: with an operation attribute, any accessibility (normally `private`), criteria parameters, `[Inject]` parameters (including keyed services), and either synchronous or asynchronous (`Task` or `ValueTask`). The only requirement is that the class, and every type that contains it, is `partial`.

```csharp
using System.Threading.Tasks;
using Csla;

[CslaImplementProperties]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  public partial int Id { get; private set; }
  public partial string Name { get; set; }

  [Create]
  private void Create()
  {
    BusinessRules.CheckRules();
  }

  [Fetch]
  private async Task Fetch(int id, [Inject] IPersonDal dal)
  {
    var data = await dal.GetAsync(id);
    using (BypassPropertyChecks)
    {
      Id = data.Id;
      Name = data.Name;
    }
    await BusinessRules.CheckRulesAsync();
  }

  [Insert]
  private async Task Insert([Inject] IPersonDal dal)
  {
    using (BypassPropertyChecks)
      Id = await dal.InsertAsync(Name);
  }

  [Update]
  private async Task Update([Inject] IPersonDal dal)
  {
    using (BypassPropertyChecks)
      await dal.UpdateAsync(Id, Name);
  }

  [DeleteSelf]
  private async Task DeleteSelf([Inject] IPersonDal dal)
  {
    await dal.DeleteAsync(Id);
  }
}
```

Only methods with an operation attribute are considered (CSLA 11 no longer finds operation methods by name such as `DataPortal_Fetch`). Static and generic methods are not operation methods.

## What is generated

For each `partial` class that declares operation methods, the generator adds a partial declaration containing:

1. **A nested `IDataPortalOperations` interface.** It is `internal`, marked `[EditorBrowsable(EditorBrowsableState.Never)]`, and has one member per operation method. The class implements it explicitly, and each member forwards to your method.
2. **`Csla.Server.IDataPortalOperationMapping`** (criteria-based dispatch) and **`Csla.Server.IDataPortalOperationNamedMapping`** (name-based dispatch). These are only generated for concrete (non-abstract) classes, and each is skipped if your class already implements that interface itself.

For the `PersonEdit` class above, the generated interface part looks like this (shortened):

```csharp
partial class PersonEdit : global::MyApp.PersonEdit.IDataPortalOperations,
  global::Csla.Server.IDataPortalOperationMapping,
  global::Csla.Server.IDataPortalOperationNamedMapping
{
  [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
  internal interface IDataPortalOperations
  {
    void Create();
    global::System.Threading.Tasks.Task Fetch__Int32(int id, global::MyApp.IPersonDal dal);
    global::System.Threading.Tasks.Task Insert(global::MyApp.IPersonDal dal);
    global::System.Threading.Tasks.Task Update(global::MyApp.IPersonDal dal);
    global::System.Threading.Tasks.Task DeleteSelf(global::MyApp.IPersonDal dal);
  }

  void IDataPortalOperations.Create() => Create();
  global::System.Threading.Tasks.Task IDataPortalOperations.Fetch__Int32(int id, global::MyApp.IPersonDal dal) => Fetch(id, dal);
  // ... one forwarding member per operation method

  // IDataPortalOperationMapping.InvokeOperationAsync(...) and
  // IDataPortalOperationNamedMapping.InvokeNamedOperationAsync(...) implementations
}
```

The mapping interfaces are public framework types:

```csharp
namespace Csla.Server
{
  public interface IDataPortalOperationMapping
  {
    Task InvokeOperationAsync(Type operationType, bool isSync, object?[]? criteria, IServiceProvider serviceProvider);
  }

  public interface IDataPortalOperationNamedMapping
  {
    Task InvokeNamedOperationAsync(string operationName, bool isSync, object?[]? criteria, IServiceProvider serviceProvider);
  }
}
```

You never call or implement these yourself in normal code. They exist for the generated code.

## Operation names and interface member names

Every operation method has a deterministic **operation name**, built from the operation kind and the types of its criteria parameters. `[Inject]` parameters are not part of the name.

| Rule | Example method | Operation name |
|---|---|---|
| No criteria: the kind only | `[Create] void Create()` | `Create` |
| Criteria: kind, `__`, then the criteria type names joined by `_` | `[Fetch] Task Fetch(int id, [Inject] IDal dal)` | `Fetch__Int32` |
| Several criteria | `[FetchChild] void Load(int id, string name)` | `FetchChild__Int32_String` |
| Arrays: element type + `Array` | `[Fetch] void Get(string[] names)` | `Fetch__StringArray` |
| Generic types: backtick replaced with `_`, then the type arguments | `[Fetch] void Get(List<int> ids)` | `Fetch__List_1_Int32` |
| Nullable value types are generic `Nullable<T>` | `[Fetch] void Get(double? ratio)` | `Fetch__Nullable_1_Double` |

The type names are CLR metadata names (`Int32`, `String`, `Boolean`, `Decimal`, and so on), not C# keywords. The kinds are `Create`, `Fetch`, `Insert`, `Update`, `Execute`, `Delete`, `DeleteSelf`, `CreateChild`, `FetchChild`, `InsertChild`, `UpdateChild`, `DeleteSelfChild` and `ExecuteChild`.

The generated interface member is named after the operation name, not after your method. So a method called `GetById(int id)` with `[Fetch]` becomes the interface member `Fetch__Int32`. If two methods would produce the same member signature, a numeric suffix is added (for example `Fetch__Int32_2`).

## How the data portal dispatches an operation

When the server-side data portal invokes an operation on an object, it tries three strategies in order:

1. **By name.** The client sends the operation name (for example `Fetch__Int32`) with the request. The generated `InvokeNamedOperationAsync` uses a `switch` on the name to call the method directly.
2. **By criteria.** If there is no name (for example the request came from an older client), or the name isn't handled, the generated `InvokeOperationAsync` matches the operation attribute and the runtime types of the criteria values against each operation method.
3. **By reflection.** If the generated code can't handle the call (it throws the internal signal `DataPortalOperationNotSupportedException`), the data portal falls back to the reflection-based method lookup used by earlier versions of CSLA.

The client computes the operation name when it resolves the operation method. When you use the [generated data portal extension methods](DataPortalExtensions.md), the name is precomputed at compile time, so the client doesn't need to search for the method at all.

The generated code behaves the same way as reflection-based dispatch:

* `[Inject]` parameters are resolved from the request's service provider with `GetRequiredService`, or `GetService` when the parameter is nullable or uses `AllowNull = true`. Keyed services (`[Inject(Key = ...)]`) are supported.
* Calling an async operation method from a synchronous data portal call (for example `portal.Fetch(42)` on an `async Task` method) throws, as before.
* An exception thrown by your method is wrapped in a `CallMethodException`, as before.

### What is left to reflection

Some methods are never handled by generated code and always go through reflection:

* Operation methods with `ref` or `out` parameters (analyzer CSLA0019 reports these as errors anyway).
* A method whose only parameter is `object[]`, because it receives the whole criteria array.
* Operation methods declared in a base class. The generated mapping of a class only covers the methods it declares itself. An abstract base class gets only the `IDataPortalOperations` interface; operations it declares are dispatched by reflection when a concrete subclass is used.
* Methods whose criteria use a type parameter of a generic class are excluded from name-based dispatch (criteria-based dispatch still handles them).
* Overloads that the data portal cannot choose between (see CSLADP009 below).

## Why `partial` matters

The generator can only add members to a class declared `partial`, and every containing type must also be `partial`:

```csharp
public partial class Outer
{
  public partial class PersonEdit : BusinessBase<PersonEdit>
  {
    [Fetch]
    private void Fetch(int id) { }
  }
}
```

A class that is not `partial` still works: nothing is generated for it and the data portal uses reflection, exactly as in CSLA 10. But you lose the benefits:

* **Performance.** No reflection lookup or reflection invoke on the server.
* **IDE and analyzer accuracy.** Because the generated interface calls your `private` operation methods, IDEs and analyzers no longer report them as unused (for example IDE0051 "private member is unused"), and "find references" finds them.
* **Trimming and AOT.** The trimmer sees the operation methods as statically referenced code, so they are not removed.

Analyzer **CSLA0024** (warning) reports any class with operation methods that is not `partial`, or whose containing type is not. Its code fix adds the `partial` modifier to the class and to any non-partial containing types. Records are not reported. Apply the fix across a solution with "Fix all occurrences in Solution".

```csharp
// CSLA0024: Type 'CustomerEdit' has data portal operation methods and should be declared partial
public class CustomerEdit : BusinessBase<CustomerEdit> { /* [Fetch] ... */ }

// Fixed
public partial class CustomerEdit : BusinessBase<CustomerEdit> { /* [Fetch] ... */ }
```

## Operation methods that share an operation name

Two operation methods map to the same operation name when they have the same operation attribute and the same criteria parameter types, and differ only in their `[Inject]` parameters or their method name:

```csharp
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  [Fetch]
  private void Fetch(int id) { }

  [Fetch]
  private void Fetch(int id, [Inject] IPersonDal dal) { }   // used: more injected parameters

  [Fetch]
  private void Load(int id) { }
}
```

As with reflection-based dispatch, the data portal calls the method with the most injected parameters. The analyzer reports the others:

* **CSLADP002** (Info): "Methods 'Fetch(int, IPersonDal)' and 'Fetch(int)' on 'PersonEdit' both map to data portal operation 'Fetch__Int32'; name-based dispatch uses 'Fetch(int, IPersonDal)'". The losing methods are never called by the data portal. Remove them or change their criteria.
* **CSLADP009** (Warning): two or more methods map to the same operation name *and* have the same number of injected parameters. The data portal cannot choose between them, so neither is dispatched by generated code, and the reflection path reports an ambiguous match at runtime. Remove one, or make the criteria types different.

These diagnostics are only reported for `partial` classes. They are reported by an analyzer (not by the generator), so you can suppress them with `#pragma warning disable`, `[SuppressMessage]`, or an `.editorconfig` entry.

## Upgrading from CSLA 10

* Remove any `PackageReference` (and `PackageVersion`) to `Csla.Generator.AutoImplementProperties.CSharp`. The generators are now in the `Csla` package, and the build fails with `CSLABUILD001` while the reference remains. The `[CslaImplementProperties]`, `[CslaImplementPropertiesInterface<T>]` and `[CslaIgnoreProperty]` attributes are now in `Csla.dll`, still in the `Csla` namespace.
* Add `partial` to business classes with operation methods (use the CSLA0024 code fix).
* Make sure every operation method has an operation attribute (use the CSLA0014 code fix). Methods found only by name are no longer called.

See [UpgradingToCsla11.md](UpgradingToCsla11.md) for the full list of breaking changes.

## Related

* [DataPortalExtensions.md](DataPortalExtensions.md) - strongly typed extension methods for calling the data portal
* [Analyzers.md](Analyzers.md) - all CSLA analyzers and CSLADP diagnostics
