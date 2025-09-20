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

## Architecture

### Projects

- **csla-mcp.AppHost**: .NET Aspire application host
- **csla-mcp.Server**: ASP.NET Core MCP server implementation
- **csla-mcp.ServiceDefaults**: Shared Aspire service configuration

### Key Components

- **McpServer**: Core MCP protocol implementation
- **CodeExampleService**: Manages and serves code examples from the file system
- **McpController**: HTTP API endpoints for MCP communication

## Code Examples Structure

The server organizes CSLA code examples in the following structure:

```
CodeExamples/
??? business-object/          # Business object patterns
?   ??? basic/               # Basic business object examples
?   ??? advanced/            # Advanced patterns and scenarios
?   ??? patterns/            # Common design patterns
??? data-portal/             # Data portal configuration and usage
??? validation/              # Validation rules and patterns
??? authorization/           # Security and authorization
??? serialization/           # Object serialization
??? ui-binding/              # UI data binding patterns
??? dependency-injection/    # DI patterns with CSLA
??? blazor/                  # Blazor-specific patterns
??? aspnetcore/              # ASP.NET Core integration
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Visual Studio 2022 or VS Code with C# extension

### Running the Server

1. Clone the repository
2. Open the solution in Visual Studio
3. Set `csla-mcp.AppHost` as the startup project
4. Run the application (F5)

The server will start and be available at the URL shown in the Aspire dashboard.

### API Endpoints

- `GET /mcp/info` - Server information and capabilities
- `POST /mcp` - MCP protocol requests
- `GET /mcp/health` - Health check endpoint

## MCP Tools

The server provides three main MCP tools:

### 1. get_csla_example

Get CSLA code examples for specific concepts.

**Parameters:**
- `concept` (required): The CSLA concept (e.g., 'business-object', 'data-portal', 'authorization')
- `category` (optional): Category filter (e.g., 'basic', 'advanced', 'patterns')

**Example:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "get_csla_example",
    "arguments": {
      "concept": "business-object",
      "category": "basic"
    }
  }
}
```

### 2. list_csla_concepts

List all available CSLA concepts and categories.

**Example:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "list_csla_concepts",
    "arguments": {}
  }
}
```

### 3. search_csla_examples

Search for CSLA examples using semantic search.

**Parameters:**
- `query` (required): Search query to find relevant examples

**Example:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "search_csla_examples",
    "arguments": {
      "query": "validation rules for business objects"
    }
  }
}
```

## Adding New Examples

To add new code examples:

1. Navigate to the `csla-mcp.Server/CodeExamples` directory
2. Create the appropriate concept directory if it doesn't exist
3. Add your example files (`.cs` or `.md`) in the appropriate category subdirectory
4. Use meaningful file names that describe the example
5. The server will automatically discover and serve the new examples

### Example File Organization

```
CodeExamples/
??? business-object/
    ??? basic/
    ?   ??? SimpleBusinessObject.cs
    ?   ??? CustomerWithValidation.cs
    ??? advanced/
    ?   ??? OrderWithBusinessRules.cs
    ?   ??? AuditableBusinessBase.cs
    ??? patterns/
        ??? BusinessObjectPatterns.md
        ??? FactoryMethods.cs
```

## Development

### Technology Stack

- **.NET 9**: Target framework
- **ASP.NET Core**: Web framework
- **Aspire**: Cloud-native application platform
- **ModelContextProtocol**: MCP implementation
- **Microsoft.Extensions.AI**: AI integration capabilities
- **Semantic Kernel**: AI orchestration (for future enhancements)

### Project Dependencies

```xml
<PackageReference Include="ModelContextProtocol" Version="0.3.0-preview.4" />
<PackageReference Include="Microsoft.Extensions.AI" Version="9.0.1-preview.1.24570.5" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.44.0" />
```

### Configuration

The server uses standard ASP.NET Core configuration patterns. Key settings can be configured via:

- `appsettings.json`
- Environment variables
- User secrets (for development)

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
