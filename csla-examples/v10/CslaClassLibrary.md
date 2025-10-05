# CSLA .NET Class Library

Most CSLA .NET applications use a class library project to hold the business classes. This project typically references the CSLA .NET framework and any other necessary libraries.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Csla" Version="10.0.0" />
  </ItemGroup>
</Project>
```

CSLA 10 supports nullable reference types in its API.
