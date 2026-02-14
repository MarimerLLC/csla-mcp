# Creating Custom Serializers in CSLA 10

CSLA 10 allows you to create custom serializers to handle types that `MobileFormatter` cannot serialize natively. This is useful for third-party types, types with special serialization requirements, or optimized serialization of specific data structures.

## Overview

Custom serializers implement the `IMobileSerializer` interface and are registered at application startup using a `TypeMap`. When `MobileFormatter` encounters a type it cannot serialize, it checks registered custom serializers to find one that can handle the type.

## The IMobileSerializer Interface

To create a custom serializer, implement the `IMobileSerializer` interface:

```csharp
using Csla.Serialization.Mobile;

public interface IMobileSerializer
{
    void Serialize(object obj, SerializationInfo info);
    object Deserialize(SerializationInfo info);
}
```

By convention, custom serializers also include a static `CanSerialize` method used during registration:

```csharp
public static bool CanSerialize(Type type);
```

## Basic Custom Serializer Example

Here's a complete example of a custom serializer for an `AddressInfo` type:

```csharp
using Csla.Serialization.Mobile;

public class AddressInfo
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }
}

public class AddressInfoSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(AddressInfo);

    public void Serialize(object obj, SerializationInfo info)
    {
        var address = (AddressInfo)obj;
        info.AddValue("Street", address.Street);
        info.AddValue("City", address.City);
        info.AddValue("State", address.State);
        info.AddValue("ZipCode", address.ZipCode);
        info.AddValue("Country", address.Country);
    }

    public object Deserialize(SerializationInfo info)
    {
        return new AddressInfo
        {
            Street = info.GetValue<string>("Street"),
            City = info.GetValue<string>("City"),
            State = info.GetValue<string>("State"),
            ZipCode = info.GetValue<string>("ZipCode"),
            Country = info.GetValue<string>("Country")
        };
    }
}
```

## Registering a Custom Serializer

Register your custom serializer during application startup using `TypeMap`:

```csharp
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<AddressInfo, AddressInfoSerializer>(
                    AddressInfoSerializer.CanSerialize)))));
```

The `TypeMap` constructor takes:
1. **Type parameters**: The target type and the serializer type
2. **CanSerialize delegate**: A function that determines if this serializer handles a given type

## SerializationInfo Supported Types

The `SerializationInfo` class can store these primitive types:

| Type Category | Supported Types |
|--------------|-----------------|
| Numeric | `int`, `long`, `short`, `byte`, `uint`, `ulong`, `ushort`, `sbyte`, `float`, `double`, `decimal` |
| Text | `string`, `char`, `char[]` |
| Boolean | `bool` |
| Date/Time | `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` |
| Other | `Guid`, `byte[]`, `char[]` |
| Nullable | Nullable versions of all above types |

> ⚠️ Standard .NET collection types such as `List<T>` and `Dictionary<K,V>` are _not_ directly serializable by `MobileFormatter`. Use `MobileList<T>` or `MobileDictionary<K,V>` from `Csla.Core` for serializable collections, or register a custom serializer for your collection type.

For complex types, serialize them as JSON strings or implement child object handling.

## Handling Complex Types

### Using JSON for Nested Objects

For types with complex nested structures, serialize as JSON:

```csharp
using System.Text.Json;
using Csla.Serialization.Mobile;

public class ConfigSettings
{
    public string Name { get; set; }
    public Dictionary<string, string> Options { get; set; }
    public List<FeatureFlag> Features { get; set; }
}

public class ConfigSettingsSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(ConfigSettings);

    public void Serialize(object obj, SerializationInfo info)
    {
        var config = (ConfigSettings)obj;
        var json = JsonSerializer.Serialize(config);
        info.AddValue("ConfigJson", json);
    }

    public object Deserialize(SerializationInfo info)
    {
        var json = info.GetValue<string>("ConfigJson");
        return JsonSerializer.Deserialize<ConfigSettings>(json);
    }
}
```

### Handling Null Values

Always handle null values properly:

```csharp
public class PersonSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(Person);

    public void Serialize(object obj, SerializationInfo info)
    {
        var person = (Person)obj;
        info.AddValue("FirstName", person.FirstName ?? string.Empty);
        info.AddValue("LastName", person.LastName ?? string.Empty);
        info.AddValue("MiddleName", person.MiddleName); // Can be null
        info.AddValue("HasMiddleName", person.MiddleName != null);
    }

    public object Deserialize(SerializationInfo info)
    {
        var hasMiddleName = info.GetValue<bool>("HasMiddleName");
        return new Person
        {
            FirstName = info.GetValue<string>("FirstName"),
            LastName = info.GetValue<string>("LastName"),
            MiddleName = hasMiddleName ? info.GetValue<string>("MiddleName") : null
        };
    }
}
```

## Advanced Scenarios

### Serializing Binary Data

For optimized binary data handling:

```csharp
public class ImageData
{
    public byte[] Bytes { get; set; }
    public string Format { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ImageDataSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(ImageData);

    public void Serialize(object obj, SerializationInfo info)
    {
        var image = (ImageData)obj;
        info.AddValue("Bytes", image.Bytes);
        info.AddValue("Format", image.Format);
        info.AddValue("Width", image.Width);
        info.AddValue("Height", image.Height);
    }

    public object Deserialize(SerializationInfo info)
    {
        return new ImageData
        {
            Bytes = info.GetValue<byte[]>("Bytes"),
            Format = info.GetValue<string>("Format"),
            Width = info.GetValue<int>("Width"),
            Height = info.GetValue<int>("Height")
        };
    }
}
```

### Serializing Types with Constructors

For types without parameterless constructors:

```csharp
public class Money
{
    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }
}

public class MoneySerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(Money);

    public void Serialize(object obj, SerializationInfo info)
    {
        var money = (Money)obj;
        info.AddValue("Amount", money.Amount);
        info.AddValue("Currency", money.Currency);
    }

    public object Deserialize(SerializationInfo info)
    {
        return new Money(
            info.GetValue<decimal>("Amount"),
            info.GetValue<string>("Currency"));
    }
}
```

### Serializing Type Hierarchies

Handle inheritance with a type discriminator:

```csharp
public abstract class Shape
{
    public string Color { get; set; }
}

public class Circle : Shape
{
    public double Radius { get; set; }
}

public class Rectangle : Shape
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public class ShapeSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) =>
        type == typeof(Shape) || type == typeof(Circle) || type == typeof(Rectangle);

    public void Serialize(object obj, SerializationInfo info)
    {
        var shape = (Shape)obj;
        info.AddValue("Color", shape.Color);
        info.AddValue("ShapeType", obj.GetType().Name);

        switch (obj)
        {
            case Circle circle:
                info.AddValue("Radius", circle.Radius);
                break;
            case Rectangle rect:
                info.AddValue("Width", rect.Width);
                info.AddValue("Height", rect.Height);
                break;
        }
    }

    public object Deserialize(SerializationInfo info)
    {
        var shapeType = info.GetValue<string>("ShapeType");
        var color = info.GetValue<string>("Color");

        return shapeType switch
        {
            "Circle" => new Circle
            {
                Color = color,
                Radius = info.GetValue<double>("Radius")
            },
            "Rectangle" => new Rectangle
            {
                Color = color,
                Width = info.GetValue<double>("Width"),
                Height = info.GetValue<double>("Height")
            },
            _ => throw new InvalidOperationException($"Unknown shape type: {shapeType}")
        };
    }
}

// Register for all types in the hierarchy
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m =>
        {
            m.CustomSerializers.Add(
                new TypeMap<Shape, ShapeSerializer>(ShapeSerializer.CanSerialize));
            m.CustomSerializers.Add(
                new TypeMap<Circle, ShapeSerializer>(ShapeSerializer.CanSerialize));
            m.CustomSerializers.Add(
                new TypeMap<Rectangle, ShapeSerializer>(ShapeSerializer.CanSerialize));
        })));
```

### Generic Serializer for Multiple Types

Create a reusable generic serializer:

```csharp
public class JsonWrapperSerializer<T> : IMobileSerializer where T : class
{
    public static bool CanSerialize(Type type) => type == typeof(T);

    public void Serialize(object obj, SerializationInfo info)
    {
        var json = JsonSerializer.Serialize((T)obj);
        info.AddValue("Json", json);
    }

    public object Deserialize(SerializationInfo info)
    {
        var json = info.GetValue<string>("Json");
        return JsonSerializer.Deserialize<T>(json);
    }
}

// Register for multiple types
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m =>
        {
            m.CustomSerializers.Add(
                new TypeMap<OrderDto, JsonWrapperSerializer<OrderDto>>(
                    JsonWrapperSerializer<OrderDto>.CanSerialize));
            m.CustomSerializers.Add(
                new TypeMap<ProductDto, JsonWrapperSerializer<ProductDto>>(
                    JsonWrapperSerializer<ProductDto>.CanSerialize));
        })));
```

## Complete Example: Third-Party API Response

Here's a complete example serializing a third-party API response type:

```csharp
// Third-party type you cannot modify
namespace ExternalApi.Models
{
    public class ApiResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

// Custom serializer
using Csla.Serialization.Mobile;
using ExternalApi.Models;

public class ApiResponseSerializer : IMobileSerializer
{
    public static bool CanSerialize(Type type) => type == typeof(ApiResponse);

    public void Serialize(object obj, SerializationInfo info)
    {
        var response = (ApiResponse)obj;
        info.AddValue("StatusCode", response.StatusCode);
        info.AddValue("Message", response.Message);
        info.AddValue("Timestamp", response.Timestamp);

        // Serialize complex Dictionary as JSON
        if (response.Data != null)
        {
            var dataJson = JsonSerializer.Serialize(response.Data);
            info.AddValue("DataJson", dataJson);
            info.AddValue("HasData", true);
        }
        else
        {
            info.AddValue("HasData", false);
        }
    }

    public object Deserialize(SerializationInfo info)
    {
        var response = new ApiResponse
        {
            StatusCode = info.GetValue<int>("StatusCode"),
            Message = info.GetValue<string>("Message"),
            Timestamp = info.GetValue<DateTime>("Timestamp")
        };

        if (info.GetValue<bool>("HasData"))
        {
            var dataJson = info.GetValue<string>("DataJson");
            response.Data = JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson);
        }

        return response;
    }
}

// Registration
services.AddCsla(options => options
    .Serialization(s => s
        .UseMobileFormatter(m => m
            .CustomSerializers.Add(
                new TypeMap<ApiResponse, ApiResponseSerializer>(
                    ApiResponseSerializer.CanSerialize)))));

// Usage in business object
[CslaImplementProperties]
public partial class OrderStatus : ReadOnlyBase<OrderStatus>
{
    public static readonly PropertyInfo<ApiResponse> LastResponseProperty =
        RegisterProperty<ApiResponse>(nameof(LastResponse));
    public ApiResponse LastResponse
    {
        get => GetProperty(LastResponseProperty);
        private set => LoadProperty(LastResponseProperty, value);
    }

    [Fetch]
    private async Task Fetch(int orderId, [Inject] IOrderApi api)
    {
        var response = await api.GetOrderStatus(orderId);
        LastResponse = response;
    }
}
```

## Built-in Custom Serializers

CSLA 10 includes these built-in custom serializers:

| Serializer | Purpose |
|------------|---------|
| `ClaimsPrincipalSerializer` | Serializes `ClaimsPrincipal` (active by default) |
| `PocoSerializer<T>` | JSON-based serialization for simple POCOs |

## When to Use Custom Serializers

| Scenario | Recommended Approach |
|----------|---------------------|
| Simple POCO with public properties | `PocoSerializer<T>` |
| Types you own | `[AutoSerializable]` attribute |
| Third-party types | Custom serializer |
| Types with constructors | Custom serializer |
| Types with private members | Custom serializer |
| Complex serialization logic | Custom serializer |
| Performance-critical serialization | Custom serializer with optimized logic |

## Best Practices

1. **Implement `CanSerialize` as static** - This is used during registration before an instance exists.

2. **Handle nulls explicitly** - Decide how null values should be serialized and deserialized.

3. **Use consistent key names** - The string keys in `AddValue` and `GetValue` must match exactly.

4. **Test round-trip serialization** - Always verify that `Deserialize(Serialize(obj))` produces an equivalent object.

5. **Keep serializers focused** - Each serializer should handle one type or a closely related type hierarchy.

6. **Consider versioning** - If the type structure may change, include a version number:
   ```csharp
   public void Serialize(object obj, SerializationInfo info)
   {
       info.AddValue("Version", 1);
       // ... serialize properties
   }
   ```

7. **Prefer `PocoSerializer<T>` for simple types** - Only create custom serializers when you need special handling.

8. **Register all types at startup** - Missing registrations will cause runtime errors when serialization is attempted.
