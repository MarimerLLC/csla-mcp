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
