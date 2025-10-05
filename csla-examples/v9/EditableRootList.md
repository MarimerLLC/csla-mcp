# Editable Root List Stereotype

This example demonstrates a complete CSLA business list class named `OrderItemList` that includes a list of editable child items, authorization rules, and data access methods. The class derives from `BusinessListBase<T, C>` and includes a collection of editable child objects of type `OrderItemEdit`.

This class demonstrates the editable root list business class stereotype.

It also shows how to implement object-level authorization rules to control access based on user roles.

It also includes data portal operation methods for creating, fetching, inserting, updating, and deleting order item records. Note that the data access methods contain placeholder comments where actual data access logic should be invoked.

```csharp
using System;
using Csla;

public class OrderItemList : BusinessListBase<OrderItemList, OrderItemEdit>
{
    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        // Example authorization rules
        BusinessRules.AddRule(typeof(Customer), new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.GetObject, "Admin", "Manager"));
        BusinessRules.AddRule(typeof(Customer), new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.EditObject, "Admin", "Manager", "User"));
    }

    [Create]
    private void Create()
    {
        // Initialization logic if needed
    }

    [Fetch]
    private async Task Fetch([Inject] IOrderDetailDal dal, [Inject] IChildDataPortal<OrderItemEdit> childDataPortal)
    {
        var dataList = await dal.FetchAllAsync();
        using (LoadListMode)
        {
            foreach (var data in dataList)
            {
                var item = childDataPortal.FetchChild<OrderItemEdit>(data);
                Add(item);
            }
        }
    }

    [Update]
    private async Task Update()
    {
        await Child_UpdateAsync();
    }
}
```
