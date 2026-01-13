# gRPC Data Portal Configuration

The CSLA data portal supports gRPC-based communication between client and server through `GrpcProxy` (client-side) and `GrpcPortal` (server-side). This document covers configuration for both ends of the gRPC data portal channel.

## Overview

gRPC (gRPC Remote Procedure Calls) is a high-performance, language-agnostic RPC framework that uses HTTP/2 for transport and Protocol Buffers for serialization. The CSLA gRPC channel provides an alternative to the HTTP channel with potential performance benefits for high-throughput scenarios.

The architecture consists of:

- **Client-Side**: `GrpcProxy` class that serializes and sends data portal requests over gRPC
- **Server-Side**: `GrpcPortal` service that receives and processes data portal requests

Both ends must be properly configured for the gRPC data portal channel to work.

## Prerequisites

The gRPC channel requires the `Csla.Channels.Grpc` NuGet package:

```xml
<PackageReference Include="Csla.Channels.Grpc" Version="9.0.0" />
```

For the server, you also need the standard ASP.NET Core gRPC package:

```xml
<PackageReference Include="Grpc.AspNetCore" Version="2.60.0" />
```

## Part 1: Server-Side Configuration

The server-side configuration involves setting up ASP.NET Core to host the gRPC service endpoint.

### Basic ASP.NET Core Server Setup

Configure gRPC and CSLA services in your server's `Program.cs`:

```csharp
using Csla.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc();

// CSLA requires AddHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add CSLA with server-side data portal
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

// Map the gRPC data portal service
app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>();

// Optional: Add a simple endpoint to verify the server is running
app.MapGet("/", () => "gRPC Data Portal Server is running. Communication must be made through a gRPC client.");

app.Run();
```

### Kestrel Configuration for gRPC

gRPC requires HTTP/2. Configure Kestrel to support HTTP/2:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    // Configure HTTP/2 endpoint with TLS (recommended for production)
    options.ListenLocalhost(5001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

builder.Services.AddGrpc();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>();

app.Run();
```

### HTTP/2 Without TLS (Development Only)

For development scenarios, you can configure HTTP/2 without TLS:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP/2 without TLS - development only!
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});
```

> **Warning**: HTTP/2 without TLS should only be used in development. Production deployments should always use TLS.

### Configuration via appsettings.json

Configure Kestrel endpoints in `appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Grpc": {
        "Url": "https://localhost:5001",
        "Protocols": "Http2"
      }
    }
  }
}
```

For development with HTTP/2 without TLS:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Grpc": {
        "Url": "http://localhost:5000",
        "Protocols": "Http2"
      }
    },
    "EndpointDefaults": {
      "Protocols": "Http2"
    }
  }
}
```

### Server with Authentication

Configure the server to accept security principal from client:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCsla(o => o
    .Security(so => so.FlowSecurityPrincipalFromClient = true)
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>();

app.Run();
```

### Server with Data Portal Interceptors

Add data portal interceptors for logging, transactions, etc.:

```csharp
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal(ss => ss
            .AddInterceptorProvider<MyCustomInterceptor>())));
```

## Part 2: Client-Side Configuration

The client-side configuration involves setting up the gRPC proxy to communicate with the server.

### Basic Client Configuration

Configure the gRPC proxy in your client application:

```csharp
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseGrpcProxy(proxy => proxy
                .DataPortalUrl = "https://localhost:5001"))));

var provider = services.BuildServiceProvider();
```

### Console Application Client

Complete configuration for a console application:

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseGrpcProxy(proxy => proxy
                .DataPortalUrl = "https://localhost:5001"))));

var provider = services.BuildServiceProvider();
var applicationContext = provider.GetRequiredService<ApplicationContext>();

// Use the data portal
var portal = applicationContext.GetRequiredService<IDataPortal<MyBusinessObject>>();
var obj = await portal.FetchAsync(123);
```

### WPF Application Client

Configuration for a WPF application:

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
                    .UseGrpcProxy(proxy => proxy
                        .DataPortalUrl = "https://myserver.com:5001"))));

        ServiceProvider = services.BuildServiceProvider();
    }
}
```

### MAUI Application Client

Configuration for a MAUI application:

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
                    .UseGrpcProxy(proxy => proxy
                        .DataPortalUrl = "https://myserver.com:5001"))));

        return builder.Build();
    }
}
```

## Part 3: Complete Examples

### Example 1: Simple gRPC Data Portal Server and Client

**Server (Program.cs):**

```csharp
using Csla.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

// Add gRPC services
builder.Services.AddGrpc();
builder.Services.AddHttpContextAccessor();

// Add CSLA
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

// Register your DAL services
builder.Services.AddTransient<IPersonDal, PersonDal>();

var app = builder.Build();

// Map the gRPC data portal endpoint
app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>();

app.MapGet("/", () => "gRPC Data Portal Server running");

app.Run();
```

**Client (Program.cs):**

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseGrpcProxy(proxy => proxy
                .DataPortalUrl = "https://localhost:5001"))));

var provider = services.BuildServiceProvider();
var applicationContext = provider.GetRequiredService<ApplicationContext>();

var portal = applicationContext.GetRequiredService<IDataPortal<PersonEdit>>();
var person = await portal.FetchAsync("John");
Console.WriteLine($"Person: {person.Name}");
```

### Example 2: Server with CORS and Authentication

**Server (Program.cs):**

```csharp
using Csla.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

// Add gRPC with options
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16 MB
    options.MaxSendMessageSize = 16 * 1024 * 1024; // 16 MB
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddHttpContextAccessor();

// Add CSLA with security
builder.Services.AddCsla(o => o
    .Security(so => so.FlowSecurityPrincipalFromClient = true)
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

var app = builder.Build();

// Enable gRPC-Web for browser clients (optional)
app.UseGrpcWeb();

app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>().EnableGrpcWeb();

app.Run();
```

### Example 3: Environment-Specific Configuration

**appsettings.json:**
```json
{
  "DataPortal": {
    "Url": "https://api.example.com:5001"
  },
  "Kestrel": {
    "Endpoints": {
      "Grpc": {
        "Url": "https://localhost:5001",
        "Protocols": "Http2"
      }
    }
  }
}
```

**appsettings.Development.json:**
```json
{
  "DataPortal": {
    "Url": "https://localhost:5001"
  }
}
```

**Client Configuration:**
```csharp
public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    var dataPortalUrl = configuration["DataPortal:Url"];

    services.AddCsla(options => options
        .DataPortal(dp => dp
            .AddClientSideDataPortal(csp => csp
                .UseGrpcProxy(proxy => proxy
                    .DataPortalUrl = dataPortalUrl))));
}
```

## Part 4: Advanced Topics

### Custom GrpcChannel Configuration

For advanced scenarios, you can configure the GrpcChannel directly:

```csharp
using Grpc.Net.Client;

// Create a custom channel with specific options
var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
{
    MaxReceiveMessageSize = 16 * 1024 * 1024, // 16 MB
    MaxSendMessageSize = 16 * 1024 * 1024, // 16 MB
    HttpHandler = new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        EnableMultipleHttp2Connections = true
    }
});

// Register the custom channel
services.AddSingleton(channel);

services.AddCsla(o => o
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseGrpcProxy(proxy => proxy
                .DataPortalUrl = "https://localhost:5001"))));
```

### Data Portal Routing with gRPC

The `GrpcPortal` supports routing tags to direct requests to different backend servers:

```csharp
// Server-side: Override GrpcPortal to configure routing
public class CustomGrpcPortal : Csla.Channels.Grpc.GrpcPortal
{
    public CustomGrpcPortal(Csla.Server.IDataPortalServer dataPortal, Csla.ApplicationContext applicationContext)
        : base(dataPortal, applicationContext)
    {
        // Configure routing to different backend servers
        RoutingTagUrls["-v1"] = "https://server1.example.com:5001";
        RoutingTagUrls["-v2"] = "https://server2.example.com:5001";
    }
}
```

Register the custom portal:

```csharp
app.MapGrpcService<CustomGrpcPortal>();
```

### gRPC Server Reflection

Enable server reflection for debugging and testing tools:

```csharp
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}
```

### gRPC Health Checks

Add gRPC health checks for monitoring:

```csharp
builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();

var app = builder.Build();

app.MapGrpcService<Csla.Channels.Grpc.GrpcPortal>();
app.MapGrpcHealthChecksService();
```

## Part 5: gRPC vs HTTP Data Portal

### When to Use gRPC

Consider using gRPC when:

- **Performance is critical**: gRPC uses HTTP/2 with binary serialization, which is more efficient than HTTP/1.1 with JSON
- **Streaming is needed**: gRPC supports bidirectional streaming
- **Type safety is important**: Protocol Buffers provide strong typing
- **Microservices architecture**: gRPC is designed for service-to-service communication

### When to Use HTTP

Consider using HTTP when:

- **Browser clients**: Standard HTTP is easier to consume from browsers (though gRPC-Web exists)
- **Simpler debugging**: HTTP with JSON is easier to inspect and debug
- **Wider compatibility**: HTTP/1.1 works everywhere
- **Firewall considerations**: Some firewalls may block HTTP/2

### Feature Comparison

| Feature | HTTP Channel | gRPC Channel |
|---------|--------------|--------------|
| Protocol | HTTP/1.1 or HTTP/2 | HTTP/2 only |
| Serialization | CSLA serialization | CSLA serialization (in protobuf wrapper) |
| Browser support | Direct | Requires gRPC-Web |
| Streaming | No | Yes |
| Performance | Good | Better (binary, multiplexing) |
| Timeout configuration | Yes | Via channel options |
| Compression | Optional (HttpCompressionProxy) | Built into HTTP/2 |

## Best Practices

### Server-Side

1. **Always use TLS in production** - HTTP/2 without TLS is for development only
2. **Configure appropriate message sizes** - Set `MaxReceiveMessageSize` and `MaxSendMessageSize` based on your needs
3. **Enable health checks** - Use gRPC health checks for load balancers and orchestrators
4. **Use server reflection in development** - Helps with debugging and testing

### Client-Side

1. **Reuse channels** - GrpcChannel represents a long-lived connection; reuse it across calls
2. **Configure keepalive** - Set appropriate keepalive settings for long-running applications
3. **Handle connection failures** - Implement retry logic for transient failures
4. **Use environment-specific configuration** - Different URLs for development vs production

### Security

1. **Use HTTPS** - Always use TLS in production
2. **Be cautious with `FlowSecurityPrincipalFromClient`** - Only enable when necessary
3. **Validate certificates** - Don't disable certificate validation in production

## Troubleshooting

### Common Issues

#### Connection Refused
- Verify the server is running
- Check that the URL and port are correct
- Ensure HTTP/2 is configured on both client and server

#### HTTP/2 Not Supported
- Ensure Kestrel is configured for HTTP/2
- Check that TLS is properly configured (ALPN requires TLS)
- For development without TLS, set `HttpProtocols.Http2` explicitly

#### Certificate Errors
- In development, trust the ASP.NET Core development certificate: `dotnet dev-certs https --trust`
- In production, use properly issued certificates

#### Message Size Exceeded
- Configure `MaxReceiveMessageSize` and `MaxSendMessageSize` on both client and server
- Consider if you're transferring too much data in a single call

### Debugging Tips

1. **Enable detailed errors** in development:
   ```csharp
   builder.Services.AddGrpc(options =>
   {
       options.EnableDetailedErrors = true;
   });
   ```

2. **Enable gRPC logging**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Grpc": "Debug"
       }
     }
   }
   ```

3. **Use gRPC server reflection** with tools like grpcurl or BloomRPC

4. **Check server logs** for detailed error information

## Notes

- gRPC requires HTTP/2, which requires TLS in most production scenarios
- The CSLA gRPC channel uses the same serialization as other channels (MobileFormatter)
- Business objects don't need any changes to work with gRPC vs HTTP
- The gRPC channel is available in CSLA 6 and later
- For Blazor WebAssembly, consider gRPC-Web or the standard HTTP channel
