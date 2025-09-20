using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace csla_mcp.Server;

public class CslaMcpServer : McpServer
{
    private readonly CodeExampleService _codeExampleService;
    private readonly ILogger<CslaMcpServer> _logger;

    public CslaMcpServer(CodeExampleService codeExampleService, ILogger<CslaMcpServer> logger)
        : base(new ServerInfo
        {
            Name = "CSLA .NET MCP Server",
            Version = "1.0.0"
        })
    {
        _codeExampleService = codeExampleService;
        _logger = logger;
        
        RegisterTools();
    }

    private void RegisterTools()
    {
        RegisterTool(new Tool
        {
            Name = "get_csla_example",
            Description = "Get CSLA code examples for specific concepts",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    concept = new
                    {
                        type = "string",
                        description = "The CSLA concept to get examples for (e.g., 'BusinessBase', 'DataAccess', 'Validation')"
                    },
                    category = new
                    {
                        type = "string",
                        description = "Optional category to filter examples within the concept"
                    }
                },
                required = new[] { "concept" }
            }
        }, HandleGetCslaExample);

        RegisterTool(new Tool
        {
            Name = "list_csla_concepts",
            Description = "List all available CSLA concepts and categories",
            InputSchema = new
            {
                type = "object",
                properties = new { }
            }
        }, HandleListCslaConcepts);

        RegisterTool(new Tool
        {
            Name = "search_csla_examples",
            Description = "Search CSLA examples using natural language",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "Natural language search query for CSLA examples"
                    }
                },
                required = new[] { "query" }
            }
        }, HandleSearchCslaExamples);
    }

    private async Task<CallToolResult> HandleGetCslaExample(CallToolRequest request)
    {
        try
        {
            var arguments = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(request.Params.Arguments));
            
            var concept = arguments.TryGetProperty("concept", out var conceptElement) 
                ? conceptElement.GetString() 
                : null;
                
            var category = arguments.TryGetProperty("category", out var categoryElement) && !categoryElement.ValueKind.Equals(JsonValueKind.Null)
                ? categoryElement.GetString() 
                : null;

            if (string.IsNullOrWhiteSpace(concept))
            {
                return new CallToolResult
                {
                    Content = new[]
                    {
                        new Content
                        {
                            Type = "text",
                            Text = "Error: 'concept' parameter is required. Use 'list_csla_concepts' to see available concepts."
                        }
                    },
                    IsError = true
                };
            }

            _logger.LogDebug("Getting examples for concept: {Concept}, category: {Category}", concept, category);

            var examples = await _codeExampleService.GetExamplesByConcept(concept!, category);
            
            if (!examples.Any())
            {
                return new CallToolResult
                {
                    Content = new[]
                    {
                        new Content
                        {
                            Type = "text",
                            Text = $"No examples found for concept '{concept}'" + 
                                   (category != null ? $" in category '{category}'" : "") + 
                                   ". Use the 'list_csla_concepts' tool to see available concepts and categories."
                        }
                    },
                    IsError = false
                };
            }
            
            return new CallToolResult
            {
                Content = examples.Select(ex => new Content
                {
                    Type = "text",
                    Text = $"# {ex.Name}\n\n**Concept**: {ex.Concept}  \n**Category**: {ex.Category}  \n**File**: {ex.FilePath}\n\n{ex.Content}"
                }).ToArray(),
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CSLA example");
            return new CallToolResult
            {
                Content = new[]
                {
                    new Content
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }

    private async Task<CallToolResult> HandleListCslaConcepts(CallToolRequest request)
    {
        try
        {
            _logger.LogDebug("Listing CSLA concepts");
            
            var concepts = await _codeExampleService.GetAvailableConcepts();
            
            var conceptList = string.Join("\n\n", concepts.Select(c => 
                $"## {c.Name}\n{c.Description}\n**Categories**: {string.Join(", ", c.Categories)}"));

            return new CallToolResult
            {
                Content = new[]
                {
                    new Content
                    {
                        Type = "text",
                        Text = $"# Available CSLA Concepts\n\n{conceptList}"
                    }
                },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing CSLA concepts");
            return new CallToolResult
            {
                Content = new[]
                {
                    new Content
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }

    private async Task<CallToolResult> HandleSearchCslaExamples(CallToolRequest request)
    {
        try
        {
            var arguments = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(request.Params.Arguments));
            var query = arguments.GetProperty("query").GetString();
            
            if (string.IsNullOrWhiteSpace(query))
            {
                return new CallToolResult
                {
                    Content = new[]
                    {
                        new Content
                        {
                            Type = "text",
                            Text = "Error: 'query' parameter is required."
                        }
                    },
                    IsError = true
                };
            }
            
            _logger.LogDebug("Searching examples with query: {Query}", query);
            
            var examples = await _codeExampleService.SearchExamples(query!);
            
            if (!examples.Any())
            {
                return new CallToolResult
                {
                    Content = new[]
                    {
                        new Content
                        {
                            Type = "text",
                            Text = $"No examples found matching the query '{query}'. Try using broader search terms or use 'list_csla_concepts' to see available concepts."
                        }
                    },
                    IsError = false
                };
            }
            
            return new CallToolResult
            {
                Content = examples.Select(ex => new Content
                {
                    Type = "text",
                    Text = $"# {ex.Name} (Search Result)\n\n**Concept**: {ex.Concept}  \n**Category**: {ex.Category}  \n**File**: {ex.FilePath}\n\n{ex.Content}"
                }).ToArray(),
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CSLA examples");
            return new CallToolResult
            {
                Content = new[]
                {
                    new Content
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }
}