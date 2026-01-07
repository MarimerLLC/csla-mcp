# Business Rules: Authorization

Authorization rules control what users can do with business objects and their properties. These rules determine whether a user has permission to read or write properties, execute methods, or perform data portal operations (create, fetch, update, delete).

## Overview

Authorization rules differ from validation and calculation rules:

- **Validation rules** check if data is valid
- **Calculation rules** compute property values
- **Authorization rules** check if the current user has permission to perform an action

Authorization rules inherit from `AuthorizationRule` instead of `BusinessRule`.

## Important: One Authorization Rule Per Action

**CRITICAL:** You can only have **ONE** authorization rule per property/action combination:

- Only one rule for `WriteProperty` on `SalaryProperty`
- Only one rule for `ReadProperty` on `SalaryProperty`
- Only one rule for `CreateObject` on the type
- Only one rule for `EditObject` on the type

If you add multiple authorization rules for the same action, only the last one added will be used.

**Correct:**
```csharp
// One rule per action - CORRECT
BusinessRules.AddRule(
    new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin", "HR"));
BusinessRules.AddRule(
    new IsInRole(AuthorizationActions.ReadProperty, SalaryProperty, "Admin", "HR", "Manager"));
```

**Incorrect:**
```csharp
// Multiple rules for same action - WRONG! Only the last rule will be used
BusinessRules.AddRule(
    new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin"));
BusinessRules.AddRule(
    new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "HR")); // This replaces the previous rule!
```

If you need complex authorization logic, create a single custom rule that handles all the logic.

## Authorization Actions

Authorization rules enforce one of these actions:

```csharp
public enum AuthorizationActions
{
    WriteProperty,   // Can the user set a property value?
    ReadProperty,    // Can the user read a property value?
    ExecuteMethod,   // Can the user execute a method?
    CreateObject,    // Can the user create a new object?
    GetObject,       // Can the user fetch an existing object?
    EditObject,      // Can the user update/save an object?
    DeleteObject     // Can the user delete an object?
}
```

## Basic Authorization Rule Structure

```csharp
using Csla.Rules;

public class MyAuthorizationRule : AuthorizationRule
{
    public MyAuthorizationRule(AuthorizationActions action, IMemberInfo element)
        : base(action, element)
    {
    }

    protected override void Execute(IAuthorizationContext context)
    {
        // Check if the user has permission
        if (/* user has permission */)
        {
            context.HasPermission = true;
        }
        else
        {
            context.HasPermission = false; // Default is false
        }
    }
}
```

## Property Authorization

### IsInRole Rule (Built-in)

The most common authorization rule checks if the user is in a specific role:

```csharp
using Csla.Rules.CommonRules;

protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Only Admin users can write the Salary property
    BusinessRules.AddRule(
        new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin"));

    // Only HR and Manager users can read the Salary property
    BusinessRules.AddRule(
        new IsInRole(AuthorizationActions.ReadProperty, SalaryProperty, "HR", "Manager"));
}
```

### IsNotInRole Rule (Built-in)

Deny access to users in specific roles:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Everyone EXCEPT Guest users can write the Name property
    BusinessRules.AddRule(
        new IsNotInRole(AuthorizationActions.WriteProperty, NameProperty, "Guest"));
}
```

## Claims-Based Authorization (Most Common Custom Pattern)

**Claims-based authorization is the most common pattern for custom authorization rules.** Modern authentication systems use Claims to represent user attributes like ID, department, permissions, etc.

**IMPORTANT:** When working with Claims, use `ApplicationContext.Principal` (returns `ClaimsPrincipal`) instead of `ApplicationContext.User` (returns `IPrincipal`). The `Principal` property provides access to the `Claims` collection.

```csharp
// CORRECT - Use Principal for Claims
var principal = context.ApplicationContext.Principal;
var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "UserId");

// For role checks, you can use either User or Principal
var user = context.ApplicationContext.User;
bool isAdmin = user.IsInRole("Admin");
```

### Simple Claims Authorization

Check if a user has a specific claim:

```csharp
public class HasClaim : AuthorizationRule
{
    private readonly string _claimType;
    private readonly string _claimValue;

    public HasClaim(
        AuthorizationActions action,
        IMemberInfo element,
        string claimType,
        string claimValue = null)
        : base(action, element)
    {
        _claimType = claimType;
        _claimValue = claimValue;
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var principal = context.ApplicationContext.Principal;

        if (_claimValue == null)
        {
            // Just check if the claim exists
            context.HasPermission = principal.Claims.Any(c => c.Type == _claimType);
        }
        else
        {
            // Check for specific claim value
            context.HasPermission = principal.Claims.Any(c => c.Type == _claimType && c.Value == _claimValue);
        }
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Only users with "CanApprovePurchases" claim can write ApprovalDate
    BusinessRules.AddRule(
        new HasClaim(AuthorizationActions.WriteProperty, ApprovalDateProperty, "CanApprovePurchases"));

    // Only users in Finance department can read Budget
    BusinessRules.AddRule(
        new HasClaim(AuthorizationActions.ReadProperty, BudgetProperty, "Department", "Finance"));
}
```

### Claims Combined with Property Values

The most common custom pattern combines claims with object state:

```csharp
public class CanEditIfOwnerOrManager : AuthorizationRule
{
    private readonly IPropertyInfo _ownerIdProperty;

    public CanEditIfOwnerOrManager(
        AuthorizationActions action,
        IMemberInfo element,
        IPropertyInfo ownerIdProperty)
        : base(action, element)
    {
        _ownerIdProperty = ownerIdProperty;
        CacheResult = false; // Re-evaluate because it depends on property values
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var principal = context.ApplicationContext.Principal;

        // Check if user is a manager (via claim)
        var isManager = principal.Claims.Any(c => c.Type == "Role" && c.Value == "Manager");
        if (isManager)
        {
            context.HasPermission = true;
            return;
        }

        // Check if user is the owner (via claim + property value)
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            int ownerId = (int)ReadProperty(context.Target, _ownerIdProperty);
            context.HasPermission = (userId == ownerId);
        }
        else
        {
            context.HasPermission = false;
        }
    }
}
```

**Usage:**

```csharp
[CslaImplementProperties]
public partial class Document : BusinessBase<Document>
{
    public partial int DocumentId { get; private set; }
    public partial int OwnerId { get; private set; }
    public partial string Title { get; set; }
    public partial string Content { get; set; }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // Users can edit if they're the owner OR a manager
        BusinessRules.AddRule(
            new CanEditIfOwnerOrManager(
                AuthorizationActions.WriteProperty,
                ContentProperty,
                OwnerIdProperty));
    }
}
```

### Department-Based Authorization with Claims

Control access based on user's department:

```csharp
public class RequiresDepartment : AuthorizationRule
{
    private readonly string[] _allowedDepartments;

    public RequiresDepartment(
        AuthorizationActions action,
        IMemberInfo element,
        params string[] allowedDepartments)
        : base(action, element)
    {
        _allowedDepartments = allowedDepartments;
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var principal = context.ApplicationContext.Principal;

        // Get user's department from claims
        var departmentClaim = principal.Claims.FirstOrDefault(c => c.Type == "Department");

        if (departmentClaim == null)
        {
            context.HasPermission = false;
            return;
        }

        // Check if user's department is in the allowed list
        context.HasPermission = _allowedDepartments.Contains(departmentClaim.Value);
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Only Finance and Accounting departments can write Budget
    BusinessRules.AddRule(
        new RequiresDepartment(
            AuthorizationActions.WriteProperty,
            BudgetProperty,
            "Finance", "Accounting"));

    // Only HR department can write Salary
    BusinessRules.AddRule(
        new RequiresDepartment(
            AuthorizationActions.WriteProperty,
            SalaryProperty,
            "HR"));
}
```

### Permission Level with Claims

Check permission levels from claims:

```csharp
public class RequiresPermissionLevel : AuthorizationRule
{
    private readonly int _requiredLevel;
    private readonly IPropertyInfo _contextProperty;

    public RequiresPermissionLevel(
        AuthorizationActions action,
        IMemberInfo element,
        int requiredLevel,
        IPropertyInfo contextProperty = null)
        : base(action, element)
    {
        _requiredLevel = requiredLevel;
        _contextProperty = contextProperty;
        CacheResult = (contextProperty == null); // Cache if not context-dependent
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var principal = context.ApplicationContext.Principal;

        // Get user's permission level from claims
        var levelClaim = principal.Claims.FirstOrDefault(c => c.Type == "PermissionLevel");

        if (levelClaim == null || !int.TryParse(levelClaim.Value, out int userLevel))
        {
            context.HasPermission = false;
            return;
        }

        // Basic permission check
        if (userLevel < _requiredLevel)
        {
            context.HasPermission = false;
            return;
        }

        // If there's a context property, check additional conditions
        if (_contextProperty != null)
        {
            decimal amount = (decimal)ReadProperty(context.Target, _contextProperty);

            // Higher amounts need higher permission levels
            if (amount > 10000 && userLevel < 3)
                context.HasPermission = false;
            else if (amount > 1000 && userLevel < 2)
                context.HasPermission = false;
            else
                context.HasPermission = true;
        }
        else
        {
            context.HasPermission = true;
        }
    }
}
```

**Usage:**

```csharp
[CslaImplementProperties]
public partial class PurchaseOrder : BusinessBase<PurchaseOrder>
{
    public partial decimal Amount { get; set; }
    public partial string Status { get; set; }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // Users need permission level 2+ to approve, but amount affects this
        BusinessRules.AddRule(
            new RequiresPermissionLevel(
                AuthorizationActions.WriteProperty,
                StatusProperty,
                2,
                AmountProperty));
    }
}
```

## Custom Property Authorization Rule (Context-Based)

Create a custom rule with conditional logic:

```csharp
public class OnlyForUS : AuthorizationRule
{
    private IMemberInfo _countryProperty;

    public OnlyForUS(
        AuthorizationActions action,
        IMemberInfo element,
        IMemberInfo countryProperty)
        : base(action, element)
    {
        _countryProperty = countryProperty;

        // Don't cache the result - re-evaluate each time
        CacheResult = false;
    }

    protected override void Execute(IAuthorizationContext context)
    {
        // Get the country value from the target object
        string country = (string)ReadProperty(context.Target, _countryProperty);

        // Only allow the action if country is "US"
        context.HasPermission = country == "US";
    }
}
```

**Usage:**

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial string Country { get; set; }
    public partial string State { get; set; }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // State property can only be written if Country is "US"
        BusinessRules.AddRule(
            new OnlyForUS(AuthorizationActions.WriteProperty, StateProperty, CountryProperty));
    }
}
```

## Object-Level Authorization

Control who can create, fetch, update, or delete entire objects:

```csharp
public class CanEditOrder : AuthorizationRule
{
    public CanEditOrder(AuthorizationActions action)
        : base(action)
    {
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var user = context.ApplicationContext.User;

        // Check if user is in Admin role OR is the order owner
        if (user.IsInRole("Admin"))
        {
            context.HasPermission = true;
        }
        else if (context.Target is Order order)
        {
            // Get current user's ID from claims
            var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                context.HasPermission = (order.OwnerId == userId);
            }
        }
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Control who can edit orders
    BusinessRules.AddRule(new CanEditOrder(AuthorizationActions.EditObject));

    // Control who can delete orders
    BusinessRules.AddRule(new CanEditOrder(AuthorizationActions.DeleteObject));
}
```

## Static Object Authorization

Use attributes for type-level authorization that doesn't depend on object state:

```csharp
[ObjectAuthorizationRules]
public static void AddObjectAuthorizationRules(IAddObjectAuthorizationRulesContext context)
{
    // Only Admin users can create new Customer objects
    context.Rules.AddRule(typeof(Customer),
        new IsInRole(AuthorizationActions.CreateObject, "Admin"));

    // Only Admin and Manager users can delete Customer objects
    context.Rules.AddRule(typeof(Customer),
        new IsInRole(AuthorizationActions.DeleteObject, "Admin", "Manager"));

    // Anyone can fetch Customer objects
    // (No rule = allowed by default)
}
```

**Alternative syntax using AddBusinessRules:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Object-level authorization
    BusinessRules.AddRule(typeof(Customer),
        new IsInRole(AuthorizationActions.CreateObject, "Admin"));

    // Property-level authorization
    BusinessRules.AddRule(
        new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin"));
}
```

## Method Authorization

Authorize specific method execution:

```csharp
public class CanApproveOrder : AuthorizationRule
{
    public CanApproveOrder(AuthorizationActions action, IMemberInfo method)
        : base(action, method)
    {
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var user = context.ApplicationContext.User;

        // Only users in Manager or Admin roles can approve orders
        context.HasPermission = user.IsInRole("Manager") || user.IsInRole("Admin");
    }
}
```

**Usage:**

```csharp
[CslaImplementProperties]
public partial class Order : BusinessBase<Order>
{
    // ... properties ...

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // Authorize the Approve method
        BusinessRules.AddRule(
            new CanApproveOrder(AuthorizationActions.ExecuteMethod,
                typeof(Order).GetMethod(nameof(Approve))));
    }

    public void Approve()
    {
        // Check authorization before executing
        if (!CanExecuteMethod(nameof(Approve)))
            throw new System.Security.SecurityException("Not authorized to approve orders");

        // Approve the order
        Status = "Approved";
        ApprovedDate = DateTime.Now;
    }
}
```

## CacheResult Property

By default, authorization results are cached. Set `CacheResult = false` if the result depends on object state:

```csharp
public class OnlyForUS : AuthorizationRule
{
    public OnlyForUS(AuthorizationActions action, IMemberInfo element, IMemberInfo countryProperty)
        : base(action, element)
    {
        // Re-evaluate every time because it depends on the Country property value
        CacheResult = false;
    }

    protected override void Execute(IAuthorizationContext context)
    {
        // ...
    }
}
```

**When to use CacheResult = false:**
- Authorization depends on property values in the object
- Authorization depends on database data that might change
- Authorization is based on time-sensitive data

**When to use CacheResult = true (default):**
- Authorization only depends on user roles
- Authorization only depends on user claims
- Authorization logic is expensive and the result won't change

## Checking Authorization in Code

### Check Property Authorization

```csharp
public void DoSomething()
{
    if (CanWriteProperty(SalaryProperty))
    {
        Salary = 50000;
    }
    else
    {
        throw new System.Security.SecurityException("Not authorized to write Salary");
    }
}
```

### Check Method Authorization

```csharp
public void Approve()
{
    if (!CanExecuteMethod(nameof(Approve)))
        throw new System.Security.SecurityException("Not authorized to approve");

    // Proceed with approval
    Status = "Approved";
}
```

### Check Object Authorization

```csharp
public static bool CanUserCreateOrder(IDataPortal<Order> portal)
{
    return Csla.Rules.BusinessRules.HasPermission(
        AuthorizationActions.CreateObject,
        typeof(Order));
}
```

## UI Integration

UI frameworks can query authorization rules to show/hide controls:

```csharp
// In a view model or controller
public bool CanEditSalary
{
    get
    {
        if (_customer != null)
            return _customer.CanWriteProperty(Customer.SalaryProperty);
        return false;
    }
}
```

**In XAML (WPF/MAUI):**

```xml
<TextBox Text="{Binding Customer.Salary}"
         IsEnabled="{Binding CanEditSalary}" />
```

**In Razor (Blazor):**

```razor
@if (Customer.CanWriteProperty(Customer.SalaryProperty))
{
    <InputNumber @bind-Value="Customer.Salary" />
}
else
{
    <span>@Customer.Salary</span>
}
```

## Combining Authorization and Validation

Authorization rules run before validation rules. A property that fails authorization won't trigger validation:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Authorization - runs first
    BusinessRules.AddRule(
        new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin"));

    // Validation - only runs if authorized
    BusinessRules.AddRule(
        new MinValue<decimal>(SalaryProperty, 0));
}
```

## Asynchronous Authorization Rules

For authorization that requires async operations (like database queries), use `AuthorizationRuleAsync`:

```csharp
public class CanEditDocument : AuthorizationRuleAsync
{
    public CanEditDocument(AuthorizationActions action, IMemberInfo element)
        : base(action, element)
    {
        CacheResult = false; // Re-check each time
    }

    protected override async Task ExecuteAsync(IAuthorizationContext context)
    {
        if (context.Target is Document doc)
        {
            // Query database to check permissions
            var dal = context.ApplicationContext.GetRequiredService<IDocumentDal>();
            var hasPermission = await dal.UserCanEditAsync(doc.Id, context.ApplicationContext.User);

            context.HasPermission = hasPermission;
        }
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Async authorization rule
    BusinessRules.AddRule(
        new CanEditDocument(AuthorizationActions.WriteProperty, ContentProperty));
}
```

## Common Patterns

### Pattern 1: Role-Based Property Access

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Admin can write everything
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin"));
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, TitleProperty, "Admin"));

    // Manager can write some fields
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, TitleProperty, "Manager"));
}
```

### Pattern 2: Ownership-Based Authorization

```csharp
public class IsOwner : AuthorizationRule
{
    private IPropertyInfo _ownerIdProperty;

    public IsOwner(AuthorizationActions action, IMemberInfo element, IPropertyInfo ownerIdProperty)
        : base(action, element)
    {
        _ownerIdProperty = ownerIdProperty;
        CacheResult = false; // Depends on object state
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var ownerId = (int)ReadProperty(context.Target, _ownerIdProperty);
        var currentUserId = GetCurrentUserId(context);

        context.HasPermission = (ownerId == currentUserId) || context.ApplicationContext.User.IsInRole("Admin");
    }

    private int GetCurrentUserId(IAuthorizationContext context)
    {
        var claim = context.ApplicationContext.Principal.Claims.FirstOrDefault(c => c.Type == "UserId");
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }
}
```

### Pattern 3: Time-Based Authorization

```csharp
public class OnlyDuringBusinessHours : AuthorizationRule
{
    public OnlyDuringBusinessHours(AuthorizationActions action, IMemberInfo element)
        : base(action, element)
    {
        CacheResult = false; // Time changes
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var now = DateTime.Now;
        var isBusinessHours = now.Hour >= 9 && now.Hour < 17 && now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday;

        context.HasPermission = isBusinessHours || context.ApplicationContext.User.IsInRole("Admin");
    }
}
```

### Pattern 4: Combined Role and State

```csharp
public class CanApproveBasedOnAmount : AuthorizationRule
{
    private IPropertyInfo _amountProperty;

    public CanApproveBasedOnAmount(AuthorizationActions action, IMemberInfo element, IPropertyInfo amountProperty)
        : base(action, element)
    {
        _amountProperty = amountProperty;
        CacheResult = false;
    }

    protected override void Execute(IAuthorizationContext context)
    {
        decimal amount = (decimal)ReadProperty(context.Target, _amountProperty);
        var user = context.ApplicationContext.User;

        if (amount <= 1000 && user.IsInRole("Manager"))
            context.HasPermission = true;
        else if (amount <= 10000 && user.IsInRole("Director"))
            context.HasPermission = true;
        else if (user.IsInRole("CEO"))
            context.HasPermission = true;
        else
            context.HasPermission = false;
    }
}
```

## Best Practices

1. **Remember: One rule per action** - Only one authorization rule per property/action combination
2. **Prefer Claims over Roles** - Claims provide more granular control and are more flexible
3. **Combine Claims with property values** - Most custom rules check both user claims and object state
4. **Use built-in rules for simple cases** - `IsInRole` and `IsNotInRole` work for basic scenarios
5. **Set CacheResult appropriately** - Use `false` when authorization depends on object state or property values
6. **Check authorization in methods** - Always verify authorization before sensitive operations
7. **Provide user feedback** - UI should reflect what users can/cannot do
8. **Fail securely** - Default to `HasPermission = false`
9. **Keep rules simple** - Complex authorization logic should be in a service, not a rule
10. **Test authorization thoroughly** - Security bugs are critical
11. **Don't put business logic in auth rules** - Keep authorization separate from validation
12. **Document authorization requirements** - Make security requirements clear to developers

## Authorization vs. Validation

| Authorization Rules | Validation Rules |
|---------------------|------------------|
| Answer: Can the user do this? | Answer: Is the data valid? |
| Based on user identity | Based on data values |
| Security concern | Data quality concern |
| Failure = SecurityException | Failure = BrokenRules |
| Run first | Run after authorization passes |
| Usually role-based | Usually property-based |

## Notes

- Authorization rules run before validation rules
- If authorization fails, validation rules don't run
- Authorization results can be cached for performance (default)
- Object-level authorization can be checked before creating/fetching objects
- The `ReadProperty` method in authorization rules bypasses authorization checks
- Authorization rules work seamlessly across application tiers with CSLA's Data Portal

## See Also

- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesPriority.md](BusinessRulesPriority.md) - Rule priorities and execution order
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Validation rules
- [BusinessRulesAsync.md](BusinessRulesAsync.md) - Asynchronous rules (including async authorization)
