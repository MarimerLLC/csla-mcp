# Vector Store Persistence Implementation

## Overview

This document describes the implementation of pre-generated vector embeddings for the CSLA MCP server, addressing issue #XX which requested storing vector results to avoid regenerating embeddings on every server startup.

## Problem Statement

Previously, the csla-mcp-server would:
1. Start up
2. Connect to Azure OpenAI
3. Generate embeddings for all code samples in the csla-examples directory
4. Store embeddings in memory

This approach had several drawbacks:
- Slow startup time (waiting for all embeddings to be generated)
- High Azure OpenAI API costs (regenerating embeddings on every restart)
- Unnecessary computation for unchanged files

## Solution

The solution implements a build-time embedding generation process:

### 1. CLI Tool (csla-embeddings-generator)

A new console application that:
- Scans the csla-examples directory for .cs and .md files
- Connects to Azure OpenAI to generate embeddings
- Saves all embeddings to a JSON file (`embeddings.json`)
- Can be run independently or as part of the build process

**Location**: `csla-embeddings-generator/`

**Key Classes**:
- `Program.cs`: Entry point with command-line argument parsing
- `EmbeddingsGenerator.cs`: Core logic for generating embeddings
- `DocumentEmbedding.cs`: Data model for document embeddings

### 2. Enhanced VectorStoreService

The VectorStoreService now supports:
- Loading embeddings from JSON file via `LoadEmbeddingsFromJsonAsync()`
- Exporting embeddings to JSON file via `ExportEmbeddingsToJsonAsync()`
- Maintains the same in-memory structure as before

**Changes**: `csla-mcp-server/Services/VectorStoreService.cs`

### 3. Updated Startup Logic

The server now:
1. Checks for `embeddings.json` in the application directory
2. If found, loads pre-generated embeddings (fast startup)
3. If not found, falls back to the original behavior (generate at runtime)
4. Still requires Azure OpenAI credentials at runtime for user query embeddings

**Changes**: `csla-mcp-server/Program.cs`

### 4. Build Process Integration

The `build.sh` script now:
1. Builds the embeddings generator CLI tool
2. Runs the CLI tool to generate `embeddings.json`
3. Creates an empty JSON array if generation fails (allows Docker build to succeed)
4. Builds the Docker container with embeddings included

**Changes**: `build.sh`

### 5. Docker Container

The Dockerfile now:
- Copies the `embeddings.json` file into the container at `/app/embeddings.json`
- Server loads embeddings from this location on startup

**Changes**: `csla-mcp-server/Dockerfile`

## Benefits

1. **Faster Startup**: Server starts immediately, loading pre-generated embeddings from disk
2. **Reduced Costs**: Embeddings generated once during build, not on every server restart
3. **Simpler Architecture**: Server only loads embeddings, doesn't generate them for example files
4. **Build-time Validation**: Embedding generation happens during build, catching issues early

## Usage

### Building with Embeddings

Use the provided build script:

```bash
./build.sh
```

This will generate embeddings and build the Docker container.

### Manual Embedding Generation

Generate embeddings independently:

```bash
dotnet run --project csla-embeddings-generator -- --examples-path ./csla-examples --output ./embeddings.json
```

### Running the Server

The server automatically loads embeddings if available:

```bash
docker run --rm -p 8080:80 \
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" \
  -e AZURE_OPENAI_API_KEY="your-api-key" \
  csla-mcp-server:latest
```

## File Structure

```
csla-mcp/
├── csla-embeddings-generator/          # New CLI tool
│   ├── Program.cs
│   ├── EmbeddingsGenerator.cs
│   ├── DocumentEmbedding.cs
│   ├── csla-embeddings-generator.csproj
│   └── README.md
├── csla-mcp-server/
│   ├── Services/
│   │   └── VectorStoreService.cs      # Enhanced with JSON I/O
│   ├── Program.cs                      # Updated startup logic
│   └── Dockerfile                      # Updated to copy embeddings.json
├── build.sh                            # Updated build script
├── embeddings.json                     # Generated file (gitignored)
└── readme.md                           # Updated documentation

```

## Configuration

### Environment Variables (Build Time)

Required for embedding generation:
- `AZURE_OPENAI_ENDPOINT`: Azure OpenAI service endpoint
- `AZURE_OPENAI_API_KEY`: Azure OpenAI API key
- `AZURE_OPENAI_EMBEDDING_MODEL`: Embedding model name (default: text-embedding-3-small)

### Environment Variables (Runtime)

Still required for user query embeddings:
- `AZURE_OPENAI_ENDPOINT`: Azure OpenAI service endpoint
- `AZURE_OPENAI_API_KEY`: Azure OpenAI API key

## Testing

Since this environment doesn't have .NET 10.0 SDK or Azure OpenAI credentials, full testing requires:

1. **Build Environment**: .NET 10.0 SDK
2. **Azure Credentials**: Valid Azure OpenAI endpoint and API key
3. **Code Samples**: The csla-examples directory with sample files

### Manual Testing Steps

1. Set Azure OpenAI environment variables
2. Run `./build.sh` to generate embeddings and build container
3. Verify `embeddings.json` is created and contains embeddings
4. Run the Docker container
5. Check startup logs for "Loaded X pre-generated embeddings"
6. Test search functionality to ensure semantic search works

## Backward Compatibility

The implementation maintains API compatibility:
- If `embeddings.json` is missing, server will start but semantic search will be disabled
- Keyword search continues to work without embeddings
- Users are notified to generate embeddings using the CLI tool
- No breaking changes to the API or user experience

## Future Enhancements

Potential improvements:
1. Incremental updates: Only regenerate embeddings for changed files
2. Compression: Compress embeddings.json to reduce size
3. Versioning: Track embeddings file version for compatibility
4. Caching: Add timestamps to detect stale embeddings
