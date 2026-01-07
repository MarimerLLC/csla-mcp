# Data Portal Activator

When the server-side data portal needs to create an instance of a type, it uses an `IDataPortalActivator` to do that work.

The default activator does no mapping; it just returns instances of the requested type.

It is possible to create a custom activator that maps types to other types. This is particularly useful for scenarios where an interface type needs to map to a concrete implementation.

Another thing the activator can do, is initialize and finalize an object instance. This was primarily useful before dependency injection was available in .NET, but may still be useful in some scenarios.

The `IDataPortalActivator` defines the following members:

| Member | Description |
| --- | --- |
| `ResolveType` | Resolves a type to (potentially) another type; allows mapping types to other tyeps, such as `ICustomerFactory` to `ConcreteCustomerFactory` |
| `CreateInstance` | Creates an instance of a type (possibly after mapping it to another type via `ResolveType`) |
| `InitializeInstance` | Called immediately after an instance of a type is created; allows things like setting properties or calling methods on the new object |
| `FinalizeInstance` | Called immediately before the server-side data portal completes the root data portal request; allows things like setting properties or calling methods on the object so it can do any clean-up before being dereferenced |

## Example activator

The following is a custom activator that does simple type mapping and logging to the console:

```csharp
using System;
using Csla;
using Csla.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CustomActivator
{
  public class CustomActivator : Csla.Server.IDataPortalActivator
  {
    public CustomActivator(ApplicationContext applicationContext)
    {
      ApplicationContext = applicationContext;
    }

    private ApplicationContext ApplicationContext { get; set; }

    public void InitializeInstance(object obj)
    {
      Console.WriteLine($"InitializeInstance({obj.GetType().Name})");
    }

    public void FinalizeInstance(object obj)
    {
      Console.WriteLine($"FinalizeInstance({obj.GetType().Name})");
    }

    public object CreateInstance(Type requestedType, params object[] parameters)
    {
      Console.WriteLine($"{nameof(CreateInstance)}({requestedType.Name})");
      object result;
      var realType = ResolveType(requestedType);
      var serviceProvider = (IServiceProvider)ApplicationContext.GetRequiredService<IServiceProvider>();
      result = ActivatorUtilities.CreateInstance(serviceProvider, realType, parameters);
      if (result is IUseApplicationContext tmp)
      {
        tmp.ApplicationContext = serviceProvider.GetRequiredService<ApplicationContext>();
      }
      InitializeInstance(result);
      return result;
    }

    public Type ResolveType(Type requestedType)
    {
      var resolvedType = requestedType;
      if (requestedType.Equals(typeof(ITestItem)))
        resolvedType = typeof(TestItem);
      Console.WriteLine($"{nameof(ResolveType)}({requestedType.Name})->{resolvedType.Name}");
      return resolvedType;
    }
  }
}
```

## Registering a custom activator

A custom activator is registered during app startup, usually in `Program.cs`. Registration is part of the `AddCsla` call. For example:

```csharp
      services.AddCsla(opt => opt
        .DataPortal(dpo => dpo
          .AddServerSideDataPortal(sso => sso
            .RegisterActivator<CustomActivator>()
            )
          )
        );
```

The server-side data portal uses exactly one activator, so registering a custom activator replaces the default activator.
