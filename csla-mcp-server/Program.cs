using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using CslaMcpServer.Tools;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;
using System.IO;

public sealed class AppSettings : CommandSettings
{
    [CommandOption("-f|--folder <FOLDER>")]
    public string? Folder { get; set; }
}

public sealed class RunCommand : Command<AppSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] AppSettings settings)
    {
        // Priority: CLI (-f) > ENV CSLA_CODE_SAMPLES_PATH > default
        // First, check environment variable and apply it if present (validation will be performed)
        var envPath = Environment.GetEnvironmentVariable("CSLA_CODE_SAMPLES_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
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
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Failed to process CSLA_CODE_SAMPLES_PATH environment variable: {ex.Message}");
                return 6;
            }
        }

        // Then, if CLI option is present it overrides the environment variable
        if (!string.IsNullOrWhiteSpace(settings.Folder))
        {
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
        }

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<CslaCodeTool>();

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
        app.MapMcp();
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