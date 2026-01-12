# ASP.NET Core MVC and Razor Pages Configuration

CSLA supports ASP.NET Core MVC and Razor Pages applications with straightforward configuration. Both frameworks share nearly identical CSLA setup, making them easy to configure for server-side web applications.

## Overview

ASP.NET Core MVC and Razor Pages are server-side rendering frameworks that run entirely on the server. Unlike Blazor, there is no client/server boundary for the data portal, which simplifies configuration significantly.

**Key characteristics:**
- All code executes on the server
- No data portal proxy configuration needed (unless using a separate app server)
- Standard ASP.NET Core authentication and authorization
- Simple dependency injection setup

## Basic Configuration

### Razor Pages Application

Configure CSLA in `Program.cs` for a Razor Pages application:

```csharp
using Csla.Configuration;
using Csla.Web.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages with CSLA model binder
builder.Services.AddRazorPages().AddMvcOptions(options =>
{
    options.ModelBinderProviders.Insert(0, new CslaModelBinderProvider());
});

// Required for CSLA ASP.NET Core integration
builder.Services.AddHttpContextAccessor();

// Add CSLA services
builder.Services.AddCsla(o => o
    .AddAspNetCore());

// Register your data access layer
builder.Services.AddTransient(typeof(DataAccess.IPersonDal), typeof(DataAccess.PersonDal));

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
```

### MVC Application

Configure CSLA in `Program.cs` (or `Startup.cs`) for an MVC application:

```csharp
using Csla.Configuration;
using Csla.Web.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add MVC with CSLA model binder
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new CslaModelBinderProvider());
});

// Required for CSLA ASP.NET Core integration
builder.Services.AddHttpContextAccessor();

// Add CSLA services
builder.Services.AddCsla(o => o
    .AddAspNetCore());

// Register your data access layer
builder.Services.AddTransient(typeof(DataAccess.IPersonDal), typeof(DataAccess.PersonDal));

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### Legacy Startup.cs Configuration

For applications using the legacy `Startup.cs` pattern:

```csharp
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // For MVC:
        services.AddControllersWithViews(options =>
            options.ModelBinderProviders.Insert(0, new CslaModelBinderProvider()));
        
        // Or for Razor Pages:
        // services.AddRazorPages().AddMvcOptions(options =>
        //     options.ModelBinderProviders.Insert(0, new CslaModelBinderProvider()));

        services.AddCsla(o => o.AddAspNetCore());
        
        // Register DAL services
        services.AddTransient(typeof(DataAccess.IPersonDal), typeof(DataAccess.PersonDal));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
        }
        
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });

        app.UseCsla();
    }
}
```

## Key Configuration Points

### AddAspNetCore()

The `AddAspNetCore()` extension method configures CSLA for ASP.NET Core hosting:

```csharp
builder.Services.AddCsla(o => o
    .AddAspNetCore());
```

This registers:
- `ApplicationContextManagerHttpContext` as the context manager
- HTTP context accessor integration
- MVC model binder support

### CslaModelBinderProvider

The CSLA model binder enables proper binding of CSLA business objects in controller actions and page handlers:

```csharp
options.ModelBinderProviders.Insert(0, new CslaModelBinderProvider());
```

This model binder:
- Properly handles CSLA object state during model binding
- Supports validation integration with ASP.NET Core model state
- Works with both MVC controllers and Razor Page handlers

### HttpContextAccessor

CSLA requires access to the HTTP context for proper context management:

```csharp
builder.Services.AddHttpContextAccessor();
```

> **Note:** The `AddAspNetCore()` method automatically registers the HTTP context accessor, but explicitly adding it ensures clarity and prevents issues if the order of registration changes.

## Using CSLA in Razor Pages

### Page Model with Dependency Injection

Inject the data portal into your page models:

```csharp
using BusinessLibrary;
using Csla;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyApp.Pages
{
    public class ListPeopleModel : PageModel
    {
        private readonly IDataPortal<PersonList> _portal;

        public ListPeopleModel(IDataPortal<PersonList> portal)
        {
            _portal = portal;
        }

        public PersonList PersonList { get; set; }

        public async Task OnGet()
        {
            PersonList = await _portal.FetchAsync();
        }
    }
}
```

### View Imports

Add CSLA tag helpers in `_ViewImports.cshtml`:

```cshtml
@using MyApp
@namespace MyApp.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, Csla.AspNetCore
```

## Using CSLA in MVC Controllers

### Controller with Dependency Injection

Inject the data portal into your controllers:

```csharp
using BusinessLibrary;
using Csla;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    public class PersonController : Controller
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

        public async Task<IActionResult> Index()
        {
            var list = await _listPortal.FetchAsync();
            return View(list);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var person = await _personPortal.FetchAsync(id);
            return View(person);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(PersonEdit person)
        {
            if (person.IsSavable)
            {
                person = await person.SaveAsync();
                return RedirectToAction("Index");
            }
            return View(person);
        }
    }
}
```

## Authentication Configuration

### Cookie Authentication

Add standard ASP.NET Core authentication:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCsla(o => o.AddAspNetCore());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
```

CSLA automatically uses the authenticated user from the HTTP context for authorization rules.

## Multi-Tier Configuration

### Using a Separate Data Portal Server

If your web application calls a separate data portal server:

```csharp
builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddClientSideDataPortal(client => client
            .UseHttpProxy(proxy => proxy
                .DataPortalUrl = "https://appserver.example.com/api/DataPortal"))));
```

### Hosting a Data Portal Endpoint

To expose a data portal endpoint from your web application:

```csharp
// Add controllers for the data portal
builder.Services.AddControllers();

builder.Services.AddCsla(o => o
    .AddAspNetCore()
    .DataPortal(dp => dp
        .AddServerSideDataPortal()));

// ...

app.MapControllers();
```

Create a data portal controller:

```csharp
using Csla;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class DataPortalController : Csla.Server.Hosts.HttpPortalController
{
    public DataPortalController(ApplicationContext applicationContext)
        : base(applicationContext)
    {
    }
}
```

## Comparison: Razor Pages vs MVC

| Feature | Razor Pages | MVC |
|---------|-------------|-----|
| CSLA Configuration | Identical | Identical |
| Service Registration | `AddRazorPages()` | `AddControllersWithViews()` |
| Routing | Page-based (`@page`) | Controller-based |
| Data Portal Injection | PageModel constructor | Controller constructor |
| Model Binder | Same `CslaModelBinderProvider` | Same `CslaModelBinderProvider` |

Both frameworks use the same CSLA configuration. The only differences are in how ASP.NET Core routes requests.

## Best Practices

1. **Use dependency injection** - Always inject `IDataPortal<T>` rather than using static methods
2. **Register the model binder** - The CSLA model binder is essential for proper object binding
3. **Add HttpContextAccessor** - Required for CSLA's context management
4. **Use async methods** - Prefer `FetchAsync`, `SaveAsync`, etc. for better scalability
5. **Handle validation** - Check `IsSavable` before calling `SaveAsync()`
6. **Register DAL services** - Use dependency injection for data access layer components

## Troubleshooting

### Business Object State Not Preserved

If business object state is lost during post-back:
- Verify `CslaModelBinderProvider` is registered
- Ensure it's inserted at position 0 in the provider list

### Context Not Available

If you get errors about missing context:
- Verify `AddHttpContextAccessor()` is called
- Ensure `AddAspNetCore()` is called in CSLA configuration

### Data Portal Not Working

If data portal calls fail:
- For local data portal: Verify DAL services are registered
- For remote data portal: Check the data portal URL and network connectivity
- Ensure business object assemblies are referenced correctly

## Notes

- ASP.NET Core MVC and Razor Pages are much simpler to configure than Blazor because all code runs on the server
- No client/server state synchronization is needed
- Authentication automatically flows through HTTP context
- The same CSLA configuration works for both MVC and Razor Pages
