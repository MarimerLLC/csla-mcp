# Custom Serializers for MobileFormatter

CSLA's `MobileFormatter` supports custom serializers for types that are not normally serializable. This allows you to serialize POCO types, third-party types, or any custom types that don't implement `IMobileObject`.

## Overview

Custom serializers enable `MobileFormatter` to serialize types that it wouldn't normally be able to handle. CSLA includes two built-in custom serializers:

- `ClaimsPrincipalSerializer` - Serializes `ClaimsPrincipal` types (configured and active by default)
- `PocoSerializer<T>` - Serializes simple C# classes with public read/write properties using JSON

## Using the POCO Serializer

The POCO serializer uses `System.Text.Json` to serialize any simple C# class that has public read/write properties.

### Basic Configuration

Configure the POCO serializer in your application's startup code:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla(options => options
        .Serialization(s => s
            .UseMobileFormatter(m => m
                .CustomSerializers.Add(
                    new TypeMap<MyCustomType, PocoSerializer<MyCustomType>>(
                        PocoSerializer<MyCustomType>.CanSerialize)))));
}
```

### Example POCO Type

Here's an example of a POCO type that can be serialized:

```csharp
public class CustomerCriteria
{
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public string Region { get; set; }
    public bool IsActive { get; set; }
}
```

### Using with Data Portal

Once configured, you can use the POCO type as a criteria parameter:

```csharp
// Configure the serializer for CustomerCriteria
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<CustomerCriteria, PocoSerializer<CustomerCriteria>>(
                    PocoSerializer<CustomerCriteria>.CanSerialize)))));

// Business object using the criteria
[CslaImplementProperties]
public partial class CustomerList : ReadOnlyListBase<CustomerList, CustomerInfo>
{
    [Fetch]
    private async Task Fetch(CustomerCriteria criteria, [Inject] ICustomerDal dal)
    {
        var customers = await dal.GetByCriteria(
            criteria.MinAge,
            criteria.MaxAge,
            criteria.Region,
            criteria.IsActive);

        using (LoadListMode)
        {
            foreach (var customer in customers)
            {
                Add(DataPortal.FetchChild<CustomerInfo>(customer));
            }
        }
    }
}

// Client code
var criteria = new CustomerCriteria
{
    MinAge = 25,
    MaxAge = 65,
    Region = "West",
    IsActive = true
};

var customers = await customerListPortal.FetchAsync(criteria);
```

## Multiple Custom Serializers

You can register multiple custom serializers for different types:

```csharp
public void ConfigureServices(IServiceCollection services)
{
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
                    new TypeMap<ProductSearchParams, PocoSerializer<ProductSearchParams>>(
                        PocoSerializer<ProductSearchParams>.CanSerialize));
            })));
}
```

## Creating Your Own Custom Serializer

To create a custom serializer, implement the `IMobileSerializer` interface:

```csharp
using Csla.Serialization.Mobile;

public class MyCustomSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type)
    {
        // Return true if this serializer can handle the type
        return type == typeof(MyComplexType);
    }

    public void Serialize(object obj, SerializationInfo info)
    {
        if (obj is MyComplexType complex)
        {
            // Store serializable data in the SerializationInfo
            info.AddValue("Property1", complex.Property1);
            info.AddValue("Property2", complex.Property2);
            info.AddValue("Timestamp", complex.Timestamp);
        }
    }

    public object Deserialize(SerializationInfo info)
    {
        // Reconstruct the object from the SerializationInfo
        return new MyComplexType
        {
            Property1 = info.GetValue<string>("Property1"),
            Property2 = info.GetValue<int>("Property2"),
            Timestamp = info.GetValue<DateTime>("Timestamp")
        };
    }
}
```

### Registering a Custom Serializer

Register your custom serializer in the startup code:

```csharp
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<MyComplexType, MyCustomSerializer>(
                    MyCustomSerializer.CanSerialize)))));
```

## SerializationInfo Supported Types

The `SerializationInfo` class can store the following "primitive" types:

- All .NET primitive types (int, long, double, bool, etc.)
- `string`
- `DateTime`, `TimeSpan`, `DateTimeOffset`, `DateOnly`, `TimeOnly`
- `Guid`
- `byte[]`
- `char[]`

> ⚠️ Standard .NET collection types such as `List<T>` and `Dictionary<K,V>` are _not_ directly serializable by `MobileFormatter`. Use `MobileList<T>` or `MobileDictionary<K,V>` from `Csla.Core` for serializable collections, or register a custom serializer for your collection type.

## Common Scenarios

### Scenario 1: Serializing Third-Party DTOs

When using DTOs from third-party libraries that aren't serializable:

```csharp
// Third-party DTO
namespace ThirdParty.Models
{
    public class ApiRequest
    {
        public string Endpoint { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Payload { get; set; }
    }
}

// Configuration
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<ThirdParty.Models.ApiRequest, PocoSerializer<ThirdParty.Models.ApiRequest>>(
                    PocoSerializer<ThirdParty.Models.ApiRequest>.CanSerialize)))));
```

### Scenario 2: Complex Criteria Objects

For complex search criteria with multiple parameters:

```csharp
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

// Configure serializer
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<AdvancedSearchCriteria, PocoSerializer<AdvancedSearchCriteria>>(
                    PocoSerializer<AdvancedSearchCriteria>.CanSerialize)))));

// Use in business object
[Fetch]
private async Task Fetch(AdvancedSearchCriteria criteria, [Inject] IProductDal dal)
{
    var products = await dal.AdvancedSearch(criteria);
    // Load products...
}
```

### Scenario 3: Custom Serializer for Binary Data

When you need to optimize serialization of large binary data:

```csharp
public class ImageDataSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(ImageData);

    public void Serialize(object obj, SerializationInfo info)
    {
        var imageData = (ImageData)obj;
        info.AddValue("ImageBytes", imageData.Bytes);
        info.AddValue("Format", imageData.Format);
        info.AddValue("Width", imageData.Width);
        info.AddValue("Height", imageData.Height);
    }

    public object Deserialize(SerializationInfo info)
    {
        return new ImageData
        {
            Bytes = info.GetValue<byte[]>("ImageBytes"),
            Format = info.GetValue<string>("Format"),
            Width = info.GetValue<int>("Width"),
            Height = info.GetValue<int>("Height")
        };
    }
}

// Register
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<ImageData, ImageDataSerializer>(
                    ImageDataSerializer.CanSerialize)))));
```

### Scenario 4: Using JSON for Complex Nested Types

When you have complex nested types that are easier to serialize with JSON:

```csharp
public class ComplexConfigSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(ComplexConfig);

    public void Serialize(object obj, SerializationInfo info)
    {
        var config = (ComplexConfig)obj;
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        info.AddValue("ConfigJson", json);
    }

    public object Deserialize(SerializationInfo info)
    {
        var json = info.GetValue<string>("ConfigJson");
        return System.Text.Json.JsonSerializer.Deserialize<ComplexConfig>(json);
    }
}
```

## Best Practices

1. **Use PocoSerializer for simple types** - If your type has only public read/write properties, use `PocoSerializer<T>`
2. **Implement custom serializers for complex types** - For types with complex logic or non-public members, create a custom serializer
3. **Follow naming conventions** - Implement a static `CanSerialize` method by convention
4. **Store only primitive types** - Only store types supported by `SerializationInfo` in your custom serializer
5. **Register all serializers at startup** - Configure all custom serializers in your `ConfigureServices` method
6. **Test serialization round-trips** - Always test that your serializer can serialize and deserialize objects correctly
7. **Consider performance** - JSON serialization (used by `PocoSerializer`) has a performance cost; for high-performance scenarios, consider custom binary serialization

## Migration from CriteriaBase

In CSLA 9, `CriteriaBase` is obsolete. Instead of inheriting from `CriteriaBase`, use one of these approaches:

### Option 1: Simple Parameters (Recommended)

Pass criteria values directly as parameters:

```csharp
[Fetch]
private async Task Fetch(string region, int minAge, int maxAge, [Inject] ICustomerDal dal)
{
    var customers = await dal.GetByCriteria(region, minAge, maxAge);
    // Load data...
}
```

### Option 2: POCO with Custom Serializer

Create a POCO type and register a custom serializer:

```csharp
public class CustomerCriteria
{
    public string Region { get; set; }
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
}

// Register serializer
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<CustomerCriteria, PocoSerializer<CustomerCriteria>>(
                    PocoSerializer<CustomerCriteria>.CanSerialize)))));
```

### Option 3: ReadOnlyBase (For Complex Criteria)

For complex criteria with validation logic, use `ReadOnlyBase`:

```csharp
[Serializable]
public class CustomerCriteria : ReadOnlyBase<CustomerCriteria>
{
    public static readonly PropertyInfo<string> RegionProperty =
        RegisterProperty<string>(nameof(Region));
    public string Region
    {
        get => GetProperty(RegionProperty);
        private set => LoadProperty(RegionProperty, value);
    }

    public static readonly PropertyInfo<int> MinAgeProperty =
        RegisterProperty<int>(nameof(MinAge));
    public int MinAge
    {
        get => GetProperty(MinAgeProperty);
        private set => LoadProperty(MinAgeProperty, value);
    }

    // Additional properties and business rules...
}
```

## Notes

- The `ClaimsPrincipalSerializer` is configured and active by default in CSLA 9+
- Custom serializers are called by `MobileFormatter` when it encounters a type it can't serialize natively
- The `CanSerialize` method is used to determine if a serializer can handle a specific type
- Multiple serializers can be registered, and `MobileFormatter` will use the first one that returns `true` from `CanSerialize`
