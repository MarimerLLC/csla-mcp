# Data Portal Operation Fetch

This code snippet demonstrates how to implement the `Fetch` data portal operation method in a CSLA .NET business class. The `Fetch` method is responsible for retrieving an existing instance of the business object from a data access layer (DAL) based on a provided identifier.

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Call DAL Fetch method to get data
    var customerData = await customerDal.Fetch(id);
    
    // Load values from DAL
    LoadProperty(IdProperty, customerData.Id);
    LoadProperty(NameProperty, customerData.Name);
    LoadProperty(EmailProperty, customerData.Email);
    LoadProperty(CreatedDateProperty, customerData.CreatedDate);
    LoadProperty(IsActiveProperty, customerData.IsActive);
    
    BusinessRules.CheckRules();
}
```

You can also use the BypassPropertyChecks property to set property values directly without triggering business rules or validation:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Call DAL Fetch method to get data
    var customerData = await customerDal.Fetch(id);
    
    // Load values from DAL using BypassPropertyChecks
    using (BypassPropertyChecks)
    {
        Id = customerData.Id;
        Name = customerData.Name;
        Email = customerData.Email;
        CreatedDate = customerData.CreatedDate;
        IsActive = customerData.IsActive;
    }
    
    BusinessRules.CheckRules();
}
```

You can also use `DataMapper` or similar tools to map data from the DAL object to the business object properties if you have many properties to map.

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    // Call DAL Fetch method to get data
    var customerData = await customerDal.Fetch(id);
    
    // Map values from DAL to business object properties
    DataMapper.Map(customerData, this);
    
    BusinessRules.CheckRules();
}
```
