# Creating Business Rules

CSLA provides a powerful rules engine that validates business logic and enforces authorization. Rules are automatically invoked when properties change, when objects are saved, or when explicitly triggered.

## Ways to Create Rules

There are three primary approaches to creating business rules in CSLA:

1. **Implement `IBusinessRule`** (typically by subclassing `BusinessRule`) - For validation and calculation logic
2. **Implement `IAuthorizationRule`** (typically by subclassing `AuthorizationRule`) - For permission-based rules
3. **Create custom data annotations** - For reusable validation attributes that work like built-in annotations

## Using Built-In Rules

CSLA provides many built-in validation rules in the `Csla.Rules.CommonRules` namespace:

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial string Name { get; set; }
    public partial string Email { get; set; }
    public partial int Age { get; set; }
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        
        // Built-in validation rules
        BusinessRules.AddRule(new Required(NameProperty));
        BusinessRules.AddRule(new MaxLength(NameProperty, 100));
        BusinessRules.AddRule(new Required(EmailProperty));
        BusinessRules.AddRule(new Email(EmailProperty));
        BusinessRules.AddRule(new MinValue<int>(AgeProperty, 18));
        BusinessRules.AddRule(new MaxValue<int>(AgeProperty, 120));
    }
}
```

### Common Built-In Rules

```csharp
using Csla.Rules.CommonRules;

// String validation
BusinessRules.AddRule(new Required(PropertyInfo));
BusinessRules.AddRule(new MaxLength(PropertyInfo, 100));
BusinessRules.AddRule(new MinLength(PropertyInfo, 2));
BusinessRules.AddRule(new RegEx(PropertyInfo, @"pattern"));

// Numeric validation
BusinessRules.AddRule(new MinValue<int>(PropertyInfo, 0));
BusinessRules.AddRule(new MaxValue<int>(PropertyInfo, 100));
BusinessRules.AddRule(new Range<decimal>(PropertyInfo, 0.01m, 999.99m));

// Rule relationships
BusinessRules.AddRule(new Dependency(PropertyInfo, DependentProperty));
```

## Custom Business Rules

Create custom validation or calculation rules by inheriting from `BusinessRule`.

### Simple Validation Rule

```csharp
using Csla.Rules;

public class ValidZipCode : BusinessRule
{
    public ValidZipCode(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties = new List<IPropertyInfo> { primaryProperty };
    }
    
    protected override void Execute(IRuleContext context)
    {
        var zipCode = (string)context.InputPropertyValues[PrimaryProperty];
        
        if (string.IsNullOrWhiteSpace(zipCode))
            return; // Don't validate empty values (use Required rule for that)
        
        // US ZIP code pattern: 12345 or 12345-6789
        var pattern = @"^\d{5}(-\d{4})?$";
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(zipCode, pattern))
        {
            context.AddErrorResult("Invalid ZIP code format");
        }
    }
}

// Usage
[CslaImplementProperties]
public partial class Address : BusinessBase<Address>
{
    public partial string ZipCode { get; set; }
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new ValidZipCode(ZipCodeProperty));
    }
}
```

### Rule with Multiple Input Properties

```csharp
public class EndDateAfterStartDate : BusinessRule
{
    private IPropertyInfo _startDateProperty;
    private IPropertyInfo _endDateProperty;
    
    public EndDateAfterStartDate(IPropertyInfo startDateProperty, IPropertyInfo endDateProperty)
        : base(endDateProperty) // Primary property is the one that shows the error
    {
        _startDateProperty = startDateProperty;
        _endDateProperty = endDateProperty;
        
        InputProperties = new List<IPropertyInfo> { startDateProperty, endDateProperty };
        
        // Re-run this rule when either property changes
        AffectedProperties.Add(endDateProperty);
    }
    
    protected override void Execute(IRuleContext context)
    {
        var startDate = (DateTime?)context.InputPropertyValues[_startDateProperty];
        var endDate = (DateTime?)context.InputPropertyValues[_endDateProperty];
        
        if (startDate.HasValue && endDate.HasValue && endDate.Value <= startDate.Value)
        {
            context.AddErrorResult("End date must be after start date");
        }
    }
}

// Usage
[CslaImplementProperties]
public partial class Project : BusinessBase<Project>
{
    public partial DateTime? StartDate { get; set; }
    public partial DateTime? EndDate { get; set; }
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new EndDateAfterStartDate(StartDateProperty, EndDateProperty));
    }
}
```

### Calculation Rule

Rules can set property values, not just validate them:

```csharp
public class CalculateTotal : BusinessRule
{
    private IPropertyInfo _quantityProperty;
    private IPropertyInfo _priceProperty;
    private IPropertyInfo _totalProperty;
    
    public CalculateTotal(IPropertyInfo quantityProperty, IPropertyInfo priceProperty, IPropertyInfo totalProperty)
        : base(quantityProperty)
    {
        _quantityProperty = quantityProperty;
        _priceProperty = priceProperty;
        _totalProperty = totalProperty;
        
        InputProperties = new List<IPropertyInfo> { quantityProperty, priceProperty };
        AffectedProperties.Add(totalProperty);
        
        // Don't run on server-side during data portal operations
        IsAsync = false;
    }
    
    protected override void Execute(IRuleContext context)
    {
        var quantity = (int)context.InputPropertyValues[_quantityProperty];
        var price = (decimal)context.InputPropertyValues[_priceProperty];
        
        var total = quantity * price;
        
        // Set the calculated value
        context.AddOutValue(_totalProperty, total);
    }
}

// Usage
[CslaImplementProperties]
public partial class OrderItem : BusinessBase<OrderItem>
{
    public partial int Quantity { get; set; }
    public partial decimal Price { get; set; }
    public partial decimal Total { get; private set; } // Calculated by rule
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new CalculateTotal(QuantityProperty, PriceProperty, TotalProperty));
        BusinessRules.AddRule(new Dependency(PriceProperty, QuantityProperty));
    }
}
```

### Async Business Rule

For rules that need to call external services or databases:

```csharp
public class CheckEmailUnique : BusinessRuleAsync
{
    public CheckEmailUnique(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties = new List<IPropertyInfo> { primaryProperty };
        IsAsync = true;
    }
    
    protected override async Task ExecuteAsync(IRuleContext context)
    {
        var email = (string)context.InputPropertyValues[PrimaryProperty];
        
        if (string.IsNullOrWhiteSpace(email))
            return;

        // Execute a command object to check if email exists
        var portal = ApplicationContext.GetRequiredService<IDataPortal<EmailExistsCommand>>();
        var command = await portal.ExecuteAsync(email);

        if (command.Exists)
        {
            context.AddErrorResult("Email address is already in use");
        }
    }
}

// Usage
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial string Email { get; set; }
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new CheckEmailUnique(EmailProperty));
    }
}
```

> **Note:** A rule's `Execute` or `ExecuteAsync` method must NEVER directly talk to the database or any external API. Any such operations MUST flow through something like a command object.

## Authorization Rules

Authorization rules control whether users can read, write, or execute operations on objects and properties.

### Custom Authorization Rule

```csharp
using Csla.Rules;
using System.Security.Claims;

public class HasClaim : AuthorizationRule
{
    private string _claimType;
    private string _claimValue;
    
    public HasClaim(AuthorizationActions action, string claimType, string claimValue)
        : base(action)
    {
        _claimType = claimType;
        _claimValue = claimValue;
    }
    
    public HasClaim(AuthorizationActions action, IPropertyInfo property, string claimType, string claimValue)
        : base(action, property)
    {
        _claimType = claimType;
        _claimValue = claimValue;
    }
    
    protected override void Execute(IAuthorizationContext context)
    {
        var principal = ApplicationContext.Principal as ClaimsPrincipal;
        if (principal == null || !principal.HasClaim(_claimType, _claimValue))
        {
            context.HasPermission = false;
        }
    }
}

// Usage for object-level authorization
[CslaImplementProperties]
public partial class SensitiveData : BusinessBase<SensitiveData>
{
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        
        // Object-level authorization
        BusinessRules.AddRule(new HasClaim(AuthorizationActions.ReadProperty, "Department", "Management"));
        BusinessRules.AddRule(new HasClaim(AuthorizationActions.WriteProperty, ClaimTypes.Role, "Administrator"));
        BusinessRules.AddRule(new HasClaim(AuthorizationActions.DeleteObject, ClaimTypes.Role, "Administrator"));
    }
}

// Usage for property-level authorization
[CslaImplementProperties]
public partial class Employee : BusinessBase<Employee>
{
    public partial string Name { get; set; }
    public partial decimal Salary { get; set; }
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        
        // Anyone can read/write name
        
        // Only users with Department=Management claim can read salary
        BusinessRules.AddRule(new HasClaim(AuthorizationActions.ReadProperty, SalaryProperty, "Department", "Management"));
        
        // Only users with Role=HR claim can write salary
        BusinessRules.AddRule(new HasClaim(AuthorizationActions.WriteProperty, SalaryProperty, ClaimTypes.Role, "HR"));
    }
}
```

### Common Authorization Actions

```csharp
// Object-level actions
AuthorizationActions.CreateObject
AuthorizationActions.GetObject
AuthorizationActions.EditObject
AuthorizationActions.DeleteObject

// Property-level actions
AuthorizationActions.ReadProperty
AuthorizationActions.WriteProperty

// Method execution
AuthorizationActions.ExecuteMethod
```

### Method Authorization

You can control authorization for specific methods on your business objects:

```csharp
[CslaImplementProperties]
public partial class Calculator : BusinessBase<Calculator>
{
    public static MethodInfo DoCalcMethod = RegisterMethod(c => c.DoCalc());
    
    /// <summary>
    /// Performs a calculation. Only authorized users can execute this method.
    /// </summary>
    public void DoCalc()
    {
        // Check authorization
        CanExecuteMethod(DoCalcMethod, true);
        
        // Implementation of method goes here
    }
    
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        
        // Only users in Role1 or Role2 can execute DoCalc
        BusinessRules.AddRule(new IsInRole(AuthorizationActions.ExecuteMethod, DoCalcMethod, "Role1", "Role2"));
    }
}
```

### Built-In Authorization Rules

CSLA provides built-in authorization rules:

```csharp
using Csla.Rules.CommonRules;

protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    // User must be in one of these roles
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "HR", "Administrator"));
    
    // User must NOT be in this role
    BusinessRules.AddRule(new IsNotInRole(AuthorizationActions.DeleteObject, "Guest"));
    
    // Object-level authorization by role
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.EditObject, "Manager", "Administrator"));
    BusinessRules.AddRule(new IsNotInRole(AuthorizationActions.DeleteObject, "Guest", "User"));
}
```

## Custom Data Annotations

Create reusable validation attributes that integrate with CSLA's rules engine:

```csharp
using System;
using System.ComponentModel.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public class ValidZipCodeAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return ValidationResult.Success; // Use [Required] for null checks
        
        var zipCode = value.ToString();
        var pattern = @"^\d{5}(-\d{4})?$";
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(zipCode, pattern))
        {
            return new ValidationResult(
                ErrorMessage ?? "Invalid ZIP code format",
                new[] { validationContext.MemberName });
        }
        
        return ValidationResult.Success;
    }
}

// Usage
[CslaImplementProperties]
public partial class Address : BusinessBase<Address>
{
    [Required]
    [ValidZipCode(ErrorMessage = "Please enter a valid US ZIP code")]
    public partial string ZipCode { get; set; }
}
```

### Custom Annotation with Parameters

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class MinAgeAttribute : ValidationAttribute
{
    public int MinimumAge { get; }
    
    public MinAgeAttribute(int minimumAge)
    {
        MinimumAge = minimumAge;
    }
    
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null)
            return ValidationResult.Success;
        
        if (value is DateTime birthDate)
        {
            var age = DateTime.Today.Year - birthDate.Year;
            if (birthDate > DateTime.Today.AddYears(-age))
                age--;
            
            if (age < MinimumAge)
            {
                return new ValidationResult(
                    ErrorMessage ?? $"Must be at least {MinimumAge} years old",
                    new[] { validationContext.MemberName });
            }
        }
        
        return ValidationResult.Success;
    }
}

// Usage
[CslaImplementProperties]
public partial class Person : BusinessBase<Person>
{
    [Required]
    [MinAge(18, ErrorMessage = "Must be 18 or older to register")]
    public partial DateTime BirthDate { get; set; }
}
```

## Rule Priority and Execution Order

Rules can have priorities that control execution order:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    // Lower priority numbers run first (default is 0)
    BusinessRules.AddRule(new Required(NameProperty) { Priority = 1 });
    BusinessRules.AddRule(new MaxLength(NameProperty, 100) { Priority = 2 });
    
    // This calculation runs after validation
    BusinessRules.AddRule(new CalculateTotal(QuantityProperty, PriceProperty, TotalProperty) { Priority = 10 });
}
```

> **Note:** Async rules do not honor priorities. They run asynchronously and potentially in parallel, resulting in non-deterministic completion order. Only synchronous rules execute in priority order.

## Rule Severity Levels

Rules can have different severity levels:

```csharp
protected override void Execute(IRuleContext context)
{
    var value = (string)context.InputPropertyValues[PrimaryProperty];
    
    if (string.IsNullOrWhiteSpace(value))
    {
        // Error - blocks saving
        context.AddErrorResult("Name is required");
    }
    else if (value.Length < 3)
    {
        // Warning - doesn't block saving but notifies user
        context.AddWarningResult("Name is very short");
    }
    else if (!char.IsUpper(value[0]))
    {
        // Information - just informational
        context.AddInformationResult("Name should start with a capital letter");
    }
}
```

## Short-Circuiting Rules

You can create rules that stop further rule processing based on conditions. Here's an example of a custom rule that stops validation if the user can't write to a property:

```csharp
public class StopIfNotCanWrite : BusinessRule
{
    public StopIfNotCanWrite(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties = new List<IPropertyInfo> { primaryProperty };
    }
    
    protected override void Execute(IRuleContext context)
    {
        var canWrite = CanWriteProperty(PrimaryProperty);
        if (!canWrite)
        {
            context.AddSuccessResult(true); // Stop processing subsequent rules
        }
    }
}

// Usage
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    // Check if user can write (authorization rules don't use Priority)
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, NameProperty, "User"));
    
    // If user can't write, don't run other validation rules
    BusinessRules.AddRule(new StopIfNotCanWrite(NameProperty) { Priority = 0 });
    
    // These only run if user has write permission
    BusinessRules.AddRule(new Required(NameProperty) { Priority = 1 });
    BusinessRules.AddRule(new MaxLength(NameProperty, 100) { Priority = 2 });
}
```

## Best Practices

1. **Use data annotations for simple validation** - They're declarative and reusable
2. **Use BusinessRule for complex validation** - When you need multiple properties or complex logic
3. **Use AuthorizationRule for permissions** - Keep security logic separate from validation
4. **Set appropriate priorities** - Ensure rules run in the correct order
5. **Don't validate null/empty in custom rules** - Use `Required` rule instead
6. **Use warning/information severities** - For non-blocking feedback to users
7. **Make async rules truly async** - Don't block on async calls
8. **Cache rule results when possible** - Especially for expensive operations
9. **Test rules independently** - Rules should be unit-testable

## Common Patterns

### Per-Type Rules

Define rules once for all instances of a type:

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    // Called once when type is first used
    protected override void AddBusinessRulesExtend()
    {
        base.AddBusinessRulesExtend();
        // Rules added here apply to all instances
    }
    
    // Called for each instance
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        // Rules added here are per-instance
    }
}
```

### Conditional Rules

Business rules (validation, calculation) that only apply under certain conditions can use self-contained conditional logic or rule delegation. Note that authorization rules should always be self-contained and cannot use delegation patterns.

#### Self-Contained Conditional Logic

The rule itself contains the logic to determine when it should apply:

```csharp
public class RequiredIfNew : BusinessRule
{
    public RequiredIfNew(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties = new List<IPropertyInfo> { primaryProperty };
    }
    
    protected override void Execute(IRuleContext context)
    {
        var target = (BusinessBase)context.Target;
        if (!target.IsNew)
        {
            return; // Don't validate if not new
        }
        
        var value = context.InputPropertyValues[PrimaryProperty];
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            context.AddErrorResult("Password is required for new objects");
        }
    }
}

// Usage
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    BusinessRules.AddRule(new RequiredIfNew(PasswordProperty));
}
```

#### Rule Delegation Approach

```csharp
public class ConditionalRule : BusinessRule
{
    private IBusinessRule _innerRule;
    private Func<IRuleContext, bool> _condition;
    
    public ConditionalRule(IBusinessRule innerRule, Func<IRuleContext, bool> condition)
        : base(innerRule.PrimaryProperty)
    {
        _innerRule = innerRule;
        _condition = condition;
        InputProperties = innerRule.InputProperties;
        AffectedProperties = innerRule.AffectedProperties;
    }
    
    protected override void Execute(IRuleContext context)
    {
        if (_condition(context))
        {
            // Delegate to the inner rule if condition is met
            _innerRule.Execute(context);
        }
    }
}

// Usage
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    // Only require password for new objects
    var passwordRule = new Required(PasswordProperty);
    BusinessRules.AddRule(new ConditionalRule(passwordRule, ctx => ((BusinessBase)ctx.Target).IsNew));
}
```

#### Conditional Authorization Rules

Authorization rules should be self-contained and include their own conditional logic:

```csharp
public class AdminOnlyForSensitiveData : AuthorizationRule
{
    public AdminOnlyForSensitiveData(AuthorizationActions action, IPropertyInfo property)
        : base(action, property)
    {
    }
    
    protected override void Execute(IAuthorizationContext context)
    {
        var target = (BusinessBase)context.Target;
        
        // Only enforce this authorization rule for sensitive data
        if (target.IsSensitive) // Assuming IsSensitive is a property on the business object
        {
            if (!ApplicationContext.User.IsInRole("Administrator"))
            {
                context.HasPermission = false;
            }
        }
        // If not sensitive, permission is granted by default
    }
}
```

### Cascading Property Updates

When one property change should update others:

```csharp
public class UpdateFullName : BusinessRule
{
    private IPropertyInfo _firstNameProperty;
    private IPropertyInfo _lastNameProperty;
    private IPropertyInfo _fullNameProperty;
    
    public UpdateFullName(IPropertyInfo firstNameProperty, IPropertyInfo lastNameProperty, IPropertyInfo fullNameProperty)
        : base(fullNameProperty)
    {
        _firstNameProperty = firstNameProperty;
        _lastNameProperty = lastNameProperty;
        _fullNameProperty = fullNameProperty;
        
        InputProperties = new List<IPropertyInfo> { firstNameProperty, lastNameProperty };
        AffectedProperties.Add(fullNameProperty);
    }
    
    protected override void Execute(IRuleContext context)
    {
        var firstName = (string)context.InputPropertyValues[_firstNameProperty];
        var lastName = (string)context.InputPropertyValues[_lastNameProperty];
        
        var fullName = $"{firstName} {lastName}".Trim();
        context.AddOutValue(_fullNameProperty, fullName);
    }
}
```
