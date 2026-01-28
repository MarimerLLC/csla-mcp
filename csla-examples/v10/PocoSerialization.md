# Serializing POCOs in CSLA 10

CSLA 10 introduces custom serializers as a cleaner way to serialize POCOs (Plain Old C# Objects), DTOs (Data Transfer Objects), and other types that don't inherit from CSLA base classes. The built-in `PocoSerializer<T>` makes this especially easy.

## Overview

In CSLA 10, instead of manually implementing `IMobileObject` on each POCO type, you can register a custom serializer during application startup. CSLA provides `PocoSerializer<T>` which uses `System.Text.Json` to automatically serialize any simple C# class with public read/write properties.

## Using PocoSerializer

### Basic Configuration

Configure `PocoSerializer<T>` for your POCO types in your application's startup:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla(options => options
        .Serialization(s => s
            .UseMobileFormatter(m => m
                .CustomSerializers.Add(
                    new TypeMap<CustomerCriteria, PocoSerializer<CustomerCriteria>>(
                        PocoSerializer<CustomerCriteria>.CanSerialize)))));
}
```

### Example POCO

Your POCO requires no special attributes or interfaces:

```csharp
public class CustomerCriteria
{
    public string Region { get; set; }
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public bool IsActive { get; set; }
}
```

### Using the POCO

Once configured, use the POCO anywhere in your CSLA application:

```csharp
// Business object using the criteria
[Fetch]
private async Task Fetch(CustomerCriteria criteria, [Inject] ICustomerDal dal)
{
    var data = await dal.GetByCriteria(
        criteria.Region,
        criteria.MinAge,
        criteria.MaxAge,
        criteria.IsActive);
    // Load data...
}

// Client code
var criteria = new CustomerCriteria
{
    Region = "West",
    MinAge = 25,
    MaxAge = 65,
    IsActive = true
};

var customers = await customerListPortal.FetchAsync(criteria);
```

## Registering Multiple POCOs

Register multiple POCO types in one configuration block:

```csharp
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m =>
        {
            m.CustomSerializers.Add(
                new TypeMap<CustomerCriteria, PocoSerializer<CustomerCriteria>>(
                    PocoSerializer<CustomerCriteria>.CanSerialize));

            m.CustomSerializers.Add(
                new TypeMap<OrderFilter, PocoSerializer<OrderFilter>>(
                    PocoSerializer<OrderFilter>.CanSerialize));

            m.CustomSerializers.Add(
                new TypeMap<AddressDto, PocoSerializer<AddressDto>>(
                    PocoSerializer<AddressDto>.CanSerialize));

            m.CustomSerializers.Add(
                new TypeMap<ProductSearchParams, PocoSerializer<ProductSearchParams>>(
                    PocoSerializer<ProductSearchParams>.CanSerialize));
        })));
```

## Complete Example

Here's a complete example showing configuration and usage:

### POCO Types

```csharp
// Simple DTO for address information
public class AddressDto
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }
}

// Search criteria with multiple parameters
public class AdvancedSearchCriteria
{
    public string SearchTerm { get; set; }
    public List<string> Categories { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
}
```

### Application Startup

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCsla(options => options
            .Serialization(s => s
                .UseMobileFormatter(m =>
                {
                    m.CustomSerializers.Add(
                        new TypeMap<AddressDto, PocoSerializer<AddressDto>>(
                            PocoSerializer<AddressDto>.CanSerialize));

                    m.CustomSerializers.Add(
                        new TypeMap<AdvancedSearchCriteria, PocoSerializer<AdvancedSearchCriteria>>(
                            PocoSerializer<AdvancedSearchCriteria>.CanSerialize));
                })));

        // Other service configuration...
    }
}
```

### Business Object

```csharp
[CslaImplementProperties]
public partial class ProductList : ReadOnlyListBase<ProductList, ProductInfo>
{
    [Fetch]
    private async Task Fetch(AdvancedSearchCriteria criteria, [Inject] IProductDal dal)
    {
        var products = await dal.AdvancedSearch(
            criteria.SearchTerm,
            criteria.Categories,
            criteria.MinPrice,
            criteria.MaxPrice,
            criteria.StartDate,
            criteria.EndDate,
            criteria.PageSize,
            criteria.PageNumber);

        using (LoadListMode)
        {
            foreach (var product in products)
            {
                Add(DataPortal.FetchChild<ProductInfo>(product));
            }
        }
    }
}
```

## Alternative: AutoSerializable Attribute

For POCOs you own and want to make serializable without configuration, CSLA 10 also provides the `[AutoSerializable]` attribute with source generation:

```csharp
using Csla.Serialization;

[AutoSerializable]
public partial class AddressDto
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }
}
```

This approach:
- Requires the `Csla.Generator.AutoSerialization.CSharp` NuGet package
- Class must be marked `partial`
- Generated code implements `IMobileObject` at compile time
- No runtime reflection needed
- No configuration required

## When to Use Each Approach

| Approach | Best For |
|----------|----------|
| `PocoSerializer<T>` | Third-party types, types you can't modify, quick configuration |
| `[AutoSerializable]` | Types you own, maximum performance, no runtime overhead |
| Manual `IMobileObject` | Complex serialization logic, custom child handling |

## PocoSerializer Requirements

`PocoSerializer<T>` works with types that:

- Have a parameterless constructor
- Use public read/write properties
- Contain types supported by `System.Text.Json`

Types with complex constructors or private setters may need a custom serializer.

## Creating a Custom Serializer

For more control, implement `IMobileSerializer`:

```csharp
using Csla.Serialization.Mobile;

public class CustomAddressSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(AddressDto);

    public void Serialize(object obj, SerializationInfo info)
    {
        var address = (AddressDto)obj;
        info.AddValue("Street", address.Street);
        info.AddValue("City", address.City);
        info.AddValue("State", address.State);
        info.AddValue("ZipCode", address.ZipCode);
        info.AddValue("Country", address.Country);
    }

    public object Deserialize(SerializationInfo info)
    {
        return new AddressDto
        {
            Street = info.GetValue<string>("Street"),
            City = info.GetValue<string>("City"),
            State = info.GetValue<string>("State"),
            ZipCode = info.GetValue<string>("ZipCode"),
            Country = info.GetValue<string>("Country")
        };
    }
}

// Register the custom serializer
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<AddressDto, CustomAddressSerializer>(
                    CustomAddressSerializer.CanSerialize)))));
```

## Migration from CSLA 9

In CSLA 9, you had to implement `IMobileObject` manually with `GetState`/`SetState` methods. In CSLA 10:

### Before (CSLA 9)

```csharp
public class CustomerCriteria : IMobileObject
{
    public string Region { get; set; }
    public int MinAge { get; set; }

    public void GetState(SerializationInfo info)
    {
        info.AddValue("Region", Region);
        info.AddValue("MinAge", MinAge);
    }

    public void SetState(SerializationInfo info)
    {
        Region = info.GetValue<string>("Region");
        MinAge = info.GetValue<int>("MinAge");
    }

    public void GetChildren(SerializationInfo info, MobileFormatter formatter) { }
    public void SetChildren(SerializationInfo info, MobileFormatter formatter) { }
}
```

### After (CSLA 10)

```csharp
// Simple POCO - no interface needed
public class CustomerCriteria
{
    public string Region { get; set; }
    public int MinAge { get; set; }
}

// Configuration at startup
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<CustomerCriteria, PocoSerializer<CustomerCriteria>>(
                    PocoSerializer<CustomerCriteria>.CanSerialize)))));
```

## Best Practices

1. **Register all POCO types at startup** - Ensure all types that pass through the data portal are registered.

2. **Use `[AutoSerializable]` for types you own** - Offers better performance than `PocoSerializer<T>`.

3. **Use `PocoSerializer<T>` for third-party types** - When you can't modify the type's source code.

4. **Test serialization round-trips** - Verify that serialization and deserialization produce identical objects.

5. **Initialize collection properties** - Avoid null reference exceptions:

```csharp
   public List<string> Tags { get; set; } = new();
```

6. **Consider passing simple values directly** - For criteria with just a few parameters:

```csharp
   [Fetch]
   private async Task Fetch(string region, int minAge, [Inject] ICustomerDal dal)
   {
       // Direct parameters - no serialization configuration needed
   }
```
