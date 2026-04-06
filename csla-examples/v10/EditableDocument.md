# Editable Document Stereotype

The Editable Document stereotype represents a business object that combines editable properties with an embedded collection of editable child objects. It merges the capabilities of `BusinessBase<T>` and `BusinessListBase<T,C>` into a single type, eliminating the need for a separate child list wrapper class when an entity has both its own properties and a collection of children.

**Key Characteristics**:

* Derives from `BusinessDocumentBase<T, C>` where T is the document type and C is the child type
* Has managed properties with full rules engine support (validation, authorization, change tracking)
* Is also a collection of editable child objects (implements `IList<C>`)
* Can be used as a root object or as a child-that-is-a-parent
* State properties (`IsDirty`, `IsValid`, `IsBusy`) aggregate across both properties and children
* Drop-in replacement for either `BusinessBase<T>` or `BusinessListBase<T,C>`
* Requires .NET 8 or later

**Common use cases**: Invoice with line items, Order with order details, Document with sections - any entity that has its own properties and directly contains a list of child objects.

## Implementation Example

This example demonstrates an invoice editable document with properties and an embedded collection of line item children.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class InvoiceEdit : BusinessDocumentBase<InvoiceEdit, LineItemEdit>
    {
        public partial int Id { get; private set; }

        [Required]
        [StringLength(20)]
        public partial string InvoiceNumber { get; set; }

        [Required]
        public partial DateTime InvoiceDate { get; set; }

        [Required]
        [StringLength(100)]
        public partial string CustomerName { get; set; }

        // Calculated property - sum of all line item totals
        public partial decimal Total { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            BusinessRules.AddRule(new Rules.CommonRules.Dependency(InvoiceDateProperty, InvoiceNumberProperty));
        }

        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            BusinessRules.AddRule(typeof(InvoiceEdit),
                new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.CreateObject, "Admin", "Billing"));
            BusinessRules.AddRule(typeof(InvoiceEdit),
                new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.EditObject, "Admin", "Billing"));
            BusinessRules.AddRule(typeof(InvoiceEdit),
                new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.DeleteObject, "Admin"));
        }

        [Create]
        private async Task Create([Inject] IChildDataPortal<LineItemEdit> itemPortal)
        {
            LoadProperty(InvoiceDateProperty, DateTime.Today);
            LoadProperty(InvoiceNumberProperty, string.Empty);
            LoadProperty(TotalProperty, 0m);
            await BusinessRules.CheckRulesAsync();
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] IInvoiceDal dal, [Inject] IChildDataPortal<LineItemEdit> itemPortal)
        {
            var data = await dal.GetAsync(id);

            LoadProperty(IdProperty, data.Id);
            LoadProperty(InvoiceNumberProperty, data.InvoiceNumber);
            LoadProperty(InvoiceDateProperty, data.InvoiceDate);
            LoadProperty(CustomerNameProperty, data.CustomerName);
            LoadProperty(TotalProperty, data.Total);

            // Load child items into the embedded collection
            var items = await dal.GetLineItemsAsync(id);
            using (LoadListMode)
            {
                foreach (var itemData in items)
                {
                    var item = await itemPortal.FetchChildAsync(itemData);
                    Add(item);
                }
            }

            await BusinessRules.CheckRulesAsync();
        }

        [Insert]
        private async Task Insert([Inject] IInvoiceDal dal)
        {
            var data = new InvoiceData
            {
                InvoiceNumber = ReadProperty(InvoiceNumberProperty),
                InvoiceDate = ReadProperty(InvoiceDateProperty),
                CustomerName = ReadProperty(CustomerNameProperty),
                Total = ReadProperty(TotalProperty)
            };

            var result = await dal.InsertAsync(data);
            LoadProperty(IdProperty, result.Id);

            // Cascade save to all child line items
            await FieldManager.UpdateChildrenAsync();
        }

        [Update]
        private async Task Update([Inject] IInvoiceDal dal)
        {
            var data = new InvoiceData
            {
                Id = ReadProperty(IdProperty),
                InvoiceNumber = ReadProperty(InvoiceNumberProperty),
                InvoiceDate = ReadProperty(InvoiceDateProperty),
                CustomerName = ReadProperty(CustomerNameProperty),
                Total = ReadProperty(TotalProperty)
            };

            await dal.UpdateAsync(data);

            // Cascade save to all child line items
            await FieldManager.UpdateChildrenAsync();
        }

        [DeleteSelf]
        private async Task DeleteSelf([Inject] IInvoiceDal dal)
        {
            await dal.DeleteAsync(ReadProperty(IdProperty));
        }

        [Delete]
        private async Task Delete(int id, [Inject] IInvoiceDal dal)
        {
            await dal.DeleteAsync(id);
        }
    }
}
```

### Child Item

The child objects contained in the document are standard editable child objects:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class LineItemEdit : BusinessBase<LineItemEdit>
    {
        public partial int Id { get; private set; }

        [Required]
        [StringLength(100)]
        public partial string ProductName { get; set; }

        [Required]
        [Range(1, 10000)]
        public partial int Quantity { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        public partial decimal Price { get; set; }

        public partial decimal LineTotal { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // Recalculate LineTotal when Quantity or Price changes
            BusinessRules.AddRule(new Rules.CommonRules.Dependency(QuantityProperty, LineTotalProperty));
            BusinessRules.AddRule(new Rules.CommonRules.Dependency(PriceProperty, LineTotalProperty));
            BusinessRules.AddRule(new CalcLineTotalRule(LineTotalProperty, QuantityProperty, PriceProperty));
        }

        [CreateChild]
        private async Task CreateChild()
        {
            LoadProperty(QuantityProperty, 1);
            LoadProperty(PriceProperty, 0m);
            LoadProperty(LineTotalProperty, 0m);
            await BusinessRules.CheckRulesAsync();
        }

        [FetchChild]
        private async Task FetchChild(LineItemData data)
        {
            LoadProperty(IdProperty, data.Id);
            LoadProperty(ProductNameProperty, data.ProductName);
            LoadProperty(QuantityProperty, data.Quantity);
            LoadProperty(PriceProperty, data.Price);
            LoadProperty(LineTotalProperty, data.LineTotal);
            await BusinessRules.CheckRulesAsync();
        }

        [InsertChild]
        private async Task InsertChild(object parent, [Inject] ILineItemDal dal)
        {
            var invoice = (InvoiceEdit)parent;
            var data = new LineItemData
            {
                InvoiceId = invoice.Id,
                ProductName = ReadProperty(ProductNameProperty),
                Quantity = ReadProperty(QuantityProperty),
                Price = ReadProperty(PriceProperty),
                LineTotal = ReadProperty(LineTotalProperty)
            };

            var result = await dal.InsertAsync(data);
            LoadProperty(IdProperty, result.Id);
        }

        [UpdateChild]
        private async Task UpdateChild(object parent, [Inject] ILineItemDal dal)
        {
            var data = new LineItemData
            {
                Id = ReadProperty(IdProperty),
                ProductName = ReadProperty(ProductNameProperty),
                Quantity = ReadProperty(QuantityProperty),
                Price = ReadProperty(PriceProperty),
                LineTotal = ReadProperty(LineTotalProperty)
            };

            await dal.UpdateAsync(data);
        }

        [DeleteSelfChild]
        private async Task DeleteSelfChild(object parent, [Inject] ILineItemDal dal)
        {
            await dal.DeleteAsync(ReadProperty(IdProperty));
        }

        private class CalcLineTotalRule : Rules.BusinessRule
        {
            private Core.IPropertyInfo _quantityProperty;
            private Core.IPropertyInfo _priceProperty;

            public CalcLineTotalRule(Core.IPropertyInfo primaryProperty,
                Core.IPropertyInfo quantityProperty, Core.IPropertyInfo priceProperty)
                : base(primaryProperty)
            {
                _quantityProperty = quantityProperty;
                _priceProperty = priceProperty;
                InputProperties = new List<Core.IPropertyInfo> { quantityProperty, priceProperty };
            }

            protected override void Execute(Rules.IRuleContext context)
            {
                var quantity = (int)context.InputPropertyValues[_quantityProperty];
                var price = (decimal)context.InputPropertyValues[_priceProperty];
                context.AddOutValue(PrimaryProperty, quantity * price);
            }
        }
    }
}
```

## Using the Editable Document

### Creating a New Instance

```csharp
// Inject IDataPortal<InvoiceEdit> via dependency injection
var invoice = await invoicePortal.CreateAsync();
invoice.InvoiceNumber = "INV-001";
invoice.CustomerName = "Acme Corp";
```

### Fetching an Existing Instance

```csharp
var invoice = await invoicePortal.FetchAsync(invoiceId);
// Properties and child items are loaded together
var lineCount = invoice.Count;
var firstItem = invoice[0];
```

### Adding Child Items

```csharp
var invoice = await invoicePortal.FetchAsync(invoiceId);

// Create a new child item
var item = await itemPortal.CreateChildAsync();
item.ProductName = "Widget";
item.Quantity = 5;
item.Price = 9.99m;

// Add directly to the document (no separate list object)
invoice.Add(item);
```

### Removing Child Items

```csharp
// Remove by index (marks child for deletion)
invoice.RemoveAt(0);

// Remove by reference
invoice.Remove(item);
```

### Saving Changes

```csharp
// Option 1: SaveAndMerge - continue using same reference
await invoice.SaveAndMergeAsync();

// Option 2: Save - get new reference
invoice = await invoice.SaveAsync();
```

### Iterating Over Children

```csharp
// Standard collection operations work
foreach (var item in invoice)
{
    Console.WriteLine($"{item.ProductName}: {item.Quantity} x {item.Price}");
}

// LINQ works too
var total = invoice.Sum(item => item.LineTotal);
var expensive = invoice.Where(item => item.Price > 100);
```

### Deleting

```csharp
// Option 1: Fetch then delete
var invoice = await invoicePortal.FetchAsync(invoiceId);
invoice.Delete();
await invoice.SaveAsync();

// Option 2: Delete without fetching
await invoicePortal.DeleteAsync(invoiceId);
```

## Key Concepts

### State Aggregation

`BusinessDocumentBase` aggregates state across the document's own properties and all child objects:

* `IsDirty` - true if any property has changed OR any child is dirty, new, or deleted
* `IsValid` - true only if the document's own rules pass AND all children are valid
* `IsBusy` - true if the document or any child is executing async rules

This means a single check on `invoice.IsValid` validates the entire object graph.

### Deleted List Management

When a child item is removed from the document, it is moved to an internal deleted list rather than discarded. During save, the deleted list is processed first (deleting items from the data store), followed by inserts and updates for active items.

```csharp
// Remove marks for deletion
invoice.RemoveAt(0);

// The removed item is tracked internally
// On save, it will be deleted from the database
await invoice.SaveAsync();
```

### Suppressing Events During Bulk Operations

Use `LoadListMode` when loading multiple children to suppress change notifications until all items are loaded:

```csharp
using (LoadListMode)
{
    foreach (var itemData in items)
    {
        var item = await itemPortal.FetchChildAsync(itemData);
        Add(item);
    }
}
```

Use `SuppressListChangedEvents` when making bulk changes in application code:

```csharp
using (invoice.SuppressListChangedEvents)
{
    // Add many items without firing CollectionChanged each time
    foreach (var data in bulkData)
    {
        var item = await itemPortal.CreateChildAsync();
        item.ProductName = data.Name;
        invoice.Add(item);
    }
}
```

### N-Level Undo

N-level undo cascades to all child objects. When `CancelEdit()` is called, both property changes and collection changes (adds, removes) are reverted.

### Performance: Avoiding N+1 Queries

Load all child data in one database call during `Fetch`, then pass individual rows to each child's `FetchChild`:

```csharp
// In InvoiceEdit Fetch
var items = await dal.GetLineItemsAsync(id);
using (LoadListMode)
{
    foreach (var itemData in items)
    {
        var item = await itemPortal.FetchChildAsync(itemData);
        Add(item);
    }
}
```

### Using as a Child-that-is-a-Parent

An editable document can also be used as a child object within a larger parent, using child data portal operations:

```csharp
[CslaImplementProperties]
public partial class OrderSectionEdit : BusinessDocumentBase<OrderSectionEdit, OrderLineEdit>
{
    public partial string SectionName { get; set; }
    public partial decimal SectionTotal { get; private set; }

    [FetchChild]
    private async Task FetchChild(SectionData data, [Inject] IChildDataPortal<OrderLineEdit> linePortal)
    {
        LoadProperty(SectionNameProperty, data.Name);

        using (LoadListMode)
        {
            foreach (var lineData in data.Lines)
            {
                var line = await linePortal.FetchChildAsync(lineData);
                Add(line);
            }
        }

        await BusinessRules.CheckRulesAsync();
    }
}
```

## When to Use Editable Document vs Editable Root + Child List

| Scenario | Recommended Approach |
| :------- | :------------------- |
| Entity has properties AND a single child collection | **Editable Document** - simpler, fewer classes |
| Entity has properties AND multiple child collections | **Editable Root** with separate child list properties |
| Entity is only a collection, no extra properties | **Editable Root List** or **Editable Child List** |
| Entity has only properties, no child collection | **Editable Root** or **Editable Child** |
| Migrating from BusinessBase + BusinessListBase pair | **Editable Document** is a drop-in replacement |
