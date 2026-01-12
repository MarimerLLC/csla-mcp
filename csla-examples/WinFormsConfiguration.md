# Windows Forms Configuration (Modern)

CSLA supports Windows Forms applications using modern development practices, including dependency injection (DI). This guide focuses on configuring a Windows Forms application for greenfield development, which may differ from legacy approaches.

## Overview

Modern Windows Forms applications can leverage the same DI and configuration concepts as ASP.NET Core and other modern platforms. This simplifies the setup and promotes a consistent architecture across your ecosystem.

**Key characteristics:**

- All code executes on the client
- The local data portal is used by default
- Configuration is done in `Program.cs` using a host builder
- Services, forms, and pages are registered for DI

## Greenfield Configuration

For new Windows Forms applications, it is recommended to use a host builder to configure your application services, including CSLA.

### Application Startup

Configure CSLA and other services in `Program.cs`:

```csharp
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows.Forms;

namespace WinFormsExample
{
  static class Program
  {
    [STAThread]
    static void Main()
    {
      Application.SetHighDpiMode(HighDpiMode.SystemAware);
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      var host = new HostBuilder()
        .ConfigureServices((hostContext, services) =>
        {
          services.AddTransient<MainForm>();
          services.AddTransient<Pages.HomePage>();
          services.AddTransient<Pages.PersonEditPage>();
          services.AddTransient<Pages.PersonListPage>();

          services.AddTransient<DataAccess.IPersonDal, DataAccess.PersonDal>();
          services.AddCsla(o => o.AddWindowsForms());
        })
        .Build();

      host.UseCsla();

      Application.Run(host.Services.GetService<MainForm>());
    }
  }
}
```

## Brownfield Configuration

For existing Windows Forms applications where dependency injection is not used, CSLA can be configured by exposing a `Csla.ApplicationContext` instance as an app-wide static property. This approach uses the service locator pattern, where the application's code can use the static `ApplicationContext` property to access the underlying DI container and create CSLA services.

### Application Startup

Configure CSLA in `Program.cs` and expose a static `ApplicationContext` property:

```csharp
using System;
using System.Windows.Forms;
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrownfieldWinForms
{
  static class Program
  {
    [STAThread]
    static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      
      var services = new ServiceCollection();
      services.AddCsla(o => o.AddWindowsForms());
      var provider = services.BuildServiceProvider();
      ApplicationContext = provider.GetService<ApplicationContext>();

      Application.Run(new MainForm());
    }

    public static ApplicationContext ApplicationContext { get; private set; }
  }
}
```

Your code can then access any CSLA service via this static property. For example, you can get a data portal instance like this:

```csharp
var portal = Program.ApplicationContext.GetRequiredService<IDataPortal<MyBusinessClass>>();
```

## Key Configuration Points

### AddWindowsForms()

The `AddWindowsForms()` extension method configures CSLA for Windows Forms hosting:

```csharp
builder.Services.AddCsla(options => options.AddWindowsForms());
```

This registers services required for a Windows Forms application to work correctly, such as binding list converters.

### UseCsla()

The `UseCsla()` method on the host applies the CSLA configuration:

```csharp
host.UseCsla();
```

This should be called after the host is built.

## Using CSLA in a Form

### Form with Dependency Injection

Inject the data portal and other services into your forms and user controls:

```csharp
using BusinessLayer;
using Csla;
using Csla.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Forms;

namespace WinFormsExample.Pages
{
  public partial class PersonEditPage : UserControl
  {
    private IDataPortal<PersonEdit> _portal;
    private PersonEdit _person;

    public PersonEditPage(IDataPortal<PersonEdit> portal)
    {
      _portal = portal;
      InitializeComponent();
    }

    public async void LoadData(int id)
    {
      if (id == -1)
        _person = await _portal.CreateAsync();
      else
        _person = await _portal.FetchAsync(id);
      personEditBindingSource.DataSource = _person;
    }

    private void saveButton_Click(object sender, EventArgs e)
    {
      personEditBindingSource.EndEdit();
      _person = await _person.SaveAsync();
      // TODO: navigate back to list page
    }
  }
}
```

### Form without Dependency Injection

In an application that doesn't use DI, you can get a data portal instance from the static `ApplicationContext` property:

```csharp
using BusinessLayer;
using Csla;
using System;
using System.Windows.Forms;

namespace BrownfieldWinForms.Pages
{
  public partial class PersonEditPage : UserControl
  {
    private IDataPortal<PersonEdit> _portal;
    private PersonEdit _person;

    public PersonEditPage()
    {
      _portal = Program.ApplicationContext.GetRequiredService<IDataPortal<PersonEdit>>();
      InitializeComponent();
    }

    public async void LoadData(int id)
    {
      if (id == -1)
        _person = await _portal.CreateAsync();
      else
        _person = await _portal.FetchAsync(id);
      personEditBindingSource.DataSource = _person;
    }

    private void saveButton_Click(object sender, EventArgs e)
    {
      personEditBindingSource.EndEdit();
      _person = await _person.SaveAsync();
      // TODO: navigate back to list page
    }
  }
}
```

## Data Binding

Windows Forms data binding is a powerful feature that works well with CSLA business objects. The `BindingSource` component is key to this integration.

-   Set the `BindingSource`'s `DataSource` property to your CSLA business object.
-   Set the `DataSourceUpdateMode` property to `OnPropertyChanged` to ensure the UI and object are synchronized.
-   Bind UI controls to the `BindingSource`.

CSLA business objects implement `INotifyPropertyChanged`, so any changes made in the UI will be immediately reflected in the object, and any changes made in the object (e.g., by business rules) will be reflected in the UI.

## Multi-Tier Configuration

If your Windows Forms application calls a separate data portal server, you need to configure a client-side data portal proxy.

```csharp
builder.Services.AddCsla(options => options
  .AddWindowsForms()
  .DataPortal(dp => dp
    .AddClientSideDataPortal(client => client
      .UseHttpProxy(proxy => proxy
        .DataPortalUrl = "https://appserver.example.com/api/DataPortal"))));
```

## Best Practices

1.  **Use dependency injection** - Always inject `IDataPortal<T>` rather than using static methods.
2.  **Use a host builder** - Configure your application using the `IHostBuilder` for a modern, DI-first approach.
3.  **Use the `BindingSource` component** - This is the best way to manage data binding between your UI controls and CSLA business objects.
4.  **Use async methods** - Prefer `FetchAsync`, `SaveAsync`, etc. for a responsive user interface.
5.  **Handle validation** - Check `IsSavable` before calling `SaveAsync()`.
6.  **Register DAL services** - Use dependency injection for data access layer components if using the local data portal.

