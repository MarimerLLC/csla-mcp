using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using CslaMcpServer.Services;

namespace CslaMcpServer.Tools
{
  [McpServerToolType]
  public class CslaCodeTool
  {
  public static string CodeSamplesPath { get; set; } = @"../csla-examples";
    public static VectorStoreService? VectorStore { get; set; }

    public class SearchResult
    {
      public double Score { get; set; }
      public string FileName { get; set; } = string.Empty;
    }

    public class SemanticMatch
    {
      public string FileName { get; set; } = string.Empty;
      public float SimilarityScore { get; set; }
    }

    public class ConsolidatedSearchResult
    {
      public string FileName { get; set; } = string.Empty;
      public double Score { get; set; }
      public double? VectorScore { get; set; }
      public double? WordScore { get; set; }
    }

    public class ErrorResult
    {
      public string Error { get; set; } = string.Empty;
      public string Message { get; set; } = string.Empty;
    }

    [McpServerTool, Description("Searches CSLA .NET code samples and snippets for examples of how to implement code that makes use of #cslanet. Returns a JSON array of consolidated search results that merge semantic and word search scores.")]
    public static string Search(
      [Description("Keywords used to match against CSLA code samples and snippets. For example, read-write property, editable root, read-only list.")]string message,
      [Description("Optional CSLA version number (e.g., 9 or 10). If not provided, defaults to the highest version available.")]int? version = null)
    {
      Console.WriteLine($"[CslaCodeTool.Search] Called with message: '{message}', version: {version?.ToString() ?? "not specified (will use highest)"}");
      
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

        // If version not specified, detect highest version from subdirectories
        if (!version.HasValue)
        {
          version = GetHighestVersionFromFileSystem();
          Console.WriteLine($"[CslaCodeTool.Search] Version not specified, defaulting to highest: v{version}");
        }

        var csFiles = Directory.GetFiles(CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
        var mdFiles = Directory.GetFiles(CodeSamplesPath, "*.md", SearchOption.AllDirectories);
        var allFiles = csFiles.Concat(mdFiles);
        
        Console.WriteLine($"[CslaCodeTool.Search] Found {csFiles.Length} .cs files and {mdFiles.Length} .md files");
        
        // Extract words from the message, preserving order for multi-word combinations
        var allWords = message
          .Split(new char[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_' }, 
                 StringSplitOptions.RemoveEmptyEntries)
          .Where(word => word.Length > 3)
          .Select(word => word.ToLowerInvariant())
          .ToList();

        // Create single words (remove duplicates)
        var singleWords = allWords.Distinct().ToList();
        
        // Create 2-word combinations from adjacent words
        var twoWordPhrases = new List<string>();
        for (int i = 0; i < allWords.Count - 1; i++)
        {
          var phrase = $"{allWords[i]} {allWords[i + 1]}";
          if (!twoWordPhrases.Contains(phrase))
          {
            twoWordPhrases.Add(phrase);
          }
        }

        // Combine single words and 2-word phrases
        var searchTerms = new List<string>();
        searchTerms.AddRange(singleWords);
        searchTerms.AddRange(twoWordPhrases);

        Console.WriteLine($"[CslaCodeTool.Search] Extracted single words: [{string.Join(", ", singleWords)}]");
        Console.WriteLine($"[CslaCodeTool.Search] Extracted 2-word phrases: [{string.Join(", ", twoWordPhrases)}]");
        Console.WriteLine($"[CslaCodeTool.Search] Total search terms: {searchTerms.Count}");

        if (!searchTerms.Any())
        {
          Console.WriteLine("[CslaCodeTool.Search] No search terms found, returning empty results");
          return JsonSerializer.Serialize(new List<ConsolidatedSearchResult>());
        }

        // Create tasks for parallel execution
        var wordSearchTask = Task.Run(() => PerformWordSearch(allFiles, searchTerms, version.Value));
        var semanticSearchTask = Task.Run(() => PerformSemanticSearch(message, version));

        // Wait for both tasks to complete
        Task.WaitAll(wordSearchTask, semanticSearchTask);

        var wordMatches = wordSearchTask.Result;
        var semanticMatches = semanticSearchTask.Result;
        
        // Create consolidated results
        var consolidatedResults = ConsolidateSearchResults(semanticMatches, wordMatches);
        
        Console.WriteLine($"[CslaCodeTool.Search] Returning {consolidatedResults.Count} consolidated results");
        
        return JsonSerializer.Serialize(consolidatedResults, new JsonSerializerOptions { WriteIndented = true });
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

    private static List<ConsolidatedSearchResult> ConsolidateSearchResults(List<SemanticMatch> semanticMatches, List<SearchResult> wordMatches)
    {
      Console.WriteLine("[CslaCodeTool.ConsolidateSearchResults] Starting result consolidation");
      
      var consolidatedResults = new Dictionary<string, ConsolidatedSearchResult>();
      
      // Add semantic matches
      foreach (var semantic in semanticMatches)
      {
        if (!consolidatedResults.ContainsKey(semantic.FileName))
        {
          consolidatedResults[semantic.FileName] = new ConsolidatedSearchResult
          {
            FileName = semantic.FileName,
            VectorScore = semantic.SimilarityScore,
            WordScore = null,
            Score = semantic.SimilarityScore
          };
        }
      }
      
      // Add word matches and merge with semantic matches
      foreach (var word in wordMatches)
      {
        if (consolidatedResults.ContainsKey(word.FileName))
        {
          // File exists in both - calculate average
          var existing = consolidatedResults[word.FileName];
          existing.WordScore = word.Score;
          existing.Score = (existing.VectorScore.GetValueOrDefault(0) + word.Score) / 2.0;
          Console.WriteLine($"[CslaCodeTool.ConsolidateSearchResults] Merged scores for '{word.FileName}': Vector={existing.VectorScore:F3}, Word={existing.WordScore:F3}, Average={existing.Score:F3}");
        }
        else
        {
          // File only in word matches
          consolidatedResults[word.FileName] = new ConsolidatedSearchResult
          {
            FileName = word.FileName,
            VectorScore = null,
            WordScore = word.Score,
            Score = word.Score
          };
        }
      }
      
      // Sort by score descending, then by filename
      var sortedResults = consolidatedResults.Values
        .OrderByDescending(r => r.Score)
        .ThenBy(r => r.FileName)
        .ToList();
      
      Console.WriteLine($"[CslaCodeTool.ConsolidateSearchResults] Consolidated {consolidatedResults.Count} unique files");
      Console.WriteLine($"[CslaCodeTool.ConsolidateSearchResults] Files with both scores: {consolidatedResults.Values.Count(r => r.VectorScore.HasValue && r.WordScore.HasValue)}");
      Console.WriteLine($"[CslaCodeTool.ConsolidateSearchResults] Files with only vector scores: {consolidatedResults.Values.Count(r => r.VectorScore.HasValue && !r.WordScore.HasValue)}");
      Console.WriteLine($"[CslaCodeTool.ConsolidateSearchResults] Files with only word scores: {consolidatedResults.Values.Count(r => !r.VectorScore.HasValue && r.WordScore.HasValue)}");
      
      return sortedResults;
    }

    private static List<SearchResult> PerformWordSearch(IEnumerable<string> allFiles, List<string> searchTerms, int version)
    {
      Console.WriteLine($"[CslaCodeTool.PerformWordSearch] Starting word search for version {version}");
      var results = new List<SearchResult>();
      
      foreach (var file in allFiles)
      {
        try
        {
          // Get relative path from CodeSamplesPath
          var relativePath = Path.GetRelativePath(CodeSamplesPath, file);
          
          // Filter by version: include if in top directory (common) or in matching version subdirectory
          var isCommon = !relativePath.Contains(Path.DirectorySeparatorChar);
          var isMatchingVersion = relativePath.StartsWith($"v{version}{Path.DirectorySeparatorChar}");
          
          if (!isCommon && !isMatchingVersion)
          {
            continue; // Skip files from other version directories
          }
          
          var content = File.ReadAllText(file);
          var totalScore = 0;
          
          foreach (var term in searchTerms)
          {
            var count = CountWordOccurrences(content, term);
            if (count > 0)
            {
              // Give higher weight to multi-word phrases
              var weight = term.Contains(' ') ? 2 : 1;
              totalScore += count * weight;
              Console.WriteLine($"[CslaCodeTool.PerformWordSearch] Found {count} matches for '{term}' in '{relativePath}' (weight: {weight})");
            }
          }
          
          if (totalScore > 0)
          {
            Console.WriteLine($"[CslaCodeTool.PerformWordSearch] Found matches in '{relativePath}' with total score {totalScore}");
            results.Add(new SearchResult
            {
              Score = totalScore,
              FileName = relativePath.Replace("\\", "/") // Normalize path separators
            });
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[CslaCodeTool.PerformWordSearch] Error reading file {file}: {ex.Message}");
          // Continue processing other files
        }
      }
      
      // Normalize scores using max-score normalization
      var normalizedResults = NormalizeWordSearchResults(results);
      
      // Order by score descending, then by filename
      var sortedResults = normalizedResults.OrderByDescending(r => r.Score).ThenBy(r => r.FileName).ToList();
      
      Console.WriteLine($"[CslaCodeTool.PerformWordSearch] Found {sortedResults.Count} word match results");
      return sortedResults;
    }

    private static List<SearchResult> NormalizeWordSearchResults(List<SearchResult> results)
    {
      if (!results.Any())
      {
        Console.WriteLine("[CslaCodeTool.NormalizeWordSearchResults] No results to normalize");
        return results;
      }
      
      var maxScore = results.Max(r => r.Score);
      Console.WriteLine($"[CslaCodeTool.NormalizeWordSearchResults] Normalizing {results.Count} results with max score: {maxScore}");
      
      if (maxScore <= 0)
      {
        Console.WriteLine("[CslaCodeTool.NormalizeWordSearchResults] Max score is 0 or negative, returning original results");
        return results;
      }
      
      var normalizedResults = results.Select(r => new SearchResult
      {
        FileName = r.FileName,
        Score = r.Score / maxScore
      }).ToList();
      
      Console.WriteLine($"[CslaCodeTool.NormalizeWordSearchResults] Normalized scores range from {normalizedResults.Min(r => r.Score):F3} to {normalizedResults.Max(r => r.Score):F3}");
      
      return normalizedResults;
    }

    private static List<SemanticMatch> PerformSemanticSearch(string message, int? version)
    {
      Console.WriteLine($"[CslaCodeTool.PerformSemanticSearch] Starting semantic search for version {version}");
      var semanticMatches = new List<SemanticMatch>();
      
      if (VectorStore != null && VectorStore.IsReady())
      {
        Console.WriteLine("[CslaCodeTool.PerformSemanticSearch] Performing semantic search");
        var semanticResults = VectorStore.SearchAsync(message, version, topK: 10).GetAwaiter().GetResult();
        semanticMatches = semanticResults.Select(r => new SemanticMatch
        {
          FileName = r.FileName,
          SimilarityScore = r.SimilarityScore
        }).ToList();
        Console.WriteLine($"[CslaCodeTool.PerformSemanticSearch] Found {semanticMatches.Count} semantic matches");
      }
      else if (VectorStore != null && !VectorStore.IsHealthy())
      {
        Console.WriteLine("[CslaCodeTool.PerformSemanticSearch] Semantic search unavailable due to Azure OpenAI configuration issues");
      }
      else
      {
        Console.WriteLine("[CslaCodeTool.PerformSemanticSearch] Vector store not available, using keyword search only");
      }
      
      return semanticMatches;
    }

    private static int GetHighestVersionFromFileSystem()
    {
      try
      {
        var versionDirs = Directory.GetDirectories(CodeSamplesPath, "v*")
          .Select(dir => Path.GetFileName(dir))
          .Where(name => name.StartsWith("v") && int.TryParse(name.Substring(1), out _))
          .Select(name => int.Parse(name.Substring(1)))
          .ToList();
        
        if (versionDirs.Any())
        {
          var highest = versionDirs.Max();
          Console.WriteLine($"[CslaCodeTool.GetHighestVersionFromFileSystem] Found versions: [{string.Join(", ", versionDirs.OrderBy(v => v))}], highest: {highest}");
          return highest;
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[CslaCodeTool.GetHighestVersionFromFileSystem] Error detecting versions: {ex.Message}");
      }
      
      // No version directories found - return a reasonable default
      // This will be used when all content is in the root directory (common to all versions)
      Console.WriteLine("[CslaCodeTool.GetHighestVersionFromFileSystem] No version directories found, defaulting to latest known CSLA version");
      return 10; // Default fallback when no version subdirectories exist
    }

    private static int CountWordOccurrences(string content, string searchTerm)
    {
      // Handle multi-word phrases
      if (searchTerm.Contains(' '))
      {
        // For phrases, we need to ensure word boundaries at the beginning and end
        var escapedTerm = Regex.Escape(searchTerm);
        var pattern = $@"\b{escapedTerm}\b";
        var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
        return matches.Count;
      }
      else
      {
        // For single words, use word boundaries to ensure we only match complete words
        var pattern = $@"\b{Regex.Escape(searchTerm)}\b";
        var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
        return matches.Count;
      }
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

        // Normalize path separator to system default
        var normalizedFileName = fileName.Replace("/", Path.DirectorySeparatorChar.ToString());
        var filePath = Path.Combine(CodeSamplesPath, normalizedFileName);
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
