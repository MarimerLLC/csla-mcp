# CSLA DataMapper

The `DataMapper` class in the `Csla.Data` namespace provides utilities for mapping data between objects by copying property and field values. It is commonly used in data portal operation methods to efficiently transfer data from Data Access Layer (DAL) objects to CSLA business objects, and vice versa.

## Overview

`DataMapper` simplifies the process of copying property values from one object to another, eliminating the need to manually write `LoadProperty` calls for each property. It supports automatic type coercion, making it useful when working with different object types that share similar property structures.

## Basic Usage

The simplest form of `DataMapper.Map()` copies all matching public properties from a source object to a target object:

**CSLA 9:**

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Get data from DAL
    var customerData = await customerDal.Get(id);

    // Map all matching properties from DAL object to business object
    Csla.Data.DataMapper.Map(customerData, this);

    BusinessRules.CheckRules();
}
```

**CSLA 10:**

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Get data from DAL
    var customerData = await customerDal.Get(id);

    // Map all matching properties from DAL object to business object
    Csla.Data.DataMapper.Map(customerData, this);

    await CheckRulesAsync();
}
```

> **Note:** In CSLA 10, it is recommended to use `await CheckRulesAsync()` instead of `BusinessRules.CheckRules()` to ensure that asynchronous business rules execute properly.

This approach works when:
- Property names match between source and target objects
- Property types are compatible or can be coerced
- You want to map all available properties

## Using an Ignore List

When you need to exclude certain properties from being mapped, use the `ignoreList` parameter:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Get data from DAL
    var customerData = await customerDal.Get(id);

    // Map properties, but ignore specific ones
    Csla.Data.DataMapper.Map(customerData, this, "CreatedDate", "ModifiedDate");

    // Set ignored properties manually if needed
    LoadProperty(CreatedDateProperty, DateTime.Now);

    await CheckRulesAsync();  // Use BusinessRules.CheckRules() in CSLA 9
}
```

The ignored properties will not be copied from the source to the target object. This is useful when:
- Certain properties should be set separately with custom logic
- Some properties exist on the source but not the target
- You want to prevent overwriting specific target properties

## Using Explicit Mapping with DataMap

For more complex scenarios where property names differ or you need fine-grained control over mappings, use the `DataMap` class:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Get data from DAL
    var customerData = await customerDal.Get(id);

    // Create a DataMap with explicit mappings
    var map = new Csla.Data.DataMap(typeof(CustomerData), typeof(CustomerEdit));

    // Add property-to-property mappings
    map.AddPropertyMapping("Id", "Id");
    map.AddPropertyMapping("CustomerName", "Name");  // Different property names
    map.AddPropertyMapping("EmailAddr", "Email");     // Different property names
    map.AddPropertyMapping("Active", "IsActive");

    // Add field-to-property mappings if needed
    map.AddFieldToPropertyMapping("_created", "CreatedDate");

    // Perform the mapping using the DataMap
    Csla.Data.DataMapper.Map(customerData, this, map);

    await CheckRulesAsync();  // Use BusinessRules.CheckRules() in CSLA 9
}
```

The `DataMap` class provides these mapping methods:

- `AddPropertyMapping(sourceProperty, targetProperty)` - Maps a property to a property
- `AddFieldMapping(sourceField, targetField)` - Maps a field to a field
- `AddFieldToPropertyMapping(sourceField, targetProperty)` - Maps a field to a property
- `AddPropertyToFieldMapping(sourceProperty, targetField)` - Maps a property to a field

You can also initialize a `DataMap` with a list of property names for 1:1 mappings:

```csharp
var map = new Csla.Data.DataMap(
    typeof(CustomerData),
    typeof(CustomerEdit),
    new[] { "Id", "Name", "Email", "IsActive" }
);
```

## Type Coercion

`DataMapper` automatically handles type coercion for common conversions:

- Numeric types (int, double, decimal, etc.)
- String to numeric conversions
- Nullable types
- Enum conversions (from string or int)
- DateTime and SmartDate conversions
- Guid conversions
- Boolean conversions

For example, if the source has a `string` property with value `"42"` and the target has an `int` property, `DataMapper` will automatically convert the string to an integer.

## Working with Dictionaries

`DataMapper` can also map data from dictionaries to objects:

```csharp
var source = new Dictionary<string, object>
{
    { "Id", 1 },
    { "Name", "John Doe" },
    { "Email", "john@example.com" }
};

Csla.Data.DataMapper.Map(source, customerObject);
```

And from objects to dictionaries:

```csharp
var target = new Dictionary<string, object>();
Csla.Data.DataMapper.Map(customerObject, target);
```

## Suppressing Exceptions

By default, `DataMapper` throws exceptions when property mapping fails. You can suppress exceptions using the `suppressExceptions` parameter:

```csharp
// Suppress exceptions - failed mappings will be silently ignored
Csla.Data.DataMapper.Map(customerData, this, suppressExceptions: true);
```

This can be useful when:
- Source and target objects have different sets of properties
- You want a best-effort mapping without failing on individual property errors
- Working with dynamic data where property existence may vary

## Additional Utilities

`DataMapper` also provides utility methods for setting individual properties and fields:

```csharp
// Set a property value with type coercion
Csla.Data.DataMapper.SetPropertyValue(target, "Age", "25");

// Set a field value (including private fields)
Csla.Data.DataMapper.SetFieldValue(target, "_name", "John Doe");

// Get a field value
var value = Csla.Data.DataMapper.GetFieldValue(target, "_id");
```

## Best Practices

1. **Use the simplest approach** - Start with basic `Map(source, target)` unless you need special handling
2. **Use ignore lists** when a few properties need special handling
3. **Use DataMap** when property names differ significantly or you need complex mappings
4. **Always check business rules after mapping** - Use `await CheckRulesAsync()` in CSLA 10 or `BusinessRules.CheckRules()` in CSLA 9 after mapping data in Fetch/Create operations
5. **Consider type compatibility** - While DataMapper handles many conversions, ensure your types are reasonably compatible
6. **Map efficiently** - Mapping in one call is more efficient than multiple `LoadProperty` calls for many properties

## Common Scenarios

### Scenario 1: Simple DAL to Business Object Mapping

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] IPersonDal dal)
{
    var data = await dal.Get(id);
    Csla.Data.DataMapper.Map(data, this);
    await CheckRulesAsync();  // Use BusinessRules.CheckRules() in CSLA 9
}
```

### Scenario 2: Mapping with Some Properties Excluded

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] IOrderDal dal)
{
    var data = await dal.Get(id);
    // Don't map Total - it will be calculated by business rules
    Csla.Data.DataMapper.Map(data, this, "Total");
    await CheckRulesAsync();  // Use BusinessRules.CheckRules() in CSLA 9
}
```

### Scenario 3: Mapping with Different Property Names

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] IProductDal dal)
{
    var data = await dal.Get(id);

    var map = new Csla.Data.DataMap(typeof(ProductData), typeof(ProductEdit));
    map.AddPropertyMapping("ProductId", "Id");
    map.AddPropertyMapping("ProductName", "Name");
    map.AddPropertyMapping("UnitPrice", "Price");

    Csla.Data.DataMapper.Map(data, this, map);
    await CheckRulesAsync();  // Use BusinessRules.CheckRules() in CSLA 9
}
```

### Scenario 4: Mapping Business Object to DAL Object for Updates

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal)
{
    var data = new CustomerData();
    Csla.Data.DataMapper.Map(this, data);
    await dal.Update(data);
}
```
