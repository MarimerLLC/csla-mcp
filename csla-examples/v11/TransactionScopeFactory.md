# Transactions and ITransactionScopeFactory

This document applies to CSLA 11. It describes how the server-side data portal runs data portal operations inside a transaction: the `[Transactional]` attribute, the `DataOptions` defaults, and the `ITransactionScopeFactory` service that creates the `TransactionScope`. Several details changed from CSLA 10 (see [Changes from CSLA 10](#changes-from-csla-10)).

## Transaction types

The `TransactionalTypes` enum (namespace `Csla`) selects the transaction technology:

| Value | Description |
|---|---|
| `Manual` | No automatic transaction. Your code manages transactions itself (for example an ADO.NET `DbTransaction`). This is the behavior when a method has no `[Transactional]` attribute. |
| `TransactionScope` | The data portal wraps the operation in a `System.Transactions.TransactionScope`. This is the default when `[Transactional]` is applied without a type. |
| `EnterpriseServices` | COM+ transactions. Only exists in the .NET Framework builds of CSLA. |

`TransactionScope` transactions are not supported in the browser (Blazor WebAssembly). The data portal throws `PlatformNotSupportedException` if a `TransactionScope` operation runs there. In practice this is not an issue because the server-side data portal normally runs on a server.

## Using [Transactional]

Put `[Transactional]` on a root data portal operation method (`[Create]`, `[Fetch]`, `[Insert]`, `[Update]`, `[DeleteSelf]`, `[Delete]`, `[Execute]`) or on an `ObjectFactory` method. The transaction wraps the entire root operation, including any child data portal operations called from it, and is committed only if the operation completes without an exception.

```csharp
using System.Threading.Tasks;
using Csla;

[CslaImplementProperties]
public partial class OrderEdit : BusinessBase<OrderEdit>
{
  public partial int Id { get; private set; }
  public partial string CustomerName { get; set; }
  public partial LineItems LineItems { get; private set; }

  [Transactional(TransactionalTypes.TransactionScope)]
  [Insert]
  private async Task Insert([Inject] IOrderDal dal)
  {
    var id = await dal.InsertAsync(CustomerName);
    LoadProperty(IdProperty, id);
    // child updates run inside the same transaction
    await FieldManager.UpdateChildrenAsync(this);
  }

  [Transactional(TransactionalTypes.TransactionScope)]
  [Update]
  private async Task Update([Inject] IOrderDal dal)
  {
    await dal.UpdateAsync(Id, CustomerName);
    await FieldManager.UpdateChildrenAsync(this);
  }
}
```

### Attribute constructors

```csharp
[Transactional]                                    // TransactionScope, all settings from DataOptions
[Transactional(TransactionalTypes.TransactionScope)]
[Transactional(TransactionalTypes.TransactionScope, TransactionIsolationLevel.ReadCommitted)]
[Transactional(TransactionalTypes.TransactionScope, TransactionIsolationLevel.ReadCommitted,
  TransactionScopeAsyncFlowOption.Enabled)]
[Transactional(TransactionalTypes.TransactionScope, TransactionIsolationLevel.ReadCommitted, 120)]  // timeout in seconds
[Transactional(TransactionalTypes.TransactionScope, TransactionIsolationLevel.ReadCommitted, 120,
  TransactionScopeAsyncFlowOption.Enabled)]
```

`TransactionScopeAsyncFlowOption` is in the `System.Transactions` namespace. `TransactionIsolationLevel` is a CSLA enum (`Csla.TransactionIsolationLevel`) with the values `Unspecified`, `Serializable`, `RepeatableRead`, `ReadCommitted`, `ReadUncommitted` and `Snapshot`.

### Attribute properties

All properties are read-only and are set through the constructor.

| Property | Type | Value when not specified |
|---|---|---|
| `TransactionType` | `TransactionalTypes` | `TransactionScope` |
| `TransactionIsolationLevel` | `TransactionIsolationLevel` | `TransactionIsolationLevel.Unspecified` |
| `TimeoutInSeconds` | `int?` | `null` |
| `AsyncFlowOption` | `TransactionScopeAsyncFlowOption?` | `null` |

A value that is `Unspecified` or `null` means "use the configured default". The defaults are read from `DataOptions` **when the transaction is created**, not when the attribute is constructed.

## Default transaction settings (DataOptions)

`Csla.Configuration.DataOptions` holds the defaults used for any value the attribute does not specify:

| Property | Type | Default |
|---|---|---|
| `DefaultTransactionIsolationLevel` | `TransactionIsolationLevel` | `Unspecified` |
| `DefaultTransactionTimeoutInSeconds` | `int` | `30` |
| `DefaultTransactionAsyncFlowOption` | `TransactionScopeAsyncFlowOption` | `Suppress` |

Configure them with the `Data` method on the CSLA options:

```csharp
using System.Transactions;
using Csla;
using Csla.Configuration;

builder.Services.AddCsla(o => o
  .Data(d =>
  {
    d.DefaultTransactionIsolationLevel = TransactionIsolationLevel.ReadCommitted;
    d.DefaultTransactionTimeoutInSeconds = 60;
    d.DefaultTransactionAsyncFlowOption = TransactionScopeAsyncFlowOption.Enabled;
  }));
```

In CSLA 11 these values belong to each `CslaOptions` instance. They are no longer static, so two hosts (or two test hosts) in the same process can use different defaults.

If both the attribute and `DefaultTransactionIsolationLevel` are `Unspecified`, the default factory creates the `TransactionScope` with `IsolationLevel.Unspecified`, and System.Transactions uses its own default isolation level (`Serializable`).

**Async operation methods:** when your operation methods are `async`, use `TransactionScopeAsyncFlowOption.Enabled`, either on the attribute or as the `DataOptions` default. With the default `Suppress`, the ambient transaction does not flow across `await`, so data access code after an `await` may not enlist in the transaction, and disposing the scope on a different thread fails.

## ITransactionScopeFactory

For `TransactionalTypes.TransactionScope`, the server-side data portal (`TransactionalDataPortal`) gets the `TransactionScope` from an `ITransactionScopeFactory` service (namespace `Csla.Server`):

```csharp
public interface ITransactionScopeFactory
{
  TransactionScope CreateTransactionScope(TransactionScopeFactoryContext context);
}
```

The data portal calls `Complete()` on the returned scope when the operation succeeds and disposes it afterwards. The factory only creates the scope.

The factory is not used for `Manual` or `EnterpriseServices` transactions.

### TransactionScopeFactoryContext

`Csla.Server.TransactionScopeFactoryContext` describes the operation being run:

| Member | Type | Description |
|---|---|---|
| `ObjectType` | `Type` | The business object type. |
| `Operation` | `DataPortalOperations` | `Create`, `Fetch`, `Update`, `Delete` or `Execute`. Insert, update and delete-self of an editable object all report `Update`. A command object reports `Execute`. |
| `Method` | `MethodInfo` | The method that implements the operation. This can be a method of the business type or of an `ObjectFactory` type. |
| `MethodName` | `string` | `Method.Name`. Use it (or the method's attributes) to tell `Insert`, `Update` and `DeleteSelf` apart. |
| `TransactionalAttribute` | `TransactionalAttribute` | The attribute applied to the method. |
| `BusinessObject` | `ICslaObject?` | The object being inserted, updated, deleted or executed. `null` for criteria-based operations (create, fetch, delete by criteria, execute by criteria). |
| `Criteria` | `IReadOnlyList<object?>` | The criteria values for create, fetch, execute and delete operations. Empty when there are no criteria or when the operation is based on a business object. |

`BusinessObject` is the server-side instance (including its children) before the operation runs. Treat it as read-only. A factory must not change it.

### DefaultTransactionScopeFactory

`Csla.Server.DefaultTransactionScopeFactory` is registered by default. It uses the attribute values and falls back to `DataOptions` for anything the attribute leaves unspecified. It creates the scope with `TransactionScopeOption.Required`. Its members:

```csharp
public class DefaultTransactionScopeFactory : ITransactionScopeFactory
{
  public DefaultTransactionScopeFactory(CslaOptions options);

  // The DataOptions that provide the default values
  protected DataOptions DataOptions { get; }

  // Creates new TransactionScope(TransactionScopeOption.Required,
  //   GetTransactionOptions(context), GetAsyncFlowOption(context))
  public virtual TransactionScope CreateTransactionScope(TransactionScopeFactoryContext context);

  // Isolation level and timeout: the attribute value, or the DataOptions default
  protected virtual TransactionOptions GetTransactionOptions(TransactionScopeFactoryContext context);

  // The attribute AsyncFlowOption, or DataOptions.DefaultTransactionAsyncFlowOption
  protected virtual TransactionScopeAsyncFlowOption GetAsyncFlowOption(TransactionScopeFactoryContext context);
}
```

Subclass it and override one of the `protected virtual` hooks when you only need to adjust part of the behavior.

### Registering a custom factory

Register the factory type on the server-side data portal options. The service is registered with a scoped lifetime and created through DI, so its constructor can take other services.

```csharp
builder.Services.AddCsla(o => o
  .DataPortal(dp => dp
    .AddServerSideDataPortal(s => s
      .RegisterTransactionScopeFactory<BatchAwareTransactionScopeFactory>())));
```

Register it wherever the server-side data portal runs (the application server for a remote data portal, or the client app in a 1- or 2-tier deployment).

## Example: longer timeout for large batches

This factory keeps the default behavior, but gives large saves more time. It checks the number of items in the object being saved, and also a batch size passed as criteria to a command.

```csharp
using System;
using System.Collections;
using System.Linq;
using System.Transactions;
using Csla;
using Csla.Configuration;
using Csla.Server;

public class BatchAwareTransactionScopeFactory(CslaOptions options)
  : DefaultTransactionScopeFactory(options)
{
  private const int LargeBatchSize = 1000;

  protected override TransactionOptions GetTransactionOptions(TransactionScopeFactoryContext context)
  {
    var transactionOptions = base.GetTransactionOptions(context);

    if (IsLargeBatch(context))
      transactionOptions.Timeout = TimeSpan.FromMinutes(5);

    return transactionOptions;
  }

  private static bool IsLargeBatch(TransactionScopeFactoryContext context)
  {
    // Saving a large editable list (BusinessBase objects are not ICollection)
    if (context.BusinessObject is ICollection { Count: > LargeBatchSize })
      return true;

    // A command executed with a batch size as criteria
    if (context.Operation == DataPortalOperations.Execute
        && context.Criteria.OfType<int>().Any(size => size > LargeBatchSize))
      return true;

    return false;
  }
}
```

System.Transactions caps every scope timeout at `TransactionManager.MaximumTimeout` (10 minutes by default), so a larger value has no effect unless that machine-wide limit is also raised.

## Example: factory written from scratch

Implement `ITransactionScopeFactory` directly when you want full control. This one ignores the attribute's isolation level and uses `ReadCommitted` for fetch operations and `Serializable` for everything else.

```csharp
using System;
using System.Transactions;
using Csla;
using Csla.Server;

public class OperationTransactionScopeFactory : ITransactionScopeFactory
{
  public TransactionScope CreateTransactionScope(TransactionScopeFactoryContext context)
  {
    var options = new TransactionOptions
    {
      IsolationLevel = context.Operation == DataPortalOperations.Fetch
        ? IsolationLevel.ReadCommitted
        : IsolationLevel.Serializable,
      Timeout = TimeSpan.FromSeconds(context.TransactionalAttribute.TimeoutInSeconds ?? 30)
    };
    return new TransactionScope(TransactionScopeOption.Required, options, TransactionScopeAsyncFlowOption.Enabled);
  }
}
```

Note that `IsolationLevel` here is `System.Transactions.IsolationLevel`, not `Csla.TransactionIsolationLevel`.

## Changes from CSLA 10

| CSLA 10 | CSLA 11 |
|---|---|
| The attribute constructor copied the `DataOptions` defaults into the attribute. Because CSLA caches attributes, configuration changes after first use were ignored. | Unspecified values stay unspecified on the attribute and are resolved from `DataOptions` when the transaction is created. |
| `TimeoutInSeconds` was `int`. | `TimeoutInSeconds` is `int?` (`null` = use the default). |
| `AsyncFlowOption` was `TransactionScopeAsyncFlowOption`. | `AsyncFlowOption` is `TransactionScopeAsyncFlowOption?` (`null` = use the default). |
| `DataOptions` defaults were stored in static fields shared by all `CslaOptions` instances. | The defaults are per `CslaOptions` instance. |
| `TransactionalDataPortal(DataPortalBroker, TransactionalAttribute)` created the scope itself. | `TransactionalDataPortal(DataPortalBroker, ITransactionScopeFactory, TransactionScopeFactoryContext)`; the scope comes from `ITransactionScopeFactory`. |
| With `EnterpriseServices`, an unspecified isolation level threw `ArgumentOutOfRangeException`. | It uses `DataOptions.DefaultTransactionIsolationLevel`, then `Serializable`. |

Code that reads the attribute values must handle `null`:

```csharp
// CSLA 10
int timeout = attribute.TimeoutInSeconds;

// CSLA 11
int timeout = attribute.TimeoutInSeconds ?? dataOptions.DefaultTransactionTimeoutInSeconds;
```

Code that constructed `TransactionalDataPortal` directly (rare, usually in custom data portal servers or tests) must pass a factory and context instead of the attribute. Most applications don't need to change anything.
