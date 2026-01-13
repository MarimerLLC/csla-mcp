# Data Portal Channels

This document provides a comprehensive overview of CSLA .NET data portal channels and instructions for implementing custom channels to support new network transport protocols.

## Overview

A **data portal channel** is the mechanism by which the CSLA data portal communicates between a client application and a server-side data portal host. It consists of two main components:

- **Client-Side Proxy**: Serializes requests and sends them to the server over the chosen transport
- **Server-Side Portal**: Receives requests, deserializes them, invokes the data portal, and returns responses

CSLA includes built-in channels for HTTP, and additional channels are available via NuGet packages for gRPC and RabbitMQ. You can implement custom channels for any transport protocol.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              CLIENT                                          │
│  ┌──────────────┐    ┌────────────────┐    ┌───────────────────────────────┐│
│  │ IDataPortal  │───►│ DataPortalProxy│───►│ Custom/HttpProxy/GrpcProxy    ││
│  │ <T>          │    │ (base class)   │    │ CallDataPortalServer()        ││
│  └──────────────┘    └────────────────┘    └───────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ Network Transport (HTTP, gRPC, AMQP, etc.)
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SERVER                                          │
│  ┌───────────────────────────────┐    ┌────────────────┐    ┌─────────────┐ │
│  │ HttpPortalController/         │───►│ IDataPortal    │───►│ Business    │ │
│  │ GrpcPortal/RabbitMqPortal     │    │ Server         │    │ Object DAL  │ │
│  └───────────────────────────────┘    └────────────────┘    └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Key Concepts

### Message Flow

Every data portal operation follows this flow:

1. **Client creates request**: Business object type, criteria, security principal, and context are packaged into a request message
2. **Serialization**: The request is serialized using CSLA's `ISerializationFormatter` (MobileFormatter by default)
3. **Transport**: The serialized bytes are sent to the server via the channel's transport mechanism
4. **Server receives**: The server-side portal receives and deserializes the request
5. **Processing**: The server invokes the appropriate data portal operation
6. **Response**: Results (or error info) are packaged into a response message
7. **Return transport**: The serialized response is sent back to the client
8. **Client receives**: The proxy deserializes the response and returns the business object

### Message Types

CSLA defines standard message types in the `Csla.Server.Hosts.DataPortalChannel` namespace:

**CriteriaRequest** - Used for Create, Fetch, and Delete operations:
```csharp
public class CriteriaRequest
{
    public string TypeName { get; set; }        // Assembly-qualified business object type
    public byte[] CriteriaData { get; set; }    // Serialized criteria object
    public byte[] Principal { get; set; }       // Serialized security principal
    public byte[] ClientContext { get; set; }   // Serialized client context dictionary
    public string ClientCulture { get; set; }   // Culture name (e.g., "en-US")
    public string ClientUICulture { get; set; } // UI culture name
}
```

**UpdateRequest** - Used for Update operations:
```csharp
public class UpdateRequest
{
    public byte[] ObjectData { get; set; }      // Serialized business object
    public byte[] Principal { get; set; }       // Serialized security principal
    public byte[] ClientContext { get; set; }   // Serialized client context dictionary
    public string ClientCulture { get; set; }   // Culture name
    public string ClientUICulture { get; set; } // UI culture name
}
```

**DataPortalResponse** - Returned from all operations:
```csharp
public class DataPortalResponse
{
    public byte[] ObjectData { get; set; }          // Serialized result object
    public DataPortalErrorInfo ErrorData { get; set; } // Error information if failed
    public bool HasError => ErrorData != null;      // Indicates error occurred
}
```

**DataPortalErrorInfo** - Exception details for errors:
```csharp
public class DataPortalErrorInfo
{
    public string ExceptionTypeName { get; set; }   // Exception type name
    public string Message { get; set; }              // Exception message
    public string StackTrace { get; set; }           // Stack trace
    public string Source { get; set; }               // Source assembly
    public DataPortalErrorInfo InnerError { get; set; } // Inner exception (recursive)
}
```

### Operations and Routing

The data portal supports four operations:
- `create` - Create a new business object with default values
- `fetch` - Retrieve an existing business object
- `update` - Persist changes to a business object
- `delete` - Delete a business object

Operations can include an optional **routing token** for directing requests to specific backend servers. The format is:
```
operation/routingTag-versionTag
```

For example: `fetch/accounting-v2` routes fetch requests to a server tagged for accounting operations, version 2.

## Built-In Channels

### HTTP Channel

The default channel using standard HTTP/HTTPS:
- **Client**: `HttpProxy` or `HttpCompressionProxy` (`Csla.DataPortalClient`)
- **Server**: `HttpPortalController` (ASP.NET Core MVC controller)
- **Package**: Included in core CSLA

See [HttpDataPortalConfiguration.md](HttpDataPortalConfiguration.md) for configuration details.

### gRPC Channel

High-performance binary protocol over HTTP/2:
- **Client**: `GrpcProxy` (`Csla.Channels.Grpc`)
- **Server**: `GrpcPortal` (gRPC service)
- **Package**: `Csla.Channels.Grpc`

See [GrpcDataPortalConfiguration.md](GrpcDataPortalConfiguration.md) for configuration details.

### RabbitMQ Channel

Message queue-based asynchronous communication:
- **Client**: `RabbitMqProxy` (`Csla.Channels.RabbitMq`)
- **Server**: `RabbitMqPortal` (standalone listener)
- **Package**: `Csla.Channels.RabbitMq`

See [RabbitMqDataPortalConfiguration.md](RabbitMqDataPortalConfiguration.md) for configuration details.

## Implementing a Custom Channel

To implement a custom data portal channel, you need to create:

1. A client-side **proxy** class that inherits from `DataPortalProxy`
2. A server-side **portal** class or controller that processes requests
3. An **options** class for configuration
4. **Extension methods** for registration

### Step 1: Create the Proxy Class

The proxy must inherit from `Csla.DataPortalClient.DataPortalProxy` and implement the abstract `CallDataPortalServer` method:

```csharp
using Csla;
using Csla.DataPortalClient;

namespace MyApp.Channels.Custom
{
    /// <summary>
    /// Data portal proxy that communicates over a custom transport.
    /// </summary>
    public class CustomProxy : DataPortalProxy
    {
        private readonly CustomProxyOptions _options;

        /// <summary>
        /// Creates a new instance of the proxy.
        /// </summary>
        /// <param name="applicationContext">CSLA application context.</param>
        /// <param name="options">Channel configuration options.</param>
        public CustomProxy(ApplicationContext applicationContext, CustomProxyOptions options)
            : base(applicationContext)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Gets the data portal URL for this proxy.
        /// </summary>
        public override string DataPortalUrl => _options.DataPortalUrl;

        /// <summary>
        /// Sends a serialized request to the server and returns the serialized response.
        /// </summary>
        /// <param name="serialized">Serialized request data (CriteriaRequest or UpdateRequest).</param>
        /// <param name="operation">Operation name: create, fetch, update, or delete.</param>
        /// <param name="routingToken">Optional routing token for server selection.</param>
        /// <param name="isSync">True for synchronous execution, false for async.</param>
        /// <returns>Serialized response data (DataPortalResponse).</returns>
        protected override async Task<byte[]> CallDataPortalServer(
            byte[] serialized,
            string operation,
            string? routingToken,
            bool isSync)
        {
            // Build the full operation string including routing token
            var fullOperation = string.IsNullOrEmpty(routingToken)
                ? operation
                : $"{operation}/{routingToken}";

            // IMPLEMENT: Send serialized bytes to server via your transport
            // The serialized parameter contains:
            // - For create/fetch/delete: A serialized CriteriaRequest
            // - For update: A serialized UpdateRequest

            byte[] responseBytes;

            if (isSync)
            {
                // Synchronous implementation (if supported)
                responseBytes = SendRequestSync(serialized, fullOperation);
            }
            else
            {
                // Asynchronous implementation
                responseBytes = await SendRequestAsync(serialized, fullOperation)
                    .ConfigureAwait(false);
            }

            // Return serialized DataPortalResponse bytes
            return responseBytes;
        }

        private byte[] SendRequestSync(byte[] request, string operation)
        {
            // IMPLEMENT: Synchronous transport logic
            // Example: TCP socket, named pipe, etc.
            throw new NotImplementedException("Implement synchronous transport");
        }

        private async Task<byte[]> SendRequestAsync(byte[] request, string operation)
        {
            // IMPLEMENT: Asynchronous transport logic
            // Example: TCP socket, WebSocket, message queue, etc.
            throw new NotImplementedException("Implement asynchronous transport");
        }
    }
}
```

### Step 2: Create the Options Class

```csharp
namespace MyApp.Channels.Custom
{
    /// <summary>
    /// Configuration options for the custom data portal channel.
    /// </summary>
    public class CustomProxyOptions
    {
        /// <summary>
        /// Gets or sets the server endpoint URL or address.
        /// </summary>
        public string DataPortalUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the connection timeout.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        // Add additional transport-specific options as needed
    }
}
```

### Step 3: Create the Server-Side Portal

The server-side portal receives requests, deserializes them, calls the data portal, and returns responses:

```csharp
using Csla;
using Csla.Serialization;
using Csla.Server;
using Csla.Server.Hosts.DataPortalChannel;

namespace MyApp.Channels.Custom
{
    /// <summary>
    /// Server-side portal that processes data portal requests.
    /// </summary>
    public class CustomPortal : IDisposable
    {
        private readonly ApplicationContext _applicationContext;
        private readonly IDataPortalServer _dataPortal;
        private readonly CustomPortalOptions _options;

        public CustomPortal(
            ApplicationContext applicationContext,
            IDataPortalServer dataPortal,
            CustomPortalOptions options)
        {
            _applicationContext = applicationContext;
            _dataPortal = dataPortal;
            _options = options;
        }

        /// <summary>
        /// Dictionary of routing tag to server URL mappings for forwarding requests.
        /// Set a value to "localhost" to process locally.
        /// </summary>
        public Dictionary<string, string> RoutingTagUrls { get; } = new();

        /// <summary>
        /// Processes a raw request received from the transport.
        /// </summary>
        /// <param name="requestData">Serialized request bytes.</param>
        /// <param name="operation">Operation string (may include routing token).</param>
        /// <returns>Serialized response bytes.</returns>
        public async Task<byte[]> ProcessRequest(byte[] requestData, string operation)
        {
            // Parse operation and routing token
            string op = operation;
            string? routingToken = null;

            if (operation.Contains('/'))
            {
                var parts = operation.Split('/');
                op = parts[0];
                routingToken = parts[1];
            }

            // Check for routing to another server
            if (!string.IsNullOrEmpty(routingToken) &&
                RoutingTagUrls.TryGetValue(routingToken, out var targetUrl) &&
                targetUrl != "localhost")
            {
                // Forward to remote server
                return await ForwardToRemoteServer(requestData, operation, targetUrl)
                    .ConfigureAwait(false);
            }

            // Process locally
            return await InvokePortal(requestData, op).ConfigureAwait(false);
        }

        private async Task<byte[]> InvokePortal(byte[] requestData, string operation)
        {
            var serializer = _applicationContext.GetRequiredService<ISerializationFormatter>();
            DataPortalResponse response;

            try
            {
                response = operation switch
                {
                    "create" => await Create(requestData, serializer).ConfigureAwait(false),
                    "fetch" => await Fetch(requestData, serializer).ConfigureAwait(false),
                    "update" => await Update(requestData, serializer).ConfigureAwait(false),
                    "delete" => await Delete(requestData, serializer).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unknown operation: {operation}")
                };
            }
            catch (Exception ex)
            {
                response = new DataPortalResponse
                {
                    ErrorData = _applicationContext.CreateInstanceDI<DataPortalErrorInfo>(ex)
                };
            }

            // Allow response modification before serialization
            response = ConvertResponse(response);

            return serializer.Serialize(response);
        }

        private async Task<DataPortalResponse> Create(byte[] requestData, ISerializationFormatter serializer)
        {
            var request = (CriteriaRequest)serializer.Deserialize(requestData)!;
            request = ConvertRequest(request);

            // Set up context from request
            SetContext(request, serializer);

            // Invoke the data portal
            var objectType = Csla.Reflection.MethodCaller.GetType(
                AssemblyNameTranslator.GetAssemblyQualifiedName(request.TypeName), true)!;
            var criteria = serializer.Deserialize(request.CriteriaData);

            var result = await _dataPortal.Create(objectType, criteria,
                new DataPortalContext(_applicationContext, isRemotePortal: true), isSync: false)
                .ConfigureAwait(false);

            return new DataPortalResponse
            {
                ObjectData = serializer.Serialize(result.ReturnObject)
            };
        }

        private async Task<DataPortalResponse> Fetch(byte[] requestData, ISerializationFormatter serializer)
        {
            var request = (CriteriaRequest)serializer.Deserialize(requestData)!;
            request = ConvertRequest(request);

            SetContext(request, serializer);

            var objectType = Csla.Reflection.MethodCaller.GetType(
                AssemblyNameTranslator.GetAssemblyQualifiedName(request.TypeName), true)!;
            var criteria = serializer.Deserialize(request.CriteriaData);

            var result = await _dataPortal.Fetch(objectType, criteria,
                new DataPortalContext(_applicationContext, isRemotePortal: true), isSync: false)
                .ConfigureAwait(false);

            return new DataPortalResponse
            {
                ObjectData = serializer.Serialize(result.ReturnObject)
            };
        }

        private async Task<DataPortalResponse> Update(byte[] requestData, ISerializationFormatter serializer)
        {
            var request = (UpdateRequest)serializer.Deserialize(requestData)!;
            request = ConvertRequest(request);

            SetContext(request, serializer);

            var obj = serializer.Deserialize(request.ObjectData);

            var result = await _dataPortal.Update(obj,
                new DataPortalContext(_applicationContext, isRemotePortal: true), isSync: false)
                .ConfigureAwait(false);

            return new DataPortalResponse
            {
                ObjectData = serializer.Serialize(result.ReturnObject)
            };
        }

        private async Task<DataPortalResponse> Delete(byte[] requestData, ISerializationFormatter serializer)
        {
            var request = (CriteriaRequest)serializer.Deserialize(requestData)!;
            request = ConvertRequest(request);

            SetContext(request, serializer);

            var objectType = Csla.Reflection.MethodCaller.GetType(
                AssemblyNameTranslator.GetAssemblyQualifiedName(request.TypeName), true)!;
            var criteria = serializer.Deserialize(request.CriteriaData);

            await _dataPortal.Delete(objectType, criteria,
                new DataPortalContext(_applicationContext, isRemotePortal: true), isSync: false)
                .ConfigureAwait(false);

            return new DataPortalResponse();
        }

        private void SetContext(CriteriaRequest request, ISerializationFormatter serializer)
        {
            // Set culture
            if (!string.IsNullOrEmpty(request.ClientCulture))
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo(request.ClientCulture);
            if (!string.IsNullOrEmpty(request.ClientUICulture))
                System.Threading.Thread.CurrentThread.CurrentUICulture =
                    new System.Globalization.CultureInfo(request.ClientUICulture);

            // Set client context
            if (request.ClientContext != null)
                _applicationContext.SetClientContext(
                    (Csla.Core.ContextDictionary)serializer.Deserialize(request.ClientContext)!);

            // Set principal (if flowing from client)
            if (request.Principal != null)
                _applicationContext.User =
                    (System.Security.Principal.IPrincipal)serializer.Deserialize(request.Principal)!;
        }

        private void SetContext(UpdateRequest request, ISerializationFormatter serializer)
        {
            if (!string.IsNullOrEmpty(request.ClientCulture))
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo(request.ClientCulture);
            if (!string.IsNullOrEmpty(request.ClientUICulture))
                System.Threading.Thread.CurrentThread.CurrentUICulture =
                    new System.Globalization.CultureInfo(request.ClientUICulture);

            if (request.ClientContext != null)
                _applicationContext.SetClientContext(
                    (Csla.Core.ContextDictionary)serializer.Deserialize(request.ClientContext)!);

            if (request.Principal != null)
                _applicationContext.User =
                    (System.Security.Principal.IPrincipal)serializer.Deserialize(request.Principal)!;
        }

        /// <summary>
        /// Override to modify requests before processing.
        /// </summary>
        protected virtual CriteriaRequest ConvertRequest(CriteriaRequest request) => request;

        /// <summary>
        /// Override to modify update requests before processing.
        /// </summary>
        protected virtual UpdateRequest ConvertRequest(UpdateRequest request) => request;

        /// <summary>
        /// Override to modify responses before returning to client.
        /// </summary>
        protected virtual DataPortalResponse ConvertResponse(DataPortalResponse response) => response;

        private async Task<byte[]> ForwardToRemoteServer(byte[] requestData, string operation, string targetUrl)
        {
            // IMPLEMENT: Forward request to another server
            // This enables multi-tier routing scenarios
            throw new NotImplementedException("Implement remote forwarding");
        }

        public void Dispose()
        {
            // Clean up transport resources
        }
    }
}
```

### Step 4: Create Extension Methods for Registration

```csharp
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Channels.Custom
{
    /// <summary>
    /// Extension methods for registering the custom channel.
    /// </summary>
    public static class CustomChannelExtensions
    {
        /// <summary>
        /// Configures the data portal to use the custom proxy for client-side operations.
        /// </summary>
        public static DataPortalClientOptions UseCustomProxy(
            this DataPortalClientOptions config,
            Action<CustomProxyOptions>? options = null)
        {
            var proxyOptions = new CustomProxyOptions();
            options?.Invoke(proxyOptions);

            config.Services.AddTransient<Csla.DataPortalClient.IDataPortalProxy>(sp =>
            {
                var applicationContext = sp.GetRequiredService<ApplicationContext>();
                return new CustomProxy(applicationContext, proxyOptions);
            });

            return config;
        }

        /// <summary>
        /// Configures the server to use the custom portal for server-side operations.
        /// </summary>
        public static DataPortalServerOptions UseCustomPortal(
            this DataPortalServerOptions config,
            Action<CustomPortalOptions>? options = null)
        {
            var portalOptions = new CustomPortalOptions();
            options?.Invoke(portalOptions);

            config.Services.AddSingleton(portalOptions);
            config.Services.AddScoped<CustomPortal>();

            return config;
        }
    }
}
```

### Step 5: Client Configuration

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseCustomProxy(proxy =>
            {
                proxy.DataPortalUrl = "custom://myserver:9999/dataportal";
                proxy.Timeout = TimeSpan.FromSeconds(60);
            }))));
```

### Step 6: Server Configuration

```csharp
services.AddCsla(options => options
    .DataPortal(dp => dp
        .AddServerSideDataPortal(ssp => ssp
            .UseCustomPortal(portal =>
            {
                portal.ListenAddress = "custom://0.0.0.0:9999/dataportal";
            }))));
```

## Implementation Patterns from Built-In Channels

### HTTP Channel Pattern

The HTTP channel uses a simple request-response model:

```
Client: POST {DataPortalUrl}?operation={operation/routingTag}
Body: Serialized CriteriaRequest or UpdateRequest
Response: Serialized DataPortalResponse
```

Key implementation details:
- Uses `HttpClient` for async operations
- Uses `WebClient` for sync operations (legacy support)
- Supports optional Base64 text encoding for environments that don't support binary
- Supports compression via `HttpCompressionProxy`

### gRPC Channel Pattern

The gRPC channel wraps CSLA messages in protobuf:

```protobuf
service GrpcService {
    rpc Invoke (RequestMessage) returns (ResponseMessage) {}
}

message RequestMessage {
    bytes body = 1;      // Serialized CSLA request
    string operation = 2; // Operation name with optional routing
}

message ResponseMessage {
    bytes body = 1;      // Serialized DataPortalResponse
}
```

Key implementation details:
- Single RPC method handles all operations
- Binary serialization wrapped in protobuf
- Long-lived gRPC channel for connection reuse
- Both sync and async support

### RabbitMQ Channel Pattern

The RabbitMQ channel uses message queues:

```
Client:
  - Publishes to server's queue
  - Creates reply queue for responses
  - Uses CorrelationId to match replies

Server:
  - Listens on configured queue
  - Processes requests
  - Publishes response to client's ReplyTo queue
```

Key implementation details:
- Operation type passed in message properties (`BasicProperties.Type`)
- Correlation ID for request-response matching
- Async-only (sync operations throw `NotSupportedException`)
- Supports load balancing via multiple consumers on same queue

## Extensibility Hooks

The `DataPortalProxy` base class provides virtual methods for customization:

### ConvertRequest

Modify requests before serialization and sending:

```csharp
protected override CriteriaRequest ConvertRequest(CriteriaRequest request)
{
    // Add custom headers, encrypt data, etc.
    return request;
}

protected override UpdateRequest ConvertRequest(UpdateRequest request)
{
    // Add custom headers, encrypt data, etc.
    return request;
}
```

### ConvertResponse

Modify responses after receiving:

```csharp
protected override DataPortalResponse ConvertResponse(DataPortalResponse response)
{
    // Decrypt data, validate signatures, etc.
    return response;
}
```

### OnServerComplete / OnServerCompleteClient

Execute logic after operations complete:

```csharp
protected override void OnServerComplete(
    DataPortalResult result,
    Type objectType,
    DataPortalOperations operationType)
{
    // Called after any operation (server and client)
    // Useful for logging, metrics, etc.
}

protected override void OnServerCompleteClient(
    DataPortalResult result,
    Type objectType,
    DataPortalOperations operationType)
{
    // Called only for client-originated operations
    // Not called for chained server-side calls
}
```

## Best Practices

### Proxy Implementation

1. **Handle both sync and async**: Support `isSync` parameter if your transport allows synchronous operations. Throw `NotSupportedException` if sync is not supported.

2. **Connection management**: Consider connection pooling for transports that benefit from it (TCP, WebSocket, etc.).

3. **Timeout handling**: Implement proper timeout handling using `CancellationToken` or transport-specific mechanisms.

4. **Error handling**: Catch transport exceptions and let them propagate as-is. The base class will wrap them in `DataPortalResult`.

5. **Routing token**: Always include the routing token in your transport's operation identifier.

### Portal Implementation

1. **Context setup**: Always set culture and client context from the request before invoking the data portal.

2. **Error handling**: Catch exceptions and wrap them in `DataPortalErrorInfo`. Don't let raw exceptions escape.

3. **Routing support**: Implement `RoutingTagUrls` dictionary for multi-tier scenarios.

4. **Resource cleanup**: Implement `IDisposable` and clean up transport resources properly.

### Configuration

1. **Options classes**: Use options classes for all configuration to support DI patterns.

2. **Extension methods**: Provide fluent extension methods for easy registration.

3. **Timeout defaults**: Use reasonable default timeouts (30-60 seconds is common).

4. **URL validation**: Validate connection strings/URLs early in configuration.

## Testing Custom Channels

Test your channel implementation with:

1. **Unit tests**: Mock the transport layer and verify serialization/deserialization

2. **Integration tests**: Test with actual business objects against a running server

3. **Error scenarios**: Verify proper error propagation for network failures, timeouts, and server exceptions

4. **Routing**: Test routing token handling if implementing multi-tier support

Example integration test:

```csharp
[Fact]
public async Task CustomChannel_FetchAsync_ReturnsObject()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddCsla(o => o
        .DataPortal(dp => dp
            .AddClientSideDataPortal(csp => csp
                .UseCustomProxy(p => p.DataPortalUrl = "custom://localhost:9999"))));

    var provider = services.BuildServiceProvider();
    var portal = provider.GetRequiredService<IDataPortal<TestBusinessObject>>();

    // Act
    var result = await portal.FetchAsync(123);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(123, result.Id);
}
```

## Summary

Implementing a custom data portal channel requires:

1. **Proxy class** inheriting from `DataPortalProxy` with `CallDataPortalServer` implementation
2. **Portal class** that deserializes requests, invokes the data portal, and returns responses
3. **Options classes** for configuration
4. **Extension methods** for DI registration

The key is understanding that CSLA handles all the serialization and deserialization of business objects and messages - your channel only needs to transport the raw bytes between client and server.

## Related Documents

- [DataPortalGuide.md](DataPortalGuide.md) - Overview of the data portal concept
- [HttpDataPortalConfiguration.md](HttpDataPortalConfiguration.md) - HTTP channel configuration
- [GrpcDataPortalConfiguration.md](GrpcDataPortalConfiguration.md) - gRPC channel configuration
- [RabbitMqDataPortalConfiguration.md](RabbitMqDataPortalConfiguration.md) - RabbitMQ channel configuration
- [DataPortalRouting.md](DataPortalRouting.md) - Server-side routing configuration
