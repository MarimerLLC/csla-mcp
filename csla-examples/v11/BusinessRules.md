# Creating Business Rules

CSLA provides a powerful rules engine that validates business logic and enforces authorization. Rules are automatically invoked when properties change, when objects are saved, or when explicitly triggered.

## Ways to Create Rules

There are three primary approaches to creating business rules in CSLA:

1. **Implement `IBusinessRule`** (typically by subclassing `BusinessRule`) - For validation and calculation logic
2. **Implement `IAuthorizationRule`** (typically by subclassing `AuthorizationRule`, or `AuthorizationRuleAsync` for async rules) - For permission-based rules
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
        BusinessRules.AddRule(new RegExMatch(EmailProperty, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));
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
BusinessRules.AddRule(new RegExMatch(PropertyInfo, @"pattern"));

// Numeric validation
BusinessRules.AddRule(new MinValue<int>(PropertyInfo, 0));
BusinessRules.AddRule(new MaxValue<int>(PropertyInfo, 100));
BusinessRules.AddRule(new MinValue<decimal>(PropertyInfo, 0.01m));
BusinessRules.AddRule(new MaxValue<decimal>(PropertyInfo, 999.99m));

// Rule relationships
BusinessRules.AddRule(new Dependency(PropertyInfo, DependentProperty));

// Inline rule
BusinessRules.AddRule(new Lambda(PropertyInfo, context => { /* ... */ }));
```

The `Csla.Rules.CommonRules` namespace contains `Required`, `MaxLength`, `MinLength`, `MinValue<T>`, `MaxValue<T>`, `RegExMatch`, `InfoMessage`, `Lambda`, `Dependency` and `DataAnnotation`, plus the `IsInRole` and `IsNotInRole` authorization rules. There is no built-in email or range rule class; use the `[EmailAddress]` and `[Range]` data annotations (or `RegExMatch`, `MinValue<T>` and `MaxValue<T>`) instead.

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
        InputProperties.Add(primaryProperty);
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
        
        InputProperties.Add(startDateProperty);
        InputProperties.Add(endDateProperty);
        
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
        
        InputProperties.Add(quantityProperty);
        InputProperties.Add(priceProperty);
        AffectedProperties.Add(totalProperty);
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
        InputProperties.Add(primaryProperty);
    }
    
    protected override async Task ExecuteAsync(IRuleContext context)
    {
        var email = (string)context.InputPropertyValues[PrimaryProperty];
        
        if (string.IsNullOrWhiteSpace(email))
            return;

        // Execute a command object to check if email exists
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<EmailExistsCommand>>();
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
        // HasPermission starts out false, so the rule must set it explicitly
        var principal = context.ApplicationContext.Principal;
        context.HasPermission = principal.HasClaim(_claimType, _claimValue);
    }
}

// Usage for object-level authorization
[CslaImplementProperties]
public partial class SensitiveData : BusinessBase<SensitiveData>
{
    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        // Object-level (per-type) rules: at most one rule per action
        BusinessRules.AddRule(typeof(SensitiveData), new HasClaim(AuthorizationActions.GetObject, "Department", "Management"));
        BusinessRules.AddRule(typeof(SensitiveData), new HasClaim(AuthorizationActions.EditObject, ClaimTypes.Role, "Administrator"));
        BusinessRules.AddRule(typeof(SensitiveData), new HasClaim(AuthorizationActions.DeleteObject, ClaimTypes.Role, "Administrator"));
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

### Async Per-Type Authorization Rules and IsSavable

Object-level (per-type) authorization rules can be asynchronous. Derive from `AuthorizationRuleAsync` and override `ExecuteAsync`:

```csharp
using Csla.Rules;

public class CanEditCustomerRule : AuthorizationRuleAsync
{
    public CanEditCustomerRule(AuthorizationActions action)
        : base(action)
    { }

    protected override async Task ExecuteAsync(IAuthorizationContext context, CancellationToken ct)
    {
        // Talk to the server through a command object, never directly to a database
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<UserCanEditCommand>>();
        var userName = context.ApplicationContext.User.Identity?.Name ?? string.Empty;
        var command = await portal.ExecuteAsync(userName);
        context.HasPermission = command.CanEdit;
    }
}

[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(Customer), new CanEditCustomerRule(AuthorizationActions.EditObject));
    }
}
```

Evaluate async rules with `BusinessRules.HasPermissionAsync(...)`. The synchronous `BusinessRules.HasPermission(...)` can only use an async rule whose task has already completed; if the rule is still running, it throws an `InvalidOperationException` telling you to use `HasPermissionAsync`.

In CSLA 11, `IsSavable` fully supports truly async `CreateObject`, `EditObject` and `DeleteObject` rules:

* A rule that completes synchronously (a sync rule, or an async rule that doesn't actually await) is evaluated every time `IsSavable` is read, and is not cached. This is the same as before.
* When the rule is still running, the object is marked busy (`IsBusy` is `true`) and `IsSavable` returns `false`. Reading `IsSavable` can therefore raise `BusyChanged`.
* When the rule completes, the result is cached for that action (new, existing or deleted object) and user. The object is marked idle and a `PropertyChanged` notification is raised for `IsSavable`. The cached result is re-evaluated when the action or the user changes, but not when other properties are edited.
* If the rule throws, `IsSavable` is `false` and the exception is raised through the object's `UnhandledAsyncException` event.
* Calling `SaveAsync` while the check is still running throws, the same as saving while async business rules are running. Wait for the object to become idle first.
* For `BusinessListBase` and `BusinessBindingListBase`, a pending `EditObject` check is included in `IsBusy` and raises `BusyChanged`.

Because the existing busy handling is reused, UI helpers that already wait for idle (the XAML and Blazor view models, `WaitForIdle`) handle these rules without changes.

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
        InputProperties.Add(primaryProperty);
    }
    
    protected override void Execute(IRuleContext context)
    {
        var target = (BusinessBase)context.Target;
        var canWrite = target.CanWriteProperty(PrimaryProperty);
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

Business and validation rules are per-type: CSLA calls `AddBusinessRules()` once for each type (and rule set), caches the rules, and applies them to every instance. The rule objects are shared, so a rule must not keep per-instance state in fields; get everything it needs from the `IRuleContext`.

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial string Name { get; set; }

    // Called once per type; the rules apply to all instances
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Required(NameProperty));
    }

    // Object-level (per-type) authorization rules
    [ObjectAuthorizationRules]
    public static void AddObjectAuthorizationRules()
    {
        BusinessRules.AddRule(typeof(Customer), new IsInRole(AuthorizationActions.EditObject, "Manager"));
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
        InputProperties.Add(primaryProperty);
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
        // InputProperties and AffectedProperties are get-only lists, so copy the items
        InputProperties.AddRange(innerRule.InputProperties);
        foreach (var property in innerRule.AffectedProperties)
            if (!AffectedProperties.Contains(property))
                AffectedProperties.Add(property);
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
        // Assuming IsSensitive is a property on your business class
        var target = (Employee)context.Target!;
        
        // HasPermission starts out false, so always set it.
        // Only Administrators may access sensitive data; anyone may access other data.
        context.HasPermission = !target.IsSensitive
            || context.ApplicationContext.User.IsInRole("Administrator");
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
        
        InputProperties.Add(firstNameProperty);
        InputProperties.Add(lastNameProperty);
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
