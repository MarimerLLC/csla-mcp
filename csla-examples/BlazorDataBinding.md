# Blazor Data Binding with CSLA Business Objects

This guide explains how to create effective and efficient Blazor pages that are data bound to editable CSLA business objects. It covers the UI helper components provided by CSLA, proper patterns for data binding, validation display, authorization checks, and state management.

## Overview

CSLA provides a rich set of Blazor components and patterns for building data entry forms:

1. **ViewModel<T>** - Manages the lifecycle of a business object and provides the `GetPropertyInfo()` method
2. **GetPropertyInfo() / Csla.Blazor.IPropertyInfo** - Exposes property **metastate** (validation, authorization, busy state) for smart data binding
3. **CslaValidator** - Integrates CSLA validation with Blazor's EditForm
4. **CslaValidationMessages** - Displays validation messages per property
5. **StateManager** - Manages state across render modes

## Core Pattern: ViewModel-Based Data Binding

The `ViewModel<T>` class is the foundation for Blazor pages that edit CSLA business objects.

### Injecting Required Services

```csharp
@inject Csla.Blazor.State.StateManager StateManager
@inject Csla.IDataPortal<PersonEdit> personEditPortal
@inject Csla.Blazor.ViewModel<PersonEdit> vm
@inject NavigationManager NavigationManager
```

**Required Services:**
- `StateManager` - Must be initialized on every interactive page
- `IDataPortal<T>` - Creates/fetches business objects
- `ViewModel<T>` - Manages the business object lifecycle
- `NavigationManager` - For post-save navigation

### Page Initialization Pattern

```csharp
@code {
    [Parameter]
    public string? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // CRITICAL: Always call StateManager.InitializeAsync() first
        await StateManager.InitializeAsync();

        // Wire up event handlers
        vm.Saved += () => NavigationManager.NavigateTo("list");
        vm.ModelPropertyChanged += async (s, e) =>
            await InvokeAsync(() => StateHasChanged());

        // Load or create the business object
        if (string.IsNullOrWhiteSpace(Id))
            await vm.RefreshAsync(() => personEditPortal.CreateAsync());
        else
            await vm.RefreshAsync(() => personEditPortal.FetchAsync(int.Parse(Id)));
    }
}
```

**Critical Points:**
1. Always call `StateManager.InitializeAsync()` first - this is required for state synchronization in multi-render-mode apps
2. Wire up `ModelPropertyChanged` to trigger UI refresh when properties change
3. Wire up `Saved` event for post-save navigation
4. Use `RefreshAsync()` to load the business object - it handles errors and busy state

## ViewModel<T> Reference

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Model` | `T` | The CSLA business object being edited |
| `IsBusy` | `bool` | True during async operations |
| `ViewModelErrorText` | `string` | Error messages from save/refresh operations |
| `ManageObjectLifetime` | `bool` | Controls n-level undo (default: false) |

### Authorization Properties

| Property | Type | Description |
|----------|------|-------------|
| `CanCreateObject` | `bool` | User can create new instances |
| `CanGetObject` | `bool` | User can retrieve instances |
| `CanEditObject` | `bool` | User can edit/save instances |
| `CanDeleteObject` | `bool` | User can delete instances |

### Key Methods

| Method | Description |
|--------|-------------|
| `RefreshAsync(Func<Task<T>>)` | Loads/creates a model asynchronously |
| `SaveAsync()` | Saves the model asynchronously |
| `GetPropertyInfo(Expression<Func<P>>)` | Returns `Csla.Blazor.IPropertyInfo` - property metastate for UI binding |
| `DoCancel()` | Cancels edits if ManageObjectLifetime is true |

### Events

| Event | Description |
|-------|-------------|
| `Saved` | Raised after successful save |
| `Error` | Raised when save/refresh fails |
| `ModelPropertyChanged` | Property changed on model |
| `ModelChanging` | Before model is replaced |
| `ModelChanged` | After model is replaced |

## Property Metastate with GetPropertyInfo

The `ViewModel<T>.GetPropertyInfo()` method is central to CSLA Blazor data binding. It returns a `Csla.Blazor.IPropertyInfo` object that exposes the **metastate** for a property - runtime information about validation status, authorization, busy state, and the property value itself.

> **Important:** `Csla.Blazor.IPropertyInfo` is different from `Csla.Core.IPropertyInfo`. The Core type is the property registration metadata used with `RegisterProperty<T>()`. The Blazor type is a runtime wrapper that provides UI-focused metastate for data binding.

### How GetPropertyInfo Works

The `GetPropertyInfo()` method:
1. Takes a lambda expression identifying the property (e.g., `() => vm.Model.Name`)
2. Creates a `Csla.Blazor.PropertyInfo` wrapper around that property
3. Caches the wrapper for performance (cache is cleared when the Model changes)
4. Returns `Csla.Blazor.IPropertyInfo` which provides access to the property's runtime metastate

### GetPropertyInfo Overloads

| Method Signature | Description |
|------------------|-------------|
| `GetPropertyInfo<P>(Expression<Func<P>> property)` | Get metastate using a lambda expression |
| `GetPropertyInfo<P>(string textSeparator, Expression<Func<P>> property)` | Get metastate with custom text separator for validation messages |
| `GetPropertyInfo<P>(Expression<Func<P>> property, string id)` | Get metastate for items in a list/array with unique ID |
| `GetPropertyInfo(string propertyName)` | Get metastate by property name |
| `GetPropertyInfo(string propertyName, string id)` | Get metastate by property name for list/array items |

### Csla.Blazor.IPropertyInfo Properties (Metastate)

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `object` | Get/set the property value on the business object |
| `FriendlyName` | `string` | Display name from property registration (via `Csla.Core.IPropertyInfo`) |
| `PropertyName` | `string` | Technical property name |
| `ErrorText` | `string` | Validation error messages (RuleSeverity.Error), concatenated |
| `WarningText` | `string` | Validation warning messages (RuleSeverity.Warning), concatenated |
| `InformationText` | `string` | Validation info messages (RuleSeverity.Information), concatenated |
| `CanRead` | `bool` | Whether the current user is authorized to read this property |
| `CanWrite` | `bool` | Whether the current user is authorized to write this property |
| `IsBusy` | `bool` | Whether the property is currently executing async business rules |
| `TextSeparator` | `string` | Separator used when concatenating multiple validation messages (default: " ") |

### Csla.Blazor.IPropertyInfo Methods

| Method | Description |
|--------|-------------|
| `Refresh()` | Triggers PropertyChanged for all properties to force UI refresh |
| `GetPropertyInfo()` | Returns the `System.Reflection.PropertyInfo` for the property |

### Usage Pattern

```razor
@if (vm.GetPropertyInfo(() => vm.Model.Name).CanRead)
{
    <tr>
        <td>@(vm.GetPropertyInfo(() => vm.Model.Name).FriendlyName)</td>
        <td>
            <TextInput Property="@(vm.GetPropertyInfo(() => vm.Model.Name))" />
        </td>
    </tr>
}
```

### How Metastate Properties Work

The `Csla.Blazor.PropertyInfo` class retrieves metastate from the underlying CSLA business object:

- **`Value`**: Uses `Csla.Utilities.CallByName` to get/set the property value via reflection
- **`FriendlyName`**: Retrieves from `Csla.Core.FieldManager.PropertyInfoManager.GetRegisteredProperty()` - this is where it accesses `Csla.Core.IPropertyInfo`
- **`ErrorText`/`WarningText`/`InformationText`**: Calls `BrokenRulesCollection.ToString()` filtered by severity and property name
- **`CanRead`/`CanWrite`**: Calls `IAuthorizeReadWrite.CanReadProperty()`/`CanWriteProperty()` on the business object
- **`IsBusy`**: Calls `BusinessBase.IsPropertyBusy()` to check if async rules are running

## Two Approaches to Data Binding

### Approach 1: Direct Property Binding (Recommended for Complex Forms)

This approach uses custom input components that take `Csla.Blazor.IPropertyInfo` as a parameter.

```razor
@page "/editperson/{id?}"
@rendermode InteractiveAuto

@inject Csla.Blazor.State.StateManager StateManager
@inject Csla.IDataPortal<PersonEdit> personEditPortal
@inject Csla.Blazor.ViewModel<PersonEdit> vm
@inject NavigationManager NavigationManager

<h1>Edit Person</h1>
<p class="alert-danger">@vm.ViewModelErrorText</p>

@if (vm.Model == null)
{
    <p>Loading...</p>
}
else
{
    <table class="table">
        <tbody>
            <tr>
                <td>Id</td>
                <td>@vm.Model.Id</td>
            </tr>
            <TextInputRow Property="@(vm.GetPropertyInfo(() => vm.Model.Name))" />
            <TextInputRow Property="@(vm.GetPropertyInfo(() => vm.Model.Email))" />
        </tbody>
    </table>

    @if (vm.CanEditObject)
    {
        <button @onclick="vm.SaveAsync" disabled="@(!vm.Model.IsSavable)">Save</button>
        <button @onclick="Cancel">Cancel</button>
    }
}

@code {
    [Parameter]
    public string? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await StateManager.InitializeAsync();

        vm.Saved += () => NavigationManager.NavigateTo("listpersons");
        vm.ModelPropertyChanged += async (s, e) =>
            await InvokeAsync(() => StateHasChanged());

        if (string.IsNullOrWhiteSpace(Id))
            await vm.RefreshAsync(() => personEditPortal.CreateAsync());
        else
            await vm.RefreshAsync(() => personEditPortal.FetchAsync(int.Parse(Id)));
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("listpersons");
    }
}
```

### Approach 2: EditForm with CslaValidator

This approach uses Blazor's standard `EditForm` with CSLA validation integration.

```razor
@page "/editpersonform/{id?}"
@rendermode InteractiveAuto

@inject Csla.Blazor.State.StateManager StateManager
@inject Csla.IDataPortal<PersonEdit> personEditPortal
@inject Csla.Blazor.ViewModel<PersonEdit> vm
@inject NavigationManager NavigationManager

<h1>Edit Person</h1>
<p class="alert-danger">@vm.ViewModelErrorText</p>

@if (vm.Model == null)
{
    <p>Loading...</p>
}
else
{
    <EditForm Model="@vm.Model" OnSubmit="vm.SaveAsync">
        <CslaValidator />
        <ValidationSummary />

        <div class="mb-3">
            <label class="form-label">Name</label>
            <InputText @bind-Value="vm.Model.Name" class="form-control" />
            <CslaValidationMessages For="() => vm.Model.Name" />
        </div>

        <div class="mb-3">
            <label class="form-label">Email</label>
            <InputText @bind-Value="vm.Model.Email" class="form-control" />
            <CslaValidationMessages For="() => vm.Model.Email" />
        </div>

        <button type="submit" class="btn btn-primary"
                disabled="@(!vm.Model.IsSavable)">Save</button>
        <button type="button" class="btn btn-secondary"
                @onclick="Cancel">Cancel</button>
    </EditForm>
}

@code {
    [Parameter]
    public string? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await StateManager.InitializeAsync();

        vm.Saved += () => NavigationManager.NavigateTo("listpersons");
        vm.ModelPropertyChanged += async (s, e) =>
            await InvokeAsync(() => StateHasChanged());

        if (string.IsNullOrWhiteSpace(Id))
            await vm.RefreshAsync(() => personEditPortal.CreateAsync());
        else
            await vm.RefreshAsync(() => personEditPortal.FetchAsync(int.Parse(Id)));
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("listpersons");
    }
}
```

## Reusable Input Components

Create reusable input components that accept `Csla.Blazor.IPropertyInfo` (property metastate) as a parameter for consistent behavior across your application. These components can leverage the metastate to:

- Get/set the property value via `Property.Value`
- Display the friendly name via `Property.FriendlyName`
- Show validation messages via `Property.ErrorText`, `Property.WarningText`, `Property.InformationText`
- Control read/write access via `Property.CanRead` and `Property.CanWrite`
- Show busy indicators via `Property.IsBusy`

### TextInput Component

```razor
@* TextInput.razor *@
<div>
    <input @bind-value="TextValue"
           @bind-value:event="oninput"
           disabled="@(!Property.CanWrite)"
           class="form-control" />
    <span class="text-danger">@Property.ErrorText</span>
    <span class="text-warning">@Property.WarningText</span>
    <span class="text-info">@Property.InformationText</span>
</div>

@code {
    [Parameter]
    public Csla.Blazor.IPropertyInfo Property { get; set; } = null!;

    private string TextValue
    {
        get => Property.Value?.ToString() ?? string.Empty;
        set => Property.Value = value;
    }
}
```

### TextInputRow Component (with label)

```razor
@* TextInputRow.razor *@
@if (Property.CanRead)
{
    <tr>
        <td>@Property.FriendlyName</td>
        <td>
            <TextInput Property="@Property" />
        </td>
    </tr>
}

@code {
    [Parameter]
    public Csla.Blazor.IPropertyInfo Property { get; set; } = null!;
}
```

### DateInput Component

```razor
@* DateInput.razor *@
<div>
    <input @bind="TextValue"
           disabled="@(!Property.CanWrite)"
           class="form-control" />
    <span class="text-danger">@Property.ErrorText</span>
    <span class="text-warning">@Property.WarningText</span>
    <span class="text-info">@Property.InformationText</span>
</div>

@code {
    [Parameter]
    public Csla.Blazor.IPropertyInfo Property { get; set; } = null!;

    [Parameter]
    public bool EmptyIsMin { get; set; } = true;

    [Parameter]
    public string FormatString { get; set; } = "MM/dd/yyyy";

    private Csla.SmartDate DateValue = new();

    private string TextValue
    {
        get
        {
            DateValue = new Csla.SmartDate((DateTime?)Property.Value, EmptyIsMin);
            DateValue.FormatString = FormatString;
            return DateValue.Text;
        }
        set
        {
            try
            {
                DateValue.Text = value;
            }
            catch (ArgumentException)
            {
                // Invalid text entry, don't update value
                return;
            }
            Property.Value = DateValue.IsEmpty ? null : DateValue.Date;
        }
    }
}
```

### SelectInput Component (for dropdowns)

```razor
@* SelectInput.razor *@
@typeparam TValue

<div>
    <select @bind="SelectedValue"
            disabled="@(!Property.CanWrite)"
            class="form-select">
        @if (AllowEmpty)
        {
            <option value="">@EmptyText</option>
        }
        @foreach (var item in Items)
        {
            <option value="@GetValue(item)">@GetDisplay(item)</option>
        }
    </select>
    <span class="text-danger">@Property.ErrorText</span>
    <span class="text-warning">@Property.WarningText</span>
    <span class="text-info">@Property.InformationText</span>
</div>

@code {
    [Parameter]
    public Csla.Blazor.IPropertyInfo Property { get; set; } = null!;

    [Parameter]
    public IEnumerable<TValue> Items { get; set; } = Enumerable.Empty<TValue>();

    [Parameter]
    public Func<TValue, string> GetDisplay { get; set; } = x => x?.ToString() ?? "";

    [Parameter]
    public Func<TValue, string> GetValue { get; set; } = x => x?.ToString() ?? "";

    [Parameter]
    public bool AllowEmpty { get; set; } = false;

    [Parameter]
    public string EmptyText { get; set; } = "-- Select --";

    private string SelectedValue
    {
        get => Property.Value?.ToString() ?? "";
        set => Property.Value = string.IsNullOrEmpty(value) ? default :
            Items.FirstOrDefault(x => GetValue(x) == value);
    }
}
```

### LabelRow Component (for read-only display)

```razor
@* LabelRow.razor *@
@if (Property.CanRead)
{
    <tr>
        <td>@Property.FriendlyName</td>
        <td>@Property.Value</td>
    </tr>
}

@code {
    [Parameter]
    public Csla.Blazor.IPropertyInfo Property { get; set; } = null!;
}
```

### TextAreaRow Component

```razor
@* TextAreaRow.razor *@
@if (Property.CanRead)
{
    <tr>
        <td>@Property.FriendlyName</td>
        <td>
            <textarea @bind="TextValue"
                      @bind:event="oninput"
                      rows="@Rows"
                      disabled="@(!Property.CanWrite)"
                      class="form-control"></textarea>
            <span class="text-danger">@Property.ErrorText</span>
            <span class="text-warning">@Property.WarningText</span>
            <span class="text-info">@Property.InformationText</span>
        </td>
    </tr>
}

@code {
    [Parameter]
    public Csla.Blazor.IPropertyInfo Property { get; set; } = null!;

    [Parameter]
    public int Rows { get; set; } = 3;

    private string TextValue
    {
        get => Property.Value?.ToString() ?? string.Empty;
        set => Property.Value = value;
    }
}
```

## CslaValidationMessages Reference

The `CslaValidationMessages<T>` component displays validation messages for a specific property.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `For` | `Expression<Func<T>>` | Required | Lambda identifying the property |
| `WrapperId` | `string` | "wrapper" | ID for wrapper div |
| `WrapperClass` | `string` | "validation-messages" | CSS class for wrapper |
| `ErrorClass` | `string` | "text-danger" | CSS class for errors |
| `WarningClass` | `string` | "text-warning" | CSS class for warnings |
| `InfoClass` | `string` | "text-info" | CSS class for info |

### Usage

```razor
<CslaValidationMessages For="() => vm.Model.Name" />

<!-- With custom styling -->
<CslaValidationMessages For="() => vm.Model.Email"
                        ErrorClass="error-text"
                        WarningClass="warning-text" />
```

## Working with Child Collections

CSLA business objects often contain child collections. Here's how to display and edit them.

### Displaying Child Items

```razor
@if (vm.Model.Resources != null)
{
    <h3>Assigned Resources</h3>
    <table class="table">
        <thead>
            <tr>
                <th>Name</th>
                <th>Role</th>
                @if (vm.CanEditObject)
                {
                    <th>Actions</th>
                }
            </tr>
        </thead>
        <tbody>
            @foreach (var item in vm.Model.Resources)
            {
                <tr>
                    <td>@item.FirstName @item.LastName</td>
                    <td>@item.RoleName</td>
                    @if (vm.CanEditObject)
                    {
                        <td>
                            <button @onclick="() => EditResource(item)">Edit</button>
                            <button @onclick="() => RemoveResource(item)">Remove</button>
                        </td>
                    }
                </tr>
            }
        </tbody>
    </table>

    @if (vm.CanEditObject)
    {
        <button @onclick="AddResource">Add Resource</button>
    }
}

@code {
    private ProjectResourceEdit? selectedResource;
    private bool isEditingResource = false;

    private void EditResource(ProjectResourceEdit resource)
    {
        selectedResource = resource;
        selectedResource.BeginEdit();
        isEditingResource = true;
    }

    private void SaveResourceEdit()
    {
        if (selectedResource != null)
        {
            selectedResource.ApplyEdit();
            selectedResource = null;
            isEditingResource = false;
        }
    }

    private void CancelResourceEdit()
    {
        if (selectedResource != null)
        {
            selectedResource.CancelEdit();
            selectedResource = null;
            isEditingResource = false;
        }
    }

    private void RemoveResource(ProjectResourceEdit resource)
    {
        vm.Model.Resources.Remove(resource);
    }

    private async Task AddResource()
    {
        // Typically show a dialog to select a resource to add
        var newResource = await vm.Model.Resources.AddNewAsync();
        EditResource(newResource);
    }
}
```

### Child Edit Dialog Pattern

```razor
@if (isEditingResource && selectedResource != null)
{
    <div class="modal show d-block" tabindex="-1">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Edit Resource Assignment</h5>
                </div>
                <div class="modal-body">
                    <table class="table">
                        <tbody>
                            <tr>
                                <td>Resource</td>
                                <td>@selectedResource.FirstName @selectedResource.LastName</td>
                            </tr>
                            <SelectInputRow Property="@(vm.GetPropertyInfo(() => selectedResource.Role))"
                                            Items="@roleList"
                                            GetDisplay="@(r => r.Name)"
                                            GetValue="@(r => r.Id.ToString())" />
                        </tbody>
                    </table>
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CancelResourceEdit">Cancel</button>
                    <button class="btn btn-primary" @onclick="SaveResourceEdit">OK</button>
                </div>
            </div>
        </div>
    </div>
    <div class="modal-backdrop show"></div>
}
```

## List View Pattern

For displaying lists of business objects with navigation to edit pages.

```razor
@page "/listpersons"
@rendermode InteractiveAuto

@inject Csla.Blazor.State.StateManager StateManager
@inject Csla.IDataPortal<PersonList> personListPortal
@inject Csla.Blazor.ViewModel<PersonList> vm

<h1>People</h1>
<p class="alert-danger">@vm.ViewModelErrorText</p>

@if (vm.Model == null)
{
    <p>Loading...</p>
}
else
{
    @if (vm.CanCreateObject)
    {
        <p><a href="editperson" class="btn btn-primary">Add Person</a></p>
    }

    <table class="table">
        <thead>
            <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var item in vm.Model)
            {
                <tr>
                    <td>@item.Name</td>
                    <td>@item.Email</td>
                    <td>
                        @if (vm.CanEditObject)
                        {
                            <a href="editperson/@item.Id">Edit</a>
                        }
                        @if (vm.CanDeleteObject)
                        {
                            <button @onclick="() => DeletePerson(item.Id)">Delete</button>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    protected override async Task OnInitializedAsync()
    {
        await StateManager.InitializeAsync();
        await vm.RefreshAsync(() => personListPortal.FetchAsync());
    }

    private async Task DeletePerson(int id)
    {
        // Call delete portal and refresh list
        await personDeletePortal.DeleteAsync(id);
        await vm.RefreshAsync(() => personListPortal.FetchAsync());
    }
}
```

## Authorization Patterns

### Page-Level Authorization

Use the `[HasPermission]` attribute to restrict access to entire pages:

```csharp
@page "/editperson/{id?}"
@attribute [HasPermission(Csla.Rules.AuthorizationActions.EditObject, typeof(PersonEdit))]
```

### UI Element Authorization

Use ViewModel authorization properties to show/hide UI elements:

```razor
@* Show save button only if user can edit *@
@if (vm.CanEditObject)
{
    <button @onclick="vm.SaveAsync" disabled="@(!vm.Model.IsSavable)">Save</button>
}

@* Show delete button only if user can delete *@
@if (vm.CanDeleteObject)
{
    <button @onclick="Delete">Delete</button>
}

@* Show add button only if user can create *@
@if (vm.CanCreateObject)
{
    <a href="editperson">Add New</a>
}
```

### Property-Level Authorization

Use `GetPropertyInfo()` to access property metastate and check property-level permissions:

```razor
@* Only show field if user can read it *@
@if (vm.GetPropertyInfo(() => vm.Model.Salary).CanRead)
{
    <tr>
        <td>@(vm.GetPropertyInfo(() => vm.Model.Salary).FriendlyName)</td>
        <td>
            @if (vm.GetPropertyInfo(() => vm.Model.Salary).CanWrite)
            {
                <TextInput Property="@(vm.GetPropertyInfo(() => vm.Model.Salary))" />
            }
            else
            {
                @vm.Model.Salary.ToString("C")
            }
        </td>
    </tr>
}
```

## Error Handling

### Displaying ViewModel Errors

Always display `ViewModelErrorText` at the top of your form:

```razor
@if (!string.IsNullOrEmpty(vm.ViewModelErrorText))
{
    <div class="alert alert-danger">@vm.ViewModelErrorText</div>
}
```

### Handling Save Errors

Subscribe to the Error event for custom error handling:

```csharp
protected override async Task OnInitializedAsync()
{
    await StateManager.InitializeAsync();

    vm.Error += (sender, args) =>
    {
        // Log error, show toast, etc.
        Console.WriteLine($"Error: {args.Exception.Message}");
    };

    // ... rest of initialization
}
```

## Loading States

### Simple Loading Indicator

```razor
@if (vm.Model == null)
{
    <p>Loading...</p>
}
else
{
    <!-- Form content -->
}
```

### Busy State During Save

```razor
<button @onclick="vm.SaveAsync"
        disabled="@(!vm.Model.IsSavable || vm.IsBusy)">
    @if (vm.IsBusy)
    {
        <span>Saving...</span>
    }
    else
    {
        <span>Save</span>
    }
</button>
```

## Complete Example: Project Edit Form

Here's a complete example showing all patterns together:

```razor
@page "/editproject"
@page "/editproject/{id:int}"
@rendermode InteractiveAuto
@attribute [HasPermission(Csla.Rules.AuthorizationActions.EditObject, typeof(ProjectEdit))]

@inject Csla.Blazor.State.StateManager StateManager
@inject Csla.IDataPortal<ProjectEdit> projectEditPortal
@inject Csla.Blazor.ViewModel<ProjectEdit> vm
@inject NavigationManager NavigationManager

<h1>@(Id.HasValue ? "Edit Project" : "New Project")</h1>

@if (!string.IsNullOrEmpty(vm.ViewModelErrorText))
{
    <div class="alert alert-danger">@vm.ViewModelErrorText</div>
}

@if (vm.Model == null)
{
    <div class="d-flex justify-content-center">
        <div class="spinner-border" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    </div>
}
else
{
    <div class="card">
        <div class="card-body">
            <table class="table">
                <tbody>
                    <LabelRow Property="@(vm.GetPropertyInfo(() => vm.Model.Id))" />
                    <TextInputRow Property="@(vm.GetPropertyInfo(() => vm.Model.Name))" />
                    <TextAreaRow Property="@(vm.GetPropertyInfo(() => vm.Model.Description))"
                                 Rows="5" />
                    <DateInputRow Property="@(vm.GetPropertyInfo(() => vm.Model.Started))" />
                    <DateInputRow Property="@(vm.GetPropertyInfo(() => vm.Model.Ended))" />
                </tbody>
            </table>
        </div>
    </div>

    @if (vm.Model.Resources != null && vm.Model.Resources.Count > 0)
    {
        <div class="card mt-3">
            <div class="card-header">
                <h5>Assigned Resources</h5>
            </div>
            <div class="card-body">
                <table class="table">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Role</th>
                            @if (vm.CanEditObject)
                            {
                                <th>Actions</th>
                            }
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var resource in vm.Model.Resources)
                        {
                            <tr>
                                <td>@resource.FirstName @resource.LastName</td>
                                <td>@resource.RoleName</td>
                                @if (vm.CanEditObject)
                                {
                                    <td>
                                        <button class="btn btn-sm btn-outline-danger"
                                                @onclick="() => vm.Model.Resources.Remove(resource)">
                                            Remove
                                        </button>
                                    </td>
                                }
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </div>
    }

    <div class="mt-3">
        @if (vm.CanEditObject)
        {
            <button class="btn btn-primary"
                    @onclick="vm.SaveAsync"
                    disabled="@(!vm.Model.IsSavable || vm.IsBusy)">
                @if (vm.IsBusy)
                {
                    <span class="spinner-border spinner-border-sm" role="status"></span>
                    <span>Saving...</span>
                }
                else
                {
                    <span>Save</span>
                }
            </button>
        }
        <button class="btn btn-secondary" @onclick="Cancel">Cancel</button>
    </div>
}

@code {
    [Parameter]
    public int? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await StateManager.InitializeAsync();

        vm.Saved += () => NavigationManager.NavigateTo("projects");
        vm.ModelPropertyChanged += async (s, e) =>
            await InvokeAsync(() => StateHasChanged());

        if (Id.HasValue)
            await vm.RefreshAsync(() => projectEditPortal.FetchAsync(Id.Value));
        else
            await vm.RefreshAsync(() => projectEditPortal.CreateAsync());
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("projects");
    }
}
```

## Best Practices Summary

1. **Always call `StateManager.InitializeAsync()` first** in `OnInitializedAsync()`
2. **Wire up `ModelPropertyChanged`** to trigger UI refresh when properties change
3. **Use `GetPropertyInfo()`** for metastate-driven data binding with authorization and validation
4. **Check `IsSavable` before enabling Save** - this ensures all validation passes
5. **Display `ViewModelErrorText`** prominently to show operation errors
6. **Use authorization properties** (`CanEditObject`, etc.) to conditionally render UI
7. **Create reusable input components** that accept `Csla.Blazor.IPropertyInfo` for consistency
8. **Handle loading states** by checking if `vm.Model == null`
9. **Handle busy states** by checking `vm.IsBusy` during async operations
10. **Use `BeginEdit`/`ApplyEdit`/`CancelEdit`** when editing child objects inline

## Notes

- The `@rendermode InteractiveAuto` directive enables the page to work in both server and WebAssembly render modes
- For modern Blazor apps with multiple render modes, state synchronization via `StateManager` is critical
- All input components should respect the `CanRead` and `CanWrite` metastate from `Csla.Blazor.IPropertyInfo`
- Validation messages include Error, Warning, and Information severities - display all three for the best user experience
- The `FriendlyName` property comes from the business object's property registration (`Csla.Core.IPropertyInfo`) and provides consistent labeling
- `Csla.Blazor.IPropertyInfo` (metastate) is different from `Csla.Core.IPropertyInfo` (registration metadata) - the Blazor type wraps a property to expose runtime state
