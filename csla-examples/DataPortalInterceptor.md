# Data Portal Interceptor

The server-side data portal supports a concept called an interceptor. This type implements the `IInterceptDataPortal` interface, and interceptors are invoked as the data portal starts and completes each server-side root data portal invocation.

## Implementing a Data Portal Interceptor

An interceptor implements `Csla.Server.IInterceptDataPortal`.

The `InitializeAsync` method is invoked when a root server-side data portal call begins. This is a location to run code before any normal user code (like data portal operation methods) are executed on the server.

The `Complete` method is invoked after a root server-side data portal call is complete. This is a location to run code after any normal user code (like data portal operation methods) have been executed on the server.

The `Complete` method will be invoked regardless of whether an exception occurred during normal server-side processing. If an exception occurred, the `InterceptArgs` parameter's `Exception` property will provide access to the exception object.

The following example is an interceptor that will commit or rollback a database connection after the data portal operation is complete.

```csharp
public class TransactionInterceptor(IServiceProvider _serviceProvider) : IInterceptDataPortal
{
    public Task InitializeAsync(InterceptArgs e)
    {
        // Called before the data portal operation begins
        // Transaction is already created by DI scope, so nothing to do here
        return Task.CompletedTask;
    }

    public void Complete(InterceptArgs e)
    {
        // Called after the data portal operation completes
        var transaction = _serviceProvider.GetService(typeof(SqlTransaction)) as SqlTransaction;
        
        if (transaction != null)
        {
            if (e.Exception == null)
            {
                // No exception - commit the transaction
                transaction.Commit();
            }
            else
            {
                // Exception occurred - rollback the transaction
                transaction.Rollback();
            }
        }
    }
}
```

There are many possible uses for an interceptor, including implementing logging, metrics, managing transactions, executing pre- or post-processing on all calls, etc.

## Registering a Data Portal Interceptor Service

Once an interceptor service has been implemented, it must be registered as a service on the device where the server-side data portal is running. This is often an actual server for a remote data portal, but in a 1- or 2-tier deployment the "server-side" data portal might be running on the client device.

The registration is generally in `Program.cs`, and is part of the `AddCsla` method call, within which the data portal can be configured:

```csharp
builder.Services.AddCsla((o) => o
    .DataPortal((x) => x
      .AddServerSideDataPortal((s) => s
        .AddInterceptorProvider<TransactionInterceptor>())));
```

In this example, the `AddCsla` method is used to configure the data portal to add the server-side data portal, which has the registration of the interceptor.

It is important to understand that the server-side data portal allows multiple interceptors to be registered, and they are all invoked, so it is possible to have a number of pre- and post-processing operations occur on each root data portal call.

## Built-In Interceptors

CSLA includes a built-in interceptor called the **RevalidatingInterceptor** that is registered by default. This interceptor executes all business rules of the business object graph during pre-processing (before Insert, Update, or Delete operations), ensuring that all rules are run even if the logical client somehow didn't run the rules.

### RevalidatingInterceptor

The RevalidatingInterceptor helps ensure data integrity by validating business rules on the server before data portal operations execute.

**CSLA 9:**
The interceptor validates on all operations (Insert, Update, Delete) with no configuration options.

**CSLA 10:**
The interceptor can be configured using the .NET Options pattern to skip validation during Delete operations. See [v10/RevalidatingInterceptor.md](v10/RevalidatingInterceptor.md) for details on configuring the `IgnoreDeleteOperation` option.

**Common Configuration (All Versions):**

The RevalidatingInterceptor is automatically registered when you call `AddCsla()`. You don't need to manually register it:

```csharp
builder.Services.AddCsla(); // RevalidatingInterceptor is automatically included
```

If you want to disable it entirely, you can clear the interceptor providers (though this is rarely needed):

```csharp
builder.Services.AddCsla((o) => o
    .DataPortal((x) => x
      .AddServerSideDataPortal((s) => s
        .InterceptorProviders.Clear()))); // Removes all interceptors including RevalidatingInterceptor
```
