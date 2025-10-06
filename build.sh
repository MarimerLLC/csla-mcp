#!/usr/bin/env bash

# Build the embeddings generator CLI tool
echo "Building embeddings generator..."
dotnet build csla-embeddings-generator/csla-embeddings-generator.csproj -c Release

# Run the embeddings generator to create embeddings.json
echo "Generating embeddings..."
dotnet run --project csla-embeddings-generator/csla-embeddings-generator.csproj --configuration Release -- --examples-path ./csla-examples --output ./embeddings.json

# Build the Docker container with the embeddings.json file
echo "Building Docker container..."
docker build -t csla-mcp-server:latest -f csla-mcp-server/Dockerfile .
