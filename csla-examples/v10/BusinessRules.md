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

// Comparison
BusinessRules.AddRule(new Dependency(PropertyInfo, DependentProperty));
BusinessRules.AddRule(new StopIfNotCanWrite(PropertyInfo));

// Relationship
BusinessRules.AddRule(new Required(ParentProperty));
BusinessRules.AddRule(new Required(ChildListProperty));
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
        : base(totalProperty)
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
        
        // Get service from DI container
        var dal = ApplicationContext.GetRequiredService<ICustomerDal>();
        
        var exists = await dal.EmailExistsAsync(email);
        
        if (exists)
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

## Authorization Rules

Authorization rules control whether users can read, write, or execute operations on objects and properties.

### Custom Authorization Rule

```csharp
using Csla.Rules;

public class IsInRole : AuthorizationRule
{
    private string _role;
    
    public IsInRole(AuthorizationActions action, string role)
        : base(action)
    {
        _role = role;
    }
    
    public IsInRole(AuthorizationActions action, IPropertyInfo property, string role)
        : base(action, property)
    {
        _role = role;
    }
    
    protected override void Execute(IAuthorizationContext context)
    {
        if (!ApplicationContext.User.IsInRole(_role))
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
        BusinessRules.AddRule(new IsInRole(AuthorizationActions.ReadProperty, "Manager"));
        BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, "Administrator"));
        BusinessRules.AddRule(new IsInRole(AuthorizationActions.DeleteObject, "Administrator"));
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
        
        // Only managers can read salary
        BusinessRules.AddRule(new IsInRole(AuthorizationActions.ReadProperty, SalaryProperty, "Manager"));
        
        // Only HR can write salary
        BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "HR"));
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

// Command execution
AuthorizationActions.ExecuteMethod
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

Stop rule processing if a condition isn't met:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    // If user can't write, don't run other validation rules
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, NameProperty, "User") { Priority = -1 });
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

Rules that only apply under certain conditions:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    if (IsNew)
    {
        BusinessRules.AddRule(new Required(PasswordProperty));
    }
    
    if (ApplicationContext.User.IsInRole("Administrator"))
    {
        // Admins have additional validation
        BusinessRules.AddRule(new CustomAdminValidation(SomeProperty));
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
