# Testing Guide for Vector Store Persistence

## Overview

This guide explains how to test the new vector store persistence feature that was implemented to address the issue of storing vector results in a database.

## Prerequisites for Testing

1. **.NET 10.0 SDK** - The solution targets .NET 10.0
2. **Azure OpenAI Account** with:
   - Valid endpoint URL
   - API key
   - Deployed embedding model (e.g., `text-embedding-3-small`)
3. **Docker** (for container testing)

## Setting Up Environment Variables

Before testing, set the required environment variables:

### PowerShell (Windows)
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key-here"
$env:AZURE_OPENAI_EMBEDDING_MODEL = "text-embedding-3-small"
```

### Bash (Linux/macOS)
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
export AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-small"
```

## Test 1: CLI Tool - Generate Embeddings

### Run the embeddings generator
```bash
cd /path/to/csla-mcp
dotnet run --project csla-embeddings-generator -- --examples-path ./csla-examples --output ./embeddings.json
```

### Expected Output
```
[EmbeddingsGenerator] Starting CSLA Embeddings Generator
[EmbeddingsGenerator] Examples path: ./csla-examples
[EmbeddingsGenerator] Output path: ./embeddings.json
[EmbeddingsGenerator] Using Azure OpenAI endpoint: https://...
[EmbeddingsGenerator] Using embedding model: text-embedding-3-small
[EmbeddingsGenerator] Found XX files to process
[EmbeddingsGenerator] Processing: DataPortalOperationCreate.md (common)
[EmbeddingsGenerator] Processed 5/XX files...
[EmbeddingsGenerator] Successfully processed XX files
[EmbeddingsGenerator] Generated XX embeddings
[EmbeddingsGenerator] Embeddings saved to ./embeddings.json
```

### Verification
1. Check that `embeddings.json` was created
2. Verify file size (should be several MB for typical example set)
3. Open file and verify JSON structure:
```json
[
  {
    "FileName": "DataPortalOperationCreate.md",
    "Content": "...",
    "Embedding": [0.123, 0.456, ...],
    "Version": null
  },
  ...
]
```

## Test 2: Server - Load Pre-generated Embeddings

### Copy embeddings to server directory
```bash
cp embeddings.json csla-mcp-server/bin/Debug/net10.0/
```

### Run the server
```bash
cd csla-mcp-server
dotnet run
```

### Expected Output
Look for these key log messages:
```
[Startup] Using Azure OpenAI endpoint: https://...
[Startup] Using embedding model deployment: text-embedding-3-small
[Startup] Vector store initialized successfully - semantic search enabled.
[Startup] Loaded XX pre-generated embeddings from /path/to/embeddings.json
```

### Verification
- Server should start quickly (within seconds)
- No embedding generation messages for individual files
- Search functionality should work normally

## Test 3: Server - Missing Embeddings

### Remove embeddings.json
```bash
rm csla-mcp-server/bin/Debug/net10.0/embeddings.json
```

### Run the server again
```bash
cd csla-mcp-server
dotnet run
```

### Expected Output
```
[Startup] Using Azure OpenAI endpoint: https://...
[Startup] Using embedding model deployment: text-embedding-3-small
[Startup] Vector store initialized successfully - semantic search enabled.
[Startup] Warning: No pre-generated embeddings found. Semantic search will not be available.
[Startup] To enable semantic search, generate embeddings using: dotnet run --project csla-embeddings-generator
```

### Verification
- Server starts quickly
- Warning message indicates semantic search is not available
- Keyword search continues to work
- Server does NOT attempt to generate embeddings at runtime

## Test 4: Build Script

### Run the full build script
```bash
./build.sh
```

### Expected Output
```
Building embeddings generator...
Build succeeded.
Generating embeddings...
[EmbeddingsGenerator] Starting CSLA Embeddings Generator
...
[EmbeddingsGenerator] Embeddings saved to ./embeddings.json
Building Docker container...
[+] Building XX.Xs (XX/XX finished)
...
```

### Verification
1. `embeddings.json` created in repository root
2. Docker image built successfully: `docker images | grep csla-mcp-server`
3. Image includes embeddings file

## Test 5: Docker Container

### Run the container
```bash
docker run --rm -p 8080:80 \
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" \
  -e AZURE_OPENAI_API_KEY="your-api-key" \
  csla-mcp-server:latest
```

### Expected Output (in container logs)
```
[Startup] Vector store initialized successfully - semantic search enabled.
[Startup] Loaded XX pre-generated embeddings from /app/embeddings.json
[Startup] Skipping embedding generation - using pre-generated embeddings
```

### Verification
- Container starts quickly
- Pre-generated embeddings are used
- Search API responds correctly

## Test 6: Semantic Search Functionality

### Test the search functionality
Once the server is running, test the search endpoint to ensure semantic search works with pre-generated embeddings.

The search should return relevant results based on semantic similarity.

## Troubleshooting

### Issue: CLI tool fails with "AZURE_OPENAI_ENDPOINT not set"
**Solution**: Ensure environment variables are set in the current shell session

### Issue: Docker build fails with "embeddings.json not found"
**Solution**: Run `build.sh` which generates embeddings before building Docker image

### Issue: Server loads 0 embeddings
**Solution**: 
- Check that embeddings.json exists in the application directory
- Verify JSON file is valid and not empty
- Check file permissions

### Issue: Embeddings file is too large
**Solution**: This is expected - embeddings contain float arrays with 1536+ dimensions per file

## Performance Comparison

### Before (Runtime Generation)
- Startup time: ~30-60 seconds for typical example set
- Azure OpenAI API calls: One per file on every startup

### After (Pre-generated Embeddings)
- Startup time: ~2-5 seconds
- Azure OpenAI API calls: Zero for file embeddings (only for user queries)

## Success Criteria

The implementation is successful if:
1. ✅ CLI tool generates valid embeddings.json
2. ✅ Server loads pre-generated embeddings on startup
3. ✅ Server startup is significantly faster with pre-generated embeddings
4. ✅ Server provides clear warnings if embeddings.json is missing
5. ✅ Semantic search functionality works correctly with pre-generated embeddings
6. ✅ Docker container includes and uses pre-generated embeddings
7. ✅ Build script generates embeddings before building container

## Notes

- The embeddings.json file is gitignored as it's a build artifact
- File size will vary based on number of examples (typically 5-20 MB)
- Each embedding is a float array (1536 dimensions for text-embedding-3-small)
- Pre-generated embeddings are immutable - regenerate when examples change
