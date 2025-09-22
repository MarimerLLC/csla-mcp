# Data Portal Operation Update

This code snippet demonstrates how to implement the `Update` data portal operation method in a CSLA .NET business class. The `Update` method is responsible for updating an existing instance of the business object in a data access layer (DAL).

```csharp
[Update]
private async Task Update([Inject] ICustomerDal customerDal)
{
    // Create a data transfer object (DTO) to hold the values to update
    var customerData = new CustomerData
    {
        Id = ReadProperty(IdProperty),
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty),
        CreatedDate = ReadProperty(CreatedDateProperty),
        IsActive = ReadProperty(IsActiveProperty)
    };
    
    // Call DAL Upsert method for update and get result
    await customerDal.Update(customerData);
}
```

In this example, the `Update` method creates a data transfer object (DTO) or entity object with the current property values of the business object, including the identifier. It then calls the `Update` method of the injected data access layer (DAL) to perform the update. 

After the update, there is typically no need to load any returned values back into the business object's properties, as the properties should already reflect the current state.

The `ReadProperty` method is used to get the current values of the properties without triggering any business rules or validation, while the `LoadProperty` method is used to set the property's value internally within the class, bypassing any business rules or validation.

You can also use the `BypassPropertyChecks` property to set property values directly without triggering business rules or validation.
