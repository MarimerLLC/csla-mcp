# CSLA .NET Class Library

A CSLA .NET class library project contains business domain classes (stereotypes like EditableRoot, ReadOnlyRoot, etc.). This is the core business logic layer of the application.

**Related Documents**:
- `../SolutionArchitecture.md` - Comprehensive guide to solution structure and layered architecture
- `../Data-Access.md` - Data access layer implementation patterns
- `../Glossary.md` - CSLA terminology and concepts

## Project Structure

The class library project is typically separate from the UI/API layer and the data access layer, though small applications may combine them. For a complete guide to solution architecture, see `../SolutionArchitecture.md`.

**Recommended solution structure**:

```
MySolution/
├── MyApp.Business/              # Business Layer (this project)
│   ├── PersonEdit.cs            # Editable business objects
│   ├── PersonInfo.cs            # Read-only info objects
│   └── Rules/                   # Custom business rules
├── MyApp.Dal/                   # Data Access Abstraction
│   ├── IPersonDal.cs            # DAL interfaces
│   └── PersonData.cs            # DTOs
├── MyApp.Dal.EfCore/            # Data Access Concrete
│   ├── PersonDal.cs             # EF Core implementation
│   └── MyAppDbContext.cs
└── MyApp.Web/                   # Presentation Layer
    ├── Pages/                   # Interface (Razor pages)
    └── ViewModels/              # Interface Control
```

**Key architectural principles**:
- The Business project references ONLY the Dal abstraction project (interfaces/DTOs)
- The Business project NEVER references concrete DAL implementations
- All business logic (validation, calculation, authorization) belongs in the Business project

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
    <PackageReference Include="Csla" Version="11.0.0" />
  </ItemGroup>
</Project>
```

The `Csla` package is the only package a business library needs. In CSLA 11 the CSLA source generators (`[CslaImplementProperties]` property implementation, data portal operation dispatch, and `[DataPortalExtensions]`) and the CSLA analyzers are bundled inside the `Csla` package and run automatically in any C# project that references it.

> **Important:** Do NOT add a `PackageReference` to `Csla.Generator.AutoImplementProperties.CSharp`. That package is retired in CSLA 11. If a project still references it (or has a matching `PackageVersion` entry in `Directory.Packages.props`), the build fails with error `CSLABUILD001` until the reference is removed.

To multi-target, for example to support both current .NET versions:

```xml
<TargetFrameworks>net10.0;net11.0</TargetFrameworks>
```

### Adding Packages via CLI

```bash
dotnet add package Csla
```

## Configuration Details

### Target Framework

CSLA 11 supports:

* .NET Framework 4.6.2 (`net462`)
* .NET Framework 4.7.2 (`net472`)
* .NET Framework 4.8 (`net48`)
* .NET Standard 2.0 (`netstandard2.0`)
* .NET 10.0 (`net10.0`)
* .NET 11.0 (`net11.0`)

CSLA 11 no longer targets .NET 8 or .NET 9. Projects that used `net8.0` or `net9.0` must move to `net10.0` or later. Some types, such as `BusinessDocumentBase<T, C>`, are only available on the .NET (`net10.0`/`net11.0`) targets, not on .NET Framework or `netstandard2.0`.

### Nullable Reference Types

CSLA 11 supports nullable reference types in its API. Enable with `<Nullable>enable</Nullable>`.

### C# Language Version

CSLA code generation relies on partial properties, which require C# 13 or later. The .NET 10 SDK defaults to C# 14 for `net10.0` projects. If you target .NET Framework or `netstandard2.0`, set `<LangVersion>14</LangVersion>` (or `latest`) explicitly.

## Package Purposes

### Csla Package

The core CSLA framework providing:

* Base classes for stereotypes (`BusinessBase<T>`, `ReadOnlyBase<T>`, etc.)
* Data portal infrastructure
* Business rules engine
* Authorization framework
* N-level undo/serialization support
* Bundled source generators:
  * Implements `partial` properties when the class has the `[CslaImplementProperties]` attribute (see `Properties.md`)
  * Generates direct (non-reflection) calls to data portal operation methods for every `partial` class that has operation methods
  * Generates strongly typed data portal extension methods for classes marked with `[DataPortalExtensions]` (see `DataPortalExtensions.md`)
* Bundled analyzers, including `CSLA0024` (classes with data portal operation methods should be `partial`) and `CSLA0014` (operation method without an operation attribute)

### Csla.Generator.AutoSerialization.CSharp Package (optional)

Still a separate package in CSLA 11. Add it only if you use the `[AutoSerializable]` attribute to make your own POCO types serializable by `MobileFormatter` (see `Serialization.md`). Business classes derived from CSLA base types do not need it.

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
