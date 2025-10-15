# CSLA Embeddings Generator

A command-line tool that generates vector embeddings for CSLA .NET code samples using Azure OpenAI.

## Purpose

This tool pre-generates vector embeddings for all code samples in the `csla-examples` directory and saves them to a JSON file. This eliminates the need to regenerate embeddings every time the MCP server starts, significantly reducing startup time and Azure OpenAI API costs.

## Prerequisites

- .NET 10.0 SDK
- Azure OpenAI service with a deployed embedding model (e.g., `text-embedding-3-large`)
- Required environment variables:
  - `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI service endpoint
  - `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key
  - `AZURE_OPENAI_EMBEDDING_MODEL` (optional): The embedding model deployment name (default: `text-embedding-3-large`)

## Usage

### Basic Usage

```bash
dotnet run --project csla-embeddings-generator
```

This will:
- Look for the `csla-examples` directory in the current working directory
- Generate embeddings for all `.cs` and `.md` files
- Save the results to `embeddings.json` in the current directory

### Custom Paths

You can specify custom paths for the examples directory and output file:

```bash
dotnet run --project csla-embeddings-generator -- --examples-path /path/to/examples --output /path/to/embeddings.json
```

### Command-Line Options

- `--examples-path <PATH>`: Path to the directory containing code samples (default: `./csla-examples`)
- `--output <PATH>`: Path where the embeddings JSON file will be saved (default: `./embeddings.json`)

## Build Process Integration

This tool is integrated into the Docker build process via the `build.sh` script:

1. The tool is built and run to generate `embeddings.json`
2. The `embeddings.json` file is copied into the Docker container during build
3. The MCP server loads the pre-generated embeddings on startup instead of regenerating them

## Output Format

The tool generates a JSON file containing an array of document embeddings:

```json
[
  {
    "FileName": "DataPortalOperationCreate.md",
    "Content": "...",
    "Embedding": [0.123, 0.456, ...],
    "Version": null
  },
  ...
]
```

Each embedding includes:
- `FileName`: Relative path to the file from the examples directory
- `Content`: Full text content of the file
- `Embedding`: Array of floating-point numbers representing the embedding vector
- `Version`: CSLA version number (extracted from path) or null for common files

## Error Handling

The tool will exit with specific error codes:
- `0`: Success
- `1`: Examples directory not found
- `2`: `AZURE_OPENAI_ENDPOINT` environment variable not set
- `3`: `AZURE_OPENAI_API_KEY` environment variable not set
- `4`: Error during embedding generation or file writing
