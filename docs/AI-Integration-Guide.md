# Using CSLA MCP Server with AI Assistants

This guide explains how to integrate the CSLA .NET MCP Server with various AI coding assistants.

## Overview

The CSLA MCP Server provides three main tools that AI assistants can use:

1. **get_csla_example** - Get specific CSLA code examples
2. **list_csla_concepts** - Browse available concepts and categories  
3. **search_csla_examples** - Search examples using natural language

## Server Setup

### 1. Start the Server

```bash
# Using .NET Aspire (recommended)
cd csla-mcp.AppHost
dotnet run

# Or run the server directly
cd csla-mcp.Server
dotnet run
```

The server will be available at `https://localhost:5001` (or the port shown in the console).

### 2. Test the Server

```bash
# Check server health
curl https://localhost:5001/mcp/health

# Get server info
curl https://localhost:5001/mcp/info
```

## Integration Examples

### Claude Desktop (Anthropic)

Add to your Claude Desktop configuration file:

```json
{
  "mcpServers": {
    "csla-net": {
      "command": "node",
      "args": ["path/to/mcp-client.js"],
      "env": {
        "CSLA_MCP_URL": "https://localhost:5001"
      }
    }
  }
}
```

### GitHub Copilot (via Extension)

Create an MCP extension that connects to the CSLA server:

```typescript
// Extension manifest
{
  "name": "csla-mcp-extension",
  "contributes": {
    "configuration": {
      "csla.mcpServer.url": {
        "type": "string", 
        "default": "https://localhost:5001",
        "description": "CSLA MCP Server URL"
      }
    }
  }
}
```

### Custom Integration

Use the provided client example:

```csharp
var client = new McpClient("https://localhost:5001");

// Initialize connection
await client.InitializeAsync();

// Get business object examples
var response = await client.GetCslaExampleAsync("business-object", "basic");
```

## Example Queries

Here are example queries that AI assistants can make:

### 1. Get Basic Business Object Examples

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

**Response**: Returns basic CSLA business object code examples.

### 2. Search for Validation Patterns

```json
{
  "method": "tools/call", 
  "params": {
    "name": "search_csla_examples",
    "arguments": {
      "query": "validation rules for email addresses"
    }
  }
}
```

**Response**: Returns examples containing email validation logic.

### 3. List Available Concepts

```json
{
  "method": "tools/call",
  "params": {
    "name": "list_csla_concepts",
    "arguments": {}
  }
}
```

**Response**: Returns all available CSLA concepts and their categories.

## AI Assistant Prompts

### For Code Generation

When an AI assistant needs to generate CSLA code, it can:

1. **Query for relevant examples**: Use `get_csla_example` or `search_csla_examples`
2. **Analyze the returned code**: Parse the official examples
3. **Generate appropriate code**: Create new code based on CSLA patterns
4. **Validate patterns**: Ensure the generated code follows CSLA conventions

### Example AI Workflow

```
User: "Create a CSLA business object for a Product with validation"

AI Assistant:
1. Query: get_csla_example("business-object", "basic")
2. Query: search_csla_examples("validation rules")  
3. Analyze returned examples
4. Generate Product class following CSLA patterns
5. Include appropriate validation rules
```

## Supported CSLA Concepts

The server currently provides examples for:

- **business-object**: Core business object patterns
- **data-portal**: Data access configuration  
- **validation**: Business rule validation
- **authorization**: Security and access control
- **serialization**: Object state management
- **ui-binding**: User interface binding
- **dependency-injection**: DI integration
- **blazor**: Blazor-specific patterns
- **aspnetcore**: ASP.NET Core integration

## Adding Custom Examples

To extend the server with your own examples:

1. Navigate to `csla-mcp.Server/CodeExamples/`
2. Create appropriate folder structure
3. Add `.cs` or `.md` files with your examples
4. Restart the server

Example structure:
```
CodeExamples/
??? business-object/
    ??? advanced/
        ??? MyCustomPattern.cs
```

## Troubleshooting

### Connection Issues

- Verify server is running: `curl https://localhost:5001/mcp/health`
- Check firewall settings
- Ensure correct URL and port

### No Examples Returned

- Check server logs for errors
- Verify examples exist in file system
- Test with direct API calls

### Performance Issues

- Monitor server resources
- Consider caching configuration
- Check for large example files

## Security Considerations

### Production Deployment

- Use HTTPS in production
- Implement authentication if needed
- Configure CORS appropriately
- Monitor server access logs

### API Access

- Consider rate limiting
- Implement API keys for public access
- Use reverse proxy for additional security

## Support

For issues with the CSLA MCP Server:

1. Check the [GitHub repository](https://github.com/your-org/csla-mcp)
2. Review server logs
3. Test with the included client example
4. Submit issues with reproduction steps