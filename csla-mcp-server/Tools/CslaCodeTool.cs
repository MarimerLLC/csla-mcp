using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace CslaMcpServer.Tools
{
  [McpServerToolType]
  public class CslaCodeTool
  {
    private static readonly string _codeSamplesPath = @"s:\src\rdl\csla-mcp\csla-examples\";

    public class WordMatch
    {
      public string Word { get; set; } = string.Empty;
      public int Count { get; set; }
    }

    public class SearchResult
    {
      public int Score { get; set; }
      public string FileName { get; set; } = string.Empty;
      public List<WordMatch> MatchingWords { get; set; } = new List<WordMatch>();
    }

    [McpServerTool, Description("Searches the code samples and snippets for specific keywords. Returns a JSON array of search results with scores, file names, and matching words with their counts, ordered by score.")]
    public static string Search(string message)
    {
      var csFiles = Directory.GetFiles(_codeSamplesPath, "*.cs", SearchOption.AllDirectories);
      var mdFiles = Directory.GetFiles(_codeSamplesPath, "*.md", SearchOption.AllDirectories);
      var allFiles = csFiles.Concat(mdFiles);
      
      // Extract words longer than 4 characters from the message
      var searchWords = message
        .Split(new char[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_' }, 
               StringSplitOptions.RemoveEmptyEntries)
        .Where(word => word.Length > 3)
        .Select(word => word.ToLowerInvariant())
        .Distinct()
        .ToList();

      if (!searchWords.Any())
      {
        return JsonSerializer.Serialize(new List<SearchResult>());
      }

      var results = new List<SearchResult>();
      
      foreach (var file in allFiles)
      {
        var content = File.ReadAllText(file).ToLowerInvariant();
        var matchingWords = new List<WordMatch>();
        var totalScore = 0;
        
        foreach (var word in searchWords)
        {
          var count = CountWordOccurrences(content, word);
          if (count > 0)
          {
            matchingWords.Add(new WordMatch { Word = word, Count = count });
            totalScore += count;
          }
        }
        
        if (totalScore > 0)
        {
          results.Add(new SearchResult
          {
            Score = totalScore,
            FileName = Path.GetFileName(file),
            MatchingWords = matchingWords
          });
        }
      }
      
      // Order by score descending, then by filename
      var sortedResults = results.OrderByDescending(r => r.Score).ThenBy(r => r.FileName).ToList();
      
      return JsonSerializer.Serialize(sortedResults, new JsonSerializerOptions { WriteIndented = true });
    }

    private static int CountWordOccurrences(string content, string word)
    {
      int count = 0;
      int index = 0;
      
      while ((index = content.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) != -1)
      {
        count++;
        index += word.Length;
      }
      
      return count;
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
