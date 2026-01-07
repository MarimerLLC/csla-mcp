# Business Rules: Object-Level Rules

Object-level rules validate or authorize the business object as a whole, rather than individual properties. These rules are essential for cross-property validation and object-level authorization.

## Overview

Most business rules are property-level rules that validate or calculate individual property values. However, many business scenarios require validating relationships between multiple properties or authorizing actions on entire objects.

**Object-level rules:**
- Have no primary property (`PrimaryProperty` is `null`)
- Validate relationships between multiple properties
- Check overall object state
- Control object-level authorization (Create, Get, Edit, Delete)
- Run when `CheckRules()` or `CheckRulesAsync()` is called
- Can run when any dependent property changes

## Object-Level Validation Rules

### Basic Object-Level Validation

Create a rule without specifying a primary property:

```csharp
public class ValidDateRange : BusinessRule
{
    private IPropertyInfo _startDateProperty;
    private IPropertyInfo _endDateProperty;

    public ValidDateRange(IPropertyInfo startDateProperty, IPropertyInfo endDateProperty)
        : base(null)  // No primary property - this is an object-level rule
    {
        _startDateProperty = startDateProperty;
        _endDateProperty = endDateProperty;

        InputProperties.Add(startDateProperty);
        InputProperties.Add(endDateProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        DateTime startDate = (DateTime)context.InputPropertyValues[_startDateProperty];
        DateTime endDate = (DateTime)context.InputPropertyValues[_endDateProperty];

        if (startDate > endDate)
        {
            // Object-level error - not associated with a specific property
            context.AddErrorResult("Start date must be before end date");
        }
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Property-level rules
    BusinessRules.AddRule(new Required(StartDateProperty));
    BusinessRules.AddRule(new Required(EndDateProperty));

    // Object-level rule
    BusinessRules.AddRule(new ValidDateRange(StartDateProperty, EndDateProperty));

    // Trigger object-level rule when either property changes
    BusinessRules.AddRule(new Dependency(StartDateProperty, null));  // null = object level
    BusinessRules.AddRule(new Dependency(EndDateProperty, null));
}
```

**Key Points:**
- Pass `null` as the primary property to create an object-level rule
- Object-level errors appear in `BusinessRules.BrokenRules` but not associated with a specific property
- Use `Dependency(property, null)` to trigger object-level rules when properties change

### Multi-Property Validation

Validate complex relationships between multiple properties:

```csharp
public class ValidOrderState : BusinessRule
{
    private IPropertyInfo _statusProperty;
    private IPropertyInfo _approvedDateProperty;
    private IPropertyInfo _approvedByProperty;

    public ValidOrderState(
        IPropertyInfo statusProperty,
        IPropertyInfo approvedDateProperty,
        IPropertyInfo approvedByProperty)
        : base(null)  // Object-level rule
    {
        _statusProperty = statusProperty;
        _approvedDateProperty = approvedDateProperty;
        _approvedByProperty = approvedByProperty;

        InputProperties.Add(statusProperty);
        InputProperties.Add(approvedDateProperty);
        InputProperties.Add(approvedByProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string status = (string)context.InputPropertyValues[_statusProperty];
        DateTime? approvedDate = (DateTime?)context.InputPropertyValues[_approvedDateProperty];
        string approvedBy = (string)context.InputPropertyValues[_approvedByProperty];

        if (status == "Approved")
        {
            // If approved, must have approval date and approver
            if (!approvedDate.HasValue)
                context.AddErrorResult("Approved orders must have an approval date");

            if (string.IsNullOrWhiteSpace(approvedBy))
                context.AddErrorResult("Approved orders must specify who approved them");
        }
        else
        {
            // If not approved, should NOT have approval data
            if (approvedDate.HasValue || !string.IsNullOrWhiteSpace(approvedBy))
                context.AddWarningResult("Approval information will be cleared when status is not 'Approved'");
        }
    }
}
```

### Business Logic Validation

Validate business logic that spans multiple properties:

```csharp
public class ValidDiscount : BusinessRule
{
    private IPropertyInfo _subtotalProperty;
    private IPropertyInfo _discountPercentProperty;
    private IPropertyInfo _discountAmountProperty;
    private IPropertyInfo _totalProperty;

    public ValidDiscount(
        IPropertyInfo subtotalProperty,
        IPropertyInfo discountPercentProperty,
        IPropertyInfo discountAmountProperty,
        IPropertyInfo totalProperty)
        : base(null)  // Object-level
    {
        _subtotalProperty = subtotalProperty;
        _discountPercentProperty = discountPercentProperty;
        _discountAmountProperty = discountAmountProperty;
        _totalProperty = totalProperty;

        InputProperties.Add(subtotalProperty);
        InputProperties.Add(discountPercentProperty);
        InputProperties.Add(discountAmountProperty);
        InputProperties.Add(totalProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal subtotal = (decimal)context.InputPropertyValues[_subtotalProperty];
        decimal discountPercent = (decimal)context.InputPropertyValues[_discountPercentProperty];
        decimal discountAmount = (decimal)context.InputPropertyValues[_discountAmountProperty];
        decimal total = (decimal)context.InputPropertyValues[_totalProperty];

        // Verify discount calculations are consistent
        decimal expectedDiscount = subtotal * (discountPercent / 100);
        decimal expectedTotal = subtotal - discountAmount;

        if (Math.Abs(discountAmount - expectedDiscount) > 0.01m)
        {
            context.AddErrorResult($"Discount amount ${discountAmount} doesn't match discount percent {discountPercent}%");
        }

        if (Math.Abs(total - expectedTotal) > 0.01m)
        {
            context.AddErrorResult($"Total ${total} is incorrect. Expected ${expectedTotal}");
        }
    }
}
```

## Object-Level Authorization Rules

Object-level authorization controls who can perform operations on entire business objects.

### Authorization for Data Portal Operations

Control who can create, fetch, update, or delete objects:

```csharp
public class RequiresManagerRole : AuthorizationRule
{
    public RequiresManagerRole(AuthorizationActions action)
        : base(action)  // No element - applies to entire object
    {
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var user = context.ApplicationContext.User;

        if (!user.IsInRole("Manager") && !user.IsInRole("Admin"))
        {
            context.HasPermission = false;
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
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Object-level authorization
    BusinessRules.AddRule(new RequiresManagerRole(AuthorizationActions.CreateObject));
    BusinessRules.AddRule(new RequiresManagerRole(AuthorizationActions.DeleteObject));

    // Anyone can fetch (no rule = allowed)
    // Property-level rules control who can edit specific fields
}
```

### Static Object Authorization

Use the `[ObjectAuthorizationRules]` attribute for type-level authorization:

```csharp
[ObjectAuthorizationRules]
public static void AddObjectAuthorizationRules(IAddObjectAuthorizationRulesContext context)
{
    // Only Admin users can create Order objects
    context.Rules.AddRule(typeof(Order),
        new IsInRole(AuthorizationActions.CreateObject, "Admin"));

    // Only Admin and Manager users can delete Order objects
    context.Rules.AddRule(typeof(Order),
        new IsInRole(AuthorizationActions.DeleteObject, "Admin", "Manager"));

    // Anyone can fetch orders (no rule = allowed)

    // Edit authorization is handled per-instance in AddBusinessRules
}
```

### State-Based Object Authorization

Authorization based on object state and user identity:

```csharp
public class CanEditOrder : AuthorizationRule
{
    private IPropertyInfo _ownerIdProperty;
    private IPropertyInfo _statusProperty;

    public CanEditOrder(IPropertyInfo ownerIdProperty, IPropertyInfo statusProperty)
        : base(AuthorizationActions.EditObject)
    {
        _ownerIdProperty = ownerIdProperty;
        _statusProperty = statusProperty;

        CacheResult = false;  // Re-evaluate each time
    }

    protected override void Execute(IAuthorizationContext context)
    {
        // Get object state
        int ownerId = (int)ReadProperty(context.Target, _ownerIdProperty);
        string status = (string)ReadProperty(context.Target, _statusProperty);

        // Get current user
        var principal = context.ApplicationContext.Principal;
        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "UserId");

        if (userIdClaim == null)
        {
            context.HasPermission = false;
            return;
        }

        int currentUserId = int.Parse(userIdClaim.Value);

        // Business rules:
        // 1. Admins can edit any order
        // 2. Owners can edit their own orders if status is "Draft" or "Pending"
        // 3. No one can edit "Completed" or "Cancelled" orders

        if (status == "Completed" || status == "Cancelled")
        {
            // Only admins can edit completed/cancelled orders
            context.HasPermission = principal.IsInRole("Admin");
        }
        else if (ownerId == currentUserId)
        {
            // Owner can edit their own draft/pending orders
            context.HasPermission = true;
        }
        else
        {
            // Others need admin role
            context.HasPermission = principal.IsInRole("Admin");
        }
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Object-level authorization based on state
    BusinessRules.AddRule(new CanEditOrder(OwnerIdProperty, StatusProperty));
}
```

## Triggering Object-Level Rules

### Manual Trigger

Object-level rules run when `CheckRules()` is called:

```csharp
[Create]
private void Create()
{
    LoadProperty(StatusProperty, "Draft");
    LoadProperty(CreatedDateProperty, DateTime.UtcNow);

    // Runs all rules, including object-level
    await BusinessRules.CheckRulesAsync();
}
```

### Automatic Trigger with Dependencies

Set up dependencies to trigger object-level rules when properties change:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Object-level rule
    BusinessRules.AddRule(new ValidDateRange(StartDateProperty, EndDateProperty));

    // Trigger object-level rules when these properties change
    BusinessRules.AddRule(new Dependency(StartDateProperty, null));  // null = object level
    BusinessRules.AddRule(new Dependency(EndDateProperty, null));
}
```

When `StartDate` or `EndDate` changes, `ValidDateRange` will automatically run.

## Displaying Object-Level Errors

Object-level broken rules aren't associated with a specific property:

```csharp
// Check for object-level errors
if (!myObject.IsValid)
{
    var objectErrors = myObject.BrokenRulesCollection
        .Where(r => r.Property == null || string.IsNullOrEmpty(r.Property))
        .Select(r => r.Description);

    foreach (var error in objectErrors)
    {
        Console.WriteLine($"Object Error: {error}");
    }
}
```

**In UI (example for Blazor):**

```razor
@if (!Model.IsValid)
{
    <div class="alert alert-danger">
        <h5>Validation Errors:</h5>
        <ul>
            @foreach (var rule in Model.BrokenRulesCollection.Where(r => string.IsNullOrEmpty(r.Property)))
            {
                <li>@rule.Description</li>
            }
        </ul>
    </div>
}
```

## Common Patterns

### Pattern 1: Required Combination

At least one of multiple fields must have a value:

```csharp
public class RequireOneOf : BusinessRule
{
    private IPropertyInfo[] _properties;

    public RequireOneOf(params IPropertyInfo[] properties)
        : base(null)  // Object-level
    {
        _properties = properties;
        foreach (var prop in properties)
        {
            InputProperties.Add(prop);
        }
    }

    protected override void Execute(IRuleContext context)
    {
        bool hasValue = _properties.Any(prop =>
        {
            var value = context.InputPropertyValues[prop];
            return value != null && !string.IsNullOrWhiteSpace(value.ToString());
        });

        if (!hasValue)
        {
            var names = string.Join(", ", _properties.Select(p => p.FriendlyName));
            context.AddErrorResult($"At least one of the following is required: {names}");
        }
    }
}
```

### Pattern 2: Mutually Exclusive Fields

Only one of multiple fields can have a value:

```csharp
public class MutuallyExclusive : BusinessRule
{
    private IPropertyInfo[] _properties;

    public MutuallyExclusive(params IPropertyInfo[] properties)
        : base(null)
    {
        _properties = properties;
        foreach (var prop in properties)
        {
            InputProperties.Add(prop);
        }
    }

    protected override void Execute(IRuleContext context)
    {
        int countWithValues = _properties.Count(prop =>
        {
            var value = context.InputPropertyValues[prop];
            return value != null && !string.IsNullOrWhiteSpace(value.ToString());
        });

        if (countWithValues > 1)
        {
            var names = string.Join(", ", _properties.Select(p => p.FriendlyName));
            context.AddErrorResult($"Only one of the following can have a value: {names}");
        }
    }
}
```

### Pattern 3: Workflow State Validation

Validate object state transitions:

```csharp
public class ValidStatusTransition : BusinessRule
{
    private IPropertyInfo _statusProperty;

    public ValidStatusTransition(IPropertyInfo statusProperty)
        : base(null)
    {
        _statusProperty = statusProperty;
        InputProperties.Add(statusProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var target = (Order)context.Target;
        string newStatus = (string)context.InputPropertyValues[_statusProperty];
        string oldStatus = ReadProperty(target, _statusProperty) as string;

        if (oldStatus == newStatus)
            return;

        // Define valid transitions
        var validTransitions = new Dictionary<string, string[]>
        {
            { "Draft", new[] { "Pending", "Cancelled" } },
            { "Pending", new[] { "Approved", "Rejected", "Cancelled" } },
            { "Approved", new[] { "Completed", "Cancelled" } },
            { "Rejected", new[] { "Draft" } },
            { "Completed", Array.Empty<string>() },  // Final state
            { "Cancelled", Array.Empty<string>() }   // Final state
        };

        if (!validTransitions.ContainsKey(oldStatus))
        {
            context.AddErrorResult($"Invalid current status: {oldStatus}");
            return;
        }

        if (!validTransitions[oldStatus].Contains(newStatus))
        {
            context.AddErrorResult($"Cannot transition from {oldStatus} to {newStatus}");
        }
    }
}
```

## Best Practices

1. **Use object-level rules for cross-property validation** - Don't try to validate multiple properties from a single-property rule
2. **Set up dependencies** - Use `Dependency(property, null)` to trigger object-level rules automatically
3. **Display object-level errors prominently** - They're not tied to a specific field in the UI
4. **Use for complex authorization** - Object-level authorization can check multiple properties and object state
5. **Keep rules focused** - Even object-level rules should have a single responsibility
6. **Consider performance** - Object-level rules with many input properties can be expensive
7. **Test edge cases** - Verify rules work when properties are null or have default values
8. **Document business logic** - Object-level rules often encode complex business rules

## Notes

- Object-level rules have `PrimaryProperty == null`
- Object-level errors appear in `BrokenRulesCollection` with an empty or null `Property` value
- Use `Dependency(property, null)` to trigger object-level rules when specific properties change
- Object-level authorization rules control Create, Get, Edit, and Delete operations
- Static authorization (via `[ObjectAuthorizationRules]`) is checked before object instantiation
- Instance authorization (via `AddBusinessRules`) is checked after object is loaded

## See Also

- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Property-level validation rules
- [BusinessRulesAuthorization.md](BusinessRulesAuthorization.md) - Authorization rules
- [BusinessRulesContext.md](BusinessRulesContext.md) - Rule context and execution flags
- [BusinessRulesPriority.md](BusinessRulesPriority.md) - Rule priorities and execution order
