# Quick Azure OpenAI Setup Guide

## Step-by-Step Deployment Instructions

### 1. Access Azure OpenAI Studio
- Go to [https://oai.azure.com/](https://oai.azure.com/)
- Sign in with your Azure credentials
- Select your Azure OpenAI resource

### 2. Create the Embedding Model Deployment

1. **Navigate to Deployments**:
   - Click "Deployments" in the left sidebar
   - Click "Create new deployment" button

2. **Configure the Deployment**:
   - **Model**: Select `text-embedding-3-small` (recommended)
   - **Model version**: Leave as "Auto-update to default"
   - **Deployment name**: Enter `text-embedding-3-small` (must match exactly)
   - **Content filter**: Use default
   - **Rate limit**: Choose appropriate limit (e.g., 240K tokens per minute)

3. **Deploy**:
   - Click "Create" button
   - Wait for deployment to complete (usually 2-5 minutes)
   - Status should show "Succeeded"

### 3. Get Your Configuration Values

1. **Endpoint URL**:
   - Go to your Azure OpenAI resource in the Azure Portal
   - Copy the "Endpoint" value (e.g., `https://your-resource.openai.azure.com/`)

2. **API Key**:
   - In the Azure Portal, go to "Keys and Endpoint"
   - Copy either "Key 1" or "Key 2"

### 4. Set Environment Variables

**PowerShell:**
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-actual-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-actual-api-key-here"
$env:AZURE_OPENAI_EMBEDDING_MODEL = "text-embedding-3-small"
$env:AZURE_OPENAI_API_VERSION = "2024-02-01"  # Optional, but recommended
```

### 5. Test the Configuration

Run the server and look for these success messages:
```
[Startup] Using Azure OpenAI endpoint: https://your-resource.openai.azure.com/
[Startup] Using embedding model deployment: text-embedding-3-small
[Startup] Using API version: 2024-02-01
[VectorStore] Initialized with API version: 2024-02-01
[VectorStore] Testing Azure OpenAI connectivity...
[VectorStore] Connectivity test passed - semantic search available.
```

## Common Issues and Solutions

### Issue: "DeploymentNotFound" Error
**Solution**: The deployment name doesn't match OR wrong API version
- Check your deployment name in Azure OpenAI Studio
- Ensure `AZURE_OPENAI_EMBEDDING_MODEL` matches exactly
- Try API version `2024-02-01` for text-embedding-3-small
- Try API version `2023-07-01-preview` for text-embedding-ada-002

### Issue: "API version not supported" Error
**Solution**: Wrong API version for your model
- For `text-embedding-3-small`: Use `2024-02-01` or `2023-12-01-preview`
- For `text-embedding-ada-002`: Use `2023-07-01-preview` or `2023-05-15`

### Issue: "Authentication Failed"
**Solution**: Wrong API key
- Verify the API key in Azure Portal
- Make sure you copied the full key without extra spaces

### Issue: "Resource Not Found"
**Solution**: Wrong endpoint
- Verify the endpoint URL in Azure Portal
- Should end with `.openai.azure.com/`

## API Version Guide

Different models require different API versions:

### For text-embedding-3-small (recommended):
```powershell
$env:AZURE_OPENAI_API_VERSION = "2024-02-01"
```

### For text-embedding-ada-002 (legacy):
```powershell
$env:AZURE_OPENAI_API_VERSION = "2023-07-01-preview"
```

### If unsure, try the default:
```powershell
# Don't set it - defaults to 2024-02-01