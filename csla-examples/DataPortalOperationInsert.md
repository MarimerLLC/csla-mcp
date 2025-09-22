# Data Portal Operation Insert

This code snippet demonstrates how to implement the `Insert` data portal operation method in a CSLA .NET business class. The `Insert` method is responsible for inserting a new instance of the business object into a data access layer (DAL) and handling any returned values, such as a newly generated identifier.

```csharp
[Insert]
private async Task Insert([Inject] ICustomerDal customerDal)
{
    // Create a data transfer object (DTO) to hold the values to insert
    var customerData = new CustomerData
    {
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty),
        CreatedDate = ReadProperty(CreatedDateProperty),
        IsActive = ReadProperty(IsActiveProperty)
    };
    
    // Call DAL Insert method to insert data
    var insertedCustomerData = await customerDal.Insert(customerData);
    
    // Load returned values from DAL, such as the new Id
    LoadProperty(IdProperty, insertedCustomerData.Id);
}
```

In this example, the `Insert` method creates a data transfer object (DTO) or entity object with the current property values of the business object. It then calls the `Insert` method of the injected data access layer (DAL) to perform the insertion. After the insertion, it loads any returned values, such as a newly generated identifier, back into the business object's properties using the `LoadProperty` method.

The `ReadProperty` method is used to get the current values of the properties without triggering any business rules or validation, while the `LoadProperty` method is used to set the property's value internally within the class, bypassing any business rules or validation.

You can also use the `BypassPropertyChecks` property to set property values directly without triggering business rules or validation.
