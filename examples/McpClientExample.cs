using System.Text.Json;
using System.Text;

namespace csla_mcp.Client;

/// <summary>
/// Example client for testing the CSLA MCP Server
/// </summary>
public class McpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public McpClient(string baseUrl = "https://localhost:7000")
    {
        _httpClient = new HttpClient();
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Initialize the MCP connection
    /// </summary>
    public async Task<JsonDocument> InitializeAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "csla-mcp-test-client",
                    version = "1.0.0"
                }
            }
        };

        return await SendRequestAsync(request);
    }

    /// <summary>
    /// List available MCP tools
    /// </summary>
    public async Task<JsonDocument> ListToolsAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { }
        };

        return await SendRequestAsync(request);
    }

    /// <summary>
    /// Get CSLA examples for a specific concept
    /// </summary>
    public async Task<JsonDocument> GetCslaExampleAsync(string concept, string? category = null)
    {
        var arguments = new Dictionary<string, object> { ["concept"] = concept };
        if (!string.IsNullOrEmpty(category))
        {
            arguments["category"] = category;
        }

        var request = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "get_csla_example",
                arguments
            }
        };

        return await SendRequestAsync(request);
    }

    /// <summary>
    /// List all available CSLA concepts
    /// </summary>
    public async Task<JsonDocument> ListCslaConceptsAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "list_csla_concepts",
                arguments = new { }
            }
        };

        return await SendRequestAsync(request);
    }

    /// <summary>
    /// Search for CSLA examples
    /// </summary>
    public async Task<JsonDocument> SearchCslaExamplesAsync(string query)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "tools/call",
            @params = new
            {
                name = "search_csla_examples",
                arguments = new { query }
            }
        };

        return await SendRequestAsync(request);
    }

    private async Task<JsonDocument> SendRequestAsync(object request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/mcp", content);
        
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(responseJson);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Example console application demonstrating MCP client usage
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var client = new McpClient();

        try
        {
            Console.WriteLine("CSLA MCP Server Test Client");
            Console.WriteLine("============================");

            // Initialize connection
            Console.WriteLine("\n1. Initializing MCP connection...");
            var initResponse = await client.InitializeAsync();
            Console.WriteLine($"Response: {initResponse.RootElement.GetRawText()}");

            // List available tools
            Console.WriteLine("\n2. Listing available tools...");
            var toolsResponse = await client.ListToolsAsync();
            Console.WriteLine($"Response: {toolsResponse.RootElement.GetRawText()}");

            // List CSLA concepts
            Console.WriteLine("\n3. Listing CSLA concepts...");
            var conceptsResponse = await client.ListCslaConceptsAsync();
            if (conceptsResponse.RootElement.TryGetProperty("content", out var content))
            {
                var text = content[0].GetProperty("text").GetString();
                Console.WriteLine($"Available concepts:\n{text}");
            }

            // Get business object examples
            Console.WriteLine("\n4. Getting business object examples...");
            var exampleResponse = await client.GetCslaExampleAsync("business-object", "basic");
            if (exampleResponse.RootElement.TryGetProperty("content", out var exampleContent))
            {
                foreach (var item in exampleContent.EnumerateArray())
                {
                    var text = item.GetProperty("text").GetString();
                    Console.WriteLine($"Example:\n{text}\n");
                }
            }

            // Search for validation examples
            Console.WriteLine("\n5. Searching for validation examples...");
            var searchResponse = await client.SearchCslaExamplesAsync("validation rules");
            if (searchResponse.RootElement.TryGetProperty("content", out var searchContent))
            {
                foreach (var item in searchContent.EnumerateArray())
                {
                    var text = item.GetProperty("text").GetString();
                    Console.WriteLine($"Search result:\n{text}\n");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}