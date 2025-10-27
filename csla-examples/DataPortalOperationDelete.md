# Data Portal Operations: Delete and DeleteSelf

CSLA provides two delete operations: `[Delete]` and `[DeleteSelf]`. Both remove business objects from the data source, but they're used in different scenarios.

## Delete vs DeleteSelf

- **`[Delete]`**: Static method that deletes by ID without loading the object first
- **`[DeleteSelf]`**: Instance method that deletes the current object (called during `SaveAsync()`)

## Delete Operation

Deletes an object by criteria without loading it first. More efficient when you just have an ID.

### Basic Delete by ID

```csharp
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class Customer : BusinessBase<Customer>
    {
        public partial int Id { get; private set; }
        public partial string Name { get; set; }
        
        [Delete]
        private async Task Delete(int id, [Inject] ICustomerDal dal)
        {
            // Delete without loading the object
            await dal.DeleteAsync(id);
        }
    }
}

// Client usage
await customerPortal.DeleteAsync(customerId: 123);
```

### Delete with Authorization Check

```csharp
[Delete]
private async Task Delete(int id, [Inject] ICustomerDal dal, [Inject] IApplicationContext appContext)
{
    // Verify user has permission
    if (!appContext.User.IsInRole("Manager"))
        throw new SecurityException("Not authorized to delete customers");
    
    await dal.DeleteAsync(id);
}
```

### Delete with Multiple Criteria

```csharp
[Delete]
private async Task Delete(int customerId, string tenantId, [Inject] ICustomerDal dal)
{
    // Delete with composite key
    await dal.DeleteAsync(customerId, tenantId);
}

// Client usage
await customerPortal.DeleteAsync(customerId: 123, tenantId: "ACME");
```

### Delete with Criteria Object

```csharp
public class DeleteCriteria
{
    public int Id { get; set; }
    public byte[] RowVersion { get; set; }  // For optimistic concurrency
}

[Delete]
private async Task Delete(DeleteCriteria criteria, [Inject] ICustomerDal dal)
{
    try
    {
        await dal.DeleteAsync(criteria.Id, criteria.RowVersion);
    }
    catch (ConcurrencyException ex)
    {
        throw new DataPortalException("Record was modified by another user", ex);
    }
}

// Client usage
var criteria = new DeleteCriteria { Id = 123, RowVersion = currentRowVersion };
await customerPortal.DeleteAsync(criteria);
```

## DeleteSelf Operation

Deletes the current object instance. Called automatically when you mark an object for deletion and save it.

### Basic DeleteSelf

```csharp
[DeleteSelf]
private async Task DeleteSelf([Inject] ICustomerDal dal)
{
    // Delete using current object's ID
    await dal.DeleteAsync(ReadProperty(IdProperty));
    
    // Mark object as new after deletion
    MarkNew();
}

// Client usage
var customer = await customerPortal.FetchAsync(123);
customer.Delete();  // Marks for deletion
await customer.SaveAsync();  // Triggers DeleteSelf
```

### DeleteSelf with Optimistic Concurrency

```csharp
[DeleteSelf]
private async Task DeleteSelf([Inject] ICustomerDal dal)
{
    try
    {
        await dal.DeleteAsync(
            ReadProperty(IdProperty),
            ReadProperty(RowVersionProperty));
        
        MarkNew();
    }
    catch (ConcurrencyException ex)
    {
        throw new DataPortalException("Record was modified by another user", ex, this);
    }
}
```

### DeleteSelf with Child Objects

Children are typically deleted by database cascade or explicitly:

```csharp
[DeleteSelf]
private async Task DeleteSelf([Inject] IOrderDal orderDal, [Inject] IOrderItemDal itemDal)
{
    var orderId = ReadProperty(IdProperty);
    
    // Option 1: Delete children explicitly (if no cascade)
    await itemDal.DeleteByOrderIdAsync(orderId);
    
    // Delete parent
    await orderDal.DeleteAsync(orderId);
    
    MarkNew();
}

// Option 2: Rely on database cascade delete
[DeleteSelf]
private async Task DeleteSelf([Inject] IOrderDal dal)
{
    // Database will cascade delete items automatically
    await dal.DeleteAsync(ReadProperty(IdProperty));
    MarkNew();
}
```

### DeleteSelf with Using BypassPropertyChecks

```csharp
[DeleteSelf]
private async Task DeleteSelf([Inject] ICustomerDal dal)
{
    int id;
    byte[] rowVersion;
    
    using (BypassPropertyChecks)
    {
        id = Id;
        rowVersion = RowVersion;
    }
    
    await dal.DeleteAsync(id, rowVersion);
    MarkNew();
}
```

## Important Notes

1. **Always call `MarkNew()` after `DeleteSelf`** - indicates object no longer exists in database
2. **Use `[Delete]` for efficiency** - when you only have an ID and don't need to load the object
3. **Use `[DeleteSelf]` when object is already loaded** - especially when you need current property values
4. **Handle concurrency** - include row version if using optimistic locking
5. **Consider cascade deletes** - database may auto-delete children
6. **Check authorization** - verify user has permission to delete

## Common Patterns

### Soft Delete with DeleteSelf

Mark as deleted instead of physically removing:

```csharp
[DeleteSelf]
private async Task DeleteSelf([Inject] ICustomerDal dal, [Inject] IApplicationContext appContext)
{
    // Soft delete - just mark as inactive
    var data = new CustomerData
    {
        Id = ReadProperty(IdProperty),
        IsActive = false,
        DeletedBy = appContext.User.Identity.Name,
        DeletedDate = DateTime.UtcNow
    };
    
    await dal.UpdateAsync(data);  // Update, not delete
    
    // Object still exists in DB, but marked deleted
    using (BypassPropertyChecks)
    {
        IsActive = false;
    }
}
```

### Delete with Audit Trail

```csharp
[Delete]
private async Task Delete(int id, [Inject] ICustomerDal dal, [Inject] IAuditDal auditDal, [Inject] IApplicationContext appContext)
{
    // Log deletion before deleting
    await auditDal.LogDeletionAsync(
        tableName: "Customer",
        recordId: id,
        deletedBy: appContext.User.Identity.Name);
    
    await dal.DeleteAsync(id);
}
```

### Conditional Delete

```csharp
[DeleteSelf]
private async Task DeleteSelf([Inject] ICustomerDal dal, [Inject] IOrderDal orderDal)
{
    var customerId = ReadProperty(IdProperty);
    
    // Check if customer has orders
    var hasOrders = await orderDal.CustomerHasOrdersAsync(customerId);
    
    if (hasOrders)
        throw new BusinessException("Cannot delete customer with existing orders");
    
    await dal.DeleteAsync(customerId);
    MarkNew();
}
```

## When to Use Each

**Use `[Delete]`:**
- When you only have an ID
- For bulk delete operations
- When object doesn't need to be loaded first
- More efficient - saves a round trip to load object

**Use `[DeleteSelf]`:**
- When object is already loaded
- When you need current property values (for concurrency, audit, etc.)
- When using the `Delete()` + `SaveAsync()` pattern
- When working with the object graph (parent/children)
