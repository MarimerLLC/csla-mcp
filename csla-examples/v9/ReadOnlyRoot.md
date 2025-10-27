# Read-Only Root Stereotype

The Read-Only Root stereotype represents an immutable business object that can be fetched but not modified. It's used for displaying data that users can view but not edit.

**Key Characteristics**:

* Can exist independently (not contained in a parent)
* Derives from `ReadOnlyBase<T>`
* All properties are read-only (private set)
* Only supports Fetch operation (no Create, Insert, Update, Delete)
* Supports authorization rules
* Often used for reports, lookups, and display-only data

**Common use cases**: Product catalog display, customer information lookup, report data, historical records.

## Implementation Example

This example demonstrates a read-only customer information object.

```csharp
using System;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    public class CustomerInfo : ReadOnlyBase<CustomerInfo>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }

        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        public string Name
        {
            get => GetProperty(NameProperty);
            private set => LoadProperty(NameProperty, value);
        }

        public static readonly PropertyInfo<string> EmailProperty = RegisterProperty<string>(nameof(Email));
        public string Email
        {
            get => GetProperty(EmailProperty);
            private set => LoadProperty(EmailProperty, value);
        }

        public static readonly PropertyInfo<DateTime> CreatedDateProperty = RegisterProperty<DateTime>(nameof(CreatedDate));
        public DateTime CreatedDate
        {
            get => GetProperty(CreatedDateProperty);
            private set => LoadProperty(CreatedDateProperty, value);
        }

        public static readonly PropertyInfo<bool> IsActiveProperty = RegisterProperty<bool>(nameof(IsActive));
        public bool IsActive
        {
            get => GetProperty(IsActiveProperty);
            private set => LoadProperty(IsActiveProperty, value);
        }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();
            
            // Property-level authorization: only Admin can view Email
            BusinessRules.AddRule(
                new Rules.CommonRules.IsInRole(
                    Rules.AuthorizationActions.ReadProperty,
                    EmailProperty,
                    "Admin"));
        }

        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            // Object-level authorization
            BusinessRules.AddRule(typeof(CustomerInfo),
                new Rules.CommonRules.IsInRole(
                    Rules.AuthorizationActions.GetObject,
                    "Admin", "Manager", "User"));
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] ICustomerDal dal)
        {
            var data = await dal.GetAsync(id);
            
            if (data == null)
                throw new DataNotFoundException($"Customer {id} not found");
            
            LoadProperty(IdProperty, data.Id);
            LoadProperty(NameProperty, data.Name);
            LoadProperty(EmailProperty, data.Email);
            LoadProperty(CreatedDateProperty, data.CreatedDate);
            LoadProperty(IsActiveProperty, data.IsActive);
        }
    }
}
```

## Using the Read-Only Root

### Fetching an Instance

```csharp
// Inject IDataPortal<CustomerInfo> via dependency injection
var customer = await customerInfoPortal.FetchAsync(customerId);

// Display in UI
lblName.Text = customer.Name;
lblEmail.Text = customer.Email;
```

### Read-Only Properties

All properties are read-only. Attempting to set a value will result in a compile error:

```csharp
var customer = await customerInfoPortal.FetchAsync(customerId);
// customer.Name = "New Name"; // Compile error - property is read-only
```

## Key Concepts

### When to Use Read-Only Root

**Use Read-Only Root when**:

* Data is for display only
* Users should not be able to modify the data
* Data comes from queries or views optimized for reading
* Implementing reports or read-only grids

**Use Editable Root when**:

* Users need to modify the data
* Data needs to be created, updated, or deleted

See `EditableRoot.md` for editable objects.

### Authorization

**Object-level authorization**: Controls who can fetch the object (defined with `[ObjectAuthorizationRules]`)

**Property-level authorization**: Controls who can read specific properties (defined in `AddBusinessRules()`)

### Data Portal Operations

Read-only roots only support:

* `[Fetch]` - Retrieve instance from database

No Create, Insert, Update, Delete, or DeleteSelf operations.

### Performance

Read-only objects are lighter weight than editable objects:

* No change tracking
* No n-level undo
* No dirty state management
* Optimized for display scenarios

### Read-Only Objects with Children

Read-only roots can contain read-only child objects or lists. See `ReadOnlyChild.md` and `ReadOnlyChildList.md` for details.
