using System.Numerics.Tensors;
using System.Text;
using System.Text.Json;

namespace CslaMcpServer.Services
{
  public class VectorStoreService
  {
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, DocumentEmbedding> _vectorStore;
    private readonly string _ollamaEndpoint;
    private readonly string _modelName;

    public class DocumentEmbedding
    {
      public string FileName { get; set; } = string.Empty;
      public string Content { get; set; } = string.Empty;
      public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    public class SemanticSearchResult
    {
      public string FileName { get; set; } = string.Empty;
      public float SimilarityScore { get; set; }
    }

    public VectorStoreService(string ollamaEndpoint = "http://localhost:11434", string modelName = "nomic-embed-text:latest")
    {
      _httpClient = new HttpClient();
      _vectorStore = new Dictionary<string, DocumentEmbedding>();
      _ollamaEndpoint = ollamaEndpoint;
      _modelName = modelName;
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
      try
      {
        var request = new
        {
          model = _modelName,
          prompt = text
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_ollamaEndpoint}/api/embeddings", content);
        
        if (!response.IsSuccessStatusCode)
        {
          Console.WriteLine($"[VectorStore] Failed to generate embedding. Status: {response.StatusCode}");
          return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        if (result.TryGetProperty("embedding", out var embeddingElement))
        {
          var embedding = embeddingElement.EnumerateArray()
            .Select(e => (float)e.GetDouble())
            .ToArray();
          
          return embedding;
        }

        return null;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Error generating embedding: {ex.Message}");
        return null;
      }
    }

    public async Task IndexDocumentAsync(string fileName, string content)
    {
      try
      {
        Console.WriteLine($"[VectorStore] Indexing document: {fileName}");
        
        var embedding = await GenerateEmbeddingAsync(content);
        
        if (embedding != null && embedding.Length > 0)
        {
          _vectorStore[fileName] = new DocumentEmbedding
          {
            FileName = fileName,
            Content = content,
            Embedding = embedding
          };
          
          Console.WriteLine($"[VectorStore] Successfully indexed {fileName} with {embedding.Length} dimensions");
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

    public async Task<List<SemanticSearchResult>> SearchAsync(string query, int topK = 10)
    {
      try
      {
        Console.WriteLine($"[VectorStore] Performing semantic search for: {query}");
        
        var queryEmbedding = await GenerateEmbeddingAsync(query);
        
        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
          Console.WriteLine("[VectorStore] Failed to generate query embedding");
          return new List<SemanticSearchResult>();
        }

        var results = new List<SemanticSearchResult>();

        foreach (var doc in _vectorStore.Values)
        {
          var similarity = CosineSimilarity(queryEmbedding, doc.Embedding);
          results.Add(new SemanticSearchResult
          {
            FileName = doc.FileName,
            SimilarityScore = similarity
          });
        }

        // Sort by similarity score descending and take top K
        var topResults = results
          .OrderByDescending(r => r.SimilarityScore)
          .Take(topK)
          .Where(r => r.SimilarityScore > 0.1f) // Filter out very low similarity scores
          .ToList();

        Console.WriteLine($"[VectorStore] Found {topResults.Count} semantic matches");
        
        return topResults;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[VectorStore] Error during semantic search: {ex.Message}");
        return new List<SemanticSearchResult>();
      }
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
      return _vectorStore.Count > 0;
    }
  }
}
