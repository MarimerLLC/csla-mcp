# CSLA .NET Solution Architecture

This document describes the recommended solution and project structure for applications built using CSLA .NET. Following this layered architecture ensures clean separation of concerns, testability, and maintainability.

**Related Documents**:
- `Glossary.md` - Quick reference for CSLA terminology and concepts
- `Data-Access.md` - Detailed data access layer implementation examples
- `v10/CslaClassLibrary.md` - Business layer project setup for CSLA 10
- `DataPortalGuide.md` - In-depth guide to the data portal architecture

## Architectural Overview

CSLA applications follow a logical layered architecture with three primary tiers:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         1. PRESENTATION TIER                             │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │              Interface Layer (UI/API Surface)                        │ │
│  │  HTML/Razor • JSON API • XAML • WinForms • MAUI • Console           │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │           Interface Control Layer (UI Logic)                         │ │
│  │  ViewModels • Controllers • Presenters • Code-Behind • Page Models  │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          2. BUSINESS TIER                                │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                    Business Layer (Domain)                           │ │
│  │  CSLA Business Objects • Validation Rules • Authorization Rules     │ │
│  │  Calculation Rules • Business Logic • Object Graphs                 │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        3. DATA ACCESS TIER                               │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │             Data Access Abstraction (Interfaces/DTOs)                │ │
│  │  DAL Interfaces • Data Transfer Objects (DTOs) • Entity Types       │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │             Data Access Concrete (Implementation)                    │ │
│  │  Entity Framework Core • ADO.NET • Dapper • HTTP Clients            │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                    Data Storage Layer                                │ │
│  │  SQL Server • PostgreSQL • SQLite • Files • External APIs           │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

## Recommended Solution Structure

A typical CSLA solution contains the following projects:

```
MySolution/
├── MySolution.sln
│
├── src/
│   ├── MyApp.Business/              # Business Layer
│   │   ├── MyApp.Business.csproj
│   │   ├── PersonEdit.cs            # Editable root business object
│   │   ├── PersonInfo.cs            # Read-only info object  
│   │   ├── PersonList.cs            # Read-only list
│   │   └── Rules/                   # Custom business rules
│   │       └── PersonNameRule.cs
│   │
│   ├── MyApp.Dal/                   # Data Access Abstraction
│   │   ├── MyApp.Dal.csproj
│   │   ├── IPersonDal.cs            # DAL interface
│   │   └── PersonData.cs            # DTO/Entity type
│   │
│   ├── MyApp.Dal.EfCore/            # Data Access Concrete (EF Core)
│   │   ├── MyApp.Dal.EfCore.csproj
│   │   ├── PersonDal.cs             # DAL implementation
│   │   └── MyAppDbContext.cs        # EF DbContext
│   │
│   └── MyApp.Web/                   # Presentation (Blazor/API)
│       ├── MyApp.Web.csproj
│       ├── Program.cs               # Host configuration
│       ├── Pages/                   # Blazor pages (Interface Layer)
│       │   ├── PersonEdit.razor
│       │   └── PersonList.razor
│       └── ViewModels/              # Interface Control Layer
│           ├── PersonEditViewModel.cs
│           └── PersonListViewModel.cs
│
└── tests/
    ├── MyApp.Business.Tests/        # Business layer unit tests
    └── MyApp.Dal.Tests/             # DAL integration tests
```

## Project Dependencies

The dependency flow should always point downward (toward lower layers):

```
┌─────────────────────┐
│   Presentation      │  References: Business, Dal (for DI registration)
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│     Business        │  References: Dal (abstractions only)
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   Dal (Abstractions)│  References: None (pure interfaces/DTOs)
└─────────────────────┘
           ▲
           │
┌─────────────────────┐
│  Dal.Concrete       │  References: Dal (abstractions)
└─────────────────────┘
```

**Key rules:**
- The Business layer NEVER references a concrete DAL implementation
- The Business layer references ONLY the Dal abstraction project
- The Presentation layer references everything (for DI wiring)
- The concrete DAL references ONLY the abstraction project

---

## 1. Presentation Tier

The presentation tier handles all user interaction and is divided into two sub-layers.

### Interface Layer

The interface layer is the actual surface that users or systems interact with. This varies by application type:

| Application Type | Interface Layer Technology |
| --- | --- |
| Blazor Server/WebAssembly | Razor components (`.razor` files) |
| ASP.NET Core API | JSON endpoints via controllers |
| WPF | XAML views (`.xaml` files) |
| WinForms | Form classes and controls |
| MAUI | XAML pages and ContentViews |
| Console | Command-line text interface |

The interface layer should contain ONLY:
- Visual layout and styling (HTML/XAML/etc.)
- Data binding expressions
- User input handling (calling into the control layer)
- Navigation

The interface layer should NOT contain:
- Business logic
- Data access code
- Complex conditional logic

### Interface Control Layer

The interface control layer manages the behavior behind the interface. It serves as the bridge between the UI and the business objects:

| Pattern | Technologies | Description |
| --- | --- | --- |
| ViewModel | WPF, MAUI, Blazor | Class that exposes properties and commands for data binding |
| Controller | ASP.NET Core MVC/API | Handles HTTP requests and returns responses |
| Presenter | MVP pattern | Mediates between view and model |
| Code-Behind | Blazor, WinForms | Code directly behind a form or page |
| Page Model | Razor Pages | CSLA `ViewModelBase<T>` or custom class |

**Responsibilities:**
- Obtain business objects via the data portal (`IDataPortal<T>`)
- Expose business object properties for data binding
- Handle user actions (save, delete, refresh, etc.)
- Manage UI state (loading, error messages, navigation)

**Example ViewModel with CSLA `ViewModelBase<T>`:**

```csharp
public class PersonEditViewModel : ViewModelBase<PersonEdit>
{
    public PersonEditViewModel(IDataPortal<PersonEdit> portal)
    {
        DataPortal = portal;
    }

    public IDataPortal<PersonEdit> DataPortal { get; }

    public async Task LoadAsync(int id)
    {
        try
        {
            Model = await DataPortal.FetchAsync(id);
        }
        catch (DataPortalException ex)
        {
            // Handle and display error
        }
    }

    public async Task SaveAsync()
    {
        Model = await Model.SaveAsync();
    }
}
```

**Example ASP.NET Core Controller:**

```csharp
[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly IDataPortal<PersonEdit> _personPortal;
    private readonly IDataPortal<PersonList> _listPortal;

    public PersonController(
        IDataPortal<PersonEdit> personPortal,
        IDataPortal<PersonList> listPortal)
    {
        _personPortal = personPortal;
        _listPortal = listPortal;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _listPortal.FetchAsync();
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var person = await _personPortal.FetchAsync(id);
        return Ok(person);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PersonDto dto)
    {
        var person = await _personPortal.CreateAsync();
        person.Name = dto.Name;
        person.Email = dto.Email;
        person = await person.SaveAsync();
        return CreatedAtAction(nameof(Get), new { id = person.Id }, person);
    }
}
```

---

## 2. Business Tier

The business tier contains ALL domain logic and is implemented entirely using CSLA base types.

### Business Layer

The business layer consists of domain objects built using CSLA stereotypes. These objects encapsulate:

| Concern | Implementation |
| --- | --- |
| **Validation** | Business rules using `BusinessRules.AddRule()` and data annotations |
| **Calculations** | Calculation rules that compute derived values |
| **Authorization** | Property and object-level authorization rules |
| **State Management** | CSLA metastate tracking (IsNew, IsDirty, IsValid, etc.) |
| **Object Relationships** | Parent-child hierarchies via child objects and lists |
| **Data Binding** | Automatic `INotifyPropertyChanged` support |

**Business Layer Project (.csproj):**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Csla" Version="10.0.0" />
    <PackageReference Include="Csla.Generator.AutoImplementProperties.CSharp" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp.Dal\MyApp.Dal.csproj" />
  </ItemGroup>

</Project>
```

**Example Editable Root Business Object:**

```csharp
using Csla;
using System.ComponentModel.DataAnnotations;
using MyApp.Dal;

namespace MyApp.Business;

[CslaImplementProperties]
public partial class PersonEdit : BusinessBase<PersonEdit>
{
    [Create]
    [Fetch]
    public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
    public partial int Id { get; private set; }

    [Create]
    [Fetch]
    [Required]
    [StringLength(100)]
    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public partial string Name { get; set; }

    [Create]
    [Fetch]
    [EmailAddress]
    public static readonly PropertyInfo<string> EmailProperty = RegisterProperty<string>(nameof(Email));
    public partial string Email { get; set; }

    [Create]
    [Fetch]
    public static readonly PropertyInfo<decimal> SalaryProperty = RegisterProperty<decimal>(nameof(Salary));
    public partial decimal Salary { get; set; }

    // Object-level authorization
    [ObjectAuthorizationRules]
    private static void AddObjectAuthorizationRules()
    {
        Csla.Rules.BusinessRules.AddRule(
            typeof(PersonEdit),
            new Csla.Rules.CommonRules.IsInRole(
                Csla.Rules.AuthorizationActions.EditObject, "Admin", "HR"));
    }

    // Business rules
    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        
        // Property-level authorization
        BusinessRules.AddRule(
            new Csla.Rules.CommonRules.IsInRole(
                Csla.Rules.AuthorizationActions.WriteProperty, 
                SalaryProperty, 
                "Admin", "HR"));
        
        // Custom validation rules
        BusinessRules.AddRule(new SalaryMustBePositiveRule(SalaryProperty));
    }

    // Data portal operations
    [Create]
    private async Task CreateAsync()
    {
        // Initialize default values
        using (BypassPropertyChecks)
        {
            Name = string.Empty;
            Email = string.Empty;
            Salary = 0m;
        }
        await BusinessRules.CheckRulesAsync();
    }

    [Fetch]
    private async Task FetchAsync(int id, [Inject] IPersonDal dal)
    {
        var data = await dal.GetPersonByIdAsync(id);
        using (BypassPropertyChecks)
        {
            Id = data.Id;
            Name = data.Name;
            Email = data.Email;
            Salary = data.Salary;
        }
        await BusinessRules.CheckRulesAsync();
    }

    [Insert]
    private async Task InsertAsync([Inject] IPersonDal dal)
    {
        var data = new PersonData
        {
            Name = Name,
            Email = Email,
            Salary = Salary
        };
        data = await dal.InsertPersonAsync(data);
        using (BypassPropertyChecks)
        {
            Id = data.Id;
        }
    }

    [Update]
    private async Task UpdateAsync([Inject] IPersonDal dal)
    {
        var data = new PersonData
        {
            Id = Id,
            Name = Name,
            Email = Email,
            Salary = Salary
        };
        await dal.UpdatePersonAsync(data);
    }

    [DeleteSelf]
    private async Task DeleteSelfAsync([Inject] IPersonDal dal)
    {
        await dal.DeletePersonAsync(Id);
    }
}
```

---

## 3. Data Access Tier

The data access tier is divided into three conceptual parts, typically implemented as two or three projects.

### Data Access Abstraction (Interfaces and DTOs)

This project contains ONLY:
- DAL interfaces (contracts)
- Data Transfer Objects (DTOs) / Entity types
- No implementation code

**Why separate abstractions?**
- Business layer depends only on interfaces
- Multiple DAL implementations can coexist (SQL, mock, etc.)
- Easy to swap data storage without changing business code

**Dal Project (.csproj):**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

**DAL Interface:**

```csharp
namespace MyApp.Dal;

public interface IPersonDal
{
    Task<List<PersonData>> GetAllPersonsAsync();
    Task<PersonData> GetPersonByIdAsync(int id);
    Task<PersonData> InsertPersonAsync(PersonData data);
    Task<PersonData> UpdatePersonAsync(PersonData data);
    Task DeletePersonAsync(int id);
}
```

**DTO/Entity Type:**

```csharp
namespace MyApp.Dal;

public class PersonData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}
```

### Data Access Concrete (Implementation)

This project contains the actual data access implementation using a specific technology:

| Technology | When to Use |
| --- | --- |
| Entity Framework Core | Object-relational mapping to SQL databases |
| ADO.NET | Direct SQL access with full control |
| Dapper | Lightweight micro-ORM |
| HTTP Client | Calling external REST APIs |
| File I/O | File-based data storage |

**EF Core Dal Implementation Project (.csproj):**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp.Dal\MyApp.Dal.csproj" />
  </ItemGroup>

</Project>
```

**DbContext:**

```csharp
using Microsoft.EntityFrameworkCore;

namespace MyApp.Dal.EfCore;

public class MyAppDbContext : DbContext
{
    public MyAppDbContext(DbContextOptions<MyAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<PersonData> Persons => Set<PersonData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var person = modelBuilder.Entity<PersonData>();
        person.ToTable("Persons");
        person.HasKey(p => p.Id);
        person.Property(p => p.Id).ValueGeneratedOnAdd();
        person.Property(p => p.Name).HasMaxLength(100).IsRequired();
        person.Property(p => p.Email).HasMaxLength(200);
        person.Property(p => p.Salary).HasPrecision(18, 2);
    }
}
```

**DAL Implementation:**

```csharp
using Microsoft.EntityFrameworkCore;

namespace MyApp.Dal.EfCore;

public class PersonDal : IPersonDal
{
    private readonly MyAppDbContext _dbContext;

    public PersonDal(MyAppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<PersonData>> GetAllPersonsAsync()
    {
        return await _dbContext.Persons
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<PersonData> GetPersonByIdAsync(int id)
    {
        return await _dbContext.Persons.FindAsync(id)
            ?? throw new DataNotFoundException($"Person with ID {id} not found.");
    }

    public async Task<PersonData> InsertPersonAsync(PersonData data)
    {
        _dbContext.Persons.Add(data);
        await _dbContext.SaveChangesAsync();
        return data;
    }

    public async Task<PersonData> UpdatePersonAsync(PersonData data)
    {
        _dbContext.Persons.Update(data);
        await _dbContext.SaveChangesAsync();
        return data;
    }

    public async Task DeletePersonAsync(int id)
    {
        var entity = await _dbContext.Persons.FindAsync(id)
            ?? throw new DataNotFoundException($"Person with ID {id} not found.");
        _dbContext.Persons.Remove(entity);
        await _dbContext.SaveChangesAsync();
    }
}
```

### Data Storage Layer

The data storage layer is the actual physical storage, not a code project:

- SQL Server, PostgreSQL, SQLite, MySQL, Oracle databases
- File systems (JSON, XML, CSV files)
- Azure Blob Storage, AWS S3
- External web services and APIs
- In-memory storage (for testing)

---

## Host Configuration

The presentation/host project wires everything together using dependency injection:

```csharp
// Program.cs in the Web/API project

var builder = WebApplication.CreateBuilder(args);

// Add CSLA
builder.Services.AddCsla();
builder.Services.AddTransient(typeof(IDataPortal<>), typeof(DataPortal<>));
builder.Services.AddTransient(typeof(IChildDataPortal<>), typeof(ChildDataPortal<>));

// Add data access
builder.Services.AddDbContext<MyAppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPersonDal, PersonDal>();

// Add presentation services
builder.Services.AddControllersWithViews();
// or for Blazor:
// builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
// ... middleware configuration
app.Run();
```

---

## Alternative Project Structures

### Combined Dal Project (Simpler)

For smaller applications, the DAL abstraction and implementation can be combined:

```
MySolution/
├── MyApp.Business/         # Business layer
├── MyApp.Dal/              # Combined interfaces + EF Core implementation
└── MyApp.Web/              # Presentation
```

### Multiple UI Projects

Enterprise applications often have multiple presentation projects sharing the same business layer:

```
MySolution/
├── MyApp.Business/         # Shared business layer
├── MyApp.Dal/              # Shared data access
├── MyApp.Web.Admin/        # Admin Blazor app
├── MyApp.Web.Public/       # Public website
├── MyApp.Api/              # REST API
└── MyApp.Mobile/           # MAUI mobile app
```

### Multiple DAL Implementations

Switch between data sources or support multiple storage backends:

```
MySolution/
├── MyApp.Business/         # Business layer
├── MyApp.Dal/              # Abstractions only
├── MyApp.Dal.SqlServer/    # SQL Server implementation
├── MyApp.Dal.PostgreSql/   # PostgreSQL implementation
├── MyApp.Dal.Mock/         # Mock implementation for testing
└── MyApp.Web/              # Presentation
```

---

## Best Practices Summary

| Layer | Do | Don't |
| --- | --- | --- |
| **Interface** | Focus on visual layout and binding | Put logic in XAML/Razor |
| **Interface Control** | Use `IDataPortal<T>` for all data access | Call DAL directly |
| **Business** | Put ALL business logic here | Reference concrete DAL |
| **Dal Abstraction** | Keep interfaces and DTOs only | Add implementation code |
| **Dal Concrete** | Implement one technology per project | Mix EF and ADO.NET in one project |
| **Data Storage** | Choose appropriate storage for the use case | Hardcode connection strings |

---

## See Also

- `Data-Access.md` - Detailed ADO.NET and Entity Framework Core DAL examples
- `DataPortalGuide.md` - Understanding the data portal architecture
- `ObjectStereotypes.md` - Choosing the right CSLA base class
- `BlazorConfiguration.md` - Blazor-specific setup
- `MauiConfiguration.md` - MAUI-specific setup
- `HttpDataPortalConfiguration.md` - Remote data portal configuration
