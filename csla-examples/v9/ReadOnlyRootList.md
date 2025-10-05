# Read-Only Root List Stereotype

This example demonstrates a complete CSLA business list class named `CustomerList` that includes a list of read-only child items, authorization rules, and data access methods. The class derives from `ReadOnlyListBase<T, C>` and includes a collection of read-only child objects of type `CustomerInfo`.

This class demonstrates the read-only root list business class stereotype.

It also shows how to implement object-level authorization rules to control access based on user roles.

It also includes a data portal operation method for fetching customer records. Note that the data access method contains a placeholder comment where actual data access logic should be invoked.

```csharp
using System;
using Csla;

public class CustomerList : ReadOnlyListBase<CustomerList, CustomerInfo>
{
    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        // Example authorization rules
        BusinessRules.AddRule(typeof(CustomerInfo), new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.GetObject, "Admin", "Manager"));
    }

    [Fetch]
    private async Task Fetch([Inject] ICustomerDal dal, [Inject] IChildDataPortal<CustomerInfo> childDataPortal)
    {
        var dataList = await dal.FetchAllAsync();
        using (LoadListMode)
        {
            foreach (var data in dataList)
            {
                var item = childDataPortal.FetchChild<CustomerInfo>(data);
                Add(item);
            }
        }
    }
}
```
