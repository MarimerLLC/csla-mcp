# Business Rules: ChildChanged Event Handling

When working with parent-child object graphs, changes to child objects often need to trigger calculations or validations at the parent level. CSLA's `ChildChanged` event provides this capability, allowing parent objects to respond to property changes deep in the object graph.

## Overview

A common scenario is an Order that contains a list of Line Items. When a line item's quantity or price changes:

1. The **line item** calculates its own `LineItemTotal` (quantity × price)
2. The **parent order** must recalculate its `OrderTotal` (sum of all line item totals)

The parent cannot use standard `Dependency` rules because those only work for properties on the same object. Instead, the parent must:

1. Override the virtual `OnChildChanged` method
2. Examine the event args to determine what changed
3. Call `CheckRulesAsync()` to trigger rules on the appropriate parent property

## Complete Implementation Example

### LineItemEdit - Editable Child

The line item handles its own calculation when quantity or price changes:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class LineItemEdit : BusinessBase<LineItemEdit>
    {
        public partial int Id { get; private set; }
        
        [Required]
        [StringLength(100)]
        public partial string ProductName { get; set; }
        
        [Range(1, 10000)]
        public partial int Quantity { get; set; }
        
        [Range(0.01, 100000)]
        public partial decimal UnitPrice { get; set; }
        
        // Calculated property - read-only to consumers
        public partial decimal LineItemTotal { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // When Quantity or UnitPrice changes, recalculate LineItemTotal
            BusinessRules.AddRule(new Dependency(QuantityProperty, LineItemTotalProperty));
            BusinessRules.AddRule(new Dependency(UnitPriceProperty, LineItemTotalProperty));

            // Calculation rule for line item total
            BusinessRules.AddRule(new CalcLineItemTotal(LineItemTotalProperty, QuantityProperty, UnitPriceProperty));
        }

        [CreateChild]
        private async Task CreateChild()
        {
            LoadProperty(QuantityProperty, 1);
            LoadProperty(UnitPriceProperty, 0m);
            LoadProperty(LineItemTotalProperty, 0m);
            
            await BusinessRules.CheckRulesAsync();
        }

        [FetchChild]
        private async Task FetchChild(LineItemData data)
        {
            LoadProperty(IdProperty, data.Id);
            LoadProperty(ProductNameProperty, data.ProductName);
            LoadProperty(QuantityProperty, data.Quantity);
            LoadProperty(UnitPriceProperty, data.UnitPrice);
            LoadProperty(LineItemTotalProperty, data.Quantity * data.UnitPrice);
            
            await BusinessRules.CheckRulesAsync();
        }

        [InsertChild]
        private async Task InsertChild(int orderId, [Inject] ILineItemDal dal)
        {
            var data = new LineItemData
            {
                OrderId = orderId,
                ProductName = ReadProperty(ProductNameProperty),
                Quantity = ReadProperty(QuantityProperty),
                UnitPrice = ReadProperty(UnitPriceProperty)
            };
            
            var result = await dal.InsertAsync(data);
            LoadProperty(IdProperty, result.Id);
        }

        [UpdateChild]
        private async Task UpdateChild(int orderId, [Inject] ILineItemDal dal)
        {
            var data = new LineItemData
            {
                Id = ReadProperty(IdProperty),
                OrderId = orderId,
                ProductName = ReadProperty(ProductNameProperty),
                Quantity = ReadProperty(QuantityProperty),
                UnitPrice = ReadProperty(UnitPriceProperty)
            };
            
            await dal.UpdateAsync(data);
        }

        [DeleteSelfChild]
        private async Task DeleteSelfChild(int orderId, [Inject] ILineItemDal dal)
        {
            await dal.DeleteAsync(ReadProperty(IdProperty));
        }
    }

    /// <summary>
    /// Calculates the line item total from quantity and unit price.
    /// </summary>
    public class CalcLineItemTotal : BusinessRule
    {
        private IPropertyInfo _quantityProperty;
        private IPropertyInfo _unitPriceProperty;

        public CalcLineItemTotal(
            IPropertyInfo lineItemTotalProperty,
            IPropertyInfo quantityProperty,
            IPropertyInfo unitPriceProperty)
            : base(lineItemTotalProperty)
        {
            _quantityProperty = quantityProperty;
            _unitPriceProperty = unitPriceProperty;

            InputProperties.Add(quantityProperty);
            InputProperties.Add(unitPriceProperty);
            AffectedProperties.Add(lineItemTotalProperty);
        }

        protected override void Execute(IRuleContext context)
        {
            int quantity = (int)context.InputPropertyValues[_quantityProperty];
            decimal unitPrice = (decimal)context.InputPropertyValues[_unitPriceProperty];

            decimal total = quantity * unitPrice;

            context.AddOutValue(PrimaryProperty, total);
        }
    }
}
```

### LineItemList - Editable Child List

```csharp
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class LineItemList : BusinessListBase<LineItemList, LineItemEdit>
    {
        [CreateChild]
        private void CreateChild()
        {
            // Initialize empty list
        }

        [FetchChild]
        private async Task FetchChild(int orderId, [Inject] ILineItemDal dal, [Inject] IChildDataPortal<LineItemEdit> itemPortal)
        {
            // Get ALL items for this order in ONE database call
            var items = await dal.GetAllForOrderAsync(orderId);

            using (LoadListMode)
            {
                foreach (var itemData in items)
                {
                    var item = await itemPortal.FetchChildAsync(itemData);
                    Add(item);
                }
            }
        }
    }
}
```

### OrderEdit - Editable Root with OnChildChanged Override

The parent order object overrides the `OnChildChanged` method to trigger recalculation of the order total when line items change:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;
using Csla.Core;
using Csla.Rules;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class OrderEdit : BusinessBase<OrderEdit>
    {
        public partial int Id { get; private set; }
        
        [Required]
        public partial DateTime OrderDate { get; set; }
        
        [Required]
        [StringLength(100)]
        public partial string CustomerName { get; set; }
        
        // Child list of line items
        public partial LineItemList LineItems { get; private set; }
        
        // Calculated property - sum of all line item totals
        public partial decimal OrderTotal { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // Rule to calculate order total from the child list
            // This rule is attached to the LineItems property and will be
            // triggered when we call CheckRulesAsync(LineItemsProperty)
            BusinessRules.AddRule(new CalcOrderTotal(LineItemsProperty, OrderTotalProperty));
        }

        /// <summary>
        /// Override OnChildChanged to detect changes in child line items
        /// and trigger recalculation of the order total.
        /// </summary>
        protected override async void OnChildChanged(ChildChangedEventArgs e)
        {
            base.OnChildChanged(e);

            // Check if the change occurred in a LineItemEdit object
            if (e.ChildObject is LineItemEdit)
            {
                // Check if the property that changed affects the order total
                // We care about Quantity, UnitPrice, or LineItemTotal changes
                if (e.PropertyChangedArgs != null)
                {
                    string changedProperty = e.PropertyChangedArgs.PropertyName;
                    
                    if (changedProperty == nameof(LineItemEdit.Quantity) ||
                        changedProperty == nameof(LineItemEdit.UnitPrice) ||
                        changedProperty == nameof(LineItemEdit.LineItemTotal))
                    {
                        // Trigger rules on the LineItems property to recalculate OrderTotal
                        await CheckRulesAsync(LineItemsProperty);
                    }
                }
            }

            // Also handle when items are added or removed from the list
            if (e.ListChangedArgs != null)
            {
                // ListChanged indicates items were added, removed, or moved
                await CheckRulesAsync(LineItemsProperty);
            }

            // Alternative: handle CollectionChanged for more detailed info
            if (e.CollectionChangedArgs != null)
            {
                await CheckRulesAsync(LineItemsProperty);
            }
        }

        [Create]
        private async Task Create([Inject] IChildDataPortal<LineItemList> lineItemsPortal)
        {
            LoadProperty(OrderDateProperty, DateTime.Today);
            
            // Create empty child list
            var lineItems = await lineItemsPortal.CreateChildAsync();
            LoadProperty(LineItemsProperty, lineItems);
            
            LoadProperty(OrderTotalProperty, 0m);
            
            await BusinessRules.CheckRulesAsync();
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] IOrderDal dal, [Inject] IChildDataPortal<LineItemList> lineItemsPortal)
        {
            var data = await dal.GetAsync(id);
            
            if (data == null)
                throw new DataNotFoundException($"Order {id} not found");
            
            LoadProperty(IdProperty, data.Id);
            LoadProperty(OrderDateProperty, data.OrderDate);
            LoadProperty(CustomerNameProperty, data.CustomerName);
            
            // Fetch child list
            var lineItems = await lineItemsPortal.FetchChildAsync(data.Id);
            LoadProperty(LineItemsProperty, lineItems);
            
            // Calculate initial order total
            LoadProperty(OrderTotalProperty, CalculateOrderTotal());
            
            await BusinessRules.CheckRulesAsync();
        }

        [Insert]
        private async Task Insert([Inject] IOrderDal dal)
        {
            var data = new OrderData
            {
                OrderDate = ReadProperty(OrderDateProperty),
                CustomerName = ReadProperty(CustomerNameProperty)
            };
            
            var result = await dal.InsertAsync(data);
            LoadProperty(IdProperty, result.Id);
            
            // Save children - pass order ID for FK
            await FieldManager.UpdateChildrenAsync(ReadProperty(IdProperty));
        }

        [Update]
        private async Task Update([Inject] IOrderDal dal)
        {
            var data = new OrderData
            {
                Id = ReadProperty(IdProperty),
                OrderDate = ReadProperty(OrderDateProperty),
                CustomerName = ReadProperty(CustomerNameProperty)
            };
            
            await dal.UpdateAsync(data);
            
            // Save children - pass order ID for FK
            await FieldManager.UpdateChildrenAsync(ReadProperty(IdProperty));
        }

        [DeleteSelf]
        private async Task DeleteSelf([Inject] IOrderDal dal)
        {
            await dal.DeleteAsync(ReadProperty(IdProperty));
            MarkNew();
        }

        /// <summary>
        /// Helper method to calculate order total from line items.
        /// Used by both initial load and calculation rules.
        /// </summary>
        internal decimal CalculateOrderTotal()
        {
            decimal total = 0m;
            var items = ReadProperty(LineItemsProperty);
            
            if (items != null)
            {
                foreach (var item in items)
                {
                    total += item.LineItemTotal;
                }
            }
            
            return total;
        }
    }

    /// <summary>
    /// Calculates the order total by summing all line item totals.
    /// This rule is attached to the LineItems property so it runs
    /// when CheckRulesAsync(LineItemsProperty) is called.
    /// </summary>
    public class CalcOrderTotal : BusinessRule
    {
        private IPropertyInfo _orderTotalProperty;

        public CalcOrderTotal(IPropertyInfo lineItemsProperty, IPropertyInfo orderTotalProperty)
            : base(lineItemsProperty)
        {
            _orderTotalProperty = orderTotalProperty;
            
            InputProperties.Add(lineItemsProperty);
            AffectedProperties.Add(orderTotalProperty);
        }

        protected override void Execute(IRuleContext context)
        {
            var lineItems = (LineItemList)context.InputPropertyValues[PrimaryProperty];
            
            decimal total = 0m;
            if (lineItems != null)
            {
                foreach (var item in lineItems)
                {
                    total += item.LineItemTotal;
                }
            }

            context.AddOutValue(_orderTotalProperty, total);
        }
    }
}
```

## Key Concepts

### The OnChildChanged Method

The `OnChildChanged` virtual method is called whenever a child object changes anywhere lower in the object graph:

- A property changes on any child object
- Items are added to or removed from a child list
- Any nested child changes (grandchildren, etc.)

Simply override the method to respond to child changes:

```csharp
protected override async void OnChildChanged(ChildChangedEventArgs e)
{
    base.OnChildChanged(e);
    
    // Handle the change
}
```

**Note:** Always call `base.OnChildChanged(e)` to ensure the `ChildChanged` event is raised for any external subscribers.

### ChildChangedEventArgs Properties

The `ChildChangedEventArgs` provides information about what changed:

| Property | Type | Description |
|----------|------|-------------|
| `ChildObject` | object | The child object that changed |
| `PropertyChangedArgs` | PropertyChangedEventArgs | Details when a property changed (contains `PropertyName`) |
| `ListChangedArgs` | ListChangedEventArgs | Details when list contents changed |
| `CollectionChangedArgs` | NotifyCollectionChangedEventArgs | Details for collection changes |

### Checking What Changed

```csharp
protected override async void OnChildChanged(ChildChangedEventArgs e)
{
    base.OnChildChanged(e);
    
    // Check the type of child that changed
    if (e.ChildObject is LineItemEdit lineItem)
    {
        // Property change on a line item
        if (e.PropertyChangedArgs != null)
        {
            string propertyName = e.PropertyChangedArgs.PropertyName;
            // React to specific property changes
        }
    }
    
    // Check for list changes (items added/removed)
    if (e.ListChangedArgs != null)
    {
        var changeType = e.ListChangedArgs.ListChangedType;
        // ListChangedType.ItemAdded, ItemDeleted, ItemChanged, etc.
    }
    
    // Check for collection changes
    if (e.CollectionChangedArgs != null)
    {
        var action = e.CollectionChangedArgs.Action;
        // NotifyCollectionChangedAction.Add, Remove, Replace, etc.
    }
}
```

### Triggering Parent Rules

When you detect a relevant child change, trigger rules on the appropriate parent property:

```csharp
// Trigger rules on a specific property
await CheckRulesAsync(LineItemsProperty);

// This causes any rules attached to LineItemsProperty to execute,
// which can calculate dependent properties like OrderTotal
```

## Alternative Approach: Using CheckRulesAsync with Specific Property

Instead of attaching a rule to the child list property, you can trigger rules on the calculated property directly:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Rule attached to OrderTotal - requires manual triggering
    BusinessRules.AddRule(new CalcOrderTotalDirect(OrderTotalProperty));
}

protected override async void OnChildChanged(ChildChangedEventArgs e)
{
    base.OnChildChanged(e);
    
    if (ShouldRecalculateTotal(e))
    {
        // Trigger rules on the calculated property directly
        await CheckRulesAsync(OrderTotalProperty);
    }
}

private bool ShouldRecalculateTotal(ChildChangedEventArgs e)
{
    // Check if this is a line item property change we care about
    if (e.ChildObject is LineItemEdit && e.PropertyChangedArgs != null)
    {
        var prop = e.PropertyChangedArgs.PropertyName;
        return prop == nameof(LineItemEdit.Quantity) ||
               prop == nameof(LineItemEdit.UnitPrice) ||
               prop == nameof(LineItemEdit.LineItemTotal);
    }
    
    // Check for list modifications
    return e.ListChangedArgs != null || e.CollectionChangedArgs != null;
}

/// <summary>
/// Alternative rule that reads from the parent's child list directly.
/// </summary>
public class CalcOrderTotalDirect : BusinessRule
{
    public CalcOrderTotalDirect(IPropertyInfo orderTotalProperty)
        : base(orderTotalProperty)
    {
        AffectedProperties.Add(orderTotalProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        // Access the target object to get the child list
        var order = (OrderEdit)context.Target;
        decimal total = order.CalculateOrderTotal();

        context.AddOutValue(PrimaryProperty, total);
    }
}
```

## Handling Deeply Nested Changes

For deeper object graphs, `OnChildChanged` is called for changes at any level:

```csharp
// Order -> LineItems -> LineItem -> SubItems -> SubItem
// A change to SubItem.Property will trigger OnChildChanged on Order

protected override async void OnChildChanged(ChildChangedEventArgs e)
{
    base.OnChildChanged(e);
    
    // Handle changes at any level
    switch (e.ChildObject)
    {
        case SubItemEdit subItem:
            // Handle sub-item changes
            await CheckRulesAsync(LineItemsProperty);
            break;
            
        case LineItemEdit lineItem:
            // Handle line item changes
            if (IsRelevantLineItemChange(e))
            {
                await CheckRulesAsync(LineItemsProperty);
            }
            break;
            
        case LineItemList list:
            // Handle list-level changes
            await CheckRulesAsync(LineItemsProperty);
            break;
    }
}
```

## Why Use OnChildChanged Instead of Event Subscription

Using the `OnChildChanged` override is simpler and more reliable than subscribing to the `ChildChanged` event:

| Approach | OnChildChanged Override | ChildChanged Event |
|----------|------------------------|-------------------|
| Setup required | None - just override | Subscribe in Create, Fetch, OnDeserialized |
| Serialization | Works automatically | Must re-subscribe after deserialization |
| Code complexity | Single override method | Multiple subscription points |
| Risk of memory leaks | None | Possible if not properly unsubscribed |

### Alternative: Event Subscription

If you need external components to respond to child changes, they can subscribe to the `ChildChanged` event. The `OnChildChanged` override raises this event (via `base.OnChildChanged(e)`):

```csharp
// External subscriber
order.ChildChanged += (sender, e) =>
{
    // React to child changes from outside the object
    Console.WriteLine($"Child changed: {e.ChildObject?.GetType().Name}");
};
```

## Performance Considerations

### Avoid Unnecessary Rule Execution

Only trigger rules when relevant properties change:

```csharp
protected override async void OnChildChanged(ChildChangedEventArgs e)
{
    base.OnChildChanged(e);
    
    // Only recalculate for specific property changes
    if (e.ChildObject is LineItemEdit && e.PropertyChangedArgs != null)
    {
        var propName = e.PropertyChangedArgs.PropertyName;
        
        // Ignore changes to non-financial properties
        if (propName == nameof(LineItemEdit.ProductName))
            return;
        
        // Only recalculate for Quantity, UnitPrice, or LineItemTotal
        if (propName == nameof(LineItemEdit.Quantity) ||
            propName == nameof(LineItemEdit.UnitPrice) ||
            propName == nameof(LineItemEdit.LineItemTotal))
        {
            await CheckRulesAsync(LineItemsProperty);
        }
    }
}
```

### Debouncing Rapid Changes

For scenarios with rapid changes (e.g., user typing quickly), consider debouncing:

```csharp
private CancellationTokenSource _recalculateCts;

protected override async void OnChildChanged(ChildChangedEventArgs e)
{
    base.OnChildChanged(e);
    
    if (!ShouldRecalculateTotal(e))
        return;

    // Cancel any pending recalculation
    _recalculateCts?.Cancel();
    _recalculateCts = new CancellationTokenSource();

    try
    {
        // Wait briefly for more changes
        await Task.Delay(100, _recalculateCts.Token);
        
        // No more changes - recalculate
        await CheckRulesAsync(LineItemsProperty);
    }
    catch (TaskCanceledException)
    {
        // Another change came in - this recalculation was cancelled
    }
}
```

## Complete Usage Example

```csharp
// Create a new order
var orderPortal = serviceProvider.GetRequiredService<IDataPortal<OrderEdit>>();
var lineItemPortal = serviceProvider.GetRequiredService<IChildDataPortal<LineItemEdit>>();

var order = await orderPortal.CreateAsync();
order.CustomerName = "Acme Corp";

// Add a line item
var item1 = await lineItemPortal.CreateChildAsync();
item1.ProductName = "Widget";
item1.Quantity = 5;
item1.UnitPrice = 10.00m;
order.LineItems.Add(item1);
// OnChildChanged fires -> OrderTotal is now 50.00

// Add another line item
var item2 = await lineItemPortal.CreateChildAsync();
item2.ProductName = "Gadget";
item2.Quantity = 2;
item2.UnitPrice = 25.00m;
order.LineItems.Add(item2);
// OnChildChanged fires -> OrderTotal is now 100.00

// Modify a line item
item1.Quantity = 10;
// OnChildChanged fires (Quantity changed)
// LineItem rule fires -> item1.LineItemTotal is now 100.00
// OnChildChanged fires (LineItemTotal changed)
// Order rule fires -> OrderTotal is now 150.00

// Remove a line item
order.LineItems.Remove(item2);
// OnChildChanged fires (ListChanged)
// Order rule fires -> OrderTotal is now 100.00

// Save the order
order = await order.SaveAsync();
```

## Summary

The `OnChildChanged` override enables parent objects to respond to changes anywhere in the object graph. Key points:

1. **Override OnChildChanged** - simpler than event subscription, works automatically after serialization
2. **Call base.OnChildChanged(e)** - ensures the `ChildChanged` event is raised for external subscribers
3. **Examine ChildChangedEventArgs** to determine what changed
4. **Call CheckRulesAsync()** on the appropriate property to trigger parent-level rules
5. **Filter events** to avoid unnecessary rule execution

## Related Documentation

- [BusinessRulesCalculation.md](BusinessRulesCalculation.md) - Property calculation rules
- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [EditableChildList.md](v10/EditableChildList.md) - Editable child list implementation
- [EditableRoot.md](v10/EditableRoot.md) - Editable root implementation
- [EditableChild.md](v10/EditableChild.md) - Editable child implementation

