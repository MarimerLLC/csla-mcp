# CSLA .NET Class Library

Most CSLA .NET applications use a class library project to hold the business classes. This project typically references the CSLA .NET framework and any other necessary libraries.

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
    <PackageReference Include="Csla.Generator.AutoImplementProperties.CSharp" Version="10.0.0">
      <PrivateAssets>analyzers</PrivateAssets>
      <IncludeAssets>all</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

CSLA 10 supports `TargetFramework` of .NET Framework 4.8, net8.0, net9.0, and net10.0.

CSLA 10 supports nullable reference types in its API, so `Nullable` is enabled.

CSLA 10 uses features from C# version 14, so the `LangVersion` is set to 14 (or higher).

The `Csla` package is referenced to enable the use of CSLA .NET features such as the rules engine, data portal, and other capabilities.

The `AutoImplementProperties` code generator package is referenced to enable standard code generation for CSLA properties.
