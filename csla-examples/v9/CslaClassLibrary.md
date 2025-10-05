# CSLA .NET Class Library

Most CSLA .NET applications use a class library project to hold the business classes. This project typically references the CSLA .NET framework and any other necessary libraries.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Csla" Version="9.1.0" />
  </ItemGroup>
</Project>
```

CSLA 9 does not fully support nullable reference types in its API. You can enable nullable reference types in your project, but you may need to use `#nullable disable` in some files to avoid compiler warnings.

CSLA 10 will fully support nullable reference types.
