# Asynchronous Rule Exception Handler

In CSLA 10, you can handle exceptions thrown by asynchronous business rules using the `IUnhandledAsyncRuleExceptionHandler` interface. This allows you to decide whether to handle specific exceptions or let them bubble up, and provides a centralized way to log or respond to rule failures.

## Overview

By default, exceptions thrown in asynchronous business rules are unhandled and can cause application crashes. The `IUnhandledAsyncRuleExceptionHandler` interface provides a mechanism to intercept these exceptions and handle them gracefully.

## Implementing the Interface

The interface has two methods:

- `bool CanHandle(Exception, IBusinessRuleBase)` - Determines whether this handler should process the exception
- `ValueTask Handle(Exception, IBusinessRuleBase, IRuleContext)` - Handles the exception when `CanHandle` returns true

### Basic Implementation

```csharp
using Csla.Rules;
using System;
using System.Threading.Tasks;

public class LoggingAsyncRuleExceptionHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger<LoggingAsyncRuleExceptionHandler> _logger;

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
        // Log the exception
        _logger.LogError(exception,
            $"Async rule {rule.GetType().Name} failed with exception: {exception.Message}");

        // Add an error to the rule context
        context.AddErrorResult($"A validation error occurred: {exception.Message}");

        return ValueTask.CompletedTask;
    }
}
```

## Registering the Handler

There are two ways to register your exception handler:

### Method 1: Add to Service Collection

Register the handler after adding CSLA to the service collection:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla();

    // Register the handler - must be after AddCsla()
    services.AddScoped<IUnhandledAsyncRuleExceptionHandler, LoggingAsyncRuleExceptionHandler>();
}
```

### Method 2: Use CSLA Configuration

Use the CSLA configuration options to register the handler:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla(options =>
    {
        options.UseUnhandledAsyncRuleExceptionHandler<LoggingAsyncRuleExceptionHandler>();
    });
}
```

> **Note:** The handler is registered as scoped by default when using the configuration method.

## Selective Exception Handling

You can implement `CanHandle` to selectively process specific types of exceptions:

```csharp
public class SelectiveAsyncRuleExceptionHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger _logger;

    public SelectiveAsyncRuleExceptionHandler(ILogger<SelectiveAsyncRuleExceptionHandler> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(Exception exception, IBusinessRuleBase rule)
    {
        // Only handle timeout and network exceptions
        return exception is TimeoutException
            || exception is HttpRequestException
            || exception is OperationCanceledException;
    }

    public ValueTask Handle(Exception exception, IBusinessRuleBase rule, IRuleContext context)
    {
        _logger.LogWarning(exception,
            $"Network/timeout issue in rule {rule.GetType().Name}");

        // Add a user-friendly error message
        context.AddErrorResult(
            "Unable to validate this field due to a network issue. Please try again.");

        return ValueTask.CompletedTask;
    }
}
```

## Advanced Scenarios

### Scenario 1: Retry Logic

Handle transient failures by implementing retry logic:

```csharp
public class RetryAsyncRuleExceptionHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger _logger;
    private const int MaxRetries = 3;

    public RetryAsyncRuleExceptionHandler(ILogger<RetryAsyncRuleExceptionHandler> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(Exception exception, IBusinessRuleBase rule)
    {
        return exception is HttpRequestException;
    }

    public async ValueTask Handle(Exception exception, IBusinessRuleBase rule, IRuleContext context)
    {
        _logger.LogWarning(exception,
            $"Transient failure in async rule {rule.GetType().Name}, will retry");

        // Note: Actual retry logic would need to be implemented in the rule itself
        // This handler can log and add appropriate error messages

        context.AddErrorResult(
            "Validation service temporarily unavailable. Please try saving again.");

        await ValueTask.CompletedTask;
    }
}
```

### Scenario 2: Different Handling Based on Rule Type

Handle exceptions differently based on the type of rule that failed:

```csharp
public class RuleTypeSpecificExceptionHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger _logger;
    private readonly IMetricsCollector _metrics;

    public RuleTypeSpecificExceptionHandler(
        ILogger<RuleTypeSpecificExceptionHandler> logger,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public bool CanHandle(Exception exception, IBusinessRuleBase rule)
    {
        return true;
    }

    public ValueTask Handle(Exception exception, IBusinessRuleBase rule, IRuleContext context)
    {
        // Track metrics
        _metrics.Increment("async_rule_failures", new[]
        {
            ("rule_type", rule.GetType().Name),
            ("exception_type", exception.GetType().Name)
        });

        // Different handling based on rule type
        if (rule is EmailValidationRule)
        {
            _logger.LogWarning(exception, "Email validation service failed");
            context.AddErrorResult("Unable to validate email address at this time.");
        }
        else if (rule is CreditCheckRule)
        {
            _logger.LogError(exception, "Critical: Credit check service failed");
            context.AddErrorResult("Credit verification unavailable. Please contact support.");
        }
        else
        {
            _logger.LogError(exception, $"Unhandled async rule failure: {rule.GetType().Name}");
            context.AddErrorResult("A validation error occurred. Please try again.");
        }

        return ValueTask.CompletedTask;
    }
}
```

### Scenario 3: Graceful Degradation

Allow the operation to continue with warnings instead of errors:

```csharp
public class GracefulDegradationHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger _logger;

    public GracefulDegradationHandler(ILogger<GracefulDegradationHandler> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(Exception exception, IBusinessRuleBase rule)
    {
        // Only handle non-critical validation failures
        return rule.Severity == RuleSeverity.Warning || rule.Severity == RuleSeverity.Information;
    }

    public ValueTask Handle(Exception exception, IBusinessRuleBase rule, IRuleContext context)
    {
        _logger.LogInformation(exception,
            $"Non-critical async rule {rule.GetType().Name} failed, continuing");

        // Add a warning instead of an error
        context.AddWarningResult(
            "Some optional validations could not be completed.");

        return ValueTask.CompletedTask;
    }
}
```

### Scenario 4: Combining Multiple Concerns

A comprehensive handler that logs, tracks metrics, and provides user-friendly messages:

```csharp
public class ComprehensiveAsyncRuleExceptionHandler : IUnhandledAsyncRuleExceptionHandler
{
    private readonly ILogger _logger;
    private readonly IMetricsCollector _metrics;
    private readonly IAlertService _alerts;

    public ComprehensiveAsyncRuleExceptionHandler(
        ILogger<ComprehensiveAsyncRuleExceptionHandler> logger,
        IMetricsCollector metrics,
        IAlertService alerts)
    {
        _logger = logger;
        _metrics = metrics;
        _alerts = alerts;
    }

    public bool CanHandle(Exception exception, IBusinessRuleBase rule)
    {
        return true;
    }

    public async ValueTask Handle(Exception exception, IBusinessRuleBase rule, IRuleContext context)
    {
        var ruleName = rule.GetType().Name;

        // Log the exception
        _logger.LogError(exception,
            $"Async rule {ruleName} failed: {exception.Message}");

        // Track metrics
        _metrics.Increment("async_rule_exception", new[]
        {
            ("rule", ruleName),
            ("exception_type", exception.GetType().Name)
        });

        // Send alert for critical failures
        if (exception is not TimeoutException)
        {
            await _alerts.SendAsync(
                $"Async rule failure: {ruleName}",
                exception.ToString());
        }

        // Provide user-friendly error message
        var userMessage = GetUserFriendlyMessage(exception, rule);
        context.AddErrorResult(userMessage);
    }

    private string GetUserFriendlyMessage(Exception exception, IBusinessRuleBase rule)
    {
        return exception switch
        {
            TimeoutException => "The validation service is taking too long to respond. Please try again.",
            HttpRequestException => "Unable to connect to the validation service. Please check your connection.",
            UnauthorizedAccessException => "You don't have permission to perform this validation.",
            _ => "A validation error occurred. Please try again or contact support."
        };
    }
}
```

## Best Practices

1. **Always implement CanHandle thoughtfully** - Return `true` only for exceptions you can meaningfully handle
2. **Log exceptions appropriately** - Use different log levels based on severity
3. **Provide user-friendly messages** - Don't expose technical details to end users via `context.AddErrorResult()`
4. **Track metrics** - Monitor async rule failures to identify problematic services or rules
5. **Consider severity** - Different handling for errors vs. warnings
6. **Be careful with async operations** - The `Handle` method returns a `ValueTask`, but avoid long-running operations
7. **Don't swallow critical exceptions** - If you can't handle an exception meaningfully, let it bubble up
8. **Test your handler** - Unit test different exception scenarios

## Important Notes

- By default, CSLA 10 does **not** handle exceptions in asynchronous rules - they remain unhandled
- Implementing a handler doesn't mean all exceptions are automatically handled - `CanHandle` must return `true`
- The handler runs in the same execution context as the rule
- Multiple handlers can be registered, and they will be called in the order they were registered
- The handler is registered as scoped, so it can have scoped dependencies like `DbContext` or user context
