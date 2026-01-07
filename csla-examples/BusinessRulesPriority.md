# Business Rules: Priority and Execution Order

Business rules execute in a specific order determined by their **priority** values and **dependency chains**. Understanding this execution order is critical when rules depend on the results of other rules.

## Priority Values

Each business rule has a `Priority` property that controls when it executes relative to other rules. Lower numbers run first.

### Default Priority

- Default priority is **0**
- Most validation rules should use the default priority
- Calculation rules often need negative priorities to run before validation

### Setting Priority

Set the priority when adding the rule:

```csharp
BusinessRules.AddRule(new CalcSum(SumProperty, Num1Property, Num2Property) { Priority = -1 });
```

## Priority Guidelines

| Priority Range | Typical Use |
|----------------|-------------|
| -10 to -1 | Calculation rules that must run before validation |
| 0 (default) | Most validation rules |
| 1 to 10 | Rules that should run after validation |
| 100+ | Low-priority informational rules |

## Execution Order

Rules execute in this order:

1. **By Priority** - Lower priority values run first
2. **By Registration Order** - Rules with the same priority run in the order they were added
3. **By Dependencies** - When a rule completes, rules for its `AffectedProperties` run

## Short-Circuiting on Error

**CRITICAL:** If a synchronous validation rule returns an **Error** severity result, rule execution stops immediately:

- No subsequent synchronous rules at the same or higher priority execute
- No asynchronous rules are started
- This is called "short-circuiting"

**Warnings and Information messages do NOT stop rule execution** - only Errors cause short-circuiting.

### Short-Circuiting Example

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Priority -1: Runs first
    BusinessRules.AddRule(new Required(NameProperty) { Priority = -1 });

    // Priority 0: Only runs if Required passes
    BusinessRules.AddRule(new MaxLength(NameProperty, 50));

    // Priority 1: Only runs if MaxLength passes
    BusinessRules.AddRule(new UniqueNameAsync(NameProperty) { Priority = 1 });
}
```

**Execution scenarios:**

1. **Name is empty**:
   - `Required` fails with Error → short-circuit
   - `MaxLength` does NOT run
   - `UniqueNameAsync` does NOT run

2. **Name is 100 characters**:
   - `Required` passes
   - `MaxLength` fails with Error → short-circuit
   - `UniqueNameAsync` does NOT run

3. **Name is valid**:
   - `Required` passes
   - `MaxLength` passes
   - `UniqueNameAsync` runs (async)

### Why Short-Circuiting Matters

Short-circuiting prevents:
- Wasting resources on unnecessary validation (e.g., async database checks)
- Confusing error messages (e.g., "Name is required" AND "Name is too long")
- Exceptions from rules that expect valid data

### Using Priority to Control Short-Circuiting

Place critical validation rules at lower priorities to ensure they run first:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Priority -10: Most critical - check for required value
    BusinessRules.AddRule(new Required(EmailProperty) { Priority = -10 });

    // Priority -5: Check format if value exists
    BusinessRules.AddRule(new Email(EmailProperty) { Priority = -5 });

    // Priority 0: Check uniqueness if format is valid (async)
    BusinessRules.AddRule(new UniqueEmailAsync(EmailProperty));
}
```

This ensures:
1. Don't check email format if email is missing
2. Don't check uniqueness in database if email format is invalid

## Priority Example: Calculation Before Validation

A calculation rule must run before a validation rule that checks the calculated value:

```csharp
public class Order : BusinessBase<Order>
{
    public static readonly PropertyInfo<int> Num1Property =
        RegisterProperty<int>(nameof(Num1));
    public int Num1
    {
        get => GetProperty(Num1Property);
        set => SetProperty(Num1Property, value);
    }

    public static readonly PropertyInfo<int> Num2Property =
        RegisterProperty<int>(nameof(Num2));
    public int Num2
    {
        get => GetProperty(Num2Property);
        set => SetProperty(Num2Property, value);
    }

    public static readonly PropertyInfo<int> SumProperty =
        RegisterProperty<int>(nameof(Sum));
    public int Sum
    {
        get => GetProperty(SumProperty);
        private set => LoadProperty(SumProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();

        // Dependencies to trigger Sum recalculation
        BusinessRules.AddRule(new Dependency(Num1Property, SumProperty));
        BusinessRules.AddRule(new Dependency(Num2Property, SumProperty));

        // Priority -1: Calculate sum FIRST
        BusinessRules.AddRule(new CalcSum(SumProperty, Num1Property, Num2Property) { Priority = -1 });

        // Priority 0 (default): Validate sum SECOND
        BusinessRules.AddRule(new MinValue<int>(SumProperty, 1));
    }
}
```

**Why this matters:**
- When `Num1` or `Num2` changes, `CalcSum` runs first (priority -1) to update `Sum`
- Then `MinValue` rule runs (priority 0) to validate the new `Sum` value
- Without the priority, validation might run on the old `Sum` value

## Cascading Calculations

When multiple calculations depend on each other, use priorities to control the cascade order:

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

        // Dependencies - define what triggers what
        BusinessRules.AddRule(new Dependency(SubtotalProperty, DiscountAmountProperty));
        BusinessRules.AddRule(new Dependency(DiscountProperty, DiscountAmountProperty));
        BusinessRules.AddRule(new Dependency(DiscountAmountProperty, TaxableAmountProperty));
        BusinessRules.AddRule(new Dependency(TaxableAmountProperty, TaxProperty));
        BusinessRules.AddRule(new Dependency(TaxProperty, TotalProperty));

        // Priority -4: Calculate discount amount FIRST
        BusinessRules.AddRule(new CalcDiscountAmount(
            DiscountAmountProperty, SubtotalProperty, DiscountProperty) { Priority = -4 });

        // Priority -3: Calculate taxable amount SECOND
        BusinessRules.AddRule(new CalcTaxableAmount(
            TaxableAmountProperty, SubtotalProperty, DiscountAmountProperty) { Priority = -3 });

        // Priority -2: Calculate tax THIRD
        BusinessRules.AddRule(new CalcTax(
            TaxProperty, TaxableAmountProperty, 0.08m) { Priority = -2 });

        // Priority -1: Calculate total FOURTH
        BusinessRules.AddRule(new CalcTotal(
            TotalProperty, TaxableAmountProperty, TaxProperty) { Priority = -1 });

        // Priority 0 (default): Validate total LAST
        BusinessRules.AddRule(new MinValue<decimal>(TotalProperty, 0.01m));
    }
}
```

**Execution Flow:**
1. User changes `Subtotal` to 100
2. Priority -4: `CalcDiscountAmount` runs → `DiscountAmount` = 10
3. Priority -3: `CalcTaxableAmount` runs → `TaxableAmount` = 90
4. Priority -2: `CalcTax` runs → `Tax` = 7.20
5. Priority -1: `CalcTotal` runs → `Total` = 97.20
6. Priority 0: `MinValue` validation runs on `Total`

## Rule Chaining via AffectedProperties

Rules can trigger other rules through the `AffectedProperties` list. When a rule completes, CSLA automatically runs rules for all affected properties.

### Simple Chain

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

        // This rule affects FullName
        AffectedProperties.Add(fullNameProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        string firstName = (string)context.InputPropertyValues[_firstNameProperty];
        string lastName = (string)context.InputPropertyValues[_lastNameProperty];

        context.AddOutValue(PrimaryProperty, $"{firstName} {lastName}".Trim());
    }
}

// In AddBusinessRules
BusinessRules.AddRule(new Dependency(FirstNameProperty, FullNameProperty));
BusinessRules.AddRule(new Dependency(LastNameProperty, FullNameProperty));
BusinessRules.AddRule(new FullNameRule(FullNameProperty, FirstNameProperty, LastNameProperty));

// Any validation rules on FullName will run AFTER FullNameRule completes
BusinessRules.AddRule(new MaxLength(FullNameProperty, 50));
```

**Rule Chain:**
1. User changes `FirstName`
2. `Dependency` triggers rules for `FullNameProperty`
3. `FullNameRule` runs and updates `FullName` via `AddOutValue`
4. Rules for affected properties run (like `MaxLength` on `FullNameProperty`)

### Multi-Level Chain

Rules can cascade through multiple levels:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Level 1: FirstName/LastName → FullName
    BusinessRules.AddRule(new Dependency(FirstNameProperty, FullNameProperty));
    BusinessRules.AddRule(new Dependency(LastNameProperty, FullNameProperty));
    BusinessRules.AddRule(new FullNameRule(FullNameProperty, FirstNameProperty, LastNameProperty)
        { Priority = -2 });

    // Level 2: FullName → DisplayName
    BusinessRules.AddRule(new Dependency(FullNameProperty, DisplayNameProperty));
    BusinessRules.AddRule(new FormatDisplayName(DisplayNameProperty, FullNameProperty, TitleProperty)
        { Priority = -1 });

    // Level 3: DisplayName validation
    BusinessRules.AddRule(new MaxLength(DisplayNameProperty, 100));
}
```

**Execution Flow:**
1. User changes `FirstName`
2. Priority -2: `FullNameRule` runs → updates `FullName`
3. Priority -1: `FormatDisplayName` runs → updates `DisplayName`
4. Priority 0: `MaxLength` validation runs on `DisplayName`

## Preventing Infinite Loops

Be careful not to create circular dependencies where Rule A affects Property B, and a rule on Property B affects Property A.

### Problem: Infinite Loop

```csharp
// DON'T DO THIS
BusinessRules.AddRule(new Dependency(Property1, Property2));
BusinessRules.AddRule(new Rule1(Property2, Property1)); // Sets Property1

BusinessRules.AddRule(new Dependency(Property2, Property1));
BusinessRules.AddRule(new Rule2(Property1, Property2)); // Sets Property2
// This creates an infinite loop!
```

### Solution: One-Way Dependencies

```csharp
// DO THIS
BusinessRules.AddRule(new Dependency(Property1, Property2));
BusinessRules.AddRule(new Rule1(Property2, Property1)); // Sets Property2 based on Property1

// Don't add a reverse dependency
```

## CascadeIfDirty Option

By default, rules cascade whenever the primary property changes. Use `CascadeIfDirty` to only cascade when the property is truly dirty:

```csharp
public class MyRule : BusinessRule
{
    public MyRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        // Only cascade if primary property is marked as dirty
        CascadeIfDirty = true;
    }
}
```

This prevents unnecessary rule execution during object initialization or when loading from the database.

## RunMode: Controlling When Rules Execute

The `RunMode` property controls when a rule is allowed to execute. This is useful for rules that should only run in specific contexts.

### RunMode Values

```csharp
public enum RunModes
{
    Default = 0,                    // Rule can run in any context
    DenyCheckRules = 1,             // Don't run during CheckRules
    DenyAsAffectedProperty = 2,     // Don't run as affected property
    DenyOnServerSidePortal = 4      // Don't run on server-side data portal
}
```

### Example: Client-Side Only Rule

A rule that calls a web service shouldn't run on the server:

```csharp
public class ValidateWithWebService : BusinessRuleAsync
{
    public ValidateWithWebService(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        IsAsync = true;

        // Don't run this rule on the server-side data portal
        RunMode = RunModes.DenyOnServerSidePortal;
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        // Call external web service
        var value = (string)context.InputPropertyValues[PrimaryProperty];
        var isValid = await _webService.ValidateAsync(value);

        if (!isValid)
            context.AddErrorResult("Value is not valid according to web service.");
    }
}
```

### Example: Manual-Only Rule

A rule that should only run when explicitly called, not during normal property changes:

```csharp
public class ComplexValidation : BusinessRule
{
    public ComplexValidation(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        // Only run when CheckRules is explicitly called
        RunMode = RunModes.DenyAsAffectedProperty;
    }
}
```

### Combining RunModes

Use the `|` operator to combine multiple RunMode values:

```csharp
// Don't run during CheckRules OR as affected property
RunMode = RunModes.DenyCheckRules | RunModes.DenyAsAffectedProperty;
```

## Best Practices

1. **Use negative priorities for calculations** - Calculations should typically run before validations
2. **Space priorities apart** - Use -10, -9, -8 instead of -3, -2, -1 to leave room for future rules
3. **Document priority decisions** - Add comments explaining why a rule has a specific priority
4. **Avoid deep cascades** - Long chains of rules are hard to debug and maintain
5. **Test execution order** - Unit test that rules run in the expected order
6. **Use Dependency rules** - Always add `Dependency` rules to trigger recalculation
7. **Watch for infinite loops** - Carefully review AffectedProperties to avoid circular dependencies
8. **Set CascadeIfDirty when appropriate** - Prevents unnecessary rule execution during initialization
9. **Use RunMode selectively** - Most rules should use the default RunMode

## Debugging Rule Execution

To see the order rules execute:

```csharp
// In CSLA 9
BusinessRules.RunRulesComplete += (s, e) =>
{
    Console.WriteLine($"Rules completed for {e.Property?.Name}");
};

// In CSLA 10
BusinessRules.RunRulesComplete += async (s, e) =>
{
    Console.WriteLine($"Rules completed for {e.Property?.Name}");
    await Task.CompletedTask;
};
```

## Common Patterns

### Pattern 1: Calculation → Validation

```csharp
// Priority -1: Calculate
BusinessRules.AddRule(new CalcTax(TaxProperty, SubtotalProperty, 0.08m) { Priority = -1 });

// Priority 0: Validate
BusinessRules.AddRule(new MinValue<decimal>(TaxProperty, 0));
```

### Pattern 2: Multiple Calculations in Sequence

```csharp
BusinessRules.AddRule(new CalcStep1(...) { Priority = -5 });
BusinessRules.AddRule(new CalcStep2(...) { Priority = -4 });
BusinessRules.AddRule(new CalcStep3(...) { Priority = -3 });
```

### Pattern 3: Validation → Authorization

```csharp
// Priority 0: Validate data
BusinessRules.AddRule(new Required(NameProperty));

// Priority 1: Check authorization after data is valid
BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, NameProperty, "Admin")
    { Priority = 1 });
```

## Notes

- Rules with the same priority execute in registration order
- Priority is per-property, not global - two rules on different properties can have the same priority
- `Dependency` rules don't have priorities - they just trigger other rules
- Async rules (see [BusinessRulesAsync.md](BusinessRulesAsync.md)) run after all synchronous rules at their priority level
- Authorization rules (see [BusinessRulesAuthorization.md](BusinessRulesAuthorization.md)) typically use default priority

## See Also

- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Validation rules
- [BusinessRulesCalculation.md](BusinessRulesCalculation.md) - Calculation rules with cascading examples
- [BusinessRulesAsync.md](BusinessRulesAsync.md) - Asynchronous business rules
- [BusinessRulesAuthorization.md](BusinessRulesAuthorization.md) - Authorization rules
