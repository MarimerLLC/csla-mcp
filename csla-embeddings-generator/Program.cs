using System.Text.Json;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Embeddings;

namespace CslaEmbeddingsGenerator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("[EmbeddingsGenerator] Starting CSLA Embeddings Generator");
        
        // Parse command line arguments
        string? examplesPath = null;
        string? outputPath = null;
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--examples-path" && i + 1 < args.Length)
            {
                examplesPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--output" && i + 1 < args.Length)
            {
                outputPath = args[i + 1];
                i++;
            }
        }
        
        // Default values
        examplesPath ??= Path.Combine(Directory.GetCurrentDirectory(), "csla-examples");
        outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "embeddings.json");
        
        Console.WriteLine($"[EmbeddingsGenerator] Examples path: {examplesPath}");
        Console.WriteLine($"[EmbeddingsGenerator] Output path: {outputPath}");
        
        // Validate examples path
        if (!Directory.Exists(examplesPath))
        {
            Console.Error.WriteLine($"[EmbeddingsGenerator] Error: Examples directory not found at {examplesPath}");
            return 1;
        }
        
        // Get Azure OpenAI configuration
        var azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureOpenAIApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var embeddingModel = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-large";
        
        if (string.IsNullOrWhiteSpace(azureOpenAIEndpoint))
        {
            Console.Error.WriteLine("[EmbeddingsGenerator] Error: AZURE_OPENAI_ENDPOINT environment variable is not set");
            return 2;
        }
        
        if (string.IsNullOrWhiteSpace(azureOpenAIApiKey))
        {
            Console.Error.WriteLine("[EmbeddingsGenerator] Error: AZURE_OPENAI_API_KEY environment variable is not set");
            return 3;
        }
        
        Console.WriteLine($"[EmbeddingsGenerator] Using Azure OpenAI endpoint: {azureOpenAIEndpoint}");
        Console.WriteLine($"[EmbeddingsGenerator] Using embedding model: {embeddingModel}");
        
        try
        {
            var generator = new EmbeddingsGenerator(azureOpenAIEndpoint, azureOpenAIApiKey, embeddingModel);
            var embeddings = await generator.GenerateEmbeddingsAsync(examplesPath);
            
            Console.WriteLine($"[EmbeddingsGenerator] Generated {embeddings.Count} embeddings");
            
            // Save to JSON file
            var json = JsonSerializer.Serialize(embeddings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"[EmbeddingsGenerator] Embeddings saved to {outputPath}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[EmbeddingsGenerator] Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"[EmbeddingsGenerator] Inner exception: {ex.InnerException.Message}");
            }
            return 4;
        }
    }
}
