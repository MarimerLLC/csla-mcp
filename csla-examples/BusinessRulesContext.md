# Business Rules: Rule Context and Execution Flags

The `IRuleContext` parameter passed to business rules provides important information about the execution environment and state. Understanding these context flags helps you create rules that behave appropriately in different scenarios.

## IRuleContext Overview

Every business rule receives an `IRuleContext` parameter in its `Execute` or `ExecuteAsync` method:

```csharp
protected override void Execute(IRuleContext context)
{
    // Access context properties here
}
```

## Key Context Properties

### IsCheckRulesContext

Indicates whether the rule is executing because `CheckRules()` or `CheckRulesAsync()` was explicitly called.

```csharp
public class MyRule : BusinessRule
{
    public MyRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        if (context.IsCheckRulesContext)
        {
            // Rule is running from explicit CheckRules call
            Console.WriteLine($"Rule running from CheckRules");
        }
        else
        {
            // Rule is running because a property changed
            Console.WriteLine($"Rule running from {PrimaryProperty.Name} property change");
        }

        // Normal rule logic continues...
        var value = (string)context.InputPropertyValues[PrimaryProperty];
        if (string.IsNullOrWhiteSpace(value))
            context.AddErrorResult("Value is required");
    }
}
```

**When IsCheckRulesContext is true:**
- `CheckRules()` or `CheckRulesAsync()` was explicitly called
- Usually happens during object initialization or save operations
- All rules run regardless of whether properties changed

**When IsCheckRulesContext is false:**
- Rule is running because a property value changed
- Triggered by `SetProperty()` or cascading from another rule
- Only rules for affected properties run

### Common Use Cases for IsCheckRulesContext

**Skip expensive operations during property changes:**

```csharp
public class ExpensiveValidation : BusinessRuleAsync
{
    public ExpensiveValidation(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        if (!context.IsCheckRulesContext)
        {
            // Skip expensive database lookup during property changes
            // Will still run during CheckRules (like before save)
            return;
        }

        // Expensive validation - only during CheckRules
        var value = (string)context.InputPropertyValues[PrimaryProperty];
        var dal = context.ApplicationContext.GetRequiredService<ICustomerDal>();
        bool isValid = await dal.ValidateAsync(value);

        if (!isValid)
            context.AddErrorResult("Value is not valid");
    }
}
```

**Different behavior based on context:**

```csharp
public class SmartValidation : BusinessRule
{
    public SmartValidation(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var value = (int)context.InputPropertyValues[PrimaryProperty];

        if (context.IsCheckRulesContext)
        {
            // Strict validation during CheckRules (before save)
            if (value < 1 || value > 100)
                context.AddErrorResult("Value must be between 1 and 100");
        }
        else
        {
            // Lenient validation during property changes (better UX)
            if (value < 1 || value > 100)
                context.AddWarningResult("Value should be between 1 and 100");
        }
    }
}
```

## InputPropertyValues

Access to the current values of all properties declared in `InputProperties`:

```csharp
public class CalculateTotal : BusinessRule
{
    private IPropertyInfo _quantityProperty;
    private IPropertyInfo _priceProperty;

    public CalculateTotal(
        IPropertyInfo totalProperty,
        IPropertyInfo quantityProperty,
        IPropertyInfo priceProperty)
        : base(totalProperty)
    {
        _quantityProperty = quantityProperty;
        _priceProperty = priceProperty;

        InputProperties.Add(quantityProperty);
        InputProperties.Add(priceProperty);
        AffectedProperties.Add(totalProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        // Access input values from context
        int quantity = (int)context.InputPropertyValues[_quantityProperty];
        decimal price = (decimal)context.InputPropertyValues[_priceProperty];

        decimal total = quantity * price;
        context.AddOutValue(PrimaryProperty, total);
    }
}
```

## Target

Reference to the business object being validated:

```csharp
public class ConditionalRule : BusinessRule
{
    public ConditionalRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
    }

    protected override void Execute(IRuleContext context)
    {
        // Cast to your business object type
        var obj = (MyBusinessObject)context.Target;

        // Check object state
        if (obj.IsNew)
        {
            // Different validation for new objects
        }
        else if (obj.IsDirty)
        {
            // Different validation for modified objects
        }

        // Access object properties directly (bypasses authorization)
        var status = ReadProperty(context.Target, StatusProperty);
    }
}
```

**Warning:** Be careful when accessing `context.Target` in async rules - the target object may not be thread-safe. Use `InputPropertyValues` instead when possible.

## ApplicationContext

Access to application-level services and state:

```csharp
public class UserAwareRule : BusinessRule
{
    public UserAwareRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        // Access current user
        var user = context.ApplicationContext.User;
        var principal = context.ApplicationContext.Principal;

        // Get services from DI
        var logger = context.ApplicationContext.GetRequiredService<ILogger<UserAwareRule>>();
        var dal = context.ApplicationContext.GetRequiredService<ICustomerDal>();

        // Check user permissions
        if (!user.IsInRole("Admin"))
        {
            context.AddErrorResult("Only admins can modify this field");
        }

        logger.LogInformation($"Rule executed by user: {user.Identity.Name}");
    }
}
```

## Exception Property

Check if an exception occurred during rule execution (used in `Complete` phase):

```csharp
public class RuleWithErrorHandling : BusinessRule
{
    public RuleWithErrorHandling(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
    }

    protected override void Execute(IRuleContext context)
    {
        try
        {
            // Rule logic that might throw
            var value = (string)context.InputPropertyValues[PrimaryProperty];
            var result = SomeMethodThatMightThrow(value);

            if (!result)
                context.AddErrorResult("Validation failed");
        }
        catch (Exception ex)
        {
            // Log and add appropriate error message
            var logger = context.ApplicationContext.GetRequiredService<ILogger<RuleWithErrorHandling>>();
            logger.LogError(ex, "Rule execution failed");

            context.AddErrorResult("An error occurred during validation");
        }
    }
}
```

## Adding Results to Context

### AddErrorResult

Adds an error and stops subsequent rule execution (short-circuits):

```csharp
context.AddErrorResult("This value is required");
context.AddErrorResult("Value must be between {0} and {1}", 1, 100);
```

### AddWarningResult

Adds a warning but allows subsequent rules to run:

```csharp
context.AddWarningResult("This value is unusual");
```

### AddInformationResult

Adds an informational message:

```csharp
context.AddInformationResult("Value was automatically corrected");
```

### AddSuccessResult

Indicates success and optionally stops rule execution:

```csharp
// Stop all subsequent rules for this property
context.AddSuccessResult(true);

// Mark success but allow rules to continue
context.AddSuccessResult(false);
```

### AddOutValue

Sets a property value (for calculation rules):

```csharp
context.AddOutValue(TotalProperty, calculatedTotal);
```

## Practical Examples

### Example 1: Debug Logging Based on Context

```csharp
public class DiagnosticRule : BusinessRule
{
    public DiagnosticRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var logger = context.ApplicationContext.GetRequiredService<ILogger<DiagnosticRule>>();
        var value = context.InputPropertyValues[PrimaryProperty];

        if (context.IsCheckRulesContext)
        {
            logger.LogInformation($"CheckRules validation: {PrimaryProperty.Name} = {value}");
        }
        else
        {
            logger.LogDebug($"Property change: {PrimaryProperty.Name} = {value}");
        }

        // Normal validation logic
        if (value == null)
            context.AddErrorResult("Value cannot be null");
    }
}
```

### Example 2: State-Based Validation

```csharp
public class StateBasedValidation : BusinessRule
{
    public StateBasedValidation(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var target = (ITrackStatus)context.Target;
        var value = (string)context.InputPropertyValues[PrimaryProperty];

        // Different rules based on object state
        if (target.IsNew)
        {
            // New objects must have a value
            if (string.IsNullOrWhiteSpace(value))
                context.AddErrorResult("This field is required for new records");
        }
        else if (target.IsDeleted)
        {
            // Don't validate deleted objects
            context.AddSuccessResult(true);
        }
        else
        {
            // Existing objects can have optional values
            if (string.IsNullOrWhiteSpace(value))
                context.AddInformationResult("Consider providing a value");
        }
    }
}
```

### Example 3: User-Specific Validation

```csharp
public class ManagerApprovalRequired : BusinessRule
{
    private IPropertyInfo _amountProperty;

    public ManagerApprovalRequired(IPropertyInfo statusProperty, IPropertyInfo amountProperty)
        : base(statusProperty)
    {
        _amountProperty = amountProperty;
        InputProperties.Add(statusProperty);
        InputProperties.Add(amountProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var status = (string)context.InputPropertyValues[PrimaryProperty];
        var amount = (decimal)context.InputPropertyValues[_amountProperty];
        var user = context.ApplicationContext.User;

        // Only validate when trying to approve
        if (status != "Approved")
            return;

        // Check if approval requires manager
        if (amount > 10000 && !user.IsInRole("Manager"))
        {
            context.AddErrorResult("Manager approval required for amounts over $10,000");
        }
    }
}
```

## Best Practices

1. **Use IsCheckRulesContext wisely** - Skip expensive operations during property changes when appropriate
2. **Prefer InputPropertyValues over Target** - More reliable, especially in async scenarios
3. **Handle exceptions gracefully** - Catch and log exceptions, provide user-friendly error messages
4. **Access services via ApplicationContext** - Use DI to get dependencies
5. **Log diagnostic information** - Use different log levels based on IsCheckRulesContext
6. **Consider object state** - Use ITrackStatus properties (IsNew, IsDirty, IsDeleted) for conditional logic
7. **Check user context** - Access User or Principal when validation depends on identity
8. **Test both contexts** - Ensure rules work correctly when called from CheckRules and property changes

## Notes

- `IsCheckRulesContext` is the most commonly used context flag
- Context properties are read-only - you cannot modify them
- The `Target` property should be avoided in async rules for thread-safety
- `ApplicationContext` provides access to all registered services via DI
- Context flags help rules adapt to different execution scenarios

## See Also

- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesPriority.md](BusinessRulesPriority.md) - Rule priorities and short-circuiting
- [BusinessRulesAsync.md](BusinessRulesAsync.md) - Asynchronous business rules
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Validation rules
