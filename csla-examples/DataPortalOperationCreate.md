# Data Portal Operation Create

This code snippet demonstrates how to implement the `Create` data portal operation method in a CSLA .NET business class. The `Create` method is responsible for initializing a new instance of the business object with default values, typically retrieved from a data access layer (DAL).

```csharp
[Create]
private async Task Create([Inject] ICustomerDal customerDal)
{
    // Call DAL Create method to get default values
    var customerData = await customerDal.Create();
    
    // Load default values from DAL
    LoadProperty(CreatedDateProperty, customerData.CreatedDate);
    LoadProperty(IsActiveProperty, customerData.IsActive);
    
    BusinessRules.CheckRules();
}
```

If you don't need to retrieve default values from the DAL, you can simply initialize properties directly within the `Create` method:

```csharp
[Create]
private void Create()
{
    // Initialize default values directly
    LoadProperty(CreatedDateProperty, DateTime.Now);
    LoadProperty(IsActiveProperty, true);
    
    BusinessRules.CheckRules();
}
```
