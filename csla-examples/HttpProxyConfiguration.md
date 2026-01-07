# HTTP Proxy Configuration

The CSLA data portal supports HTTP-based communication between client and server through `HttpProxy` and `HttpCompressionProxy`. These proxies can be configured with custom timeout values and other options.

## Overview

When using CSLA in a client-server architecture (such as Blazor WebAssembly, MAUI, or desktop applications), the data portal can communicate with the server over HTTP. The `HttpProxy` and `HttpCompressionProxy` classes handle this communication and can be configured through `HttpProxyOptions`.

## Basic Configuration

Configure the HTTP proxy in your client application's startup code:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCsla(options => options
        .DataPortal(dp => dp
            .AddClientSideDataPortal(csp => csp
                .UseHttpProxy(proxy => proxy
                    .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
}
```

## Configuring Timeouts

CSLA 9 and later support configuring timeout values for HTTP requests.

### Using WithTimeout

Set the overall timeout for the HTTP request:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

### Using WithReadWriteTimeout

Set the read/write timeout for the HTTP request:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithReadWriteTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

### Configuring Both Timeouts

Set both timeout values:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(60))
                .WithReadWriteTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

## HttpCompressionProxy

The `HttpCompressionProxy` compresses request and response data to reduce bandwidth usage. It supports the same timeout configuration:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpCompressionProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(60))
                .WithReadWriteTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

## Platform-Specific Configurations

### Blazor WebAssembly

Typical configuration for a Blazor WebAssembly client:

```csharp
// Program.cs in Blazor WebAssembly project
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly(blazor => blazor
        .SyncContextWithServer = true)
    .Security(security => security
        .FlowSecurityPrincipalFromClient = true)
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromMinutes(2))
                .DataPortalUrl = "/api/DataPortal"))));

await builder.Build().RunAsync();
```

### MAUI Application

Configuration for a MAUI mobile or desktop application:

```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddCsla(options => options
            .DataPortal(dp => dp
                .AddClientSideDataPortal(csp => csp
                    .UseHttpProxy(proxy => proxy
                        .WithTimeout(TimeSpan.FromSeconds(45))
                        .WithReadWriteTimeout(TimeSpan.FromSeconds(30))
                        .DataPortalUrl = "https://myserver.com/api/DataPortal"))));

        return builder.Build();
    }
}
```

### WPF or Windows Forms

Configuration for desktop applications:

```csharp
// App.xaml.cs (WPF) or Program.cs (Windows Forms)
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        services.AddCsla(options => options
            .DataPortal(dp => dp
                .AddClientSideDataPortal(csp => csp
                    .UseHttpProxy(proxy => proxy
                        .WithTimeout(TimeSpan.FromMinutes(1))
                        .DataPortalUrl = "https://myserver.com/api/DataPortal"))));

        var serviceProvider = services.BuildServiceProvider();
        // Store serviceProvider for later use
    }
}
```

## Common Scenarios

### Scenario 1: Short Timeout for Quick Operations

For applications where operations should complete quickly:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(10))
                .WithReadWriteTimeout(TimeSpan.FromSeconds(5))
                .DataPortalUrl = "https://api.example.com/DataPortal"))));
```

### Scenario 2: Long Timeout for Reports or Batch Operations

For applications that may have long-running server operations:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromMinutes(5))
                .WithReadWriteTimeout(TimeSpan.FromMinutes(3))
                .DataPortalUrl = "https://api.example.com/DataPortal"))));
```

### Scenario 3: Environment-Specific Timeouts

Different timeouts based on the environment:

```csharp
public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    var timeoutSeconds = configuration.GetValue<int>("DataPortal:TimeoutSeconds", 30);

    services.AddCsla(options => options
        .DataPortal(dp => dp
            .AddClientSideDataPortal(csp => csp
                .UseHttpProxy(proxy => proxy
                    .WithTimeout(TimeSpan.FromSeconds(timeoutSeconds))
                    .DataPortalUrl = configuration["DataPortal:Url"]))));
}
```

**appsettings.json:**
```json
{
  "DataPortal": {
    "Url": "https://api.example.com/DataPortal",
    "TimeoutSeconds": 60
  }
}
```

**appsettings.Development.json:**
```json
{
  "DataPortal": {
    "TimeoutSeconds": 300
  }
}
```

### Scenario 4: Retry Logic with Polly

Combine timeout configuration with retry logic using Polly:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://api.example.com/DataPortal"))));

// Note: Actual retry logic would be implemented in a custom data portal proxy
// or through HTTP client configuration
```

## Server-Side Configuration

The server must also be configured to handle data portal requests:

### ASP.NET Core Server

```csharp
// Program.cs on the server
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

// Map the data portal endpoint
app.MapControllers();

app.Run();
```

### Data Portal Controller

```csharp
[Route("api/[controller]")]
[ApiController]
public class DataPortalController : Csla.Server.Hosts.HttpPortalController
{
    public DataPortalController(ApplicationContext applicationContext)
        : base(applicationContext)
    {
    }
}
```

## Understanding Timeouts

### Timeout vs ReadWriteTimeout

- **Timeout**: The overall time allowed for the entire HTTP request/response cycle
- **ReadWriteTimeout**: The time allowed for reading from or writing to the HTTP stream

### Default Values

If you don't configure timeout values, default values from the underlying HTTP client are used. These defaults vary by platform but are typically around 100 seconds.

### Timeout Behavior

When a timeout occurs:
1. The HTTP request is cancelled
2. A `TimeoutException` or `TaskCanceledException` is thrown
3. The data portal operation fails
4. The exception bubbles up to the calling code

Example handling:

```csharp
try
{
    var customer = await customerPortal.FetchAsync(123);
}
catch (DataPortalException ex) when (ex.InnerException is TimeoutException)
{
    // Handle timeout
    MessageBox.Show("The operation took too long. Please try again.");
}
catch (DataPortalException ex) when (ex.InnerException is TaskCanceledException)
{
    // Handle cancellation (which may be due to timeout)
    MessageBox.Show("The operation was cancelled or timed out.");
}
```

## Best Practices

1. **Set appropriate timeouts** - Consider your network conditions and operation complexity
2. **Configure timeouts in settings** - Use `appsettings.json` for easy environment-specific configuration
3. **Use compression for large payloads** - `HttpCompressionProxy` can significantly reduce bandwidth
4. **Handle timeout exceptions** - Always catch and handle timeout exceptions gracefully
5. **Test with realistic conditions** - Test timeout behavior under poor network conditions
6. **Consider mobile networks** - Mobile applications may need longer timeouts due to variable network quality
7. **Monitor and adjust** - Track timeout occurrences and adjust values based on real-world usage
8. **Balance user experience and server load** - Very long timeouts can tie up server resources

## Troubleshooting

### Frequent Timeouts

If you're experiencing frequent timeouts:
- Increase the timeout values
- Check server performance and optimize slow operations
- Consider implementing caching
- Review network connectivity issues

### Timeouts on Mobile Devices

Mobile devices may require longer timeouts:
- Use at least 60 seconds for mobile applications
- Consider network type detection (WiFi vs cellular)
- Implement offline support with data synchronization

### Debugging Timeout Issues

To debug timeout issues:
1. Enable detailed logging on both client and server
2. Measure actual operation duration on the server
3. Compare timeout values to actual operation duration
4. Check for network latency issues
5. Use network monitoring tools to inspect HTTP traffic

## Related Configuration

### Authentication Headers

Configure authentication headers along with timeouts:

```csharp
services.AddHttpClient("CslaDataPortal", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("Authorization", "Bearer {token}");
});

services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(60))
                .DataPortalUrl = "https://api.example.com/DataPortal"))));
```

### Custom Headers

Add custom headers to data portal requests:

```csharp
// Custom data portal proxy with headers
public class CustomHttpProxy : Csla.DataPortalClient.HttpProxy
{
    protected override HttpClient GetHttpClient()
    {
        var client = base.GetHttpClient();
        client.DefaultRequestHeaders.Add("X-Custom-Header", "CustomValue");
        return client;
    }
}
```

## Notes

- Timeout configuration is available in CSLA 9 and later
- Both `HttpProxy` and `HttpCompressionProxy` support the same timeout configuration
- Timeout values should be chosen based on expected operation duration and network conditions
- The data portal URL can be relative (for same-origin scenarios) or absolute (for cross-origin scenarios)
