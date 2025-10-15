using Azure.AI.OpenAI;
using Azure;
using OpenAI.Embeddings;

namespace CslaEmbeddingsGenerator;

/// <summary>
/// Generates vector embeddings for CSLA code samples
/// </summary>
public class EmbeddingsGenerator
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly string _embeddingModelName;

    public EmbeddingsGenerator(string azureOpenAIEndpoint, string azureOpenAIApiKey, string embeddingModelName = "text-embedding-3-large")
    {
        var clientOptions = new AzureOpenAIClientOptions();
        _openAIClient = new AzureOpenAIClient(new Uri(azureOpenAIEndpoint), new AzureKeyCredential(azureOpenAIApiKey), clientOptions);
        _embeddingModelName = embeddingModelName;
    }

    /// <summary>
    /// Generates embeddings for all files in the examples directory
    /// </summary>
    public async Task<List<DocumentEmbedding>> GenerateEmbeddingsAsync(string examplesPath)
    {
        var embeddings = new List<DocumentEmbedding>();
        
        // Find all .cs and .md files
        var csFiles = Directory.GetFiles(examplesPath, "*.cs", SearchOption.AllDirectories);
        var mdFiles = Directory.GetFiles(examplesPath, "*.md", SearchOption.AllDirectories);
        var allFiles = csFiles.Concat(mdFiles).ToArray();
        
        Console.WriteLine($"[EmbeddingsGenerator] Found {allFiles.Length} files to process");
        
        int processedCount = 0;
        foreach (var file in allFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                
                // Get relative path from examples directory
                var relativePath = Path.GetRelativePath(examplesPath, file);
                
                // Detect version from path
                int? version = null;
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                if (pathParts.Length > 1 && pathParts[0].StartsWith("v") && int.TryParse(pathParts[0].Substring(1), out var versionNumber))
                {
                    version = versionNumber;
                }
                
                // Normalize path separators to forward slash for consistency
                var normalizedPath = relativePath.Replace("\\", "/");
                
                var versionInfo = version.HasValue ? $" (v{version})" : " (common)";
                Console.WriteLine($"[EmbeddingsGenerator] Processing: {normalizedPath}{versionInfo}");
                
                var embedding = await GenerateEmbeddingAsync(content);
                
                if (embedding != null && embedding.Length > 0)
                {
                    embeddings.Add(new DocumentEmbedding
                    {
                        FileName = normalizedPath,
                        Content = content,
                        Embedding = embedding,
                        Version = version
                    });
                    
                    processedCount++;
                    if (processedCount % 5 == 0)
                    {
                        Console.WriteLine($"[EmbeddingsGenerator] Processed {processedCount}/{allFiles.Length} files...");
                    }
                }
                else
                {
                    Console.WriteLine($"[EmbeddingsGenerator] Warning: Failed to generate embedding for {normalizedPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmbeddingsGenerator] Error processing file {file}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"[EmbeddingsGenerator] Successfully processed {processedCount} files");
        return embeddings;
    }

    /// <summary>
    /// Generates an embedding vector for the given text
    /// </summary>
    private async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingModelName);
            var response = await embeddingClient.GenerateEmbeddingAsync(text);
            
            if (response?.Value != null)
            {
                var embedding = response.Value.ToFloats().ToArray();
                return embedding;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmbeddingsGenerator] Error generating embedding: {ex.Message}");
            throw;
        }
    }
}
