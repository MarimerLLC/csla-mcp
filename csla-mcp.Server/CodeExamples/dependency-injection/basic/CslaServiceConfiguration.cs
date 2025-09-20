using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.DataAccess;
using MyApp.Library;

namespace MyApp.Configuration;

public static class CslaServiceConfiguration
{
    /// <summary>
    /// Configure CSLA services for ASP.NET Core applications
    /// </summary>
    public static IServiceCollection AddCslaServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add CSLA framework services
        services.AddCsla(options => options
            .AddAspNetCore()
            .DataPortal(cfg => cfg
                .AddClientSideDataPortal()
                .AddServerSideDataPortal())
            .Security(cfg => cfg
                .AddWindowsIdentity()) // or AddJwtIdentity() for JWT
            .Serialization(cfg => cfg
                .SerializationFormatter(typeof(Csla.Serialization.Mobile.MobileFormatter))));

        // Register Data Access Layer services
        RegisterDataAccessServices(services, configuration);

        // Register custom authorization providers
        services.AddScoped<IAuthorizationProvider, CustomAuthorizationProvider>();

        // Register custom rule providers if needed
        services.AddScoped<IBusinessRuleProvider, CustomBusinessRuleProvider>();

        return services;
    }

    /// <summary>
    /// Configure CSLA services for Blazor applications
    /// </summary>
    public static IServiceCollection AddCslaBlazorServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add base CSLA services
        services.AddCslaServices(configuration);

        // Add Blazor-specific services
        services.AddCsla(options => options
            .AddBlazorWebAssembly()); // or AddBlazorServer() for Blazor Server

        return services;
    }

    /// <summary>
    /// Configure CSLA for console applications or background services
    /// </summary>
    public static IServiceCollection AddCslaConsoleServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCsla(options => options
            .DataPortal(cfg => cfg
                .AddServerSideDataPortal())
            .Security(cfg => cfg
                .AddWindowsIdentity()));

        RegisterDataAccessServices(services, configuration);

        return services;
    }

    private static void RegisterDataAccessServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Entity Framework context
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Register DAL interfaces and implementations
        services.AddScoped<ICustomerDal, CustomerDal>();
        services.AddScoped<IOrderDal, OrderDal>();
        services.AddScoped<IProductDal, ProductDal>();

        // Register repository pattern if used
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Register Unit of Work pattern if used
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }
}

/// <summary>
/// Example custom authorization provider
/// </summary>
public class CustomAuthorizationProvider : IAuthorizationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserService _userService;

    public CustomAuthorizationProvider(IHttpContextAccessor httpContextAccessor, IUserService userService)
    {
        _httpContextAccessor = httpContextAccessor;
        _userService = userService;
    }

    public bool IsInRole(string role)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.IsInRole(role) ?? false;
    }

    public bool HasPermission(string permission)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return false;

        return _userService.HasPermission(userId, permission);
    }
}

/// <summary>
/// Example custom business rule provider
/// </summary>
public class CustomBusinessRuleProvider : IBusinessRuleProvider
{
    private readonly IConfiguration _configuration;

    public CustomBusinessRuleProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IEnumerable<IBusinessRule> GetRulesForType(Type objectType)
    {
        // Return custom rules based on configuration or other logic
        var rules = new List<IBusinessRule>();

        if (objectType == typeof(Customer))
        {
            // Add dynamic rules for Customer objects
            rules.Add(new CustomCustomerValidationRule());
        }

        return rules;
    }
}