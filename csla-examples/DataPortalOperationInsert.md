# Data Portal Operation: Insert

The `[Insert]` operation persists a new business object to a data source. This is invoked automatically when calling `SaveAsync()` on a new object.

## When to Use

- Saving new editable root objects (`BusinessBase<T>`) to the database
- Called automatically by data portal when object `IsNew` is true
- Inserts data and retrieves server-generated values (IDs, timestamps)
- Not used for child objects (they use `InsertChild` instead)

## Basic Insert Pattern

Standard pattern for inserting a new object:

```csharp
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class Customer : BusinessBase<Customer>
    {
        public partial int Id { get; private set; }
        public partial string Name { get; set; }
        public partial string Email { get; set; }
        public partial DateTime CreatedDate { get; private set; }
        public partial bool IsActive { get; set; }
        
        [Insert]
        private async Task Insert([Inject] ICustomerDal dal)
        {
            // Create DTO with current values
            var data = new CustomerData
            {
                Name = ReadProperty(NameProperty),
                Email = ReadProperty(EmailProperty),
                CreatedDate = ReadProperty(CreatedDateProperty),
                IsActive = ReadProperty(IsActiveProperty)
            };
            
            // Insert and get back generated values
            var result = await dal.InsertAsync(data);
            
            // Load server-generated ID
            LoadProperty(IdProperty, result.Id);
        }
    }
}
```

## Using BypassPropertyChecks

Read values using normal property syntax:

```csharp
[Insert]
private async Task Insert([Inject] ICustomerDal dal)
{
    CustomerData data;
    
    using (BypassPropertyChecks)
    {
        data = new CustomerData
        {
            Name = Name,
            Email = Email,
            CreatedDate = CreatedDate,
            IsActive = IsActive
        };
    }
    
    var result = await dal.InsertAsync(data);
    LoadProperty(IdProperty, result.Id);
}
```

## Insert with Audit Fields

Capture created by/date information:

```csharp
[Insert]
private async Task Insert([Inject] ICustomerDal dal, [Inject] IApplicationContext appContext)
{
    var currentUser = appContext.User.Identity.Name;
    var now = DateTime.UtcNow;
    
    // Update audit fields before insert
    using (BypassPropertyChecks)
    {
        CreatedDate = now;
        CreatedBy = currentUser;
    }
    
    var data = new CustomerData
    {
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty),
        CreatedDate = now,
        CreatedBy = currentUser,
        IsActive = ReadProperty(IsActiveProperty)
    };
    
    var result = await dal.InsertAsync(data);
    LoadProperty(IdProperty, result.Id);
}
```

## Insert with Child Objects

Insert parent and children together:

```csharp
[Insert]
private async Task Insert([Inject] IOrderDal dal)
{
    var data = new OrderData
    {
        OrderDate = ReadProperty(OrderDateProperty),
        CustomerId = ReadProperty(CustomerIdProperty),
        Status = ReadProperty(StatusProperty)
    };
    
    // Insert order and get generated ID
    var result = await dal.InsertAsync(data);
    LoadProperty(IdProperty, result.Id);
    
    // Now insert children (they need parent ID)
    var items = ReadProperty(ItemsProperty);
    await FieldManager.UpdateChildrenAsync(items);
}
```

## Important Notes

1. **Use `ReadProperty` or `BypassPropertyChecks`** to get values without triggering rules
2. **Use `LoadProperty`** to set server-generated values (ID, timestamps)
3. **Always use async/await** for database operations
4. **Insert children after parent** so they have the parent ID
5. **Use `FieldManager.UpdateChildrenAsync()`** to save child collections
6. **Don't call `CheckRulesAsync()`** - validation already happened before save
