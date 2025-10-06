namespace CslaEmbeddingsGenerator;

/// <summary>
/// Represents a document with its embedding vector
/// </summary>
public class DocumentEmbedding
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int? Version { get; set; } = null;
}
