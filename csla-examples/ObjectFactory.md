# ObjectFactory Data Portal Model

By default the data portal calls data portal operation methods (`[Create]`, `[Fetch]`, `[Update]`, and so on) on the business object itself. This is the *encapsulated* model. As an alternative, the **ObjectFactory** model puts the data access code in a separate factory class. You decorate the business class with `[ObjectFactory]`, and the data portal calls methods on the factory instead of the business object.

> **Version applicability**
>
> - The `[ObjectFactory]` attribute and the `ObjectFactory` base class apply to all supported CSLA versions (9, 10, and 11).
> - **Multiple criteria parameters and method-level `[Inject]` parameters on factory methods** require **CSLA 10.2 or later**. In earlier versions a factory method takes no criteria or exactly one criteria parameter, and gets dependencies only through its constructor.
> - **CSLA 11:** creating a *child* of an `[ObjectFactory]` type calls the business class's `[CreateChild]` method. See [Child Create in CSLA 11](#child-create-in-csla-11).

The encapsulated model is recommended for most applications (see [DataPortalGuide.md](DataPortalGuide.md)). Use the ObjectFactory model when you need strict separation between the business class and its persistence code. The trade-off is that the factory has to manipulate the business object's internal state with helper methods such as `LoadProperty`, `MarkOld`, and `BypassPropertyChecks`.

## Key Types

Both types are in the `Csla.Server` namespace:

| Type | Purpose |
| --- | --- |
| `ObjectFactoryAttribute` (`[ObjectFactory]`) | Applied to the business class. Names the factory type and, optionally, the factory method names |
| `ObjectFactory` | Optional base class for factory classes. Its constructor requires an `ApplicationContext` and it provides helpers for loading and marking business objects |

## The [ObjectFactory] Attribute

```csharp
using Csla;
using Csla.Server;

[Serializable]
[ObjectFactory(typeof(PersonFactory))]
public class PersonEdit : BusinessBase<PersonEdit>
{
    public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
    public int Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public string Name
    {
        get => GetProperty(NameProperty);
        set => SetProperty(NameProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Csla.Rules.CommonRules.Required(NameProperty));
    }

    // No data portal operation methods here. The factory does the data access.
}
```

The factory type can be given as a `Type` or as an assembly-qualified type name string:

```csharp
[ObjectFactory(typeof(PersonFactory))]
[ObjectFactory("MyApp.DataAccess.PersonFactory, MyApp.DataAccess")]
```

The string form lets the business assembly avoid referencing the assembly that contains the factory.

### Factory Method Names

By default the data portal calls factory methods named `Create`, `Fetch`, `Update`, `Delete`, and `Execute`. Constructor overloads let you change the names:

```csharp
// factory type, fetch method
[ObjectFactory(typeof(PersonFactory), "GetPerson")]

// factory type, create method, fetch method
[ObjectFactory(typeof(PersonFactory), "NewPerson", "GetPerson")]

// factory type, create, fetch, update, delete
[ObjectFactory(typeof(PersonFactory), "NewPerson", "GetPerson", "SavePerson", "DeletePerson")]

// factory type, create, fetch, update, delete, execute
[ObjectFactory(typeof(PersonFactory), "NewPerson", "GetPerson", "SavePerson", "DeletePerson", "Run")]
```

The same overloads exist for the string form, for example `[ObjectFactory("MyApp.DataAccess.PersonFactory, MyApp.DataAccess", "GetPerson")]`.

## Which Factory Method Is Called

| Client call | Factory method called | Parameters |
| --- | --- | --- |
| `IDataPortal<T>.CreateAsync(criteria...)` | `Create` | Criteria values |
| `IDataPortal<T>.FetchAsync(criteria...)` | `Fetch` | Criteria values |
| `obj.SaveAsync()` / `IDataPortal<T>.UpdateAsync(obj)` | `Update` | The business object (new, existing, or marked for deletion) |
| `IDataPortal<T>.DeleteAsync(criteria...)` | `Delete` | Criteria values (immediate delete) |
| `IDataPortal<T>.ExecuteAsync(command)` for a command object | `Execute` | The command object |
| `IDataPortal<T>.ExecuteAsync(criteria...)` for a command type | `Execute` | Criteria values |

Factory methods use the names from the attribute, not operation attributes. Don't put `[Create]` or `[Fetch]` on factory methods. They aren't used to find factory methods.

When several overloads have the same name, the data portal picks the one whose criteria parameters best match the values the client passed, the same way it does for encapsulated methods.

`Create` and `Fetch` return the business object. `Update` returns the object after saving it. `Execute` returns the command object. Methods can be synchronous or async (`Task<T>`). `Delete` returns `void` or `Task`.

The `[RunLocal]` attribute can be applied to a factory method to run it on the client instead of a remote data portal server.

## The Factory Class

A factory class usually inherits from `Csla.Server.ObjectFactory`. The base class constructor requires an `ApplicationContext`, so your factory needs a constructor that takes one:

```csharp
using Csla;
using Csla.Server;

public class PersonFactory : ObjectFactory
{
    public PersonFactory(ApplicationContext applicationContext)
        : base(applicationContext) { }

    public PersonEdit Create()
    {
        var obj = ApplicationContext.CreateInstanceDI<PersonEdit>();
        LoadProperty(obj, PersonEdit.NameProperty, string.Empty);
        MarkNew(obj);
        CheckRules(obj);
        return obj;
    }

    public PersonEdit Fetch(int id)
    {
        var obj = ApplicationContext.CreateInstanceDI<PersonEdit>();
        // ... read data for id ...
        LoadProperty(obj, PersonEdit.IdProperty, id);
        LoadProperty(obj, PersonEdit.NameProperty, "Loaded name");
        MarkOld(obj);
        CheckRules(obj);
        return obj;
    }

    public PersonEdit Update(PersonEdit obj)
    {
        if (obj.IsDeleted)
        {
            if (!obj.IsNew)
            {
                // delete the data for obj.Id
            }
            MarkNew(obj);
        }
        else if (obj.IsNew)
        {
            // insert the data and get the new id
            LoadProperty(obj, PersonEdit.IdProperty, 123);
            MarkOld(obj);
        }
        else
        {
            // update the data
            MarkOld(obj);
        }
        return obj;
    }

    public void Delete(int id)
    {
        // delete the data for id
    }
}
```

Always create business objects with `ApplicationContext.CreateInstanceDI<T>()`, never with `new`. The data portal needs the object to be created through DI.

### Factory Creation and Constructor Injection

The default `ObjectFactoryLoader` resolves the type name from the attribute with `Type.GetType` and creates the factory with `ApplicationContext.CreateInstanceDI`. You don't need to register the factory in DI. Its constructor parameters are resolved from the service provider, so constructor injection works in every version:

```csharp
public class PersonFactory : ObjectFactory
{
    private readonly IPersonDal _dal;

    public PersonFactory(ApplicationContext applicationContext, IPersonDal dal)
        : base(applicationContext)
    {
        _dal = dal;
    }

    public async Task<PersonEdit> Fetch(int id)
    {
        var data = await _dal.GetAsync(id);
        var obj = ApplicationContext.CreateInstanceDI<PersonEdit>();
        LoadProperty(obj, PersonEdit.IdProperty, data.Id);
        LoadProperty(obj, PersonEdit.NameProperty, data.Name);
        MarkOld(obj);
        await CheckRulesAsync(obj);
        return obj;
    }
}
```

To control how factory types are resolved, implement `IObjectFactoryLoader` (`GetFactoryType` and `GetFactory`) and register it on the server-side data portal:

```csharp
services.AddCsla(o => o
    .DataPortal(dp => dp
        .AddServerSideDataPortal(s => s
            .RegisterObjectFactoryLoader<MyFactoryLoader>())));
```

## Multiple Criteria Parameters and [Inject] (CSLA 10.2+)

Starting with CSLA 10.2, the factory data portal finds and calls factory methods the same DI-aware way it calls encapsulated `[Fetch]`/`[Create]` methods. This means a factory method can:

- take **multiple criteria parameters**, and
- take **method-level `[Inject]` parameters** resolved from the service provider.

This works for `Create`, `Fetch`, `Update`, `Delete`, and `Execute`.

```csharp
using Csla;
using Csla.Server;

public class OrderFactory : ObjectFactory
{
    public OrderFactory(ApplicationContext applicationContext)
        : base(applicationContext) { }

    // Two criteria parameters plus an injected DAL
    public async Task<OrderEdit> Create(int customerId, string region, [Inject] IOrderDal dal)
    {
        var defaults = await dal.GetDefaultsAsync(customerId, region);
        var obj = ApplicationContext.CreateInstanceDI<OrderEdit>();
        LoadProperty(obj, OrderEdit.CustomerIdProperty, customerId);
        LoadProperty(obj, OrderEdit.RegionProperty, region);
        LoadProperty(obj, OrderEdit.ShipViaProperty, defaults.ShipVia);
        MarkNew(obj);
        await CheckRulesAsync(obj);
        return obj;
    }

    // Overloads are matched by criteria count and type
    public async Task<OrderEdit> Fetch(int id, [Inject] IOrderDal dal)
    {
        var data = await dal.GetAsync(id);
        return await LoadAsync(data);
    }

    public async Task<OrderEdit> Fetch(string orderNumber, int year, [Inject] IOrderDal dal)
    {
        var data = await dal.GetByNumberAsync(orderNumber, year);
        return await LoadAsync(data);
    }

    // The business object is the criteria parameter; services are injected
    public async Task<OrderEdit> Update(OrderEdit obj, [Inject] IOrderDal dal)
    {
        if (obj.IsDeleted)
        {
            if (!obj.IsNew)
                await dal.DeleteAsync(obj.Id);
            MarkNew(obj);
        }
        else if (obj.IsNew)
        {
            var id = await dal.InsertAsync(obj.CustomerId, obj.Region, obj.ShipVia);
            LoadProperty(obj, OrderEdit.IdProperty, id);
            MarkOld(obj);
        }
        else
        {
            await dal.UpdateAsync(obj.Id, obj.Region, obj.ShipVia);
            MarkOld(obj);
        }
        return obj;
    }

    public async Task Delete(int id, [Inject] IOrderDal dal)
    {
        await dal.DeleteAsync(id);
    }

    private async Task<OrderEdit> LoadAsync(OrderDto data)
    {
        var obj = ApplicationContext.CreateInstanceDI<OrderEdit>();
        LoadProperty(obj, OrderEdit.IdProperty, data.Id);
        LoadProperty(obj, OrderEdit.CustomerIdProperty, data.CustomerId);
        LoadProperty(obj, OrderEdit.RegionProperty, data.Region);
        LoadProperty(obj, OrderEdit.ShipViaProperty, data.ShipVia);
        MarkOld(obj);
        await CheckRulesAsync(obj);
        return obj;
    }
}
```

Client code passes criteria exactly as it does for encapsulated objects:

```csharp
var newOrder = await orderPortal.CreateAsync(42, "West");
var order = await orderPortal.FetchAsync(1001);
var sameOrder = await orderPortal.FetchAsync("SO-1001", 2026);
order = await order.SaveAsync();
await orderPortal.DeleteAsync(1001);
```

`[Inject]` on factory methods follows the same rules as on encapsulated methods, including `AllowNull`, nullable reference types, and (CSLA 10.1+) keyed services with `[Inject(Key = "...")]`. See the `InjectAttribute` documentation.

If no DI-aware match is found, the data portal falls back to the older behavior: a parameterless method, or a method with a single criteria parameter.

### Command Objects

```csharp
[Serializable]
[ObjectFactory(typeof(RecalculateTotalsFactory))]
public class RecalculateTotals : CommandBase<RecalculateTotals>
{
    public static readonly PropertyInfo<int> CustomerIdProperty = RegisterProperty<int>(nameof(CustomerId));
    public int CustomerId
    {
        get => ReadProperty(CustomerIdProperty);
        set => LoadProperty(CustomerIdProperty, value);
    }

    public static readonly PropertyInfo<decimal> TotalProperty = RegisterProperty<decimal>(nameof(Total));
    public decimal Total
    {
        get => ReadProperty(TotalProperty);
        set => LoadProperty(TotalProperty, value);
    }
}

public class RecalculateTotalsFactory : ObjectFactory
{
    public RecalculateTotalsFactory(ApplicationContext applicationContext)
        : base(applicationContext) { }

    [RunLocal]
    public RecalculateTotals Create(int customerId)
    {
        var cmd = ApplicationContext.CreateInstanceDI<RecalculateTotals>();
        cmd.CustomerId = customerId;
        return cmd;
    }

    public async Task<RecalculateTotals> Execute(RecalculateTotals command, [Inject] IOrderDal dal)
    {
        command.Total = await dal.RecalculateAsync(command.CustomerId);
        return command;
    }
}

// Client
var cmd = await commandPortal.CreateAsync(42);
cmd = await commandPortal.ExecuteAsync(cmd);
```

## ObjectFactory Base Class Helpers

The `ObjectFactory` base class exposes these `protected` members:

| Member | Purpose |
| --- | --- |
| `ApplicationContext` | The current `ApplicationContext` (use it to call `CreateInstanceDI`) |
| `LoadProperty(obj, propertyInfo, value)` | Sets a managed property without authorization checks, rules, or change events |
| `ReadProperty(obj, propertyInfo)` | Reads a managed property without authorization checks |
| `BypassPropertyChecks(businessObject)` | Returns an `IDisposable`. Inside a `using` block you can set properties through their public setters with no authorization or rule checks |
| `MarkNew(obj)` | Marks the object as new |
| `MarkOld(obj)` | Marks the object as old (not new, not dirty) |
| `MarkAsChild(obj)` | Marks the object as a child |
| `CheckRules(obj)` / `CheckRulesAsync(obj)` | Runs the object's business rules |
| `WaitForIdle(obj)` / `WaitForIdle(obj, ct)` | Waits for async rules to finish |
| `SetIsReadOnly(obj, value)` | Sets `IsReadOnly` on read-only lists |
| `FieldExists(obj, propertyInfo)` | Checks whether a managed field has been loaded |
| `GetDeletedList<C>(list)` | Gets the deleted items of an editable collection |

`LoadProperty` and `ReadProperty` work on any object that implements `IManageProperties`, which includes all CSLA business base classes.

### Using BypassPropertyChecks

```csharp
var obj = ApplicationContext.CreateInstanceDI<PersonEdit>();
using (BypassPropertyChecks(obj))
{
    obj.Name = data.Name;
}
MarkOld(obj);
```

`BypassPropertyChecks` takes a `BusinessBase`, and only properties with public setters can be set this way. Use `LoadProperty` for properties with private setters, such as `Id` in the examples above.

## Parent-Child Objects

With the ObjectFactory model, the factory for the root object usually loads and saves the whole object graph itself. Mark each child as a child before you add it to a list:

```csharp
public async Task<OrderEdit> Fetch(int id, [Inject] IOrderDal dal)
{
    var data = await dal.GetAsync(id);
    var order = ApplicationContext.CreateInstanceDI<OrderEdit>();
    LoadProperty(order, OrderEdit.IdProperty, data.Id);

    var lines = ApplicationContext.CreateInstanceDI<LineItems>();
    MarkAsChild(lines);
    foreach (var lineData in data.Lines)
    {
        var line = ApplicationContext.CreateInstanceDI<LineItem>();
        LoadProperty(line, LineItem.IdProperty, lineData.Id);
        LoadProperty(line, LineItem.ProductProperty, lineData.Product);
        LoadProperty(line, LineItem.QuantityProperty, lineData.Quantity);
        MarkAsChild(line);
        MarkOld(line);
        lines.Add(line);
    }
    LoadProperty(order, OrderEdit.LineItemsProperty, lines);

    MarkOld(order);
    await CheckRulesAsync(order);
    return order;
}
```

When you save, use `GetDeletedList<C>` to handle removed children, then clear the list:

```csharp
private async Task UpdateLineItemsAsync(OrderEdit order, IOrderDal dal)
{
    var deleted = GetDeletedList<LineItem>(order.LineItems);
    foreach (var line in deleted)
    {
        if (!line.IsNew)
            await dal.DeleteLineAsync(line.Id);
    }
    deleted.Clear();

    foreach (var line in order.LineItems)
    {
        if (line.IsNew)
        {
            var newId = await dal.InsertLineAsync(order.Id, line.Product, line.Quantity);
            LoadProperty(line, LineItem.IdProperty, newId);
        }
        else if (line.IsDirty)
        {
            await dal.UpdateLineAsync(line.Id, line.Product, line.Quantity);
        }
        MarkOld(line);
    }
}
```

## Child Create in CSLA 11

When code creates a child through the child data portal (`IChildDataPortal<T>.CreateChild` or `CreateChildAsync`) and `T` has an `[ObjectFactory]` attribute:

- **CSLA 11:** the data portal calls the **business class's `[CreateChild]` method**. A factory has no child-create method. If the class has no `[CreateChild]` method of its own, the base class default is used. `BusinessBase.Child_Create()` is marked `[CreateChild]` in CSLA 11 and runs the object's business rules.
- **CSLA 10 and earlier:** the data portal looked for a method named `Child_Create`, first on the factory and then on the business class. It didn't use `[CreateChild]` for factory types. CSLA 11 no longer does this. If you're upgrading, move that code into a `[CreateChild]` method on the business class.

So in CSLA 11, put child initialization for factory-based types in a `[CreateChild]` method on the business class:

```csharp
[Serializable]
[ObjectFactory(typeof(LineItemFactory))]
public class LineItem : BusinessBase<LineItem>
{
    public static readonly PropertyInfo<int> QuantityProperty = RegisterProperty<int>(nameof(Quantity));
    public int Quantity
    {
        get => GetProperty(QuantityProperty);
        set => SetProperty(QuantityProperty, value);
    }

    [CreateChild]
    private async Task CreateChild()
    {
        LoadProperty(QuantityProperty, 1);
        await BusinessRules.CheckRulesAsync();
    }
}

// In a parent, or in a factory that builds the parent:
var line = await childPortal.CreateChildAsync();
```

## Best Practices

1. **Prefer the encapsulated model.** Use ObjectFactory only when you need the factory separation.
2. **Inherit from `Csla.Server.ObjectFactory`** and pass `ApplicationContext` to the base constructor.
3. **Create objects with `ApplicationContext.CreateInstanceDI<T>()`.** Never use `new`.
4. **Always set object state.** Call `MarkNew` after create, `MarkOld` after fetch/insert/update, and `MarkNew` after deleting a persisted object in `Update`.
5. **Run rules after loading.** Call `CheckRules`/`CheckRulesAsync` after create and fetch.
6. **Use `LoadProperty` for loading data.** It avoids rules, authorization, and change events.
7. **On CSLA 10.2+, inject per-call dependencies with `[Inject]`.** On older versions, use constructor injection.
8. **Return the object** from `Create`, `Fetch`, `Update`, and `Execute`.

## Related Documentation

- [DataPortalGuide.md](DataPortalGuide.md) - Data portal models, including encapsulated invocation
- [DataPortalOperationCreate.md](DataPortalOperationCreate.md), [DataPortalOperationFetch.md](DataPortalOperationFetch.md), [DataPortalOperationUpdate.md](DataPortalOperationUpdate.md) - Encapsulated operation methods
- [Data-Access.md](Data-Access.md) - Data access layer patterns
