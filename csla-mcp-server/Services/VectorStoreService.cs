using System.Numerics.Tensors;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Embeddings;

namespace CslaMcpServer.Services
{
  public class VectorStoreService
  {
    private readonly AzureOpenAIClient _openAIClient;
    private readonly Dictionary<string, DocumentEmbedding> _vectorStore;
    private readonly string _embeddingModelName;
    private bool _isHealthy = true;

    public class DocumentEmbedding
    {
      public string FileName { get; set; } = string.Empty;
      public string Content { get; set; } = string.Empty;
      public float[] Embedding { get; set; } = Array.Empty<float>();
      public int? Version { get; set; } = null; // null means common to all versions
    }

    public class SemanticSearchResult
    {
      public string FileName { get; set; } = string.Empty;
      public float SimilarityScore { get; set; }
    }

    public VectorStoreService(string azureOpenAIEndpoint, string azureOpenAIApiKey, string embeddingModelName = "text-embedding-3-large", string apiVersion = "2024-02-01")
    {
      // Use the latest available service version as default
      var clientOptions = new AzureOpenAIClientOptions();
      
      _openAIClient = new AzureOpenAIClient(new Uri(azureOpenAIEndpoint), new AzureKeyCredential(azureOpenAIApiKey), clientOptions);
      _vectorStore = new Dictionary<string, DocumentEmbedding>();
      _embeddingModelName = embeddingModelName;
      
      Console.WriteLine($"[VectorStore] Initialized with API version: {apiVersion} (using default client options)");
    }

    public async Task<bool> TestConnectivityAsync()
    {
      try
      {
        Console.WriteLine("[VectorStore] Testing Azure OpenAI connectivity...");
        var testEmbedding = await GenerateEmbeddingAsync("test connection");
        _isHealthy = testEmbedding != null && testEmbedding.Length > 0;
        
        if (_isHealthy)
        {
          Console.WriteLine("[VectorStore] Connectivity test passed - semantic search available.");
        }
        else
        {
          Console.WriteLine("[VectorStore] Connectivity test failed - semantic search disabled.");
        }
        
        return _isHealthy;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Connectivity test failed: {ex.Message}");
        _isHealthy = false;
        return false;
      }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
      if (!_isHealthy)
      {
        return null;
      }

      try
      {
        Console.WriteLine($"[VectorStore] Attempting to generate embedding using model: {_embeddingModelName}");
        
        var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingModelName);
        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        
        if (response?.Value != null)
        {
          var embedding = response.Value.ToFloats().ToArray();
          Console.WriteLine($"[VectorStore] Successfully generated embedding with {embedding.Length} dimensions");
          return embedding;
        }

        Console.WriteLine("[VectorStore] Response was null or empty");
        return null;
      }
      catch (Azure.RequestFailedException ex) when (ex.Status == 404)
      {
        Console.WriteLine($"[VectorStore] Error: Azure OpenAI deployment '{_embeddingModelName}' not found (404).");
        Console.WriteLine($"[VectorStore] Please ensure you have deployed the embedding model '{_embeddingModelName}' in your Azure OpenAI resource.");
        Console.WriteLine($"[VectorStore] Available deployment names in Azure should match the AZURE_OPENAI_EMBEDDING_MODEL environment variable.");
        Console.WriteLine($"[VectorStore] Also check that your model is compatible with the current API version.");
        Console.WriteLine("[VectorStore] Disabling semantic search for this session.");
        _isHealthy = false;
        return null;
      }
      catch (Azure.RequestFailedException ex) when (ex.Status == 400)
      {
        Console.WriteLine($"[VectorStore] Bad Request (400): {ex.Message}");
        Console.WriteLine("[VectorStore] This might be an API version compatibility issue.");
        Console.WriteLine("[VectorStore] For text-embedding-3-large: Ensure you're using a recent API version");
        Console.WriteLine("[VectorStore] For text-embedding-ada-002: Try setting AZURE_OPENAI_API_VERSION to a compatible version");
        Console.WriteLine("[VectorStore] Disabling semantic search for this session.");
        _isHealthy = false;
        return null;
      }
      catch (Azure.RequestFailedException ex)
      {
        Console.WriteLine($"[VectorStore] Azure OpenAI API error (Status: {ex.Status}): {ex.Message}");
        if (ex.Status == 401)
        {
          Console.WriteLine("[VectorStore] This might be an authentication issue. Please check your AZURE_OPENAI_API_KEY.");
        }
        else if (ex.Status == 403)
        {
          Console.WriteLine("[VectorStore] This might be a permissions issue. Please check your Azure OpenAI resource access.");
        }
        Console.WriteLine("[VectorStore] Disabling semantic search for this session.");
        _isHealthy = false;
        return null;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Unexpected error generating embedding: {ex.Message}");
        Console.WriteLine($"[VectorStore] Exception type: {ex.GetType().Name}");
        if (ex.InnerException != null)
        {
          Console.WriteLine($"[VectorStore] Inner exception: {ex.InnerException.Message}");
        }
        Console.WriteLine("[VectorStore] Disabling semantic search for this session.");
        _isHealthy = false;
        return null;
      }
    }

    public async Task IndexDocumentAsync(string fileName, string content, int? version = null)
    {
      try
      {
        var versionInfo = version.HasValue ? $" (v{version})" : " (common)";
        Console.WriteLine($"[VectorStore] Indexing document: {fileName}{versionInfo}");
        
        var embedding = await GenerateEmbeddingAsync(content);
        
        if (embedding != null && embedding.Length > 0)
        {
          _vectorStore[fileName] = new DocumentEmbedding
          {
            FileName = fileName,
            Content = content,
            Embedding = embedding,
            Version = version
          };
          
          Console.WriteLine($"[VectorStore] Successfully indexed {fileName}{versionInfo} with {embedding.Length} dimensions");
        }
        else
        {
          Console.WriteLine($"[VectorStore] Failed to generate embedding for {fileName}");
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Error indexing document {fileName}: {ex.Message}");
      }
    }

    public async Task<List<SemanticSearchResult>> SearchAsync(string query, int? version = null, int topK = 10)
    {
      try
      {
        // If no version specified, default to highest version
        if (!version.HasValue)
        {
          version = GetHighestVersion();
        }
        
        Console.WriteLine($"[VectorStore] Performing semantic search for: {query} (version: {version})");
        
        var queryEmbedding = await GenerateEmbeddingAsync(query);
        
        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
          Console.WriteLine("[VectorStore] Failed to generate query embedding");
          return new List<SemanticSearchResult>();
        }

        var results = new List<SemanticSearchResult>();

        // Filter documents: include common (Version == null) and version-specific (Version == version)
        foreach (var doc in _vectorStore.Values)
        {
          if (doc.Version == null || doc.Version == version)
          {
            var similarity = CosineSimilarity(queryEmbedding, doc.Embedding);
            results.Add(new SemanticSearchResult
            {
              FileName = doc.FileName,
              SimilarityScore = similarity
            });
          }
        }

        // Sort by similarity score descending and take top K
        var topResults = results
          .OrderByDescending(r => r.SimilarityScore)
          .Take(topK)
          .Where(r => r.SimilarityScore > 0.5f) // Filter out low similarity scores
          .ToList();

        Console.WriteLine($"[VectorStore] Found {topResults.Count} semantic matches for version {version}");
        
        return topResults;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Error during semantic search: {ex.Message}");
        return new List<SemanticSearchResult>();
      }
    }

    private int GetHighestVersion()
    {
      var versions = _vectorStore.Values
        .Where(doc => doc.Version.HasValue)
        .Select(doc => doc.Version!.Value)
        .Distinct()
        .ToList();
      
      if (versions.Any())
      {
        var highest = versions.Max();
        Console.WriteLine($"[VectorStore] Highest version detected: {highest}");
        return highest;
      }
      
      // No version-specific content indexed - return a reasonable default
      // This will be used when all content is common (version = null)
      Console.WriteLine("[VectorStore] No version-specific content found, defaulting to latest known CSLA version");
      return 10; // Default fallback when no version-specific documents exist
    }

    private float CosineSimilarity(float[] vector1, float[] vector2)
    {
      if (vector1.Length != vector2.Length)
      {
        throw new ArgumentException("Vectors must have the same length");
      }

      var dotProduct = TensorPrimitives.Dot(vector1.AsSpan(), vector2.AsSpan());
      var magnitude1 = Math.Sqrt(TensorPrimitives.Dot(vector1.AsSpan(), vector1.AsSpan()));
      var magnitude2 = Math.Sqrt(TensorPrimitives.Dot(vector2.AsSpan(), vector2.AsSpan()));

      if (magnitude1 == 0 || magnitude2 == 0)
      {
        return 0;
      }

      return (float)(dotProduct / (magnitude1 * magnitude2));
    }

    public int GetDocumentCount()
    {
      return _vectorStore.Count;
    }

    public bool IsReady()
    {
      return _isHealthy && _vectorStore.Count > 0;
    }

    public bool IsHealthy()
    {
      return _isHealthy;
    }

    /// <summary>
    /// Loads embeddings from a JSON file into the vector store
    /// </summary>
    public async Task<int> LoadEmbeddingsFromJsonAsync(string jsonFilePath)
    {
      try
      {
        Console.WriteLine($"[VectorStore] Loading embeddings from {jsonFilePath}");
        
        if (!File.Exists(jsonFilePath))
        {
          Console.WriteLine($"[VectorStore] Embeddings file not found at {jsonFilePath}");
          return 0;
        }

        var json = await File.ReadAllTextAsync(jsonFilePath);
        var embeddings = JsonSerializer.Deserialize<List<DocumentEmbedding>>(json);
        
        if (embeddings == null || embeddings.Count == 0)
        {
          Console.WriteLine("[VectorStore] No embeddings found in JSON file");
          return 0;
        }

        foreach (var embedding in embeddings)
        {
          _vectorStore[embedding.FileName] = embedding;
        }

        Console.WriteLine($"[VectorStore] Successfully loaded {embeddings.Count} embeddings from JSON");
        return embeddings.Count;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Error loading embeddings from JSON: {ex.Message}");
        return 0;
      }
    }

    /// <summary>
    /// Exports all embeddings to a JSON file
    /// </summary>
    public async Task<bool> ExportEmbeddingsToJsonAsync(string jsonFilePath)
    {
      try
      {
        Console.WriteLine($"[VectorStore] Exporting embeddings to {jsonFilePath}");
        
        var embeddings = _vectorStore.Values.ToList();
        var json = JsonSerializer.Serialize(embeddings, new JsonSerializerOptions 
        { 
          WriteIndented = true 
        });

        await File.WriteAllTextAsync(jsonFilePath, json);
        Console.WriteLine($"[VectorStore] Successfully exported {embeddings.Count} embeddings to JSON");
        
        return true;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Error exporting embeddings to JSON: {ex.Message}");
        return false;
      }
    }
  }
}
