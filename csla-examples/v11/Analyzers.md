# CSLA Analyzers and Diagnostics Reference

This document applies to CSLA 11. It lists every Roslyn analyzer diagnostic shipped in the `Csla` NuGet package as of CSLA 11, with its default severity, what it detects and whether it has a code fix. Severities are the defaults declared in the analyzer source. There is nothing to install: the analyzers (and the CSLA source generators) are bundled in the `Csla` package and run in any project that references it.

## What changed in CSLA 11

| ID | Change |
|---|---|
| CSLA0014 | Severity raised from **Info** to **Warning**. The data portal no longer calls methods by name (`DataPortal_Fetch`, `Child_Update`, ...), so an attribute-less method with a legacy name is never called. The code fix adds the matching operation attribute. |
| CSLA0024 | **New.** A class with data portal operation methods should be `partial` so the operations generator can dispatch without reflection. Code fix adds `partial`. |
| CSLA0025 | **New.** Use the generated `[DataPortalExtensions]` method instead of calling an untyped `IDataPortal<T>` method. Code fix replaces the call. |
| CSLADP002 - CSLADP011 | **New.** Diagnostics for the bundled data portal operations and extensions generators. They are reported by analyzers (not by the generators), so they can be suppressed in source or `.editorconfig`. |
| CSLABUILD001 | **New** MSBuild error (not an analyzer): the retired `Csla.Generator.AutoImplementProperties.CSharp` package is still referenced. |

## CSLA analyzers

| ID | Severity | Detects | Code fix |
|---|---|---|---|
| CSLA0002 | Warning | A data portal operation method is `public`. Operation methods should be private (or protected/internal). | Yes: make the method private, protected or internal |
| CSLA0003 | Error | A CSLA business class has no public parameterless constructor. | Yes: add one, or make the existing parameterless constructor public |
| CSLA0004 | Warning | A CSLA business class has a public constructor with parameters. | No |
| CSLA0005 | Error | The result of `Save()` is ignored (`obj.Save();` instead of `obj = obj.Save();`). | Yes: add the assignment |
| CSLA0006 | Error | The result of `SaveAsync()` is ignored. | Yes: add the assignment |
| CSLA0007 | Warning | A property that uses a managed backing field does more than call `GetProperty`/`SetProperty`/`ReadProperty`/`LoadProperty`. | No |
| CSLA0008 | Error | A managed backing field (`PropertyInfo<T>`) is not `public static readonly`. | Yes: fix the field declaration |
| CSLA0009 | Warning | A data portal operation is declared `public` on a business object interface. | No |
| CSLA0010 | Warning | An operation method's criteria argument type is not serializable. | No |
| CSLA0011 | Error | A business object is created with `new` outside of an `ObjectFactory`. Use the data portal instead. | No |
| CSLA0012 | Error | An operation method returns something other than `void` or `Task`. | Yes: change the return type to `void` or `Task` |
| CSLA0013 | Warning | A child operation method has `[RunLocal]`, which has no effect on child operations. | Yes: remove `[RunLocal]` |
| CSLA0014 | **Warning** (Info before v11) | A method in a CSLA type has a legacy operation name (`DataPortal_Create`, `DataPortal_Fetch`, `Child_Update`, ...) but no operation attribute, so the data portal never calls it. | Yes: add the operation attribute |
| CSLA0015 | Error | An operation attribute is used on a type that isn't a CSLA stereotype or an `ObjectFactory`, or on a `static` method. | No |
| CSLA0016 | Error | A business rule has an `async` `Execute` method; asynchronous rules should derive from `BusinessRuleAsync` and implement `ExecuteAsync`. | Yes: change the base type to the async equivalent |
| CSLA0017 | Warning | A business rule never calls an `Add...()` method (such as `AddErrorResult`) on its context. | No |
| CSLA0018 | Error | `Complete()` is called inside an asynchronous (`BusinessRuleAsync`) rule. | Yes: remove the `Complete()` calls |
| CSLA0019 | Error | An operation method has `ref` or `out` parameters. | No |
| CSLA0020 | Warning | `AddObjectAuthorizationRules` is not marked with `[ObjectAuthorizationRules]`. | No |
| CSLA0021 | Info | The object authorization rules method is not `public`. | No |
| CSLA0022 | Warning | The object authorization rules method is not `static`. | No |
| CSLA0023 | Info | A managed backing field is registered with a lambda (`RegisterProperty<T>(c => c.Name)`) instead of `nameof(Name)`. | Yes: use `nameof` |
| CSLA0024 | Warning (**new**) | A class (not a record) declares data portal operation methods but the class, or a type containing it, is not `partial`. | Yes: add `partial` to the class and its containing types |
| CSLA0025 | Warning (**new**) | Code calls an untyped criteria method (`Create`, `Fetch`, `Delete`, criteria `Execute`, `CreateChild`, `FetchChild`, or their `Async` forms) on `IDataPortal<T>`, `IChildDataPortal<T>` or `DataPortal<T>` when `T` (or its assembly) has `[DataPortalExtensions]` and a generated extension method can replace the call. | Yes: replace with the generated extension method |

CSLA0001 ("business objects must be serializable") no longer exists. It was removed when CSLA stopped requiring `[Serializable]` (CSLA 9), although its documentation page still exists in the CSLA repository.

## Data portal generator diagnostics (CSLADP)

These come with the data portal operations generator (always on) and the data portal extensions generator (`[DataPortalExtensions]`). Their category is `Csla.DataPortal`. There is no CSLADP001. None has a code fix.

| ID | Severity | Detects |
|---|---|---|
| CSLADP002 | Info | Two operation methods of a `partial` class map to the same operation name (same attribute and criteria types, different `[Inject]` parameters or method names). The one with more injected parameters is used; the other is never called. |
| CSLADP003 | Info | `[DataPortalExtensions]` is on a generic type (or a type nested in a generic type); no extension methods are generated. |
| CSLADP004 | Warning | `[DataPortalExtensions]` is on an invalid target: an abstract type, a type that doesn't implement `ICslaObject`, or a type not accessible outside its containing type. |
| CSLADP005 | Warning | A generated extension method would be hidden by an `IDataPortal<T>` or `IChildDataPortal<T>` instance method (for example an operation method named `Fetch`), so it is not generated. Set `Prefix` or rename the operation method. |
| CSLADP006 | Warning | A criteria parameter type is not accessible outside the business class (private or protected nested type), so the extension method is not generated. |
| CSLADP007 | Warning | Two operation methods would produce extension methods with the same name and parameter types; the later one is not generated. |
| CSLADP008 | Warning | The `[DataPortalExtensions]` `Prefix` (on the class or the assembly) is not a valid C# identifier; nothing is generated. |
| CSLADP009 | Warning | Two operation methods map to the same operation name with the same number of injected parameters. The data portal cannot choose between them and reports an ambiguous match at runtime. |
| CSLADP010 | Warning | The `CslaDataPortalExtensionsAsyncSuffix` MSBuild property value is not valid in an identifier; the `Async` suffix is used instead. |
| CSLADP011 | Warning | `CslaDataPortalExtensionsAsyncSuffix` is `none` but `CslaGenerateSyncDataPortalExtensions` is not `false`, so the sync methods (which would have the same names) are not generated. |

CSLADP010 and CSLADP011 are compilation-level diagnostics with no source location, and are only reported when at least one class requests extension methods.

## Build error CSLABUILD001

`CSLABUILD001` is an MSBuild error from `Csla.targets`, reported while a project still has a `PackageReference` to the retired `Csla.Generator.AutoImplementProperties.CSharp` package. In CSLA 11 its generators are part of the `Csla` package, and referencing both would run the generators twice. Remove the `PackageReference` (and any `PackageVersion` entry in `Directory.Packages.props`).

## Changing severities and suppressing

Use `.editorconfig` to change a diagnostic's severity for a project or folder:

```ini
[*.cs]
# Treat a missing partial modifier as an error
dotnet_diagnostic.CSLA0024.severity = error

# Don't nag about untyped data portal calls during a gradual migration
dotnet_diagnostic.CSLA0025.severity = suggestion

# Silence the informational duplicate-operation-name message
dotnet_diagnostic.CSLADP002.severity = none
```

Or suppress an individual occurrence in source:

```csharp
#pragma warning disable CSLA0025 // calling the untyped method on purpose
var person = await portal.FetchAsync(criteria);
#pragma warning restore CSLA0025
```

## Analyzers that help when upgrading to CSLA 11

Run these code fixes with "Fix all occurrences in Solution" early in an upgrade:

1. **CSLA0014**: adds the missing operation attributes. This is the most important one, because a method without an attribute is silently not called in CSLA 11 (or a base class method runs instead).
2. **CSLA0024**: makes business classes `partial` so operations are dispatched without reflection.
3. **CSLA0025**: only appears after you opt into `[DataPortalExtensions]`; switches calls to the strongly typed methods.

See [UpgradingToCsla11.md](UpgradingToCsla11.md), [DataPortalOperationDispatch.md](DataPortalOperationDispatch.md) and [DataPortalExtensions.md](DataPortalExtensions.md).
