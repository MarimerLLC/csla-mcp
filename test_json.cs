using System;
using System.Text.Json;
using System.Collections.Generic;

public class DocumentEmbedding
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int? Version { get; set; } = null;
}

class Program
{
    static void Main()
    {
        var json = @"[
  {
    ""FileName"": ""test.md"",
    ""Content"": ""Test content"",
    ""Embedding"": [0.1, 0.2, 0.3, 0.4, 0.5],
    ""Version"": null
  },
  {
    ""FileName"": ""v10/test.cs"",
    ""Content"": ""Test C# content"",
    ""Embedding"": [0.5, 0.4, 0.3, 0.2, 0.1],
    ""Version"": 10
  }
]";
        
        var embeddings = JsonSerializer.Deserialize<List<DocumentEmbedding>>(json);
        Console.WriteLine($"Successfully deserialized {embeddings?.Count ?? 0} embeddings");
        
        if (embeddings != null && embeddings.Count > 0)
        {
            foreach (var emb in embeddings)
            {
                Console.WriteLine($"  - {emb.FileName}: {emb.Embedding.Length} dimensions, Version: {emb.Version?.ToString() ?? "null"}");
            }
        }
        
        var serialized = JsonSerializer.Serialize(embeddings, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("\nSerialized back:");
        Console.WriteLine(serialized);
    }
}
