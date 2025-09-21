# Multi-stage Dockerfile for csla-mcp-server
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY "csla-mcp-server.csproj" ./
RUN dotnet restore "csla-mcp-server.csproj"

# copy everything else and build
COPY . ./
RUN dotnet publish "csla-mcp-server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "csla-mcp-server.dll"]
