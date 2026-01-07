# Editable Root Stereotype

This example demonstrates a complete CSLA business class named `CustomerEdit` that includes various property types, business rules, authorization rules, and data access methods. The class derives from `BusinessBase<T>` and includes both read-only and read-write properties.

This class demonstrates the editable root business class stereotype.

> **Note:** This implementation uses the `CslaImplementProperties` attribute to generate most of the code you had to write by hand in previous versions of CSLA. You can still use the CSLA v9 coding approach if desired, but the code generation in CSLA 10 makes things much simpler.

The example also shows how to implement business rules for validation, including required fields, string length constraints, and a custom rule to ensure email uniqueness. Additionally, it includes object-level authorization rules to control access based on user roles.

It also includes data portal operation methods for creating, fetching, inserting, updating, and deleting customer records. Note that the data access methods contain placeholder comments where actual data access logic should be invoked.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using Csla;

namespace CslaExamples
{
    [CslaImplementProperties]
    public partial class CustomerEdit : BusinessBase<CustomerEdit>
    {
        public partial int Id  { get; private set; }
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
            // Call base first
            base.AddBusinessRules();

            // Add custom business rules
            BusinessRules.AddRule(new Rules.CommonRules.Required(NameProperty));
            BusinessRules.AddRule(new Rules.CommonRules.MaxLength(NameProperty, 50));
            BusinessRules.AddRule(new Rules.CommonRules.MinLength(NameProperty, 2));
            
            BusinessRules.AddRule(new Rules.CommonRules.Required(EmailProperty));
            BusinessRules.AddRule(new Rules.CommonRules.RegEx(EmailProperty, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));

            // Custom rule example
            BusinessRules.AddRule(new EmailUniqueRule(EmailProperty));

            // Dependency rules
            BusinessRules.AddRule(new Rules.CommonRules.Dependency(NameProperty, EmailProperty));
        }

        [ObjectAuthorizationRules]
        public static void AddObjectAuthorizationRules()
        {
            // Example authorization rules
            BusinessRules.AddRule(typeof(Customer), new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.CreateObject, "Admin", "Manager"));
            BusinessRules.AddRule(typeof(Customer), new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.EditObject, "Admin", "Manager", "User"));
            BusinessRules.AddRule(typeof(Customer), new Rules.CommonRules.IsInRole(Rules.AuthorizationActions.DeleteObject, "Admin"));
        }

        [Create]
        private async Task Create([Inject] ICustomerDal customerDal)
        {
            // Call DAL Create method to get default values
            var customerData = await customerDal.Create();

            // Load default values from DAL
            LoadProperty(CreatedDateProperty, customerData.CreatedDate);
            LoadProperty(IsActiveProperty, customerData.IsActive);

            await CheckRulesAsync();
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
        {
            // Get data from DAL
            var customerData = await customerDal.Get(id);

            // Load properties from DAL data
            if (customerData != null)
            {
                LoadProperty(IdProperty, customerData.Id);
                LoadProperty(NameProperty, customerData.Name);
                LoadProperty(EmailProperty, customerData.Email);
                LoadProperty(CreatedDateProperty, customerData.CreatedDate);
                LoadProperty(IsActiveProperty, customerData.IsActive);
            }
            else
            {
                throw new ArgumentException($"Customer {id} not found");
            }

            await CheckRulesAsync();
        }

        private static CustomerData CreateCustomerData(Customer customer)
        {
            return new CustomerData
            {
                Id = customer.ReadProperty(IdProperty),
                Name = customer.ReadProperty(NameProperty),
                Email = customer.ReadProperty(EmailProperty),
                CreatedDate = customer.ReadProperty(CreatedDateProperty),
                IsActive = customer.ReadProperty(IsActiveProperty)
            };
        }

        [Insert]
        private async Task Insert([Inject] ICustomerDal customerDal)
        {
            // Prepare customerData with current property values
            var customerData = CreateCustomerData(this);
            
            // Call DAL Upsert method for insert and get result with new ID
            var result = await customerDal.Upsert(customerData);
            
            // Load the new ID from the result
            LoadProperty(IdProperty, result.Id);
            LoadProperty(CreatedDateProperty, result.CreatedDate);
        }

        [Update]
        private async Task Update([Inject] ICustomerDal customerDal)
        {
            // Prepare customerData with current property values
            var customerData = CreateCustomerData(this);
            
            // Call DAL Upsert method for update
            await customerDal.Upsert(customerData);
        }

        [DeleteSelf]
        private async Task DeleteSelf([Inject] ICustomerDal customerDal)
        {
            // Call DAL Delete method
            await customerDal.Delete(ReadProperty(IdProperty));
            
            // Mark as new
            MarkNew();
        }

        [Delete]
        private async Task Delete(int id, [Inject] ICustomerDal customerDal)
        {
            // Call DAL Delete method
            await customerDal.Delete(id);
        }

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
                    // Simulate checking for unique email
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
