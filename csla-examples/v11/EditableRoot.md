# Editable Root Stereotype

The Editable Root stereotype represents a business object that can exist independently and be directly created, retrieved, updated, or deleted through the data portal. It is the most common CSLA stereotype for editable business entities.

**Key Characteristics**:

* Can exist independently (not contained in a parent)
* Supports full CRUD operations through root data portal
* Derives from `BusinessBase<T>`
* Supports business rules, validation, and authorization
* Can contain child objects
* Saved using `Save()` or `SaveAndMerge()` methods
* Declared `partial` so the CSLA 11 source generator can call its data portal operation methods directly (analyzer `CSLA0024`)

**Common use cases**: Customer, Order, Invoice, Product, Employee - any top-level business entity.

## Implementation Example

This example demonstrates a customer editable root object with properties, business rules, and data portal operations.

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class CustomerEdit : BusinessBase<CustomerEdit>
    {
        public partial int Id { get; private set; }
        
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public partial string Name { get; set; }
        
        [Required]
        [EmailAddress]
        public partial string Email { get; set; }
        
        public partial DateTime CreatedDate { get; private set; }
        public partial bool IsActive { get; private set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();

            // Custom rule example
            BusinessRules.AddRule(new EmailUniqueRule(EmailProperty));

            // Dependency: when Name changes, recheck Email rules
            BusinessRules.AddRule(new Csla.Rules.CommonRules.Dependency(NameProperty, EmailProperty));
        }

        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            BusinessRules.AddRule(typeof(CustomerEdit), 
                new Csla.Rules.CommonRules.IsInRole(Csla.Rules.AuthorizationActions.CreateObject, "Admin", "Manager"));
            BusinessRules.AddRule(typeof(CustomerEdit), 
                new Csla.Rules.CommonRules.IsInRole(Csla.Rules.AuthorizationActions.EditObject, "Admin", "Manager", "User"));
            BusinessRules.AddRule(typeof(CustomerEdit), 
                new Csla.Rules.CommonRules.IsInRole(Csla.Rules.AuthorizationActions.DeleteObject, "Admin"));
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
                throw new KeyNotFoundException($"Customer {id} not found");
            
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
        private class EmailUniqueRule : Csla.Rules.BusinessRule
        {
            public EmailUniqueRule(Csla.Core.IPropertyInfo primaryProperty)
                : base(primaryProperty)
            {
                InputProperties.Add(primaryProperty);
            }

            protected override void Execute(Csla.Rules.IRuleContext context)
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

### Strongly Typed Data Portal Calls (Optional)

The calls above pass criteria as `params object[]`, so a wrong argument type is only detected at runtime. In CSLA 11 you can add `[DataPortalExtensions]` to the class, and the source generator bundled in the `Csla` package creates strongly typed extension methods on `IDataPortal<CustomerEdit>` for the `[Create]`, `[Fetch]`, `[Execute]` and `[Delete]` methods:

```csharp
[CslaImplementProperties]
[DataPortalExtensions(Prefix = "Portal")]
public partial class CustomerEdit : BusinessBase<CustomerEdit>
{
    // same properties and operation methods as above
}
```

```csharp
var newCustomer = await customerPortal.PortalCreateAsync();     // [Create] Create([Inject] ...)
var customer = await customerPortal.PortalFetchAsync(customerId); // [Fetch] Fetch(int id, [Inject] ...)
await customerPortal.PortalDeleteAsync(customerId);              // [Delete] Delete(int id, [Inject] ...)
```

* The generated name is `Prefix` + operation method name (without any `Async` suffix) + `Async`. Synchronous versions (`PortalFetch(customerId)`) are generated too.
* Only criteria parameters appear; `[Inject]` parameters are still resolved on the server.
* The prefix matters here: a method named `Fetch` with no prefix would produce `FetchAsync(int id)`, which is hidden by the `IDataPortal<T>.FetchAsync(params object[])` instance method, so it is not generated (analyzer `CSLADP005`). Use a `Prefix`, or give the operation method a more specific name such as `FetchById` (which produces `FetchByIdAsync(int id)`).
* The generated class is in the business class's namespace, so the calling code needs a `using` for that namespace.
* Saving still uses `SaveAsync()`/`SaveAndMergeAsync()`; no extensions are generated for `[Insert]`, `[Update]` or `[DeleteSelf]`.

See `DataPortalExtensions.md` for details.

## Key Concepts

### Data Annotations

CSLA automatically converts data annotations into business rules. Use them for common validation:

* `[Required]` - Property must have a value
* `[StringLength]` - String length constraints
* `[Range]` - Numeric range validation
* `[EmailAddress]` - Email format validation
* `[Display]` - Friendly name (supports localization)

### Custom Business Rules

For validation that can't be expressed with data annotations, create custom rules by deriving from `Csla.Rules.BusinessRule`. See the `EmailUniqueRule` example above.

### Authorization Rules

Object-level authorization controls who can create, fetch, edit, or delete the entire object. Define these in a static method with `[ObjectAuthorizationRules]` attribute.

### Data Portal Operations

* `[Create]` - Initialize new instance with default values
* `[Fetch]` - Retrieve existing instance by criteria
* `[Insert]` - Save new instance to database
* `[Update]` - Update existing instance in database
* `[DeleteSelf]` - Delete this instance (after fetching)
* `[Delete]` - Delete by criteria (without fetching)

In CSLA 11 the data portal only calls methods that have an operation attribute. A method named `DataPortal_Fetch` (or any other legacy `DataPortal_XYZ` name) without an attribute is never called; the old name-based fallback and the `DataPortalOptions.UseLegacyOperationMethods` option are removed. Analyzer `CSLA0014` (a warning in CSLA 11) reports such methods, and its code fix adds the attribute. Once a method has an attribute, its name is up to you.

Declare the class `partial` (and any containing types). The source generator in the `Csla` package then emits code that calls the operation methods directly instead of through reflection. Analyzer `CSLA0024` reports classes that have operation methods but are not `partial`, with a code fix that adds the modifier. A non-partial class still works, through reflection.

### Root Objects with Children

If your root object contains child objects, see `EditableChild.md` for details on managing child object lifecycles. The root manages its children through `IChildDataPortal<T>` and `FieldManager.UpdateChildrenAsync()`.
