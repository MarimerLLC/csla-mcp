param(
    [Parameter(Mandatory=$true)]
    [string]$DockerHubUser,
    [string]$Tag = "latest"
)

# Ensure image exists locally
$localImage = "csla-mcp-server:$Tag"
if (-not (docker image inspect $localImage -ErrorAction SilentlyContinue)) {
    Write-Error "Local image '$localImage' not found. Build it first with .\build-image.ps1 or .\build-image.sh"
    exit 1
}

$remoteImage = "$DockerHubUser/csla-mcp-server:$Tag"

Write-Host "Tagging $localImage -> $remoteImage"
docker tag $localImage $remoteImage

Write-Host "Pushing $remoteImage to Docker Hub (ensure you've run 'docker login')"
docker push $remoteImage

if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker push failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Pushed image: $remoteImage"
