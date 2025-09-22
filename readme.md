# CSLA .NET MCP Server

This repository contains the source code for the CSLA .NET MCP (Model Context Protocol) Server. This server is designed to support the use of generative AI (LLM) models when they are used to create .NET C# apps using the CSLA .NET framework.

## Overview

The CSLA MCP Server provides AI coding assistants with access to official CSLA .NET code examples, patterns, and best practices. It implements the Model Context Protocol (MCP) to serve as a knowledge base for CSLA development.

## Features

- **Code Examples**: Comprehensive collection of CSLA .NET code examples organized by concept and complexity
- **Semantic Search**: Find relevant examples using natural language queries
- **Concept Browsing**: Browse available CSLA concepts and categories
- **Aspire Integration**: Built with .NET Aspire for modern cloud-native development
- **HTTP API**: RESTful API endpoints for easy integration

## MCP Tools

The server currently exposes two MCP tools implemented in `CslaMcpServer.Tools.CslaCodeTool`:

- `Search` — search code samples and markdown snippets for keyword matches and return scored results.
- `Fetch` — return the raw content of a named code sample or markdown file.

Both tools operate over the repository folder that contains the example files: `csla-examples/` (the tool uses an absolute path in the server code: `s:\src\rdl\csla-mcp\csla-examples\`).

### Tool: Search

Description: Extracts significant words from the provided input text and searches `.cs` and `.md` files under the examples folder for occurrences of those words. Returns a JSON array of results ordered by score (total matching-word counts).

Parameters:
- `message` (string, required): Natural language text or keywords to search for. Words of length 4 or less are ignored by the tool.

Output: JSON array of objects with the shape:

- `Score` (int): total number of matching word occurrences across the file
- `FileName` (string): file name (without path)
- `MatchingWords` (array): list of `{ Word, Count }` objects showing which search terms matched and how many times

Example call (MCP `tools/call`):

```json
{
  "method": "tools/call",
  "params": {
    "name": "Search",
    "arguments": { "message": "data portal authorization business object" }
  }
}
```

Notes and behavior:
- The tool ignores short words (<= 3 characters) when building the search terms.
- Matching is case-insensitive and counts multiple occurrences in a file.
- Results are ordered by `Score` descending, then by filename.

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

Below are PowerShell-friendly (Windows) commands to build and run the container locally. Run these from the repository root (`s:\src\rdl\csla-mcp`) or adjust paths if running from elsewhere.

1) Build the Docker image (tags the image as `csla-mcp-server:latest`):

```powershell
docker build -f csla-mcp-server/Dockerfile -t csla-mcp-server:latest .
```

2) Run the container (maps container port 80 to host port 8080):

```powershell
docker run --rm -p 8080:80 --name csla-mcp-server csla-mcp-server:latest
```

3) Open your browser to `http://localhost:8080` (or the mapped host port) to access the server. If the server uses a different default endpoint, consult the project `Program.cs` or the server logs printed to the container.

Optional: Build with a different tag and pass an environment variable for ASP.NET Core URLs (already set in the Dockerfile):

```powershell
docker build -f csla-mcp-server/Dockerfile -t myregistry/csla-mcp-server:v1.0 .
docker run --rm -p 8080:80 --name csla-mcp-server -e ASPNETCORE_ENVIRONMENT=Development myregistry/csla-mcp-server:v1.0
```

Notes:
- The `Dockerfile` uses .NET 10 SDK and ASP.NET runtime images. Ensure your Docker installation supports the required base images.
- The Docker build will run a `dotnet publish` inside the container; it may take a few minutes the first time as NuGet packages are restored.
- If you need to debug or iterate quickly during development, consider running the app locally with `dotnet run --project csla-mcp-server/csla-mcp-server.csproj` instead of rebuilding the image for every change.

### Docker: pass the code samples folder into the container

When running the server in Docker you can mount your host `csla-examples` folder into the container and set the `CSLA_CODE_SAMPLES_PATH` environment variable so the server uses your host examples.

Example (Linux/macOS or Docker Desktop using Linux containers):

```bash
docker run --rm -p 8080:80 \
  -v "/path/on/host/csla-examples:/app/examples" \
  -e CSLA_CODE_SAMPLES_PATH="/app/examples" \
  --name csla-mcp-server csla-mcp-server:latest
```

Example (PowerShell on Windows):

```powershell
docker run --rm -p 8080:80 `
  -v "S:\src\rdl\csla-mcp\csla-examples:/app/examples" `
  -e CSLA_CODE_SAMPLES_PATH="/app/examples" `
  --name csla-mcp-server csla-mcp-server:latest
```

Notes:
- Mount the host examples folder to a path inside the container (for example `/app/examples`) and set the `CSLA_CODE_SAMPLES_PATH` env var to that in-container path.
- The CLI `-f` flag still overrides the environment variable if you supply it to the container command.
- If running Windows containers, adjust the mount target and path style appropriately.

## Configuring the code samples folder

The MCP server reads code samples and markdown examples from a configurable folder. There are three ways to control which folder is used (priority from highest to lowest):

1. Command-line flag `-f` / `--folder` when launching the server
2. Environment variable `CSLA_CODE_SAMPLES_PATH`
3. Built-in default path used by the server code

The command-line flag always overrides the environment variable. If neither is provided the server uses the default examples path (the original behavior).

Examples

- Run and point to a folder using the `-f` option (PowerShell):

```powershell
dotnet run --project csla-mcp-server -- -f "S:\src\rdl\csla-mcp\csla-examples"
```

- Set the environment variable (PowerShell) and run (no `-f`, env will be used):

```powershell
$env:CSLA_CODE_SAMPLES_PATH = 'S:\src\rdl\csla-mcp\csla-examples'
dotnet run --project csla-mcp-server --
```

- One-off launch with env var from cmd.exe (Windows):

```cmd
set CSLA_CODE_SAMPLES_PATH=S:\src\rdl\csla-mcp\csla-examples && dotnet run --project csla-mcp-server --
```

Validation and errors

- The server validates the provided folder on startup. If the folder does not exist or does not contain any `.cs` or `.md` files the server will print a helpful error and exit with a non-zero code.
- Exit codes used for validation failures:
  - `2` — CLI folder does not exist
  - `3` — CLI folder exists but contains no `.cs` or `.md` files
  - `4` — ENV folder does not exist
  - `5` — ENV folder exists but contains no `.cs` or `.md` files
  - `6` — ENV variable could not be processed (unexpected error)
