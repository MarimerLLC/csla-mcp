# Unit Testing with Csla.Testing

> **Applies to CSLA 10.2 and later.** The `Csla.Testing` package first shipped with CSLA 10.2.0. It isn't available for CSLA 9 or CSLA 10.0/10.1.

`Csla.Testing` is a NuGet package with supporting types for unit tests of CSLA business code: business classes, business rules, and authorization rules. It gives you:

- **`CslaTestHost`** - a self-contained, disposable CSLA container for one test, with a working local data portal
- **`AddCslaTesting`** - adds the supporting services a test needs to an `IServiceCollection` you build yourself
- **`BusinessRuleTester`** and **`AuthorizationRuleTester`** - run a single rule in isolation and assert on the result
- **`RuleCache`** - clears CSLA's per-type rule caches between tests

The package is for test projects only. It isn't required by, or meant for, production code. It has no dependency on any test framework, so it works with MSTest, xUnit, NUnit, or any other runner.

## Installation

```bash
dotnet add package Csla.Testing
```

Use the same version as your `Csla` package. The package depends only on `Csla` (plus `Microsoft.Extensions.Logging`).

| CSLA version | `Csla.Testing` target frameworks |
| --- | --- |
| 10.2 | `netstandard2.0`, `net8.0`, `net9.0`, `net10.0` |
| 11 | `netstandard2.0`, `net10.0`, `net11.0` |

## Namespaces

| Type | Namespace |
| --- | --- |
| `CslaTestHost`, `CslaTestHostOptions`, `CslaTestingOptions`, `CslaTestContextManager`, `CslaTestPrincipal`, `CslaTestHostEnvironment` | `Csla.Testing` |
| `AddCslaTesting` extension method | `Csla.Configuration` |
| `BusinessRuleTester`, `BusinessRuleTestResult`, `AuthorizationRuleTester`, `AuthorizationRuleTestResult`, `RuleCache` | `Csla.Testing.Rules` |

## CslaTestHost

`CslaTestHost` builds and owns a configured service provider for a single test. It calls `AddCsla` and `AddCslaTesting` for you, so a test gets an `ApplicationContext` and a local data portal with one line of code.

```csharp
using Csla.Testing;
using Microsoft.Extensions.DependencyInjection;

using var host = CslaTestHost.Create(t => t
    .ConfigureCsla(o => o.DataPortal(dp => dp.AddServerSideDataPortal()))
    .AsUser("alice", "Admin")
    .ConfigureServices(s => s.AddSingleton<ICustomerDal, FakeCustomerDal>()));

var portal = host.GetDataPortal<CustomerEdit>();
var customer = await portal.FetchAsync(42);
```

`CslaTestHost.Create()` with no arguments gives you the default configuration: a local data portal, and an authenticated user named `TestUser` with no roles.

### Builder methods (CslaTestHostOptions)

| Method | Purpose |
| --- | --- |
| `ConfigureCsla(Action<CslaOptions>)` | Configures CSLA, exactly like the callback you pass to `AddCsla` |
| `ConfigureServices(Action<IServiceCollection>)` | Registers services for the test, such as fakes for your DAL |
| `AsUser(string name, params string[] roles)` | Runs as an authenticated user with the given roles |
| `AsUnauthenticated()` | Runs as an anonymous user |
| `AsPrincipal(IPrincipal)` | Runs as a specific principal |

Calling `ConfigureCsla` or `ConfigureServices` more than once adds to the earlier callbacks instead of replacing them. A shared base fixture can configure common services, and each test can add more.

Services you register with `ConfigureServices` are applied **after** CSLA's own registrations, so your registrations win.

### Members

| Member | Description |
| --- | --- |
| `Services` | The `IServiceProvider` the host owns |
| `ApplicationContext` | The CSLA `ApplicationContext` for the test |
| `GetDataPortal<T>()` | Resolves `IDataPortal<T>` |
| `GetChildDataPortal<T>()` | Resolves `IChildDataPortal<T>` |
| `CreateScope()` | Creates an `IServiceScope`, for behavior that varies by DI scope |
| `Dispose()` / `DisposeAsync()` | Disposes the service provider the host owns |

`CslaTestHost` implements both `IDisposable` and `IAsyncDisposable`. Always dispose it, with `using` or in your test cleanup.

### Example: Business Object Under Test

```csharp
using Csla;

[Serializable]
public class CustomerEdit : BusinessBase<CustomerEdit>
{
    public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
    public int Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public string Name
    {
        get => GetProperty(NameProperty);
        set => SetProperty(NameProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        BusinessRules.AddRule(new Csla.Rules.CommonRules.Required(NameProperty));
    }

    [Fetch]
    private async Task Fetch(int id, [Inject] ICustomerDal dal)
    {
        var data = await dal.GetAsync(id);
        using (BypassPropertyChecks)
        {
            Id = data.Id;
            Name = data.Name;
        }
        await BusinessRules.CheckRulesAsync();
    }
}

public interface ICustomerDal
{
    Task<CustomerDto> GetAsync(int id);
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class FakeCustomerDal : ICustomerDal
{
    public Task<CustomerDto> GetAsync(int id) =>
        Task.FromResult(new CustomerDto { Id = id, Name = "Test Customer" });
}
```

### MSTest Example

```csharp
using Csla.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CustomerEditTests
{
    [TestMethod]
    public async Task Fetch_LoadsCustomerFromDal()
    {
        await using var host = CslaTestHost.Create(t => t
            .ConfigureServices(s => s.AddSingleton<ICustomerDal, FakeCustomerDal>()));

        var portal = host.GetDataPortal<CustomerEdit>();
        var customer = await portal.FetchAsync(42);

        Assert.AreEqual(42, customer.Id);
        Assert.AreEqual("Test Customer", customer.Name);
        Assert.IsFalse(customer.IsNew);
    }

    [TestMethod]
    public async Task Name_Empty_MakesObjectInvalid()
    {
        await using var host = CslaTestHost.Create(t => t
            .ConfigureServices(s => s.AddSingleton<ICustomerDal, FakeCustomerDal>()));

        var customer = await host.GetDataPortal<CustomerEdit>().FetchAsync(42);
        customer.Name = string.Empty;

        Assert.IsFalse(customer.IsValid);
    }
}
```

To share setup across a test class, create the host in a `[TestInitialize]` method and dispose it in `[TestCleanup]`:

```csharp
[TestClass]
public class OrderTests
{
    private CslaTestHost _host = null!;

    [TestInitialize]
    public void Setup()
    {
        _host = CslaTestHost.Create(t => t
            .AsUser("alice", "Sales")
            .ConfigureServices(s => s.AddSingleton<IOrderDal, FakeOrderDal>()));
    }

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task Create_SetsDefaults()
    {
        var order = await _host.GetDataPortal<OrderEdit>().CreateAsync();
        Assert.IsTrue(order.IsNew);
    }
}
```

### xUnit Example

xUnit creates a new test class instance for every test, so create the host in the constructor and dispose it in `Dispose`:

```csharp
using Csla.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class CustomerEditTests : IDisposable
{
    private readonly CslaTestHost _host;

    public CustomerEditTests()
    {
        _host = CslaTestHost.Create(t => t
            .AsUser("alice", "Admin")
            .ConfigureServices(s => s.AddSingleton<ICustomerDal, FakeCustomerDal>()));
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Fetch_LoadsCustomerFromDal()
    {
        var customer = await _host.GetDataPortal<CustomerEdit>().FetchAsync(42);

        Assert.Equal("Test Customer", customer.Name);
    }

    [Fact]
    public void CurrentUser_IsAlice()
    {
        Assert.Equal("alice", _host.ApplicationContext.User.Identity?.Name);
        Assert.True(_host.ApplicationContext.User.IsInRole("Admin"));
    }
}
```

## AddCslaTesting

Use `AddCslaTesting` when the test builds its own `IServiceCollection`. It's an extension method in the `Csla.Configuration` namespace, next to `AddCsla`.

```csharp
using Csla;
using Csla.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCsla(o => o.DataPortal(dp => dp.AddServerSideDataPortal()));
services.AddCslaTesting(t => t.AsUser("alice", "Admin"));
services.AddSingleton<ICustomerDal, FakeCustomerDal>();

using var provider = services.BuildServiceProvider();
var applicationContext = provider.GetRequiredService<ApplicationContext>();
var portal = provider.GetRequiredService<IDataPortal<CustomerEdit>>();
```

**`AddCslaTesting` doesn't call `AddCsla`.** You must call `AddCsla` yourself. `CslaTestHost` calls both.

`AddCslaTesting` registers these services, all with `TryAdd` semantics, so a registration you've already made wins:

- `IHostEnvironment` as `CslaTestHostEnvironment` (content root is `Path.GetTempPath()`)
- Logging (`AddLogging`)
- The configured principal, as a `CslaTestPrincipal`
- `IContextManager` as `CslaTestContextManager`

The `CslaTestingOptions` callback has the same principal methods as the host: `AsUser`, `AsUnauthenticated`, and `AsPrincipal`. `AddCslaTesting()` with no callback uses the default principal.

`AddCslaTesting` doesn't register a data portal dashboard. The dashboard stays at the CSLA default (`NullDashboard`).

## The Test Principal

- The default principal is an **authenticated** user named `TestUser` with **no roles**. A test that depends on a role has to ask for it with `AsUser("name", "Role")`.
- The principal is registered as a service, and `CslaTestContextManager` reads it the first time the current user is requested.
- `CslaTestContextManager` derives from `ApplicationContextManagerAsyncLocal`, so its state is per async flow, not static. Tests that use the host can run in parallel.

## Rule Testers

The `Csla.Testing.Rules` namespace runs a **single rule in isolation**. You don't need a business object, a `CheckRules` call, or your own `ApplicationContext` setup.

### BusinessRuleTester

Given this rule:

```csharp
using Csla.Core;
using Csla.Rules;

public class NameRequired : BusinessRule
{
    public NameRequired(IPropertyInfo primaryProperty)
        : base(primaryProperty)
    {
        InputProperties.Add(primaryProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var value = context.GetInputValue<string>(PrimaryProperty!);
        if (string.IsNullOrWhiteSpace(value))
            context.AddErrorResult("Name required");
    }
}
```

Test it like this (MSTest):

```csharp
using Csla.Testing.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NameRequiredTests
{
    [TestMethod]
    public async Task EmptyName_AddsError()
    {
        using var result = await BusinessRuleTester
            .For(new NameRequired(CustomerEdit.NameProperty))
            .WithInput(CustomerEdit.NameProperty, "")
            .ExecuteAsync();

        Assert.IsTrue(result.HasErrors);
        Assert.AreEqual("Name required", result.ErrorMessages.Single());
    }

    [TestMethod]
    public void ValidName_Succeeds()
    {
        // Execute() keeps a test for a synchronous rule synchronous
        using var result = BusinessRuleTester
            .For(new NameRequired(CustomerEdit.NameProperty))
            .WithInput(CustomerEdit.NameProperty, "Rocky")
            .Execute();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.HasErrors);
    }
}
```

`ExecuteAsync()` runs both synchronous (`IBusinessRule`) and asynchronous (`IBusinessRuleAsync`) rules, so the test doesn't need to know which kind it has. `Execute()` runs only synchronous rules and throws `InvalidOperationException` for an async rule.

#### BusinessRuleTester builder methods

| Method | Purpose |
| --- | --- |
| `For(IBusinessRuleBase rule)` | Creates the tester for the rule (static) |
| `WithInput(property, value)` | Supplies an input property value. Has both `PropertyInfo<T>` and `IPropertyInfo` overloads |
| `OnTarget(object target)` | Supplies the target business object |
| `InMode(RuleContextModes mode)` | Sets the execution mode (default `RuleContextModes.PropertyChanged`) |
| `AsUser(name, roles)` / `AsUnauthenticated()` / `AsPrincipal(principal)` | Sets the principal the rule runs as |
| `ConfigureCsla(Action<CslaOptions>)` | Configures CSLA for the rule's `ApplicationContext` |
| `ConfigureServices(Action<IServiceCollection>)` | Registers services the rule resolves through `context.ApplicationContext` |
| `UsingApplicationContext(ApplicationContext)` | Uses an existing `ApplicationContext` instead of creating one |

When you call `OnTarget`, any property in the rule's `InputProperties` that you didn't supply with `WithInput` is read from the target, the same way the rules engine reads it. Values from `WithInput` win over values from the target. As in the rules engine, an **async** rule gets the target only if the rule sets `ProvideTargetWhenAsync = true`.

#### BusinessRuleTestResult members

| Member | Description |
| --- | --- |
| `Context` | The `IRuleContext` the rule ran with |
| `Results` | All `RuleResult` entries |
| `HasErrors`, `HasWarnings`, `HasInformation` | `true` if any result has that severity |
| `IsSuccess` | `true` if every result succeeded |
| `ErrorMessages`, `WarningMessages`, `InformationMessages` | Messages by severity |
| `OutputPropertyValues` | Values set with `context.AddOutValue` |
| `DirtyProperties` | Properties the rule marked dirty |
| `GetOutValue<T>(property)` | Gets an out value. Throws `KeyNotFoundException` if the rule didn't set one |
| `TryGetOutValue<T>(property, out value)` | Tries to get an out value |

A rule that adds no result of its own still gets one success result when its context completes, as it would in the rules engine.

The result owns the services created to run the rule, so `result.Context.ApplicationContext` still works after the rule has run. The result is `IDisposable`. Dispose it to release those services. The recorded results stay readable after disposal.

#### Testing a Calculation Rule (out values)

```csharp
using Csla.Core;
using Csla.Rules;

public class UpperCaseName : BusinessRule
{
    private readonly IPropertyInfo _displayNameProperty;

    public UpperCaseName(IPropertyInfo nameProperty, IPropertyInfo displayNameProperty)
        : base(nameProperty)
    {
        _displayNameProperty = displayNameProperty;
        InputProperties.Add(nameProperty);
        AffectedProperties.Add(displayNameProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var name = context.GetInputValue<string>(PrimaryProperty!);
        context.AddOutValue(_displayNameProperty, name?.ToUpperInvariant());
    }
}
```

```csharp
[TestMethod]
public async Task UpperCaseName_SetsDisplayName()
{
    using var result = await BusinessRuleTester
        .For(new UpperCaseName(PersonEdit.NameProperty, PersonEdit.DisplayNameProperty))
        .WithInput(PersonEdit.NameProperty, "rocky")
        .ExecuteAsync();

    Assert.AreEqual("ROCKY", result.GetOutValue<string>(PersonEdit.DisplayNameProperty));
}
```

#### Testing a Rule That Uses a Service

A rule that gets a service through `context.ApplicationContext` can get a fake through `ConfigureServices`:

```csharp
public class CreditLimitRule : BusinessRule
{
    public CreditLimitRule(IPropertyInfo creditLimitProperty)
        : base(creditLimitProperty)
    {
        InputProperties.Add(creditLimitProperty);
    }

    protected override void Execute(IRuleContext context)
    {
        var policy = context.ApplicationContext.GetRequiredService<ICreditPolicy>();
        var limit = context.GetInputValue<decimal>(PrimaryProperty!);
        if (limit > policy.MaximumLimit)
            context.AddErrorResult($"Credit limit can't exceed {policy.MaximumLimit:C}");
    }
}
```

```csharp
[TestMethod]
public async Task CreditLimit_AboveMaximum_AddsError()
{
    using var result = await BusinessRuleTester
        .For(new CreditLimitRule(CustomerEdit.CreditLimitProperty))
        .WithInput(CustomerEdit.CreditLimitProperty, 50_000m)
        .ConfigureServices(s => s.AddSingleton<ICreditPolicy>(new FakeCreditPolicy(10_000m)))
        .ExecuteAsync();

    Assert.IsTrue(result.HasErrors);
}
```

#### xUnit Rule Test

```csharp
using Csla.Testing.Rules;
using Xunit;

public class NameRequiredTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task MissingName_AddsError(string? name)
    {
        using var result = await BusinessRuleTester
            .For(new NameRequired(CustomerEdit.NameProperty))
            .WithInput(CustomerEdit.NameProperty, name)
            .ExecuteAsync();

        Assert.True(result.HasErrors);
    }
}
```

### AuthorizationRuleTester

`AuthorizationRuleTester` does the same for authorization rules and returns an `AuthorizationRuleTestResult`. Its main member is `HasPermission`.

```csharp
using Csla.Rules;
using Csla.Rules.CommonRules;
using Csla.Testing.Rules;

[TestMethod]
public async Task Admin_CanEditCustomer()
{
    using var result = await AuthorizationRuleTester
        .For(new IsInRole(AuthorizationActions.EditObject, "Admin"))
        .ForType<CustomerEdit>()
        .AsUser("rocky", "Admin")
        .ExecuteAsync();

    Assert.IsTrue(result.HasPermission);
}

[TestMethod]
public async Task NonAdmin_CannotEditCustomer()
{
    using var result = await AuthorizationRuleTester
        .For(new IsInRole(AuthorizationActions.EditObject, "Admin"))
        .ForType<CustomerEdit>()
        .AsUser("rocky", "User")
        .ExecuteAsync();

    Assert.IsFalse(result.HasPermission);
}
```

Builder methods: `For(IAuthorizationRuleBase rule)`, `OnTarget(object)`, `ForType(Type)` / `ForType<T>()`, `WithCriteria(params object?[]? criteria)`, `AsUser`, `AsUnauthenticated`, `AsPrincipal`, `ConfigureCsla`, `ConfigureServices`, and `UsingApplicationContext`.

- A **target type is required**. It comes from `ForType`, or from `OnTarget` if you don't call `ForType`. `ExecuteAsync` throws `InvalidOperationException` if neither was supplied.
- `ExecuteAsync(CancellationToken ct = default)` runs both `IAuthorizationRule` and `IAuthorizationRuleAsync` rules. `Execute()` runs only synchronous rules.
- `WithCriteria` tests rules that read `context.Criteria`. CSLA 10.2 also made the criteria-taking `AuthorizationContext` constructor public for this.
- Authorization rules read the principal from `ApplicationContext`, so set the user you need with `AsUser`.

## RuleCache

CSLA registers a type's business and authorization rules once, the first time the type is used, and caches them for the life of the process. In a test process, the first test's registration stays in place for every test that follows. `Csla.Testing.Rules.RuleCache` clears those caches, so the next use of a type registers its rules again.

```csharp
using Csla.Testing.Rules;

// MSTest
[TestInitialize]
public void ResetRules() => RuleCache.Clear();

// xUnit: in the test class constructor
public MyRuleTests()
{
    RuleCache.Clear();
}
```

| Method | Clears |
| --- | --- |
| `RuleCache.Clear()` | Business and authorization rules for every type |
| `RuleCache.Clear(Type type)` | Business and authorization rules for one type |
| `RuleCache.ClearBusinessRules()` / `ClearBusinessRules(Type)` | Business rules only |
| `RuleCache.ClearAuthorizationRules()` / `ClearAuthorizationRules(Type)` | Authorization rules only |

Clear the cache when rule registration varies from test to test, for example with a different rule set or configuration, or when a mutation-testing tool reuses a test process.

> ⚠️ The rule caches are process-wide static state. Clear them only when no other test is using them. Don't call `RuleCache.Clear()` while tests that use CSLA business types run in parallel. Use `Clear(Type)` for the type under test, or run those tests serially.

> ℹ️ Object authorization rules (`[ObjectAuthorizationRules]`) aren't registered when an object is created. They're registered on the first permission check, such as `BusinessRules.HasPermission`.

## Best Practices

1. **Use one host per test.** Create a `CslaTestHost` per test, or per test class instance in xUnit, and always dispose it.
2. **Fake the DAL with `ConfigureServices`.** Register fakes for the services your `[Inject]` parameters need. Your registrations override CSLA's.
3. **Say which user and roles you need.** The default `TestUser` has no roles. Call `AsUser(name, roles)` when the behavior depends on a role.
4. **Test rules in isolation with `BusinessRuleTester`,** and test the whole object (rule wiring, `IsValid`, `IsSavable`) through the data portal from a `CslaTestHost`.
5. **Prefer `ExecuteAsync`.** It works for both sync and async rules.
6. **Dispose rule test results** with `using`.
7. **Use `RuleCache` only when registration varies** between tests, and not while tests run in parallel.

## Related Documentation

- [BusinessRulesUnitTesting.md](BusinessRulesUnitTesting.md) - Testing rules with a mocking framework (Rocks), which works on any CSLA version
- [BusinessRules.md](BusinessRules.md) - Business rule implementation overview
- [BusinessRulesAuthorization.md](BusinessRulesAuthorization.md) - Authorization rules
- [DataPortalGuide.md](DataPortalGuide.md) - Data portal operations
