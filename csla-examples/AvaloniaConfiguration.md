# Avalonia Configuration

CSLA supports [Avalonia](https://avaloniaui.net) desktop applications through the `Csla.Avalonia` NuGet package. It brings CSLA's XAML helpers to Avalonia: the `PropertyInfo` control for displaying broken rules and authorization state, and `ViewModelBase<T>` for MVVM view models. It uses dependency injection the same way modern WPF and MAUI apps do.

> **Applies to CSLA 10.2 and later.** `Csla.Avalonia` first shipped with CSLA 10.2.0. It isn't available in CSLA 9, 10.0, or 10.1.

## Overview

**Key characteristics:**

- All code runs on the client. The local data portal is used by default.
- CSLA is configured in `App.axaml.cs` (`OnFrameworkInitializationCompleted`) with a `ServiceCollection`
- `AddXaml()` configures CSLA for a XAML client
- You must resolve `ApplicationContext` once after building the service provider, so the XAML helpers can find it
- Windows, views, and view models are registered for DI

## Package and Namespaces

```xml
<PackageReference Include="Avalonia" Version="12.1.1" />
<PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
<PackageReference Include="Csla.Avalonia" Version="10.2.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

| Item | Value |
| --- | --- |
| NuGet package | `Csla.Avalonia` (same version as `Csla`) |
| Target framework | `net10.0` |
| Avalonia dependency | Avalonia 12.1.1 or later |
| Assembly name | `Csla.Xaml` (the same as the WPF package's assembly) |
| C# namespace | `Csla.Xaml` (helpers), `Csla.Configuration` (`AddXaml`) |
| XAML namespace | `xmlns:csla="clr-namespace:Csla.Xaml;assembly=Csla.Xaml"` |

Your business library projects reference only `Csla`. Only the Avalonia UI project references `Csla.Avalonia`.

## What Csla.Avalonia Includes

`Csla.Avalonia` is built from the same shared XAML source as `Csla.Wpf` and `Csla.Maui`, but only part of it compiles for Avalonia:

| Available in Csla.Avalonia | Not available in Csla.Avalonia (WPF only) |
| --- | --- |
| `PropertyInfo` control | `PropertyStatus` |
| `ViewModelBase<T>` | `ViewModel<T>` |
| `ApplicationContextManager` | `ObjectStatus` |
| `AddXaml()` configuration | `CslaDataProvider` |
| `IViewModel`, `IErrorDialog`, `ErrorEventArgs` | `ErrorDialog`, `TriggerAction`, `InvokeMethod` |
| | `BusyAnimation`, `IdentityConverter`, `IsNotConverter`, `BrokenRulesSeverityConverter` |
| | `UseCsla()` host extension |

Use `PropertyInfo` for property-level rule and authorization state, and derive view models from `ViewModelBase<T>`.

## Application Startup

Configure CSLA in `App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyAvaloniaApp;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // CSLA configured for a XAML client
        services.AddCsla(options => options.AddXaml());

        // data access (local data portal)
        services.AddSingleton<IPersonDal, PersonDal>();

        // view models, views, and windows
        services.AddTransient<PersonEditViewModel>();
        services.AddTransient<PersonEditView>();
        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();

        // IMPORTANT: create the ApplicationContext once so
        // Csla.Xaml.ApplicationContextManager captures it for
        // ViewModelBase<T> and the other XAML helpers
        _ = Services.GetRequiredService<Csla.ApplicationContext>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }
}
```

`Program.cs` is the standard Avalonia entry point:

```csharp
using Avalonia;

namespace MyAvaloniaApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

### Key Configuration Points

#### AddXaml()

```csharp
services.AddCsla(options => options.AddXaml());
```

`AddXaml()` does two things:

- Registers `Csla.Xaml.ApplicationContextManager` as the `IContextManager`. It's a static, app-wide context, which is right for a single-user desktop app.
- Sets `PropertyChangedMode` to `Xaml`, so business objects raise `PropertyChanged` the way XAML data binding expects.

#### Resolve ApplicationContext After Building the Provider

Avalonia has no `IHost.UseCsla()` extension like WPF does. Resolve `ApplicationContext` yourself right after `BuildServiceProvider()`. If you don't, `ViewModelBase<T>` throws `InvalidOperationException` (`applicationContext == null`) the first time it needs the context.

#### Compiled Bindings

`PropertyInfo` and view models are commonly bound with reflection-based bindings (`{Binding Model}` and so on). The CSLA Avalonia sample turns off compiled bindings by default in the app project:

```xml
<PropertyGroup>
  <AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault>
</PropertyGroup>
```

If you keep compiled bindings on, each view needs an `x:DataType`. Bindings through `ViewModelBase<T>.Model` are then typed to `T`.

## ViewModelBase&lt;T&gt;

Derive view models from `Csla.Xaml.ViewModelBase<T>`, where `T` is the business type:

```csharp
using Csla.Xaml;

public class PersonEditViewModel : ViewModelBase<PersonEdit>
{
}
```

Commonly used members:

| Member | Description |
| --- | --- |
| `Model` | The business object (raises `PropertyChanged`) |
| `RefreshAsync<F>(Func<Task<T?>> factory)` | Sets `Model` to the result of the factory, managing `IsBusy` |
| `SaveAsync()` | Saves `Model` and updates it with the result |
| `IsBusy`, `IsDirty`, `IsValid` | Model state |
| `CanSave`, `CanCancel`, `CanCreate`, `CanDelete`, `CanFetch`, `CanAddNew`, `CanRemove` | Computed from model state and authorization rules |
| `CanCreateObject`, `CanGetObject`, `CanEditObject`, `CanDeleteObject` | Per-type authorization |
| `ManageObjectLifetime` | When `true`, the view model clones before saving and manages n-level undo |

### Loading and Saving From a View

Inject the view model and `IDataPortal<T>` into the view:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Csla;

namespace MyAvaloniaApp.Views;

public partial class PersonEditView : UserControl
{
    private readonly IDataPortal<PersonEdit> _portal;
    private bool _isLoaded;

    public PersonEditView(PersonEditViewModel viewModel, IDataPortal<PersonEdit> portal)
    {
        InitializeComponent();
        DataContext = viewModel;
        _portal = portal;

        AttachedToVisualTree += async (_, _) =>
        {
            if (_isLoaded)
                return;
            _isLoaded = true;
            await LoadNewAsync();
        };
    }

    private PersonEditViewModel ViewModel => (PersonEditViewModel)DataContext!;

    private async Task LoadNewAsync()
    {
        await ViewModel.RefreshAsync<PersonEdit>(
            async () => await _portal.CreateAsync());
    }

    private async void SavePerson(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Model?.IsSavable == true)
            await ViewModel.SaveAsync();
    }

    private async void NewPerson(object? sender, RoutedEventArgs e)
    {
        await LoadNewAsync();
    }
}
```

## PropertyInfo Control

`Csla.Xaml.PropertyInfo` exposes the rule and authorization state of one business object property, so other controls can bind to it. In Avalonia it's a `UserControl` with no visible content. You don't bind its source with a `Property="{Binding ...}"` binding as in WPF. Instead:

- Set **`Path`** to the property name (a dotted path such as `Model.Name` also works), and
- `PropertyInfo` reads the source object from its own **`DataContext`**.

Bind other controls to it by name with Avalonia's `#elementName` syntax:

```xml
<UserControl
    x:Class="MyAvaloniaApp.Views.PersonEditView"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:csla="clr-namespace:Csla.Xaml;assembly=Csla.Xaml">

  <!-- View DataContext is PersonEditViewModel; switch to the business object -->
  <StackPanel DataContext="{Binding Model}" Margin="24" Spacing="8">
    <TextBlock Text="Name" FontWeight="SemiBold" />

    <TextBox Text="{Binding Name, Mode=TwoWay}"
             IsEnabled="{Binding #NameInfo.CanWrite}" />

    <csla:PropertyInfo x:Name="NameInfo" Path="Name" />

    <TextBlock Text="{Binding #NameInfo.ErrorText}" Foreground="Red" />
    <TextBlock Text="{Binding #NameInfo.WarningText}" />
    <TextBlock Text="{Binding #NameInfo.InformationText}" />

    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Save" IsEnabled="{Binding IsSavable}" Click="SavePerson" />
      <Button Content="New" Click="NewPerson" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

`PropertyInfo` members you can bind to:

| Property | Description |
| --- | --- |
| `IsValid` | `true` if the property has no broken rules of any severity (error, warning, or information) |
| `BrokenRules` | `ObservableCollection<BrokenRule>` for the property |
| `ErrorText`, `WarningText`, `InformationText` | Rule messages by severity |
| `RuleSeverity`, `RuleDescription` | Most severe broken rule and its description |
| `CanRead`, `CanWrite` | Property-level authorization results |
| `IsBusy` | `true` while an async rule runs for the property |
| `Value` | The current property value |
| `FriendlyName` | The property's friendly name |

`PropertyInfo` listens for `PropertyChanged` and rule changes on the business object, so the bound values update as the user edits.

## Read-Only Lists

```csharp
public class PersonListViewModel : ViewModelBase<PersonList>
{
}
```

```xml
<ListBox ItemsSource="{Binding Model}">
  <ListBox.ItemTemplate>
    <DataTemplate>
      <Grid ColumnDefinitions="80,*" Margin="4">
        <TextBlock Grid.Column="0" Text="{Binding Id}" />
        <TextBlock Grid.Column="1" Text="{Binding Name}" />
      </Grid>
    </DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

```csharp
await ViewModel.RefreshAsync<PersonList>(async () => await _listPortal.FetchAsync());
```

## Multi-Tier Configuration

To call a remote data portal server, configure a client-side proxy, as in any other client app:

```csharp
services.AddCsla(options => options
    .AddXaml()
    .DataPortal(dp => dp
        .AddClientSideDataPortal(client => client
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "https://appserver.example.com/api/DataPortal"))));
```

Any CSLA channel works (HTTP, gRPC, RabbitMQ, WCF). See [HttpDataPortalConfiguration.md](HttpDataPortalConfiguration.md).

## Testing With Avalonia.Headless

The CSLA Avalonia sample tests `PropertyInfo` with `Avalonia.Headless.XUnit`, which runs without a display:

```csharp
using Avalonia.Headless.XUnit;
using Csla;
using Csla.Configuration;
using Csla.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class PropertyInfoTests
{
    [AvaloniaFact]
    public async Task BrokenRules_follow_Name_required_rule()
    {
        var services = new ServiceCollection();
        services.AddCsla();
        services.AddSingleton<IPersonDal, PersonDal>();
        using var provider = services.BuildServiceProvider();

        var person = await provider.GetRequiredService<IDataPortal<PersonEdit>>().CreateAsync();

        var propertyInfo = new PropertyInfo
        {
            Path = nameof(PersonEdit.Name),
            DataContext = person
        };

        Assert.False(propertyInfo.IsValid);

        person.Name = "Ada Lovelace";

        Assert.True(propertyInfo.IsValid);
        Assert.Empty(propertyInfo.BrokenRules);
    }
}
```

The test assembly also needs an `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` that builds an app with `.UseHeadless(new AvaloniaHeadlessPlatformOptions())`.

## Best Practices

1. **Call `AddXaml()`** when you configure CSLA in an Avalonia app.
2. **Resolve `ApplicationContext` once** right after `BuildServiceProvider()`, before you show any window.
3. **Use dependency injection.** Register windows, views, and view models, and inject `IDataPortal<T>` instead of using static data portal methods.
4. **Derive view models from `ViewModelBase<T>`.** Use `RefreshAsync` and `SaveAsync` so `IsBusy` and the `Can*` properties stay correct.
5. **Use `PropertyInfo` with `Path`** to show validation and authorization state. `PropertyStatus` doesn't exist in Avalonia.
6. **Check `IsSavable`** before saving.
7. **Use async data portal methods** to keep the UI responsive.

## Related Documentation

- [WpfConfiguration.md](WpfConfiguration.md) - WPF configuration, which shares much of the XAML helper code
- [MauiConfiguration.md](MauiConfiguration.md) - MAUI configuration
- [HttpDataPortalConfiguration.md](HttpDataPortalConfiguration.md) - Remote data portal configuration
