# Data portal dashboard

It is possible to create a dashboard for the server-side data portal.

In modern CSLA there are two ways to implement a dashboard:

1. Implement the `IDashboard` interface to create a dashboard
2. Use the data portal interceptor concept and implement the `IInterceptDataPortal` to collect dashboard data, then expose that data as you choose (such as via Open Telemetry)

The current recommendation is to use otel via an `IInterceptDataPortal` implementation. See the document about data portal interceptors to see how that interface can be implemented.

## Using the built-in IDashboard types

CSLA includes a default implementation of `Csla.Server.Dashboard.IDashboard` that does no work. It records no data and always returns zero or null values.

CSLA also includes a `Csla.Server.Dashboard.Dashboard` type that is a simple implementation of `IDashboard` that does collect basic data portal usage metrics.

To use this simple dashboard, register it during startup on the logical server-side data portal app, usually in `Program.cs`:

```csharp
builder.Services.AddCsla(o => o
  .DataPortal(dp => dp
    .AddServerSideDataPortal(ss => ss
      .RegisterDashboard<Csla.Server.Dashboard.Dashboard>())));
```

With this configured, as the application runs and the server-side data portal is invoked, the dashboard will record activity. Your code can access the `IDashboard` instance via dependency injection. The dashboard provides the following information:

| Member | Description |
| --- | --- |
| FirstCall | DateTimeOffset of the first call to the data portal after the latest restart |
| LastCall | DateTimeOffset of the most recent call to the data portal |
| TotalCalls | A `long` with the total number of calls made to the data portal since the latest restart |
| FailedCalls | A `long` with the total number of calls that generated an exception since the latest restart |
| CompletedCalls | A `long` with the total number of successful calls since the latest restart |
| GetRecentActivity | Gets a list of `Activity` containing information about the most recent n calls to the data portal; with n set as a configuration value |

An example of using this information would be a web or Blazor page that injects the `IDashboard` instance, and then uses this information to display a graphic dashboard to the user, showing the usage of the data portal.

## Implementing a custom dashboard

You can implement the `IDashboard` interface yourself, and register your type as the dashboard for the data portal. Your dashboard will provide the same values as the built-in dashboard. Outside the `IDashboard` interface, you can choose to provide other values.

To record the activity, the dashboard is invoked by the server-side data portal at the start and end of each root data portal operation. The methods invoked are:

| Method | Description |
| --- | --- |
| `InitializeCall` | Called by the data portal before any server-side processing of the request begins; parameter includes information about the operation type, root business object type, and more |
| `CompleteCall` | Called by the data portal after all server-side processing of the request is complete; parameter includes exception information (if any) |

Your `IDashboard` implementation can record this information, log it to Open Telemetry or other logging technologies, or whatever you choose.

It should be obvious that the dashboard is invoked just like an `IInterceptDataPortal` interceptor. If you don't plan to use the fixed values exposed by the `IDashboard` interface, and will be creating a dashboard with otel metrics, you should choose to implement an interceptor instead of an `IDashboard`.
