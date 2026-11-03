# Azure Table Storage Session Store

This document applies to CSLA 11. It describes the `Csla.Blazor.State.AzureTableStorage` package, an `ISessionStore` implementation that keeps Blazor user sessions in an Azure Storage table, so they survive a server restart and can move to another server.

For the session store model itself (`ISessionStore`, `SessionStoreType`, the in-memory default), see the "Custom Session Store" section of `BlazorConfiguration.md`.

## When to use it

The server-side `SessionManager` keeps each user's session (principal, `LocalContext`, `ClientContext`) in an `ISessionStore`. The default `InMemorySessionStore` loses every session when the server restarts, and each server has its own sessions.

Use the Azure Table Storage store when:

- Sessions must survive an app restart or redeploy.
- A user's session must be available on another server after a failover or scale-in.
- The app already uses Azure Storage, or runs under .NET Aspire with an Azure Storage resource.

The store is only used with Blazor state management, which means `UseInMemoryApplicationContextManager = false`. With the default (`true`), CSLA uses a no-op session manager and no session store.

## Install and register

Add the package to the **server** project (not the Blazor WebAssembly client):

```xml
<PackageReference Include="Csla.Blazor.State.AzureTableStorage" Version="11.0.0" />
```

Register it after `AddServerSideBlazor`:

```csharp
using Csla.Configuration;

builder.Services.AddCsla(o => o
  .AddAspNetCore()
  .AddServerSideBlazor(b => b.UseInMemoryApplicationContextManager = false)
  .UseAzureTableStorageSessionStore(s =>
  {
    s.ConnectionString = builder.Configuration.GetConnectionString("SessionStorage");
    s.TableName = "CslaStateSessionManager";
    s.ApplicationDiscriminator = "MyApp";
  })
  .AddSessionPurging());
```

- `UseAzureTableStorageSessionStore` must come **after** `AddServerSideBlazor`. `AddServerSideBlazor` registers the in-memory store, and the last `ISessionStore` registration wins.
- The store is registered as a singleton, because the singleton `SessionManager` depends on it.
- `AddSessionPurging` (in `Csla.AspNetCore`) removes expired sessions and keeps live ones from being purged by other servers. See [Purging expired sessions](#purging-expired-sessions).

## Choosing the TableServiceClient

The store needs an `Azure.Data.Tables.TableServiceClient`. It picks one in this order:

1. `ConnectionString` is set: the store creates a client from it.
2. `ServiceKey` is set: the store resolves the keyed `TableServiceClient` with that key.
3. Otherwise: the store resolves the default `TableServiceClient` from the container.

The store resolves the client for its own use and never registers one, so it doesn't change which `TableServiceClient` the rest of the app receives.

Connection string (development, or a storage account key):

```csharp
.UseAzureTableStorageSessionStore(s => s.ConnectionString = "UseDevelopmentStorage=true")
```

Managed identity, with a client the app registers itself (requires the `Azure.Identity` package):

```csharp
builder.Services.AddSingleton(new TableServiceClient(
  new Uri("https://mystorageaccount.table.core.windows.net"),
  new DefaultAzureCredential()));

builder.Services.AddCsla(o => o
  .AddAspNetCore()
  .AddServerSideBlazor(b => b.UseInMemoryApplicationContextManager = false)
  .UseAzureTableStorageSessionStore());
```

.NET Aspire, with a keyed client so the session table doesn't share the app's default client:

```csharp
builder.AddKeyedAzureTableServiceClient("sessions");

builder.Services.AddCsla(o => o
  .AddAspNetCore()
  .AddServerSideBlazor(b => b.UseInMemoryApplicationContextManager = false)
  .UseAzureTableStorageSessionStore(s => s.ServiceKey = "sessions"));
```

## Options

`AzureTableStorageOptions` (namespace `Csla.Blazor.State.AzureTableStorage`):

| Property | Default | Purpose |
| --- | --- | --- |
| `ConnectionString` | `null` | Azure Storage connection string. When set, `ServiceKey` is ignored. |
| `ServiceKey` | `null` | Key of a keyed `TableServiceClient` registered in DI. |
| `TableName` | `CslaStateSessionManager` | Table that holds the sessions. Created if it doesn't exist. |
| `ApplicationDiscriminator` | `""` | Separates the sessions of apps that share a table. Give each app its own value. |

## Table layout

Each session is one row:

| Column | Contents |
| --- | --- |
| `PartitionKey` | The session key (from `ISessionIdManager`) |
| `RowKey` | `ApplicationDiscriminator` |
| `LastTouched` | `Session.LastTouched`, Unix time in seconds (UTC) |
| `Value`, `Value1` ... `Value10` | The serialized session, split into 64 KiB chunks |

Azure Tables limits a binary property to 64 KiB, so the data is split across up to 11 columns. A session can serialize to at most **704 KiB**. A larger session throws `InvalidOperationException` before anything is written or cached.

The session is serialized with the app's configured CSLA `ISerializationFormatter` (`MobileFormatter` by default). The principal and every value your app puts into `LocalContext` or `ClientContext` must be serializable by it.

## How the store behaves

- **Live sessions stay in memory.** The store keeps the sessions it has loaded in a local cache and returns them by reference, the same way `InMemorySessionStore` does. CSLA changes the returned session in place, for example when `LocalContext` or `ClientContext` is set.
- **The table is read only for sessions that aren't in memory**, such as after a restart or when a user arrives on a different server.
- **Writes happen on save.** Session data is written to the table only by `AddOrUpdateSessionAsync`: when a session is first created, and when the WebAssembly client sends its state back through `CslaStateController`. Changes made in place on the server reach the table with the next save. If the server stops before that, those changes are lost.
- **Use session affinity with more than one server.** Each server serves the sessions it has cached and doesn't reread them from the table, so a user must stay on the same server, as Blazor Server already requires. The table is what lets the session continue on another server after a restart, redeploy or failover.
- **Read and write failures propagate.** A storage error surfaces as an exception from `SessionManager.GetSession` or `UpdateSession`, so the request fails instead of silently starting with an empty session.
- **The table is created on first use.** The store is created the first time `ISessionStore` is resolved, and it calls `CreateIfNotExists` then. If storage isn't reachable, that first resolution throws. With `AddSessionPurging` this happens at app startup, because the purging service resolves the session manager when the host starts.
- **Business objects restored from the table** keep the `ApplicationContext` of the scope that deserialized them, the same as sessions that arrive through `CslaStateController`.

## Purging expired sessions

Register `AddSessionPurging` (in `Csla.AspNetCore`) so the server calls `ISessionManager.PurgeSessions` on a schedule:

```csharp
.AddSessionPurging(p =>
{
  p.Interval = TimeSpan.FromMinutes(5);   // default: 5 minutes
  p.Expiration = TimeSpan.FromHours(1);   // default: 1 hour
})
```

Each purge with this store:

1. Removes this server's cached sessions that haven't been touched within `Expiration`.
2. Writes the current `LastTouched` of every live cached session to the table. Only that column is written, so a session a request is changing is never serialized.
3. Deletes the table rows for this `ApplicationDiscriminator` whose `LastTouched` is older than `Expiration` and that aren't cached on this server. Each delete uses the ETag from the query, so a row saved after the query is kept.

Step 2 is why purging matters with more than one server: without it, the stored `LastTouched` of a user who stays active on server A goes stale, and server B's purge deletes the row. Keep `Interval` well below `Expiration`, and run purging on every server.

Purging is best effort. A failed refresh or delete is logged as a warning, a row skipped because it was saved after the query is logged at Debug, and the sweep continues. If the whole purge fails, `AddSessionPurging` logs the error and runs the next purge on schedule.

If you don't use `AddSessionPurging`, call `ISessionManager.PurgeSessions(expiration)` periodically yourself.

## Local development with Azurite

Run the Azurite table service and use the development connection string:

```bash
docker run -p 10002:10002 mcr.microsoft.com/azure-storage/azurite azurite-table --tableHost 0.0.0.0
```

```csharp
.UseAzureTableStorageSessionStore(s => s.ConnectionString = "UseDevelopmentStorage=true")
```

## Common mistakes

- **Calling `UseAzureTableStorageSessionStore` before `AddServerSideBlazor`.** The in-memory store registered by `AddServerSideBlazor` wins, and sessions never reach the table.
- **Leaving `UseInMemoryApplicationContextManager` at `true`.** No session store is used at all.
- **Several apps sharing a table with the same `ApplicationDiscriminator`.** Each app's purge then treats the other app's rows as its own and deletes them by its own `Expiration`. Give each app its own value.
- **Not purging.** Expired rows are never deleted. With more than one server, purging on some servers but not others is worse: a server that doesn't purge never refreshes the stored `LastTouched` of its live sessions, so the other servers delete them.
- **Putting large or non-serializable values in `LocalContext` or `ClientContext`.** The session must serialize with the CSLA formatter and fit in 704 KiB.
- **Adding the package to the WebAssembly client.** The store runs on the server only.
