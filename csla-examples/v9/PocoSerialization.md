# Serializing POCOs in CSLA 9

In CSLA 9, the easiest way to serialize a POCO (Plain Old C# Object), DTO (Data Transfer Object), or any other type that doesn't subclass `Csla.Core.MobileObject` is to implement the `Csla.Core.IMobileObject` interface.

## Overview

CSLA's `MobileFormatter` serializer requires that all types in an object graph implement `IMobileObject`. All CSLA base types (like `BusinessBase<T>`, `ReadOnlyBase<T>`, etc.) implement this interface automatically. However, if you want to use a custom POCO type as a property value, criteria parameter, or anywhere else in your object graph, you must implement `IMobileObject`.

## Implementing IMobileObject

The `IMobileObject` interface has four methods:

```csharp
public interface IMobileObject
{
    void GetState(SerializationInfo info);
    void SetState(SerializationInfo info);
    void GetChildren(SerializationInfo info, MobileFormatter formatter);
    void SetChildren(SerializationInfo info, MobileFormatter formatter);
}
```

### Basic Implementation

For a simple POCO with primitive properties, implement the interface like this:

```csharp
using Csla.Core;
using Csla.Serialization.Mobile;

public class CustomerCriteria : IMobileObject
{
    public string Region { get; set; }
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public bool IsActive { get; set; }

    public void GetState(SerializationInfo info)
    {
        info.AddValue("Region", Region);
        info.AddValue("MinAge", MinAge);
        info.AddValue("MaxAge", MaxAge);
        info.AddValue("IsActive", IsActive);
    }

    public void SetState(SerializationInfo info)
    {
        Region = info.GetValue<string>("Region");
        MinAge = info.GetValue<int>("MinAge");
        MaxAge = info.GetValue<int>("MaxAge");
        IsActive = info.GetValue<bool>("IsActive");
    }

    public void GetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        // No child objects to serialize
    }

    public void SetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        // No child objects to deserialize
    }
}
```

### Using the POCO in a Business Object

Once your POCO implements `IMobileObject`, you can use it anywhere in your CSLA application:

```csharp
// As a data portal criteria parameter
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

## Complete Example: Address DTO

Here's a complete example of a DTO used to transfer address information:

```csharp
using Csla.Core;
using Csla.Serialization.Mobile;

public class AddressDto : IMobileObject
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }

    public void GetState(SerializationInfo info)
    {
        info.AddValue("Street", Street);
        info.AddValue("City", City);
        info.AddValue("State", State);
        info.AddValue("ZipCode", ZipCode);
        info.AddValue("Country", Country);
    }

    public void SetState(SerializationInfo info)
    {
        Street = info.GetValue<string>("Street");
        City = info.GetValue<string>("City");
        State = info.GetValue<string>("State");
        ZipCode = info.GetValue<string>("ZipCode");
        Country = info.GetValue<string>("Country");
    }

    public void GetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        // No child objects
    }

    public void SetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        // No child objects
    }
}
```

## Handling Child Objects

If your POCO contains child objects that also implement `IMobileObject`, use the `GetChildren` and `SetChildren` methods:

```csharp
using Csla.Core;
using Csla.Serialization.Mobile;

public class OrderDto : IMobileObject
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public AddressDto ShippingAddress { get; set; }
    public AddressDto BillingAddress { get; set; }

    public void GetState(SerializationInfo info)
    {
        info.AddValue("OrderId", OrderId);
        info.AddValue("OrderDate", OrderDate);
        info.AddValue("TotalAmount", TotalAmount);
    }

    public void SetState(SerializationInfo info)
    {
        OrderId = info.GetValue<int>("OrderId");
        OrderDate = info.GetValue<DateTime>("OrderDate");
        TotalAmount = info.GetValue<decimal>("TotalAmount");
    }

    public void GetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        if (ShippingAddress != null)
            info.AddChild("ShippingAddress", formatter.SerializeObject(ShippingAddress));
        if (BillingAddress != null)
            info.AddChild("BillingAddress", formatter.SerializeObject(BillingAddress));
    }

    public void SetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        if (info.Children.ContainsKey("ShippingAddress"))
            ShippingAddress = (AddressDto)formatter.DeserializeObject(info.Children["ShippingAddress"]);
        if (info.Children.ContainsKey("BillingAddress"))
            BillingAddress = (AddressDto)formatter.DeserializeObject(info.Children["BillingAddress"]);
    }
}
```

## Handling Collections

For collections of serializable objects:

```csharp
using Csla.Core;
using Csla.Serialization.Mobile;

public class SearchResultsDto : IMobileObject
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<CustomerDto> Customers { get; set; } = new List<CustomerDto>();

    public void GetState(SerializationInfo info)
    {
        info.AddValue("TotalCount", TotalCount);
        info.AddValue("PageNumber", PageNumber);
        info.AddValue("PageSize", PageSize);
    }

    public void SetState(SerializationInfo info)
    {
        TotalCount = info.GetValue<int>("TotalCount");
        PageNumber = info.GetValue<int>("PageNumber");
        PageSize = info.GetValue<int>("PageSize");
    }

    public void GetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        var customerList = new MobileList<CustomerDto>(Customers);
        info.AddChild("Customers", formatter.SerializeObject(customerList));
    }

    public void SetChildren(SerializationInfo info, MobileFormatter formatter)
    {
        if (info.Children.ContainsKey("Customers"))
        {
            var customerList = (MobileList<CustomerDto>)formatter.DeserializeObject(info.Children["Customers"]);
            Customers = customerList.ToList();
        }
    }
}
```

## SerializationInfo Supported Types

The `SerializationInfo` class can store these "primitive" types directly:

- All .NET primitive types (`int`, `long`, `double`, `bool`, etc.)
- `string`
- `DateTime`, `TimeSpan`, `DateTimeOffset`, `DateOnly`, `TimeOnly`
- `Guid`
- `byte[]`
- `char[]`
- `List<int>` (and other primitive type lists)
- Nullable versions of the above types

For complex types (objects, lists of objects), use the child object methods.

## Best Practices

1. **Do not use `[Serializable]`** - The `[Serializable]` attribute is not required and is discouraged with CSLA's `MobileFormatter`.

2. **Use consistent property names** - The string names in `AddValue` and `GetValue` must match exactly.

3. **Handle null values** - Be aware that property values might be null when deserializing:
   ```csharp
   public void SetState(SerializationInfo info)
   {
       Name = info.GetValue<string>("Name") ?? string.Empty;
   }
   ```

4. **Initialize collections** - Always initialize collection properties to avoid null reference exceptions:
   ```csharp
   public List<string> Tags { get; set; } = new List<string>();
   ```

5. **Consider using `ReadOnlyBase` for criteria** - For complex criteria objects with validation, consider inheriting from `ReadOnlyBase<T>` instead of implementing `IMobileObject` manually.

## When to Use This Approach

Use `IMobileObject` implementation when you have:

- Simple DTOs or POCOs that need to pass through the data portal
- Third-party types you can wrap with a serializable wrapper
- Types that need custom serialization logic

For simple criteria with just a few parameters, consider passing them directly:

```csharp
// Instead of creating a criteria DTO, pass values directly
[Fetch]
private async Task Fetch(string region, int minAge, int maxAge, [Inject] ICustomerDal dal)
{
    var data = await dal.GetByCriteria(region, minAge, maxAge);
    // Load data...
}

// Client code
var customers = await customerListPortal.FetchAsync("West", 25, 65);
```
