# CSLA .NET Class Library

A CSLA .NET class library project contains business domain classes (stereotypes like EditableRoot, ReadOnlyRoot, etc.). This is the core business logic layer of the application.

## Project Structure

The class library project is typically separate from the UI/API layer and the data access layer, though small applications may combine them.

**Typical solution structure**:

* `MyApp.BusinessLibrary` - Business classes (this project)
* `MyApp.DataAccess` - DAL interfaces and implementations
* `MyApp.Web` or `MyApp.UI` - Application interface layer

## Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Csla" Version="10.0.0" />
    <PackageReference Include="Csla.Generator.AutoImplementProperties.CSharp" Version="10.0.0" />
  </ItemGroup>
</Project>
```

### Adding Packages via CLI

```bash
dotnet add package Csla
dotnet add package Csla.Generator.AutoImplementProperties.CSharp
```

## Configuration Details

### Target Framework

CSLA 10 supports:

* .NET Framework 4.6.2 (`net462`)
* .NET Framework 4.7.2 (`net472`)
* .NET Framework 4.8 (`net48`)
* .NET 8.0 (`net8.0`)
* .NET 9.0 (`net9.0`)
* .NET 10.0 (`net10.0`)

### Nullable Reference Types

CSLA 10 supports nullable reference types in its API. Enable with `<Nullable>enable</Nullable>`.

### C# Language Version

CSLA 10 uses features from C# version 14. Set `<LangVersion>14</LangVersion>` or higher.

## Package Purposes

### Csla Package

The core CSLA framework providing:

* Base classes for stereotypes (`BusinessBase<T>`, `ReadOnlyBase<T>`, etc.)
* Data portal infrastructure
* Business rules engine
* Authorization framework
* N-level undo/serialization support

### Csla.Generator.AutoImplementProperties.CSharp Package

Code generator that implements properties marked with `partial` keyword when the class has `[CslaImplementProperties]` attribute. This eliminates boilerplate property code.

## Common Using Statements

Typical imports for CSLA business classes:

```csharp
using Csla;
using System.ComponentModel.DataAnnotations;
```

For business rules:

```csharp
using Csla.Rules;
using Csla.Rules.CommonRules;
```

## Namespace Conventions

**Recommended pattern**: `CompanyName.ProjectName.BusinessLibrary` or `CompanyName.ProjectName.Business`

Example:

```csharp
namespace MyCompany.MyApp.Business
{
    [CslaImplementProperties]
    public partial class Customer : BusinessBase<Customer>
    {
        // Business class implementation
    }
}
```

## Dependency Injection Setup

Business classes use constructor injection via the data portal. DAL interfaces are injected into data portal operation methods using the `[Inject]` attribute.

**Note**: DI container registration typically happens in the UI/API project's startup, not in the business library itself. The business library defines what needs to be injected.
