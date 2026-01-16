using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CslaMcpServer.Services;

namespace CslaMcpServer.Tools
{
  [McpServerToolType]
  public sealed class CslaCodeTool(ILogger<CslaCodeTool> logger)
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

    public class MultipleFilesResult
    {
      public string Error { get; set; } = "MultipleVersionsFound";
      public string Message { get; set; } = string.Empty;
      public List<string> AvailableFiles { get; set; } = new List<string>();
    }

    private const string mcpServerDescription = @"
      Authoritative CSLA .NET (cslanet) framework knowledge base. Covers business objects
      (BusinessBase, ReadOnlyBase, CommandBase, EditableRoot, EditableChild),
      data portal operations (Create, Fetch, Insert, Update, Delete), validation
      and business rules, authorization, property implementations (managed/unmanaged),
      and mobile object patterns. Returns JSON array with file name and relevance
      scores. Fetch `README.md` for detailed usage guidance. Use `Glossary.md` for
      CSLA terminology.";
    private const string searchDescription = @"
      Natural-language query describing the CSLA concept or pattern you need. Use
      short phrases such as editable root save, data portal authorization rule, or
      read-only list fetch. Combine the most relevant keywords; the string feeds
      both semantic and keyword search scorers.";
    private const string fetchDescription = @"
      Fetches a specific CSLA.NET document, code sample, or snippet by name. Returns 
      the content of the file that can be used to understand concepts and properly 
      implement code that uses CSLA .NET.";

    [McpServerTool, Description(mcpServerDescription)]
    public async Task<string> Search(
      [Description(searchDescription)]string message,
      [Description("Optional CSLA version number (e.g., 9 or 10). If not provided, defaults to the highest version available.")]int? version = null)
    {
      logger.LogInformation("[CslaCodeTool.Search] Called with message: '{Message}', version: {Version}", message, version?.ToString() ?? "not specified (will use highest)");
      
      try
      {
        logger.LogInformation("[CslaCodeTool.Search] Using CodeSamplesPath: '{Path}'", CodeSamplesPath);
        
        // Check if the CodeSamplesPath exists
        if (!Directory.Exists(CodeSamplesPath))
        {
          var error = $"Code samples path does not exist: {CodeSamplesPath}";
          logger.LogError("[CslaCodeTool.Search] Error: {Error}", error);
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
          logger.LogInformation("[CslaCodeTool.Search] Version not specified, defaulting to highest: v{Version}", version);
        }

        var csFiles = Directory.GetFiles(CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
        var mdFiles = Directory.GetFiles(CodeSamplesPath, "*.md", SearchOption.AllDirectories);
        var allFiles = csFiles.Concat(mdFiles);
        
        logger.LogInformation("[CslaCodeTool.Search] Found {CsFiles} .cs files and {MdFiles} .md files", csFiles.Length, mdFiles.Length);
        
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

        logger.LogInformation("[CslaCodeTool.Search] Extracted single words: [{Words}]", string.Join(", ", singleWords));
        logger.LogInformation("[CslaCodeTool.Search] Extracted2-word phrases: [{Phrases}]", string.Join(", ", twoWordPhrases));
        logger.LogInformation("[CslaCodeTool.Search] Total search terms: {Count}", searchTerms.Count);

        if (!searchTerms.Any())
        {
          logger.LogInformation("[CslaCodeTool.Search] No search terms found, returning empty results");
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
        
        logger.LogInformation("[CslaCodeTool.Search] Returning {Count} consolidated results", consolidatedResults.Count);
        
        return JsonSerializer.Serialize(consolidatedResults, new JsonSerializerOptions { WriteIndented = true });
      }
      catch (Exception ex)
      {
        var error = $"Search operation failed: {ex.Message}";
        logger.LogError(ex, "[CslaCodeTool.Search] Error: {Error}", error);
        return JsonSerializer.Serialize(new ErrorResult 
        { 
          Error = "SearchFailed", 
          Message = error 
        }, new JsonSerializerOptions { WriteIndented = true });
      }
    }

    private List<ConsolidatedSearchResult> ConsolidateSearchResults(List<SemanticMatch> semanticMatches, List<SearchResult> wordMatches)
    {
      logger.LogInformation("[CslaCodeTool.ConsolidateSearchResults] Starting result consolidation");
      
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
          logger.LogInformation("[CslaCodeTool.ConsolidateSearchResults] Merged scores for '{File}': Vector={Vector:F3}, Word={Word:F3}, Average={Avg:F3}", word.FileName, existing.VectorScore, existing.WordScore, existing.Score);
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
      
      logger.LogInformation("[CslaCodeTool.ConsolidateSearchResults] Consolidated {Count} unique files", consolidatedResults.Count);
      logger.LogInformation("[CslaCodeTool.ConsolidateSearchResults] Files with both scores: {Count}", consolidatedResults.Values.Count(r => r.VectorScore.HasValue && r.WordScore.HasValue));
      logger.LogInformation("[CslaCodeTool.ConsolidateSearchResults] Files with only vector scores: {Count}", consolidatedResults.Values.Count(r => r.VectorScore.HasValue && !r.WordScore.HasValue));
      logger.LogInformation("[CslaCodeTool.ConsolidateSearchResults] Files with only word scores: {Count}", consolidatedResults.Values.Count(r => !r.VectorScore.HasValue && r.WordScore.HasValue));
      
      return sortedResults;
    }

    private List<SearchResult> PerformWordSearch(IEnumerable<string> allFiles, List<string> searchTerms, int version)
    {
      logger.LogInformation("[CslaCodeTool.PerformWordSearch] Starting word search for version {Version}", version);
      var results = new List<SearchResult>();

      // Collect candidate documents with version filtering
      var candidateDocs = new List<(string RelativePath, string Content, int DocLength)>();
      foreach (var file in allFiles)
      {
        try
        {
          var relativePath = Path.GetRelativePath(CodeSamplesPath, file);
          var isCommon = !relativePath.Contains(Path.DirectorySeparatorChar);
          var isMatchingVersion = relativePath.StartsWith($"v{version}{Path.DirectorySeparatorChar}");

          if (!isCommon && !isMatchingVersion)
            continue;

          var content = File.ReadAllText(file);
          // Document length as number of word tokens
          var docLength = GetDocumentLength(content);
          candidateDocs.Add((relativePath.Replace("\\", "/"), content, docLength));
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "[CslaCodeTool.PerformWordSearch] Error reading file {File}: {Message}", file, ex.Message);
        }
      }

      var N = candidateDocs.Count;
      if (N == 0)
      {
        logger.LogInformation("[CslaCodeTool.PerformWordSearch] No candidate documents after filtering");
        return results;
      }

      var avgDocLength = candidateDocs.Average(d => d.DocLength);
      logger.LogInformation("[CslaCodeTool.PerformWordSearch] Candidate documents: {N}, AvgDocLength: {Avg:F2}", N, avgDocLength);

      // Compute document frequency n for each term
      var docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      foreach (var term in searchTerms)
      {
        int n = 0;
        foreach (var doc in candidateDocs)
        {
          var f = CountWordOccurrences(doc.Content, term);
          if (f > 0) n++;
        }
        docFreq[term] = n;
        logger.LogInformation("[CslaCodeTool.PerformWordSearch] Term '{Term}' appears in {N}/{Total} documents", term, n, N);
      }

      // Compute BM25 score per document as sum over query terms
      foreach (var doc in candidateDocs)
      {
        double score = 0.0;
        foreach (var term in searchTerms)
        {
          var f = CountWordOccurrences(doc.Content, term);
          var n = docFreq[term];

          if (f <= 0 || n <= 0)
            continue;

          score += ComputeBM25(
            f: f,
            n: n,
            N: N,
            docLength: doc.DocLength,
            avgDocLength: avgDocLength
          );
        }

        if (score > 0)
        {
          results.Add(new SearchResult
          {
            FileName = doc.RelativePath,
            Score = score
          });
        }
      }

      // Normalize scores using max-score normalization (keeps behavior consistent with vector-score averaging)
      var normalizedResults = NormalizeWordSearchResults(results);
      var sortedResults = normalizedResults.OrderByDescending(r => r.Score).ThenBy(r => r.FileName).ToList();

      logger.LogInformation("[CslaCodeTool.PerformWordSearch] Found {Count} word match results (BM25)", sortedResults.Count);
      return sortedResults;
    }

    // BM25 scoring for a single term
    private static double ComputeBM25(
      int f, int n, int N, int docLength, double avgDocLength,
      double k1 = 1.5, double b = 0.75)
    {
      // IDF with +1 guard to avoid negative infinity if n == N
      double idf = Math.Log((N - n + 0.5) / (n + 0.5) + 1);
      double numerator = f * (k1 + 1);
      double denominator = f + k1 * (1 - b + b * docLength / avgDocLength);
      return idf * (numerator / denominator);
    }

    // Counts number of word tokens in a document (approximate)
    private static int GetDocumentLength(string content)
    {
      // Count word-like tokens (alphanumeric and underscore), culture-invariant
      var matches = Regex.Matches(content, @"\b\w+\b", RegexOptions.CultureInvariant);
      return matches.Count;
    }

    private List<SearchResult> NormalizeWordSearchResults(List<SearchResult> results)
    {
      if (!results.Any())
      {
        logger.LogInformation("[CslaCodeTool.NormalizeWordSearchResults] No results to normalize");
        return results;
      }
      
      var maxScore = results.Max(r => r.Score);
      logger.LogInformation("[CslaCodeTool.NormalizeWordSearchResults] Normalizing {Count} results with max score: {Max}", results.Count, maxScore);
      
      if (maxScore <= 0)
      {
        logger.LogInformation("[CslaCodeTool.NormalizeWordSearchResults] Max score is 0 or negative, returning original results");
        return results;
      }
      
      var normalizedResults = results.Select(r => new SearchResult
      {
        FileName = r.FileName,
        Score = r.Score / maxScore
      }).ToList();
      
      logger.LogInformation("[CslaCodeTool.NormalizeWordSearchResults] Normalized scores range from {Min:F3} to {Max:F3}", normalizedResults.Min(r => r.Score), normalizedResults.Max(r => r.Score));
      
      return normalizedResults;
    }

    private List<SemanticMatch> PerformSemanticSearch(string message, int? version)
    {
      logger.LogInformation("[CslaCodeTool.PerformSemanticSearch] Starting semantic search for version {Version}", version);
      var semanticMatches = new List<SemanticMatch>();
      
      if (VectorStore != null && VectorStore.IsReady())
      {
        logger.LogInformation("[CslaCodeTool.PerformSemanticSearch] Performing semantic search");
        var semanticResults = VectorStore.SearchAsync(message, version, topK: 10).GetAwaiter().GetResult();
        semanticMatches = semanticResults.Select(r => new SemanticMatch
        {
          FileName = r.FileName,
          SimilarityScore = r.SimilarityScore
        }).ToList();
        logger.LogInformation("[CslaCodeTool.PerformSemanticSearch] Found {Count} semantic matches", semanticMatches.Count);
      }
      else if (VectorStore != null && !VectorStore.IsHealthy())
      {
        logger.LogWarning("[CslaCodeTool.PerformSemanticSearch] Semantic search unavailable due to Azure OpenAI configuration issues");
      }
      else
      {
        logger.LogInformation("[CslaCodeTool.PerformSemanticSearch] Vector store not available, using keyword search only");
      }
      
      return semanticMatches;
    }

    private int GetHighestVersionFromFileSystem()
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
          logger.LogInformation("[CslaCodeTool.GetHighestVersionFromFileSystem] Found versions: [{Versions}], highest: {Highest}", string.Join(", ", versionDirs.OrderBy(v => v)), highest);
          return highest;
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "[CslaCodeTool.GetHighestVersionFromFileSystem] Error detecting versions: {Message}", ex.Message);
      }
      
      // No version directories found - return a reasonable default
      // This will be used when all content is in the root directory (common to all versions)
      logger.LogInformation("[CslaCodeTool.GetHighestVersionFromFileSystem] No version directories found, defaulting to latest known CSLA version");
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

    [McpServerTool, Description(fetchDescription)]
    public async Task<string> Fetch([Description("FileName from the search tool, or a version-specific path like 'v10/Command.md' if multiple versions exist.")]string fileName)
    {
      logger.LogInformation("[CslaCodeTool.Fetch] Called with fileName: '{FileName}'", fileName);
      
      try
      {
        logger.LogInformation("[CslaCodeTool.Fetch] Using CodeSamplesPath: '{Path}'", CodeSamplesPath);
        
        // Validate fileName to prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(fileName))
        {
          var error = "File name cannot be empty or null";
          logger.LogError("[CslaCodeTool.Fetch] Error: {Error}", error);
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
          logger.LogError("[CslaCodeTool.Fetch] Error: {Error}", error);
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
          logger.LogError("[CslaCodeTool.Fetch] Error: {Error}", error);
          return JsonSerializer.Serialize(new ErrorResult 
          { 
            Error = "PathNotFound", 
            Message = error 
          }, new JsonSerializerOptions { WriteIndented = true });
        }

        // Normalize path separator to system default
        var normalizedFileName = fileName.Replace("/", Path.DirectorySeparatorChar.ToString());
        var filePath = Path.Combine(CodeSamplesPath, normalizedFileName);
        logger.LogInformation("[CslaCodeTool.Fetch] Attempting to read file: '{FilePath}'", filePath);
        
        if (File.Exists(filePath))
        {
          var content = File.ReadAllText(filePath);
          logger.LogInformation("[CslaCodeTool.Fetch] Successfully read file '{FileName}' ({Length} characters)", fileName, content.Length);
          return content;
        }
        else
        {
          // File not found at exact path - check if there are version-specific alternatives
          var fileNameOnly = Path.GetFileName(fileName);
          var matchingFiles = FindVersionSpecificFiles(fileNameOnly);
          
          if (matchingFiles.Count > 1)
          {
            // Multiple version-specific files found
            var message = $"Multiple versions of '{fileNameOnly}' found. Please specify the version by providing the full path.";
            logger.LogInformation("[CslaCodeTool.Fetch] {Message}. Available files: [{Files}]", message, string.Join(", ", matchingFiles));
            return JsonSerializer.Serialize(new MultipleFilesResult
            {
              Message = message,
              AvailableFiles = matchingFiles
            }, new JsonSerializerOptions { WriteIndented = true });
          }
          else if (matchingFiles.Count == 1)
          {
            // Single match found in a version-specific folder
            var matchedFilePath = Path.Combine(CodeSamplesPath, matchingFiles[0]);
            var content = File.ReadAllText(matchedFilePath);
            logger.LogInformation("[CslaCodeTool.Fetch] Found single version-specific file '{FileName}', returning content ({Length} characters)", matchingFiles[0], content.Length);
            return content;
          }
          else
          {
            // No matches found anywhere
            var error = $"File '{fileName}' not found in code samples directory";
            logger.LogError("[CslaCodeTool.Fetch] Error: {Error}", error);
            return JsonSerializer.Serialize(new ErrorResult 
            { 
              Error = "FileNotFound", 
              Message = error 
            }, new JsonSerializerOptions { WriteIndented = true });
          }
        }
      }
      catch (Exception ex)
      {
        var error = $"Fetch operation failed: {ex.Message}";
        logger.LogError(ex, "[CslaCodeTool.Fetch] Error: {Error}", error);
        return JsonSerializer.Serialize(new ErrorResult 
        { 
          Error = "FetchFailed", 
          Message = error 
        }, new JsonSerializerOptions { WriteIndented = true });
      }
    }

    /// <summary>
    /// Searches for files with the given name across all version-specific subdirectories.
    /// Returns a list of relative paths (e.g., "v10/Command.md", "v9/Command.md").
    /// </summary>
    private List<string> FindVersionSpecificFiles(string fileName)
    {
      var results = new List<string>();
      
      try
      {
        // Get all .cs and .md files in the code samples directory
        var allFiles = Directory.GetFiles(CodeSamplesPath, "*.*", SearchOption.AllDirectories)
          .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || 
                      f.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        
        // Find files that match the requested file name
        foreach (var file in allFiles)
        {
          if (Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase))
          {
            var relativePath = Path.GetRelativePath(CodeSamplesPath, file)
              .Replace("\\", "/"); // Normalize to forward slashes for consistency
            results.Add(relativePath);
          }
        }
        
        // Sort results by version number (descending) so newest versions appear first
        results = results
          .OrderByDescending(path => ExtractVersionNumber(path))
          .ThenBy(path => path)
          .ToList();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "[CslaCodeTool.FindVersionSpecificFiles] Error searching for version-specific files: {Message}", ex.Message);
      }
      
      return results;
    }

    /// <summary>
    /// Extracts the version number from a path like "v10/Command.md" or returns 0 if not in a version folder.
    /// </summary>
    private int ExtractVersionNumber(string path)
    {
      var match = Regex.Match(path, @"^v(\d+)/");
      if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
      {
        return version;
      }
      return 0; // Files not in a version folder are considered version 0
    }

    [McpServerTool, Description("Returns the CSLA MCP server version.")]
    public string Version()
    {
      var assembly = Assembly.GetExecutingAssembly();
      var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
          ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
      return version;
    }
  }
}
