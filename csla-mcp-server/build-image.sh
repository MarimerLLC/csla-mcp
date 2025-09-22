#!/usr/bin/env bash
set -euo pipefail

TAG=${1:-latest}
BUILD_CTX=${2:-.}

echo "Building Docker image csla-mcp-server:${TAG} from context ${BUILD_CTX}"

docker build -t csla-mcp-server:${TAG} "${BUILD_CTX}"

echo "Built image: csla-mcp-server:${TAG}"
