# Business Rules Overview

CSLA business rules provide a powerful, flexible system for implementing validation, calculation, and authorization logic in business objects. Rules execute automatically when properties change and can produce error, warning, or information messages.

## Rule Types

CSLA supports several types of rules:

1. **Validation Rules** - Validate property values and add error, warning, or information messages
2. **Calculation Rules** - Calculate and set property values based on other properties
3. **Authorization Rules** - Control read/write access to properties
4. **Async Rules** - Rules that execute asynchronously (e.g., server lookups, API calls)
5. **Lambda Rules** - Inline rules defined using lambda expressions for simple validation logic

## Rule Severity Levels

Rules can produce different severity levels of messages:

- **Error** - Prevents the object from being saved (sets `IsValid` to false)
- **Warning** - Indicates potential issues but doesn't prevent saving
- **Information** - Provides informational feedback to users

## Basic Rule Structure

All business rules inherit from `BusinessRule` or `BusinessRuleAsync` and implement the `Execute` method:

```csharp
public class MyRule : BusinessRule
{
    public MyRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        // Configure input properties if needed
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        // Rule logic here
        // Add results using context.AddErrorResult(), AddWarningResult(), or AddInformationResult()
    }
}
```

## Registering Rules

Rules are registered in the `AddBusinessRules()` method:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules(); // Processes data annotations

    // Add validation rules
    BusinessRules.AddRule(new Required(NameProperty));
    BusinessRules.AddRule(new MaxLength(NameProperty, 50));

    // Add custom rules
    BusinessRules.AddRule(new MyCustomRule(EmailProperty));
}
```

## Key Concepts

### Input Properties

Input properties specify which property values the rule needs to execute:

```csharp
public MyRule(IPropertyInfo primaryProperty, IPropertyInfo secondProperty)
    : base(primaryProperty)
{
    InputProperties.Add(primaryProperty);
    InputProperties.Add(secondProperty);
}
```

### Output Values

Rules can set property values using `context.AddOutValue()`:

```csharp
protected override void Execute(IRuleContext context)
{
    int value1 = (int)context.InputPropertyValues[Property1];
    int value2 = (int)context.InputPropertyValues[Property2];
    int sum = value1 + value2;

    context.AddOutValue(SumProperty, sum);
}
```

### Affected Properties

Affected properties specify which properties should be re-validated when this rule runs:

```csharp
public MyRule(IPropertyInfo primaryProperty)
    : base(primaryProperty)
{
    AffectedProperties.Add(RelatedProperty);
}
```

### Dependencies

Dependencies ensure that when one property changes, rules on dependent properties are re-evaluated:

```csharp
// When Num1 changes, re-check rules on Sum
BusinessRules.AddRule(new Dependency(Num1Property, SumProperty));
```

### Severity Levels

Rules can add results with different severity levels:

```csharp
context.AddErrorResult("This must be fixed");        // Error - prevents save
context.AddWarningResult("This should be checked");  // Warning - allows save
context.AddInformationResult("Just FYI");           // Information - no action needed
```

**Important:** Only **Error** severity stops rule execution (short-circuiting). Warnings and Information do not stop subsequent rules.

## Rule Execution Flow

1. Property value changes
2. Rules attached to that property execute (by priority, lowest first)
3. **If any rule adds an Error, execution stops (short-circuits)**
4. Rules calculate output values
5. Dependent property rules execute
6. Affected property rules execute
7. Results are collected and applied

**Short-Circuiting Example:**

```csharp
// Priority -1: Check if required (runs first)
BusinessRules.AddRule(new Required(EmailProperty) { Priority = -1 });

// Priority 0: Check format (only if Required passed)
BusinessRules.AddRule(new Email(EmailProperty));

// Priority 1: Check uniqueness (only if Email format passed)
BusinessRules.AddRule(new UniqueEmailAsync(EmailProperty) { Priority = 1 });
```

If `EmailProperty` is empty, only the `Required` rule runs. The `Email` and `UniqueEmailAsync` rules don't execute because `Required` returned an Error.

See [BusinessRulesPriority.md](BusinessRulesPriority.md) for detailed information on priorities and short-circuiting.

## Common Rule Patterns

### Simple Validation

```csharp
BusinessRules.AddRule(new Required(NameProperty));
BusinessRules.AddRule(new MaxLength(NameProperty, 50));
BusinessRules.AddRule(new RegEx(EmailProperty, @"^[^@]+@[^@]+\.[^@]+$"));
```

### Multi-Property Validation

```csharp
BusinessRules.AddRule(new LessThan(StartDateProperty, EndDateProperty));
```

### Calculation

```csharp
BusinessRules.AddRule(new CalcSum(TotalProperty, Subtotal Property, TaxProperty));
```

### Conditional Validation

```csharp
BusinessRules.AddRule(new RequiredIf(StateProperty, CountryProperty, "US"));
```

## Related Documentation

For detailed information on specific rule types and patterns, see:

- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Simple and complex validation rules
- [BusinessRulesCalculation.md](BusinessRulesCalculation.md) - Property calculation rules
- [BusinessRulesLambda.md](BusinessRulesLambda.md) - Inline lambda expression rules for simple validation
- [BusinessRulesPriority.md](BusinessRulesPriority.md) - Rule priorities, execution order, and short-circuiting
- [BusinessRulesContext.md](BusinessRulesContext.md) - Rule context, execution flags (IsCheckRulesContext), and context properties
- [BusinessRulesObjectLevel.md](BusinessRulesObjectLevel.md) - Object-level validation and authorization rules
- [BusinessRulesAsync.md](BusinessRulesAsync.md) - Asynchronous rules
- [BusinessRulesAuthorization.md](BusinessRulesAuthorization.md) - Authorization rules
- [BusinessRulesChildChanged.md](BusinessRulesChildChanged.md) - Triggering parent rules from child object changes
- [BusinessRulesParentAccess.md](BusinessRulesParentAccess.md) - Accessing parent object properties from child rules
- [BusinessRulesUnitTesting.md](BusinessRulesUnitTesting.md) - Unit testing rules in isolation with Rocks

## Best Practices

1. **Call base.AddBusinessRules()** - Always call the base method to process data annotations
2. **Use InputProperties** - Declare all properties the rule needs to read
3. **Use Dependencies** - Set up dependencies for calculated properties
4. **Consider priorities** - Use priorities when rule execution order matters (see [BusinessRulesPriority.md](BusinessRulesPriority.md))
5. **Remember short-circuiting** - Error results stop subsequent rules from executing
6. **Keep rules focused** - Each rule should have a single responsibility
7. **Test rules independently** - Rules should be testable in isolation
8. **Use appropriate severity** - Use Error for critical validation, Warning for suggestions, Information for helpful messages
9. **Avoid side effects** - Rules should not modify external state

## Common Built-In Rules

CSLA provides many built-in rules in the `Csla.Rules.CommonRules` namespace:

- `Required` - Property must have a value
- `MaxLength` - String length maximum
- `MinLength` - String length minimum
- `MaxValue<T>` - Maximum numeric value
- `MinValue<T>` - Minimum numeric value
- `RegEx` - Regular expression pattern match
- `Range<T>` - Value must be within a range
- `Dependency` - Establishes property dependencies
- `IsInRole` - Authorization based on user roles
- `IsNotInRole` - Authorization preventing certain roles

## Example Business Object

```csharp
[Serializable]
public class Order : BusinessBase<Order>
{
    public static readonly PropertyInfo<decimal> SubtotalProperty =
        RegisterProperty<decimal>(nameof(Subtotal));
    public decimal Subtotal
    {
        get => GetProperty(SubtotalProperty);
        set => SetProperty(SubtotalProperty, value);
    }

    public static readonly PropertyInfo<decimal> TaxProperty =
        RegisterProperty<decimal>(nameof(Tax));
    public decimal Tax
    {
        get => GetProperty(TaxProperty);
        private set => LoadProperty(TaxProperty, value);
    }

    public static readonly PropertyInfo<decimal> TotalProperty =
        RegisterProperty<decimal>(nameof(Total));
    public decimal Total
    {
        get => GetProperty(TotalProperty);
        private set => LoadProperty(TotalProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // Validation
        BusinessRules.AddRule(new MinValue<decimal>(SubtotalProperty, 0));

        // Dependencies for calculations
        BusinessRules.AddRule(new Dependency(SubtotalProperty, TaxProperty));
        BusinessRules.AddRule(new Dependency(SubtotalProperty, TotalProperty));
        BusinessRules.AddRule(new Dependency(TaxProperty, TotalProperty));

        // Calculation rules with priorities
        BusinessRules.AddRule(new CalcTax(TaxProperty, SubtotalProperty) { Priority = -1 });
        BusinessRules.AddRule(new CalcTotal(TotalProperty, SubtotalProperty, TaxProperty) { Priority = 0 });
    }
}
```

## Notes

- Rules execute automatically when properties change via `SetProperty()`
- Rules do not execute when using `LoadProperty()` (typically used in data portal methods)
- Use `BusinessRules.CheckRules()` (CSLA 9) or `await CheckRulesAsync()` (CSLA 10) to manually trigger rule execution
- Async rules run in the background and complete asynchronously
- Rule results are cached - rules only re-execute when input properties change
