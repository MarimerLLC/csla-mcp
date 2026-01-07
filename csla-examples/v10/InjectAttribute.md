# Inject Attribute

The `[Inject]` attribute is used in CSLA data portal operation methods to inject dependencies from the dependency injection (DI) container. This allows you to access services like data access layers (DALs), repositories, or other application services within your business object methods.

## Basic Usage

The simplest form uses `[Inject]` to inject a required service:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal)
{
    var customerData = await customerDal.Get(id);
    Csla.Data.DataMapper.Map(customerData, this);
    await CheckRulesAsync();
}
```

In this example, `ICustomerDal` is resolved from the DI container using `GetRequiredService`, which will throw an exception if the service is not registered.

## AllowNull Property (CSLA 10)

In CSLA 10, the `[Inject]` attribute includes an `AllowNull` property that controls whether the service resolution uses `GetService` (can return null) or `GetRequiredService` (throws if not found).

### Using AllowNull = true

When a service is optional and you want to handle the case where it might not be registered:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal customerDal, [Inject(AllowNull = true)] ILogger logger)
{
    var customerData = await customerDal.Get(id);

    // Logger might be null, so check before using
    logger?.LogInformation($"Fetching customer {id}");

    Csla.Data.DataMapper.Map(customerData, this);
    await CheckRulesAsync();
}
```

### Using AllowNull = false (Default)

This is the default behavior and ensures the service must be available:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject(AllowNull = false)] ICustomerDal customerDal)
{
    // customerDal is guaranteed to be non-null
    var customerData = await customerDal.Get(id);
    Csla.Data.DataMapper.Map(customerData, this);
    await CheckRulesAsync();
}
```

## Nullable Reference Types Integration

If you're using nullable reference types (`#nullable enable`), the `AllowNull` property is implicitly determined by the parameter's nullability annotation:

```csharp
#nullable enable

[Fetch]
private async Task Fetch(
    int id,
    [Inject] ICustomerDal customerDal,      // GetRequiredService - non-nullable
    [Inject] ILogger? logger,               // GetService - nullable
    [Inject] ICache? cache)                 // GetService - nullable
{
    var customerData = await customerDal.Get(id);

    // customerDal is guaranteed non-null
    // logger and cache might be null, so use null-conditional operator
    logger?.LogInformation($"Fetching customer {id}");

    var cachedData = cache?.Get($"customer_{id}");
    if (cachedData == null)
    {
        Csla.Data.DataMapper.Map(customerData, this);
    }

    await CheckRulesAsync();
}
```

### Explicit Override with AllowNull

You can explicitly set `AllowNull` to override the nullable reference type annotation:

```csharp
#nullable enable

[Fetch]
private async Task Fetch(
    int id,
    [Inject(AllowNull = true)] IService service)  // Explicitly allows null
{
    // Even though IService is not marked as nullable,
    // AllowNull = true means GetService is used and it can be null
    if (service != null)
    {
        await service.DoSomething();
    }
}
```

> **Note:** If you use `[Inject(AllowNull = false)]` with a nullable parameter like `IService?`, the nullable annotation takes precedence and the parameter will still be nullable. The `AllowNull` property cannot make a nullable parameter non-nullable.

## Multiple Injected Dependencies

You can inject multiple services in a single data portal operation:

```csharp
[Fetch]
private async Task Fetch(
    int id,
    [Inject] ICustomerDal customerDal,
    [Inject] IEmailService emailService,
    [Inject] ILogger? logger)
{
    logger?.LogInformation($"Fetching customer {id}");

    var customerData = await customerDal.Get(id);
    Csla.Data.DataMapper.Map(customerData, this);

    // Send welcome email if this is a new fetch
    await emailService.SendWelcomeEmail(customerData.Email);

    await CheckRulesAsync();
}
```

## Common Scenarios

### Scenario 1: Required DAL Service

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal dal)
{
    var data = await dal.Get(id);
    Csla.Data.DataMapper.Map(data, this);
    await CheckRulesAsync();
}
```

### Scenario 2: Optional Logging Service

```csharp
[Update]
private async Task Update([Inject] ICustomerDal dal, [Inject] ILogger? logger)
{
    logger?.LogInformation($"Updating customer {Id}");

    var data = new CustomerData();
    Csla.Data.DataMapper.Map(this, data);
    await dal.Update(data);

    logger?.LogInformation($"Customer {Id} updated successfully");
}
```

### Scenario 3: Optional Caching with Fallback

```csharp
[Fetch]
private async Task Fetch(
    int id,
    [Inject] ICustomerDal dal,
    [Inject(AllowNull = true)] ICache cache)
{
    CustomerData data;

    // Try to get from cache first
    if (cache != null)
    {
        data = cache.Get<CustomerData>($"customer_{id}");
        if (data != null)
        {
            Csla.Data.DataMapper.Map(data, this);
            await CheckRulesAsync();
            return;
        }
    }

    // Cache miss or no cache - fetch from DAL
    data = await dal.Get(id);
    Csla.Data.DataMapper.Map(data, this);

    // Store in cache for next time
    cache?.Set($"customer_{id}", data, TimeSpan.FromMinutes(5));

    await CheckRulesAsync();
}
```

### Scenario 4: Mixing Required and Optional Services

```csharp
#nullable enable

[Create]
private async Task Create(
    [Inject] ICustomerDal dal,              // Required
    [Inject] IDefaultsProvider defaults,    // Required
    [Inject] ILogger? logger,               // Optional
    [Inject] IMetricsCollector? metrics)    // Optional
{
    logger?.LogInformation("Creating new customer");
    metrics?.Increment("customer.create");

    var defaultData = await defaults.GetCustomerDefaults();
    LoadProperty(CreatedDateProperty, defaultData.CreatedDate);
    LoadProperty(IsActiveProperty, defaultData.IsActive);

    await CheckRulesAsync();
}
```

## Best Practices

1. **Use required services for critical dependencies** - If your code cannot function without a service, don't use `AllowNull = true`
2. **Use optional services for cross-cutting concerns** - Logging, metrics, and caching are good candidates for optional services
3. **Check for null before using optional services** - Always use the null-conditional operator (`?.`) or null checks when working with optional services
4. **Leverage nullable reference types** - In CSLA 10, enable nullable reference types to get compiler assistance with null safety
5. **Avoid overriding nullable annotations** - Let the nullable reference type system guide your `AllowNull` settings
6. **Keep injection simple** - Inject only what you need for the specific operation
