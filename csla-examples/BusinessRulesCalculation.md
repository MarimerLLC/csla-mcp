# Business Rules: Calculation

Calculation rules compute and set property values based on other properties. These rules use `context.AddOutValue()` to modify property values.

## Simple Property Calculation

Calculate a single property from one input:

```csharp
public class CalculateTax : BusinessRule
{
    private decimal _taxRate;

    public CalculateTax(IPropertyInfo taxProperty, IPropertyInfo subtotalProperty, decimal taxRate)
        : base(taxProperty)
    {
        _taxRate = taxRate;

        InputProperties.Add(subtotalProperty);
        AffectedProperties.Add(taxProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal subtotal = (decimal)context.InputPropertyValues[InputProperties[0]];
        decimal tax = subtotal * _taxRate;

        context.AddOutValue(PrimaryProperty, tax);
    }
}
```

**Usage:**
```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // When Subtotal changes, recalculate Tax
    BusinessRules.AddRule(new Dependency(SubtotalProperty, TaxProperty));
    BusinessRules.AddRule(new CalculateTax(TaxProperty, SubtotalProperty, 0.08m));
}
```

## Multi-Property Calculation

Calculate a property from multiple inputs:

```csharp
public class CalcSum : BusinessRule
{
    public CalcSum(IPropertyInfo sumProperty, params IPropertyInfo[] inputProperties)
        : base(sumProperty)
    {
        InputProperties.AddRange(inputProperties);
        AffectedProperties.Add(sumProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        // Sum all input property values
        decimal sum = context.InputPropertyValues.Sum(kvp => Convert.ToDecimal(kvp.Value));

        context.AddOutValue(PrimaryProperty, sum);
    }
}
```

**Usage:**
```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Set up dependencies so Sum recalculates when Num1 or Num2 changes
    BusinessRules.AddRule(new Dependency(Num1Property, SumProperty));
    BusinessRules.AddRule(new Dependency(Num2Property, SumProperty));

    // Add calculation rule
    BusinessRules.AddRule(new CalcSum(SumProperty, Num1Property, Num2Property));
}
```

## Complex Calculation Rules

### Order Total Calculation

Calculate total from subtotal, tax, and shipping:

```csharp
public class CalculateOrderTotal : BusinessRule
{
    private IPropertyInfo _subtotalProperty;
    private IPropertyInfo _taxProperty;
    private IPropertyInfo _shippingProperty;

    public CalculateOrderTotal(
        IPropertyInfo totalProperty,
        IPropertyInfo subtotalProperty,
        IPropertyInfo taxProperty,
        IPropertyInfo shippingProperty)
        : base(totalProperty)
    {
        _subtotalProperty = subtotalProperty;
        _taxProperty = taxProperty;
        _shippingProperty = shippingProperty;

        InputProperties.Add(subtotalProperty);
        InputProperties.Add(taxProperty);
        InputProperties.Add(shippingProperty);
        AffectedProperties.Add(totalProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal subtotal = (decimal)context.InputPropertyValues[_subtotalProperty];
        decimal tax = (decimal)context.InputPropertyValues[_taxProperty];
        decimal shipping = (decimal)context.InputPropertyValues[_shippingProperty];

        decimal total = subtotal + tax + shipping;

        context.AddOutValue(PrimaryProperty, total);
    }
}
```

**Usage:**
```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Dependencies
    BusinessRules.AddRule(new Dependency(SubtotalProperty, TotalProperty));
    BusinessRules.AddRule(new Dependency(TaxProperty, TotalProperty));
    BusinessRules.AddRule(new Dependency(ShippingProperty, TotalProperty));

    // Calculation
    BusinessRules.AddRule(new CalculateOrderTotal(
        TotalProperty, SubtotalProperty, TaxProperty, ShippingProperty));
}
```

## Modifying Multiple Properties

A single rule can modify multiple properties:

```csharp
public class CalculateTaxAndTotal : BusinessRule
{
    private IPropertyInfo _taxProperty;
    private IPropertyInfo _totalProperty;
    private decimal _taxRate;

    public CalculateTaxAndTotal(
        IPropertyInfo subtotalProperty,
        IPropertyInfo taxProperty,
        IPropertyInfo totalProperty,
        decimal taxRate)
        : base(subtotalProperty)
    {
        _taxProperty = taxProperty;
        _totalProperty = totalProperty;
        _taxRate = taxRate;

        InputProperties.Add(subtotalProperty);
        AffectedProperties.Add(taxProperty);
        AffectedProperties.Add(totalProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal subtotal = (decimal)context.InputPropertyValues[PrimaryProperty];

        // Calculate tax
        decimal tax = subtotal * _taxRate;

        // Calculate total
        decimal total = subtotal + tax;

        // Set both output values
        context.AddOutValue(_taxProperty, tax);
        context.AddOutValue(_totalProperty, total);
    }
}
```

**Usage:**
```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    BusinessRules.AddRule(new Dependency(SubtotalProperty, TaxProperty));
    BusinessRules.AddRule(new Dependency(SubtotalProperty, TotalProperty));

    BusinessRules.AddRule(new CalculateTaxAndTotal(
        SubtotalProperty, TaxProperty, TotalProperty, 0.08m));
}
```

## Lookup-Based Calculation

Set a property based on looking up another property:

```csharp
public class SetStateName : BusinessRule
{
    private IPropertyInfo _stateNameProperty;
    private IDataPortal<StatesLookup> _statesPortal;

    public SetStateName(
        IPropertyInfo stateCodeProperty,
        IPropertyInfo stateNameProperty,
        IDataPortal<StatesLookup> statesPortal)
        : base(stateCodeProperty)
    {
        _stateNameProperty = stateNameProperty;
        _statesPortal = statesPortal;

        InputProperties.Add(stateCodeProperty);
        AffectedProperties.Add(stateNameProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string stateCode = (string)context.InputPropertyValues[PrimaryProperty];

        if (string.IsNullOrWhiteSpace(stateCode))
        {
            context.AddOutValue(_stateNameProperty, string.Empty);
            return;
        }

        // Look up state name
        var lookup = _statesPortal.Fetch();
        var state = lookup.FirstOrDefault(s => s.Code == stateCode);

        string stateName = state?.Name ?? "Unknown";

        context.AddOutValue(_stateNameProperty, stateName);
    }
}
```

**Usage:**
```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    BusinessRules.AddRule(new Dependency(StateCodeProperty, StateNameProperty));

    var statesPortal = ApplicationContext.GetRequiredService<IDataPortal<StatesLookup>>();
    BusinessRules.AddRule(new SetStateName(StateCodeProperty, StateNameProperty, statesPortal));
}
```

## Cascading Calculations

Multiple rules that depend on each other need proper priority ordering:

```csharp
public class Order : BusinessBase<Order>
{
    public static readonly PropertyInfo<decimal> SubtotalProperty =
        RegisterProperty<decimal>(nameof(Subtotal));
    public decimal Subtotal
    {
        get => GetProperty(SubtotalProperty);
        set => SetProperty(SubtotalProperty, value);
    }

    public static readonly PropertyInfo<decimal> DiscountProperty =
        RegisterProperty<decimal>(nameof(Discount));
    public decimal Discount
    {
        get => GetProperty(DiscountProperty);
        set => SetProperty(DiscountProperty, value);
    }

    public static readonly PropertyInfo<decimal> DiscountAmountProperty =
        RegisterProperty<decimal>(nameof(DiscountAmount));
    public decimal DiscountAmount
    {
        get => GetProperty(DiscountAmountProperty);
        private set => LoadProperty(DiscountAmountProperty, value);
    }

    public static readonly PropertyInfo<decimal> TaxableAmountProperty =
        RegisterProperty<decimal>(nameof(TaxableAmount));
    public decimal TaxableAmount
    {
        get => GetProperty(TaxableAmountProperty);
        private set => LoadProperty(TaxableAmountProperty, value);
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

        // Dependencies
        BusinessRules.AddRule(new Dependency(SubtotalProperty, DiscountAmountProperty));
        BusinessRules.AddRule(new Dependency(DiscountProperty, DiscountAmountProperty));
        BusinessRules.AddRule(new Dependency(DiscountAmountProperty, TaxableAmountProperty));
        BusinessRules.AddRule(new Dependency(TaxableAmountProperty, TaxProperty));
        BusinessRules.AddRule(new Dependency(TaxProperty, TotalProperty));

        // Calculations with priorities to ensure correct order
        // Priority -3: Calculate discount amount first
        BusinessRules.AddRule(new CalcDiscountAmount(
            DiscountAmountProperty, SubtotalProperty, DiscountProperty) { Priority = -3 });

        // Priority -2: Calculate taxable amount second
        BusinessRules.AddRule(new CalcTaxableAmount(
            TaxableAmountProperty, SubtotalProperty, DiscountAmountProperty) { Priority = -2 });

        // Priority -1: Calculate tax third
        BusinessRules.AddRule(new CalcTax(
            TaxProperty, TaxableAmountProperty, 0.08m) { Priority = -1 });

        // Priority 0: Calculate total last (default priority)
        BusinessRules.AddRule(new CalcTotal(
            TotalProperty, TaxableAmountProperty, TaxProperty) { Priority = 0 });
    }
}
```

## Common Patterns

### Pattern 1: Simple Derived Value

```csharp
public class FullNameRule : BusinessRule
{
    private IPropertyInfo _firstNameProperty;
    private IPropertyInfo _lastNameProperty;

    public FullNameRule(
        IPropertyInfo fullNameProperty,
        IPropertyInfo firstNameProperty,
        IPropertyInfo lastNameProperty)
        : base(fullNameProperty)
    {
        _firstNameProperty = firstNameProperty;
        _lastNameProperty = lastNameProperty;

        InputProperties.Add(firstNameProperty);
        InputProperties.Add(lastNameProperty);
        AffectedProperties.Add(fullNameProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string firstName = (string)context.InputPropertyValues[_firstNameProperty];
        string lastName = (string)context.InputPropertyValues[_lastNameProperty];

        string fullName = $"{firstName} {lastName}".Trim();

        context.AddOutValue(PrimaryProperty, fullName);
    }
}
```

### Pattern 2: Percentage Calculation

```csharp
public class CalcPercentage : BusinessRule
{
    private IPropertyInfo _totalProperty;

    public CalcPercentage(
        IPropertyInfo percentageProperty,
        IPropertyInfo partProperty,
        IPropertyInfo totalProperty)
        : base(percentageProperty)
    {
        _totalProperty = totalProperty;

        InputProperties.Add(partProperty);
        InputProperties.Add(totalProperty);
        AffectedProperties.Add(percentageProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal part = (decimal)context.InputPropertyValues[InputProperties[0]];
        decimal total = (decimal)context.InputPropertyValues[_totalProperty];

        decimal percentage = total > 0 ? (part / total) * 100 : 0;

        context.AddOutValue(PrimaryProperty, percentage);
    }
}
```

### Pattern 3: Conditional Calculation

```csharp
public class CalcShipping : BusinessRule
{
    private IPropertyInfo _subtotalProperty;
    private IPropertyInfo _isPrimeProperty;

    public CalcShipping(
        IPropertyInfo shippingProperty,
        IPropertyInfo subtotalProperty,
        IPropertyInfo isPrimeProperty)
        : base(shippingProperty)
    {
        _subtotalProperty = subtotalProperty;
        _isPrimeProperty = isPrimeProperty;

        InputProperties.Add(subtotalProperty);
        InputProperties.Add(isPrimeProperty);
        AffectedProperties.Add(shippingProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        decimal subtotal = (decimal)context.InputPropertyValues[_subtotalProperty];
        bool isPrime = (bool)context.InputPropertyValues[_isPrimeProperty];

        decimal shipping;

        if (isPrime)
        {
            shipping = 0; // Free shipping for Prime members
        }
        else if (subtotal >= 50)
        {
            shipping = 0; // Free shipping over $50
        }
        else
        {
            shipping = 7.99m; // Standard shipping
        }

        context.AddOutValue(PrimaryProperty, shipping);
    }
}
```

## Best Practices

1. **Use AddOutValue** - Always use `context.AddOutValue()` to modify properties, never call `SetProperty()` in a rule
2. **Declare affected properties** - Add calculated properties to `AffectedProperties`
3. **Set up dependencies** - Use `Dependency` rules to trigger recalculation when inputs change
4. **Use priorities** - When calculations depend on other calculations, use priorities to control execution order
5. **Handle division by zero** - Check for zero before dividing
6. **Keep calculations pure** - Don't access external state; only use input property values
7. **Make properties read-only** - Calculated properties should typically have private setters
8. **Test independently** - Calculation logic should be testable without a full business object

## Notes

- Calculation rules execute automatically when input properties change
- Use negative priorities for calculations that must run before validation rules
- Multiple properties can be calculated in a single rule using multiple `AddOutValue()` calls
- The calculated property should use `LoadProperty()` in its setter, not `SetProperty()`
- Calculation rules should not produce error/warning/information messages (use validation rules for that)

See [BusinessRulesPriority.md](BusinessRulesPriority.md) for more information on controlling rule execution order with priorities.
