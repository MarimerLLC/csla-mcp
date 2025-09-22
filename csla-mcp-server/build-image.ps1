param(
    [string]$Tag = "latest",
    [string]$BuildContext = "."
)

Write-Host "Building Docker image csla-mcp-server:$Tag from context $BuildContext"

docker build -t csla-mcp-server:$Tag $BuildContext

if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Built image: csla-mcp-server:$Tag"
