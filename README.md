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

## Deployment

This project made public via a container image file that you can host.

Docker Hub is used to host the [CSLA .NET MCP server container image](https://hub.docker.com/repository/docker/rockylhotka/csla-mcp-server).

You can run this container image in any container host that supports x64 Linux containers. This includes Docker Desktop, Azure Web Service, ACA, Kubernetes, etc.

### Run on Docker Desktop

To run it on your local Docker instance with keyword-only search:

```powershell
docker run --rm -p 8080:8080 `
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

> ℹ️ The container image includes pre-generated embeddings and CSLA code examples.
>
> ℹ️ The container exposes port 8080 by default. You can map it to a different port on your host (e.g., `-p 9000:8080`).

To enable semantic search with vector embeddings, provide Azure OpenAI credentials:

```powershell
docker run --rm -p 8080:8080 `
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" `
  -e AZURE_OPENAI_API_KEY="your-api-key-here" `
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

> ⚠️ Azure OpenAI credentials are required to generate embeddings for user search queries at runtime.

#### Optional: Override embedded data with custom examples

If you want to use your own code examples or updated embeddings, you can override the embedded data with volume mounts:

```powershell
docker run --rm -p 8080:8080 `
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" `
  -e AZURE_OPENAI_API_KEY="your-api-key-here" `
  -v ${PWD}/embeddings.json:/app/embeddings.json:ro `
  -v ${PWD}/csla-examples:/app/csla-examples:ro `
  --name csla-mcp-server rockylhotka/csla-mcp-server:latest
```

## Connecting to the MCP Server

Once the server is running, you can connect to it from MCP-compatible tools like VS Code with GitHub Copilot.

### Connecting from VS Code

1. **Install the GitHub Copilot Chat extension** in VS Code (if not already installed)

2. **Open VS Code Settings** (File > Preferences > Settings or `Ctrl+,`)

3. **Search for "MCP"** in the settings search bar

4. **Find "Chat > MCP: Servers"** and click "Edit in settings.json"

5. **Add your MCP server configuration** to the `github.copilot.chat.mcp.servers` object:

   ```json
   {
     "github.copilot.chat.mcp.servers": {
       "csla-mcp": {
         "type": "http",
         "url": "http://localhost:8080"
       }
     }
   }
   ```

   > **Note:** If you mapped the Docker container to a different port (e.g., `-p 9000:8080`), use that port in the URL: `http://localhost:9000`

6. **Restart VS Code** to apply the changes

7. **Verify the connection**: Open GitHub Copilot Chat and you should now be able to use the CSLA MCP tools in your conversations. The server provides two tools:
   - `Search` - Search CSLA code examples and documentation
   - `Fetch` - Retrieve specific code examples by filename

### Testing the Connection

You can test the MCP server is working by asking GitHub Copilot questions about CSLA, such as:

- "Show me how to create an editable root business object in CSLA"
- "How do I implement a read-only property in CSLA?"
- "What's an example of using the data portal in CSLA?"

Copilot will use the MCP server tools to search for and retrieve relevant CSLA code examples.

### Troubleshooting

- **Connection failed**: Ensure the Docker container is running (`docker ps`) and accessible at the configured URL
- **No results**: Check the server logs (`docker logs csla-mcp-server`) for errors
- **Semantic search not working**: Verify Azure OpenAI environment variables are set correctly

### Azure OpenAI Configuration

The server uses Azure OpenAI for vector embeddings to provide semantic search capabilities. You must configure the following environment variables:

#### Required Environment Variables

- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI service endpoint (e.g., `https://your-resource.openai.azure.com/`)
- `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key

#### Optional Environment Variables

- `AZURE_OPENAI_EMBEDDING_MODEL`: The embedding model deployment name to use (default: `text-embedding-3-large`)
- `AZURE_OPENAI_API_VERSION`: The API version to use (default: `2024-02-01`)

#### ⚠️ Important: Model Deployment Required

**Before running the server**, you must deploy an embedding model in your Azure OpenAI resource. The deployment name must exactly match the `AZURE_OPENAI_EMBEDDING_MODEL` environment variable.

**Quick Setup**: See [azure-openai-setup-guide.md](azure-openai-setup-guide.md) for step-by-step instructions.

To deploy a model:

1. Go to [Azure OpenAI Studio](https://oai.azure.com/)
2. Navigate to "Deployments"
3. Create a new deployment with the model `text-embedding-3-large`
4. Ensure the deployment name matches your environment variable

**Fallback Mode**: If Azure OpenAI isn't configured, the server will run in keyword-only search mode.

#### Example Configuration

**PowerShell (Windows):**

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key-here"
$env:AZURE_OPENAI_EMBEDDING_MODEL = "text-embedding-3-large"  # Must match deployment name
$env:AZURE_OPENAI_API_VERSION = "2024-02-01"  # Optional, API version
```

**Bash (Linux/macOS):**

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
export AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-large"  # Must match deployment name
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

### Running the Server Locally

When running the server locally for development, you must use the `run` command:

```bash
# From the repository root
dotnet run --project csla-mcp-server -- run

# With custom code samples path
dotnet run --project csla-mcp-server -- run --folder ./my-custom-examples
```

### Configuring Code Samples Path

The server needs to know where to find the CSLA code samples. There are three ways to configure this (priority from highest to lowest):

1. **Command-line flag** `--folder` or `-f`
2. **Environment variable** `CSLA_CODE_SAMPLES_PATH`
3. **Default path**: `../csla-examples` (relative to the executable)

#### Examples

**Using command-line flag:**

```bash
dotnet run --project csla-mcp-server -- run --folder ./csla-examples
```

**Using environment variable (PowerShell):**

```powershell
$env:CSLA_CODE_SAMPLES_PATH = "S:\src\rdl\csla-mcp\csla-examples"
dotnet run --project csla-mcp-server -- run
```

**Using environment variable (Bash):**

```bash
export CSLA_CODE_SAMPLES_PATH="/path/to/csla-examples"
dotnet run --project csla-mcp-server -- run
```

**Using default path:**

```bash
# When running from the repository root, the default ../csla-examples works automatically
dotnet run --project csla-mcp-server -- run
```

### Embeddings File Location

The server loads pre-generated embeddings from `embeddings.json` in the application's base directory. In a containerized deployment, this file should be mounted via a volume (see Docker examples above). When running locally with `dotnet run`, place the file in the same directory as the server executable or in the project directory.

### Benefits

- **Faster Startup**: Server starts immediately without waiting for embedding generation
- **Reduced Costs**: Code sample embeddings are only generated once, not on every server restart
- **Offline Development**: Server can start without Azure OpenAI (though semantic search requires it for user queries)
- **Consistent Results**: Same embeddings used across all server instances

## MCP Tools

The server currently exposes two MCP tools implemented in the `CslaCodeTool` class:

- `Search` — search code samples and markdown snippets for keyword matches and return scored results.
- `Fetch` — return the raw content of a named code sample or markdown file.

Both tools operate over the repository folder that contains the example files. By default, this is `../csla-examples` relative to the server executable, but this can be configured using:

- The `--folder` or `-f` command-line option
- The `CSLA_CODE_SAMPLES_PATH` environment variable
- When running from the repository root, the default resolves to `csla-examples/`

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

Description: Returns the text contents of a specific file from the configured code samples folder by file name.

Parameters:

- `fileName` (string, required): The name or relative path of the file to fetch (for example, `ReadOnlyProperty.md`, `v10/EditableRoot.md`, or `MyBusinessClass.cs`). The tool resolves the file by combining the configured code samples path with the given file name. Path traversal attempts (e.g., `../`) are blocked for security.

Output: Raw file contents as a string. If the file is not found or the path is invalid, the tool returns a JSON error object with `Error` and `Message` fields.

Example call (MCP `tools/call`):

```json
{
  "method": "tools/call",
  "params": {
    "name": "Fetch",
    "arguments": { "fileName": "v10/ReadOnlyProperty.md" }
  }
}
```

Security note:

- The implementation validates file paths to prevent path traversal attacks. Only files within the configured code samples directory can be accessed. Relative paths like `../` or absolute paths are rejected.

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
