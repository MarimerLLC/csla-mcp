# Read-Only Child List Stereotype

The Read-Only Child List stereotype represents a collection of read-only child objects that is itself a child of a parent object. Both the list and its items are immutable and used for display purposes only.

**Key Characteristics**:

* The list itself is a child object (contained within a parent)
* Contains read-only child objects
* Derives from `ReadOnlyListBase<T, C>` where T is the list type, C is the contained child type
* Only supports FetchChild operation
* Cannot be modified after fetching
* Cannot exist independently - always part of a parent

**Important**: This is an immutable collection that is part of a parent object. It cannot be modified after being fetched.

**Common use cases**: Order items in order history, addresses in customer profile, transaction details in account view.

## Implementation Example

This example demonstrates a read-only child list of order items contained within an order.

```csharp
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderItemInfoList : ReadOnlyListBase<OrderItemInfoList, OrderItemInfo>
    {
        [FetchChild]
        private async Task FetchChild(int orderId, [Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemInfo> itemPortal)
        {
            // Get ALL items for this order in ONE database call
            var items = await dal.GetAllForOrderAsync(orderId);
            
            // Load items into list
            using (LoadListMode)
            {
                foreach (var itemData in items)
                {
                    // Pass data to child - avoids N+1 query problem
                    var item = await itemPortal.FetchChildAsync(itemData);
                    Add(item);
                }
            }
            
            IsReadOnly = true; // Make the list immutable
        }
    }
}
```

## Parent Containing Read-Only Child List

Here's how a parent object manages a read-only child list:

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
        
        // Read-only child list property
        public static readonly PropertyInfo<OrderItemInfoList> ItemsProperty = RegisterProperty<OrderItemInfoList>(nameof(Items));
        public OrderItemInfoList Items
        {
            get => GetProperty(ItemsProperty);
            private set => LoadProperty(ItemsProperty, value);
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] IOrderDal dal, [Inject] IChildDataPortal<OrderItemInfoList> itemsPortal)
        {
            var data = await dal.GetAsync(id);
            LoadProperty(IdProperty, data.Id);
            LoadProperty(OrderDateProperty, data.OrderDate);
            
            // Fetch read-only child list (which will fetch all children)
            var items = await itemsPortal.FetchChildAsync(data.Id);
            LoadProperty(ItemsProperty, items);
        }
    }
}
```

## Child List vs Root List

### Read-Only Child List (ReadOnlyListBase as child)

* The list itself is a child of a parent object
* Contains **read-only child objects**
* Uses `[FetchChild]` operation
* Fetched by parent using `IChildDataPortal<ListType>`
* Use when the collection belongs to a parent entity

```csharp
// Parent fetches the child list
var items = await itemsPortal.FetchChildAsync(orderId);
LoadProperty(ItemsProperty, items);
```

### Read-Only Root List (ReadOnlyListBase as root)

* The list itself is a root object
* Contains **read-only child objects**
* Uses `[Fetch]` operation
* Fetched using `IDataPortal<ListType>`
* Use when the collection is a top-level entity

See `ReadOnlyRootList.md` for root list examples.

## Performance: Avoiding N+1 Queries

**Critical**: The child list's `FetchChild` should get ALL child data in ONE database query, then pass individual rows to each child item's `FetchChild`.

```csharp
[FetchChild]
private async Task FetchChild(int orderId, [Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemInfo> itemPortal)
{
    // ONE database call to get all items
    var items = await dal.GetAllForOrderAsync(orderId);
    
    using (LoadListMode)
    {
        foreach (var itemData in items)
        {
            // No database call - just loads from data
            var item = await itemPortal.FetchChildAsync(itemData);
            Add(item);
        }
    }
    
    IsReadOnly = true;
}
```

Each child item receives its data directly:

```csharp
// In OrderItemInfo class
[FetchChild]
private void FetchChild(OrderItemData data)
{
    // No database call - just load from data parameter
    LoadProperty(IdProperty, data.Id);
    LoadProperty(ProductNameProperty, data.ProductName);
    LoadProperty(QuantityProperty, data.Quantity);
    LoadProperty(PriceProperty, data.Price);
}
```

**Performance Note**: This pattern is essential to avoid N+1 queries.

## Using the Read-Only Child List

### Accessing in Parent

```csharp
var order = await orderPortal.FetchAsync(orderId);

// Access the child list
foreach (var item in order.Items)
{
    Console.WriteLine($"{item.ProductName}: {item.Quantity} x {item.Price:C}");
}

// Calculate totals
var total = order.Items.Sum(i => i.Total);
```

### Immutability

The list cannot be modified:

```csharp
var order = await orderPortal.FetchAsync(orderId);

// These operations will throw exceptions:
// order.Items.Add(newItem);        // NotSupportedException
// order.Items.RemoveAt(0);         // NotSupportedException
// order.Items.Clear();             // NotSupportedException
```

### Filtering and Display

Use LINQ for filtering and display purposes:

```csharp
var order = await orderPortal.FetchAsync(orderId);

// Filter items
var highValueItems = order.Items.Where(i => i.Total > 100).ToList();

// Sort for display
var sortedItems = order.Items.OrderBy(i => i.ProductName).ToList();
```

## Read-Only Child List vs Editable Child List

### Read-Only Child List (ReadOnlyListBase)

* Cannot be modified after fetching
* Only has FetchChild operation
* No change tracking
* Lighter weight for display scenarios
* Items are read-only children

### Editable Child List (BusinessListBase)

* Items can be added, edited, removed
* Has CreateChild and FetchChild operations
* Full change tracking
* Items are editable children
* Saved through parent's save operation

See `EditableChildList.md` for editable child lists.

## Key Points

### Child List Lifecycle

1. **Fetch**: Parent fetches the child list via `IChildDataPortal<ListType>.FetchChildAsync(criteria)`
2. **Display**: List and items are displayed in UI
3. **Immutable**: No modifications allowed

### Data Portal Operations

Read-only child lists only support:

* `[FetchChild]` - Load list with children from database

No CreateChild, Update, Insert, or Delete operations.

### Authorization

Property-level authorization can be applied to child items to control visibility of specific properties.

### When to Use

**Use Read-Only Child List when**:

* Displaying a collection of child data that should not be modified
* Child data is for historical or reference purposes
* Reducing overhead for display-only scenarios

**Examples**: Order history items, transaction details, archived records.

**Use Editable Child List when**:

* Users need to add, edit, or delete child items
* The collection participates in parent's save operation

## Best Practices

### Optimize Queries

Fetch all child data in a single optimized query:

```csharp
// In DAL
public async Task<List<OrderItemData>> GetAllForOrderAsync(int orderId)
{
    // Single query with all needed data
    return await context.OrderItems
        .Where(i => i.OrderId == orderId)
        .Include(i => i.Product)  // Include related data if needed
        .ToListAsync();
}
```

### Calculated Totals

Add calculated properties to the list for totals:

```csharp
public class OrderItemInfoList : ReadOnlyListBase<OrderItemInfoList, OrderItemInfo>
{
    public decimal GrandTotal => this.Sum(i => i.Total);
    public int TotalItems => this.Sum(i => i.Quantity);
}
```
