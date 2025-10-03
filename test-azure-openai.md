# Test Your Azure OpenAI Configuration

Use this configuration for testing. The server now handles API versions automatically and provides better error messages.

## Test Configuration (PowerShell)

```powershell
# Set your actual values here
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key-here"
$env:AZURE_OPENAI_EMBEDDING_MODEL = "text-embedding-3-small"  # Must match your deployment name

# Optional - for logging purposes only
$env:AZURE_OPENAI_API_VERSION = "2024-02-01"

# Run the server
dotnet run --project csla-mcp-server
```

## What to Look For

### Success Messages:
```
[Startup] Using Azure OpenAI endpoint: https://your-resource.openai.azure.com/
[Startup] Using embedding model deployment: text-embedding-3-small
[Startup] Using API version: 2024-02-01
[VectorStore] Initialized with API version: 2024-02-01 (using default client options)
[VectorStore] Testing Azure OpenAI connectivity...
[VectorStore] Attempting to generate embedding using model: text-embedding-3-small
[VectorStore] Successfully generated embedding with 1536 dimensions
[VectorStore] Connectivity test passed - semantic search available.
```

### Common Error Messages and Solutions:

#### DeploymentNotFound (404):
```
[VectorStore] Error: Azure OpenAI deployment 'text-embedding-3-small' not found (404).
```
**Solution**: Create the deployment in Azure OpenAI Studio with the exact name `text-embedding-3-small`

#### Bad Request (400):
```
[VectorStore] Bad Request (400): The API version is not supported for this operation.
```
**Solution**: Your model might be too old. Try redeploying it in Azure OpenAI Studio.

#### Authentication (401):
```
[VectorStore] Azure OpenAI API error (Status: 401): Authentication failed.
```
**Solution**: Check your `AZURE_OPENAI_API_KEY` value.

#### Fallback Mode (No Azure OpenAI):
```
[Startup] Azure OpenAI configuration not found - running in keyword-only search mode.
```
**Result**: Server runs but only provides keyword search, no semantic search.

## Quick Test

After setting your environment variables, run:
```powershell
dotnet run --project csla-mcp-server
```

The server should start and show either:
1. Successful Azure OpenAI connection with semantic search enabled
2. Fallback to keyword-only mode with clear instructions
3. Specific error messages telling you exactly what to fix