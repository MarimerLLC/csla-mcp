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

The server provides three main MCP tools:

### 1. get_csla_example

Get CSLA code examples for specific concepts.

**Parameters:**
- `concept` (required): The CSLA concept (e.g., 'business-object', 'data-portal', 'authorization')
- `category` (optional): Category filter (e.g., 'basic', 'advanced', 'patterns')

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
