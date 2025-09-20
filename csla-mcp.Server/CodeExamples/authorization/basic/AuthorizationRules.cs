using Csla;
using Csla.Rules;

namespace MyApp.Library
{
    [Serializable]
    public class SecureCustomer : BusinessBase<SecureCustomer>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => SetProperty(IdProperty, value);
        }

        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        public string Name
        {
            get => GetProperty(NameProperty);
            set => SetProperty(NameProperty, value);
        }

        public static readonly PropertyInfo<decimal> SalaryProperty = RegisterProperty<decimal>(nameof(Salary));
        public decimal Salary
        {
            get => GetProperty(SalaryProperty);
            set => SetProperty(SalaryProperty, value);
        }

        protected override void AddBusinessRules()
        {
            // Only managers can read/write salary information
            BusinessRules.AddRule(new IsInRole(AuthorizationActions.ReadProperty, 
                SalaryProperty, "Manager", "HR"));
            BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, 
                SalaryProperty, "Manager", "HR"));

            // Only HR can create new customers
            BusinessRules.AddRule(new IsInRole(AuthorizationActions.CreateObject, "HR"));

            // Managers and HR can edit existing customers
            BusinessRules.AddRule(new IsInRole(AuthorizationActions.EditObject, "Manager", "HR"));

            // Only HR can delete customers
            BusinessRules.AddRule(new IsInRole(AuthorizationActions.DeleteObject, "HR"));
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] ICustomerDal dal)
        {
            // Check if user can read this object
            if (!Csla.Rules.BusinessRules.HasPermission(AuthorizationActions.GetObject, this))
                throw new Csla.Security.SecurityException("Not authorized to fetch customer");

            var data = await dal.GetAsync(id);
            using (BypassPropertyChecks)
            {
                Id = data.Id;
                Name = data.Name;
                
                // Only load salary if user has permission
                if (CanReadProperty(SalaryProperty))
                    Salary = data.Salary;
            }
        }

        [Insert]
        private async Task Insert([Inject] ICustomerDal dal)
        {
            var data = await dal.InsertAsync(Name, Salary);
            using (BypassPropertyChecks)
            {
                Id = data.Id;
            }
        }

        [Update]
        private async Task Update([Inject] ICustomerDal dal)
        {
            await dal.UpdateAsync(Id, Name, Salary);
        }

        [Delete]
        private async Task Delete([Inject] ICustomerDal dal)
        {
            await dal.DeleteAsync(Id);
        }
    }
}