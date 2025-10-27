# Editable Child List Stereotype

The Editable Child List stereotype represents a collection of editable child objects that is itself a child of a parent object. Both the list and its items are managed through the parent's lifecycle.

**Key Characteristics**:

* The list itself is a child object (contained within a parent)
* Contains editable child objects
* Derives from `BusinessListBase<T, C>` where T is the list type, C is the contained child type
* Uses child data portal operations (`[CreateChild]`, `[FetchChild]`)
* List and its items are saved through parent's save operation
* Cannot exist independently - always part of a parent

**Important**: The list is created and fetched by the parent, and saved when the parent saves (via `FieldManager.UpdateChildrenAsync()`).

**Common use cases**: Order contains list of order items, Customer contains list of addresses, Invoice contains list of line items.

## Implementation Example

This example demonstrates a child list of order items contained within an order.

```csharp
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderItemList : BusinessListBase<OrderItemList, OrderItemEdit>
    {
        [CreateChild]
        private void CreateChild()
        {
            // Initialize empty list
        }

        [FetchChild]
        private async Task FetchChild(int orderId, [Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
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
        }
    }
}
```

## Parent Containing Child List

Here's how a parent object manages a child list:

```csharp
using System;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderEdit : BusinessBase<OrderEdit>
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
            set => SetProperty(OrderDateProperty, value);
        }
        
        // Child list property
        public static readonly PropertyInfo<OrderItemList> ItemsProperty = RegisterProperty<OrderItemList>(nameof(Items));
        public OrderItemList Items
        {
            get => GetProperty(ItemsProperty);
            private set => LoadProperty(ItemsProperty, value);
        }

        [Create]
        private async Task Create([Inject] IChildDataPortal<OrderItemList> itemsPortal)
        {
            LoadProperty(OrderDateProperty, DateTime.Today);
            
            // Create empty child list
            var items = await itemsPortal.CreateChildAsync();
            LoadProperty(ItemsProperty, items);
            
            await BusinessRules.CheckRulesAsync();
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] IOrderDal dal, [Inject] IChildDataPortal<OrderItemList> itemsPortal)
        {
            var data = await dal.GetAsync(id);
            LoadProperty(IdProperty, data.Id);
            LoadProperty(OrderDateProperty, data.OrderDate);
            
            // Fetch child list (which will fetch all children)
            var items = await itemsPortal.FetchChildAsync(data.Id);
            LoadProperty(ItemsProperty, items);
            
            await BusinessRules.CheckRulesAsync();
        }

        [Insert]
        private async Task Insert([Inject] IOrderDal dal)
        {
            var result = await dal.InsertAsync(new OrderData { OrderDate = OrderDate });
            LoadProperty(IdProperty, result.Id);
            
            // This will cascade to child list and all its children
            await FieldManager.UpdateChildrenAsync();
        }

        [Update]
        private async Task Update([Inject] IOrderDal dal)
        {
            await dal.UpdateAsync(new OrderData { Id = Id, OrderDate = OrderDate });
            
            // This will cascade to child list and all its children
            await FieldManager.UpdateChildrenAsync();
        }
    }
}
```

## Child List vs Root List

### Editable Child List (BusinessListBase as child)

* The list itself is a child of a parent object
* Contains **editable child objects**
* Uses `[CreateChild]` and `[FetchChild]` operations
* Fetched by parent using `IChildDataPortal<ListType>`
* Saved through parent's `FieldManager.UpdateChildrenAsync()`
* Use when the collection belongs to a parent entity

```csharp
// Parent creates/fetches the child list
var items = await itemsPortal.CreateChildAsync();
LoadProperty(ItemsProperty, items);

// Parent saves - which saves the list and all its children
await order.SaveAsync();
```

### Editable Root List (BusinessListBase as root)

* The list itself is a root object
* Contains **editable child objects**
* Uses `[Create]`, `[Fetch]`, and `[Update]` operations
* Fetched using `IDataPortal<ListType>`
* Saved directly via list's `Save()` method
* Use when the collection is a top-level entity

See `EditableRootList.md` for root list examples.

## Performance: Avoiding N+1 Queries

**Critical**: The child list's `FetchChild` should get ALL child data in ONE database query, then pass individual rows to each child item's `FetchChild`.

```csharp
[FetchChild]
private async Task FetchChild(int orderId, [Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
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
}
```

Each child item receives its data directly:

```csharp
// In OrderItemEdit class
[FetchChild]
private async Task FetchChild(OrderItemData data)
{
    // No database call - just load from data parameter
    LoadProperty(IdProperty, data.Id);
    LoadProperty(ProductNameProperty, data.ProductName);
    LoadProperty(QuantityProperty, data.Quantity);
    LoadProperty(PriceProperty, data.Price);
    
    await BusinessRules.CheckRulesAsync();
}
```

**Performance Note**: This pattern is essential. Having each child make its own database call would result in N+1 queries.

## Using the Child List

### Adding Items (in UI or business logic)

```csharp
// Get the parent order
var order = await orderPortal.FetchAsync(orderId);

// Create new child item
var newItem = await itemPortal.CreateChildAsync();
newItem.ProductName = "Widget";
newItem.Quantity = 5;

// Add to parent's child list
order.Items.Add(newItem);

// Save parent (which saves the list and all its children)
order = await order.SaveAsync();
```

### Editing Items

```csharp
var order = await orderPortal.FetchAsync(orderId);

// Edit an item
var item = order.Items[0];
item.Quantity = 10;

// Save parent (which updates the modified child)
order = await order.SaveAsync();
```

### Deleting Items

```csharp
var order = await orderPortal.FetchAsync(orderId);

// Remove from list (marks child for deletion)
order.Items.RemoveAt(0);

// Save parent (which deletes the removed child)
order = await order.SaveAsync();
```

## Key Points

### Child List Lifecycle

1. **Create**: Parent creates empty child list via `IChildDataPortal<ListType>.CreateChildAsync()`
2. **Fetch**: Parent fetches populated child list via `IChildDataPortal<ListType>.FetchChildAsync(criteria)`
3. **Save**: Parent calls `FieldManager.UpdateChildrenAsync()` which cascades to list and all items

### No Direct Persistence

* Child lists never have `[Insert]`, `[Update]`, or `[Delete]` operations
* The list's children are saved through their own `[InsertChild]`, `[UpdateChild]`, `[DeleteSelfChild]` operations
* All persistence happens through parent's save operation

### Data Portal Operations

Child lists only use:

* `[CreateChild]` - Initialize new empty list
* `[FetchChild]` - Load list with children from database

### Business Rules

Child lists can have authorization rules but typically don't have validation rules. Validation usually happens on the child items themselves.

### Managing Child Items

Use standard list operations to manage items:

* `Add(item)` - Add new child item
* `RemoveAt(index)` - Mark child for deletion
* `Clear()` - Mark all children for deletion
* `Items[index]` - Access child for editing

All changes are tracked and persisted when the parent saves.
