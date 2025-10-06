using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using CslaMcpServer.Tools;
using CslaMcpServer.Services;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Csla.Configuration;

public sealed class AppSettings : CommandSettings
{
    [CommandOption("-f|--folder <FOLDER>")]
    public string? Folder { get; set; }
}

public sealed class RunCommand : Command<AppSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AppSettings settings)
    {
        // Log startup information
        Console.WriteLine($"[Startup] Current working directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"[Startup] Application base directory: {AppDomain.CurrentDomain.BaseDirectory}");
        Console.WriteLine($"[Startup] Default CodeSamplesPath: {CslaCodeTool.CodeSamplesPath}");
        
        // Priority: CLI (-f) > ENV CSLA_CODE_SAMPLES_PATH > default
        // First, check environment variable and apply it if present (validation will be performed)
        var envPath = Environment.GetEnvironmentVariable("CSLA_CODE_SAMPLES_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            Console.WriteLine($"[Startup] Found environment variable CSLA_CODE_SAMPLES_PATH: {envPath}");
            try
            {
                var envFull = Path.GetFullPath(envPath);
                if (!envFull.EndsWith(Path.DirectorySeparatorChar)) envFull += Path.DirectorySeparatorChar;

                if (!Directory.Exists(envFull))
                {
                    Console.Error.WriteLine($"Error: The environment variable CSLA_CODE_SAMPLES_PATH is set to '{envFull}' but that folder does not exist.");
                    return 4;
                }

                var envHasFiles = Directory.EnumerateFiles(envFull, "*.cs", SearchOption.AllDirectories).Any()
                    || Directory.EnumerateFiles(envFull, "*.md", SearchOption.AllDirectories).Any();

                if (!envHasFiles)
                {
                    Console.Error.WriteLine($"Error: The environment variable CSLA_CODE_SAMPLES_PATH is set to '{envFull}' but that folder does not contain any .cs or .md files.");
                    return 5;
                }

                CslaCodeTool.CodeSamplesPath = envFull;
                Console.WriteLine($"[Startup] Using CodeSamplesPath from environment: {envFull}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Failed to process CSLA_CODE_SAMPLES_PATH environment variable: {ex.Message}");
                return 6;
            }
        }
        else
        {
            Console.WriteLine("[Startup] No CSLA_CODE_SAMPLES_PATH environment variable found");
        }

        // Then, if CLI option is present it overrides the environment variable
        if (!string.IsNullOrWhiteSpace(settings.Folder))
        {
            Console.WriteLine($"[Startup] CLI folder option provided: {settings.Folder}");
            // Resolve and normalize folder path
            var fullPath = Path.GetFullPath(settings.Folder);
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar)) fullPath += Path.DirectorySeparatorChar;

            // Validate existence
            if (!Directory.Exists(fullPath))
            {
                Console.Error.WriteLine($"Error: The specified folder '{fullPath}' does not exist.");
                return 2;
            }

            // Validate it contains at least one .cs or .md file
            var hasFiles = Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories).Any()
                || Directory.EnumerateFiles(fullPath, "*.md", SearchOption.AllDirectories).Any();

            if (!hasFiles)
            {
                Console.Error.WriteLine($"Error: The specified folder '{fullPath}' does not contain any .cs or .md files.");
                return 3;
            }

            CslaCodeTool.CodeSamplesPath = fullPath;
            Console.WriteLine($"[Startup] Using CodeSamplesPath from CLI: {fullPath}");
        }

        // Final logging of the resolved path
        Console.WriteLine($"[Startup] Final CodeSamplesPath: {CslaCodeTool.CodeSamplesPath}");
        Console.WriteLine($"[Startup] CodeSamplesPath exists: {Directory.Exists(CslaCodeTool.CodeSamplesPath)}");
        
        if (Directory.Exists(CslaCodeTool.CodeSamplesPath))
        {
            var csFiles = Directory.GetFiles(CslaCodeTool.CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
            var mdFiles = Directory.GetFiles(CslaCodeTool.CodeSamplesPath, "*.md", SearchOption.AllDirectories);
            Console.WriteLine($"[Startup] Found {csFiles.Length} .cs files and {mdFiles.Length} .md files");
        }

        // Initialize vector store with Azure OpenAI
        Console.WriteLine("[Startup] Initializing vector store with Azure OpenAI...");
        
        var azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureOpenAIApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var embeddingModel = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small";
        var apiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") ?? "2024-02-01";
        
        VectorStoreService? vectorStore = null;
        
        if (string.IsNullOrWhiteSpace(azureOpenAIEndpoint) || string.IsNullOrWhiteSpace(azureOpenAIApiKey))
        {
            Console.WriteLine("[Startup] Azure OpenAI configuration not found - running in keyword-only search mode.");
            Console.WriteLine("[Startup] To enable semantic search, set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY environment variables.");
            Console.WriteLine("[Startup] See azure-openAI-config.md for detailed setup instructions.");
        }
        else
        {
            Console.WriteLine($"[Startup] Using Azure OpenAI endpoint: {azureOpenAIEndpoint}");
            Console.WriteLine($"[Startup] Using embedding model deployment: {embeddingModel}");
            Console.WriteLine($"[Startup] Using API version: {apiVersion}");
            Console.WriteLine($"[Startup] IMPORTANT: Ensure you have deployed the '{embeddingModel}' model in your Azure OpenAI resource.");
            Console.WriteLine($"[Startup] The deployment name in Azure must exactly match the model name: {embeddingModel}");
            
            try
            {
                vectorStore = new VectorStoreService(azureOpenAIEndpoint, azureOpenAIApiKey, embeddingModel, apiVersion);
                Console.WriteLine("[Startup] Vector store initialized successfully - semantic search enabled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] Failed to initialize vector store: {ex.Message}");
                Console.WriteLine("[Startup] Continuing in keyword-only search mode.");
                Console.WriteLine("[Startup] Check your Azure OpenAI configuration and deployment setup.");
            }
        }
        
        CslaCodeTool.VectorStore = vectorStore;
        
        // Index all files asynchronously
        var indexingTask = Task.Run(async () =>
        {
            try
            {
                if (vectorStore != null)
                {
                    // Try to load pre-generated embeddings from JSON file
                    var embeddingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "embeddings.json");
                    var loadedCount = await vectorStore.LoadEmbeddingsFromJsonAsync(embeddingsPath);
                    
                    if (loadedCount > 0)
                    {
                        Console.WriteLine($"[Startup] Loaded {loadedCount} pre-generated embeddings from {embeddingsPath}");
                        Console.WriteLine("[Startup] Skipping embedding generation - using pre-generated embeddings");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("[Startup] No pre-generated embeddings found, will generate embeddings at runtime");
                    }
                }
                
                if (Directory.Exists(CslaCodeTool.CodeSamplesPath))
                {
                    // Test connectivity first if vector store is available
                    bool canIndex = vectorStore == null || await vectorStore.TestConnectivityAsync();
                    
                    if (!canIndex)
                    {
                        Console.WriteLine("[Startup] Skipping vector indexing due to Azure OpenAI connectivity issues.");
                        Console.WriteLine("[Startup] Server will run with keyword search only.");
                        return;
                    }

                    var csFiles = Directory.GetFiles(CslaCodeTool.CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
                    var mdFiles = Directory.GetFiles(CslaCodeTool.CodeSamplesPath, "*.md", SearchOption.AllDirectories);
                    var allFiles = csFiles.Concat(mdFiles).ToArray();
                    
                    if (vectorStore != null)
                    {
                        Console.WriteLine($"[Startup] Starting to index {allFiles.Length} files for semantic search...");
                        
                        var indexedCount = 0;
                        foreach (var file in allFiles)
                        {
                            try
                            {
                                var content = File.ReadAllText(file);
                                
                                // Get relative path from CodeSamplesPath
                                var relativePath = Path.GetRelativePath(CslaCodeTool.CodeSamplesPath, file);
                                
                                // Detect version from path
                                int? version = null;
                                var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                                if (pathParts.Length > 1 && pathParts[0].StartsWith("v") && int.TryParse(pathParts[0].Substring(1), out var versionNumber))
                                {
                                    version = versionNumber;
                                }
                                
                                // Normalize path separators to forward slash for consistency
                                var normalizedPath = relativePath.Replace("\\", "/");
                                
                                await vectorStore.IndexDocumentAsync(normalizedPath, content, version);
                                indexedCount++;
                                
                                if (indexedCount % 5 == 0)
                                {
                                    Console.WriteLine($"[Startup] Indexed {indexedCount}/{allFiles.Length} files...");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Startup] Failed to index file {file}: {ex.Message}");
                            }
                        }
                        
                        Console.WriteLine($"[Startup] Completed indexing {indexedCount} files");
                    }
                    else
                    {
                        Console.WriteLine("[Startup] Vector store not available - skipping semantic indexing.");
                        Console.WriteLine($"[Startup] Found {allFiles.Length} files available for keyword search.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] Error during file indexing: {ex.Message}");
            }
        });
        
        // Don't wait for indexing to complete before starting the server
        // The server will start immediately and semantic search will become available as indexing progresses

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<CslaCodeTool>();

        // CSLA services (modern pattern using IDataPortal<T>)
        builder.Services.AddCsla();

        // Add health checks for k8s liveness/readiness
        builder.Services.AddHealthChecks();

        builder.Services.AddOpenTelemetry()
            .WithTracing(b => b.AddSource("*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(b => b.AddMeter("*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithLogging()
            .UseOtlpExporter();

        var app = builder.Build();

        // Expose health endpoint at /health
        app.MapHealthChecks("/health");

        app.MapMcp();
        
        Console.WriteLine($"[Startup] Server starting on: {builder.Configuration["ASPNETCORE_URLS"] ?? "default URLs"}");
        
        app.Run();

        return 0;
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("csla-mcp-server");
                config.AddCommand<RunCommand>("run");
            });

            // Set the default command to RunCommand so `-f` works at top level
            app.SetDefaultCommand<RunCommand>();

            return app.Run(args);
    }
}