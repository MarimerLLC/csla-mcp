# RabbitMQ Data Portal Configuration

The CSLA data portal supports RabbitMQ-based communication between client and server through `RabbitMqProxy` (client-side) and `RabbitMqPortal` (server-side). This document covers configuration for both ends of the RabbitMQ data portal channel.

## Overview

RabbitMQ is a message broker that implements the Advanced Message Queuing Protocol (AMQP). The CSLA RabbitMQ channel uses message queues for bidirectional communication between clients and servers, enabling decoupled, asynchronous processing of data portal requests.

The architecture consists of:

- **Client-Side**: `RabbitMqProxy` class that serializes and sends data portal requests to a RabbitMQ queue
- **Server-Side**: `RabbitMqPortal` service that listens on a queue and processes data portal requests

Messages flow from client to server and back through RabbitMQ queues. Each message contains a "return address" so the server can reply to the correct client queue. Both clients and servers create their own queues automatically.

## Prerequisites

The RabbitMQ channel requires a running RabbitMQ server instance. For development, the simplest approach is to use Docker:

```bash
docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 rabbitmq:latest
```

Both client and server projects need the `Csla.Channels.RabbitMq` NuGet package:

```xml
<PackageReference Include="Csla.Channels.RabbitMq" Version="9.0.0" />
```

## Part 1: Server-Side Configuration

Unlike HTTP or gRPC channels, the RabbitMQ server is typically a standalone console application that continuously listens for messages on a queue. It does not require ASP.NET Core.

### Basic Server Setup

Configure CSLA and the RabbitMQ portal in your server's `Program.cs`:

```csharp
using Csla.Channels.RabbitMq;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Configure CSLA with RabbitMQ portal
services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://localhost:5672/myqueue")))));

var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("RabbitMQ Data Portal Server starting");

// Get the portal factory and create the portal
var factory = serviceProvider.GetRequiredService<IRabbitMqPortalFactory>();
var rabbitMqPortal = factory.CreateRabbitMqPortal();

using (rabbitMqPortal)
{
    // Start listening for messages (fire and forget)
    _ = rabbitMqPortal.StartListening();
    
    Console.WriteLine("Server is listening. Press any key to exit...");
    Console.ReadKey();
}
```

### URI Format

The RabbitMQ URI format is:

```
rabbitmq://[username:password@]host[:port]/queuename
```

**Components:**

| Component | Required | Description |
|-----------|----------|-------------|
| `rabbitmq://` | Yes | Protocol scheme (must be `rabbitmq`) |
| `username:password@` | No | RabbitMQ credentials |
| `host` | Yes | RabbitMQ server hostname |
| `port` | No | RabbitMQ port (default: 5672) |
| `/queuename` | Yes | Name of the queue to listen on |

**Examples:**

```
rabbitmq://localhost/myqueue
rabbitmq://localhost:5672/myqueue
rabbitmq://guest:guest@localhost:5672/dataportal
rabbitmq://admin:secret@rabbitmq.example.com/csla-dataportal
```

### Server with Custom Portal Factory

You can provide a custom portal factory for advanced scenarios:

```csharp
public class CustomRabbitMqPortalFactory : IRabbitMqPortalFactory
{
    private readonly ApplicationContext _applicationContext;
    private readonly IDataPortalServer _dataPortal;
    private readonly RabbitMqPortalOptions _options;

    public CustomRabbitMqPortalFactory(
        ApplicationContext applicationContext, 
        IDataPortalServer dataPortal, 
        RabbitMqPortalOptions options)
    {
        _applicationContext = applicationContext;
        _dataPortal = dataPortal;
        _options = options;
    }

    public RabbitMqPortal CreateRabbitMqPortal()
    {
        // Add custom initialization logic here
        return new RabbitMqPortal(_applicationContext, _dataPortal, _options);
    }
}
```

Register the custom factory:

```csharp
services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o =>
            {
                o.DataPortalUri = new Uri("rabbitmq://localhost:5672/myqueue");
                o.SetPortalFactoryType<CustomRabbitMqPortalFactory>();
            }))));
```

### Server with Data Access Layer

A complete server example with DAL registration:

```csharp
using Csla.Channels.RabbitMq;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register DAL services
services.AddTransient<IPersonDal, PersonDal>();

// Configure CSLA
services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://localhost:5672/dataportal")))));

var serviceProvider = services.BuildServiceProvider();

var factory = serviceProvider.GetRequiredService<IRabbitMqPortalFactory>();
var portal = factory.CreateRabbitMqPortal();

using (portal)
{
    _ = portal.StartListening();
    Console.WriteLine("Server running. Press any key to stop...");
    Console.ReadKey();
}
```

### Load Balancing with Multiple Servers

Multiple server instances can listen on the same queue name to distribute load:

```csharp
// Server Instance 1
services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://localhost:5672/dataportal")))));

// Server Instance 2 (same queue name)
services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://localhost:5672/dataportal")))));
```

RabbitMQ will round-robin messages across all consumers listening on the same queue, providing automatic load balancing.

## Part 2: Client-Side Configuration

The client-side configuration involves setting up the RabbitMQ proxy to send messages to the server's queue.

### Basic Client Configuration

Configure the RabbitMQ proxy in your client application:

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .DataPortal(o => o
        .AddClientSideDataPortal(o => o
            .UseRabbitMqProxy(o => o
                .DataPortalUrl = "rabbitmq://localhost:5672/myqueue"))));

var provider = services.BuildServiceProvider();
```

### Client with Named Reply Queue

By default, clients create an unnamed (exclusive) queue for receiving replies. You can specify a named reply queue:

```csharp
services.AddCsla(o => o
    .DataPortal(o => o
        .AddClientSideDataPortal(o => o
            .UseRabbitMqProxy(o => o
                .DataPortalUrl = "rabbitmq://localhost:5672/myqueue?reply=client1"))));
```

The `?reply=uniqueName` query parameter creates a named queue for replies. This can be useful for debugging or when you need predictable queue names.

### Client with Timeout Configuration

Configure the network operation timeout:

```csharp
services.AddCsla(o => o
    .DataPortal(o => o
        .AddClientSideDataPortal(o => o
            .UseRabbitMqProxy(o =>
            {
                o.DataPortalUrl = "rabbitmq://localhost:5672/myqueue";
                o.Timeout = TimeSpan.FromSeconds(60); // Default is 30 seconds
            }))));
```

### Console Application Client

Complete console application example:

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .DataPortal(o => o
        .AddClientSideDataPortal(o => o
            .UseRabbitMqProxy(o => o
                .DataPortalUrl = "rabbitmq://localhost:5672/dataportal"))));

var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("RabbitMQ Client starting");

var portal = serviceProvider.GetRequiredService<IDataPortal<PersonEdit>>();
var person = await portal.FetchAsync("John");

Console.WriteLine($"Person fetched: {person.Name}");
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

        services.AddCsla(o => o
            .DataPortal(o => o
                .AddClientSideDataPortal(o => o
                    .UseRabbitMqProxy(o => o
                        .DataPortalUrl = "rabbitmq://rabbitmq.example.com:5672/dataportal"))));

        ServiceProvider = services.BuildServiceProvider();
    }
}
```

## Part 3: Complete Examples

### Example 1: Simple Server and Client

**Server (RabbitMqService/Program.cs):**

```csharp
using Csla.Channels.RabbitMq;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://localhost:5672/myservice")))));

var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("RabbitMQ Service starting");

var factory = serviceProvider.GetRequiredService<IRabbitMqPortalFactory>();
var rabbitMqService = factory.CreateRabbitMqPortal();

using (rabbitMqService)
{
    // Start listening in the background
    _ = rabbitMqService.StartListening();
    Console.WriteLine("Press any key to exit");
    Console.ReadKey();
}
```

**Client (RabbitMqClient/Program.cs):**

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .DataPortal(o => o
        .AddClientSideDataPortal(o => o
            .UseRabbitMqProxy(o => o
                .DataPortalUrl = "rabbitmq://localhost:5672/myservice"))));

var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("RabbitMQ Client starting");

var portal = serviceProvider.GetRequiredService<IDataPortal<PersonEdit>>();
var person = await portal.FetchAsync("Abdi");

Console.WriteLine($"Person fetched: {person.Name}");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
```

### Example 2: Server with Authentication

```csharp
using Csla.Channels.RabbitMq;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCsla(o => o
    .Security(s => s.FlowSecurityPrincipalFromClient = true)
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://admin:secret@localhost:5672/secure-portal")))));

var serviceProvider = services.BuildServiceProvider();

var factory = serviceProvider.GetRequiredService<IRabbitMqPortalFactory>();
var portal = factory.CreateRabbitMqPortal();

using (portal)
{
    _ = portal.StartListening();
    Console.WriteLine("Secure server running...");
    Console.ReadKey();
}
```

### Example 3: Windows Service Host

For production deployments, host the RabbitMQ portal in a Windows Service or Linux daemon:

```csharp
using Csla.Channels.RabbitMq;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri("rabbitmq://localhost:5672/dataportal")))));

builder.Services.AddHostedService<RabbitMqPortalService>();

var host = builder.Build();
await host.RunAsync();

public class RabbitMqPortalService : BackgroundService
{
    private readonly IRabbitMqPortalFactory _factory;
    private RabbitMqPortal? _portal;

    public RabbitMqPortalService(IRabbitMqPortalFactory factory)
    {
        _factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _portal = _factory.CreateRabbitMqPortal();
        await _portal.StartListening();
        
        // Keep running until cancellation requested
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _portal?.Dispose();
        base.Dispose();
    }
}
```

## Part 4: Advanced Topics

### Async-Only Operations

The RabbitMQ channel only supports asynchronous operations. Attempting to use synchronous data portal calls will throw a `NotSupportedException`:

```csharp
// This will throw NotSupportedException:
// var person = portal.Fetch("John");

// Use async methods instead:
var person = await portal.FetchAsync("John");
```

### Message Flow

Understanding the message flow helps with debugging:

1. **Client sends request**: Client serializes the request and puts it on the server's queue
2. **Server processes**: Server picks up the message, deserializes, and invokes the data portal
3. **Server sends reply**: Server serializes the response and puts it on the client's reply queue
4. **Client receives reply**: Client picks up the response from its reply queue

```
┌────────┐                    ┌──────────────┐                    ┌────────┐
│ Client │ ──── Request ────► │   RabbitMQ   │ ──── Request ────► │ Server │
│        │                    │   Broker     │                    │        │
│        │ ◄──── Reply ────── │              │ ◄──── Reply ────── │        │
└────────┘                    └──────────────┘                    └────────┘
     │                              │                                  │
     │                              │                                  │
     ▼                              ▼                                  ▼
  Client                       Server Queue                         Server
  Reply Queue                  (myqueue)                            Listener
```

### Connection Management

The RabbitMQ proxy creates and disposes connections for each data portal operation. This ensures clean connection handling but means each operation has connection overhead.

For high-throughput scenarios, consider:
- Using connection pooling at the RabbitMQ level
- Batching multiple operations when possible

### Configuration via appsettings.json

```json
{
  "RabbitMq": {
    "Server": {
      "Uri": "rabbitmq://localhost:5672/dataportal"
    },
    "Client": {
      "Url": "rabbitmq://localhost:5672/dataportal",
      "Timeout": 30
    }
  }
}
```

**Server Configuration:**

```csharp
var rabbitMqUri = configuration["RabbitMq:Server:Uri"];

services.AddCsla(o => o
    .DataPortal(o => o
        .AddServerSideDataPortal(o => o
            .UseRabbitMqPortal(o => o
                .DataPortalUri = new Uri(rabbitMqUri)))));
```

**Client Configuration:**

```csharp
var rabbitMqUrl = configuration["RabbitMq:Client:Url"];
var timeout = int.Parse(configuration["RabbitMq:Client:Timeout"] ?? "30");

services.AddCsla(o => o
    .DataPortal(o => o
        .AddClientSideDataPortal(o => o
            .UseRabbitMqProxy(o =>
            {
                o.DataPortalUrl = rabbitMqUrl;
                o.Timeout = TimeSpan.FromSeconds(timeout);
            }))));
```

## Part 5: RabbitMQ vs HTTP vs gRPC

### When to Use RabbitMQ

Consider using RabbitMQ when:

- **Decoupled architecture**: Client and server don't need to be online simultaneously
- **Load balancing**: Multiple servers can consume from the same queue
- **Resilience**: Messages can be persisted if servers are temporarily unavailable
- **Fire-and-forget scenarios**: Though CSLA still waits for responses
- **Existing RabbitMQ infrastructure**: Already using RabbitMQ for other messaging

### When to Use HTTP or gRPC

Consider HTTP or gRPC when:

- **Synchronous operations needed**: RabbitMQ only supports async
- **Simple deployment**: No message broker infrastructure required
- **Browser clients**: HTTP is directly accessible from web browsers
- **Lower latency**: Direct connections may have less overhead than message queuing

### Feature Comparison

| Feature | HTTP Channel | gRPC Channel | RabbitMQ Channel |
|---------|--------------|--------------|------------------|
| Sync operations | Yes | Yes | No (async only) |
| Protocol | HTTP/1.1 or HTTP/2 | HTTP/2 | AMQP |
| Infrastructure | Web server | gRPC server | Message broker |
| Load balancing | External (nginx, etc.) | External | Built-in (multiple consumers) |
| Message persistence | No | No | Yes (configurable) |
| Decoupled | No | No | Yes |
| Browser support | Yes | Via gRPC-Web | No |

## Best Practices

### Server-Side

1. **Use a Container, Windows Service or daemon** for production
2. **Handle disposal properly** - Always use `using` or dispose the portal when done
3. **Monitor queue depth** - Large queue backlogs indicate processing issues
4. **Use durable queues in production** - Configure RabbitMQ for message persistence

### Client-Side

1. **Use async methods only** - Sync methods will throw exceptions
2. **Configure appropriate timeouts** - Default 30 seconds may not be enough for long operations
3. **Consider named reply queues** - Helps with debugging and monitoring
4. **Handle connection failures** - Implement retry logic for transient failures

### RabbitMQ Configuration

1. **Enable management plugin** - For monitoring and debugging

   ```bash
   docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:management
   ```

2. **Use credentials in production** - Don't rely on guest/guest defaults

3. **Configure clustering** - For high availability in production

4. **Set up dead letter queues** - To handle failed messages

## Running the Example

To run the RabbitMQ data portal example from the CSLA repository:

1. **Install Docker Desktop**

2. **Start RabbitMQ container:**
   ```bash
   docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 rabbitmq:latest
   ```

3. **Open the solution in Visual Studio**

4. **Set multiple startup projects:**
   - `RabbitMqExample.Server`
   - `RabbitMqExample.Client`
   - Make sure `RabbitMqExample.Server` starts first

5. **Run the solution**

## Troubleshooting

### Common Issues

#### Connection Refused
- Verify RabbitMQ is running: `docker ps`
- Check the hostname and port in the URI
- Ensure the port is not blocked by firewall

#### Timeout Waiting for Reply
- Verify the server is running and listening
- Check that client and server use the same queue name
- Increase the timeout if processing takes longer than 30 seconds
- Check RabbitMQ management console for queue status

#### NotSupportedException: isSync == true
- The RabbitMQ channel only supports async operations
- Replace `portal.Fetch()` with `await portal.FetchAsync()`

#### URI Format Exception
- Ensure the scheme is `rabbitmq://` (not `amqp://`)
- Verify the queue name is provided after the host
- Check for valid host and port values

### Debugging Tips

1. **Enable RabbitMQ Management Console:**
   ```bash
   docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:management
   ```
   Access at http://localhost:15672 (guest/guest)

2. **Monitor queues** - Check for message backlog or stuck messages

3. **Check connection status** - Verify clients and servers are connected

4. **Enable detailed logging** in your application

## Notes

- The RabbitMQ channel was updated in CSLA 9 to work with dependency injection and RabbitMQ.Client v7
- All operations are async - sync operations throw `NotSupportedException`
- The channel uses the same CSLA serialization as other channels (MobileFormatter)
- Business objects don't need any changes to work with RabbitMQ vs HTTP or gRPC
- Multiple servers can listen on the same queue for automatic load distribution
