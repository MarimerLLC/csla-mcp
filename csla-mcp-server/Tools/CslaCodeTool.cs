using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CslaMcpServer.Tools
{
  [McpServerToolType]
  public class CslaCodeTool
  {
    private static readonly string _codeSamplesPath = @"s:\src\rdl\csla-mcp\csla-examples\";

    [McpServerTool, Description("Searches the code samples and snippets for specific keywords. Returns a comma-separated list of file names with matching content.")]
    public static string Search(string message)
    {
      var files = Directory.GetFiles(_codeSamplesPath, "*.cs", SearchOption.AllDirectories);
      var matches = new List<string>();
      foreach (var file in files)
      {
        var content = File.ReadAllText(file);
        if (content.Contains(message, StringComparison.OrdinalIgnoreCase))
        {
          matches.Add(Path.GetFileName(file));
        }
      }
      return string.Join(", ", matches);
    }

    [McpServerTool, Description("Fetches a specific code sample or snippet by name. Returns the content of the file.")]
    public static string Fetch(string fileName)
    {
      var filePath = Path.Combine(_codeSamplesPath, fileName);
      if (File.Exists(filePath))
      {
        return File.ReadAllText(filePath);
      }
      else
      {
        return $"File '{fileName}' not found.";
      }
    }
  }
}
