namespace csla_mcp.Server;

public static class McpEndpointExtensions
{
    public static WebApplication MapMcpEndpoints(this WebApplication app)
    {
        // The MCP endpoints are handled by the McpController
        // This method can be used for additional MCP-specific routing if needed
        
        app.MapGet("/", () => Results.Redirect("/mcp/info"));
        
        return app;
    }
}