#!/usr/bin/env bash

# Parse command line arguments
SKIP_EMBEDDINGS=false
while [[ $# -gt 0 ]]; do
  case $1 in
    --skip-embeddings|-s)
      SKIP_EMBEDDINGS=true
      shift
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: ./build.sh [--skip-embeddings|-s]"
      exit 1
      ;;
  esac
done

# Generate version string: 1.0.0+YYYYMMDD.githash
BUILD_DATE=$(date -u +%Y%m%d)
GIT_HASH=$(git rev-parse --short HEAD 2>/dev/null || echo "nogit")
VERSION="1.0.0+${BUILD_DATE}.${GIT_HASH}"
echo "Building with version: $VERSION"

if [ "$SKIP_EMBEDDINGS" = false ]; then
  # Build the embeddings generator CLI tool
  echo "Building embeddings generator..."
  dotnet build csla-embeddings-generator/csla-embeddings-generator.csproj -c Release

  # Run the embeddings generator to create embeddings.json
  echo "Generating embeddings..."
  dotnet run --project csla-embeddings-generator/csla-embeddings-generator.csproj --configuration Release -- --examples-path ./csla-examples --output ./embeddings.json
else
  echo "Skipping embeddings generation (--skip-embeddings)"
fi

# If embeddings.json doesn't exist (e.g., missing Azure credentials), create an empty array JSON file
# This allows the Docker build to succeed, but semantic search will be disabled at runtime
if [ ! -f ./embeddings.json ]; then
  echo "Warning: embeddings.json not created, creating empty file for Docker build"
  echo "[]" > ./embeddings.json
fi

# Build the Docker container with the embeddings.json file and version embedded in assembly
echo "Building Docker container..."
docker build -t csla-mcp-server:latest \
  --build-arg VERSION=$VERSION \
  -f csla-mcp-server/Dockerfile .
