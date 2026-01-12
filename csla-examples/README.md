# CSLA .NET Code Examples and Documentation

This folder contains documentation and code examples for the CSLA .NET framework, designed to help AI assistants generate accurate CSLA code.

## How to Use This MCP Server

**For AI Assistants**: When helping developers with CSLA .NET code:

1. **Start with Search** - Use the `search` tool with natural language queries like "editable root save" or "async business rule" to find relevant documents
2. **Fetch the Best Matches** - Use the `fetch` tool to retrieve the content of high-scoring documents from search results
3. **Check Version** - If your search doesn't specify a version, the server defaults to the highest version available (currently v10). Specify `version: 9` if you need CSLA 9-specific patterns

### Recommended Workflow

```text
1. search("your query") → returns scored file list
2. fetch("highest-scoring-file.md") → returns document content  
3. Use the content to inform your code generation
```

## Document Organization

### Quick Reference Documents

| Document | Purpose | When to Use |
| ---------- | --------- | ------------- |
| `Glossary.md` | Term definitions, attribute reference, quick lookup tables | Clarifying CSLA terminology, looking up attribute syntax, understanding concepts |
| `ObjectStereotypes.md` | Overview of all CSLA object stereotypes and base classes | Deciding which base class to use for a business object |
| `DataPortalGuide.md` | In-depth guide to data portal architecture and operations | Understanding data portal flow, client/server architecture, transactions |

### Topic-Specific Documents

| Document | Covers |
| ---------- | -------- |
| `Data-Access.md` | Data access layer patterns, DAL design, encapsulated invocation |
| `DataMapper.md` | Mapping between DTOs and business objects |
| `DataPortalChannels.md` | Data portal channels architecture, built-in channels, implementing custom channels |
| `BusinessRules.md` | Business rule implementation overview |
| `BusinessRulesValidation.md` | Validation rules (Required, StringLength, etc.) |
| `BusinessRulesAsync.md` | Async business rules |
| `BusinessRulesAuthorization.md` | Authorization rules for properties and objects |
| `BusinessRulesCalculation.md` | Calculated/derived property rules |
| `BusinessRulesContext.md` | Rule context and execution flow |
| `BusinessRulesObjectLevel.md` | Object-level validation rules |
| `BusinessRulesPriority.md` | Rule execution priority |
| `BusinessRulesUnitTesting.md` | Unit testing rules with Rocks mocking framework |

### Data Portal Operation Documents

| Document | Operation |
| ---------- | ----------- |
| `DataPortalOperationCreate.md` | `[Create]` - Initializing new objects |
| `DataPortalOperationFetch.md` | `[Fetch]` - Retrieving existing objects |
| `DataPortalOperationInsert.md` | `[Insert]` - Saving new objects |
| `DataPortalOperationUpdate.md` | `[Update]` - Updating existing objects |
| `DataPortalOperationDelete.md` | `[Delete]` and `[DeleteSelf]` - Deleting objects |
| `DataPortalOperationExecute.md` | `[Execute]` - Command execution |

### Configuration Documents

| Document | Covers |
| ---------- | -------- |
| `BlazorConfiguration.md` | Blazor-specific CSLA configuration |
| `HttpDataPortalConfiguration.md` | HTTP channel configuration for remote data portal |
| `GrpcDataPortalConfiguration.md` | gRPC channel configuration |
| `RabbitMqDataPortalConfiguration.md` | RabbitMQ channel configuration |
| `CustomSerializers.md` | Custom serialization configuration |

### Extension Points

| Document | Covers |
| ---------- | -------- |
| `DataPortalActivator.md` | Custom object activation |
| `DataPortalCache.md` | Data portal caching |
| `DataPortalDashboard.md` | Monitoring and diagnostics |
| `DataPortalExceptionInspector.md` | Exception handling customization |
| `DataPortalInterceptor.md` | Intercepting data portal operations |

### Version-Specific Documentation

The `v9/` and `v10/` subdirectories contain version-specific implementation details:

| Folder | CSLA Version | Key Differences |
| -------- | -------------- | ----------------- |
| `v10/` | CSLA 10+ | Uses `[CslaImplementProperties]` for code generation, partial properties |
| `v9/` | CSLA 9.x | Manual property implementation with `GetProperty`/`SetProperty` |

**Version-specific documents include:**

- `Properties.md` - Property declaration patterns (major differences between versions)
- `EditableRoot.md`, `EditableChild.md`, etc. - Complete stereotype implementations
- `Command.md` - Command object patterns
- `InjectAttribute.md` - Dependency injection patterns

## Document Relationships

```text
┌─────────────────────────────────────────────────────────────────┐
│                        README.md (this file)                     │
│                    Entry point / Navigation guide                │
└─────────────────────────────────────────────────────────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Glossary.md   │    │ObjectStereotypes│    │DataPortalGuide  │
│  Quick lookup   │    │  .md            │    │    .md          │
│  & definitions  │    │ Stereotype      │    │ Architecture &  │
│                 │    │ overview        │    │ deep concepts   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
          │                       │                       │
          │              ┌────────┴────────┐              │
          │              ▼                 ▼              │
          │     ┌─────────────┐   ┌─────────────┐         │
          │     │ v10/        │   │ v9/         │         │
          │     │ Stereotype  │   │ Stereotype  │         │
          │     │ impls       │   │ impls       │         │
          │     └─────────────┘   └─────────────┘         │
          │                                               │
          └──────────────────┬────────────────────────────┘
                             ▼
              ┌──────────────────────────────┐
              │  Topic & Operation docs      │
              │  (BusinessRules*, DataPortal │
              │   Operation*, Data-Access)   │
              └──────────────────────────────┘
```

## Search Tips for AI Assistants

**Effective queries:**

- "editable root with child list" - Find parent-child patterns
- "async business rule validation" - Find async rule implementation
- "data portal fetch operation" - Find fetch implementation details
- "authorization rule property" - Find property-level authorization
- "command execute server" - Find command object patterns
- "custom data portal channel" - Find channel implementation guidance

**The search combines:**

- **Semantic/vector search** - Understands concept similarity
- **BM25 keyword search** - Finds exact term matches
- Results are scored and ranked by relevance

## Notes on Document Overlap

Some concepts appear in multiple documents at different levels of detail:

| Concept | Quick Reference | Detailed Coverage |
| --------- | ----------------- | ------------------- |
| Stereotypes | `Glossary.md` | `ObjectStereotypes.md`, `v10/*.md` |
| Data Portal Operations | `Glossary.md` | `DataPortalGuide.md`, `DataPortalOperation*.md` |
| Data Access Models | `Glossary.md` | `DataPortalGuide.md`, `Data-Access.md` |
| Business Rules | `Glossary.md` | `BusinessRules*.md` |
| Properties | `Glossary.md` | `v10/Properties.md`, `v9/Properties.md` |

This intentional overlap allows:

- Quick lookups via `Glossary.md` for syntax and definitions
- Deep dives via specialized documents for implementation guidance

## Additional Resources

For information beyond what's in this documentation:

- [CSLA .NET Website](https://cslanet.com/) - Official documentation, books, and training
- [CSLA .NET GitHub](https://github.com/MarimerLLC/csla) - Source code and issue tracking
- [CSLA .NET Discussions](https://github.com/MarimerLLC/csla/discussions) - Community Q&A

> **Note for AI assistants**: These external links are provided for user reference. The local documentation in this folder should be sufficient for most code generation tasks.
