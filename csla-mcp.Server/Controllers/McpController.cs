using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace csla_mcp.Server.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly CslaMcpServer _mcpServer;
    private readonly ILogger<McpController> _logger;

    public McpController(CslaMcpServer mcpServer, ILogger<McpController> logger)
    {
        _mcpServer = mcpServer;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest([FromBody] JsonDocument request)
    {
        try
        {
            _logger.LogDebug("Received MCP request: {Request}", request.RootElement.GetRawText());
            
            var requestJson = request.RootElement.GetRawText();
            var response = await _mcpServer.HandleRequestAsync(requestJson);
            
            return Ok(JsonSerializer.Deserialize<object>(response));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in MCP request");
            return BadRequest(new { 
                jsonrpc = "2.0", 
                error = new { 
                    code = -32700, 
                    message = "Parse error", 
                    data = ex.Message 
                } 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling MCP request");
            return StatusCode(500, new { 
                jsonrpc = "2.0", 
                error = new { 
                    code = -32603, 
                    message = "Internal error", 
                    data = ex.Message 
                } 
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { 
            status = "healthy", 
            service = "csla-mcp-server",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    [HttpGet("info")]
    public IActionResult Info()
    {
        return Ok(new
        {
            name = "CSLA .NET MCP Server",
            version = "1.0.0",
            description = "MCP server providing CSLA .NET code examples and patterns",
            protocolVersion = "2024-11-05",
            capabilities = new[] { "tools" },
            tools = new[]
            {
                new { name = "get_csla_example", description = "Get CSLA code examples for specific concepts" },
                new { name = "list_csla_concepts", description = "List all available CSLA concepts and categories" },
                new { name = "search_csla_examples", description = "Search CSLA examples using natural language" }
            },
            endpoints = new
            {
                mcp = "/mcp",
                health = "/mcp/health",
                info = "/mcp/info"
            }
        });
    }

    [HttpGet("concepts")]
    public async Task<IActionResult> GetConcepts()
    {
        try
        {
            var codeExampleService = HttpContext.RequestServices.GetRequiredService<CodeExampleService>();
            var concepts = await codeExampleService.GetAvailableConcepts();
            
            return Ok(new
            {
                concepts = concepts.Select(c => new
                {
                    name = c.Name,
                    description = c.Description,
                    categories = c.Categories
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting concepts");
            return StatusCode(500, new { error = "Failed to get concepts", details = ex.Message });
        }
    }
}