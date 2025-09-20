# CSLA MCP Server Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["csla-mcp.Server/csla-mcp.Server.csproj", "csla-mcp.Server/"]
COPY ["csla-mcp.ServiceDefaults/csla-mcp.ServiceDefaults.csproj", "csla-mcp.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "csla-mcp.Server/csla-mcp.Server.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/csla-mcp.Server"
RUN dotnet build "csla-mcp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "csla-mcp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy code examples
COPY --from=publish /app/publish/CodeExamples ./CodeExamples

ENTRYPOINT ["dotnet", "csla-mcp.Server.dll"]