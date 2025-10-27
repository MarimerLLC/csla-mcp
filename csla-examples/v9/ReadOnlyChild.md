# Read-Only Child Stereotype

The Read-Only Child stereotype represents an immutable business object that is always contained within a parent object. It's used for displaying child data that users can view but not edit.

**Key Characteristics**:

* Always contained within a parent object (cannot exist independently)
* Derives from `ReadOnlyBase<T>`
* All properties are read-only (private set)
* Only supports FetchChild operation
* Managed by parent - fetched as part of parent's operations
* Supports authorization rules

**Common use cases**: Order line items for display, address information in customer view, historical transaction details.

## Implementation Example

This example shows a read-only order item as a child within an order.

```csharp
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderItemInfo : ReadOnlyBase<OrderItemInfo>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }
        
        public static readonly PropertyInfo<string> ProductNameProperty = RegisterProperty<string>(nameof(ProductName));
        public string ProductName
        {
            get => GetProperty(ProductNameProperty);
            private set => LoadProperty(ProductNameProperty, value);
        }
        
        public static readonly PropertyInfo<int> QuantityProperty = RegisterProperty<int>(nameof(Quantity));
        public int Quantity
        {
            get => GetProperty(QuantityProperty);
            private set => LoadProperty(QuantityProperty, value);
        }
        
        public static readonly PropertyInfo<decimal> PriceProperty = RegisterProperty<decimal>(nameof(Price));
        public decimal Price
        {
            get => GetProperty(PriceProperty);
            private set => LoadProperty(PriceProperty, value);
        }
        
        // Calculated property
        public decimal Total => Quantity * Price;

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();
            
            // Property-level authorization: only Manager can view Price
            BusinessRules.AddRule(
                new Rules.CommonRules.IsInRole(
                    Rules.AuthorizationActions.ReadProperty,
                    PriceProperty,
                    "Manager", "Admin"));
        }

        [FetchChild]
        private async Task FetchChild(int id, [Inject] IOrderItemDal dal)
        {
            var data = await dal.GetAsync(id);
            LoadProperty(IdProperty, data.Id);
            LoadProperty(ProductNameProperty, data.ProductName);
            LoadProperty(QuantityProperty, data.Quantity);
            LoadProperty(PriceProperty, data.Price);
        }

        [FetchChild]
        private void FetchChild(OrderItemData data)
        {
            // Overload that receives data directly (no database call)
            LoadProperty(IdProperty, data.Id);
            LoadProperty(ProductNameProperty, data.ProductName);
            LoadProperty(QuantityProperty, data.Quantity);
            LoadProperty(PriceProperty, data.Price);
        }
    }
}
```

## Parent Containing Read-Only Child

Here's how a parent object manages a read-only child:

```csharp
using System;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderInfo : ReadOnlyBase<OrderInfo>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }
        
        public static readonly PropertyInfo<DateTime> OrderDateProperty = RegisterProperty<DateTime>(nameof(OrderDate));
        public DateTime OrderDate
        {
            get => GetProperty(OrderDateProperty);
            private set => LoadProperty(OrderDateProperty, value);
        }
        
        // Read-only child property
        public static readonly PropertyInfo<OrderItemInfo> ItemProperty = RegisterProperty<OrderItemInfo>(nameof(Item));
        public OrderItemInfo Item
        {
            get => GetProperty(ItemProperty);
            private set => LoadProperty(ItemProperty, value);
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] IOrderDal dal, [Inject] IChildDataPortal<OrderItemInfo> itemPortal)
        {
            var data = await dal.GetAsync(id);
            LoadProperty(IdProperty, data.Id);
            LoadProperty(OrderDateProperty, data.OrderDate);
            
            // Fetch the read-only child
            var item = await itemPortal.FetchChildAsync(data.ItemId);
            LoadProperty(ItemProperty, item);
        }
    }
}
```

## Read-Only Child vs Editable Child

### Read-Only Child (ReadOnlyBase)

* Cannot be modified
* Only has FetchChild operation
* No business rules for validation (only authorization)
* No change tracking or undo
* Lighter weight for display scenarios

### Editable Child (BusinessBase)

* Can be created, modified, and deleted
* Has CreateChild, FetchChild, InsertChild, UpdateChild, DeleteSelfChild operations
* Full business rules support
* Change tracking and n-level undo

See `EditableChild.md` for editable children.

## Key Points for Read-Only Children

### Fetching Child Instances

Parents fetch children using `IChildDataPortal<T>`:

```csharp
// In parent's Fetch operation
var child = await childPortal.FetchChildAsync(criteria);
LoadProperty(ChildProperty, child);
```

### Fetching Children Efficiently in Lists

**Important**: When a parent is a list, fetch all child data in one database query, then pass individual rows to each child's `FetchChild` operation.

```csharp
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderItemInfoList : ReadOnlyListBase<OrderItemInfoList, OrderItemInfo>
    {
        [Fetch]
        private async Task Fetch(int orderId, [Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemInfo> itemPortal)
        {
            // Get ALL items for this order in ONE database call
            var items = await dal.GetAllForOrderAsync(orderId);
            
            // Loop through the data and create child objects
            using (LoadListMode)
            {
                foreach (var itemData in items)
                {
                    // Pass the data row to child - child does NOT make its own database call
                    var child = await itemPortal.FetchChildAsync(itemData);
                    Add(child);
                }
            }
        }
    }
}
```

The child's `FetchChild` overload receives the data directly:

```csharp
[FetchChild]
private void FetchChild(OrderItemData data)
{
    // No database call here - just load from the data parameter
    LoadProperty(IdProperty, data.Id);
    LoadProperty(ProductNameProperty, data.ProductName);
    LoadProperty(QuantityProperty, data.Quantity);
    LoadProperty(PriceProperty, data.Price);
}
```

**Performance Note**: This pattern avoids N+1 queries.

### Data Portal Operations

Read-only children only support:

* `[FetchChild]` - Load existing child data (called by parent)

No CreateChild, InsertChild, UpdateChild, or DeleteSelfChild operations.

### Authorization in Read-Only Children

Children support property-level authorization to control who can read specific properties:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    // Only managers can view sensitive properties
    BusinessRules.AddRule(
        new Rules.CommonRules.IsInRole(
            Rules.AuthorizationActions.ReadProperty,
            SalaryProperty,
            "Manager", "Admin"));
}
```

### Calculated Properties

Read-only children commonly include calculated properties:

```csharp
public decimal Total => Quantity * Price;
public string FullName => $"{FirstName} {LastName}";
public bool IsExpired => ExpirationDate < DateTime.Today;
```

### When to Use

**Use Read-Only Child when**:

* Displaying child data that should not be modified
* Child data comes from views or optimized read queries
* Reducing overhead for display-only scenarios

**Use Editable Child when**:

* Users need to create, modify, or delete child objects
* Child participates in parent's save operation
