# Read-Only Root List Stereotype

The Read-Only Root List stereotype represents a root collection of read-only child objects. The list is fetched through the data portal and contains immutable child items for display purposes.

**Key Characteristics**:

* The list itself is a root object (can exist independently)
* Contains read-only child objects
* Derives from `ReadOnlyListBase<T, C>` where T is the list type, C is the contained child type
* Only supports Fetch operation
* Cannot be modified after fetching
* Supports authorization rules on the list

**Important**: This is an immutable collection. Items cannot be added, removed, or modified after the list is fetched.

**Common use cases**: Product lists, customer directories, lookup lists, report data collections.

## Implementation Example

This example demonstrates a root list of read-only customer information objects.

```csharp
using System;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class CustomerInfoList : ReadOnlyListBase<CustomerInfoList, CustomerInfo>
    {
        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            BusinessRules.AddRule(typeof(CustomerInfoList),
                new Rules.CommonRules.IsInRole(
                    Rules.AuthorizationActions.GetObject,
                    "Admin", "Manager", "User"));
        }

        [Fetch]
        private async Task Fetch([Inject] ICustomerDal dal, [Inject] IChildDataPortal<CustomerInfo> childPortal)
        {
            // Get ALL items in ONE database call
            var items = await dal.GetAllAsync();
            
            // Load items into list
            using (LoadListMode)
            {
                foreach (var itemData in items)
                {
                    // Pass data to child - avoids N+1 query problem
                    var item = await childPortal.FetchChildAsync(itemData);
                    Add(item);
                }
            }
            
            IsReadOnly = true; // Make the list immutable
        }
    }
}
```

## Using the Read-Only Root List

### Fetching the List

```csharp
// Inject IDataPortal<CustomerInfoList> via dependency injection
var customers = await customerListPortal.FetchAsync();

// Display in grid
dataGrid.ItemsSource = customers;

// Access items
foreach (var customer in customers)
{
    Console.WriteLine(customer.Name);
}
```

### Immutability

The list is read-only after fetching:

```csharp
var customers = await customerListPortal.FetchAsync();

// These operations will throw exceptions:
// customers.Add(newCustomer);     // NotSupportedException
// customers.RemoveAt(0);           // NotSupportedException
// customers.Clear();               // NotSupportedException
```

## Root List vs Child List

### Read-Only Root List (ReadOnlyListBase as root)

* The list itself is a root object
* Contains **read-only child objects**
* Fetched through `IDataPortal<ListType>`
* Use when the collection is a top-level entity

```csharp
// Using a root list
var items = await itemListPortal.FetchAsync();
dataGrid.ItemsSource = items;
```

### Read-Only Child List (ReadOnlyListBase as child)

* The list itself is a child of a parent object
* Contains **read-only child objects**
* Fetched through parent's data portal operation
* Use when the collection belongs to a parent

See `ReadOnlyChildList.md` for child list examples.

## Performance: Avoiding N+1 Queries

**Critical**: Get all child data in ONE database query, then pass data rows to each item's `FetchChild`.

```csharp
[Fetch]
private async Task Fetch([Inject] ICustomerDal dal, [Inject] IChildDataPortal<CustomerInfo> childPortal)
{
    // ONE query for all items
    var items = await dal.GetAllAsync();
    
    using (LoadListMode)
    {
        foreach (var itemData in items)
        {
            // No database call here - just loads from data
            var item = await childPortal.FetchChildAsync(itemData);
            Add(item);
        }
    }
    
    IsReadOnly = true;
}
```

The contained child object receives data directly:

```csharp
// In CustomerInfo class
[FetchChild]
private async Task FetchChild(CustomerData data)
{
    // No database call - just load from data parameter
    LoadProperty(IdProperty, data.Id);
    LoadProperty(NameProperty, data.Name);
    LoadProperty(EmailProperty, data.Email);
    // ... etc
}
```

**Performance Note**: This pattern is essential to avoid N+1 queries.

## When to Use

**Use Read-Only Root List when**:

* Displaying a collection of read-only data
* Implementing lookup lists or reference data
* Generating reports
* Data should not be modified

**Examples**: Product catalog, customer directory, order history.

**Use Editable Root List when**:

* Users need to add, edit, or delete items
* The collection needs to be saved

See `EditableRootList.md` for editable collections.

## Key Points

### Data Portal Operations

Read-only lists only support:

* `[Fetch]` - Retrieve list with all children

No Create, Update, Insert, or Delete operations.

### Authorization

Object-level authorization on the list controls who can fetch the entire collection.

### Filtering and Sorting

Since the list is immutable, filtering and sorting should be done at the query level or using LINQ for display:

```csharp
var customers = await customerListPortal.FetchAsync();
var filtered = customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
```
