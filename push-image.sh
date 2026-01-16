#!/usr/bin/env bash
set -euo pipefail

DOCKERHUB_USER=${1:?"Usage: $0 <dockerhub-username> [tag]"}
TAG=${2:-latest}

LOCAL_IMAGE="csla-mcp-server:${TAG}"
REMOTE_IMAGE="${DOCKERHUB_USER}/csla-mcp-server:${TAG}"

if ! docker image inspect "${LOCAL_IMAGE}" >/dev/null 2>&1; then
  echo "Local image '${LOCAL_IMAGE}' not found. Build it first with ./build-image.sh"
  exit 1
fi

echo "Tagging ${LOCAL_IMAGE} -> ${REMOTE_IMAGE}"
docker tag "${LOCAL_IMAGE}" "${REMOTE_IMAGE}"

echo "Pushing ${REMOTE_IMAGE} to Docker Hub (ensure you've run 'docker login')"
docker push "${REMOTE_IMAGE}"

echo "Pushed image: ${REMOTE_IMAGE}"

# Brief delay to allow Docker Hub to propagate the image
echo "Waiting for Docker Hub propagation..."
sleep 5

echo "Triggering Azure Web App to pull new image..."
az webapp config container set \
  --name csla-mcp-server \
  --resource-group csla-mcp \
  --container-image-name "${REMOTE_IMAGE}" \
  --output none

echo "Restarting Azure Web App to ensure fresh pull..."
az webapp restart \
  --name csla-mcp-server \
  --resource-group csla-mcp \
  --output none

echo "Deployment triggered successfully"
