# Data portal cache support

The client-side data portal supports caching of domain object graphs. This can be very useful for types that are frequently requested from the server, and which don't change very rapidly.

Examples might include authorization rule data, read-only lists used to populate combobox controls, objects that contain immutable and versioned reference data, etc.

In many cases the caching can be implemented using the built-in .NET MemoryCache capability, and the timeouts for cached items can be set based on how quickly the underlying data is likely to change.

A cache implementation will implement the `Csla.DataPortalClient.IDataPortalCache` interface. This interface has the following members:

| Member | Description |
| --- | --- |
| GetDataPortalResultAsync | Gets a `DataPortalResult` from the data portal or the cache |

The default `IDataPortalCache` implementation in CSLA does no caching, and always delegates to the underlying data portal implementation.

## Example cache implementation

Here is an example of a cache implementation that caches requests to create an object of type `ReferenceData`. Cached values are stored in an `IMemoryCache` service.

```csharp
using System.Text;
using Csla;
using Csla.DataPortalClient;
using Csla.Server;
using Microsoft.Extensions.Caching.Memory;

namespace DataPortalCacheExample
{
  public class DataPortalCache(IMemoryCache cache) : IDataPortalCache
  {
    private readonly IMemoryCache _cache = cache;

    public async Task<DataPortalResult> GetDataPortalResultAsync(Type objectType, object criteria, DataPortalOperations operation, Func<Task<DataPortalResult>> portal)
    {
      if (operation == DataPortalOperations.Create && objectType == typeof(ReferenceData))
      {
        // this operation + type is cached
        return await GetResultAsync(objectType, criteria, operation, portal);
      }
      else
      {
        // the result isn't cached
        return await portal();
      }
    }

    private async Task<DataPortalResult> GetResultAsync(Type objectType, object criteria, DataPortalOperations operation, Func<Task<DataPortalResult>> portal)
    {
      DataPortalResult? result;
      var key = GetKey(objectType, criteria, operation);
      result = await _cache.GetOrCreateAsync(key, async (v) => 
      {
        var obj = await portal();
        v.AbsoluteExpiration = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        return obj;
      });
      if (result != null)
        return result;
      else
        return await portal();
    }

    private static string GetKey(Type objectType, object criteria, DataPortalOperations operation)
    {
      var builder = new StringBuilder();
      // requested type
      builder.Append(objectType.FullName);
      builder.Append('|');

      // criteria values (each criteria has 'valid' ToString)
      var criteriaList = Csla.Server.DataPortal.GetCriteriaArray(criteria);
      foreach (var item in criteriaList)
      {
        builder.Append(item.ToString());
        builder.Append('|');
      }

      // operation
      builder.Append(operation.ToString());
      return builder.ToString();
    }
  }
}
```

In this implementation, the `GetKey` method is responsible for creating a unique key for the cached data portal request/result pair. This usually includes the data portal operation, the type of the root business object, and the criteria used in the data portal request. The key must be able to differentiate between different data portal calls to avoid ambiguity in the cached values.

The `GetResultAsync` method is responsible for returning the requested value from the cache, or adding it to the cache if it is not yet in the cache. It also manages cache expiration.

The public `GetDataPortalResultAsync` is invoked as part of the client-side data portal pipeline, and in this implementation it is responsible for determining which type/operation combinations should be cached.

## Registering a data portal cache

A custom data portal cache is registered in the logical client-side app during startup, usually in `Program.cs`. The registration is part of the `AddCsla` method.

```csharp
// use standard memory cache
services.AddMemoryCache();
// use CSLA with client-side data portal cache
services.AddCsla(o => o
  .DataPortal(o => o
    .AddClientSideDataPortal(o => o
      .DataPortalCacheType = typeof(DataPortalCache))));
```

In this example, because the cache uses an `IMemoryCache` service, the `AddMemoryCache` method is called to set up the standard .NET in-memory cache.

Then the `AddCsla` method is enhanced to configure the data portal; specifically the client-side data portal with the `DataPortalCacheType` property being set to the custom cache implementation.
