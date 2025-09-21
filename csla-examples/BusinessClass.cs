using System;
using System.ComponentModel.DataAnnotations;
using Csla;

namespace CslaExamples
{
    [Serializable]
    public class Customer : BusinessBase<Customer>
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
        private void Create()
        {
            // Initialize default values for new object
            LoadProperty(CreatedDateProperty, DateTime.Now);
            LoadProperty(IsActiveProperty, true);
            
            BusinessRules.CheckRules();
        }

        [Fetch]
        private void Fetch(int id)
        {
            // Simulate data access - replace with actual data access logic
            // Example: using Entity Framework, ADO.NET, etc.
            
            // For demonstration purposes, creating sample data
            if (id > 0)
            {
                LoadProperty(IdProperty, id);
                LoadProperty(NameProperty, $"Customer {id}");
                LoadProperty(EmailProperty, $"customer{id}@example.com");
                LoadProperty(CreatedDateProperty, DateTime.Now.AddDays(-30));
                LoadProperty(IsActiveProperty, true);
            }
            else
            {
                throw new ArgumentException("Invalid customer ID");
            }
        }

        [Insert]
        private void Insert()
        {
            // Simulate insert operation
            // In real implementation, this would save to database
            
            // Simulate generating new ID
            LoadProperty(IdProperty, new Random().Next(1000, 9999));
            LoadProperty(CreatedDateProperty, DateTime.Now);
            
            // Mark as old (saved) object
            MarkOld();
        }

        [Update]
        private void Update()
        {
            // Simulate update operation
            // In real implementation, this would update the database record
            
            // Mark as old (saved) object
            MarkOld();
        }

        [DeleteSelf]
        private void DeleteSelf()
        {
            // Simulate delete operation
            // In real implementation, this would delete from database
            
            // Mark for deletion
            MarkNew();
        }

        [Delete]
        private void Delete(int id)
        {
            // Static delete operation
            // In real implementation, this would delete from database by ID
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