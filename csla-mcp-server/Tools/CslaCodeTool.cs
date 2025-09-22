using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace CslaMcpServer.Tools
{
  [McpServerToolType]
  public class CslaCodeTool
  {
  public static string CodeSamplesPath { get; set; } = @"../csla-examples";

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

    public class ErrorResult
    {
      public string Error { get; set; } = string.Empty;
      public string Message { get; set; } = string.Empty;
    }

    [McpServerTool, Description("Searches the code samples and snippets for specific keywords. Returns a JSON array of search results with scores, file names, and matching words with their counts, ordered by score.")]
    public static string Search(string message)
    {
      try
      {
        // Check if the CodeSamplesPath exists
        if (!Directory.Exists(CodeSamplesPath))
        {
          var error = $"Code samples path does not exist: {CodeSamplesPath}";
          Console.WriteLine($"Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "PathNotFound", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        var csFiles = Directory.GetFiles(CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
        var mdFiles = Directory.GetFiles(CodeSamplesPath, "*.md", SearchOption.AllDirectories);
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
          try
          {
            var content = File.ReadAllText(file);
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
          catch (Exception ex)
          {
            Console.WriteLine($"Error reading file {file}: {ex.Message}");
            // Continue processing other files
          }
        }
        
        // Order by score descending, then by filename
        var sortedResults = results.OrderByDescending(r => r.Score).ThenBy(r => r.FileName).ToList();
        
        return JsonSerializer.Serialize(sortedResults, new JsonSerializerOptions { WriteIndented = true });
      }
      catch (Exception ex)
      {
        var error = $"Search operation failed: {ex.Message}";
        Console.WriteLine($"Error: {error}");
        return JsonSerializer.Serialize(new ErrorResult 
        { 
          Error = "SearchFailed", 
          Message = error 
        }, new JsonSerializerOptions { WriteIndented = true });
      }
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
      try
      {
        // Validate fileName to prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(fileName))
        {
          var error = "File name cannot be empty or null";
          Console.WriteLine($"Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "InvalidFileName", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        // Check for path traversal attempts
        if (fileName.Contains("..") || Path.IsPathRooted(fileName))
        {
          var error = $"Invalid file name: {fileName}. Only relative file names are allowed.";
          Console.WriteLine($"Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "InvalidFileName", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        // Check if the CodeSamplesPath exists
        if (!Directory.Exists(CodeSamplesPath))
        {
          var error = $"Code samples path does not exist: {CodeSamplesPath}";
          Console.WriteLine($"Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "PathNotFound", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        var filePath = Path.Combine(CodeSamplesPath, fileName);
        
        if (File.Exists(filePath))
        {
          return File.ReadAllText(filePath);
        }
        else
        {
          var error = $"File '{fileName}' not found in code samples directory";
          Console.WriteLine($"Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "FileNotFound", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }
      }
      catch (Exception ex)
      {
        var error = $"Fetch operation failed: {ex.Message}";
        Console.WriteLine($"Error: {error}");
        return JsonSerializer.Serialize(new ErrorResult 
        { 
          Error = "FetchFailed", 
          Message = error 
        }, new JsonSerializerOptions { WriteIndented = true });
      }
    }
  }
}
