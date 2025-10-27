# Editable Root Stereotype

The Editable Root stereotype represents a business object that can exist independently and be directly created, retrieved, updated, or deleted through the data portal. It is the most common CSLA stereotype for editable business entities.

**Key Characteristics**:

* Can exist independently (not contained in a parent)
* Supports full CRUD operations through root data portal
* Derives from `BusinessBase<T>`
* Supports business rules, validation, and authorization
* Can contain child objects
* Saved using `Save()` or `SaveAndMerge()` methods

**Common use cases**: Customer, Order, Invoice, Product, Employee - any top-level business entity.

## Implementation Example

This example demonstrates a customer editable root object with properties, business rules, and data portal operations.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [Serializable]
    public class CustomerEdit : BusinessBase<CustomerEdit>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }

        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string Name
        {
            get => GetProperty(NameProperty);
            set => SetProperty(NameProperty, value);
        }

        public static readonly PropertyInfo<string> EmailProperty = RegisterProperty<string>(nameof(Email));
        [Required]
        [EmailAddress]
        public string Email
        {
            get => GetProperty(EmailProperty);
            set => SetProperty(EmailProperty, value);
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
            set => SetProperty(IsActiveProperty, value);
        }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // Custom rule example
            BusinessRules.AddRule(new EmailUniqueRule(EmailProperty));

            // Dependency: when Name changes, recheck Email rules
            BusinessRules.AddRule(new Rules.CommonRules.Dependency(NameProperty, EmailProperty));
        }

        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            BusinessRules.AddRule(typeof(CustomerEdit), 
                new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.CreateObject, "Admin", "Manager"));
            BusinessRules.AddRule(typeof(CustomerEdit), 
                new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.EditObject, "Admin", "Manager", "User"));
            BusinessRules.AddRule(typeof(CustomerEdit), 
                new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.DeleteObject, "Admin"));
        }

        [Create]
        private async Task Create([Inject] ICustomerDal dal)
        {
            // Get default values from DAL
            var data = await dal.CreateAsync();
            
            LoadProperty(CreatedDateProperty, data.CreatedDate);
            LoadProperty(IsActiveProperty, data.IsActive);
            
            await BusinessRules.CheckRulesAsync();
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

            await BusinessRules.CheckRulesAsync();
        }

        [Insert]
        private async Task Insert([Inject] ICustomerDal dal)
        {
            var data = new CustomerData
            {
                Name = ReadProperty(NameProperty),
                Email = ReadProperty(EmailProperty),
                CreatedDate = ReadProperty(CreatedDateProperty),
                IsActive = ReadProperty(IsActiveProperty)
            };
            
            var result = await dal.InsertAsync(data);
            
            LoadProperty(IdProperty, result.Id);
            LoadProperty(CreatedDateProperty, result.CreatedDate);
        }

        [Update]
        private async Task Update([Inject] ICustomerDal dal)
        {
            var data = new CustomerData
            {
                Id = ReadProperty(IdProperty),
                Name = ReadProperty(NameProperty),
                Email = ReadProperty(EmailProperty),
                CreatedDate = ReadProperty(CreatedDateProperty),
                IsActive = ReadProperty(IsActiveProperty)
            };
            
            await dal.UpdateAsync(data);
        }

        [DeleteSelf]
        private async Task DeleteSelf([Inject] ICustomerDal dal)
        {
            await dal.DeleteAsync(ReadProperty(IdProperty));
            MarkNew();
        }

        [Delete]
        private async Task Delete(int id, [Inject] ICustomerDal dal)
        {
            await dal.DeleteAsync(id);
        }

        // Custom business rule example
        private class EmailUniqueRule : Rules.BusinessRule
        {
            public EmailUniqueRule(Core.IPropertyInfo primaryProperty)
                : base(primaryProperty)
            {
                InputProperties = new List<Core.IPropertyInfo> { primaryProperty };
            }

            protected override void Execute(Rules.IRuleContext context)
            {
                var email = (string)context.InputPropertyValues[PrimaryProperty];

                if (!string.IsNullOrEmpty(email))
                {
                    // In real implementation, this would check against database
                    if (email.ToLower() == "duplicate@example.com")
                    {
                        context.AddErrorResult("Email address is already in use.");
                    }
                }
            }
        }
    }
}
```

## Using the Editable Root

### Creating a New Instance

```csharp
// Inject IDataPortal<CustomerEdit> via dependency injection
var customer = await customerPortal.CreateAsync();
customer.Name = "John Doe";
customer.Email = "john@example.com";
```

### Fetching an Existing Instance

```csharp
var customer = await customerPortal.FetchAsync(customerId);
customer.Name = "Jane Doe";
```

### Saving Changes

```csharp
// Option 1: SaveAndMerge - continue using same reference
await customer.SaveAndMergeAsync();
// customer reference is still valid

// Option 2: Save - get new reference
customer = await customer.SaveAsync();
// must use returned reference
```

### Deleting

```csharp
// Option 1: Fetch then delete (uses DeleteSelf)
var customer = await customerPortal.FetchAsync(customerId);
customer.Delete();
await customer.SaveAsync();

// Option 2: Delete without fetching (uses Delete)
await customerPortal.DeleteAsync(customerId);
```

## Key Concepts

### Data Annotations

CSLA automatically converts data annotations into business rules. Use them for common validation:

* `[Required]` - Property must have a value
* `[StringLength]` - String length constraints
* `[Range]` - Numeric range validation
* `[EmailAddress]` - Email format validation
* `[Display]` - Friendly name (supports localization)

### Custom Business Rules

For validation that can't be expressed with data annotations, create custom rules by deriving from `Rules.BusinessRule`. See the `EmailUniqueRule` example above.

### Authorization Rules

Object-level authorization controls who can create, fetch, edit, or delete the entire object. Define these in a static method with `[ObjectAuthorizationRules]` attribute.

### Data Portal Operations

* `[Create]` - Initialize new instance with default values
* `[Fetch]` - Retrieve existing instance by criteria
* `[Insert]` - Save new instance to database
* `[Update]` - Update existing instance in database
* `[DeleteSelf]` - Delete this instance (after fetching)
* `[Delete]` - Delete by criteria (without fetching)

### Root Objects with Children

If your root object contains child objects, see `EditableChild.md` for details on managing child object lifecycles. The root manages its children through `IChildDataPortal<T>` and `FieldManager.UpdateChildrenAsync()`.
