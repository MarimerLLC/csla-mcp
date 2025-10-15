# CSLA .NET MCP Server

This repository contains the source code for the CSLA .NET MCP (Model Context Protocol) Server. This server is designed to support the use of generative AI (LLM) models when they are used to create .NET C# apps using the CSLA .NET framework.

## Overview

The CSLA MCP Server provides AI coding assistants with access to official CSLA .NET code examples, patterns, and best practices. It implements the Model Context Protocol (MCP) to serve as a knowledge base for CSLA development.

## Features

- **Code Examples**: Comprehensive collection of CSLA .NET code examples organized by concept and complexity
- **Semantic Search**: Find relevant examples using natural language queries powered by Azure OpenAI embeddings
- **Concept Browsing**: Browse available CSLA concepts and categories
- **Aspire Integration**: Built with .NET Aspire for modern cloud-native development
- **HTTP API**: RESTful API endpoints for easy integration

## Azure OpenAI Configuration

The server uses Azure OpenAI for vector embeddings to provide semantic search capabilities. You must configure the following environment variables:

### Required Environment Variables
- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI service endpoint (e.g., `https://your-resource.openai.azure.com/`)
- `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key

### Optional Environment Variables
- `AZURE_OPENAI_EMBEDDING_MODEL`: The embedding model deployment name to use (default: `text-embedding-3-small`)
- `AZURE_OPENAI_API_VERSION`: The API version to use (default: `2024-02-01`)

### ⚠️ Important: Model Deployment Required

**Before running the server**, you must deploy an embedding model in your Azure OpenAI resource. The deployment name must exactly match the `AZURE_OPENAI_EMBEDDING_MODEL` environment variable.

**Quick Setup**: See [azure-openai-setup-guide.md](azure-openai-setup-guide.md) for step-by-step instructions.

To deploy a model:
1. Go to [Azure OpenAI Studio](https://oai.azure.com/)
2. Navigate to "Deployments"
3. Create a new deployment with the model `text-embedding-3-small`
4. Ensure the deployment name matches your environment variable

**Fallback Mode**: If Azure OpenAI isn't configured, the server will run in keyword-only search mode.

### Example Configuration

**PowerShell (Windows):**
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key-here"
$env:AZURE_OPENAI_EMBEDDING_MODEL = "text-embedding-3-small"  # Must match deployment name
$env:AZURE_OPENAI_API_VERSION = "2024-02-01"  # Optional, API version
```

**Bash (Linux/macOS):**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
export AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-small"  # Must match deployment name
export AZURE_OPENAI_API_VERSION="2024-02-01"  # Optional, API version
```

For more detailed configuration information, see [azure-openai-config.md](azure-openai-config.md).

## Vector Embeddings

The server uses **pre-generated vector embeddings** for semantic search functionality. This significantly reduces startup time and eliminates Azure OpenAI API costs for embedding generation.

### How It Works

1. **Embedding Generation** (before running the server):
   - Run the `csla-embeddings-generator` CLI tool to generate embeddings for all code samples
   - This creates an `embeddings.json` file containing pre-computed vector embeddings
   
2. **Server Startup**:
   - The server loads the pre-generated embeddings from `embeddings.json` at startup
   - No embedding generation occurs during server initialization
   
3. **Runtime** (user queries):
   - Azure OpenAI credentials are still required to generate embeddings for user search queries
   - The server compares user query embeddings against the pre-loaded code sample embeddings

### Generating Embeddings

**Before running the server**, you must generate embeddings for your code samples:

```bash
# Generate embeddings for the default csla-examples directory
dotnet run --project csla-embeddings-generator

# Or specify custom paths
dotnet run --project csla-embeddings-generator -- --examples-path ./csla-examples --output ./embeddings.json
```

This will create an `embeddings.json` file in the current directory (or the specified output path).

See [csla-embeddings-generator/README.md](csla-embeddings-generator/README.md) for more details.

### Configuring Embeddings Path

The server needs to know where to find the `embeddings.json` file. There are three ways to configure this (priority from highest to lowest):

1. **Command-line flag** `--embeddings` or `-e`
2. **Environment variable** `CSLA_EMBEDDINGS_PATH`
3. **Default path**: `./embeddings.json` (current directory)

#### Examples

**Using command-line flag:**
```bash
dotnet run --project csla-mcp-server -- --embeddings ./path/to/embeddings.json
```

**Using environment variable (PowerShell):**
```powershell
$env:CSLA_EMBEDDINGS_PATH = "S:\src\rdl\csla-mcp\embeddings.json"
dotnet run --project csla-mcp-server
```

**Using environment variable (Bash):**
```bash
export CSLA_EMBEDDINGS_PATH="/path/to/embeddings.json"
dotnet run --project csla-mcp-server
```

**Using default path:**
```bash
# Assumes embeddings.json exists in current directory
dotnet run --project csla-mcp-server
```

### Benefits

- **Faster Startup**: Server starts immediately without waiting for embedding generation
- **Reduced Costs**: Code sample embeddings are only generated once, not on every server restart
- **Offline Development**: Server can start without Azure OpenAI (though semantic search requires it for user queries)
- **Consistent Results**: Same embeddings used across all server instances

## MCP Tools

The server currently exposes two MCP tools implemented in `CslaMcpServer.Tools.CslaCodeTool`:

- `Search` — search code samples and markdown snippets for keyword matches and return scored results.
- `Fetch` — return the raw content of a named code sample or markdown file.

Both tools operate over the repository folder that contains the example files: `csla-examples/` (the tool uses an absolute path in the server code: `s:\src\rdl\csla-mcp\csla-examples\`).

### Tool: Search

Description: Extracts significant words from the provided input text and searches `.cs` and `.md` files under the examples folder for occurrences of those words. Returns a JSON array of consolidated search results that merge semantic (vector-based) and word-based (keyword) search scores.

Parameters:
- `message` (string, required): Natural language text or keywords to search for. Words of length 4 or less are ignored by the tool. The tool also searches for 2-word combinations from adjacent words to find phrase matches (e.g., "create operation" and "operation method" from "create operation method").
- `version` (integer, optional): CSLA version number to filter results (e.g., `9` or `10`). If not provided, defaults to the highest version available by scanning version subdirectories in the examples folder (e.g., `v9/`, `v10/`). Files in the root directory (common to all versions) are included regardless of the specified version.

Output: JSON array of objects with the shape:

- `FileName` (string): relative file path from the examples folder (e.g., `v10/ReadOnlyProperty.md` or `CommonFile.cs`)
- `Score` (double): normalized combined score (0.0 to 1.0) from semantic and word searches
- `VectorScore` (double, nullable): semantic similarity score from Azure OpenAI embeddings (null if semantic search unavailable)
- `WordScore` (double, nullable): normalized keyword match score (null if no keyword matches found)

Example call (MCP `tools/call`):

```json
{
  "method": "tools/call",
  "params": {
    "name": "Search",
    "arguments": { 
      "message": "data portal authorization business object",
      "version": 10
    }
  }
}
```

Example call without version (uses highest available):

```json
{
  "method": "tools/call",
  "params": {
    "name": "Search",
    "arguments": { 
      "message": "read-write property editable root"
    }
  }
}
```

Notes and behavior:
- The tool ignores short words (<= 3 characters) when building the search terms.
- The tool creates 2-word combinations from adjacent words in the search message to find phrase matches. Multi-word phrase matches receive higher scores (weight of 2) compared to single word matches (weight of 1).
- Word matching uses word boundaries to ensure exact matches. For example, searching for "property" will not match "ReadProperty" or "GetProperty".
- Matching is case-insensitive and counts multiple occurrences in a file.
- Results combine both semantic search (when Azure OpenAI is configured) and keyword search for more accurate results.
- Results are ordered by `Score` descending, then by filename.
- Version filtering: Files in version subdirectories (e.g., `v9/`, `v10/`) are filtered by the specified version. Files in the root directory are considered common to all versions and are always included.

### Tool: Fetch

Description: Returns the text contents of a specific file from the `csla-examples/` folder by file name.

Parameters:
- `fileName` (string, required): The name of the file to fetch (for example, `ReadOnlyProperty.md` or `MyBusinessClass.cs`). The tool resolves the file by combining the configured examples path with the given file name.

Output: Raw file contents as a string. If the file is not found the tool returns a simple error message string like `"File 'X' not found."`.

Example call (MCP `tools/call`):

```json
{
  "method": "tools/call",
  "params": {
    "name": "Fetch",
    "arguments": { "fileName": "ReadOnlyProperty.md" }
  }
}
```

Security note:
- The current implementation combines the configured absolute path and the provided `fileName` directly and does not perform additional validation to prevent path traversal. When exposing these tools remotely, consider adding validation to ensure only allowed files are returned.

If you need the previous higher-level tools such as `get_csla_example`, `list_csla_concepts`, or a semantic search wrapper, those are not implemented in `CslaCodeTool.cs` and would need to be added separately.

## Integration with AI Assistants

This MCP server is designed to be used by AI coding assistants to provide accurate, up-to-date CSLA .NET examples and guidance. When integrated:

1. AI assistants can query for specific CSLA patterns
2. The server returns official, tested code examples
3. AI assistants can provide more accurate CSLA guidance to developers

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add your code examples following the established patterns
4. Test your changes
5. Submit a pull request

### Code Example Guidelines

- Use clear, descriptive file names
- Include comprehensive examples that demonstrate the concept
- Add explanatory comments in code examples
- Create accompanying markdown documentation for complex patterns
- Follow CSLA best practices and conventions

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For questions about CSLA .NET, visit:

- [CSLA .NET Website](https://cslanet.com/)
- [CSLA .NET GitHub](https://github.com/MarimerLLC/csla)
- [CSLA .NET Discussions](https://github.com/MarimerLLC/csla/discussions)

## Docker: Build and Run

This project includes a multi-stage `Dockerfile` for the `csla-mcp-server` located at `csla-mcp-server/Dockerfile` that builds and publishes the app, then produces a small runtime image.

### Building from Source

**Important**: Before building the Docker image, you must generate vector embeddings for the code samples.

Use the `build.sh` script to automate the entire process:

```bash
./build.sh
```

This script will:
1. Build the embeddings generator CLI tool
2. Generate embeddings for all code samples in `csla-examples/`
3. Create `embeddings.json` in the repository root
4. Build the Docker image with embeddings included

Alternatively, you can perform these steps manually:

```bash
# Step 1: Generate embeddings
dotnet run --project csla-embeddings-generator -- --examples-path ./csla-examples --output ./embeddings.json

# Step 2: Build Docker image
docker build -f csla-mcp-server/Dockerfile -t csla-mcp-server:latest .
```

### Using Pre-built Image from Docker Hub

The official pre-built image is available on Docker Hub and already includes pre-generated embeddings:

```bash
docker pull rockylhotka/csla-mcp-server:latest
```

This image contains:
- The csla-mcp-server application
- Pre-generated embeddings for the official CSLA code samples
- All necessary runtime dependencies

### Running the Container

Run the container with Azure OpenAI configuration (maps container port 80 to host port 8080):

**Using Docker Hub image:**
```powershell
docker run --rm -p 8080:80 `
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" `
  -e AZURE_OPENAI_API_KEY="your-api-key-here" `
  -e AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-small" `
  -e AZURE_OPENAI_API_VERSION="2024-02-01" `
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

**Using locally built image:**
```powershell
docker run --rm -p 8080:80 `
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" `
  -e AZURE_OPENAI_API_KEY="your-api-key-here" `
  -e AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-small" `
  -e AZURE_OPENAI_API_VERSION="2024-02-01" `
  --name csla-mcp-server csla-mcp-server:latest
```

Open your browser to `http://localhost:8080` to access the server.

### Using Custom Embeddings with Docker

If you want to use your own embeddings file with the Docker container:

1. Generate your embeddings locally:
```bash
dotnet run --project csla-embeddings-generator -- --examples-path ./my-examples --output ./my-embeddings.json
```

2. Mount the embeddings file into the container:
```powershell
docker run --rm -p 8080:80 `
  -v "S:\path\to\my-embeddings.json:/app/embeddings.json" `
  -e CSLA_EMBEDDINGS_PATH="/app/embeddings.json" `
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" `
  -e AZURE_OPENAI_API_KEY="your-api-key-here" `
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

### Docker: Mounting Custom Code Samples

You can mount your own `csla-examples` folder into the container and set the `CSLA_CODE_SAMPLES_PATH` environment variable:

**Linux/macOS:**
```bash
docker run --rm -p 8080:80 \
  -v "/path/on/host/csla-examples:/app/examples" \
  -e CSLA_CODE_SAMPLES_PATH="/app/examples" \
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" \
  -e AZURE_OPENAI_API_KEY="your-api-key-here" \
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

**Windows (PowerShell):**
```powershell
docker run --rm -p 8080:80 `
  -v "S:\src\rdl\csla-mcp\csla-examples:/app/examples" `
  -e CSLA_CODE_SAMPLES_PATH="/app/examples" `
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" `
  -e AZURE_OPENAI_API_KEY="your-api-key-here" `
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

**Note**: If you mount custom code samples, you should also mount custom embeddings that were generated from those samples, otherwise the semantic search results may not match your custom code samples.

### Docker Build Notes

- The `Dockerfile` uses .NET 10 SDK and ASP.NET runtime images. Ensure your Docker installation supports the required base images.
- The Docker build includes the `embeddings.json` file that should exist in the repository root before building.
- If you need to debug or iterate quickly during development, consider running the app locally with `dotnet run --project csla-mcp-server/csla-mcp-server.csproj` instead of rebuilding the image for every change.
- **Important**: The Azure OpenAI environment variables are required for the semantic search functionality to work at runtime (for user query embeddings).

## Configuring the Code Samples Folder

The MCP server reads code samples and markdown examples from a configurable folder. There are three ways to control which folder is used (priority from highest to lowest):

1. Command-line flag `-f` / `--folder` when launching the server
2. Environment variable `CSLA_CODE_SAMPLES_PATH`
3. Built-in default path used by the server code

The command-line flag always overrides the environment variable. If neither is provided the server uses the default examples path.

### Examples

**Run and point to a folder using the `-f` option (PowerShell):**
```powershell
dotnet run --project csla-mcp-server -- -f "S:\src\rdl\csla-mcp\csla-examples"
```

**Set the environment variable (PowerShell) and run (no `-f`, env will be used):**
```powershell
$env:CSLA_CODE_SAMPLES_PATH = 'S:\src\rdl\csla-mcp\csla-examples'
dotnet run --project csla-mcp-server --
```

**One-off launch with env var from cmd.exe (Windows):**
```cmd
set CSLA_CODE_SAMPLES_PATH=S:\src\rdl\csla-mcp\csla-examples && dotnet run --project csla-mcp-server --
```

### Validation and Errors

- The server validates the provided folder on startup. If the folder does not exist or does not contain any `.cs` or `.md` files the server will print a helpful error and exit with a non-zero code.
- Exit codes used for validation failures:
  - `2` — CLI folder does not exist
  - `3` — CLI folder exists but contains no `.cs` or `.md` files
  - `4` — ENV folder does not exist
  - `5` — ENV folder exists but contains no `.cs` or `.md` files
  - `6` — ENV variable could not be processed (unexpected error)
