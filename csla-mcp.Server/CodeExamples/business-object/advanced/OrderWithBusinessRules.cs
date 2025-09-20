using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;

namespace MyApp.Library
{
    [Serializable]
    public class Order : BusinessBase<Order>
    {
        #region Properties

        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => SetProperty(IdProperty, value);
        }

        public static readonly PropertyInfo<string> OrderNumberProperty = RegisterProperty<string>(nameof(OrderNumber));
        public string OrderNumber
        {
            get => GetProperty(OrderNumberProperty);
            set => SetProperty(OrderNumberProperty, value);
        }

        public static readonly PropertyInfo<DateTime> OrderDateProperty = RegisterProperty<DateTime>(nameof(OrderDate));
        public DateTime OrderDate
        {
            get => GetProperty(OrderDateProperty);
            set => SetProperty(OrderDateProperty, value);
        }

        public static readonly PropertyInfo<int> CustomerIdProperty = RegisterProperty<int>(nameof(CustomerId));
        public int CustomerId
        {
            get => GetProperty(CustomerIdProperty);
            set => SetProperty(CustomerIdProperty, value);
        }

        public static readonly PropertyInfo<string> CustomerNameProperty = RegisterProperty<string>(nameof(CustomerName));
        public string CustomerName
        {
            get => GetProperty(CustomerNameProperty);
            private set => SetProperty(CustomerNameProperty, value);
        }

        public static readonly PropertyInfo<OrderStatus> StatusProperty = RegisterProperty<OrderStatus>(nameof(Status));
        public OrderStatus Status
        {
            get => GetProperty(StatusProperty);
            set => SetProperty(StatusProperty, value);
        }

        public static readonly PropertyInfo<OrderItemList> ItemsProperty = RegisterProperty<OrderItemList>(nameof(Items));
        public OrderItemList Items
        {
            get => GetProperty(ItemsProperty);
            private set => SetProperty(ItemsProperty, value);
        }

        // Calculated property
        public static readonly PropertyInfo<decimal> TotalAmountProperty = RegisterProperty<decimal>(nameof(TotalAmount));
        public decimal TotalAmount
        {
            get => GetProperty(TotalAmountProperty);
            private set => SetProperty(TotalAmountProperty, value);
        }

        #endregion

        #region Business Rules

        protected override void AddBusinessRules()
        {
            // Property rules
            BusinessRules.AddRule(new Required(OrderNumberProperty));
            BusinessRules.AddRule(new MaxLength(OrderNumberProperty, 50));
            BusinessRules.AddRule(new Required(OrderDateProperty));
            BusinessRules.AddRule(new Required(CustomerIdProperty));

            // Custom business rules
            BusinessRules.AddRule(new OrderDateValidation(OrderDateProperty));
            BusinessRules.AddRule(new MinimumOrderAmount(ItemsProperty, TotalAmountProperty));

            // Calculated field rules
            BusinessRules.AddRule(new CalculateTotalAmount(TotalAmountProperty, ItemsProperty));

            // Authorization rules
            BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, 
                StatusProperty, "Manager", "OrderProcessor"));
        }

        #endregion

        #region Factory Methods

        public static async Task<Order> CreateAsync()
        {
            return await DataPortal.CreateAsync<Order>();
        }

        public static async Task<Order> GetAsync(int id)
        {
            return await DataPortal.FetchAsync<Order>(id);
        }

        public static async Task DeleteAsync(int id)
        {
            await DataPortal.DeleteAsync<Order>(id);
        }

        #endregion

        #region Data Portal Methods

        [Create]
        private void Create()
        {
            OrderDate = DateTime.Today;
            Status = OrderStatus.Draft;
            Items = DataPortal.CreateChild<OrderItemList>();
            Items.ListChanged += Items_ListChanged;
            
            BusinessRules.CheckRules();
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] IOrderDal dal)
        {
            var data = await dal.GetAsync(id);
            using (BypassPropertyChecks)
            {
                Id = data.Id;
                OrderNumber = data.OrderNumber;
                OrderDate = data.OrderDate;
                CustomerId = data.CustomerId;
                CustomerName = data.CustomerName;
                Status = data.Status;
                Items = await DataPortal.FetchChildAsync<OrderItemList>(id);
            }
            
            Items.ListChanged += Items_ListChanged;
        }

        [Insert]
        private async Task Insert([Inject] IOrderDal dal)
        {
            var data = await dal.InsertAsync(OrderNumber, OrderDate, CustomerId, Status);
            using (BypassPropertyChecks)
            {
                Id = data.Id;
                CustomerName = data.CustomerName;
            }
            
            await FieldManager.UpdateChildrenAsync();
        }

        [Update]
        private async Task Update([Inject] IOrderDal dal)
        {
            await dal.UpdateAsync(Id, OrderNumber, OrderDate, CustomerId, Status);
            await FieldManager.UpdateChildrenAsync();
        }

        [Delete]
        private async Task Delete([Inject] IOrderDal dal)
        {
            await dal.DeleteAsync(Id);
        }

        #endregion

        #region Child Events

        private void Items_ListChanged(object? sender, ListChangedEventArgs e)
        {
            // Recalculate total when items change
            BusinessRules.CheckRules(TotalAmountProperty);
        }

        #endregion
    }

    #region Custom Business Rules

    public class OrderDateValidation : BusinessRule
    {
        public OrderDateValidation(IPropertyInfo primaryProperty) : base(primaryProperty)
        {
            InputProperties = new List<IPropertyInfo> { primaryProperty };
        }

        protected override void Execute(IRuleContext context)
        {
            var orderDate = (DateTime)context.InputPropertyValues[PrimaryProperty];
            
            if (orderDate > DateTime.Today)
            {
                context.AddErrorResult("Order date cannot be in the future.");
            }
            
            if (orderDate < DateTime.Today.AddYears(-1))
            {
                context.AddWarningResult("Order date is more than one year old.");
            }
        }
    }

    public class MinimumOrderAmount : BusinessRule
    {
        private const decimal MinAmount = 10.00m;

        public MinimumOrderAmount(IPropertyInfo itemsProperty, IPropertyInfo totalProperty) 
            : base(totalProperty)
        {
            InputProperties = new List<IPropertyInfo> { itemsProperty };
            AffectedProperties.Add(totalProperty);
        }

        protected override void Execute(IRuleContext context)
        {
            var items = (OrderItemList)context.InputPropertyValues[InputProperties[0]];
            var total = items.Sum(item => item.Quantity * item.UnitPrice);
            
            if (total > 0 && total < MinAmount)
            {
                context.AddErrorResult($"Order total must be at least {MinAmount:C}.");
            }
        }
    }

    public class CalculateTotalAmount : BusinessRule
    {
        public CalculateTotalAmount(IPropertyInfo totalProperty, IPropertyInfo itemsProperty) 
            : base(totalProperty)
        {
            InputProperties = new List<IPropertyInfo> { itemsProperty };
            AffectedProperties.Add(totalProperty);
        }

        protected override void Execute(IRuleContext context)
        {
            var items = (OrderItemList)context.InputPropertyValues[InputProperties[0]];
            var total = items.Sum(item => item.Quantity * item.UnitPrice);
            context.AddOutValue(PrimaryProperty, total);
        }
    }

    #endregion

    public enum OrderStatus
    {
        Draft,
        Submitted,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }
}