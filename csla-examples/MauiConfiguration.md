````markdown
# MAUI Configuration

CSLA supports .NET MAUI applications for building cross-platform mobile and desktop apps. This guide covers how to configure CSLA in a MAUI application for both local data portal scenarios and n-tier configurations with a remote server.

## Overview

.NET MAUI (Multi-platform App UI) enables building native apps for Android, iOS, macOS, and Windows from a single codebase. CSLA integrates with MAUI through the `Csla.Maui` NuGet package, which provides XAML support and platform-specific integration.

MAUI applications can use CSLA in two primary configurations:
- **Local data portal**: All business logic and data access runs within the MAUI app
- **Remote data portal**: The MAUI app acts as a client, communicating with a server-side data portal via HTTP

## NuGet Packages

Add the following NuGet package to your MAUI project:

```xml
<PackageReference Include="Csla.Maui" Version="10.0.0" />
```

The `Csla.Maui` package includes the core CSLA library and adds XAML-specific support for MAUI applications.

## Basic Configuration (Local Data Portal)

For a MAUI application using a local data portal where all operations run within the app, configure CSLA in `MauiProgram.cs`:

```csharp
using Csla.Configuration;

namespace MauiExample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Configure CSLA for MAUI with XAML support
        builder.Services.AddCsla(options => options
            .AddXaml());

        // Register your services
        builder.Services.AddTransient<DataAccess.IPersonDal, DataAccess.PersonDal>();

        return builder.Build();
    }
}
```

**Key Configuration Points:**

- `AddCsla()` - Registers all CSLA services with the dependency injection container
- `AddXaml()` - Enables XAML support for data binding and ViewModels in MAUI

## Remote Data Portal Configuration

For n-tier scenarios where the MAUI app communicates with a remote server, configure the HTTP proxy:

```csharp
using Csla.Configuration;

namespace MauiExample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register HttpClient for data portal communication
        builder.Services.AddTransient<HttpClient>();

        // Configure CSLA for MAUI with remote data portal
        builder.Services.AddCsla(options => options
            .AddXaml()
            .DataPortal(dp => dp
                .AddClientSideDataPortal(csp => csp
                    .UseHttpProxy(proxy => proxy
                        .DataPortalUrl = "https://myserver.com/api/DataPortal"))));

        return builder.Build();
    }
}
```

**Key Configuration Points:**

- `AddTransient<HttpClient>()` - Registers HttpClient for network communication
- `UseHttpProxy()` - Configures the HTTP proxy for remote data portal calls
- `DataPortalUrl` - The URL of the server-side data portal endpoint

## ViewModels and XAML Binding

CSLA provides `ViewModelBase<T>` for MAUI applications, which simplifies data binding between your UI and business objects.

### Creating a ViewModel

```csharp
using BusinessLibrary;
using Csla.Xaml;

namespace MauiExample.ViewModels
{
    public class PersonEditViewModel : ViewModelBase<PersonEdit>
    {
    }
}
```

### Using the ViewModel in a Page

```csharp
using BusinessLibrary;
using Csla;
using MauiExample.ViewModels;

namespace MauiExample.Pages;

public partial class PersonEditPage : ContentPage, IQueryAttributable
{
    private readonly PersonEditViewModel _viewModel;
    private readonly IDataPortal<PersonEdit> _portal;

    public PersonEditPage(PersonEditViewModel viewModel, IDataPortal<PersonEdit> portal)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _portal = portal;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("PersonId", out var personId))
        {
            _ = LoadPerson(Convert.ToInt32(personId));
        }
    }

    private async Task LoadPerson(int id)
    {
        try
        {
            var person = await _portal.FetchAsync(id);
            _viewModel.Model = person;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
```

### XAML Binding

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="MauiExample.Pages.PersonEditPage">
    <VerticalStackLayout Padding="20">
        <Label Text="Person Edit" FontSize="24" />
        
        <Entry Text="{Binding Model.Name}" 
               Placeholder="Enter name" />
        
        <Label Text="{Binding Model.BrokenRulesCollection[0].Description}"
               TextColor="Red"
               IsVisible="{Binding Model.IsValid, Converter={StaticResource InverseBooleanConverter}}" />
        
        <Button Text="Save" 
                Command="{Binding SaveCommand}"
                IsEnabled="{Binding Model.IsSavable}" />
    </VerticalStackLayout>
</ContentPage>
```

## Dependency Injection

Register ViewModels and pages with the DI container for proper injection:

```csharp
private static void AddDependencyInjection(MauiAppBuilder builder)
{
    // Register data access layer
    builder.Services.AddTransient<DataAccess.IPersonDal, DataAccess.PersonDal>();

    // Register ViewModels
    builder.Services.AddScoped<PersonEditViewModel>();
    builder.Services.AddScoped<PersonListViewModel>();

    // Register pages (optional, if using DI for page resolution)
    builder.Services.AddTransient<PersonEditPage>();
    builder.Services.AddTransient<PersonListPage>();
}
```

## Shell Navigation

MAUI Shell provides navigation capabilities. Register routes for your pages:

```csharp
using MauiExample.Pages;

namespace MauiExample;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        RegisterRoutes();
    }

    private void RegisterRoutes()
    {
        Routing.RegisterRoute("PersonEdit", typeof(PersonEditPage));
        Routing.RegisterRoute("PersonList", typeof(PersonListPage));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
```

Navigate to pages with parameters:

```csharp
await Shell.Current.GoToAsync($"PersonEdit?PersonId={personId}");
```

## Business Objects

Business objects in MAUI apps follow standard CSLA patterns. Here's an example using source generation:

```csharp
using System.ComponentModel.DataAnnotations;
using Csla;

namespace BusinessLibrary
{
    [Serializable]
    [CslaImplementProperties]
    public partial class PersonEdit : BusinessBase<PersonEdit>
    {
        public partial int Id { get; set; }

        [Required]
        public partial string Name { get; set; }

        protected override void AddBusinessRules()
        {
            base.AddBusinessRules();
            BusinessRules.AddRule(new Csla.Rules.CommonRules.Required(NameProperty));
        }

        [Create, RunLocal]
        private void Create()
        {
            Id = -1;
            BusinessRules.CheckRules();
        }

        [Fetch]
        private void Fetch(int id, [Inject] DataAccess.IPersonDal dal)
        {
            var data = dal.Get(id);
            using (BypassPropertyChecks)
                Csla.Data.DataMapper.Map(data, this);
            BusinessRules.CheckRules();
        }

        [Insert]
        private void Insert([Inject] DataAccess.IPersonDal dal)
        {
            using (BypassPropertyChecks)
            {
                var data = new DataAccess.PersonEntity { Name = Name };
                var result = dal.Insert(data);
                Id = result.Id;
            }
        }

        [Update]
        private void Update([Inject] DataAccess.IPersonDal dal)
        {
            using (BypassPropertyChecks)
            {
                var data = new DataAccess.PersonEntity { Id = Id, Name = Name };
                dal.Update(data);
            }
        }

        [Delete]
        private void Delete(int id, [Inject] DataAccess.IPersonDal dal)
        {
            dal.Delete(id);
        }
    }
}
```

## Read-Only Lists

For displaying lists of data:

```csharp
using Csla;

namespace BusinessLibrary
{
    [Serializable]
    public class PersonList : ReadOnlyListBase<PersonList, PersonInfo>
    {
        [Create, RunLocal]
        private void Create() { }

        [Fetch]
        private void Fetch([Inject] DataAccess.IPersonDal dal, 
                          [Inject] IChildDataPortal<PersonInfo> personPortal)
        {
            IsReadOnly = false;
            var data = dal.Get().Select(d => personPortal.FetchChild(d));
            AddRange(data);
            IsReadOnly = true;
        }
    }
}
```

## Platform-Specific Considerations

### Android

CSLA 9 and later versions fix issues with transferring binary data between client and server in MAUI Android apps. The default configuration now works correctly:

```csharp
builder.Services.AddCsla(options => options
    .AddXaml()
    .DataPortal(dp => dp
        .AddClientSideDataPortal(csp => csp
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "https://server/api/DataPortal"))));
```

### iOS and macOS

Entry points for iOS and macOS platforms use the standard MAUI pattern:

```csharp
// iOS AppDelegate
using Foundation;

namespace MauiExample;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
```

### Windows

Windows apps use the WinUI application delegate:

```csharp
namespace MauiExample.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
```

## Common Scenarios

### Scenario 1: Simple MAUI App with Local Data Access

Complete configuration for a MAUI app with local data portal:

```csharp
using Csla.Configuration;
using MauiExample.ViewModels;

namespace MauiExample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Configure CSLA
        builder.Services.AddCsla(options => options
            .AddXaml());

        // Register services
        builder.Services.AddTransient<DataAccess.IPersonDal, DataAccess.PersonDal>();
        builder.Services.AddScoped<PersonEditViewModel>();
        builder.Services.AddScoped<PersonListViewModel>();

        return builder.Build();
    }
}
```

### Scenario 2: N-Tier MAUI App with Remote Server

Configuration for a MAUI app communicating with a remote data portal:

```csharp
using Csla.Configuration;
using MauiExample.ViewModels;

namespace MauiExample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register HttpClient
        builder.Services.AddTransient<HttpClient>();

        // Configure CSLA with remote data portal
        builder.Services.AddCsla(options => options
            .AddXaml()
            .DataPortal(dp => dp
                .AddClientSideDataPortal(csp => csp
                    .UseHttpProxy(proxy => proxy
                        .DataPortalUrl = "https://api.mycompany.com/api/DataPortal"))));

        // Register ViewModels
        builder.Services.AddScoped<PersonEditViewModel>();
        builder.Services.AddScoped<PersonListViewModel>();

        return builder.Build();
    }
}
```

### Scenario 3: Environment-Based Configuration

Different configuration for debug and release builds:

```csharp
using Csla.Configuration;

namespace MauiExample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

#if DEBUG
        // Use local data portal for debugging
        builder.Services.AddCsla(options => options
            .AddXaml());
        builder.Services.AddTransient<DataAccess.IPersonDal, DataAccess.PersonDal>();
#else
        // Use remote data portal for production
        builder.Services.AddTransient<HttpClient>();
        builder.Services.AddCsla(options => options
            .AddXaml()
            .DataPortal(dp => dp
                .AddClientSideDataPortal(csp => csp
                    .UseHttpProxy(proxy => proxy
                        .DataPortalUrl = "https://api.production.com/api/DataPortal"))));
#endif

        return builder.Build();
    }
}
```

### Scenario 4: Using IDataPortal in Pages

Inject and use `IDataPortal<T>` directly in pages:

```csharp
using BusinessLibrary;
using Csla;

namespace MauiExample.Pages;

public partial class PersonListPage : ContentPage
{
    private readonly IDataPortal<PersonList> _portal;

    public PersonListPage(IDataPortal<PersonList> portal)
    {
        InitializeComponent();
        _portal = portal;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            var list = await _portal.FetchAsync();
            listView.ItemsSource = list;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
```

## Project Structure

A typical MAUI solution using CSLA follows this structure:

```
MySolution/
├── MyApp.Maui/              # MAUI application project
│   ├── MauiProgram.cs       # CSLA configuration
│   ├── App.xaml.cs          # Shell route registration
│   ├── AppShell.xaml        # Navigation structure
│   ├── Pages/               # ContentPages
│   │   ├── MainPage.xaml
│   │   ├── PersonEditPage.xaml
│   │   └── PersonListPage.xaml
│   ├── ViewModels/          # CSLA ViewModels
│   │   ├── PersonEditViewModel.cs
│   │   └── PersonListViewModel.cs
│   └── Platforms/           # Platform-specific code
│       ├── Android/
│       ├── iOS/
│       ├── MacCatalyst/
│       └── Windows/
├── BusinessLibrary/         # CSLA business objects
│   ├── PersonEdit.cs
│   ├── PersonInfo.cs
│   └── PersonList.cs
└── DataAccess/              # Data access layer
    ├── IPersonDal.cs
    ├── PersonDal.cs
    └── PersonEntity.cs
```

## Best Practices

1. **Use ViewModelBase<T>** - Leverage CSLA's `ViewModelBase<T>` for simplified XAML binding
2. **Register services properly** - Use `AddScoped` for ViewModels and `AddTransient` for data access
3. **Handle exceptions** - Wrap data portal calls in try-catch blocks and display user-friendly messages
4. **Use async/await** - Always use async data portal methods (`FetchAsync`, `SaveAsync`, etc.)
5. **Separate concerns** - Keep business logic in the business library, not in the MAUI project
6. **Test on all platforms** - Verify your app works correctly on all target platforms
7. **Configure timeouts** - For remote data portal, consider setting appropriate timeouts for network operations
8. **Use HTTPS** - Always use HTTPS for remote data portal URLs in production

## Troubleshooting

### Data Portal Calls Failing

If data portal calls fail:
- Verify the data portal URL is correct and accessible
- Check that the server has the data portal controller configured
- Ensure proper network permissions are set in platform-specific configurations
- Check for certificate issues when using HTTPS

### XAML Binding Not Working

If data binding isn't working:
- Verify `AddXaml()` is called in CSLA configuration
- Ensure BindingContext is set to the ViewModel
- Check that property names match between XAML and business objects

### Dependency Injection Issues

If services aren't being resolved:
- Verify all services are registered in `MauiProgram.cs`
- Check service lifetimes (Scoped, Transient, Singleton)
- Ensure constructor parameters match registered services

### Platform-Specific Issues

For platform-specific problems:
- Check platform-specific code in the Platforms folder
- Verify required permissions are configured in platform manifests
- Review platform-specific debugging logs

## Notes

- MAUI apps can run data portal operations locally or communicate with a remote server
- The `Csla.Maui` package provides platform-specific integration for all MAUI targets
- ViewModels should be registered as `Scoped` to maintain state during page lifetime
- Business objects and data access can be shared between MAUI and server projects
- CSLA 9+ includes fixes for binary data transfer issues on Android

````
