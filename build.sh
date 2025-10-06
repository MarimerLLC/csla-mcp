#!/usr/bin/env bash

# Build the embeddings generator CLI tool
echo "Building embeddings generator..."
dotnet build csla-embeddings-generator/csla-embeddings-generator.csproj -c Release

# Run the embeddings generator to create embeddings.json
echo "Generating embeddings..."
dotnet run --project csla-embeddings-generator/csla-embeddings-generator.csproj --configuration Release -- --examples-path ./csla-examples --output ./embeddings.json

# If embeddings.json doesn't exist (e.g., missing Azure credentials), create an empty array JSON file
# This allows the Docker build to succeed, and the server will fall back to runtime generation
if [ ! -f ./embeddings.json ]; then
  echo "Warning: embeddings.json not created, creating empty file for Docker build"
  echo "[]" > ./embeddings.json
fi

# Build the Docker container with the embeddings.json file
echo "Building Docker container..."
docker build -t csla-mcp-server:latest -f csla-mcp-server/Dockerfile .
