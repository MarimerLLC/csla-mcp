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

# Build the Docker container with the embeddings.json file and version embedded in assembly
echo "Building Docker container..."
docker build -t csla-mcp-server:latest \
  --build-arg VERSION=$VERSION \
  --build-arg SKIP_EMBEDDINGS=$SKIP_EMBEDDINGS \
  --build-arg AZURE_OPENAI_ENDPOINT=$AZURE_OPENAI_ENDPOINT \
  --build-arg AZURE_OPENAI_API_KEY=$AZURE_OPENAI_API_KEY \
  -f csla-mcp-server/Dockerfile .
