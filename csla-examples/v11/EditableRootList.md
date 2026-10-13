# Editable Root List Stereotype

The Editable Root List stereotype represents a root collection of editable child objects. The list itself is a root object that can be directly created and fetched through the data portal, but it contains child objects.

**Key Characteristics**:

* The list itself is a root object (can exist independently)
* Contains editable child objects (not root objects)
* Derives from `BusinessListBase<T, C>` where T is the list type, C is the contained child type
* List is fetched/created through root data portal
* Children are saved through the list's save operation
* Supports authorization rules on the list
* Enforces `AllowNew` and `AllowRemove` (CSLA 11): adding or removing items when the matching property is `false` throws `NotSupportedException`
* Declared `partial` so the CSLA 11 source generator can call its operation methods directly (analyzer `CSLA0024`)

**Important**: When you call `Save()` on the list, it automatically saves all child items through their Insert/Update/DeleteSelf operations.

**Common use cases**: Collection of addresses, list of phone numbers, collection of order line items - where the entire collection is managed as a unit.

## Implementation Example

This example demonstrates a root list of order items where each item is an editable child object.

```csharp
using System;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class OrderItemList : BusinessListBase<OrderItemList, OrderItemEdit>
    {
        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            BusinessRules.AddRule(typeof(OrderItemList), 
                new Csla.Rules.CommonRules.IsInRole(Csla.Rules.AuthorizationActions.GetObject, "Admin", "Manager"));
            BusinessRules.AddRule(typeof(OrderItemList), 
                new Csla.Rules.CommonRules.IsInRole(Csla.Rules.AuthorizationActions.EditObject, "Admin", "Manager", "User"));
        }

        [Create]
        private void Create()
        {
            // Initialize empty list
        }

        [Fetch]
        private async Task Fetch([Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
        {
            // Get ALL items in ONE database call
            var items = await dal.GetAllAsync();
            
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

        [Update]
        private async Task Update()
        {
            // This will call Insert/Update/DeleteSelf on all child items
            await FieldManager.UpdateChildrenAsync();
        }
    }
}
```

## Controlling Add and Remove (AllowNew / AllowRemove)

In CSLA 11, `BusinessListBase` enforces its `AllowNew` and `AllowRemove` properties. Earlier versions only used them as hints for data binding.

* Both default to `true`. (`AllowRemove` previously defaulted to `false` for `BusinessListBase`.)
* When `AllowNew` is `false`, `AddNew`, `AddNewAsync`, `Add`, `AddRange` and `Insert` throw `NotSupportedException`.
* When `AllowRemove` is `false`, `Remove`, `RemoveAt`, `Clear` and `RemoveItemAsync` throw `NotSupportedException`.
* Replacing an item through the indexer (`list[i] = item`) is still allowed, and n-level undo, deserialization and graph merging still add and remove items as needed.
* The setters are `protected`, so set them inside the list class.
* `LoadListMode` does NOT bypass the check. If the list should not allow adds or removes, set the properties to `false` AFTER the items are loaded in the fetch operation. The value is kept when the data portal returns the list to the client.

```csharp
[Fetch]
private async Task Fetch([Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
{
    var items = await dal.GetAllAsync();

    using (LoadListMode)
    {
        foreach (var itemData in items)
            Add(await itemPortal.FetchChildAsync(itemData));
    }

    // Set AFTER loading. Setting AllowNew = false before the loop
    // would make Add() throw NotSupportedException.
    AllowNew = false;
    AllowRemove = false;
}
```

UI code that adds or removes items should check `list.AllowNew` / `list.AllowRemove` first, or be prepared to handle `NotSupportedException`.

## Root List vs Child List

### Editable Root List (BusinessListBase as root)

* The list itself is a root object
* Contains **editable child objects**
* Fetched through `IDataPortal<ListType>`
* Saved through list's `Save()` method which cascades to children
* Use when the collection is a top-level entity

```csharp
// Using a root list
var items = await itemListPortal.FetchAsync();
items.Add(newItem);
items = await items.SaveAsync();  // Saves all children
```

### Editable Child List (BusinessListBase as child)

* The list itself is a child of a parent object
* Contains **editable child objects**  
* Fetched through parent's data portal operation
* Saved through parent's save operation
* Use when the collection belongs to a parent

See `EditableChild.md` for child list examples.

## Using the Editable Root List

### Fetching the List

```csharp
// Inject IDataPortal<OrderItemList> via dependency injection
var items = await itemListPortal.FetchAsync();

// Display in grid
dataGrid.ItemsSource = items;
```

### Adding Items

```csharp
// Create new child item through child data portal
var newItem = await itemPortal.CreateChildAsync();
newItem.ProductName = "Widget";
newItem.Quantity = 5;

// Add to list (throws NotSupportedException if items.AllowNew is false)
items.Add(newItem);

// Save the list (which saves all children)
items = await items.SaveAsync();
```

### Editing Items

```csharp
// User edits item in grid
var item = items[0];
item.Quantity = 10;

// Save the list (which updates the modified child)
items = await items.SaveAsync();
```

### Deleting Items

```csharp
// Remove from list (marks child for deletion)
// Throws NotSupportedException if items.AllowRemove is false
items.RemoveAt(0);

// Save the list (which deletes the removed child)
items = await items.SaveAsync();
```

## Performance Consideration

**Critical**: When fetching the list, get all data in ONE database query, then pass data rows to each item's `FetchChild`. This avoids the N+1 query problem.

```csharp
[Fetch]
private async Task Fetch([Inject] IOrderItemDal dal, [Inject] IChildDataPortal<OrderItemEdit> itemPortal)
{
    // ONE query for all items
    var items = await dal.GetAllAsync();
    
    using (LoadListMode)
    {
        foreach (var itemData in items)
        {
            // No database call here - just loads from data
            var item = await itemPortal.FetchChildAsync(itemData);
            Add(item);
        }
    }
}
```

The contained object's `FetchChild` receives the data directly:

```csharp
// In OrderItemEdit class
[FetchChild]
private async Task FetchChild(OrderItemData data)
{
    // No database call - just load properties
    LoadProperty(IdProperty, data.Id);
    LoadProperty(ProductNameProperty, data.ProductName);
    // ... etc
    
    await BusinessRules.CheckRulesAsync();
}
```

## Strongly Typed Data Portal Calls (Optional)

With `[DataPortalExtensions(Prefix = "Portal")]` on the list class, the bundled source generator creates `PortalCreateAsync()` and `PortalFetchAsync()` extension methods on `IDataPortal<OrderItemList>` that match the `[Create]` and `[Fetch]` criteria parameters (here, none):

```csharp
var items = await itemListPortal.PortalFetchAsync();
```

Without a prefix, methods named `Create` and `Fetch` would produce names hidden by the `IDataPortal<T>` instance methods, so nothing would be generated for them. See `DataPortalExtensions.md`.

## When to Use Root List

**Use Editable Root List when**:

* You need a standalone collection that can be fetched/saved independently
* The collection is a top-level entity (not contained in another object)
* Items are child objects managed by the list
* Binding to a grid where the entire list is saved as a unit

**Examples**: List of addresses for display/editing, collection of phone numbers, stand-alone list of items.

**Use Editable Child List when**:

* The collection belongs to a parent object
* The list is saved as part of the parent's save operation
* Examples: Order contains OrderItems, Customer contains Addresses

**Use Dynamic Root List when**:

* Items are editable root objects (not children)
* Each item saved/deleted independently
* Primarily for grid binding with row-level save/delete
* Derives from `DynamicListBase<T>` (where `T` is the editable root item type); `AllowNew`/`AllowRemove` are enforced there too, and `AllowRemove` also defaults to `true` in CSLA 11
