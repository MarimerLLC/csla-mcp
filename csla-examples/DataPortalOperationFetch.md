# Data Portal Operation: Fetch

The `[Fetch]` operation retrieves an existing business object from a data source. This is invoked when calling `IDataPortal<T>.FetchAsync()` on the client.

## When to Use

- Loading existing editable root objects (`BusinessBase<T>`)
- Loading existing read-only root objects (`ReadOnlyBase<T>`)
- Retrieving data by ID or other criteria
- Not used for child objects (they use `FetchChild` instead)

## Basic Fetch Pattern

Standard pattern for loading an object by ID:

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
        
        [Fetch]
        private async Task Fetch(int id, [Inject] ICustomerDal dal)
        {
            // Retrieve data from DAL
            var data = await dal.GetAsync(id);
            
            // Load values using LoadProperty
            LoadProperty(IdProperty, data.Id);
            LoadProperty(NameProperty, data.Name);
            LoadProperty(EmailProperty, data.Email);
            LoadProperty(CreatedDateProperty, data.CreatedDate);
            LoadProperty(IsActiveProperty, data.IsActive);
            
            // Validate loaded data
            await BusinessRules.CheckRulesAsync();
        }
    }
}
```

## Using BypassPropertyChecks

More natural syntax when loading many properties:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal dal)
{
    var data = await dal.GetAsync(id);
    
    using (BypassPropertyChecks)
    {
        Id = data.Id;
        Name = data.Name;
        Email = data.Email;
        CreatedDate = data.CreatedDate;
        IsActive = data.IsActive;
    }
    
    await BusinessRules.CheckRulesAsync();
}
```

## Using DataMapper

Efficient mapping for objects with many properties:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal dal)
{
    var data = await dal.GetAsync(id);
    
    // Automatically map matching properties from data to this object
    DataMapper.Map(data, this);
    
    await BusinessRules.CheckRulesAsync();
}
```

**Note**: `DataMapper` requires property names to match between source and target objects.

## Fetch with Multiple Parameters

Query by multiple criteria:

```csharp
// Client code
var customer = await customerPortal.FetchAsync(customerId: 123, includeInactive: true);

// Server-side Fetch operation
[Fetch]
private async Task Fetch(int customerId, bool includeInactive, [Inject] ICustomerDal dal)
{
    var data = await dal.GetAsync(customerId, includeInactive);
    
    using (BypassPropertyChecks)
    {
        Id = data.Id;
        Name = data.Name;
        Email = data.Email;
        IsActive = data.IsActive;
    }
    
    await BusinessRules.CheckRulesAsync();
}
```

## Fetch with Criteria Object

For complex query parameters:

```csharp
public class CustomerCriteria
{
    public int? Id { get; set; }
    public string Email { get; set; }
    public DateTime? RegisteredAfter { get; set; }
}

// Client code
var criteria = new CustomerCriteria { Email = "user@example.com" };
var customer = await customerPortal.FetchAsync(criteria);

// Server-side Fetch operation
[Fetch]
private async Task Fetch(CustomerCriteria criteria, [Inject] ICustomerDal dal)
{
    var data = await dal.FindAsync(criteria);
    
    if (data == null)
        throw new DataNotFoundException("Customer not found");
    
    DataMapper.Map(data, this);
    await BusinessRules.CheckRulesAsync();
}
```

## Fetch with Child Objects

Load parent with children (avoiding N+1 queries):

```csharp
[Fetch]
private async Task Fetch(int orderId, [Inject] IOrderDal orderDal, [Inject] IChildDataPortal<OrderItemList> itemPortal)
{
    // Fetch order data and all related items in ONE query
    var orderData = await orderDal.GetOrderWithItemsAsync(orderId);
    
    using (BypassPropertyChecks)
    {
        Id = orderData.Id;
        OrderDate = orderData.OrderDate;
        CustomerId = orderData.CustomerId;
        Status = orderData.Status;
        
        // Fetch child list, passing the item rows
        Items = await itemPortal.FetchChildAsync(orderData.Items);
    }
    
    await BusinessRules.CheckRulesAsync();
}
```

## Read-Only Object Fetch

Pattern for read-only objects:

```csharp
[CslaImplementProperties]
public partial class CustomerInfo : ReadOnlyBase<CustomerInfo>
{
    public partial int Id { get; private set; }
    public partial string Name { get; private set; }
    public partial string Email { get; private set; }
    
    [Fetch]
    private async Task Fetch(int id, [Inject] ICustomerDal dal)
    {
        var data = await dal.GetAsync(id);
        
        using (BypassPropertyChecks)
        {
            Id = data.Id;
            Name = data.Name;
            Email = data.Email;
        }
        
        // Read-only objects typically don't need CheckRulesAsync
    }
}
```

## Important Notes

1. **Always use async/await** for database operations
2. **Use `LoadProperty` or `BypassPropertyChecks`** to avoid triggering change tracking
3. **Call `CheckRulesAsync()` at the end** to validate loaded data (editable objects)
4. **Read-only objects** typically don't need `CheckRulesAsync()`
5. **Handle not found scenarios** - throw exception or return null depending on requirements
6. **Load child objects in same query** when possible to avoid N+1 queries

## Common Patterns

### Fetch with Authorization Check

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal dal, [Inject] IApplicationContext appContext)
{
    var data = await dal.GetAsync(id);
    
    // Verify user has permission to access this customer
    if (data.OwnerId != appContext.User.Identity.Name && !appContext.User.IsInRole("Manager"))
        throw new SecurityException("Not authorized to access this customer");
    
    DataMapper.Map(data, this);
    await BusinessRules.CheckRulesAsync();
}
```

### Fetch with Optimistic Concurrency

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial int Id { get; private set; }
    public partial string Name { get; set; }
    public partial byte[] RowVersion { get; private set; }
    
    [Fetch]
    private async Task Fetch(int id, [Inject] ICustomerDal dal)
    {
        var data = await dal.GetAsync(id);
        
        using (BypassPropertyChecks)
        {
            Id = data.Id;
            Name = data.Name;
            RowVersion = data.RowVersion; // For concurrency checking
        }
        
        await BusinessRules.CheckRulesAsync();
    }
}
```
