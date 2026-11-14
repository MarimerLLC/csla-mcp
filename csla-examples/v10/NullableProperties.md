# Nullable Reference Types and CSLA Properties

With nullable reference types enabled (`<Nullable>enable</Nullable>`), a hand-written CSLA property whose type is a non-nullable reference type gets **CS8603** ("Possible null reference return") in its getter. This is common when upgrading from CSLA 9.

```csharp
public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
public string Name
{
    get => GetProperty(NameProperty); // CS8603
    set => SetProperty(NameProperty, value);
}
```

Don't wrap getters in `#pragma warning disable CS8603`. Decide for each property whether it can actually be null, then either apply `!` or declare the property nullable.

## Why the Getters Return `P?`

`GetProperty`, `ReadProperty`, `LazyGetProperty`, `LazyReadProperty`, `LazyGetPropertyAsync` and `LazyReadPropertyAsync` all return `P?`. The annotation is accurate, because each method can return the property's default value instead of a stored value:

* **Read access denied**: if an authorization rule denies `ReadProperty`, `GetProperty` returns the default value (it doesn't throw).
* **Never loaded**: a property that was never set returns its default value.
* **Async lazy load in progress**: `LazyGetPropertyAsync` and `LazyReadPropertyAsync` start loading and return the default value immediately. The real value arrives later with a `PropertyChanged` event.
* **The value is null**: for example, a lazy-load value generator that returns `null`.

The default value of a `string` property is `string.Empty`, not `null`, unless a different default is registered. For other reference types it is `null`.

The synchronous `LazyGetProperty` does **not** return null on first access. It runs the value generator immediately and returns the result.

## Generated Properties Handle This Already

With `[CslaImplementProperties]` (see `Properties.md`), nullability follows the property declaration. The generator emits `GetProperty(XProperty)!` for a non-nullable property and a plain `GetProperty(XProperty)` for a nullable one:

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial string Name { get; set; }     // non-nullable: generated getter uses !
    public partial string? Notes { get; set; }   // nullable
}
```

Use the rules below for properties you write by hand, such as lazy-loaded children or properties with custom logic.

## Decision Rules for Hand-Written Properties

| Situation | Property type | Getter |
| --------- | ------------- | ------ |
| Always loaded, no read authorization rule | Non-nullable | `GetProperty(XProperty)!` |
| `string` property (defaults to `string.Empty`) | Non-nullable | `GetProperty(XProperty)!` |
| Sync lazy load whose generator always returns an object | Non-nullable | `LazyGetProperty(XProperty, ...)!` |
| Non-`string` property with a read authorization rule | Nullable (`T?`) | `GetProperty(XProperty)` |
| Async lazy load (`LazyGetPropertyAsync`) | Nullable (`T?`) | `LazyGetPropertyAsync(XProperty, ...)` |
| Value can legitimately be absent | Nullable (`T?`) | `GetProperty(XProperty)` |

**Keep the `PropertyInfo<T>` type argument non-nullable**, even when the property is nullable. Use `PropertyInfo<CreditSummary>` with a `CreditSummary?` property, not `PropertyInfo<CreditSummary?>`. With `PropertyInfo<ShipmentInfo?>`, passing a `Task<ShipmentInfo>` to `LazyGetPropertyAsync` produces **CS8620**.

## Examples

### Non-Nullable Property

```csharp
public static readonly PropertyInfo<string> CustomerNameProperty = RegisterProperty<string>(nameof(CustomerName));
public string CustomerName
{
    get => GetProperty(CustomerNameProperty)!;
    set => SetProperty(CustomerNameProperty, value);
}
```

### Synchronous Lazy-Loaded Child

`LazyGetProperty` fetches the child on first access and returns it, so the property stays non-nullable:

```csharp
public static readonly PropertyInfo<LineItemList> LineItemsProperty =
    RegisterProperty<LineItemList>(nameof(LineItems), RelationshipTypes.LazyLoad);
public LineItemList LineItems
{
    get => LazyGetProperty(LineItemsProperty,
        () => ApplicationContext.GetRequiredService<IChildDataPortal<LineItemList>>().FetchChild(Id))!;
    private set => LoadProperty(LineItemsProperty, value);
}
```

The same pattern works when the generator fetches a root object through `IDataPortal<T>`:

```csharp
get => LazyGetProperty(FlexViewPanelsProperty,
    () => ApplicationContext.GetRequiredService<IDataPortal<FlexViewPanelChildGetter>>().Fetch(Id).FlexViewPanels)!;
```

### Asynchronous Lazy-Loaded Child

The first read returns `null`, so the property must be nullable. `LazyGetPropertyAsync` takes a `Task<P>`, and the task is already running when it's passed in. Check whether the value is loaded or loading first, or every read starts another fetch:

```csharp
public static readonly PropertyInfo<ShipmentInfo> ShipmentProperty =
    RegisterProperty<ShipmentInfo>(nameof(Shipment), RelationshipTypes.LazyLoad);
[SuppressMessage("Usage", "CSLA0007", Justification = "The guard keeps each read from starting another fetch.")]
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
```

Callers either handle `null` (UI data binding updates on `PropertyChanged`) or wait for the load:

```csharp
_ = order.Shipment;          // starts the load, returns null
await order.WaitForIdle();
var carrier = order.Shipment?.Carrier;
```

### Property with a Read Authorization Rule

A user who can't read the property gets `null`, so the property is nullable:

```csharp
public static readonly PropertyInfo<CreditSummary> CreditProperty = RegisterProperty<CreditSummary>(nameof(Credit));
public CreditSummary? Credit
{
    get => GetProperty(CreditProperty);
    private set => LoadProperty(CreditProperty, value);
}

protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.ReadProperty, CreditProperty, "Manager"));
}
```

## Analyzer CSLA0007 and `!`

In CSLA 10, analyzer **CSLA0007** ("Properties that use managed backing fields should only use Get/Set/Read/Load methods and nothing else") reports hand-written getters that end in `!`, such as `get => GetProperty(NameProperty)!;`. That's a false positive: the `!` adds no logic. CSLA 11 accepts it. In CSLA 10, either keep the `!` and ignore or suppress CSLA0007 for those properties, or use `[CslaImplementProperties]` for simple properties, whose generated code suppresses it already. Don't remove the `!` to avoid CSLA0007.

## Common Mistakes

* **`#pragma warning disable CS8603` around getters**: hides the decision. Use `!` instead.
* **Making a property nullable only to silence CS8603**: callers then have to null-check a value that's never null.
* **`PropertyInfo<T?>` for a nullable property**: keep the type argument non-nullable.
* **Non-nullable property with `LazyGetPropertyAsync`**: the first read is always `null`.
* **Creating the `Task` on every read in an async lazy getter**: starts a new fetch on each read. Guard it as shown above.

## Related Documents

* `Properties.md`: property declaration patterns
* `BusinessRulesAuthorization.md`: property authorization rules
* CSLA sample: `Samples/NullablePropertiesExample` in the CSLA repository
