# Vector Store Persistence Architecture

## Workflow Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         BUILD TIME                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  1. build.sh script runs                                            │
│     │                                                                │
│     ├─> Builds csla-embeddings-generator CLI tool                   │
│     │                                                                │
│     ├─> Runs CLI tool:                                              │
│     │   ├─> Scans csla-examples/ directory                          │
│     │   ├─> Connects to Azure OpenAI                                │
│     │   ├─> Generates embeddings for each file                      │
│     │   └─> Saves to embeddings.json                                │
│     │                                                                │
│     └─> Builds Docker container:                                    │
│         ├─> Copies csla-examples/ → /csla-examples                  │
│         ├─> Copies embeddings.json → /app/embeddings.json           │
│         └─> Copies application → /app                               │
│                                                                       │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                         RUNTIME (Container)                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  1. Server starts                                                    │
│     │                                                                │
│     ├─> Initializes VectorStoreService                              │
│     │   ├─> Requires Azure OpenAI credentials                       │
│     │   └─> Used for user query embeddings                          │
│     │                                                                │
│     └─> Loads embeddings:                                           │
│         │                                                            │
│         ├─> Checks for /app/embeddings.json                         │
│         │                                                            │
│         ├─> If found:                                               │
│         │   ├─> Loads pre-generated embeddings (FAST!)              │
│         │   ├─> Populates in-memory vector store                    │
│         │   └─> Ready in seconds                                    │
│         │                                                            │
│         └─> If not found:                                           │
│             ├─> Displays warning message                            │
│             ├─> Semantic search disabled                            │
│             └─> Keyword search still available                      │
│                                                                       │
│  2. Server handles user requests                                    │
│     │                                                                │
│     └─> Search requests:                                            │
│         ├─> Uses pre-loaded file embeddings (if available)          │
│         ├─> Generates embedding for user query (Azure OpenAI)       │
│         └─> Returns semantic search results                         │
│                                                                       │
└─────────────────────────────────────────────────────────────────────┘
```

## Component Interaction

```
┌──────────────────────┐
│  csla-embeddings-    │
│    generator         │
│  (CLI Tool)          │
└──────────┬───────────┘
           │ generates
           ↓
    ┌──────────────┐
    │ embeddings.  │
    │   json       │
    └──────┬───────┘
           │ included in
           ↓
    ┌──────────────┐         ┌─────────────────┐
    │   Docker     │────────→│  csla-mcp-      │
    │  Container   │ contains │   server        │
    └──────────────┘         └────────┬────────┘
                                      │ loads on startup
                                      ↓
                             ┌─────────────────┐
                             │ VectorStore     │
                             │  Service        │
                             │ (in-memory)     │
                             └────────┬────────┘
                                      │ serves
                                      ↓
                             ┌─────────────────┐
                             │  Search API     │
                             │   Requests      │
                             └─────────────────┘
```

## Data Flow

### Build Time Data Flow
```
csla-examples/*.{cs,md}
    │
    │ (read)
    ↓
csla-embeddings-generator
    │
    │ (Azure OpenAI API)
    ↓
embeddings.json (5-20 MB)
    │
    │ (COPY in Dockerfile)
    ↓
Docker Container: /app/embeddings.json
```

### Runtime Data Flow
```
/app/embeddings.json
    │
    │ (LoadEmbeddingsFromJsonAsync)
    ↓
VectorStoreService._vectorStore
    │ (in-memory Dictionary)
    │
    │ (semantic search)
    ↓
Search Results
```

## Key Design Decisions

### 1. JSON Format Choice
- **Decision**: Use JSON for embeddings storage
- **Rationale**: 
  - Simple to implement
  - Human-readable for debugging
  - Built-in .NET serialization support
  - No external database dependencies
  - Sufficient for expected file count (<1000 files)

### 2. Build-Time Generation
- **Decision**: Generate embeddings during Docker build
- **Rationale**:
  - Separates concerns (build vs. runtime)
  - Reduces runtime dependencies
  - Faster container startup
  - Embeddings immutable per build

### 3. No Runtime Generation
- **Decision**: Server does not generate embeddings for example files at runtime
- **Rationale**:
  - Separates concerns (build-time generation vs. runtime loading)
  - Reduces runtime dependencies on Azure OpenAI for file indexing
  - Faster startup guaranteed (no waiting for generation)
  - Clear separation of build-time and runtime operations
  - Azure OpenAI only needed at runtime for user queries

### 4. In-Memory Storage
- **Decision**: Continue using in-memory Dictionary
- **Rationale**:
  - Fast lookups (O(1))
  - Simple implementation
  - No serialization overhead during queries
  - Matches existing architecture

## File Formats

### embeddings.json Structure
```json
[
  {
    "FileName": "relative/path/to/file.cs",
    "Content": "full file content...",
    "Embedding": [0.1, 0.2, ..., 0.n],  // 1536 floats for text-embedding-3-small
    "Version": 10                         // or null for version-agnostic
  }
]
```

### File Size Estimates
- Per file: ~50-200 KB (depending on content size)
- Typical total: 5-20 MB for full example set
- Embedding vector: ~6 KB (1536 floats × 4 bytes)

## Performance Characteristics

### Build Time
- Embeddings generation: ~30-60 seconds (depends on file count)
- Docker build: +2-5 seconds (copy embeddings.json)

### Runtime (with embeddings.json)
- Startup time: 2-5 seconds
- Memory footprint: +5-20 MB
- Search latency: Unchanged (still requires user query embedding)

### Runtime (without embeddings.json)
- Startup time: 2-5 seconds (same as with embeddings)
- Semantic search: Disabled (keyword search still available)
- Memory footprint: Minimal (no embeddings loaded)

## Security Considerations

### Build Time
- Azure OpenAI credentials required
- Credentials should be in CI/CD environment variables
- embeddings.json contains no secrets (only vectors and content)

### Runtime
- Azure OpenAI credentials still required for user queries
- embeddings.json is read-only
- No external database credentials needed

## Scalability

### Current Implementation
- Suitable for: 10-1000 files
- Memory: ~5-20 MB
- Load time: ~2-5 seconds

### If Scale Increases
Consider:
- Compression (gzip embeddings.json)
- Lazy loading (load embeddings on demand)
- External vector database (Pinecone, Weaviate, etc.)
- Pagination/chunking

## Maintenance

### When to Regenerate Embeddings
- When code samples change
- When CSLA version files are added/modified
- When switching embedding models
- As part of CI/CD pipeline

### Monitoring
- Check embeddings.json file size
- Monitor server startup time
- Track API costs (should decrease significantly)

## Future Enhancements

Potential improvements:
1. **Incremental Updates**: Only regenerate changed files
2. **Versioning**: Track embeddings format version
3. **Compression**: Compress embeddings.json
4. **Checksums**: Validate embeddings file integrity
5. **Metadata**: Store generation timestamp, model version
6. **Caching**: Add HTTP caching headers for embeddings
