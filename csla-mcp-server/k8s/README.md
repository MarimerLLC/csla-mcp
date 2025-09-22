csla-mcp-server Kubernetes manifests

Files in this folder:

- `csla-mcp-deployment.yaml` - Deployment (2 replicas) and Service (ClusterIP) to run the `csla-mcp-server` Docker image.

How to use

1. By default the manifest expects an image named `csla-mcp-server:latest` (this matches the local build scripts below).

   To build locally, run from the `csla-mcp-server` folder:

   Windows PowerShell:

   ```pwsh
   .\build-image.ps1 -Tag "latest" -BuildContext .
   ```

   POSIX shell:

   ```bash
   ./build-image.sh latest .
   ```

   Or set the image to your registry tag, for example:

   ```yaml
   image: myregistry.example.com/marimerllc/csla-mcp-server:1.0.0
   ```

   Pushing to Docker Hub

   You can push the locally-built `csla-mcp-server` image to Docker Hub with the helper scripts in the project root of `csla-mcp-server`.

   PowerShell:

   ```pwsh
   #.\push-image.ps1 -DockerHubUser "your-dockerhub-username" -Tag "latest"
   .
   ```

   POSIX shell:

   ```bash
   ./push-image.sh your-dockerhub-username latest
   ```

   Make sure you've logged in with `docker login` before pushing.

2. Apply to your cluster:

   ```pwsh
   kubectl apply -f ./k8s/csla-mcp-deployment.yaml
   ```

3. To expose externally on cloud clusters, edit the Service `type` to `LoadBalancer` or create an Ingress resource.

Notes

- The container listens on port 80 (see `Dockerfile`) and probes are configured to use `/`.
- Change replica count, resource requests/limits, and probes as appropriate for your environment.
