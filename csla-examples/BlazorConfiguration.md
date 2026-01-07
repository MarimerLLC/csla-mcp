# Blazor Configuration

CSLA supports various Blazor application architectures, including modern Blazor with multiple render modes, legacy Blazor Server, and legacy Blazor WebAssembly. The configuration differs based on your Blazor architecture and requirements.

## Overview

Blazor applications can use different render modes:
- **Server-static**: Rendered on the server without interactivity
- **Server-interactive**: Interactive components running on the server with SignalR
- **WebAssembly-interactive**: Interactive components running in the browser
- **Auto**: Automatically chooses between Server and WebAssembly based on availability

CSLA configuration must account for these different modes and whether state needs to be synchronized between client and server.

## Modern Blazor App (Solution Template)

The modern Blazor solution template creates two projects: a server-side project and a client-side project. This configuration supports all render modes, including InteractiveAuto which can switch between server and WebAssembly rendering.

> **Security Note:** For Blazor Web Apps using InteractiveAuto or multiple render modes, it is **strongly recommended** to authenticate users on the server using SSR (Server-Side Rendering) login processes, and have the user identity flow from server to client via the CSLA Blazor state management subsystem. **Avoid** setting `FlowSecurityPrincipalFromClient = true` as this creates potential security vulnerabilities by allowing client-side code to specify the user identity.

### Server-Side Project Configuration (Recommended)

Configure CSLA in the server project's `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure authentication (e.g., Cookie, Identity, etc.)
builder.Services.AddAuthentication()
    .AddCookie();

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .AddServerSideBlazor(blazor => blazor
        .UseInMemoryApplicationContextManager = false));

var app = builder.Build();

// Configure middleware
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>();

app.Run();
```

**Key Configuration Points:**

- `AddAspNetCore()` - Enables ASP.NET Core integration
- `UseInMemoryApplicationContextManager = false` - Uses state management subsystem for multi-mode support
- Authentication configured on the server - User identity is established server-side
- **No** `FlowSecurityPrincipalFromClient` - Security principal flows from server to client automatically via state management

### Client-Side Project Configuration (Recommended)

Configure CSLA in the client project's `Program.cs`:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly(blazor => blazor
        .SyncContextWithServer = true)
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "/api/DataPortal"))));

await builder.Build().RunAsync();
```

**Key Configuration Points:**

- `AddBlazorWebAssembly()` - Enables Blazor WebAssembly integration
- `SyncContextWithServer = true` - Synchronizes application context (including user identity) from server to client
- **No** `FlowSecurityPrincipalFromClient` - Client receives user identity from server
- `UseHttpProxy()` - Configures HTTP communication with the server data portal

### Alternative Configuration (Not Recommended)

While it is technically possible to configure `FlowSecurityPrincipalFromClient = true` to allow the client to send the user identity to the server, this approach is **not recommended** due to security concerns:

```csharp
// NOT RECOMMENDED - Security risk
.Security(security => security
    .FlowSecurityPrincipalFromClient = true)
```

This configuration should only be used in specific scenarios where:
- You have a legacy application that requires this behavior
- You have implemented additional security measures to validate client-provided identities
- You fully understand the security implications

### Server Data Portal Controller

Add a data portal controller in the server project:

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

## Legacy Blazor Server

For traditional Blazor Server applications (single project, server-side only), there is no client/server boundary and therefore no need for identity flow configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure authentication
builder.Services.AddAuthentication()
    .AddCookie();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .AddServerSideBlazor(blazor => blazor
        .UseInMemoryApplicationContextManager = true));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

**Key Configuration Points:**

- `UseInMemoryApplicationContextManager = true` - Uses in-memory context for server-only scenarios
- No data portal proxy needed since everything runs on the server
- **No identity flow concerns** - Everything executes in the same server-side context
- Authentication is handled through standard ASP.NET Core mechanisms

## Legacy Blazor WebAssembly

For traditional Blazor WebAssembly applications (client-only), there is no server-side CSLA execution:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

builder.Services.AddCsla(options => options
    .AddClientSideBlazor());

await builder.Build().RunAsync();
```

**Key Configuration Points:**

- `AddClientSideBlazor()` - Minimal configuration for client-side only
- No server communication configured (suitable for standalone WebAssembly)
- **No identity flow concerns** - Everything executes in the browser context
- Authentication would be handled separately if the app calls backend APIs

## Blazor WebAssembly with Server API

When your Blazor WebAssembly app needs to communicate with a server API:

### Client Configuration

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly()
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(60))
                .DataPortalUrl = "https://myserver.com/api/DataPortal"))));

await builder.Build().RunAsync();
```

### Server Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:5001")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();
```

## Common Scenarios

### Scenario 1: Modern Blazor with Auto Render Mode (Recommended)

Complete configuration for a modern Blazor app supporting all render modes with server-side authentication:

**Server Project (Program.cs):**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure authentication on the server
builder.Services.AddAuthentication()
    .AddCookie();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .AddServerSideBlazor(blazor => blazor
        .UseInMemoryApplicationContextManager = false)
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

app.MapControllers();

app.Run();
```

**Client Project (Program.cs):**

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly(blazor => blazor
        .SyncContextWithServer = true)
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "/api/DataPortal"))));

await builder.Build().RunAsync();
```

**Security Notes:**
- User authenticates on the server using standard ASP.NET Core authentication
- User identity flows from server to client via CSLA state management (`SyncContextWithServer`)
- No `FlowSecurityPrincipalFromClient` configuration needed or recommended

### Scenario 2: Blazor Server with Authentication

Blazor Server app with authentication:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .AddServerSideBlazor(blazor => blazor
        .UseInMemoryApplicationContextManager = true));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

### Scenario 3: Blazor WebAssembly with Custom Headers

Client configuration with authentication headers:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddHttpClient("CslaDataPortal", client =>
{
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
});

builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly()
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "/api/DataPortal"))));

await builder.Build().RunAsync();
```

### Scenario 4: Environment-Based Configuration

Different configuration for development and production:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

var dataPortalUrl = builder.HostEnvironment.IsDevelopment()
    ? "https://localhost:5001/api/DataPortal"
    : "https://api.production.com/api/DataPortal";

builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly()
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .WithTimeout(TimeSpan.FromSeconds(
                    builder.HostEnvironment.IsDevelopment() ? 300 : 60))
                .DataPortalUrl = dataPortalUrl))));

await builder.Build().RunAsync();
```

## State Management

### In-Memory Context Manager

Use when state only needs to persist within a single execution context:

```csharp
.AddServerSideBlazor(blazor => blazor
    .UseInMemoryApplicationContextManager = true)
```

**Use for:**
- Legacy Blazor Server apps
- Single render mode scenarios

### Persistent Context Manager

Use when state needs to persist across multiple execution contexts:

```csharp
.AddServerSideBlazor(blazor => blazor
    .UseInMemoryApplicationContextManager = false)
```

**Use for:**
- Modern Blazor apps with multiple render modes
- Apps that switch between server and WebAssembly rendering

## Security Configuration

### Recommended Approach: Server-Side Authentication

For Blazor Web Apps with multiple render modes (InteractiveAuto), **always authenticate users on the server** using standard ASP.NET Core authentication mechanisms:

```csharp
// Server-side configuration
builder.Services.AddAuthentication()
    .AddCookie();

builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .AddServerSideBlazor(blazor => blazor
        .UseInMemoryApplicationContextManager = false));
```

```csharp
// Client-side configuration
builder.Services.AddCsla(options => options
    .AddBlazorWebAssembly(blazor => blazor
        .SyncContextWithServer = true));
```

The user identity automatically flows from server to client through the CSLA state management subsystem when `SyncContextWithServer = true` is set.

### Flow Security Principal (Not Recommended)

While technically supported, flowing the security principal from client to server is **not recommended** due to security concerns:

```csharp
// NOT RECOMMENDED - potential security vulnerability
.Security(security => security
    .FlowSecurityPrincipalFromClient = true)
```

This configuration allows client-side code to specify the user identity, which creates a security risk. Only use this if:
- You have a legacy application requiring this behavior
- You have implemented additional security measures to validate client-provided identities
- You fully understand the security implications

### Custom Authorization

Implement custom authorization rules on the server:

```csharp
builder.Services.AddCsla(options => options
    .AddAspNetCore()
    .Security(security =>
    {
        security.AuthorizationRuleType = typeof(MyCustomAuthorizationRules);
    }));
```

## Best Practices

1. **Authenticate on the server** - For multi-mode Blazor apps, use server-side SSR authentication
2. **Choose the right context manager** - Use in-memory for server-only, persistent for multi-mode
3. **Sync context for multi-mode apps** - Set `SyncContextWithServer = true` to flow state from server to client
4. **Avoid client-to-server identity flow** - Do not set `FlowSecurityPrincipalFromClient = true` in production
5. **Configure timeouts appropriately** - WebAssembly apps may need longer timeouts than server apps
6. **Use HTTPS in production** - Always use HTTPS for data portal URLs in production
7. **Configure CORS properly** - Ensure CORS is configured correctly for cross-origin scenarios
8. **Test all render modes** - If using Auto mode, test with both server and WebAssembly rendering
9. **Handle connection failures** - Implement proper error handling for network issues
10. **Use compression** - Consider `HttpCompressionProxy` for large data transfers

## Troubleshooting

### State Not Persisting

If state doesn't persist across render mode changes:
- Ensure `UseInMemoryApplicationContextManager = false` on the server
- Verify `SyncContextWithServer = true` on the client

### Authentication Not Working

If authentication isn't working in a multi-mode Blazor app:
- Verify authentication is configured on the server (not the client)
- Ensure `UseAuthentication()` and `UseAuthorization()` middleware are added to the server pipeline
- Check that `SyncContextWithServer = true` is set on the client
- Verify `UseInMemoryApplicationContextManager = false` is set on the server
- Ensure the data portal controller is using the correct ApplicationContext
- Check that you are **not** using `FlowSecurityPrincipalFromClient = true` (this is not recommended)

### Data Portal Calls Failing

If data portal calls fail:
- Verify the data portal URL is correct
- Check that the server has the data portal controller
- Ensure CORS is configured if client and server are on different origins
- Check browser console and server logs for errors

## Notes

- Modern Blazor apps require careful configuration to support all render modes
- The `UseInMemoryApplicationContextManager` setting is critical for state management
- **Security Recommendation**: Authenticate users on the server and let identity flow from server to client via state management
- **Avoid** `FlowSecurityPrincipalFromClient = true` in production - this creates security vulnerabilities
- For pure server-side or pure WebAssembly apps, identity flow is not a concern as there is no client/server boundary
- Always test with the actual render modes you'll use in production
