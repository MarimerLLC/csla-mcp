# Business Rules: Asynchronous Rules

Asynchronous business rules allow you to perform long-running operations like database queries, web service calls, or complex computations without blocking the UI thread. Async rules inherit from `BusinessRuleAsync` instead of `BusinessRule`.

## Overview

Async rules are useful when you need to:

- Call external web services or APIs
- Query databases for validation data
- Perform expensive calculations
- Execute data portal operations
- Make network requests

## Basic Async Rule Structure

```csharp
using Csla.Rules;
using System.Threading.Tasks;

public class MyAsyncRule : BusinessRuleAsync
{
    public MyAsyncRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        // Async operation here
        await Task.Delay(1000);

        // Access input property values
        var value = (string)context.InputPropertyValues[PrimaryProperty];

        // Add results
        if (value == "Error")
            context.AddErrorResult("Invalid value");
    }
}
```

## Key Differences from Synchronous Rules

| Feature | Synchronous Rule | Asynchronous Rule |
|---------|-----------------|-------------------|
| Base class | `BusinessRule` | `BusinessRuleAsync` |
| Execute method | `Execute(IRuleContext)` | `async Task ExecuteAsync(IRuleContext)` |
| IsAsync property | `false` (default) | `true` (always) |
| Execution | Runs on calling thread | Runs asynchronously |
| UI blocking | Blocks UI | Doesn't block UI |

## Simple Async Validation Rule

A rule that simulates a delay (like a web service call):

```csharp
public class AsyncRule : BusinessRuleAsync
{
    public AsyncRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        // Simulate async operation (e.g., web service call)
        await Task.Delay(3000);

        string value = (string)context.InputPropertyValues[PrimaryProperty];

        if (value == "Error")
            context.AddErrorResult("Invalid data!");
        else if (value == "Warning")
            context.AddWarningResult("This might not be a great idea!");
        else if (value == "Information")
            context.AddInformationResult("Just an FYI!");
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    BusinessRules.AddRule(new AsyncRule(NameProperty));
}
```

## Async Rule with Data Portal

Validate a value by fetching data from the database:

```csharp
public class ValidRole : BusinessRuleAsync
{
    public ValidRole(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        // Get the role ID to validate
        int roleId = (int)context.InputPropertyValues[PrimaryProperty];

        // Fetch the valid roles list from the database
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<RoleList>>();
        var roles = await portal.FetchAsync();

        // Check if the role is valid
        if (!roles.ContainsKey(roleId))
            context.AddErrorResult("Role must be in RoleList");
    }
}
```

**Usage:**

```csharp
[CslaImplementProperties]
public partial class Assignment : BusinessBase<Assignment>
{
    public partial int RoleId { get; set; }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new ValidRole(RoleIdProperty));
    }
}
```

## Async Rule with Command Object

Execute a command object to perform validation:

```csharp
public class MyExpensiveRule : BusinessRuleAsync
{
    protected override async Task ExecuteAsync(IRuleContext context)
    {
        // Get the data portal for the command
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<MyExpensiveCommand>>();

        // Create and execute the command
        var command = portal.Create();
        var result = await portal.ExecuteAsync(command);

        // Process the result
        if (result == null)
            context.AddErrorResult("Command failed to run");
        else if (result.Result)
            context.AddInformationResult(result.ResultText);
        else
            context.AddErrorResult(result.ResultText);
    }
}
```

## Async Rule with External Web Service

Validate data by calling an external API:

```csharp
public class ValidateEmailWithService : BusinessRuleAsync
{
    private readonly HttpClient _httpClient;

    public ValidateEmailWithService(IPropertyInfo primaryProperty, HttpClient httpClient)
        : base(primaryProperty)
    {
        _httpClient = httpClient;
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        string email = (string)context.InputPropertyValues[PrimaryProperty];

        if (string.IsNullOrWhiteSpace(email))
            return; // Let Required rule handle this

        try
        {
            // Call external validation service
            var response = await _httpClient.GetAsync($"https://api.example.com/validate-email?email={email}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ValidationResult>(json);

                if (!result.IsValid)
                    context.AddErrorResult($"Email validation failed: {result.Reason}");
            }
            else
            {
                context.AddWarningResult("Unable to verify email address with validation service");
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail validation
            context.AddWarningResult("Email validation service is currently unavailable");
        }
    }
}
```

## Async Rule with Database Query

Query the database directly to validate uniqueness:

```csharp
public class UniqueUsername : BusinessRuleAsync
{
    private IPropertyInfo _idProperty;

    public UniqueUsername(IPropertyInfo usernameProperty, IPropertyInfo idProperty)
        : base(usernameProperty)
    {
        _idProperty = idProperty;
        InputProperties.Add(usernameProperty);
        InputProperties.Add(idProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        string username = (string)context.InputPropertyValues[PrimaryProperty];
        int id = (int)context.InputPropertyValues[_idProperty];

        if (string.IsNullOrWhiteSpace(username))
            return;

        // Get database connection from DI
        var dal = context.ApplicationContext.GetRequiredService<IUserDal>();

        // Check if username exists (excluding current user)
        bool exists = await dal.UsernameExistsAsync(username, id);

        if (exists)
            context.AddErrorResult("Username is already taken");
    }
}
```

**Usage:**

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    BusinessRules.AddRule(new UniqueUsername(UsernameProperty, IdProperty));
}
```

## Controlling Async Rule Execution

### RunMode for Async Rules

Async rules often need to control when they run. Use `RunMode` to prevent execution in certain contexts:

```csharp
public class AsyncLookupRule : BusinessRuleAsync
{
    public AsyncLookupRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);

        // Don't run on server-side data portal
        RunMode = RunModes.DenyOnServerSidePortal;
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        // This only runs on the client
        await CallExternalServiceAsync();
    }
}
```

### Preventing Rule Execution

Create a base class for async rules that should only run on the client:

```csharp
public abstract class AsyncLookupRule : BusinessRule
{
    protected AsyncLookupRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        IsAsync = true;

        // Control when the rule runs
        RunMode = RunModes.DenyOnServerSidePortal | RunModes.DenyAsAffectedProperty;
    }
}
```

**Why use these settings:**
- `DenyOnServerSidePortal` - Prevents the rule from running on the server (useful for client-only validation)
- `DenyAsAffectedProperty` - Prevents the rule from running when it's triggered by another rule
- `DenyCheckRules` - Prevents the rule from running during explicit CheckRules calls

## Async Rules and Priority

Async rules support priorities just like synchronous rules, but they have special execution characteristics:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // Priority -1: Sync calculation runs first
    BusinessRules.AddRule(new CalcTotal(TotalProperty, SubtotalProperty, TaxProperty) { Priority = -1 });

    // Priority 0: Async validation runs after sync rules at same priority
    BusinessRules.AddRule(new ValidateWithWebService(TotalProperty));

    // Priority 1: Sync validation runs after async completes
    BusinessRules.AddRule(new MinValue<decimal>(TotalProperty, 0) { Priority = 1 });
}
```

**Execution order:**
1. All synchronous rules at priority -1
2. All asynchronous rules at priority -1 (run concurrently)
3. Wait for all async rules to complete
4. All synchronous rules at priority 0
5. All asynchronous rules at priority 0
6. And so on...

## Async Rules and Dependencies

Async rules work with dependencies just like synchronous rules:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();

    // When Email changes, run async validation
    BusinessRules.AddRule(new Dependency(EmailProperty, EmailProperty));
    BusinessRules.AddRule(new ValidateEmailWithService(EmailProperty, _httpClient));
}
```

## Exception Handling in Async Rules

### Basic Exception Handling

Handle exceptions within the rule:

```csharp
public class SafeAsyncRule : BusinessRuleAsync
{
    public SafeAsyncRule(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        try
        {
            await CallExternalServiceAsync();
        }
        catch (HttpRequestException ex)
        {
            // Network error - add warning instead of error
            context.AddWarningResult("Unable to validate with external service");
        }
        catch (Exception ex)
        {
            // Unexpected error - add error
            context.AddErrorResult($"Validation failed: {ex.Message}");
        }
    }

    private async Task CallExternalServiceAsync()
    {
        // External service call
        await Task.Delay(1000);
    }
}
```

### Using IUnhandledAsyncRuleExceptionHandler (CSLA 10)

In CSLA 10, you can register a global exception handler for async rules:

```csharp
public class LoggingAsyncRuleExceptionHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger _logger;

    public LoggingAsyncRuleExceptionHandler(ILogger<LoggingAsyncRuleExceptionHandler> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(Exception exception, IBusinessRuleBase rule)
    {
        // Handle all exceptions
        return true;
    }

    public ValueTask Handle(Exception exception, IBusinessRuleBase rule, IRuleContext context)
    {
        _logger.LogError(exception, $"Async rule {rule.GetType().Name} failed");
        context.AddErrorResult("A validation error occurred. Please try again.");
        return ValueTask.CompletedTask;
    }
}

// Register in Startup
services.AddCsla(options =>
{
    options.UseUnhandledAsyncRuleExceptionHandler<LoggingAsyncRuleExceptionHandler>();
});
```

See [AsyncRuleExceptionHandler.md](v10/AsyncRuleExceptionHandler.md) for more details on CSLA 10's async rule exception handling.

## Best Practices

1. **Always use async/await** - Don't block async operations with `.Result` or `.Wait()`
2. **Handle exceptions gracefully** - Catch and handle expected exceptions within the rule
3. **Use RunMode appropriately** - Prevent async rules from running on the server if they call external services
4. **Keep rules focused** - Each async rule should do one thing well
5. **Consider timeouts** - Add timeout logic for external service calls
6. **Test async rules thoroughly** - Async code is harder to test, so write comprehensive tests
7. **Use priorities carefully** - Remember that async rules at the same priority run concurrently
8. **Log failures** - Always log unexpected failures for debugging
9. **Provide user feedback** - Add appropriate error/warning/information messages
10. **Optimize for performance** - Async rules can be expensive; only use them when necessary

## Common Patterns

### Pattern 1: Lookup Validation

```csharp
public class ValidateAgainstLookup : BusinessRuleAsync
{
    public ValidateAgainstLookup(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        var value = context.InputPropertyValues[PrimaryProperty];
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<LookupList>>();
        var list = await portal.FetchAsync();

        if (!list.Contains(value))
            context.AddErrorResult("Value not found in valid list");
    }
}
```

### Pattern 2: External Service with Timeout

```csharp
public class ValidateWithTimeout : BusinessRuleAsync
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public ValidateWithTimeout(IPropertyInfo primaryProperty, HttpClient httpClient)
        : base(primaryProperty)
    {
        _httpClient = httpClient;
        InputProperties.Add(primaryProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        var value = (string)context.InputPropertyValues[PrimaryProperty];

        using var cts = new CancellationTokenSource(_timeout);

        try
        {
            var response = await _httpClient.GetAsync($"https://api.example.com/validate?value={value}", cts.Token);

            if (!response.IsSuccessStatusCode)
                context.AddErrorResult("Validation service rejected the value");
        }
        catch (TaskCanceledException)
        {
            context.AddWarningResult("Validation service timed out");
        }
        catch (Exception ex)
        {
            context.AddWarningResult($"Unable to validate: {ex.Message}");
        }
    }
}
```

### Pattern 3: Async Calculation

```csharp
public class CalculateFromService : BusinessRuleAsync
{
    private IPropertyInfo _resultProperty;

    public CalculateFromService(IPropertyInfo inputProperty, IPropertyInfo resultProperty)
        : base(inputProperty)
    {
        _resultProperty = resultProperty;
        InputProperties.Add(inputProperty);
        AffectedProperties.Add(resultProperty);
    }

    protected override async Task ExecuteAsync(IRuleContext context)
    {
        var input = (decimal)context.InputPropertyValues[PrimaryProperty];

        // Call external service for calculation
        var portal = context.ApplicationContext.GetRequiredService<IDataPortal<CalculationCommand>>();
        var command = await portal.CreateAsync(input);
        var result = await portal.ExecuteAsync(command);

        // Set the calculated value
        context.AddOutValue(_resultProperty, result.CalculatedValue);
    }
}
```

## Notes

- Async rules always have `IsAsync = true`
- Multiple async rules at the same priority level execute concurrently
- Async rules execute after all synchronous rules at the same priority level
- Use `context.ApplicationContext.GetRequiredService<T>()` to get dependencies within rules
- Async rules work seamlessly with the Data Portal across application boundaries
- The UI remains responsive while async rules execute

## See Also

- [BusinessRules.md](BusinessRules.md) - Overview of the business rules system
- [BusinessRulesPriority.md](BusinessRulesPriority.md) - Rule priorities and execution order
- [BusinessRulesValidation.md](BusinessRulesValidation.md) - Synchronous validation rules
- [v10/AsyncRuleExceptionHandler.md](v10/AsyncRuleExceptionHandler.md) - CSLA 10 async rule exception handling
- [DataPortalOperation*.md](DataPortalOperationFetch.md) - Data Portal operations used in async rules
