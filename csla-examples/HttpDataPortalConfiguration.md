# HTTP Data Portal Configuration

The CSLA data portal supports HTTP-based communication between client and server through `HttpProxy` (client-side) and `HttpPortalController` (server-side). This document covers configuration for both ends of the HTTP data portal channel.

## Overview

When using CSLA in a client-server architecture (such as Blazor WebAssembly, MAUI, or desktop applications), the data portal can communicate with the server over HTTP. The architecture consists of:

- **Client-Side**: `HttpProxy` or `HttpCompressionProxy` classes that serialize and send data portal requests
- **Server-Side**: `HttpPortalController` that receives and processes data portal requests

Both ends must be properly configured for the HTTP data portal channel to work.

## Part 1: Server-Side Configuration

The server-side configuration involves setting up ASP.NET Core to host the data portal controller endpoint.

### Basic ASP.NET Core Server Setup

Configure CSLA and required services in your server's `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add controller support
builder.Services.AddControllers();

// CSLA requires AddHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add CSLA with ASP.NET Core support
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Data Portal Controller

Create a data portal controller that inherits from `HttpPortalController`:

```csharp
using Csla;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataPortalController(ApplicationContext applicationContext) 
        : Csla.Server.Hosts.HttpPortalController(applicationContext)
    {
        [HttpGet]
        public string Get()
        {
            return "DataPortal running...";
        }
    }
}
```

The `HttpGet` method is optional but useful for testing that the data portal endpoint is reachable.

### Server Configuration with CORS

When the client and server are on different origins (common with Blazor WebAssembly or mobile apps), configure CORS:

```csharp
const string BlazorClientPolicy = "AllowAllOrigins";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Support CORS so clients can call services
builder.Services.AddCors(options =>
{
    options.AddPolicy(BlazorClientPolicy,
        builder =>
        {
            builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors(BlazorClientPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Server Configuration with Authentication

When security principal flows from client to server:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCsla(o => o
    .Security(so => so.FlowSecurityPrincipalFromClient = true)
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

> **Note**: Setting `FlowSecurityPrincipalFromClient = true` allows the client to send user identity to the server. Use this only when you trust the client or have additional security measures in place.

### Server with Data Portal Dashboard

Register the data portal dashboard for monitoring and diagnostics:

```csharp
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal(ss => ss
            .RegisterDashboard<Csla.Server.Dashboard.Dashboard>())));
```

Create a controller to expose the dashboard:

```csharp
using Csla;
using Csla.Server.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataPortalController(ApplicationContext applicationContext, IDashboard dashboard) 
        : Csla.Server.Hosts.HttpPortalController(applicationContext)
    {
        private IDashboard Dashboard { get; } = dashboard;

        [HttpGet]
        public IDashboard Get() => Dashboard;
    }
}
```

## Part 2: Client-Side Configuration

The client-side configuration involves setting up the HTTP proxy to communicate with the server.

### Basic Client Configuration

Configure the HTTP proxy in your client application:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

### Configuring Timeouts

CSLA 9 and later support configuring timeout values for HTTP requests.

#### Using WithTimeout

Set the overall timeout for the HTTP request:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

#### Using WithReadWriteTimeout

Set the read/write timeout for the HTTP request:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithReadWriteTimeout(TimeSpan.FromSeconds(30))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

#### Configuring Both Timeouts

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

### HttpCompressionProxy

The `HttpCompressionProxy` compresses request and response data to reduce bandwidth usage:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpCompressionProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(60))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));
```

## Part 3: Complete Examples

### Example 1: Modern Blazor Web App

This example shows a complete modern Blazor application with InteractiveAuto render mode.

**Server Project (Program.cs):**

```csharp
using Csla.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add controller support for data portal
builder.Services.AddControllers();

// Add Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Configure authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
builder.Services.AddCascadingAuthenticationState();

// CSLA requires AddHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add CSLA with server-side Blazor support
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .AddServerSideBlazor(o => o.UseInMemoryApplicationContextManager = false)
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MyApp.Client._Imports).Assembly);

app.Run();
```

**Server Data Portal Controller:**

```csharp
using Csla;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataPortalController(ApplicationContext applicationContext) 
        : Csla.Server.Hosts.HttpPortalController(applicationContext)
    {
        [HttpGet]
        public string Get() => "DataPortal running...";
    }
}
```

**Server State Controller (for Blazor state management):**

```csharp
using Csla;
using Csla.State;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CslaStateController(ApplicationContext applicationContext, ISessionManager sessionManager) 
        : Csla.AspNetCore.Blazor.State.StateController(applicationContext, sessionManager)
    {
    }
}
```

**Client Project (Program.cs):**

```csharp
using Csla.Configuration;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMemoryCache();

builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) 
});

builder.Services.AddCsla(o => o
    .AddBlazorWebAssembly(o => o.SyncContextWithServer = true)
    .DataPortal(o => o.AddClientSideDataPortal(o => o
        .UseHttpProxy(o => o.DataPortalUrl = "/api/dataportal"))));

await builder.Build().RunAsync();
```

### Example 2: Standalone App Server

This example shows a dedicated app server that hosts only the data portal endpoint.

**App Server (Program.cs):**

```csharp
using Csla.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;

const string BlazorClientPolicy = "AllowAllOrigins";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Support CORS so clients can call services
builder.Services.AddCors(options =>
{
    options.AddPolicy(BlazorClientPolicy,
        builder =>
        {
            builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

builder.Services.AddCsla(o => o
    .Security(so => so.FlowSecurityPrincipalFromClient = true)
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

// Configure Kestrel if needed
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.AllowSynchronousIO = true;
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors(BlazorClientPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
```

**Console or WPF Client:**

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddTransient<HttpClient>();
services.AddCsla(o => o
    .DataPortal(dp => dp.AddClientSideDataPortal(co => co
        .UseHttpProxy(hp => hp
            .DataPortalUrl = "https://localhost:5001/api/dataportal"))));

var provider = services.BuildServiceProvider();
var applicationContext = provider.GetRequiredService<ApplicationContext>();

// Use the data portal
var portal = applicationContext.GetRequiredService<IDataPortal<MyBusinessObject>>();
var obj = await portal.FetchAsync(123);
```

### Example 3: MAUI Application with App Server

**MAUI Client (MauiProgram.cs):**

```csharp
using Csla.Configuration;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

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

### Example 4: WPF or Windows Forms

**WPF Client (App.xaml.cs):**

```csharp
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; }

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

        ServiceProvider = services.BuildServiceProvider();
    }
}
```

## Part 4: Common Scenarios

### Short Timeout for Quick Operations

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

### Long Timeout for Reports or Batch Operations

For applications with long-running server operations:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromMinutes(5))
                .WithReadWriteTimeout(TimeSpan.FromMinutes(3))
                .DataPortalUrl = "https://api.example.com/DataPortal"))));
```

### Environment-Specific Configuration

Use configuration files for environment-specific settings:

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

## Part 5: Advanced Topics

### Custom Data Portal Controller

Override methods in the controller for custom behavior:

```csharp
[Route("api/[controller]")]
[ApiController]
public class DataPortalController : Csla.Server.Hosts.HttpPortalController
{
    public DataPortalController(ApplicationContext applicationContext)
        : base(applicationContext) { }

    [HttpGet]
    public string Get() => "DataPortal running...";

    public override Task PostAsync([FromQuery] string operation)
    {
        // Add custom logging or processing here
        var result = base.PostAsync(operation);
        return result;
    }

    protected override Task PostAsync(string operation, string routingTag)
    {
        // Handle routing tag-based requests
        return base.PostAsync(operation, routingTag);
    }
}
```

### Data Portal Routing

Use routing tags to direct requests to different backend servers:

```csharp
[Route("api/[controller]")]
[ApiController]
public class DataPortalController : Csla.Server.Hosts.HttpPortalController
{
    public DataPortalController(ApplicationContext applicationContext)
        : base(applicationContext)
    {
        // Configure routing to different backend servers
        RoutingTagUrls["-v1"] = "http://server1.example.com/api/DataPortal";
        RoutingTagUrls["-v2"] = "http://server2.example.com/api/DataPortal";
    }
}
```

See the [DataPortalRouting](DataPortalRouting.md) document for detailed information.

### Custom HTTP Headers

Add custom headers to data portal requests:

```csharp
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

### Authentication Headers

Configure authentication headers with timeouts:

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

## Part 6: Understanding Timeouts

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
    Console.WriteLine("The operation took too long. Please try again.");
}
catch (DataPortalException ex) when (ex.InnerException is TaskCanceledException)
{
    // Handle cancellation (which may be due to timeout)
    Console.WriteLine("The operation was cancelled or timed out.");
}
```

## Best Practices

### Server-Side

1. **Use `AddHttpContextAccessor()`** - Required for CSLA ASP.NET Core integration
2. **Configure CORS appropriately** - Set up CORS only for trusted origins in production
3. **Use `AddServerSideDataPortal()`** - Required for the server to process data portal requests
4. **Consider the data portal dashboard** - Useful for monitoring and diagnostics

### Client-Side

1. **Set appropriate timeouts** - Consider network conditions and operation complexity
2. **Configure timeouts in settings** - Use `appsettings.json` for environment-specific configuration
3. **Use compression for large payloads** - `HttpCompressionProxy` can significantly reduce bandwidth
4. **Handle timeout exceptions** - Always catch and handle timeout exceptions gracefully
5. **Consider mobile networks** - Mobile applications may need longer timeouts

### Security

1. **Use HTTPS** - Always use HTTPS for production deployments
2. **Be cautious with `FlowSecurityPrincipalFromClient`** - Only enable when necessary and with additional security measures
3. **Use server-side authentication** - Prefer authenticating users on the server, especially for Blazor apps
4. **Configure CORS restrictively** - Don't use `AllowAnyOrigin()` in production

## Troubleshooting

### Common Server Issues

- **404 Not Found**: Ensure `app.MapControllers()` is called and the controller route is correct
- **500 Internal Server Error**: Check server logs for details; ensure all dependencies are registered
- **CORS Errors**: Verify CORS policy is configured and applied correctly

### Common Client Issues

- **Frequent Timeouts**: Increase timeout values or optimize server operations
- **Connection Refused**: Verify the server is running and the URL is correct
- **Serialization Errors**: Ensure business objects are serializable and available on both client and server

### Debugging Tips

1. Add the `[HttpGet]` method to your data portal controller to verify the endpoint is reachable
2. Enable detailed logging on both client and server
3. Use browser developer tools or Fiddler to inspect HTTP traffic
4. Check that the data portal URL matches between client configuration and server route

## Notes

- Timeout configuration is available in CSLA 9 and later
- The data portal URL can be relative (for same-origin scenarios) or absolute (for cross-origin scenarios)
- Both `HttpProxy` and `HttpCompressionProxy` support the same timeout configuration
- Server-side configuration requires the `Csla.AspNetCore` NuGet package
