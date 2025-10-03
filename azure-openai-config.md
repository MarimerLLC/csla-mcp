# Azure OpenAI Configuration

The CSLA MCP Server now uses Azure OpenAI for vector embeddings instead of Ollama.

## Required Environment Variables

Before running the server, you need to set the following environment variables:

### Required
- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI service endpoint (e.g., `https://your-resource.openai.azure.com/`)
- `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key

### Optional
- `AZURE_OPENAI_EMBEDDING_MODEL`: The embedding model deployment name to use (default: `text-embedding-3-small`)
- `AZURE_OPENAI_API_VERSION`: The API version to use (default: `2024-02-01`)

## Important: Model Deployment Setup

**CRITICAL**: You must deploy an embedding model in your Azure OpenAI resource before the server will work. The deployment name must exactly match the value you set for `AZURE_OPENAI_EMBEDDING_MODEL`.

### Steps to Deploy an Embedding Model:

1. **Go to Azure OpenAI Studio**: Visit [https://oai.azure.com/](https://oai.azure.com/)
2. **Navigate to Deployments**: Click on "Deployments" in the left sidebar
3. **Create New Deployment**: Click "Create new deployment"
4. **Select Model**: Choose an embedding model (recommended: `text-embedding-3-small`)
5. **Set Deployment Name**: **Important** - The deployment name must match your environment variable
   - If using default: deployment name should be `text-embedding-3-small`
   - If using custom model: set `AZURE_OPENAI_EMBEDDING_MODEL` to match your deployment name
6. **Deploy**: Complete the deployment process

### Common Deployment Names:
- `text-embedding-3-small` (recommended for most use cases)
- `text-embedding-3-large` (higher quality, more expensive)
- `text-embedding-ada-002` (legacy model)

## API Version Information

The Azure OpenAI client automatically uses the appropriate API version for your requests. You can optionally specify `AZURE_OPENAI_API_VERSION` for logging and troubleshooting purposes, but the client will use compatible API versions automatically.

### Current Behavior:
- The client uses the latest compatible API version automatically
- Setting `AZURE_OPENAI_API_VERSION` helps with logging and troubleshooting
- Different models may require different minimum API versions

### Model Compatibility:
- **text-embedding-3-small/large**: Requires newer API versions (handled automatically)
- **text-embedding-ada-002**: Works with older API versions (handled automatically)

### If You Experience API Version Issues:
The error messages will guide you to check:
1. Your model deployment exists
2. Your model is compatible with current API versions
3. Try redeploying your model if it's very old

## Example Configuration

### PowerShell (Windows)
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key-here"
$env:AZURE_OPENAI_EMBEDDING_MODEL = "text-embedding-3-small"  # Must match your deployment name
$env:AZURE_OPENAI_API_VERSION = "2024-02-01"  # Optional, defaults to 2024-02-01
```

### Bash (Linux/macOS)
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
export AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-small"  # Must match your deployment name
export AZURE_OPENAI_API_VERSION="2024-02-01"  # Optional, defaults to 2024-02-01
```

### Docker
```bash
docker run -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" \
           -e AZURE_OPENAI_API_KEY="your-api-key-here" \
           -e AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-small" \
           -e AZURE_OPENAI_API_VERSION="2024-02-01" \
           --rm -p 8080:80 csla-mcp-server:latest
```

## Troubleshooting

### "The API deployment for this resource does not exist" Error

This error occurs when:
1. **No deployment exists**: You haven't deployed the embedding model in Azure OpenAI Studio
2. **Wrong deployment name**: The `AZURE_OPENAI_EMBEDDING_MODEL` doesn't match your actual deployment name
3. **Recent deployment**: If you just created the deployment, wait 5-10 minutes for it to become available
4. **Wrong API version**: The API version doesn't support your model or deployment

### Steps to Fix:
1. Check your Azure OpenAI Studio deployments
2. Verify the deployment name exactly matches your environment variable
3. Ensure the deployment status is "Succeeded"
4. Try a different API version if using newer models
5. Wait a few minutes if you just created the deployment

### API Version Issues:
- **text-embedding-3-small/large**: Requires `2024-02-01` or `2023-12-01-preview`
- **text-embedding-ada-002**: Works with `2023-07-01-preview` or newer
- **Unsupported version**: Try `2024-02-01` (default) or check Azure OpenAI documentation

### Other Common Issues:
- **401 Unauthorized**: Check your `AZURE_OPENAI_API_KEY`
- **403 Forbidden**: Check your Azure OpenAI resource permissions
- **404 Not Found**: Check your `AZURE_OPENAI_ENDPOINT` URL
- **400 Bad Request**: Often indicates wrong API version for your model

## Supported Embedding Models

Azure OpenAI supports various embedding models. Common options include:
- `text-embedding-3-small` (default) - 1536 dimensions, cost-effective, requires API version 2023-12-01-preview or later
- `text-embedding-3-large` - 3072 dimensions, higher quality, requires API version 2023-12-01-preview or later
- `text-embedding-ada-002` - 1536 dimensions, legacy model, works with older API versions

## Migration from Ollama

The server will now use Azure OpenAI instead of the local Ollama service. The semantic search functionality remains the same, but now leverages Azure's cloud-based embedding generation for potentially better quality results.

## Error Handling

If the environment variables are not set properly, the server will exit with specific error codes:
- Error code 7: `AZURE_OPENAI_ENDPOINT` not set
- Error code 8: `AZURE_OPENAI_API_KEY` not set