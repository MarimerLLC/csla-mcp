# Business Rules: Lambda Rules

Lambda rules allow you to define simple validation rules inline using lambda expressions, without creating separate rule classes. This is useful for quick, simple validations that don't require reuse across multiple business objects.

## Overview

Lambda rules are implemented using the `Csla.Rules.CommonRules.Lambda` class and the `BusinessRulesExtensions` extension methods. They provide a fluent API for adding validation rules directly in the `AddBusinessRules()` method.

## Extension Method API

The `BusinessRulesExtensions` class provides several overloads of `AddRule<T>()` for creating lambda rules:

### Basic Overloads

| Method Signature | Description |
|-----------------|-------------|
| `AddRule<T>(property, handler, message)` | Simple rule with static message |
| `AddRule<T>(property, handler, message, severity)` | Rule with static message and severity |
| `AddRule<T>(property, handler, messageDelegate)` | Rule with dynamic/localized message |
| `AddRule<T>(property, handler, messageDelegate, severity)` | Rule with dynamic message and severity |
| `AddRule<T>(ruleSet, property, handler, message, severity)` | Rule in a specific rule set |
| `AddRule<T>(ruleSet, property, handler, messageDelegate, severity)` | Rule in a specific rule set with dynamic message |

## Simple Lambda Rules

### Basic Validation with Static Message

The simplest form of lambda rule uses a predicate function that returns `true` if the rule passes, and `false` if it fails:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Rule passes if the predicate returns true
    // If false, the error message is displayed
    BusinessRules.AddRule<Person>(
        NameProperty,
        o => !string.IsNullOrEmpty(o.Name),
        "Name is required.");

    // Numeric validation
    BusinessRules.AddRule<Order>(
        QuantityProperty,
        o => o.Quantity > 0,
        "Quantity must be greater than zero.");

    // String length validation
    BusinessRules.AddRule<Person>(
        EmailProperty,
        o => o.Email.Length <= 100,
        "Email cannot exceed 100 characters.");
}
```

### Message Format Placeholders

You can use `{0}` in the message to insert the property's friendly name:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    BusinessRules.AddRule<Person>(
        AgeProperty,
        o => o.Age >= 18,
        "{0} must be at least 18.");

    BusinessRules.AddRule<Product>(
        PriceProperty,
        o => o.Price > 0,
        "{0} must be greater than zero.");
}
```

### Specifying Rule Severity

By default, lambda rules use `RuleSeverity.Error`. You can specify a different severity:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Error (default) - prevents saving
    BusinessRules.AddRule<Person>(
        NameProperty,
        o => !string.IsNullOrEmpty(o.Name),
        "Name is required.",
        RuleSeverity.Error);

    // Warning - allows saving but shows warning
    BusinessRules.AddRule<Product>(
        PriceProperty,
        o => o.Price >= 10,
        "{0} is unusually low. Please verify.",
        RuleSeverity.Warning);

    // Information - informational message only
    BusinessRules.AddRule<Order>(
        TotalProperty,
        o => o.Total < 1000,
        "Orders over $1000 may require manager approval.",
        RuleSeverity.Information);
}
```

## Lambda Rules with Dynamic Messages

For localized or dynamic messages, use a delegate that returns the message string:

### Localizable Messages

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Using a resource file for localization
    BusinessRules.AddRule<Person>(
        NameProperty,
        o => !string.IsNullOrEmpty(o.Name),
        () => Resources.NameRequired);

    // With severity
    BusinessRules.AddRule<Order>(
        QuantityProperty,
        o => o.Quantity <= o.MaxQuantity,
        () => Resources.QuantityExceedsMax,
        RuleSeverity.Warning);
}
```

### Dynamic Messages with Runtime Values

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Dynamic message based on configuration
    BusinessRules.AddRule<Product>(
        StockProperty,
        o => o.Stock >= MinimumStock,
        () => $"Stock must be at least {MinimumStock}.");
}
```

## Multi-Property Lambda Rules

Lambda rules can access multiple properties of the business object:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Compare two properties
    BusinessRules.AddRule<Order>(
        EndDateProperty,
        o => o.EndDate > o.StartDate,
        "End date must be after start date.");

    // Validate based on related property
    BusinessRules.AddRule<Person>(
        StateProperty,
        o => o.Country != "US" || !string.IsNullOrEmpty(o.State),
        "State is required for US addresses.");

    // Complex multi-property validation
    BusinessRules.AddRule<Order>(
        DiscountProperty,
        o => o.Quantity < 100 || o.Discount <= 0.5m,
        "Discount cannot exceed 50% for bulk orders.",
        RuleSeverity.Warning);
}
```

**Important:** When using multi-property lambda rules, you should also set up dependencies so the rule re-executes when related properties change:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // The rule checks EndDate based on StartDate
    BusinessRules.AddRule<Order>(
        EndDateProperty,
        o => o.EndDate > o.StartDate,
        "End date must be after start date.");

    // Re-run EndDate rules when StartDate changes
    BusinessRules.AddRule(new Dependency(StartDateProperty, EndDateProperty));
}
```

## Using Rule Sets

Lambda rules can be added to specific rule sets:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Default rule set
    BusinessRules.AddRule<Person>(
        NameProperty,
        o => !string.IsNullOrEmpty(o.Name),
        "Name is required.");

    // Custom rule set for special validation
    BusinessRules.AddRule<Person>(
        "strict",
        NameProperty,
        o => o.Name.Length >= 5,
        "Name must be at least 5 characters for strict validation.",
        RuleSeverity.Error);
}
```

## Direct Use of Lambda Class

You can also use the `CommonRules.Lambda` class directly for more control:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Create lambda rule directly with full context access
    BusinessRules.AddRule(new CommonRules.Lambda(
        NameProperty,
        context =>
        {
            var target = (Person)context.Target;
            if (string.IsNullOrEmpty(target.Name))
            {
                context.AddErrorResult("Name is required.");
            }
            else if (target.Name.Length < 2)
            {
                context.AddWarningResult("Name seems too short.");
            }
        }));

    // Object-level lambda rule (no primary property)
    BusinessRules.AddRule(new CommonRules.Lambda(
        context =>
        {
            var target = (Order)context.Target;
            if (target.Items.Count == 0)
            {
                context.AddErrorResult("Order must have at least one item.");
            }
        }));
}
```

## Complete Example

Here's a complete example showing various lambda rule patterns:

```csharp
[Serializable]
public class Order : BusinessBase<Order>
{
    public static readonly PropertyInfo<string> CustomerNameProperty =
        RegisterProperty<string>(nameof(CustomerName));
    public string CustomerName
    {
        get => GetProperty(CustomerNameProperty);
        set => SetProperty(CustomerNameProperty, value);
    }

    public static readonly PropertyInfo<int> QuantityProperty =
        RegisterProperty<int>(nameof(Quantity));
    public int Quantity
    {
        get => GetProperty(QuantityProperty);
        set => SetProperty(QuantityProperty, value);
    }

    public static readonly PropertyInfo<decimal> PriceProperty =
        RegisterProperty<decimal>(nameof(Price));
    public decimal Price
    {
        get => GetProperty(PriceProperty);
        set => SetProperty(PriceProperty, value);
    }

    public static readonly PropertyInfo<decimal> DiscountProperty =
        RegisterProperty<decimal>(nameof(Discount));
    public decimal Discount
    {
        get => GetProperty(DiscountProperty);
        set => SetProperty(DiscountProperty, value);
    }

    public static readonly PropertyInfo<DateTime> OrderDateProperty =
        RegisterProperty<DateTime>(nameof(OrderDate));
    public DateTime OrderDate
    {
        get => GetProperty(OrderDateProperty);
        set => SetProperty(OrderDateProperty, value);
    }

    public static readonly PropertyInfo<DateTime> ShipDateProperty =
        RegisterProperty<DateTime>(nameof(ShipDate));
    public DateTime ShipDate
    {
        get => GetProperty(ShipDateProperty);
        set => SetProperty(ShipDateProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // Simple required field
        BusinessRules.AddRule<Order>(
            CustomerNameProperty,
            o => !string.IsNullOrEmpty(o.CustomerName),
            "{0} is required.");

        // Numeric range
        BusinessRules.AddRule<Order>(
            QuantityProperty,
            o => o.Quantity > 0 && o.Quantity <= 1000,
            "{0} must be between 1 and 1000.");

        // Positive value
        BusinessRules.AddRule<Order>(
            PriceProperty,
            o => o.Price > 0,
            "{0} must be greater than zero.");

        // Discount validation with warning
        BusinessRules.AddRule<Order>(
            DiscountProperty,
            o => o.Discount <= 0.3m,
            "Discounts over 30% require manager approval.",
            RuleSeverity.Warning);

        BusinessRules.AddRule<Order>(
            DiscountProperty,
            o => o.Discount <= 0.5m,
            "{0} cannot exceed 50%.",
            RuleSeverity.Error);

        // Date comparison
        BusinessRules.AddRule<Order>(
            ShipDateProperty,
            o => o.ShipDate >= o.OrderDate,
            "Ship date cannot be before order date.");

        // Dependency for date validation
        BusinessRules.AddRule(new Dependency(OrderDateProperty, ShipDateProperty));

        // Bulk order warning with localized message
        BusinessRules.AddRule<Order>(
            QuantityProperty,
            o => o.Quantity < 100 || o.Discount >= 0.1m,
            () => Resources.BulkOrderDiscountSuggestion,
            RuleSeverity.Information);
    }
}
```

## When to Use Lambda Rules

**Use Lambda Rules When:**
- The validation logic is simple (one or two conditions)
- The rule is specific to one business object and won't be reused
- You want to keep the validation logic visible in `AddBusinessRules()`
- You need a quick inline validation without creating a separate class

**Use Regular Rule Classes When:**
- The validation logic is complex
- The rule will be reused across multiple business objects
- The rule needs to access input properties via `InputProperties`
- The rule needs to set output values or affect other properties
- The rule requires extensive testing in isolation
- The rule is asynchronous (use `BusinessRuleAsync` instead)

## Lambda Rule Internals

Lambda rules work by wrapping your predicate in a `CommonRules.Lambda` instance. The extension methods:

1. Create a `Lambda` rule instance
2. Wrap your predicate in a delegate that accesses the target object
3. Use `BypassPropertyChecks` to read property values without triggering recursive rule checks
4. Add a `RuleResult` to the context if the predicate returns `false`
5. Encode the lambda method signature into the rule URI to ensure uniqueness

This means each lambda rule has a unique rule name based on its method signature, allowing multiple lambda rules on the same property.

## Best Practices

1. **Keep predicates simple** - If your validation logic is complex, create a regular rule class
2. **Return true for success** - The predicate should return `true` when the value is valid
3. **Use dependencies** - Set up `Dependency` rules when lambda rules access multiple properties
4. **Choose appropriate severity** - Use Error for validation that must pass, Warning for suggestions
5. **Consider localization** - Use message delegates for applications requiring localization
6. **Avoid side effects** - Lambda rules should only validate, not modify state
7. **Test the behavior** - Even though lambda rules are inline, test them through the business object

## Related Documentation

- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Creating rule classes for validation
- [BusinessRulesPriority.md](BusinessRulesPriority.md) - Rule priorities and execution order
- [BusinessRulesContext.md](BusinessRulesContext.md) - Understanding the rule context
