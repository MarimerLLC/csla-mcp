# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage builds the csla-mcp-server project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
WORKDIR /src
COPY ["csla-mcp-server/csla-mcp-server.csproj", "csla-mcp-server/"]
RUN dotnet restore "./csla-mcp-server/csla-mcp-server.csproj"
COPY . .
WORKDIR "/src/csla-mcp-server"
RUN dotnet build "./csla-mcp-server.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:InformationalVersion=$VERSION


# This stage publishes the service project
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
RUN dotnet publish "./csla-mcp-server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:InformationalVersion=$VERSION


# This stage generates the embeddings file
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS embeddings-generator
ARG SKIP_EMBEDDINGS=true
ARG AZURE_OPENAI_ENDPOINT
ARG AZURE_OPENAI_API_KEY
ENV AZURE_OPENAI_ENDPOINT=$AZURE_OPENAI_ENDPOINT
ENV AZURE_OPENAI_API_KEY=$AZURE_OPENAI_API_KEY
WORKDIR /src
COPY csla-embeddings-generator/ ./csla-embeddings-generator/
COPY csla-examples/ ./csla-examples/
RUN if [ "$SKIP_EMBEDDINGS" = "false" ]; then \
      dotnet restore "./csla-embeddings-generator/csla-embeddings-generator.csproj" && \
      dotnet build "./csla-embeddings-generator/csla-embeddings-generator.csproj" -c Release -o /app/embeddings-build && \
      dotnet run --project "./csla-embeddings-generator/csla-embeddings-generator.csproj" --configuration Release -- --examples-path /src/csla-examples --output /embeddings.json; \
    fi
RUN if [ ! -f /embeddings.json ]; then echo "[]" > /embeddings.json; fi


# This stage is used in production or when running from VS in regular mode
FROM base AS final
WORKDIR /
COPY --from=build /src/csla-examples /csla-examples
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=embeddings-generator /embeddings.json ./embeddings.json
ENTRYPOINT ["dotnet", "csla-mcp-server.dll"]
