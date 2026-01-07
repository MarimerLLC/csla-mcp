# Business Rules: Validation

Validation rules check property values and produce error, warning, or information messages. This document covers simple single-property validation and complex multi-property validation rules.

## Simple Validation Rules

Simple validation rules check a single property value and produce a message.

### Error-Level Validation

Error-level validation prevents the object from being saved:

```csharp
public class SimpleErrorRule : BusinessRule
{
    public SimpleErrorRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string value = (string)context.InputPropertyValues[PrimaryProperty];

        if (string.IsNullOrWhiteSpace(value))
        {
            context.AddErrorResult("This field is required.");
        }
    }
}
```

**Usage:**
```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    BusinessRules.AddRule(new SimpleErrorRule(NameProperty));
}
```

### Warning-Level Validation

Warning-level validation provides guidance but doesn't prevent saving:

```csharp
public class PasswordStrengthRule : BusinessRule
{
    public PasswordStrengthRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string password = (string)context.InputPropertyValues[PrimaryProperty];

        if (password != null && password.Length < 8)
        {
            context.AddWarningResult("Password should be at least 8 characters for better security.");
        }
    }
}
```

**Usage:**
```csharp
BusinessRules.AddRule(new PasswordStrengthRule(PasswordProperty));
```

### Information-Level Validation

Information-level validation provides helpful feedback:

```csharp
public class CharacterCountRule : BusinessRule
{
    private int _maxLength;

    public CharacterCountRule(IPropertyInfo primaryProperty, int maxLength)
        : base(primaryProperty)
    {
        _maxLength = maxLength;
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string value = (string)context.InputPropertyValues[PrimaryProperty];
        int remaining = _maxLength - (value?.Length ?? 0);

        if (remaining >= 0)
        {
            context.AddInformationResult($"{remaining} characters remaining.");
        }
    }
}
```

**Usage:**
```csharp
BusinessRules.AddRule(new CharacterCountRule(DescriptionProperty, 500));
```

## Complex Multi-Property Validation

Complex validation rules use multiple property values to perform validation.

### Two-Property Comparison

Compare two property values:

```csharp
public class LessThanProperty : BusinessRule
{
    private IPropertyInfo _compareToProperty;

    public LessThanProperty(IPropertyInfo primaryProperty, IPropertyInfo compareToProperty)
        : base(primaryProperty)
    {
        _compareToProperty = compareToProperty;

        // Add both properties as inputs
        InputProperties.Add(primaryProperty);
        InputProperties.Add(compareToProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        int value1 = (int)context.InputPropertyValues[PrimaryProperty];
        int value2 = (int)context.InputPropertyValues[_compareToProperty];

        if (value1 >= value2)
        {
            context.AddErrorResult($"{PrimaryProperty.FriendlyName} must be less than {_compareToProperty.FriendlyName}.");
        }
    }
}
```

**Usage:**
```csharp
// StartDate must be less than EndDate
BusinessRules.AddRule(new LessThanProperty(StartDateProperty, EndDateProperty));

// Num1 must be less than Num2
BusinessRules.AddRule(new LessThanProperty(Num1Property, Num2Property));
```

### Date Range Validation

Validate that dates are in a proper range:

```csharp
public class DateRangeRule : BusinessRule
{
    private IPropertyInfo _startDateProperty;
    private IPropertyInfo _endDateProperty;

    public DateRangeRule(IPropertyInfo startDateProperty, IPropertyInfo endDateProperty)
        : base(startDateProperty)
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
            context.AddErrorResult("Start date must be before or equal to end date.");
        }

        TimeSpan duration = endDate - startDate;
        if (duration.TotalDays > 365)
        {
            context.AddWarningResult("Date range exceeds one year. This may affect performance.");
        }
    }
}
```

**Usage:**
```csharp
BusinessRules.AddRule(new DateRangeRule(StartDateProperty, EndDateProperty));
```

### Conditional Required Field

Require a field only when another field has a specific value:

```csharp
public class StringRequiredIf : BusinessRule
{
    private IPropertyInfo _conditionProperty;
    private object _conditionValue;

    public StringRequiredIf(IPropertyInfo primaryProperty, IPropertyInfo conditionProperty, object conditionValue)
        : base(primaryProperty)
    {
        _conditionProperty = conditionProperty;
        _conditionValue = conditionValue;

        InputProperties.Add(primaryProperty);
        InputProperties.Add(conditionProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        object conditionValue = context.InputPropertyValues[_conditionProperty];

        // Only validate if condition is met
        if (Equals(conditionValue, _conditionValue))
        {
            string value = (string)context.InputPropertyValues[PrimaryProperty];

            if (string.IsNullOrWhiteSpace(value))
            {
                context.AddErrorResult($"{PrimaryProperty.FriendlyName} is required when {_conditionProperty.FriendlyName} is {_conditionValue}.");
            }
        }
    }
}
```

**Usage:**
```csharp
// State is required when Country is "US"
BusinessRules.AddRule(new StringRequiredIf(StateProperty, CountryProperty, "US"));

// AdditionalInfo is required when OrderType is "Custom"
BusinessRules.AddRule(new StringRequiredIf(AdditionalInfoProperty, OrderTypeProperty, "Custom"));
```

### Using Inner Rules

Execute existing rules conditionally:

```csharp
public class StringRequiredIfUS : BusinessRule
{
    private IPropertyInfo _countryProperty;
    private IBusinessRule _innerRule;

    public StringRequiredIfUS(IPropertyInfo primaryProperty, IPropertyInfo countryProperty)
        : base(primaryProperty)
    {
        _countryProperty = countryProperty;
        _innerRule = new Csla.Rules.CommonRules.Required(primaryProperty);

        // Add condition property as input
        InputProperties.Add(countryProperty);

        // Add input properties required by inner rule
        foreach (var inputProp in _innerRule.InputProperties)
        {
            if (!InputProperties.Contains(inputProp))
                InputProperties.Add(inputProp);
        }
    }

    protected override void Execute(IRuleContext context)
    {
        string country = (string)context.InputPropertyValues[_countryProperty];

        if (country == "US")
        {
            // Execute the inner Required rule
            _innerRule.Execute(context.GetChainedContext(_innerRule));
        }
    }
}
```

**Usage:**
```csharp
BusinessRules.AddRule(new StringRequiredIfUS(ZipCodeProperty, CountryProperty));
```

## Multi-Property Error, Warning, and Information

Rules can produce different severity levels for different conditions:

```csharp
public class BudgetValidationRule : BusinessRule
{
    private IPropertyInfo _estimatedCostProperty;
    private IPropertyInfo _actualCostProperty;

    public BudgetValidationRule(IPropertyInfo estimatedCostProperty, IPropertyInfo actualCostProperty)
        : base(estimatedCostProperty)
    {
        _estimatedCostProperty = estimatedCostProperty;
        _actualCostProperty = actualCostProperty;

        InputProperties.Add(estimatedCostProperty);
        InputProperties.Add(actualCostProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal estimated = (decimal)context.InputPropertyValues[_estimatedCostProperty];
        decimal actual = (decimal)context.InputPropertyValues[_actualCostProperty];

        if (actual <= 0)
            return; // No actual cost yet

        decimal variance = actual - estimated;
        decimal percentOver = (variance / estimated) * 100;

        if (percentOver > 50)
        {
            context.AddErrorResult($"Actual cost is {percentOver:F1}% over budget. Manager approval required.");
        }
        else if (percentOver > 20)
        {
            context.AddWarningResult($"Actual cost is {percentOver:F1}% over budget. Consider reviewing.");
        }
        else if (percentOver > 0)
        {
            context.AddInformationResult($"Actual cost is {percentOver:F1}% over budget.");
        }
        else
        {
            context.AddInformationResult($"Project is within budget (under by {Math.Abs(percentOver):F1}%).");
        }
    }
}
```

**Usage:**
```csharp
BusinessRules.AddRule(new BudgetValidationRule(EstimatedCostProperty, ActualCostProperty));
```

## Common Patterns

### Pattern 1: Simple Field Validation

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Required fields
    BusinessRules.AddRule(new Required(NameProperty));
    BusinessRules.AddRule(new Required(EmailProperty));

    // String length
    BusinessRules.AddRule(new MaxLength(NameProperty, 100));
    BusinessRules.AddRule(new MinLength(PasswordProperty, 8));

    // Regex patterns
    BusinessRules.AddRule(new RegEx(EmailProperty, @"^[^@]+@[^@]+\.[^@]+$"));
    BusinessRules.AddRule(new RegEx(PhoneProperty, @"^\d{10}$"));

    // Numeric ranges
    BusinessRules.AddRule(new Range<int>(AgeProperty, 0, 120));
    BusinessRules.AddRule(new MinValue<decimal>(PriceProperty, 0));
}
```

### Pattern 2: Cross-Property Validation

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Date validation
    BusinessRules.AddRule(new LessThanProperty(StartDateProperty, EndDateProperty));

    // Numeric comparison
    BusinessRules.AddRule(new LessThanProperty(MinPriceProperty, MaxPriceProperty));

    // Conditional requirements
    BusinessRules.AddRule(new StringRequiredIf(StateProperty, CountryProperty, "US"));
}
```

### Pattern 3: Complex Business Logic

```csharp
public class OrderValidationRule : BusinessRule
{
    private IPropertyInfo _quantityProperty;
    private IPropertyInfo _priceProperty;
    private IPropertyInfo _discountProperty;

    public OrderValidationRule(IPropertyInfo quantityProperty, IPropertyInfo priceProperty, IPropertyInfo discountProperty)
        : base(quantityProperty)
    {
        _quantityProperty = quantityProperty;
        _priceProperty = priceProperty;
        _discountProperty = discountProperty;

        InputProperties.Add(quantityProperty);
        InputProperties.Add(priceProperty);
        InputProperties.Add(discountProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        int quantity = (int)context.InputPropertyValues[_quantityProperty];
        decimal price = (decimal)context.InputPropertyValues[_priceProperty];
        decimal discount = (decimal)context.InputPropertyValues[_discountProperty];

        decimal total = quantity * price * (1 - discount);

        if (quantity > 100 && discount < 0.10m)
        {
            context.AddWarningResult("Orders over 100 units typically qualify for 10% discount.");
        }

        if (total > 10000 && discount < 0.15m)
        {
            context.AddInformationResult("Large orders may qualify for additional discount.");
        }

        if (quantity < 1)
        {
            context.AddErrorResult("Quantity must be at least 1.");
        }

        if (discount > 0.5m)
        {
            context.AddErrorResult("Discount cannot exceed 50%. Manager override required.");
        }
    }
}
```

## Best Practices

1. **Use appropriate severity** - Errors prevent saving, warnings suggest improvements, information provides feedback
2. **Clear messages** - Provide actionable feedback to users
3. **Declare inputs** - Always add all properties you need to `InputProperties`
4. **Avoid redundancy** - Don't duplicate validation that data annotations already provide
5. **Keep rules focused** - One validation concern per rule
6. **Handle nulls** - Check for null values before using property values
7. **Use friendly names** - `PrimaryProperty.FriendlyName` provides user-friendly property names
8. **Test independently** - Each rule should be testable without creating a full business object

## Notes

- Validation rules should not modify property values (use calculation rules for that)
- Rules execute automatically when properties change via `SetProperty()`
- Multiple rules can execute on the same property
- Rule results are cumulative - all broken rules produce messages
- The object is invalid (`IsValid = false`) if any rule produces an error
