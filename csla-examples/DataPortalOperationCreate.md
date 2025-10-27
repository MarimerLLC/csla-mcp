# Data Portal Operation: Create

The `[Create]` operation initializes a new business object instance with default values. This is invoked when calling `IDataPortal<T>.CreateAsync()` on the client.

## When to Use

- Creating new editable root objects (`BusinessBase<T>`)
- Setting default values for properties (timestamps, status flags, etc.)
- Optionally retrieving default values from a database or service
- Not used for child objects (they use `CreateChild` instead)

## Basic Create with Direct Initialization

Most common pattern - set default values directly. Use `[RunLocal]` when no DAL services are needed:

```csharp
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class Customer : BusinessBase<Customer>
    {
        public partial int Id { get; private set; }
        public partial string Name { get; set; }
        public partial DateTime CreatedDate { get; private set; }
        public partial bool IsActive { get; set; }
        
        [Create]
        [RunLocal]  // Optimizes data portal - no remote call needed
        private void Create()
        {
            // Set default values using LoadProperty
            LoadProperty(CreatedDateProperty, DateTime.UtcNow);
            LoadProperty(IsActiveProperty, true);
            
            // Check business rules after initialization
            await BusinessRules.CheckRulesAsync();
        }
    }
}
```

**Important**: Only use `[RunLocal]` when the Create method does NOT inject any DAL services or dependencies. If you inject services, remove `[RunLocal]`.

## Create with DAL-Provided Defaults

When default values come from database or service. Do NOT use `[RunLocal]` when injecting services:

```csharp
[Create]  // No [RunLocal] - we're injecting a DAL service
private async Task Create([Inject] ICustomerDal dal)
{
    // Get default values from DAL
    var defaults = await dal.GetDefaultsAsync();
    
    // Load values using LoadProperty
    LoadProperty(CreatedDateProperty, defaults.CreatedDate);
    LoadProperty(CreatedByProperty, defaults.CreatedBy);
    LoadProperty(IsActiveProperty, defaults.IsActive);
    LoadProperty(StatusProperty, defaults.DefaultStatus);
    
    await BusinessRules.CheckRulesAsync();
}
```

**Important**: Because this method injects `ICustomerDal`, it must NOT have the `[RunLocal]` attribute. The data portal needs to execute this on the server where the DAL is available.

## Using BypassPropertyChecks

Alternative syntax using normal property assignment:

```csharp
[Create]
private async Task Create([Inject] ICustomerDal dal)
{
    var defaults = await dal.GetDefaultsAsync();
    
    using (BypassPropertyChecks)
    {
        CreatedDate = defaults.CreatedDate;
        CreatedBy = defaults.CreatedBy;
        IsActive = defaults.IsActive;
        Status = defaults.DefaultStatus;
    }
    
    await BusinessRules.CheckRulesAsync();
}
```

## Create with Parameters

Pass initialization parameters from the client. Can use `[RunLocal]` if no services are injected:

```csharp
// Client code
var customer = await customerPortal.CreateAsync("Initial Name");

// Server-side Create operation
[Create]
[RunLocal]  // OK to use - no injected services
private void Create(string initialName)
{
    using (BypassPropertyChecks)
    {
        Name = initialName;
        CreatedDate = DateTime.UtcNow;
        IsActive = true;
    }
    
    await BusinessRules.CheckRulesAsync();
}
```

## Create with Dependency Injection

Access services through dependency injection. Cannot use `[RunLocal]` with injected services:

```csharp
[Create]  // No [RunLocal] - injecting services
private async Task Create([Inject] ICustomerDal dal, [Inject] IUserContext userContext)
{
    var defaults = await dal.GetDefaultsAsync();
    var currentUser = userContext.GetCurrentUser();
    
    using (BypassPropertyChecks)
    {
        CreatedDate = DateTime.UtcNow;
        CreatedBy = currentUser.UserName;
        IsActive = defaults.IsActive;
        Region = currentUser.DefaultRegion;
    }
    
    await BusinessRules.CheckRulesAsync();
}
```

## Important Notes

1. **Always call `CheckRulesAsync()`** at the end to validate default values
2. **Use `LoadProperty` or `BypassPropertyChecks`** to set values without triggering change tracking
3. **Mark as async** if you need to call any async DAL methods
4. **Don't use for child objects** - they use `[CreateChild]` instead
5. **Property values set here don't mark the object as dirty** - it remains in "new" state
6. **Use `[RunLocal]` for optimization** - but ONLY when NOT injecting any services or DAL dependencies

## RunLocal Attribute Rules

**Use `[RunLocal]`:**

- When setting hard-coded default values
- When using local calculations (DateTime.UtcNow, Guid.NewGuid(), etc.)
- When no `[Inject]` parameters are present
- Optimizes by avoiding remote data portal call

**Do NOT use `[RunLocal]`:**

- When injecting any DAL services (`[Inject] ICustomerDal`)
- When injecting any other services (`[Inject] IUserContext`)
- When accessing resources that require server-side execution
- Data portal must execute on server to resolve dependencies

## Common Patterns

### Create with Current User (No RunLocal)

```csharp
[Create]  // No [RunLocal] - injecting IApplicationContext
private void Create([Inject] IApplicationContext applicationContext)
{
    LoadProperty(CreatedDateProperty, DateTime.UtcNow);
    LoadProperty(CreatedByProperty, applicationContext.User.Identity.Name);
    LoadProperty(IsActiveProperty, true);
    
    await BusinessRules.CheckRulesAsync();
}
```

### Create with Only Hard-Coded Values (Use RunLocal)

```csharp
[Create]
[RunLocal]  // Optimized - no injected services
private void Create()
{
    LoadProperty(CreatedDateProperty, DateTime.UtcNow);
    LoadProperty(StatusProperty, CustomerStatus.Prospect);
    LoadProperty(IsActiveProperty, true);
    LoadProperty(IdProperty, Guid.NewGuid());
    
    await BusinessRules.CheckRulesAsync();
}
```

### Create with Child Collections (No RunLocal)

```csharp
[Create]  // No [RunLocal] - injecting child data portal
private async Task Create([Inject] IChildDataPortal<OrderItemList> itemPortal)
{
    LoadProperty(OrderDateProperty, DateTime.UtcNow);
    LoadProperty(StatusProperty, OrderStatus.Draft);
    
    // Initialize empty child collection
    LoadProperty(ItemsProperty, await itemPortal.CreateChildAsync());
    
    await BusinessRules.CheckRulesAsync();
}
```

**Note**: Even though `IChildDataPortal<T>` is injected, this must run server-side, so no `[RunLocal]`.
