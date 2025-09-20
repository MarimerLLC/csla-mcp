var builder = DistributedApplication.CreateBuilder(args);

// Add the MCP server
var mcpServer = builder.AddProject<Projects.csla_mcp_Server>("mcp-server");

builder.Build().Run();
