# Data Portal Operation: Update

The `[Update]` operation persists changes to an existing business object. This is invoked automatically when calling `SaveAsync()` on a modified object.

## When to Use

- Saving modified editable root objects (`BusinessBase<T>`) to the database
- Called automatically by data portal when object `IsNew` is false and `IsDirty` is true
- Updates data and optionally retrieves server-updated values (timestamps, row versions)
- Not used for child objects (they use `UpdateChild` instead)

## Basic Update Pattern

Standard pattern for updating an existing object:

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
        public partial DateTime? ModifiedDate { get; private set; }
        
        [Update]
        private async Task Update([Inject] ICustomerDal dal)
        {
            // Create DTO with current values
            var data = new CustomerData
            {
                Id = ReadProperty(IdProperty),
                Name = ReadProperty(NameProperty),
                Email = ReadProperty(EmailProperty)
            };
            
            // Update in database
            await dal.UpdateAsync(data);
        }
    }
}
```

## Using BypassPropertyChecks

Read values using normal property syntax:

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal)
{
    CustomerData data;
    
    using (BypassPropertyChecks)
    {
        data = new CustomerData
        {
            Id = Id,
            Name = Name,
            Email = Email
        };
    }
    
    await dal.UpdateAsync(data);
}
```

## Update with Audit Fields

Track who modified and when:

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal, [Inject] IApplicationContext appContext)
{
    var currentUser = appContext.User.Identity.Name;
    var now = DateTime.UtcNow;
    
    // Update audit fields before save
    using (BypassPropertyChecks)
    {
        ModifiedDate = now;
        ModifiedBy = currentUser;
    }
    
    var data = new CustomerData
    {
        Id = ReadProperty(IdProperty),
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty),
        ModifiedDate = now,
        ModifiedBy = currentUser
    };
    
    await dal.UpdateAsync(data);
}
```

## Update with Optimistic Concurrency

Prevent lost updates:

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal)
{
    var data = new CustomerData
    {
        Id = ReadProperty(IdProperty),
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty),
        RowVersion = ReadProperty(RowVersionProperty)
    };
    
    try
    {
        var result = await dal.UpdateAsync(data);
        
        // Load new row version for next update
        LoadProperty(RowVersionProperty, result.RowVersion);
    }
    catch (ConcurrencyException ex)
    {
        throw new DataPortalException("Record was modified by another user", ex, this);
    }
}
```

## Update with Child Objects

Update parent and children together:

```csharp
[Update]
private async Task Update([Inject] IOrderDal dal)
{
    var data = new OrderData
    {
        Id = ReadProperty(IdProperty),
        OrderDate = ReadProperty(OrderDateProperty),
        Status = ReadProperty(StatusProperty)
    };
    
    // Update parent order
    await dal.UpdateAsync(data);
    
    // Update children (insert new, update modified, delete removed)
    var items = ReadProperty(ItemsProperty);
    await FieldManager.UpdateChildrenAsync(items);
}
```

## Update with Server-Generated Values

Retrieve updated timestamps or computed values:

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal)
{
    var data = new CustomerData
    {
        Id = ReadProperty(IdProperty),
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty)
    };
    
    var result = await dal.UpdateAsync(data);
    
    using (BypassPropertyChecks)
    {
        ModifiedDate = result.ModifiedDate;  // Server timestamp
        RowVersion = result.RowVersion;      // New concurrency token
    }
}
```

## Important Notes

1. **Always include the ID** in the update data
2. **Use `ReadProperty` or `BypassPropertyChecks`** to get values
3. **Include row version** for optimistic concurrency
4. **Update audit fields** before saving
5. **Use `FieldManager.UpdateChildrenAsync()`** to save child collections
6. **Don't call `CheckRulesAsync()`** - validation already happened before save

## Common Patterns

### Conditional Update

Only update if specific conditions are met:

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal, [Inject] IApplicationContext appContext)
{
    // Verify user has permission to update
    if (!appContext.User.IsInRole("Manager"))
        throw new SecurityException("Not authorized to update customer");
    
    var data = new CustomerData
    {
        Id = ReadProperty(IdProperty),
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty)
    };
    
    await dal.UpdateAsync(data);
}
```

### Update Only Changed Properties

For performance, only send modified properties:

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal)
{
    var changedProperties = new Dictionary<string, object>();
    changedProperties["Id"] = ReadProperty(IdProperty); // Always include ID
    
    if (IsSelfDirty) // Only if this object (not children) changed
    {
        foreach (var property in FieldManager.GetRegisteredProperties())
        {
            if (FieldManager.IsFieldDirty(property))
            {
                changedProperties[property.Name] = ReadProperty(property);
            }
        }
    }
    
    await dal.PartialUpdateAsync(changedProperties);
}
