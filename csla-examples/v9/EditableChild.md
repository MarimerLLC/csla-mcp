# Editable Child Stereotype

The Editable Child stereotype represents a business object that is always contained within a parent object (root or another child). Child objects are part of an object graph and are managed through the parent's lifecycle.

**Key Characteristics**:

* Always contained within a parent object (cannot exist independently)
* Uses child data portal operations (`[CreateChild]`, `[FetchChild]`, `[InsertChild]`, `[UpdateChild]`, `[DeleteSelfChild]`)
* Managed by parent - created, fetched, and saved as part of parent's operations
* Derives from `BusinessBase<T>` (same as editable root)
* Supports full business rules, validation, and authorization

**Common use cases**: Order items in an order, addresses in a customer, line items in an invoice.

## Implementation Example

This example shows an order item as an editable child within an order.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class OrderItemEdit : BusinessBase<OrderItemEdit>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }

        public static readonly PropertyInfo<string> ProductNameProperty = RegisterProperty<string>(nameof(ProductName));
        [Required]
        [StringLength(100)]
        public string ProductName
        {
            get => GetProperty(ProductNameProperty);
            set => SetProperty(ProductNameProperty, value);
        }

        public static readonly PropertyInfo<int> QuantityProperty = RegisterProperty<int>(nameof(Quantity));
        [Range(1, 1000)]
        public int Quantity
        {
            get => GetProperty(QuantityProperty);
            set => SetProperty(QuantityProperty, value);
        }

        public static readonly PropertyInfo<decimal> PriceProperty = RegisterProperty<decimal>(nameof(Price));
        [Range(0.01, 10000.00)]
        public decimal Price
        {
            get => GetProperty(PriceProperty);
            set => SetProperty(PriceProperty, value);
        }

        // Calculated property
        public static readonly PropertyInfo<decimal> TotalProperty = RegisterProperty<decimal>(nameof(Total));
        public decimal Total
        {
            get => GetProperty(TotalProperty);
            private set => LoadProperty(TotalProperty, value);
        }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();
            
            // Property-level authorization rule
            BusinessRules.AddRule(
                new Rules.CommonRules.IsInRole(
                    Rules.AuthorizationActions.WriteProperty, 
                    PriceProperty, 
                    "Manager", "Admin"));
        }

        [CreateChild]
        private async Task CreateChild([Inject] IOrderItemDal dal)
        {
            // Initialize default values
            LoadProperty(QuantityProperty, 1);
            LoadProperty(PriceProperty, 0.00m);
            
            await BusinessRules.CheckRulesAsync();
        }

        [FetchChild]
        private async Task FetchChild(int id, [Inject] IOrderItemDal dal)
        {
            var data = await dal.GetAsync(id);
            LoadProperty(IdProperty, data.Id);
            LoadProperty(ProductNameProperty, data.ProductName);
            LoadProperty(QuantityProperty, data.Quantity);
            LoadProperty(PriceProperty, data.Price);
            
            await BusinessRules.CheckRulesAsync();
        }

        [InsertChild]
        private async Task InsertChild([Inject] IOrderItemDal dal)
        {
            var data = new OrderItemData
            {
                ProductName = ReadProperty(ProductNameProperty),
                Quantity = ReadProperty(QuantityProperty),
                Price = ReadProperty(PriceProperty)
            };
            
            var result = await dal.InsertAsync(data);
            LoadProperty(IdProperty, result.Id);
        }

        [UpdateChild]
        private async Task UpdateChild([Inject] IOrderItemDal dal)
        {
            if (!IsDirty) return;
            
            var data = new OrderItemData
            {
                Id = ReadProperty(IdProperty),
                ProductName = ReadProperty(ProductNameProperty),
                Quantity = ReadProperty(QuantityProperty),
                Price = ReadProperty(PriceProperty)
            };
            
            await dal.UpdateAsync(data);
        }

        [DeleteSelfChild]
        private async Task DeleteSelfChild([Inject] IOrderItemDal dal)
        {
            await dal.DeleteAsync(ReadProperty(IdProperty));
        }
    }
}
```

## Parent-Child Relationship

Child objects are managed by their parent. Here's how a parent (editable root) manages a child:

```csharp
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

    // Child object property
    public static readonly PropertyInfo<OrderItemEdit> ItemProperty = RegisterProperty<OrderItemEdit>(nameof(Item));
    public OrderItemEdit Item
    {
        get => GetProperty(ItemProperty);
        private set => LoadProperty(ItemProperty, value);
    }

    [Fetch]
    private async Task Fetch(int id, [Inject] IOrderDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
    {
        var data = await dal.GetAsync(id);
        LoadProperty(IdProperty, data.Id);
        LoadProperty(OrderDateProperty, data.OrderDate);
        
        // Fetch the child using child data portal
        var item = await itemPortal.FetchChildAsync(data.ItemId);
        LoadProperty(ItemProperty, item);
        
        await BusinessRules.CheckRulesAsync();
    }

    [Insert]
    private async Task Insert([Inject] IOrderDal dal)
    {
        // Insert order data
        var result = await dal.InsertAsync(new OrderData { OrderDate = OrderDate });
        LoadProperty(IdProperty, result.Id);
        
        // Child data portal will automatically call InsertChild on the child
        await FieldManager.UpdateChildrenAsync();
    }

    [Update]
    private async Task Update([Inject] IOrderDal dal)
    {
        await dal.UpdateAsync(new OrderData { Id = Id, OrderDate = OrderDate });
        
        // Child data portal will automatically call UpdateChild or DeleteSelfChild on children
        await FieldManager.UpdateChildrenAsync();
    }
}
```

## Key Points for Child Objects

### Creating Child Instances

Parents create children using `IChildDataPortal<T>`:

```csharp
// In parent's Create or Fetch operation
var child = await childPortal.CreateChildAsync();
LoadProperty(ChildProperty, child);
```

### Fetching Children Efficiently in Lists

**Important**: When a parent is a list, fetch all child data in one database query, then pass individual rows to each child's `FetchChild` operation. This avoids the N+1 query problem.

```csharp
public class OrderItemList : BusinessListBase<OrderItemList, OrderItemEdit>
{
    [Fetch]
    private async Task Fetch(int orderId, [Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
    {
        // Get ALL items for this order in ONE database call
        var items = await dal.GetAllForOrderAsync(orderId);
        
        // Loop through the data and create child objects
        foreach (var itemData in items)
        {
            // Pass the data row to child - child does NOT make its own database call
            var child = await itemPortal.FetchChildAsync(itemData);
            Add(child);
        }
    }
}
```

The child's `FetchChild` receives the data directly:

```csharp
[FetchChild]
private async Task FetchChild(OrderItemData data)
{
    // No database call here - just load from the data parameter
    LoadProperty(IdProperty, data.Id);
    LoadProperty(ProductNameProperty, data.ProductName);
    LoadProperty(QuantityProperty, data.Quantity);
    LoadProperty(PriceProperty, data.Price);
    
    await BusinessRules.CheckRulesAsync();
}
```

**Performance Note**: This pattern is critical for performance. Having each child make its own database call would result in N+1 queries (one for the parent list, plus one for each child).

### Child Persistence

* Child insert/update/delete is triggered by parent's save operation
* Parent calls `FieldManager.UpdateChildrenAsync()` which invokes child operations
* Children are never saved independently - always through parent

### Child Data Portal Operations

* `[CreateChild]` - Initialize new child instance
* `[FetchChild]` - Load existing child data (called by parent)
* `[InsertChild]` - Save new child (called during parent save)
* `[UpdateChild]` - Update existing child (called during parent save)
* `[DeleteSelfChild]` - Delete child (called during parent save if child is deleted)

### Business Rules in Children

Children support full business rules including:

* Validation rules (Required, Range, etc.)
* Property-level authorization
* Custom business rules
* Calculated properties

Rules are checked independently but contribute to parent's validation state.
            Price = ReadProperty(PriceProperty)
        };
        dal.Update(data);
    }

    [DeleteSelfChild]
    private void DeleteSelfChild([Inject] IOrderDetailDal dal)
    {
        dal.Delete(ReadProperty(IdProperty));
    }
}
```
