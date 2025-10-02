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

        // Initialize vector store
        Console.WriteLine("[Startup] Initializing vector store with Ollama...");
        var vectorStore = new VectorStoreService();
        CslaCodeTool.VectorStore = vectorStore;
        
        // Index all files asynchronously
        var indexingTask = Task.Run(async () =>
        {
            try
            {
                if (Directory.Exists(CslaCodeTool.CodeSamplesPath))
                {
                    var csFiles = Directory.GetFiles(CslaCodeTool.CodeSamplesPath, "*.cs", SearchOption.AllDirectories);
                    var mdFiles = Directory.GetFiles(CslaCodeTool.CodeSamplesPath, "*.md", SearchOption.AllDirectories);
                    var allFiles = csFiles.Concat(mdFiles).ToArray();
                    
                    Console.WriteLine($"[Startup] Starting to index {allFiles.Length} files...");
                    
                    var indexedCount = 0;
                    foreach (var file in allFiles)
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            var fileName = Path.GetFileName(file);
                            await vectorStore.IndexDocumentAsync(fileName, content);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] Error during vector store indexing: {ex.Message}");
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