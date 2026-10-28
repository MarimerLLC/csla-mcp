# Trimming and NativeAOT

This document applies to CSLA 11. It explains how to write CSLA business classes and application code that work in trimmed and NativeAOT applications, and which CSLA APIs to avoid there. Follow it when the app is published with `PublishTrimmed` or `PublishAot`. That includes Blazor WebAssembly, which always trims, and .NET MAUI on iOS and Android, which trims by default.

CSLA 11 declares what it needs from the trimmer: business types are annotated so the trimmer keeps their members, the data portal calls operation methods through generated code, and APIs that can't be made safe are marked `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]`. If your code follows the rules below and the publish produces no `IL` warnings, CSLA works in a trimmed or NativeAOT app.

## The rules

1. Declare business classes, and every type that contains them, `partial`. Give every data portal operation method an operation attribute (`[Create]`, `[Fetch]`, `[Insert]`, `[Update]`, `[DeleteSelf]`, `[Delete]`, `[Execute]`, and the `Child` variants).
2. Derive business types from the CSLA base classes (`BusinessBase<T>`, `ReadOnlyBase<T>`, `CommandBase<T>`, `BusinessListBase<T, C>`, `ReadOnlyListBase<T, C>`, and so on). Don't implement `ICslaObject` or the other CSLA interfaces from scratch.
3. Use the `ObservableCollection` based list types. Don't use the `BindingList` based types (`BusinessBindingListBase`, `ReadOnlyBindingListBase`, `DynamicBindingListBase`, `NameValueListBase`).
4. Get data portals through DI or `IDataPortalFactory` with a concrete type argument, and annotate any generic type parameter you pass through to CSLA.
5. Name object factories with `typeof`, or with an assembly qualified name.
6. Don't use the late-bound reflection helpers (`MethodCaller.CallMethod`, `Utilities.CallByName`, `DataMapper`, and so on). Write the code directly.
7. Serialize non-CSLA types with `[AutoSerializable]` or a custom serializer that uses source-generated JSON, not `PocoSerializer<T>`.
8. On a data portal server, root the assemblies that contain your business types.
9. Treat every `IL` warning as a bug. Fix it or annotate the code. Only suppress a warning when you can show the requirement holds.

## Project settings

For an app published with NativeAOT:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <!-- Report each trim warning with its location instead of one warning per assembly -->
  <TrimmerSingleWarn>false</TrimmerSingleWarn>
</PropertyGroup>
```

For an app that is trimmed but not compiled with NativeAOT (Blazor WebAssembly and MAUI set this for you):

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimmerSingleWarn>false</TrimmerSingleWarn>
</PropertyGroup>
```

For a class library that contains business types, turn on the analyzers so warnings appear at build time, not only at publish time:

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

Warnings to watch for:

| Warning | Meaning |
|---|---|
| IL2026 | Calls a member marked `[RequiresUnreferencedCode]`; it can fail after trimming |
| IL3050 | Calls a member marked `[RequiresDynamicCode]`; it can fail under NativeAOT |
| IL2067, IL2072, IL2075, IL2087, IL2091 | A `Type` or generic type parameter doesn't carry the `[DynamicallyAccessedMembers]` the callee requires |

## Business classes

A business class written the normal CSLA 11 way is trim and AOT compatible:

```csharp
using System.ComponentModel.DataAnnotations;
using Csla;

[CslaImplementProperties]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
  public partial int Id { get; private set; }

  [Required]
  public partial string Name { get; set; }

  public partial ChildList Children { get; private set; }

  [Create]
  private async Task Create([Inject] IChildDataPortal<ChildList> childPortal)
  {
    LoadProperty(ChildrenProperty, await childPortal.CreateChildAsync());
    await BusinessRules.CheckRulesAsync();
  }

  [Fetch]
  private async Task Fetch(int id, [Inject] IPersonDal dal, [Inject] IChildDataPortal<ChildList> childPortal)
  {
    var data = await dal.GetAsync(id);
    using (BypassPropertyChecks)
    {
      Id = data.Id;
      Name = data.Name;
    }
    LoadProperty(ChildrenProperty, await childPortal.FetchChildAsync(id));
    await BusinessRules.CheckRulesAsync();
  }

  [Insert]
  private async Task Insert([Inject] IPersonDal dal)
  {
    using (BypassPropertyChecks)
      Id = await dal.InsertAsync(Name);
    await FieldManager.UpdateChildrenAsync();
  }
}
```

Why this works:

* `BusinessBase<T>`, `ReadOnlyBase<T>`, `CommandBase<T>`, `BusinessListBase<T, C>` and `DynamicListBase<T>` annotate their type parameters with `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]`, so the trimmer keeps every member of `PersonEdit`, including private ones. Business rules, data annotations, authorization rule methods (`[ObjectAuthorizationRules]`) and property registrations are all preserved.
* `IDataPortal<T>`, `IChildDataPortal<T>` and `IDataPortalFactory.GetPortal<T>()` annotate `T` with `All` too, so every type you create or fetch through the data portal is preserved, including read-only lists and their children.
* Because the class is `partial`, the operations generator adds code that calls `Create`, `Fetch` and `Insert` directly, so the data portal doesn't need reflection to find or invoke them. See [DataPortalOperationDispatch.md](DataPortalOperationDispatch.md). Analyzer **CSLA0024** reports operation classes that aren't `partial`.

### Loading properties

Load property values with a value of the property's own type. `LoadProperty(IPropertyInfo, object)` falls back to reflection to convert values, for example a boxed `int` into a `long` property, and that fallback isn't available under NativeAOT. There it throws `NotSupportedException` ("Loading property '...' requires dynamic code"). Use the typed `PropertyInfo<T>` overloads, or convert the value first:

```csharp
// Works everywhere
LoadProperty(TotalProperty, (long)reader.GetInt32(0));

// Fails under NativeAOT if TotalProperty is PropertyInfo<long> and the value is a boxed int
LoadProperty((IPropertyInfo)TotalProperty, (object)reader.GetInt32(0));
```

## Lists

The `BindingList` based list types depend on `PropertyDescriptor`s, which aren't trim compatible. Their constructors are marked `[RequiresUnreferencedCode]`.

| Avoid | Use |
|---|---|
| `BusinessBindingListBase<T, C>` | `BusinessListBase<T, C>` |
| `ReadOnlyBindingListBase<T, C>` | `ReadOnlyListBase<T, C>` |
| `DynamicBindingListBase<T>` | `DynamicListBase<T>` |
| `NameValueListBase<K, V>` | A `ReadOnlyListBase<T, C>` of `ReadOnlyBase<T>` items with key and value properties |
| `SortedBindingList<T>`, `FilteredBindingList<T>` (sorting or filtering by property name) | LINQ, or a view model that exposes a sorted or filtered collection |

## Calling the data portal from generic code

Code that calls the data portal with a concrete type needs nothing special:

```csharp
public class PersonService(IDataPortal<PersonEdit> portal)
{
  public Task<PersonEdit> GetAsync(int id) => portal.FetchAsync(id);
}
```

Generic code must pass the requirement on, or the trimmer reports IL2091:

```csharp
using System.Diagnostics.CodeAnalysis;

public class Repository(IDataPortalFactory factory)
{
  public Task<T> FetchAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(object criteria)
    => factory.GetPortal<T>().FetchAsync(criteria);
}
```

The same applies to `Type` values passed to CSLA APIs. `BusinessRules.HasPermission(applicationContext, action, typeof(PersonEdit))` is fine, because `typeof` is visible to the trimmer. A `Type` parameter or field that flows into it needs `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`. Generic classes of your own that wrap CSLA generic types (for example a base page model over `PageModel<T>`) need the same annotation on their type parameter as the CSLA type has.

## Object factories

Factory types and their methods are found by reflection, so the trimmer must be able to see the factory type:

```csharp
// Best: the trimmer sees the type
[ObjectFactory(typeof(InventoryFactory))]
public partial class InventoryItem : BusinessBase<InventoryItem> { }

// Also works: assembly qualified, or a type in the business object's assembly
[ObjectFactory("MyApp.Library.CatalogFactory, MyApp.Library")]
public partial class CatalogItem : ReadOnlyBase<CatalogItem> { }
```

A custom `IObjectFactoryLoader` that maps other names to factory types must make sure those types are preserved.

## Serializing types that aren't CSLA business types

MobileFormatter serializes CSLA business types without help. For criteria classes, DTOs and other types:

* **Preferred: `[AutoSerializable]`.** The source generator implements `IMobileObject` at compile time, with no reflection. See [Serialization.md](Serialization.md).
* **Don't use `PocoSerializer<T>`** in a trimmed or NativeAOT app. It uses reflection-based `System.Text.Json` and is marked `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`.
* **Custom serializer with source-generated JSON.** If a type can't use `[AutoSerializable]`, write an `IMobileSerializer` that uses a `JsonSerializerContext`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Csla.Serialization.Mobile;

public class CustomerCriteria
{
  public string Region { get; set; } = "";
  public int MinAge { get; set; }
}

[JsonSerializable(typeof(CustomerCriteria))]
internal partial class AppJsonContext : JsonSerializerContext
{
}

public class CustomerCriteriaSerializer : IMobileSerializer
{
  public static bool CanSerialize(Type type) => type == typeof(CustomerCriteria);

  public void Serialize(object obj, SerializationInfo info)
    => info.AddValue("s", JsonSerializer.Serialize((CustomerCriteria)obj, AppJsonContext.Default.CustomerCriteria));

  public object Deserialize(SerializationInfo info)
    => JsonSerializer.Deserialize(info.GetValue<string>("s")!, AppJsonContext.Default.CustomerCriteria)!;
}
```

Register it the same way as any custom serializer:

```csharp
services.AddCsla(options => options
  .Serialization(s => s
    .UseMobileFormatter(m => m
      .CustomSerializers.Add(
        new TypeMap<CustomerCriteria, CustomerCriteriaSerializer>(CustomerCriteriaSerializer.CanSerialize)))));
```

## APIs to avoid

These APIs reflect over arbitrary objects or resolve types by name. They are marked `[RequiresUnreferencedCode]` (IL2026) or `[RequiresDynamicCode]` (IL3050). Don't call them from code that will be trimmed; write the equivalent code directly.

| API | Do instead |
|---|---|
| `MethodCaller.CallMethod`, `CallMethodIfImplemented`, `IsMethodImplemented`, `CallMethodTryAsync`, `IsAsyncMethod` (object and method name overloads) | Call the method directly, or define an interface |
| `MethodCaller.CallPropertyGetter`, `CallPropertySetter`, `Utilities.CallByName` | Use the property directly |
| `MethodCaller.CallGenericMethod`, `CallGenericStaticMethodAsync` | Call the generic method with type arguments known at compile time |
| `MethodCaller.GetType(string)` and the other name-based `GetType` overloads | Use `typeof` |
| `MethodCaller.GetPropertyDescriptor` | Use the property directly |
| `LateBoundObject` | Call members directly |
| `ServiceProviderMethodCaller.FindDataPortalMethod<T>(object, object[])` | Use the data portal |
| `DataMapper.Map`, `SetPropertyValue`, `SetFieldValue`, `GetFieldValue`, and `ObjectAdapter` | Assign properties explicitly, for example in the operation method |
| `PocoSerializer<T>` | `[AutoSerializable]`, or a serializer that uses source-generated JSON (see above) |

## Data portal servers

A data portal host (`HttpPortal`, gRPC, RabbitMQ or WCF) receives business type names from the client and resolves them with `Type.GetType`. The trimmer can't see those names. A trimmed or NativeAOT data portal server must keep every business type a client can send, including criteria and child types. The simplest way is to root the business library:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="MyApp.Library" />
</ItemGroup>
```

Clients usually don't need this, because every type they use goes through `IDataPortal<T>` or `IChildDataPortal<T>`, which preserve it.

`HttpPortalController` is an ASP.NET Core MVC controller, and MVC doesn't support NativeAOT. In a NativeAOT ASP.NET Core server, map a minimal API endpoint that does what the controller does: deserialize the request, call `HttpPortal`, and serialize the response.

```csharp
using Csla;
using Csla.Serialization;
using Csla.Server.Hosts;
using Csla.Server.Hosts.DataPortalChannel;

app.MapPost("/api/dataportal", async (HttpContext http, string operation, ApplicationContext applicationContext) =>
{
  var formatter = applicationContext.GetRequiredService<ISerializationFormatter>();
  var portal = applicationContext.CreateInstanceDI<HttpPortal>();

  using var body = new MemoryStream();
  await http.Request.Body.CopyToAsync(body);
  var request = formatter.Deserialize(body.ToArray())!;

  var response = operation switch
  {
    "create" => await portal.Create((CriteriaRequest)request),
    "fetch" => await portal.Fetch((CriteriaRequest)request),
    "update" => await portal.Update((UpdateRequest)request),
    "delete" => await portal.Delete((CriteriaRequest)request),
    _ => throw new InvalidOperationException(operation),
  };

  http.Response.ContentType = "application/octet-stream";
  await http.Response.Body.WriteAsync(formatter.Serialize(response));
});
```

This endpoint handles the default `HttpProxy` settings (binary serialization, no routing or version tags). If clients use `UseTextSerialization` or data portal routing tags, handle those the same way `HttpPortalController` does.

## Blazor

Blazor WebAssembly always trims, and the CSLA Blazor packages are trim compatible.

* `ViewModel<T>.GetPropertyInfo` and `Csla.Blazor.PropertyInfo` read and write the model's properties by name. They assume the model is a CSLA business object, whose members the base classes preserve. If you use `PropertyInfo` with a model of another type, preserve that type's public properties, for example with `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]` on the class.
* Use `[HasPermission(AuthorizationActions.EditObject, typeof(PersonEdit))]` or `CslaPolicy.GetPolicy(...)` for CSLA permission policies. They preserve the business type. A `Csla:` policy name written by hand as a string, for example in `[Authorize(Policy = "...")]` or configuration, names a type the trimmer can't see, so the app must preserve that type itself.

ASP.NET Core MVC and Razor Pages don't support trimming. The CSLA MVC tag helpers, `CslaModelBinder` and `PageModel<T>` are for apps that aren't trimmed.

## MAUI and other XAML apps

`Csla.Xaml.PropertyInfo` walks a string binding path (`Path`) over its binding source, which can be any object, so it is marked `[RequiresUnreferencedCode]`. A trimmed app gets an IL2026 warning where it uses the control, as it does for MAUI's own string-path bindings. If you use it, make sure every type along the binding path is preserved. Business objects are preserved already; a view model or other object on the path must be preserved by the app.

WinForms and WPF don't support trimming, so `Csla.Windows` and `Csla.Xaml` for WPF aren't annotated.

## Suppressing a warning

Fix warnings rather than suppressing them. When a warning is a false positive, because you can show the reflected members are preserved, suppress it on the narrowest member with `[UnconditionalSuppressMessage]` and a justification that says why it holds:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2072",
  Justification = "The model is always a CSLA business object; the base classes preserve all of its members.")]
```

Don't use `<NoWarn>` for `IL` warnings, and don't use `#pragma warning disable` for them. The pragma only silences the analyzer, not the trimmer.

## Related

* [DataPortalOperationDispatch.md](DataPortalOperationDispatch.md) - the operations generator and why business classes are `partial`
* [Properties.md](Properties.md) - `[CslaImplementProperties]` and property declarations
* [Serialization.md](Serialization.md) - MobileFormatter and `[AutoSerializable]`
* [CustomSerializers.md](CustomSerializers.md) - writing and registering `IMobileSerializer` implementations
* [UpgradingToCsla11.md](UpgradingToCsla11.md) - all CSLA 11 changes, including the trimming annotations
