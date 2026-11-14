# View Models (Csla.Mvvm.ViewModel<T>)

This document applies to CSLA 11. All CSLA view models share one implementation, `Csla.Mvvm.ViewModel<T>` in the core `Csla` package. This document describes that base type, how the UI-specific view models build on it, and the behavior code must account for (errors, saving, object lifetime).

## Summary for code generation

* **Type to use per UI:**

  | UI | View model type | Package |
  |---|---|---|
  | Blazor | `Csla.Blazor.ViewModel<T>` (adds `GetPropertyInfo`) | `Csla.Blazor` |
  | WPF | `Csla.Xaml.ViewModelBase<T>` / `ViewModel<T>` (`DependencyObject`, delegates to `Csla.Mvvm.ViewModel<T>`) | `Csla.Wpf` |
  | MAUI, Avalonia | `Csla.Xaml.ViewModelBase<T>` / `ViewModel<T>` (inherits `Csla.Mvvm.ViewModel<T>`) | `Csla.Maui`, `Csla.Avalonia` |
  | Windows Forms, console, other | `Csla.Mvvm.ViewModel<T>` directly | `Csla` |

* `SaveAsync()` and `RefreshAsync(...)` **don't throw**. Check `Exception` and `ViewModelErrorText` after the call, or handle the `Error` event. Don't wrap them in `try`/`catch` to detect failure.
* `SaveAsync()` returns `Task<T?>`: the saved `Model`.
* An invalid `Model` isn't saved: `SaveAsync` sets `ViewModelErrorText` to the model's error text **without** setting `Exception` or raising `Error`. Check both properties.
* `RefreshAsync` takes a `Func<Task<T?>>`: `await vm.RefreshAsync(async () => await portal.FetchAsync(id));`. If it fails, `Model` is set to `null`.
* `ManageObjectLifetime` (n-level undo) defaults to `true` on WPF, MAUI and Avalonia, and `false` on Blazor and `Csla.Mvvm.ViewModel<T>` created with the public constructor.
* Override `OnModelChanged(T? oldValue, T? newValue)` (Blazor's parameterless `OnModelChanged()` is gone).

## Members (all view models)

| Member | Description |
|---|---|
| `Model` | The business object. Setting it moves event handlers to the new object (`OnModelChanging`), then calls `OnModelChanged(old, new)` and raises `ModelChanged` |
| `ManageObjectLifetime` | When `true`, the view model calls `BeginEdit` on the model, and `SaveAsync` saves a clone with edits committed |
| `BusyTimeout` | How long `SaveAsync` waits for a busy model to become idle (default 30 seconds) |
| `IsBusy`, `IsDirty`, `IsValid` | Model status |
| `CanSave`, `CanCancel`, `CanCreate`, `CanDelete`, `CanFetch`, `CanAddNew`, `CanRemove` | Instance status, updated whenever the model changes (instance-level authorization rules run on each change) |
| `CanCreateObject`, `CanGetObject`, `CanEditObject`, `CanDeleteObject` | Per-type permissions. With async authorization rules they start `false` and update when the rules complete |
| `Exception`, `ViewModelErrorText` | The last error captured by `SaveAsync`, `RefreshAsync` or a status calculation |
| `RefreshAsync(Func<Task<T?>>)` | Loads the model through the factory |
| `SaveAsync()`, `SaveAsync(CancellationToken)` | Saves the model; returns the saved model |
| `DoSaveAsync`, `DoCancel`, `DoAddNew`, `DoRemove`, `DoDelete` | Protected virtual methods to override or call from a derived view model |
| Events | `Saved`, `Error`, `ModelChanging`, `ModelChanged`, `ModelPropertyChanged`, `ModelChildChanged`, `ModelCollectionChanged`, `PropertyChanged` |

## Saving and error handling

```csharp
// Wrong in CSLA 11: SaveAsync never throws, so failures go unnoticed and the page navigates away
try
{
    await vm.SaveAsync();
    await NavigateBack();
}
catch (Exception ex)
{
    ShowError(ex.Message);
}

// Right
await vm.SaveAsync();
if (vm.Exception is null && string.IsNullOrEmpty(vm.ViewModelErrorText))
    await NavigateBack();
else
    ShowError(vm.ViewModelErrorText);
```

Other `SaveAsync` behavior:

* Waits up to `BusyTimeout` for a busy model (for example async rules still running); after that it captures a `TimeoutException`. Pass a `CancellationToken` to control the wait yourself.
* Does nothing if `Model` is `null` (no `Saved` event). If the model doesn't implement `ISavable`, `DoSaveAsync` throws `InvalidOperationException`, which `SaveAsync` captures.
* With `ManageObjectLifetime = true`, saves a clone with edits committed and calls `ApplyEdit` on `Model` only after the save succeeds. If the save fails, `DoCancel` still restores the values from before the edit.
* Raises `Saved` after a successful save.

An authorization rule that throws while the status properties (`CanSave`, `CanEditObject`, ...) are calculated is captured the same way (`Exception`, `ViewModelErrorText`, `Error`), and the status properties keep their previous values.

## Blazor

```razor
@page "/editperson/{id?}"
@inject Csla.IDataPortal<PersonEdit> personEditPortal
@inject Csla.Blazor.ViewModel<PersonEdit> vm
@inject NavigationManager NavigationManager

<p class="text-danger">@vm.ViewModelErrorText</p>

@if (vm.Model is not null)
{
    <TextInput Property="@(vm.GetPropertyInfo(() => vm.Model.Name))" />
    <button @onclick="vm.SaveAsync" disabled="@(!vm.CanSave)">Save</button>
}

@code {
    [Parameter] public string? Id { get; set; }

    protected override void OnInitialized()
    {
        // Subscribe once; vm is scoped to the component
        vm.Saved += () => NavigationManager.NavigateTo("listpersons");
        vm.ModelPropertyChanged += async (s, e) => await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Id))
            await vm.RefreshAsync(async () => await personEditPortal.CreateAsync());
        else
            await vm.RefreshAsync(async () => await personEditPortal.FetchAsync(int.Parse(Id)));
    }
}
```

* `Csla.Blazor.ViewModel<T>` implements `INotifyPropertyChanged` and has the XAML status members (`IsDirty`, `IsValid`, `CanSave`, `CanCancel`, ...).
* `RefreshAsync` waits for the per-type permissions, and the `Can*Object` getters don't throw for async authorization rules.
* `ManageObjectLifetime` defaults to `false`.

Derived Blazor view model:

```csharp
public class PersonEditViewModel : Csla.Blazor.ViewModel<PersonEdit>
{
    public PersonEditViewModel(ApplicationContext applicationContext)
        : base(applicationContext)
    { }

    // CSLA 10 had OnModelChanged(); CSLA 11 passes both values
    protected override void OnModelChanged(PersonEdit? oldValue, PersonEdit? newValue)
    {
        base.OnModelChanged(oldValue, newValue);
        // react to the new model
    }

    public void Cancel() => DoCancel();
}
```

## XAML (WPF, MAUI, Avalonia)

```csharp
public class PersonEditViewModel : Csla.Xaml.ViewModelBase<PersonEdit>
{
}
```

```csharp
public partial class PersonEditPage : ContentPage
{
    private readonly IDataPortal<PersonEdit> _portal;

    public PersonEditPage(PersonEditViewModel viewModel, IDataPortal<PersonEdit> portal)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _portal = portal;
    }

    private async Task LoadAsync(int id)
    {
        var vm = (PersonEditViewModel)BindingContext;
        await vm.RefreshAsync(async () => await _portal.FetchAsync(id));
    }

    private async void SavePerson(object sender, EventArgs e)
    {
        var vm = (PersonEditViewModel)BindingContext;
        await vm.SaveAsync();
        if (vm.Exception is null && string.IsNullOrEmpty(vm.ViewModelErrorText))
            await Shell.Current.GoToAsync("..");
        else
            await DisplayAlertAsync("Error", vm.ViewModelErrorText, "OK");
    }
}
```

XAML-specific rules:

* `ManageObjectLifetime` defaults to `true` on every XAML platform. Set it to `false` to turn off n-level undo.
* On MAUI and Avalonia the public `ManageObjectLifetimeProperty` field is removed; use the `ManageObjectLifetime` property. WPF keeps its `ModelProperty` and `ManageObjectLifetimeProperty` dependency properties.
* `RefreshAsync<F>(factory)` is obsolete; call `RefreshAsync(factory)`.
* On MAUI, `BeginAddNew` is obsolete; use `DoAddNew`.
* On WPF, override `OnModelChanging` instead of `OnModelChanged` to run code before event handlers move to the new model.
* The permission properties (`CanCreate`, `CanDelete`, `CanAddNew`, `CanRemove`, `CanCreateObject`, ...) use `HasPermissionAsync`; with async rules they start `false` and raise `PropertyChanged` when the rules complete.

## Windows Forms and other UI frameworks

Use `Csla.Mvvm.ViewModel<T>` directly. Pass the `ApplicationContext`, or derive and override `ResolveApplicationContext()`.

```csharp
public class CustomerEditViewModel : Csla.Mvvm.ViewModel<CustomerEdit>
{
    public CustomerEditViewModel(ApplicationContext applicationContext)
        : base(applicationContext, manageObjectLifetime: true)
    { }

    public void Cancel() => DoCancel();
}
```

```csharp
public partial class CustomerEditForm : Form
{
    private readonly CustomerEditViewModel _vm;
    private readonly IDataPortal<CustomerEdit> _portal;
    private readonly int _customerId;

    public CustomerEditForm(ApplicationContext applicationContext, IDataPortal<CustomerEdit> portal, int customerId)
    {
        InitializeComponent();
        _portal = portal;
        _customerId = customerId;
        _vm = new CustomerEditViewModel(applicationContext);
        _vm.Error += (s, e) => MessageBox.Show(_vm.ViewModelErrorText);
        _vm.PropertyChanged += (s, e) => saveButton.Enabled = _vm.CanSave;
    }

    private async void CustomerEditForm_Load(object sender, EventArgs e)
    {
        await _vm.RefreshAsync(async () => await _portal.FetchAsync(_customerId));
        customerBindingSource.DataSource = _vm.Model;
    }

    private async void saveButton_Click(object sender, EventArgs e)
    {
        customerBindingSource.EndEdit();
        await _vm.SaveAsync();
        // On success the view model's Model is the saved object; rebind to it
        if (_vm.Exception is null && string.IsNullOrEmpty(_vm.ViewModelErrorText))
            customerBindingSource.DataSource = _vm.Model;
    }
}
```

* The public constructor `ViewModel(ApplicationContext)` sets `ManageObjectLifetime = false`; the protected `ViewModel(ApplicationContext, bool manageObjectLifetime)` sets it explicitly.
* The protected parameterless constructor resolves the context on first use through `ResolveApplicationContext()`, which throws `InvalidOperationException` unless you override it.

## Related

* `BlazorDataBinding.md` - Blazor `GetPropertyInfo` and metastate binding
* `WpfConfiguration.md`, `MauiConfiguration.md`, `AvaloniaConfiguration.md`, `WinFormsConfiguration.md` - UI setup
* `BusinessRules.md` - async authorization rules
* `UpgradingToCsla11.md` - all CSLA 10 to 11 changes
