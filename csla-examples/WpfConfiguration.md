# WPF Configuration (Modern)

CSLA supports Windows Presentation Foundation (WPF) applications using modern development practices, including dependency injection (DI). This guide focuses on configuring a WPF application for greenfield development, which may differ from legacy approaches.

## Overview

Modern WPF applications can leverage the same DI and configuration concepts as ASP.NET Core and other modern platforms. This simplifies the setup and promotes a consistent architecture across your ecosystem.

**Key characteristics:**

- All code executes on the client
- The local data portal is used by default
- Configuration is done in `App.xaml.cs` using a host builder
- Services, windows, and pages are registered for DI

## Greenfield Configuration

For new WPF applications, it is recommended to use a host builder to configure your application services, including CSLA.

### Application Startup

Configure CSLA and other services in `App.xaml.cs`:

```csharp
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace WpfExample
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private IHost Host { get; }

    public App()
    {
      Host = new HostBuilder()
        .ConfigureServices((hostContext, services) => services
          // register window and page types here
          .AddSingleton<MainWindow>()
          .AddTransient<Pages.HomePage>()
          .AddTransient<Pages.PersonEditPage>()
          .AddTransient<Pages.PersonListPage>()

          // add other services
          .AddTransient<DataAccess.IPersonDal, DataAccess.PersonDal>()
          .AddCsla(options => options.AddXaml())
        ).Build();

      Host.UseCsla();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
      var window = Host.Services.GetService<MainWindow>();
      window?.Show();
    }
  }
}
```

## Brownfield Configuration

For existing WPF applications where dependency injection is not used, CSLA can be configured by exposing a `Csla.ApplicationContext` instance as an app-wide static property. This approach uses the service locator pattern, where the application's code can use the static `ApplicationContext` property to access the underlying DI container and create CSLA services.

### Application Startup

Configure CSLA in `App.xaml.cs` and expose a static `ApplicationContext` property:

```csharp
using System;
using System.Windows;
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrownfieldWpf
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    public App()
    {
      var services = new ServiceCollection();
      services.AddCsla();
      var provider = services.BuildServiceProvider();
      ApplicationContext = provider.GetService<ApplicationContext>();
    }

    public static ApplicationContext ApplicationContext { get; private set; }
  }
}
```

Your code can then access any CSLA service via this static property. For example, you can get a data portal instance like this:

```csharp
var portal = App.ApplicationContext.GetRequiredService<IDataPortal<MyBusinessClass>>();
```



### Key Configuration Points

#### AddXaml()

The `AddXaml()` extension method configures CSLA for WPF hosting:

```csharp
builder.Services.AddCsla(options => options.AddXaml());
```

This registers services required for a XAML-based application to work correctly.

#### UseCsla()

The `UseCsla()` method on the host applies the CSLA configuration:

```csharp
Host.UseCsla();
```

This should be called after the host is built.

## PropertyStatus XAML Helper

CSLA includes a XAML helper control called `PropertyStatus` that simplifies displaying validation, business rule, and authorization messages for a specific property.

The `PropertyStatus` control is typically bound to a business object and a specific property. It automatically displays:
- Validation error messages
- Validation warning messages
- Business rule information messages
- Authorization denied messages
- Authorization allowed messages
- Busy status when a rule is running asynchronously

By using `PropertyStatus`, you can easily provide rich feedback to the user without writing complex code in your viewmodels or views.

```xml
<Window x:Class="PropertyStatus.Window1"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:csla="clr-namespace:Csla.Xaml;assembly=Csla.Xaml"
        Title="Window1" Height="250" Width="300"
        FontSize="16">
  <StackPanel>
    <TextBlock Text="To get an invalid state enter the values 'Error', 'Warning' or 'Information' and click Save"
                   TextWrapping="Wrap" Margin="10,0" />
    <TextBox x:Name="txtData" Text="{Binding Path=Data, Mode=TwoWay}" IsEnabled="{Binding ElementName=dataPropertyStatus, Path=CanWrite}" Margin="10" />
    <StackPanel Orientation="Horizontal" Margin="10,0">

      <TextBlock Text="Default:" Margin="2,2,2,2"  />
      <csla:PropertyStatus Name="dataPropertyStatus" 
                                 Property="{Binding Data}"
                                 Margin="2,2,2,2" />
    </StackPanel>
  </StackPanel>
</Window>
```

Note the `xmlns:csla="clr-namespace:Csla.Xaml;assembly=Csla.Xaml"` namespace declaration that makes the `csla:PropertyStatus` control available in XAML.


In this example, the `PropertyStatus` control will display information about the `Name` property of the bound business object.

## Using CSLA in a ViewModel

### ViewModel with Dependency Injection

Inject the data portal into your viewmodels:

```csharp
using Csla;
using System.Threading.Tasks;

public class PersonEditViewModel
{
  private readonly IDataPortal<Person> _portal;
  public Person Person { get; set; }

  public PersonEditViewModel(IDataPortal<Person> portal)
  {
    _portal = portal;
  }

  public async Task LoadPerson(int id)
  {
    Person = await _portal.FetchAsync(id);
  }

  public async Task SavePerson()
  {
    if (Person.IsSavable)
    {
      Person = await Person.SaveAsync();
    }
  }
}
```

### ViewModel without Dependency Injection

In an application that doesn't use DI, you can get a data portal instance from the static `ApplicationContext` property:

```csharp
using Csla;
using System.Threading.Tasks;

public class PersonEditViewModel
{
  private readonly IDataPortal<Person> _portal;
  public Person Person { get; set; }

  public PersonEditViewModel()
  {
    _portal = App.ApplicationContext.GetRequiredService<IDataPortal<Person>>();
  }

  public async Task LoadPerson(int id)
  {
    Person = await _portal.FetchAsync(id);
  }

  public async Task SavePerson()
  {
    if (Person.IsSavable)
    {
      Person = await Person.SaveAsync();
    }
  }
}
```

### ViewModel Deriving from ViewModelBase&lt;T&gt;

`Csla.Xaml.ViewModelBase<T>` manages `Model`, busy state, the `Can*` properties and (with `ManageObjectLifetime = true`, the WPF default) n-level undo:

```csharp
using Csla;
using Csla.Xaml;

public class PersonEditViewModel : ViewModelBase<PersonEdit>
{
  private readonly IDataPortal<PersonEdit> _portal;

  public PersonEditViewModel(IDataPortal<PersonEdit> portal)
  {
    _portal = portal;
  }

  // CSLA 10: RefreshAsync<PersonEdit>(...); CSLA 11: RefreshAsync(...)
  public Task LoadAsync(int id) =>
    RefreshAsync<PersonEdit>(async () => await _portal.FetchAsync(id));
}
```

In CSLA 10, `SaveAsync()` and `RefreshAsync()` throw when the operation fails, so wrap them in `try`/`catch`.

> **CSLA 11:** `ViewModelBase<T>` delegates to the common `Csla.Mvvm.ViewModel<T>` (on WPF it is still a `DependencyObject`). `SaveAsync()` and `RefreshAsync()` **no longer throw**: they set `Exception` and `ViewModelErrorText` and raise the `Error` event (a failed `RefreshAsync` sets `Model` to `null`). An invalid `Model` is not saved and sets only `ViewModelErrorText`. Replace `try`/`catch` around them with a check of both properties:
>
> ```csharp
> await vm.SaveAsync();
> if (vm.Exception is null && string.IsNullOrEmpty(vm.ViewModelErrorText))
>   await NavigateBack();
> else
>   ShowError(vm.ViewModelErrorText);
> ```
>
> `RefreshAsync<F>(factory)` is obsolete (CS0618); call `RefreshAsync(factory)`. `SaveAsync()` returns `Task<T?>` and waits up to `BusyTimeout` (30 seconds) for a busy `Model`; use `SaveAsync(CancellationToken)` to control the wait. New events: `Saved`, `ModelChanging`, `ModelChanged`. On WPF, override `OnModelChanging` instead of `OnModelChanged` to run code before event handlers move to the new model. See `v11/ViewModels.md`.

## Multi-Tier Configuration

If your WPF application calls a separate data portal server, you need to configure a client-side data portal proxy.

```csharp
builder.Services.AddCsla(options => options
  .AddXaml()
  .DataPortal(dp => dp
    .AddClientSideDataPortal(client => client
      .UseHttpProxy(proxy => proxy
        .DataPortalUrl = "https://appserver.example.com/api/DataPortal"))));
```

## Best Practices

1. **Use dependency injection** - Always inject `IDataPortal<T>` rather than using static methods.
2. **Use a host builder** - Configure your application using the `IHostBuilder` for a modern, DI-first approach.
3. **Use `PropertyStatus`** - Leverage the `PropertyStatus` control to simplify displaying property-level information to the user.
4. **Use async methods** - Prefer `FetchAsync`, `SaveAsync`, etc. for a responsive user interface.
5. **Handle validation** - Check `IsSavable` before calling `SaveAsync()`. In CSLA 11, `IsSavable` can be `false` while an async per-type authorization rule is running (the object is busy); it raises `PropertyChanged` for `IsSavable` when the rule completes, so bind to it rather than reading it once.
6. **Register DAL services** - Use dependency injection for data access layer components if using the local data portal.

