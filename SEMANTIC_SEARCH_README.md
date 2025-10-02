# Semantic Search Implementation

This document describes the semantic search feature that has been added to the CSLA MCP Server.

## Overview

The server now supports both **word-based matching** (original functionality) and **semantic search** using vector embeddings. Both search methods run in parallel and return results in separate sections.

## Key Components

### 1. VectorStoreService (`Services/VectorStoreService.cs`)

An in-memory vector store that:
- Communicates with Ollama's API to generate embeddings using the `nomic-embed-text:latest` model
- Stores document embeddings in memory
- Performs cosine similarity calculations for semantic search
- Manages the document index

**Key Methods:**
- `GenerateEmbeddingAsync(string text)` - Generates a vector embedding for text using Ollama
- `IndexDocumentAsync(string fileName, string content)` - Indexes a document into the vector store
- `SearchAsync(string query, int topK)` - Performs semantic search and returns top K results
- `CosineSimilarity(float[] vector1, float[] vector2)` - Calculates cosine similarity between vectors

### 2. Updated CslaCodeTool (`Tools/CslaCodeTool.cs`)

The search tool now:
- Maintains a reference to the VectorStoreService
- Performs both word-based and semantic searches
- Returns results in a new `CombinedSearchResult` format

**New Classes:**
- `SemanticMatch` - Represents a semantic search result with similarity score
- `CombinedSearchResult` - Container for both semantic and word matches

**Updated Search Response Format:**
```json
{
  "SemanticMatches": [
    {
      "FileName": "EditableRoot.md",
      "SimilarityScore": 0.87
    }
  ],
  "WordMatches": [
    {
      "Score": 15,
      "FileName": "ReadWriteProperty.md",
      "MatchingWords": [
        {
          "Word": "property",
          "Count": 10
        }
      ]
    }
  ]
}
```

### 3. Startup Initialization (`Program.cs`)

On startup, the application:
1. Creates a VectorStoreService instance
2. Asynchronously indexes all .cs and .md files from the code samples directory
3. Starts the web server without waiting for indexing to complete
4. Semantic search becomes available as files are indexed

The indexing happens in the background and logs progress:
```
[Startup] Initializing vector store with Ollama...
[Startup] Starting to index 15 files...
[Startup] Indexed 5/15 files...
[Startup] Indexed 10/15 files...
[Startup] Completed indexing 15 files
```

## Configuration

### Ollama Setup

The implementation uses Ollama running locally with the `nomic-embed-text:latest` model.

**Prerequisites:**
1. Install Ollama: https://ollama.ai
2. Pull the embedding model:
   ```bash
   ollama pull nomic-embed-text:latest
   ```
3. Ensure Ollama is running (default endpoint: `http://localhost:11434`)

### Environment Variables

You can customize the Ollama endpoint by modifying the VectorStoreService initialization in `Program.cs`:

```csharp
var vectorStore = new VectorStoreService(
    ollamaEndpoint: "http://localhost:11434",  // Custom endpoint
    modelName: "nomic-embed-text:latest"       // Custom model
);
```

## Usage

### Search Behavior

When a search is performed:
1. **Word Matching** (always runs): Searches for keyword matches in document content
2. **Semantic Matching** (if vector store ready): Searches for semantically similar documents using embeddings

Both results are returned simultaneously in the combined response.

### Semantic Match Filtering

Semantic matches with similarity scores below 0.1 are filtered out to reduce noise. This threshold can be adjusted in `VectorStoreService.SearchAsync()`:

```csharp
.Where(r => r.SimilarityScore > 0.1f)  // Adjust threshold here
```

### Top K Results

By default, semantic search returns up to 10 results. This can be adjusted in `CslaCodeTool.Search()`:

```csharp
var semanticResults = VectorStore.SearchAsync(message, topK: 10).GetAwaiter().GetResult();
```

## Dependencies

New package added:
- `System.Numerics.Tensors` - Provides efficient tensor operations for cosine similarity calculations

## Performance Considerations

1. **Startup Time**: Indexing happens asynchronously, so the server starts immediately but semantic search may not be available for the first few seconds.

2. **Memory Usage**: All embeddings are stored in memory. For large document collections, consider:
   - Implementing pagination
   - Using a persistent vector database
   - Adding memory limits

3. **Ollama Performance**: Embedding generation depends on Ollama's performance. Ensure Ollama has adequate resources.

## Error Handling

The implementation gracefully handles:
- Ollama connection failures (semantic search disabled, word search continues)
- Individual file indexing failures (logs error, continues with other files)
- Missing or invalid embeddings (skips semantic results, returns word matches)

## Future Enhancements

Potential improvements:
1. Persistent vector store (e.g., using SQLite with vector extensions)
2. Incremental indexing for new/modified files
3. Configurable similarity threshold via environment variable
4. Hybrid scoring that combines word and semantic scores
5. Batch embedding generation for faster indexing
6. Support for alternative embedding models
