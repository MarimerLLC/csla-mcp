using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text;
using Xunit;

namespace csla_mcp.Tests;

public class McpIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public McpIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task McpEndpoint_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/mcp/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }

    [Fact]
    public async Task McpEndpoint_ShouldReturnServerInfo()
    {
        // Act
        var response = await _client.GetAsync("/mcp/info");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var info = JsonDocument.Parse(content);
        
        Assert.Equal("CSLA .NET MCP Server", info.RootElement.GetProperty("name").GetString());
        Assert.Equal("1.0.0", info.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task McpEndpoint_ShouldHandleInitializeRequest()
    {
        // Arrange
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
                    name = "test-client",
                    version = "1.0.0"
                }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/mcp", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseDoc = JsonDocument.Parse(responseJson);
        
        var result = responseDoc.RootElement.GetProperty("result");
        Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
        Assert.Equal("csla-mcp-server", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task McpEndpoint_ShouldListTools()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/mcp", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseDoc = JsonDocument.Parse(responseJson);
        
        var result = responseDoc.RootElement.GetProperty("result");
        var tools = result.GetProperty("tools").EnumerateArray().ToList();
        Assert.Equal(3, tools.Count);
        
        var toolNames = tools.Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("get_csla_example", toolNames);
        Assert.Contains("list_csla_concepts", toolNames);
        Assert.Contains("search_csla_examples", toolNames);
    }

    [Fact]
    public async Task McpEndpoint_ShouldGetCslaExample()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "get_csla_example",
                arguments = new
                {
                    concept = "business-object"
                }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/mcp", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseDoc = JsonDocument.Parse(responseJson);
        
        var result = responseDoc.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.True(result.GetProperty("content").GetArrayLength() > 0);
    }
}