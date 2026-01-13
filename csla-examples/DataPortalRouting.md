# Data Portal Routing and Version Tags

The CSLA Data Portal includes built-in support for server-side routing of requests based on version tags and per-type routing tags. This feature enables sophisticated deployment scenarios where different versions of your application or different business types need to be processed by different backend servers.

## Overview

In traditional n-tier CSLA applications, clients connect directly to a single data portal endpoint:

```text
Client → https://example.com/api/dataportal → Server
```

However, in real-world scenarios, especially with smart client and mobile applications, you often need to support multiple versions of your application simultaneously. This is because:

- Mobile app stores (Apple, Google, Microsoft) may delay deployment
- Users don't always update their apps immediately
- You need to maintain backward compatibility during rolling updates

The Data Portal routing feature solves this by introducing a router pattern:

```text
Client (v1) ─┐
             ├→ Router → AppServer-v1
Client (v2) ─┘         → AppServer-v2
```

## Key Concepts

### Version Routing Tag

The `VersionRoutingTag` is a global application-level setting that identifies the version of your client application. This tag is sent with every data portal request and is used by the server-side router to direct requests to the appropriate backend server.

**Important restrictions:**

- The tag cannot contain `-` (hyphen) or `/` (slash) characters
- It should be set at application startup and not changed during runtime

### Per-Type Routing Tag

The `DataPortalServerRoutingTagAttribute` allows you to route specific business types to specific servers. This is useful when certain domain types should be processed by specialized servers.

**Important restrictions:**

- The tag cannot contain `-` (hyphen) or `/` (slash) characters
- Applied via attribute on business classes or interfaces

### Routing Tag Format

The client-side data portal constructs a routing tag with the following format:

```text
{operation}/{routingTag}-{versionTag}
```

Examples:

- With both tags: `fetch/mytag-v1`
- Version only: `fetch/-v1`
- Routing tag only: `fetch/mytag-`
- No tags: `fetch`

## Client-Side Configuration

### Setting the Version Routing Tag

Configure the version routing tag during application startup using the fluent API:

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .VersionRoutingTag = "v1"));
```

### Using Per-Type Routing Tags

Apply the `DataPortalServerRoutingTagAttribute` to business types that should be routed to specific servers:

```csharp
[Serializable]
[DataPortalServerRoutingTag("specialserver")]
public class OrderProcessor : BusinessBase<OrderProcessor>
{
    // Business logic here
}
```

You can also apply the attribute to interfaces:

```csharp
[DataPortalServerRoutingTag("legacyserver")]
public interface ILegacyBusiness { }

[Serializable]
public class LegacyCustomer : BusinessBase<LegacyCustomer>, ILegacyBusiness
{
    // Inherits routing tag from interface
}
```

## Server-Side Router Configuration

The server-side router is implemented by configuring the `HttpPortalController` with a routing table.

### Creating a Router Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using Csla;

namespace RoutedDataPortal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataPortalController : Csla.Server.Hosts.HttpPortalController
    {
        public DataPortalController(ApplicationContext applicationContext)
            : base(applicationContext)
        {
            // Configure routing table
            // Key format: "{routingTag}-{versionTag}"
            RoutingTagUrls["-v1"] = "http://appserver-v1/api/DataPortal";
            RoutingTagUrls["-v2"] = "http://appserver-v2/api/DataPortal";
            
            // Route specific types to specialized servers
            RoutingTagUrls["specialserver-v1"] = "http://special-v1/api/DataPortal";
            RoutingTagUrls["specialserver-v2"] = "http://special-v2/api/DataPortal";
            
            // Use "localhost" to process on the current server
            RoutingTagUrls["local-"] = "localhost";
        }

        [HttpPost]
        public override async Task PostAsync([FromQuery] string operation)
        {
            await base.PostAsync(operation).ConfigureAwait(false);
        }

        [HttpGet]
        public ActionResult<string> Get()
        {
            return "Router Server";
        }
    }
}
```

### Routing Logic

The `HttpPortalController` base class handles routing automatically:

1. Client sends request with routing tag in the operation parameter
2. Router extracts the routing tag from the operation
3. Router looks up the destination URL in `RoutingTagUrls`
4. If found and not "localhost", request is forwarded to the target server
5. If "localhost" or not found, request is processed locally

## Backend Server Configuration

Backend servers are standard CSLA data portal servers. They don't need special routing configuration but often include context information for debugging:

```csharp
[Route("api/[controller]")]
[ApiController]
public class DataPortalController : Csla.Server.Hosts.HttpPortalController
{
    public DataPortalController(ApplicationContext applicationContext)
        : base(applicationContext)
    {
        // Add version info to local context for debugging
        applicationContext.LocalContext.Add("serverVersion", "v1");
    }

    [HttpGet]
    public ActionResult<string> Get()
    {
        return "AppServer-v1";
    }
}
```

## Docker/Kubernetes Deployment

The routing feature is particularly powerful in containerized environments. Here's a typical deployment pattern:

### Architecture

```text
                    ┌─────────────────┐
                    │   K8s Service   │
                    │  (Public URL)   │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │     Router      │
                    │   Container     │
                    └────────┬────────┘
                             │
           ┌─────────────────┼─────────────────┐
           │                 │                 │
    ┌──────▼──────┐   ┌──────▼──────┐   ┌──────▼──────┐
    │ AppServer   │   │ AppServer   │   │ AppServer   │
    │    v1       │   │    v2       │   │    v3       │
    └─────────────┘   └─────────────┘   └─────────────┘
```

### Docker Compose Example

```yaml
version: '3.4'

services:
  routedserver:
    image: routedserver
    build:
      context: .
      dockerfile: RoutedDataPortal/Dockerfile
    ports:
      - "80:80"
    depends_on:
      - appserver1
      - appserver2

  appserver1:
    image: appserver1
    build:
      context: .
      dockerfile: AppServer1/Dockerfile
    expose:
      - "80"

  appserver2:
    image: appserver2
    build:
      context: .
      dockerfile: AppServer2/Dockerfile
    expose:
      - "80"
```

### Kubernetes Configuration

In Kubernetes, use internal service names for routing:

```csharp
RoutingTagUrls["-v1"] = "http://appserver-v1-service/api/DataPortal";
RoutingTagUrls["-v2"] = "http://appserver-v2-service/api/DataPortal";
```

## Same-Pod Deployment

For maximum efficiency, you can run the router and all app servers in the same pod, using different ports:

```csharp
RoutingTagUrls["-v1"] = "http://localhost:32123/api/DataPortal";
RoutingTagUrls["-v2"] = "http://localhost:32124/api/DataPortal";
```

This eliminates network overhead for routing while maintaining version isolation.

## Comparison with API Gateway Routing

| Feature | CSLA Built-in Routing | API Gateway |
| ------- | --------------------- | ----------- |
| Protocol awareness | Yes - understands CSLA operations | No - generic HTTP routing |
| Per-type routing | Yes | Usually no |
| No deserialization overhead | Yes - passes through binary | May inspect/transform |
| External dependencies | None | Requires gateway infrastructure |
| Configuration | Code-based | Configuration files/UI |
| Custom routing logic | Easy to extend | Depends on gateway |

Use CSLA's built-in routing when:

- You need per-type routing to specialized servers
- You want to minimize latency by avoiding message transformation
- You prefer code-based configuration
- You don't have existing API gateway infrastructure

Use an API gateway when:

- You have enterprise API management requirements
- You need advanced features like rate limiting, API keys, etc.
- You have existing gateway infrastructure
- You need routing across different protocols/services

## Complete Client Example

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class App : Application
{
    public App()
    {
        var services = new ServiceCollection();
        
        // Configure HttpClient
        services.AddTransient<HttpClient>();
        
        // Configure CSLA with version routing
        services.AddCsla(options => options
            .DataPortal(dp =>
            {
                dp.VersionRoutingTag = "v2";
                dp.AddClientSideDataPortal(cp => cp
                    .UseHttpProxy(hp => hp
                        .DataPortalUrl = "https://api.example.com/api/DataPortal"));
            }));
        
        var provider = services.BuildServiceProvider();
        ApplicationContext = provider.GetRequiredService<ApplicationContext>();
    }
    
    public static ApplicationContext ApplicationContext { get; private set; }
}
```

## Best Practices

1. **Set version tag at startup only** - The version tag should be determined at application startup and not changed during runtime.

2. **Use semantic versioning for tags** - Use clear version identifiers like "v1", "v2", "v2beta" (remember: no hyphens).

3. **Plan your routing strategy** - Decide whether you need per-type routing or just version-based routing before implementing.

4. **Test routing in development** - Verify routing behavior in development before deploying to production.

5. **Monitor routing performance** - In production, monitor which servers are receiving traffic to ensure proper load distribution.

6. **Document your routing table** - Keep clear documentation of which routing tags map to which servers.

7. **Handle unknown routes gracefully** - Decide whether unknown routing tags should be processed locally or rejected.

## Troubleshooting

### Request Not Being Routed

1. Verify the version routing tag is set correctly on the client
2. Check that the routing tag format matches keys in `RoutingTagUrls`
3. Ensure backend servers are reachable from the router

### Incorrect Server Handling Request

1. Check for typos in routing tag configuration
2. Verify the per-type routing attribute is applied correctly
3. Review the `RoutingTagUrls` dictionary configuration

### Performance Issues

1. Consider same-pod deployment to reduce network hops
2. Monitor network latency between router and backend servers
3. Evaluate if routing is necessary or if a simpler architecture would suffice

## See Also

- [Data Portal Guide](DataPortalGuide.md) - Overview of data portal concepts
- [HTTP Data Portal Configuration](HttpDataPortalConfiguration.md) - Http data portal channel configuration
