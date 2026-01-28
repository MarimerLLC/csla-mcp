````markdown
# Business Rules: Accessing Parent Objects

Sometimes a business rule needs to access properties from objects higher in the object graph. CSLA provides the `IParent` interface and `context.Target` to navigate up through the hierarchy. This pattern is useful when child object calculations depend on parent-level values.

## Overview

A common scenario is an Order with a customer discount percentage, where each Line Item must apply that discount when calculating its total. The line item rule needs to:

1. Access its own object via `context.Target`
2. Navigate up to the parent collection (LineItemList) via `IParent`
3. Navigate up again to the grandparent (OrderEdit) via `IParent`
4. Read the `CustomerDiscount` property from the order

## Enabling context.Target

By default, `context.Target` is null for performance and thread-safety reasons. To enable it, set `CanRunInParallel = false` in the rule's constructor:

```csharp
public class MyRule : BusinessRule
{
    public MyRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        // Enable access to context.Target
        CanRunInParallel = false;
        
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        // Now context.Target is available
        var target = (MyBusinessObject)context.Target;
    }
}
```

**Important:** Setting `CanRunInParallel = false` prevents the rule from running concurrently with other rules, which may impact performance when many rules execute simultaneously.

## The IParent Interface

CSLA business objects implement `IParent`, which provides access to the parent object:

```csharp
// From a child object, get its parent
var parent = ((IParent)childObject).Parent;

// Navigate multiple levels
var grandparent = ((IParent)((IParent)childObject).Parent).Parent;
```

The `Parent` property returns `null` for root objects that have no parent.

## Complete Implementation Example

### OrderEdit - Editable Root with CustomerDiscount

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;

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
        
        /// <summary>
        /// Customer discount as a percentage (e.g., 10 for 10% discount).
        /// </summary>
        [Range(0, 100)]
        public partial decimal CustomerDiscount { get; set; }
        
        // Child list of line items
        public partial LineItemList LineItems { get; private set; }
        
        // Calculated property - sum of all discounted line item totals
        public partial decimal OrderTotal { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // Rule to calculate order total from the child list
            BusinessRules.AddRule(new CalcOrderTotal(LineItemsProperty, OrderTotalProperty));
        }

        protected override async void OnChildChanged(ChildChangedEventArgs e)
        {
            base.OnChildChanged(e);

            // Recalculate order total when line items change
            if (e.ChildObject is LineItemEdit || e.ListChangedArgs != null)
            {
                await CheckRulesAsync(LineItemsProperty);
            }
        }

        [Create]
        private async Task Create([Inject] IChildDataPortal<LineItemList> lineItemsPortal)
        {
            LoadProperty(OrderDateProperty, DateTime.Today);
            LoadProperty(CustomerDiscountProperty, 0m);
            
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
            LoadProperty(CustomerDiscountProperty, data.CustomerDiscount);
            
            var lineItems = await lineItemsPortal.FetchChildAsync(data.Id);
            LoadProperty(LineItemsProperty, lineItems);
            
            LoadProperty(OrderTotalProperty, CalculateOrderTotal());
            
            await BusinessRules.CheckRulesAsync();
        }

        // ... Insert, Update, DeleteSelf methods omitted for brevity

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
}
```

### LineItemEdit - Editable Child with Parent Access Rule

The line item calculates its total by applying the customer discount from the parent order:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;
using Csla.Core;
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
        
        /// <summary>
        /// Calculated total after applying customer discount from parent order.
        /// </summary>
        public partial decimal LineItemTotal { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // When Quantity or UnitPrice changes, recalculate LineItemTotal
            BusinessRules.AddRule(new Dependency(QuantityProperty, LineItemTotalProperty));
            BusinessRules.AddRule(new Dependency(UnitPriceProperty, LineItemTotalProperty));

            // Calculation rule that reaches up to parent for discount
            BusinessRules.AddRule(new CalcLineItemTotalWithDiscount(
                LineItemTotalProperty, QuantityProperty, UnitPriceProperty));
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
            // LineItemTotal will be calculated by the rule
            
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
    /// Calculates line item total by applying the customer discount from the parent order.
    /// Uses context.Target and IParent to navigate up the object graph.
    /// </summary>
    public class CalcLineItemTotalWithDiscount : BusinessRule
    {
        private IPropertyInfo _quantityProperty;
        private IPropertyInfo _unitPriceProperty;

        public CalcLineItemTotalWithDiscount(
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

            // CRITICAL: Enable access to context.Target
            // This prevents parallel execution but allows parent access
            CanRunInParallel = false;
        }

        protected override void Execute(IRuleContext context)
        {
            int quantity = (int)context.InputPropertyValues[_quantityProperty];
            decimal unitPrice = (decimal)context.InputPropertyValues[_unitPriceProperty];

            // Calculate base total
            decimal baseTotal = quantity * unitPrice;

            // Get the discount from the parent order
            decimal discountPercent = GetCustomerDiscount(context);

            // Apply discount
            decimal discountAmount = baseTotal * (discountPercent / 100m);
            decimal total = baseTotal - discountAmount;

            context.AddOutValue(PrimaryProperty, total);
        }

        /// <summary>
        /// Navigates up the object graph to get the customer discount from the order.
        /// LineItem -> LineItemList (parent) -> OrderEdit (grandparent)
        /// </summary>
        private decimal GetCustomerDiscount(IRuleContext context)
        {
            // Get the line item (context.Target)
            var lineItem = context.Target as IParent;
            if (lineItem == null)
                return 0m;

            // Get the parent (LineItemList)
            var lineItemList = lineItem.Parent as IParent;
            if (lineItemList == null)
                return 0m;

            // Get the grandparent (OrderEdit)
            var order = lineItemList.Parent as OrderEdit;
            if (order == null)
                return 0m;

            // Return the customer discount
            return order.CustomerDiscount;
        }
    }
}
```

## Key Concepts

### Navigating Up the Object Graph

The navigation path depends on your object hierarchy:

```
OrderEdit (root)
  └── LineItemList (child list - parent of line items)
        └── LineItemEdit (child item)
```

From a `LineItemEdit`:
- `lineItem.Parent` → `LineItemList`
- `lineItemList.Parent` → `OrderEdit`

```csharp
private OrderEdit GetParentOrder(IRuleContext context)
{
    // Step 1: Get the target object (LineItemEdit)
    var lineItem = context.Target as IParent;
    if (lineItem?.Parent == null)
        return null;

    // Step 2: Get the parent collection (LineItemList)
    var collection = lineItem.Parent as IParent;
    if (collection?.Parent == null)
        return null;

    // Step 3: Get the grandparent (OrderEdit)
    return collection.Parent as OrderEdit;
}
```

### Thread Safety Considerations

Using `context.Target` introduces potential threading issues:

| Setting | Behavior | Risk |
|---------|----------|------|
| `CanRunInParallel = true` (default) | `context.Target` is null | Safe but no target access |
| `CanRunInParallel = false` | `context.Target` is available | Safe for read-only access |

**When it's safe to access parent objects:**
- Reading property values (not modifying them)
- The parent object's properties are not being modified concurrently
- You're only navigating and reading, not holding references

**When it's risky:**
- Multiple rules could modify the parent simultaneously
- Long-running operations that hold references to parent objects
- Async rules that span multiple awaits while accessing parent state

### Safe Pattern: Read-Only Access

```csharp
protected override void Execute(IRuleContext context)
{
    // Safe: Read values, calculate, return
    decimal discount = GetCustomerDiscount(context);
    
    // Use the value in calculation
    decimal total = baseTotal * (1 - discount / 100m);
    
    context.AddOutValue(PrimaryProperty, total);
}

private decimal GetCustomerDiscount(IRuleContext context)
{
    // Navigate to parent and read a single value
    var order = GetParentOrder(context);
    
    // Return the value - don't hold the reference
    return order?.CustomerDiscount ?? 0m;
}
```

### Unsafe Pattern: Modifying Parent

```csharp
// DON'T DO THIS - modifying parent from child rule
protected override void Execute(IRuleContext context)
{
    var order = GetParentOrder(context);
    
    // UNSAFE: Modifying parent from child rule
    order.SomeProperty = newValue; // This could cause threading issues
}
```

## Handling Discount Changes

When the parent's `CustomerDiscount` changes, child line items need to recalculate. Handle this in the parent's `OnPropertyChanged`:

```csharp
// In OrderEdit
protected override async void OnPropertyChanged(string propertyName)
{
    base.OnPropertyChanged(propertyName);

    // When discount changes, recalculate all line item totals
    if (propertyName == nameof(CustomerDiscount))
    {
        await RecalculateLineItemTotals();
    }
}

private async Task RecalculateLineItemTotals()
{
    var items = ReadProperty(LineItemsProperty);
    if (items == null)
        return;

    foreach (var item in items)
    {
        // Trigger rules on each line item's total property
        await item.CheckRulesAsync(LineItemEdit.LineItemTotalProperty);
    }

    // Also recalculate order total
    await CheckRulesAsync(LineItemsProperty);
}
```

## Alternative: Pass Values Down

An alternative to reaching up is to pass parent values down to children. This can be done during fetch or through a method:

```csharp
// In OrderEdit - pass discount to children when it changes
private void UpdateChildDiscount()
{
    var items = ReadProperty(LineItemsProperty);
    if (items == null)
        return;

    decimal discount = ReadProperty(CustomerDiscountProperty);
    foreach (var item in items)
    {
        item.SetParentDiscount(discount);
    }
}

// In LineItemEdit
private decimal _parentDiscount;

internal void SetParentDiscount(decimal discount)
{
    _parentDiscount = discount;
    // Trigger recalculation
    CheckRulesAsync(LineItemTotalProperty);
}
```

This approach avoids navigating up but requires more coordination between parent and children.

## Complete Usage Example

```csharp
// Create a new order with discount
var orderPortal = serviceProvider.GetRequiredService<IDataPortal<OrderEdit>>();
var lineItemPortal = serviceProvider.GetRequiredService<IChildDataPortal<LineItemEdit>>();

var order = await orderPortal.CreateAsync();
order.CustomerName = "Preferred Customer";
order.CustomerDiscount = 10m; // 10% discount

// Add a line item - discount is applied automatically
var item1 = await lineItemPortal.CreateChildAsync();
item1.ProductName = "Widget";
item1.Quantity = 5;
item1.UnitPrice = 100.00m;
order.LineItems.Add(item1);
// item1.LineItemTotal = 450.00 (500 - 10% discount)

// Add another line item
var item2 = await lineItemPortal.CreateChildAsync();
item2.ProductName = "Gadget";
item2.Quantity = 2;
item2.UnitPrice = 50.00m;
order.LineItems.Add(item2);
// item2.LineItemTotal = 90.00 (100 - 10% discount)

// Order total is sum of discounted line items
// order.OrderTotal = 540.00

// Change the discount
order.CustomerDiscount = 20m; // 20% discount
// Line items recalculate automatically
// item1.LineItemTotal = 400.00 (500 - 20% discount)
// item2.LineItemTotal = 80.00 (100 - 20% discount)
// order.OrderTotal = 480.00

// Save the order
order = await order.SaveAsync();
```

## Best Practices

1. **Always set `CanRunInParallel = false`** when using `context.Target`
2. **Only read values** from parent objects - never modify them from a child rule
3. **Don't hold references** to parent objects beyond the rule execution
4. **Handle null gracefully** at each level of navigation
5. **Use type checking** (`as` operator) to safely cast at each level
6. **Consider alternatives** like passing values down if parent access becomes complex
7. **Document the dependency** - make it clear that the child rule depends on parent state
8. **Trigger child recalculation** when relevant parent properties change

## Summary

Accessing parent objects from child rules via `context.Target` and `IParent`:

1. **Enable `context.Target`** by setting `CanRunInParallel = false` in the rule constructor
2. **Navigate up** using the `IParent` interface's `Parent` property
3. **Read values safely** - avoid modifying parent objects
4. **Handle parent changes** by triggering child rule recalculation from the parent

## Related Documentation

- [BusinessRulesChildChanged.md](BusinessRulesChildChanged.md) - Triggering parent rules from child changes
- [BusinessRulesCalculation.md](BusinessRulesCalculation.md) - Property calculation rules
- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesContext.md](BusinessRulesContext.md) - Rule context and execution flags

````
