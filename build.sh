#!/usr/bin/env bash

# Generate version string: 1.0.0+YYYYMMDD.githash
BUILD_DATE=$(date -u +%Y%m%d)
GIT_HASH=$(git rev-parse --short HEAD 2>/dev/null || echo "nogit")
VERSION="1.0.0+${BUILD_DATE}.${GIT_HASH}"
echo "Building with version: $VERSION"

# Build the embeddings generator CLI tool
echo "Building embeddings generator..."
dotnet build csla-embeddings-generator/csla-embeddings-generator.csproj -c Release

# Run the embeddings generator to create embeddings.json
echo "Generating embeddings..."
dotnet run --project csla-embeddings-generator/csla-embeddings-generator.csproj --configuration Release -- --examples-path ./csla-examples --output ./embeddings.json

# If embeddings.json doesn't exist (e.g., missing Azure credentials), create an empty array JSON file
# This allows the Docker build to succeed, but semantic search will be disabled at runtime
if [ ! -f ./embeddings.json ]; then
  echo "Warning: embeddings.json not created, creating empty file for Docker build"
  echo "[]" > ./embeddings.json
fi

# Build the Docker container with the embeddings.json file and version
echo "Building Docker container..."
docker build -t csla-mcp-server:latest -t csla-mcp-server:$VERSION \
  --build-arg VERSION=$VERSION \
  -f csla-mcp-server/Dockerfile .
