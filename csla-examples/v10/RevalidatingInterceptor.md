# Revalidating Interceptor

The `RevalidatingInterceptor` is a data portal interceptor that automatically revalidates business rules before performing data portal operations. In CSLA 10, it has been enhanced with configurable options to control when revalidation occurs.

## Overview

By default, the `RevalidatingInterceptor` runs business rule validation before every data portal operation (Insert, Update, Delete). In CSLA 10, you can configure it to skip revalidation during Delete operations.

## Configuration in CSLA 10

CSLA 10 uses the .NET Options pattern to configure the `RevalidatingInterceptor`:

### Basic Configuration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla();

    // Configure RevalidatingInterceptor options
    services.Configure<RevalidatingInterceptorOptions>(options =>
    {
        options.IgnoreDeleteOperation = true;
    });
}
```

### Available Options

- `IgnoreDeleteOperation` (bool) - When set to `true`, skips business rule validation during Delete operations. Default is `false`.

## Why Skip Validation on Delete?

There are several scenarios where you might want to skip validation during delete operations:

1. **Performance** - Delete operations typically don't need to validate business rules since the object is being removed
2. **Simplified Logic** - Deleted objects may be in an invalid state, and validating them adds unnecessary complexity
3. **User Experience** - Users shouldn't be prevented from deleting an invalid object
4. **Historical Data** - Objects that were valid when created may no longer pass current validation rules

## Usage Scenarios

### Scenario 1: Default Behavior (Validate All Operations)

If you don't configure any options, the interceptor validates on all operations:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla();
    // No configuration - validates on Insert, Update, and Delete
}
```

Example business object:

```csharp
[CslaImplementProperties]
public partial class CustomerEdit : BusinessBase<CustomerEdit>
{
    public partial int Id { get; private set; }
    [Required]
    public partial string Name { get; set; }
    [EmailAddress]
    public partial string Email { get; set; }

    [Delete]
    private async Task Delete(int id, [Inject] ICustomerDal dal)
    {
        // Business rules will be validated before this method is called
        // If validation fails, the delete operation will not proceed
        await dal.Delete(id);
    }
}
```

### Scenario 2: Skip Validation on Delete

When you configure `IgnoreDeleteOperation = true`, delete operations skip validation:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla();

    services.Configure<RevalidatingInterceptorOptions>(options =>
    {
        options.IgnoreDeleteOperation = true;
    });
}
```

Example business object:

```csharp
[CslaImplementProperties]
public partial class CustomerEdit : BusinessBase<CustomerEdit>
{
    public partial int Id { get; private set; }
    [Required]
    public partial string Name { get; set; }
    [EmailAddress]
    public partial string Email { get; set; }

    [Delete]
    private async Task Delete(int id, [Inject] ICustomerDal dal)
    {
        // Business rules are NOT validated before this method
        // The delete will proceed regardless of the object's validation state
        await dal.Delete(id);
    }

    [Update]
    private async Task Update([Inject] ICustomerDal dal)
    {
        // Business rules ARE still validated before Update
        var data = new CustomerData();
        Csla.Data.DataMapper.Map(this, data);
        await dal.Update(data);
    }
}
```

### Scenario 3: Environment-Specific Configuration

You might want different behavior in different environments:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla();

    services.Configure<RevalidatingInterceptorOptions>(options =>
    {
        // In production, skip validation on delete for better performance
        // In development, validate everything to catch potential issues
        options.IgnoreDeleteOperation = _environment.IsProduction();
    });
}
```

### Scenario 4: Configuration from Settings

Load the configuration from application settings:

**appsettings.json:**
```json
{
  "Csla": {
    "RevalidatingInterceptor": {
      "IgnoreDeleteOperation": true
    }
  }
}
```

**Startup.cs:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla();

    // Bind configuration from appsettings.json
    services.Configure<RevalidatingInterceptorOptions>(
        Configuration.GetSection("Csla:RevalidatingInterceptor"));
}
```

## Understanding the Impact

### With IgnoreDeleteOperation = false (Default)

```csharp
// Client code
var customer = await customerPortal.FetchAsync(123);
customer.Name = ""; // Make object invalid

// This will fail because validation runs and Name is required
await customerPortal.DeleteAsync(customer);
// InvalidOperationException: Cannot delete an invalid object
```

### With IgnoreDeleteOperation = true

```csharp
// Client code
var customer = await customerPortal.FetchAsync(123);
customer.Name = ""; // Make object invalid

// This will succeed because validation is skipped for delete
await customerPortal.DeleteAsync(customer);
// Delete proceeds successfully despite invalid state
```

## Best Practices

1. **Enable IgnoreDeleteOperation in most cases** - Delete operations typically don't need validation
2. **Document your choice** - Make it clear to your team whether delete validation is enabled
3. **Consider your business rules** - If you have delete-specific business rules, you may want validation enabled
4. **Use configuration files** - Store the setting in `appsettings.json` for easy environment-specific changes
5. **Test both paths** - Ensure your delete logic works correctly with and without validation

## Migration from CSLA 9

In CSLA 9, the `RevalidatingInterceptor` constructor didn't require any parameters:

```csharp
// CSLA 9
public RevalidatingInterceptor()
{
    // ...
}
```

In CSLA 10, it requires an `IOptions<RevalidatingInterceptorOptions>` parameter:

```csharp
// CSLA 10
public RevalidatingInterceptor(IOptions<RevalidatingInterceptorOptions> options)
{
    // ...
}
```

When upgrading from CSLA 9 to CSLA 10, you must configure the options even if you want the default behavior:

```csharp
// Minimum required configuration for CSLA 10
services.Configure<RevalidatingInterceptorOptions>(options =>
{
    // options.IgnoreDeleteOperation defaults to false (same as CSLA 9 behavior)
});
```

Or simply:

```csharp
// This is sufficient - default options will be used
services.AddCsla();
```

## Related Concepts

- **Data Portal Interceptors** - The `RevalidatingInterceptor` is one of several interceptors in the CSLA pipeline
- **Business Rules** - Understanding when rules run is important for optimal configuration
- **CheckRulesAsync** - Use `await CheckRulesAsync()` in CSLA 10 for async rule support

## Notes

- The `RevalidatingInterceptor` is automatically registered when you call `services.AddCsla()`
- If validation fails during Insert or Update (with default settings), an exception is thrown
- The `IgnoreDeleteOperation` option only affects the `RevalidatingInterceptor` - you can still manually call `CheckRulesAsync()` in your Delete methods if needed
- Custom interceptors can be added alongside the `RevalidatingInterceptor`
