# Unit Testing Business Rules with Rocks

Unit testing CSLA business rules in isolation allows you to verify rule behavior without the complexity of full business objects. The [Rocks](https://github.com/JasonBock/Rocks) mocking framework enables this by creating mock objects for `IPropertyInfo` and `IRuleContext` interfaces.

> ℹ️ You can use any mocking framework that provides comparable features and capabilities. This document uses the Rocks mocking framework as an example.

## Why Unit Test Rules in Isolation?

CSLA business rules encapsulate validation, authorization, calculation, and other business logic. Testing rules in isolation provides:

- **Focused testing** - Test only the rule logic, not the entire business object
- **Faster execution** - No need to create full object graphs
- **Easier setup** - Mock only the dependencies the rule needs
- **Better coverage** - Test edge cases that are hard to reach through the business object

## Setting Up Rocks

### Install the NuGet Package

```bash
dotnet add package Rocks
```

### Configure Assembly-Level Attributes

Add assembly-level attributes to generate mocks for the CSLA interfaces your rules use:

```csharp
using Csla.Core;
using Csla.Rules;
using Rocks;

[assembly: Rock(typeof(IPropertyInfo), BuildType.Create | BuildType.Make)]
[assembly: Rock(typeof(IRuleContext), BuildType.Create | BuildType.Make)]
```

- `BuildType.Create` - Generates expectation-based mocks (verify specific calls)
- `BuildType.Make` - Generates simple mocks (no verification needed)

## Example: Testing a Calculation Rule

Consider a rule that determines if a customer is active based on their last order date:

### The Rule Implementation

```csharp
using Csla.Core;
using Csla.Rules;

public class LastOrderDateRule : BusinessRule
{
    public LastOrderDateRule(IPropertyInfo lastOrderDateProperty, IPropertyInfo isActiveProperty)
        : base(lastOrderDateProperty)
    {
        InputProperties.Add(lastOrderDateProperty);
        AffectedProperties.Add(isActiveProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var lastOrderDate = (DateTime)context.InputPropertyValues[PrimaryProperty];
        var isActive = lastOrderDate > DateTime.Now.AddYears(-1);
        context.AddOutValue(AffectedProperties[1], isActive);
    }
}
```

### The Unit Test

```csharp
using Csla.Core;
using Csla.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rocks;

[TestClass]
public class LastOrderDateRuleTests
{
    [TestMethod]
    public void Execute_RecentOrder_SetsIsActiveTrue()
    {
        // Arrange
        var inputProperties = new Dictionary<IPropertyInfo, object>();
        using var context = new RockContext();

        // Create mock for LastOrderDate property with expectations
        var lastOrderPropertyExpectations = context.Create<IPropertyInfoCreateExpectations>();
        lastOrderPropertyExpectations.Properties.Getters.Name()
            .ReturnValue("LastOrderDate")
            .ExpectedCallCount(2);
        var lastOrderProperty = lastOrderPropertyExpectations.Instance();

        // Create simple mock for IsActive property (no verification needed)
        var isActiveProperty = new IPropertyInfoMakeExpectations().Instance();

        // Create mock for rule context
        var ruleContextExpectations = context.Create<IRuleContextCreateExpectations>();
        ruleContextExpectations.Properties.Getters.InputPropertyValues()
            .ReturnValue(inputProperties);
        ruleContextExpectations.Methods.AddOutValue(
            Arg.Is(isActiveProperty), true);

        // Set up input property value (order within past year)
        inputProperties.Add(lastOrderProperty, DateTime.Now.AddMonths(-6));

        // Act
        var rule = new LastOrderDateRule(lastOrderProperty, isActiveProperty);
        ((IBusinessRule)rule).Execute(ruleContextExpectations.Instance());

        // Assert - Rocks verifies expectations automatically when context is disposed
    }

    [TestMethod]
    public void Execute_OldOrder_SetsIsActiveFalse()
    {
        // Arrange
        var inputProperties = new Dictionary<IPropertyInfo, object>();
        using var context = new RockContext();

        var lastOrderPropertyExpectations = context.Create<IPropertyInfoCreateExpectations>();
        lastOrderPropertyExpectations.Properties.Getters.Name()
            .ReturnValue("LastOrderDate")
            .ExpectedCallCount(2);
        var lastOrderProperty = lastOrderPropertyExpectations.Instance();

        var isActiveProperty = new IPropertyInfoMakeExpectations().Instance();

        var ruleContextExpectations = context.Create<IRuleContextCreateExpectations>();
        ruleContextExpectations.Properties.Getters.InputPropertyValues()
            .ReturnValue(inputProperties);
        ruleContextExpectations.Methods.AddOutValue(
            Arg.Is(isActiveProperty), false);  // Expect false for old order

        // Set up input property value (order older than one year)
        inputProperties.Add(lastOrderProperty, DateTime.Now.AddYears(-2));

        // Act
        var rule = new LastOrderDateRule(lastOrderProperty, isActiveProperty);
        ((IBusinessRule)rule).Execute(ruleContextExpectations.Instance());

        // Assert - verification happens on dispose
    }
}
```

## Testing Validation Rules

For validation rules that add error results, set up expectations on `AddErrorResult`:

### The Rule

```csharp
public class ValidEmailDomain : BusinessRule
{
    private readonly string[] _allowedDomains;

    public ValidEmailDomain(IPropertyInfo emailProperty, params string[] allowedDomains)
        : base(emailProperty)
    {
        _allowedDomains = allowedDomains;
        InputProperties.Add(emailProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var email = (string)context.InputPropertyValues[PrimaryProperty];

        if (string.IsNullOrEmpty(email))
            return;

        var domain = email.Split('@').LastOrDefault();
        if (domain == null || !_allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            context.AddErrorResult($"Email must be from: {string.Join(", ", _allowedDomains)}");
        }
    }
}
```

### The Test

```csharp
[TestMethod]
public void Execute_InvalidDomain_AddsError()
{
    // Arrange
    var inputProperties = new Dictionary<IPropertyInfo, object>();
    using var context = new RockContext();

    var emailPropertyExpectations = context.Create<IPropertyInfoCreateExpectations>();
    emailPropertyExpectations.Properties.Getters.Name()
        .ReturnValue("Email")
        .ExpectedCallCount(2);
    var emailProperty = emailPropertyExpectations.Instance();

    var ruleContextExpectations = context.Create<IRuleContextCreateExpectations>();
    ruleContextExpectations.Properties.Getters.InputPropertyValues()
        .ReturnValue(inputProperties);

    // Expect an error result to be added
    ruleContextExpectations.Methods.AddErrorResult(
        Arg.Is<string>(s => s.Contains("company.com")));

    inputProperties.Add(emailProperty, "user@invalid.com");

    // Act
    var rule = new ValidEmailDomain(emailProperty, "company.com", "partner.com");
    ((IBusinessRule)rule).Execute(ruleContextExpectations.Instance());

    // Assert - Rocks verifies AddErrorResult was called
}

[TestMethod]
public void Execute_ValidDomain_NoError()
{
    // Arrange
    var inputProperties = new Dictionary<IPropertyInfo, object>();
    using var context = new RockContext();

    var emailPropertyExpectations = context.Create<IPropertyInfoCreateExpectations>();
    emailPropertyExpectations.Properties.Getters.Name()
        .ReturnValue("Email")
        .ExpectedCallCount(2);
    var emailProperty = emailPropertyExpectations.Instance();

    var ruleContextExpectations = context.Create<IRuleContextCreateExpectations>();
    ruleContextExpectations.Properties.Getters.InputPropertyValues()
        .ReturnValue(inputProperties);

    // Note: No expectation for AddErrorResult - if it's called, test will fail

    inputProperties.Add(emailProperty, "user@company.com");

    // Act
    var rule = new ValidEmailDomain(emailProperty, "company.com", "partner.com");
    ((IBusinessRule)rule).Execute(ruleContextExpectations.Instance());

    // Assert - test passes if no unexpected calls were made
}
```

## Key Concepts

### RockContext

The `RockContext` manages mock lifecycle. When disposed, it automatically verifies that all expected method calls occurred with the expected parameters.

### Create vs Make

| Method | Purpose | Verification |
| -------- | --------- | -------------- |
| `Create<T>()` | Expectation-based mock | Verifies calls happened as expected |
| `Make<T>()` | Simple stub | No verification, just returns configured values |

Use `Create` when you need to verify specific interactions. Use `Make` for dependencies that just need to exist.

### Setting Up Input Properties

Rules receive property values through `context.InputPropertyValues`. Set up this dictionary with your mock property infos as keys:

```csharp
var inputProperties = new Dictionary<IPropertyInfo, object>();
inputProperties.Add(mockProperty, testValue);

ruleContextExpectations.Properties.Getters.InputPropertyValues()
    .ReturnValue(inputProperties);
```

### Verifying Output Values

For calculation rules that call `AddOutValue`, set expectations:

```csharp
ruleContextExpectations.Methods.AddOutValue(
    Arg.Is(targetProperty), expectedValue);
```

### Verifying Error Results

For validation rules that call `AddErrorResult`:

```csharp
// Expect specific error message
ruleContextExpectations.Methods.AddErrorResult("Exact error message");

// Or match with predicate
ruleContextExpectations.Methods.AddErrorResult(
    Arg.Is<string>(s => s.Contains("expected text")));
```

## Testing Async Rules

For rules that inherit from `BusinessRuleAsync`, test the `ExecuteAsync` method:

```csharp
[TestMethod]
public async Task ExecuteAsync_ValidInput_NoError()
{
    // Arrange
    var inputProperties = new Dictionary<IPropertyInfo, object>();
    using var context = new RockContext();

    // ... set up mocks ...

    // Act
    var rule = new MyAsyncRule(mockProperty);
    await ((IBusinessRuleAsync)rule).ExecuteAsync(ruleContextExpectations.Instance());

    // Assert - verification on dispose
}
```

## Best Practices

1. **Test one behavior per test** - Each test should verify a single rule behavior
2. **Use descriptive test names** - Follow patterns like `MethodName_Scenario_ExpectedBehavior`
3. **Test edge cases** - Null values, empty strings, boundary conditions
4. **Test both pass and fail conditions** - Verify the rule validates correctly and rejects invalid data
5. **Keep input property setup clear** - Make it obvious what values the rule receives
6. **Use Make for non-essential dependencies** - Simplify tests by using stubs where verification isn't needed

## Common Patterns

### Testing Rules with Multiple Input Properties

```csharp
var inputProperties = new Dictionary<IPropertyInfo, object>
{
    { startDateProperty, DateTime.Today },
    { endDateProperty, DateTime.Today.AddDays(30) }
};
```

### Testing Rules That Check Object State

If your rule accesses `context.Target`, mock the target object:

```csharp
var targetExpectations = context.Create<IBusinessObjectExpectations>();
targetExpectations.Properties.Getters.IsNew().ReturnValue(true);

ruleContextExpectations.Properties.Getters.Target()
    .ReturnValue(targetExpectations.Instance());
```

### Testing Different Severity Levels

```csharp
// For warnings
ruleContextExpectations.Methods.AddWarningResult(Arg.Any<string>());

// For information
ruleContextExpectations.Methods.AddInformationResult(Arg.Any<string>());
```

## Related Documentation

- [BusinessRules.md](BusinessRules.md) - Business rule implementation overview
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Validation rules
- [BusinessRulesCalculation.md](BusinessRulesCalculation.md) - Calculation rules
- [BusinessRulesAsync.md](BusinessRulesAsync.md) - Async business rules

## Additional Resources

- [Rocks GitHub Repository](https://github.com/JasonBock/Rocks) - Rocks mocking framework documentation
- [Unit Testing CSLA Rules With Rocks](https://blog.lhotka.net/2025/10/02/Unit-Testing-CSLA-Rules-With-Rocks) - Original blog post with additional context
