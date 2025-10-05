# ReadOnly Root Stereotype

This example demonstrates a complete CSLA business class named `CustomerInfo` that includes various property types, authorization rules, and data access methods. The class derives from `ReadOnlyBase<T>` and includes only read-only properties.

This class demonstrates the read-only root business class stereotype.

It also shows how to implement property and object-level authorization rules to control access based on user roles.

It also includes a data portal operation method for fetching customer records. Note that the data access method contains a placeholder comment where actual data access logic should be invoked.

```csharp
using System;
using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;

[CslaImplementProperties]
public partial class CustomerInfo : ReadOnlyBase<CustomerInfo>
{
    public partial int Id { get; private set; }
    public partial string Name { get; private set; }
    public partial string Email { get; private set; }
    public partial DateTime CreatedDate { get; private set; }
    public partial bool IsActive { get; private set; }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        // Example: Only users in the "Admin" role can view the Name property
        BusinessRules.AddRule(new IsInRole(NameProperty, "Admin"));
    }

    protected override void AddAuthorizationRules()
    {
        base.AddAuthorizationRules();
        // Example: Only users in the "Admin" role can read this object
        AuthorizationRules.AllowRead(typeof(CustomerInfo), "Admin");
    }

    private async Task DataPortal_Fetch(int id, [Inject] ICustomerDal customerDal)
    {
        var customerData = await customerDal.Get(id);
        if (customerData != null)
        {
            LoadProperty(IdProperty, customerData.Id);
            LoadProperty(NameProperty, customerData.Name);
            LoadProperty(EmailProperty, customerData.Email);
            LoadProperty(CreatedDateProperty, customerData.CreatedDate);
            LoadProperty(IsActiveProperty, customerData.IsActive);
        }
        else
        {
            throw new ArgumentException($"Customer {id} not found");
        }
    }
}
```
