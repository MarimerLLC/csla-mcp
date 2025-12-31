# Data portal exception inspector

The server-side data portal can use a custom exception inspector of type `IDataPortalExceptionInspector`. The purpose of this inspector is to change the type of a server-side exception into a different exception type before returning it to the logical client-side data portal.

This can be useful in rare cases where server-side exceptions shouldn't be allowed to flow back to the caller due to security or privacy concerns. An exception inspector can inspect the actual exception, and decide whether to change it to a different exception, possibly obscuring the orignal exception type and exception stack trace information.

## Exception inspector implementation

The following is an example of a `Csla.Server.IDataPortalExceptionInspector` implementation that prevents returning of non-serializable exceptions and also the specific `ServerOnlyException` type.

```csharp
using System;
using System.Diagnostics;
using Csla.Server;

namespace AppServer
{
  public class MyDataPortalExceptionInspector : Csla.Server.IDataPortalExceptionInspector
  {
    public void InspectException(Type objectType, object businessObject, object criteria, string methodName, Exception ex)
    {
      // add your logging code here for exceptions on the server
      //Trace.TraceError("Server exception: {0} with {1}, stackTrace {2}", objectType.FullName, ex.ToString(), ex.StackTrace);
      Debug.Print("Server exception: {0} with {1}, stackTrace {2}", objectType.FullName, ex.ToString(), ex.StackTrace);

      // Transform to other exception to return to client
      if (!IsSerializable(ex) || ex.GetType().FullName.Contains("ServerOnlyException"))
        // transform to genereic exception to send to client
        throw new GenericBusinessException(ex);
    }

    private bool IsSerializable(Exception ex)
    {
      if (!ex.GetType().IsSerializable) return false;
      if (ex.InnerException != null)
      {
        return IsSerializable(ex.InnerException);
      }
      return true;
    }

    private GenericBusinessException ToGenericBusinessException(Exception ex)
    {
      if (ex.InnerException != null)
        return new GenericBusinessException(ex, ToGenericBusinessException(ex.InnerException));
      else
        return new GenericBusinessException(ex);
    }
  }
}
```

The `InspectException` method is invoked by the server-side data portal if an exception occurred during normal data portal operation processing. If an exception occurs, the data portal calls this method to provide an opportunity to change the type of exception returned to the client-side data portal.

In this case, no `ServerOnlyException` is ever returned to the client-side data portal. Instead, it is replaced by a custom `GenericBusinessException` instance.

## Registering an exception inspector

A custom exception inspector is registered on the device where the logical server-side data portal will execute. It is registered as the app starts up, typically in `Program.cs` as part of the `AddCsla` method.

```csharp
builder.Services.AddCsla(o => o
  .DataPortal(dpo => dpo
    .AddServerSideDataPortal(sso => sso
      .RegisterExceptionInspector<AppServer.MyDataPortalExceptionInspector>())));
```

Notice how the `RegisterExceptionInspector` method is called to set the type of the exception inspector during configuration of the server-side data portal.

The default exception inspector does not filter or change any exception types. Registering a different implementation will replace the default implementation.
