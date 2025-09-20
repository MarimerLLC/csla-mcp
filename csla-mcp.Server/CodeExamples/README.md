# CSLA .NET Code Examples

This directory contains code examples for various CSLA .NET concepts and patterns. The examples are organized by concept and category to make them easy to find and use.

## Structure

```
CodeExamples/
??? business-object/          # Business object patterns
?   ??? basic/               # Basic business object examples
?   ??? advanced/            # Advanced patterns and scenarios
?   ??? patterns/            # Common design patterns
??? data-portal/             # Data portal configuration and usage
?   ??? basic/               # Basic data portal setup
?   ??? configuration/       # Advanced configuration options
?   ??? patterns/            # Data access patterns
??? validation/              # Validation rules and patterns
?   ??? basic/               # Basic property and object validation
?   ??? advanced/            # Custom validation rules
?   ??? patterns/            # Validation patterns
??? authorization/           # Security and authorization
?   ??? basic/               # Basic authorization rules
?   ??? roles/               # Role-based security
?   ??? patterns/            # Security patterns
??? serialization/           # Object serialization
??? ui-binding/              # UI data binding patterns
??? dependency-injection/    # DI patterns with CSLA
??? blazor/                  # Blazor-specific patterns
??? aspnetcore/              # ASP.NET Core integration
```

## File Types

- **`.cs` files**: Complete C# code examples
- **`.md` files**: Documentation and explanation files

## Adding New Examples

To add new examples:

1. Create the appropriate concept directory if it doesn't exist
2. Add the example files in the appropriate category subdirectory
3. Use meaningful file names that describe the example
4. Include both the code and any necessary documentation

## Usage with MCP

These examples are served by the CSLA MCP server through the following tools:

- `get_csla_example`: Get examples for a specific concept
- `list_csla_concepts`: List all available concepts and categories
- `search_csla_examples`: Search examples by content or name