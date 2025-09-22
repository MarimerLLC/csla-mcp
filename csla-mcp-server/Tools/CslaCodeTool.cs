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

    [McpServerTool, Description("Searches CSLA .NET code samples and snippets for examples of how to implement code that makes use of #cslanet. Returns a JSON array of search results with scores, file names, and matching words with their counts, ordered by score. A larger score is a better match.")]
    public static string Search([Description("Keywords used to match against CSLA code samples and snippets. For example, read-write property, editable root, read-only list.")]string message)
    {
      Console.WriteLine($"[CslaCodeTool.Search] Called with message: '{message}'");
      
      try
      {
        Console.WriteLine($"[CslaCodeTool.Search] Using CodeSamplesPath: '{CodeSamplesPath}'");
        
        // Check if the CodeSamplesPath exists
        if (!Directory.Exists(CodeSamplesPath))
        {
          var error = $"Code samples path does not exist: {CodeSamplesPath}";
          Console.WriteLine($"[CslaCodeTool.Search] Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "PathNotFound", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        var csFiles = Directory.GetFiles(CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
        var mdFiles = Directory.GetFiles(CodeSamplesPath, "*.md", SearchOption.AllDirectories);
        var allFiles = csFiles.Concat(mdFiles);
        
        Console.WriteLine($"[CslaCodeTool.Search] Found {csFiles.Length} .cs files and {mdFiles.Length} .md files");
        
        // Extract words longer than 4 characters from the message
        var searchWords = message
          .Split(new char[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_' }, 
                 StringSplitOptions.RemoveEmptyEntries)
          .Where(word => word.Length > 3)
          .Select(word => word.ToLowerInvariant())
          .Distinct()
          .ToList();

        Console.WriteLine($"[CslaCodeTool.Search] Extracted search words: [{string.Join(", ", searchWords)}]");

        if (!searchWords.Any())
        {
          Console.WriteLine("[CslaCodeTool.Search] No search words found, returning empty results");
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
              Console.WriteLine($"[CslaCodeTool.Search] Found matches in '{Path.GetFileName(file)}' with score {totalScore}");
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
            Console.WriteLine($"[CslaCodeTool.Search] Error reading file {file}: {ex.Message}");
            // Continue processing other files
          }
        }
        
        // Order by score descending, then by filename
        var sortedResults = results.OrderByDescending(r => r.Score).ThenBy(r => r.FileName).ToList();
        
        Console.WriteLine($"[CslaCodeTool.Search] Returning {sortedResults.Count} results");
        
        return JsonSerializer.Serialize(sortedResults, new JsonSerializerOptions { WriteIndented = true });
      }
      catch (Exception ex)
      {
        var error = $"Search operation failed: {ex.Message}";
        Console.WriteLine($"[CslaCodeTool.Search] Error: {error}");
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

    [McpServerTool, Description("Fetches a specific CSLA .NET code sample or snippet by name. Returns the content of the file that can be used to properly implement code that uses #cslanet.")]
    public static string Fetch([Description("FileName from the search tool.")]string fileName)
    {
      Console.WriteLine($"[CslaCodeTool.Fetch] Called with fileName: '{fileName}'");
      
      try
      {
        Console.WriteLine($"[CslaCodeTool.Fetch] Using CodeSamplesPath: '{CodeSamplesPath}'");
        
        // Validate fileName to prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(fileName))
        {
          var error = "File name cannot be empty or null";
          Console.WriteLine($"[CslaCodeTool.Fetch] Error: {error}");
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
          Console.WriteLine($"[CslaCodeTool.Fetch] Error: {error}");
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
          Console.WriteLine($"[CslaCodeTool.Fetch] Error: {error}");
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "PathNotFound", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        var filePath = Path.Combine(CodeSamplesPath, fileName);
        Console.WriteLine($"[CslaCodeTool.Fetch] Attempting to read file: '{filePath}'");
        
        if (File.Exists(filePath))
        {
          var content = File.ReadAllText(filePath);
          Console.WriteLine($"[CslaCodeTool.Fetch] Successfully read file '{fileName}' ({content.Length} characters)");
          return content;
        }
        else
        {
          var error = $"File '{fileName}' not found in code samples directory";
          Console.WriteLine($"[CslaCodeTool.Fetch] Error: {error}");
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
        Console.WriteLine($"[CslaCodeTool.Fetch] Error: {error}");
        return JsonSerializer.Serialize(new ErrorResult 
        { 
          Error = "FetchFailed", 
          Message = error 
        }, new JsonSerializerOptions { WriteIndented = true });
      }
    }
  }
}
