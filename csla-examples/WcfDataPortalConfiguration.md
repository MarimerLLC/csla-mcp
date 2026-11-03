# WCF Data Portal Configuration

The CSLA data portal can communicate between client and server over Windows Communication Foundation (WCF) through `WcfProxy` (client-side) and `WcfPortal` (server-side). This document covers configuration for both ends of the WCF data portal channel.

> **Applies to CSLA 10.1 and later.** The `Csla.Channels.Wcf` package first shipped with CSLA 10.1.0. It isn't available in CSLA 9 or 10.0.

## Overview

The WCF channel is useful when:

- You're modernizing an application that already uses WCF infrastructure (bindings, security, hosting)
- You need a .NET Framework data portal server, or a .NET Framework client talking to a modern .NET server
- You want a protocol or binding that WCF supports and the HTTP/gRPC channels don't

The architecture is:

- **Client-side**: `WcfProxy` (namespace `Csla.Channels.Wcf.Client`), registered with `UseWcfProxy()`
- **Server-side**: `WcfPortal` (namespace `Csla.Channels.Wcf.Server`), registered with `UseWcfPortal()`
  - On **modern .NET**, the server is hosted with **CoreWCF** inside ASP.NET Core.
  - On **.NET Framework**, the server is hosted with classic `System.ServiceModel`, self-hosted through `WcfPortalHost` (a `ServiceHost`).

Client and server don't have to target the same framework. A .NET Framework client can call a CoreWCF server, and a modern .NET client can call a .NET Framework server.

## Prerequisites

Add the `Csla.Channels.Wcf` package to **both** the client and the server, using the same version as your `Csla` package:

```xml
<PackageReference Include="Csla.Channels.Wcf" Version="10.2.0" />
```

| CSLA version | `Csla.Channels.Wcf` target frameworks |
| --- | --- |
| 10.1 / 10.2 | `net462`, `net472`, `net48`, `net8.0`, `net9.0`, `net10.0` |
| 11 | `net462`, `net472`, `net48`, `net10.0`, `net11.0` |

On modern .NET the package brings in `CoreWCF.Primitives` and `CoreWCF.Http` (server) and `System.ServiceModel.Primitives` and `System.ServiceModel.Http` (client). On .NET Framework it uses the built-in `System.ServiceModel` assemblies. The package has no `netstandard2.0` target.

## Part 1: Server-Side Configuration

### Modern .NET Server (ASP.NET Core + CoreWCF)

```csharp
using CoreWCF.Configuration;
using CoreWCF.Description;
using Csla.Channels.Wcf.Server;
using Csla.Configuration;

var builder = WebApplication.CreateBuilder(args);

// CoreWCF services
builder.Services.AddServiceModelServices();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

// Your DAL and other services used by data portal operations
builder.Services.AddScoped<IPersonDal, PersonDal>();

// CSLA with the WCF data portal server
builder.Services.AddCsla(csla => csla
    .DataPortal(dp => dp
        .AddServerSideDataPortal(p => p
            .UseWcfPortal())));

var app = builder.Build();

var wcfPortalOptions = app.Services.GetRequiredService<WcfPortalOptions>();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<WcfPortal>()
        .AddServiceEndpoint<WcfPortal, IWcfPortalServer>(
            wcfPortalOptions.Binding,
            wcfPortalOptions.DataPortalUrl);
});

app.Run();
```

`UseWcfPortal()` registers `WcfPortalOptions` as a singleton and registers `WcfPortal` as a transient service, so CoreWCF creates the portal through DI. You still have to map the endpoint with `UseServiceModel`, as shown.

On modern .NET, `WcfPortalOptions` has these defaults:

| Property | Type | Default |
| --- | --- | --- |
| `Binding` | `CoreWCF.Channels.Binding` | `new CoreWCF.BasicHttpBinding()` |
| `DataPortalUrl` | `string` | `"/"` (endpoint path, relative to the ASP.NET Core base address) |
| `RouterBinding` | `System.ServiceModel.Channels.Binding?` | `null` (only needed for [routing](#data-portal-routing)) |

Configure them with the `UseWcfPortal` callback:

```csharp
using CoreWCF;

builder.Services.AddCsla(csla => csla
    .DataPortal(dp => dp
        .AddServerSideDataPortal(p => p
            .UseWcfPortal(o =>
            {
                o.DataPortalUrl = "/DataPortal";
                o.Binding = new BasicHttpBinding
                {
                    MaxReceivedMessageSize = 10 * 1024 * 1024
                };
            }))));
```

The endpoint is then at `<server base address>/DataPortal`, for example `https://appserver.example.com/DataPortal`.

> **Note:** on the server, `Binding` is a **CoreWCF** binding (`CoreWCF.BasicHttpBinding`, namespace `CoreWCF`), not a `System.ServiceModel` binding.

### .NET Framework Server (self-hosted)

On .NET Framework, `UseWcfPortal()` registers a singleton `WcfPortalHost` that already has an endpoint for `IWcfPortalServer`, using `WcfPortalOptions.Binding` and `WcfPortalOptions.DataPortalUrl`. Resolve the host and open it:

```csharp
using System;
using System.ServiceModel;
using Csla.Channels.Wcf.Server;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<IPersonDal, PersonDal>();
        services.AddCsla(csla => csla
            .DataPortal(dp => dp
                .AddServerSideDataPortal(p => p
                    .UseWcfPortal(o =>
                    {
                        o.DataPortalUrl = "http://localhost:8080/DataPortal";
                        o.Binding = new BasicHttpBinding
                        {
                            MaxReceivedMessageSize = 10 * 1024 * 1024
                        };
                    }))));

        var provider = services.BuildServiceProvider();

        var wcfHost = provider.GetRequiredService<WcfPortalHost>();
        wcfHost.Open();

        Console.WriteLine("WCF data portal listening. Press Enter to stop.");
        Console.ReadLine();

        wcfHost.Close();
    }
}
```

On .NET Framework, `WcfPortalOptions.Binding` is a `System.ServiceModel.Channels.Binding` (default `new BasicHttpBinding()`), and `DataPortalUrl` is an absolute address (default `"http://localhost"`).

Self-hosting an HTTP endpoint on Windows may need an HTTP URL reservation (`netsh http add urlacl`) when the process doesn't run as an administrator.

### Server-Side Data Portal Options

`UseWcfPortal` is an extension on the server-side data portal options, so you can combine it with other server settings, such as interceptors or flowing the principal from the client:

```csharp
builder.Services.AddCsla(csla => csla
    .Security(s => s.FlowSecurityPrincipalFromClient = false)
    .DataPortal(dp => dp
        .AddServerSideDataPortal(p => p
            .AddInterceptorProvider<MyInterceptor>()
            .UseWcfPortal(o => o.DataPortalUrl = "/DataPortal"))));
```

## Part 2: Client-Side Configuration

Client configuration is the same on modern .NET and .NET Framework, and doesn't depend on how the server is hosted.

```csharp
using System.ServiceModel;
using Csla.Channels.Wcf.Client;
using Csla.Configuration;

services.AddCsla(csla => csla
    .DataPortal(dp => dp
        .AddClientSideDataPortal(c => c
            .UseWcfProxy(o =>
            {
                o.DataPortalUrl = "https://appserver.example.com/DataPortal";
                o.Binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
                {
                    MaxReceivedMessageSize = 10 * 1024 * 1024
                };
            }))));
```

`WcfProxyOptions`:

| Property | Type | Default |
| --- | --- | --- |
| `Binding` | `System.ServiceModel.Channels.Binding` | `new BasicHttpBinding()` |
| `DataPortalUrl` | `string` | `"http://localhost"` |

On the client, the binding is **always** a `System.ServiceModel` binding, even on modern .NET. The client binding must match the server binding: the same binding type, the same security mode, and compatible message size limits.

`UseWcfProxy()` with no callback uses the defaults. That only works if the server listens at `http://localhost` with a default `BasicHttpBinding`, which is rarely true. Always set `DataPortalUrl`.

### WPF / Windows Forms Client

```csharp
using System.ServiceModel;
using Csla.Channels.Wcf.Client;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host = new HostBuilder()
    .ConfigureServices((context, services) => services
        .AddTransient<MainWindow>()
        .AddCsla(options => options
            .AddXaml()
            .DataPortal(dp => dp
                .AddClientSideDataPortal(c => c
                    .UseWcfProxy(o =>
                    {
                        o.DataPortalUrl = "http://appserver:8080/DataPortal";
                        o.Binding = new BasicHttpBinding();
                    })))))
    .Build();
```

### Using the Data Portal

The client code doesn't change. Inject `IDataPortal<T>` and call it as usual:

```csharp
public class PersonViewModel(IDataPortal<PersonEdit> portal)
{
    public async Task<PersonEdit> LoadAsync(int id) => await portal.FetchAsync(id);
}
```

`WcfProxy` supports both async and sync data portal calls.

## Part 3: Complete Example (CoreWCF Server, .NET Client)

### Server `Program.cs`

```csharp
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Csla.Channels.Wcf.Server;
using Csla.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceModelServices();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddScoped<IPersonDal, PersonDal>();

builder.Services.AddCsla(csla => csla
    .DataPortal(dp => dp
        .AddServerSideDataPortal(p => p
            .UseWcfPortal(o =>
            {
                o.DataPortalUrl = "/DataPortal";
                o.Binding = new BasicHttpBinding { MaxReceivedMessageSize = 10 * 1024 * 1024 };
            }))));

var app = builder.Build();

var options = app.Services.GetRequiredService<WcfPortalOptions>();
app.UseServiceModel(sb =>
{
    sb.AddService<WcfPortal>()
      .AddServiceEndpoint<WcfPortal, IWcfPortalServer>(options.Binding, options.DataPortalUrl);
});

app.Run();
```

With `launchSettings.json` or `ASPNETCORE_URLS` set to `http://localhost:5080`, the endpoint is `http://localhost:5080/DataPortal`.

### Client

```csharp
using System.ServiceModel;
using Csla;
using Csla.Channels.Wcf.Client;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCsla(csla => csla
    .DataPortal(dp => dp
        .AddClientSideDataPortal(c => c
            .UseWcfProxy(o =>
            {
                o.DataPortalUrl = "http://localhost:5080/DataPortal";
                o.Binding = new BasicHttpBinding { MaxReceivedMessageSize = 10 * 1024 * 1024 };
            }))));

var provider = services.BuildServiceProvider();
var portal = provider.GetRequiredService<IDataPortal<PersonEdit>>();

var person = await portal.FetchAsync(1);
person.Name = "Updated";
person = await person.SaveAsync();
```

The business library (with `PersonEdit` and its data portal operation methods) must be deployed to both client and server, as with any remote data portal channel.

## Part 4: Advanced Topics

### Message Size and Timeouts

CSLA sends serialized object graphs in the message body. Large object graphs can exceed WCF's default `MaxReceivedMessageSize` (64 KB). Raise it on **both** client and server bindings. Raise reader quotas too if WCF reports a quota error. Timeouts are set on the binding (`SendTimeout`, `ReceiveTimeout`, `OpenTimeout`, `CloseTimeout`):

```csharp
// Client (System.ServiceModel)
o.Binding = new BasicHttpBinding
{
    MaxReceivedMessageSize = 50 * 1024 * 1024,
    SendTimeout = TimeSpan.FromMinutes(5)
};
```

### Data Portal Routing

The WCF channel supports routing tags (`[DataPortalServerRoutingTag]`) and version routing (`DataPortalOptions.VersionRoutingTag`), like the HTTP channel. The client adds the routing and version tags to the operation. The server looks up the tag in the `protected static Dictionary<string, string> RoutingTagUrls` on `WcfPortal`. If it finds a URL other than `"localhost"`, it forwards the request to that WCF endpoint. Otherwise it processes the request locally.

To configure routing on a CoreWCF server, derive from `WcfPortal`, fill `RoutingTagUrls`, and set `RouterBinding`. That's the `System.ServiceModel` binding used to call the target server:

```csharp
using CoreWCF;
using Csla;
using Csla.Channels.Wcf.Server;
using Csla.Server;

[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
public class RoutingWcfPortal : WcfPortal
{
    public RoutingWcfPortal(IDataPortalServer dataPortal, ApplicationContext applicationContext)
        : base(dataPortal, applicationContext)
    {
        // Key format: "{routingTag}-{versionTag}"
        RoutingTagUrls["-v1"] = "http://appserver-v1:5080/DataPortal";
        RoutingTagUrls["-v2"] = "http://appserver-v2:5080/DataPortal";
        RoutingTagUrls["local-"] = "localhost";
    }
}
```

```csharp
builder.Services.AddTransient<RoutingWcfPortal>();
builder.Services.AddCsla(csla => csla
    .DataPortal(dp => dp
        .AddServerSideDataPortal(p => p
            .UseWcfPortal(o =>
            {
                o.DataPortalUrl = "/DataPortal";
                o.RouterBinding = new System.ServiceModel.BasicHttpBinding();
            }))));

// ...
app.UseServiceModel(sb =>
{
    sb.AddService<RoutingWcfPortal>()
      .AddServiceEndpoint<RoutingWcfPortal, IWcfPortalServer>(options.Binding, options.DataPortalUrl);
});
```

On .NET Framework, `RouterBinding` isn't settable. Routed calls reuse `WcfPortalOptions.Binding`. Also, `WcfPortalHost` always creates the base `WcfPortal` type. See [DataPortalRouting.md](DataPortalRouting.md) for the routing concepts.

### Customizing the Server Portal

`WcfPortal` has the same extension points as the other channel portals:

- `protected virtual CriteriaRequest ConvertRequest(CriteriaRequest request)`
- `protected virtual UpdateRequest ConvertRequest(UpdateRequest request)`
- `protected virtual DataPortalResponse ConvertResponse(DataPortalResponse response)`
- `protected virtual Task<WcfResponse> RouteMessage(string operation, string routingTag, WcfRequest request)`

Override them in a derived portal, and host the derived type as shown in the routing example.

## WCF vs Other Channels

| Aspect | WCF | HTTP | gRPC |
| --- | --- | --- | --- |
| Package | `Csla.Channels.Wcf` | Built into `Csla` / `Csla.AspNetCore` | `Csla.Channels.Grpc` |
| .NET Framework server | Yes (self-hosted `ServiceHost`) | No (ASP.NET Core) | No |
| Modern .NET server | CoreWCF in ASP.NET Core | ASP.NET Core controller | ASP.NET Core gRPC |
| Typical use | Existing WCF estates, .NET Framework servers | Default choice | High-throughput service-to-service |

For new applications, the HTTP channel is usually the simplest choice. Use WCF when you need its hosting or binding options, or when you're moving an existing WCF-based data portal forward.

## Best Practices

1. **Use the same `Csla` and `Csla.Channels.Wcf` versions** on client and server.
2. **Match bindings.** The client `System.ServiceModel` binding and the server binding (CoreWCF or `System.ServiceModel`) must use the same binding type and security mode.
3. **Raise `MaxReceivedMessageSize`** on both ends for real-world object graphs.
4. **Use transport security (HTTPS)** in production.
5. **Always set `DataPortalUrl`** on the client. The default `http://localhost` is only a placeholder.
6. **Register your DAL services on the server**, because data portal operations run there.
7. **Don't flow the principal from the client** (`FlowSecurityPrincipalFromClient`) unless you trust the client. Authenticate on the server.

## Troubleshooting

- **`EndpointNotFoundException` / 404:** the client `DataPortalUrl` doesn't match the server's base address plus `DataPortalUrl` path, or `UseServiceModel` wasn't called on a CoreWCF server.
- **`ProtocolException` / content type mismatch:** the client and server bindings differ, for example `BasicHttpBinding` on one side and `WSHttpBinding` on the other, or different security modes.
- **Quota exceeded (`MaxReceivedMessageSize`):** raise the limit on whichever side received the message. Responses are limited by the client binding, requests by the server binding.
- **`InvalidOperationException` mentioning `RouterBinding`:** a routing tag matched a remote URL, but `WcfPortalOptions.RouterBinding` isn't set (modern .NET).
- **Service can't resolve dependencies:** the DAL services used by your `[Inject]` parameters must be registered in the server's service collection.

## Related Documentation

- [DataPortalChannels.md](DataPortalChannels.md) - Data portal channel architecture
- [HttpDataPortalConfiguration.md](HttpDataPortalConfiguration.md) - HTTP channel
- [GrpcDataPortalConfiguration.md](GrpcDataPortalConfiguration.md) - gRPC channel
- [RabbitMqDataPortalConfiguration.md](RabbitMqDataPortalConfiguration.md) - RabbitMQ channel
- [DataPortalRouting.md](DataPortalRouting.md) - Routing tags and versioned servers
