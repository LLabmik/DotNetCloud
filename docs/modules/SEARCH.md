# DotNetCloud Search Module

> **Module ID:** `dotnetcloud.search`
> **Version:** 1.0.0
> **Status:** Implemented (Phase 8)
> **License:** AGPL-3.0

---

## Overview

The Search module provides full-text search across all DotNetCloud modules. Users type a query and get results from Files, Notes, Chat, Contacts, Calendar, Photos, Music, Video, and Tracks — with permission-scoped visibility ensuring users only see their own content.

The module supports three database backends (PostgreSQL, SQL Server, MariaDB) with provider-specific query optimization, advanced query syntax (phrases, filters, exclusions), and a background indexing pipeline driven by domain events.

## Key Features

| Feature | Description |
|---|---|
| **Cross-Module Search** | Single query searches Files, Notes, Chat, Contacts, Calendar, Photos, Music, Video, Tracks |
| **Multi-Database Support** | PostgreSQL (`tsvector`/`tsquery`), SQL Server (`FREETEXT`), MariaDB (`MATCH AGAINST`) |
| **Advanced Query Syntax** | Quoted phrases, `in:module` filter, `type:value` filter, `-exclusion` negation |
| **Permission Scoping** | Results filtered by `OwnerId` — users only see their own content |
| **Event-Driven Indexing** | Modules publish `SearchIndexRequestEvent` on CRUD; search indexes incrementally |
| **Scheduled Reindex** | Background service runs full reindex every 24 hours (configurable) |
| **Content Extraction** | Extracts searchable text from PDF, DOCX, XLSX, Markdown, and plain text |
| **Snippet Generation** | Context-aware snippets with `<mark>` highlighting (XSS-safe) |
| **Faceted Results** | Result counts per module for sidebar filtering |
| **REST + gRPC APIs** | REST for web/mobile clients, gRPC for inter-module communication |
| **Blazor UI** | Global search bar (Ctrl+K), results page with facets, per-module result cards |
| **Graceful Degradation** | Modules fall back to LIKE queries when the Search module is unavailable |

## Architecture

The Search module follows the standard DotNetCloud module architecture with four projects:

```
src/Modules/Search/
├── DotNetCloud.Modules.Search/            # Core: services, extractors, events, query parser
│   ├── Services/                          # SearchQueryService, SearchIndexingService, SnippetGenerator
│   ├── Extractors/                        # PDF, DOCX, XLSX, Markdown, PlainText extractors
│   └── Events/                            # SearchIndexRequestEventHandler
├── DotNetCloud.Modules.Search.Data/       # EF Core: SearchDbContext, models, configurations
│   ├── Models/                            # SearchIndexEntry, IndexingJob
│   └── Configuration/                     # Entity type configurations
├── DotNetCloud.Modules.Search.Host/       # REST + gRPC host
│   ├── Controllers/                       # SearchController (REST API)
│   ├── Services/                          # SearchGrpcService
│   └── Protos/                            # search_service.proto
└── DotNetCloud.Modules.Search.Client/     # gRPC client library for other modules
    ├── ISearchFtsClient.cs                # Client interface
    ├── SearchFtsClient.cs                 # Implementation with lazy channel
    └── SearchFtsClientOptions.cs          # Configuration
```

### Process Isolation

The Search module runs as a separate process (`dotnetcloud-module search`) and communicates with the DotNetCloud core via gRPC over Unix sockets or Named Pipes.

### Indexing Pipeline

```
Module publishes SearchIndexRequestEvent (entity created/updated/deleted)
        │
        ▼
SearchIndexRequestEventHandler.HandleAsync()
        │
        ├── Action == Remove → ISearchProvider.RemoveDocumentAsync() (immediate)
        │
        └── Action == Index  → SearchIndexingService.EnqueueAsync()
                                        │
                                        ▼
                                Background processing loop (Channel<T>):
                                  1. Fetch SearchDocument from ISearchableModule
                                  2. Extract content via ContentExtractionService
                                  3. Call ISearchProvider.IndexDocumentAsync()
                                        │
                                        ▼
                                SearchIndexCompletedEvent published (batch)
```

### Query Pipeline

```
User submits query string (e.g., "quarterly report in:notes -draft")
        │
        ▼
SearchQueryParser.Parse() → ParsedSearchQuery
  • Terms: ["quarterly", "report"]
  • Phrases: []
  • ModuleFilter: "notes"
  • Exclusions: ["draft"]
        │
        ▼
ParsedSearchQuery → Provider-specific query string
  • PostgreSQL: to_tsquery('quarterly & report & !draft')
  • SQL Server: CONTAINS(*, '"quarterly" AND "report" AND NOT "draft"')
  • MariaDB: MATCH() AGAINST('+quarterly +report -draft' IN BOOLEAN MODE)
        │
        ▼
ISearchProvider.SearchAsync() → SearchResultDto
  • Permission-scoped (WHERE OwnerId = userId)
  • Faceted counts per module
  • SnippetGenerator adds <mark> highlights
```

## Core Abstractions

### Capability Interfaces

| Interface | Tier | Purpose |
|---|---|---|
| `ISearchProvider` | Restricted | Core search operations: index, remove, search, reindex, stats |
| `ISearchableModule` | Public | Modules implement this to expose their content for indexing |
| `IContentExtractor` | Restricted | Extract plain text from file/document streams |

### DTOs (`DotNetCloud.Core.DTOs.Search`)

| Type | Purpose |
|---|---|
| `SearchDocument` | Single indexable item (ModuleId, EntityId, EntityType, Title, Content, OwnerId, Metadata) |
| `SearchQuery` | Search request (QueryText, ModuleFilter, EntityTypeFilter, UserId, Page, PageSize, SortOrder) |
| `SearchResultDto` | Search response (Items, TotalCount, Page, PageSize, FacetCounts) |
| `SearchResultItem` | Individual result (ModuleId, EntityId, Title, Snippet, RelevanceScore, Metadata) |
| `SearchIndexStats` | Index health (TotalDocuments, DocumentsPerModule, LastFullReindexAt) |
| `ExtractedContent` | Extraction result (Text, Metadata dictionary) |

### Events

| Event | Payload | Published When |
|---|---|---|
| `SearchIndexRequestEvent` | ModuleId, EntityId, Action (Index/Remove) | Any module creates/updates/deletes searchable content |
| `SearchIndexCompletedEvent` | Status, DocumentsProcessed, ModuleId | Search module finishes processing a batch |

### Search Sort Order

| Value | Description |
|---|---|
| `Relevance` | Full-text relevance score (default) |
| `DateDesc` | Newest first by UpdatedAt |
| `DateAsc` | Oldest first by UpdatedAt |

## Services

### SearchQueryService

Orchestrates query parsing, provider delegation, and result formatting.

- Parses raw query text via `SearchQueryParser`
- Extracts `in:module` and `type:value` filters from parsed query
- Short-circuits on empty or filter-only queries (returns empty result)
- Delegates to `ISearchProvider.SearchAsync()` with constructed `SearchQuery`

### SearchIndexingService

Background queue for processing indexing requests with backpressure.

- Uses `Channel<T>` with bounded capacity of 1000
- Fetches `SearchDocument` from `ISearchableModule.GetSearchableDocumentAsync()`
- Runs content extraction via `ContentExtractionService`
- Calls `ISearchProvider.IndexDocumentAsync()`
- Tracks `TotalProcessed` and `TotalFailed` counters

### SearchReindexBackgroundService

Hosted service for scheduled and on-demand full reindex.

- Automatic interval: 24 hours (configurable)
- Manual trigger: `TriggerFullReindex()` or `TriggerModuleReindex(moduleId)`
- Batch size: 200 documents per batch
- Creates `IndexingJob` records for progress tracking
- Cleans up orphaned entries for unregistered modules
- Startup delay: 1 minute (allows core to initialize)

### ContentExtractionService

Orchestrates content extraction from file streams.

- Routes to the appropriate `IContentExtractor` by MIME type
- Truncates extracted content to 100KB max
- Returns `ExtractedContent` with text and metadata

### SearchQueryParser

Parses user input into a structured `ParsedSearchQuery`.

| Syntax | Example | Effect |
|---|---|---|
| Keywords | `quarterly report` | Matches documents containing both terms |
| Quoted phrase | `"quarterly report"` | Matches exact phrase |
| Module filter | `in:notes` | Restricts to Notes module |
| Type filter | `type:pdf` | Restricts to entity type |
| Exclusion | `-draft` | Excludes documents containing "draft" |
| Combined | `"project plan" in:files -template` | Phrase search in Files, excluding "template" |

### SnippetGenerator

Generates context-aware text snippets with search term highlighting.

- Extracts ~60 characters of context around matched terms
- Default max snippet length: 200 characters
- Uses `<mark>` tags for highlighting
- XSS-safe: HTML-encodes content before inserting mark tags

## Content Extractors

| Extractor | MIME Types | Library | Features |
|---|---|---|---|
| `PlainTextExtractor` | `text/plain`, `text/csv` | Built-in | UTF-8 stream reading |
| `MarkdownContentExtractor` | `text/markdown` | Built-in + regex | Strips headings, bold, italic, links, images, code blocks |
| `PdfContentExtractor` | `application/pdf` | UglyToad.PdfPig | Per-page text extraction, author/title metadata |
| `DocxContentExtractor` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | DocumentFormat.OpenXml | Paragraph extraction, author/title metadata |
| `XlsxContentExtractor` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | DocumentFormat.OpenXml | Cell values from all sheets, shared string table resolution |

## Database Providers

### PostgreSQL (`PostgreSqlSearchProvider`)

- Full-text: `tsvector`/`tsquery` with `ts_rank()` for relevance
- Fallback: `ILIKE` for keyword matching
- Phrase search: `<->` (follow-by) operator
- Exclusion: `!` operator in tsquery

### SQL Server (`SqlServerSearchProvider`)

- Full-text: `FREETEXT` / `FREETEXTTABLE` with `RANK` for relevance
- Fallback: `Contains()` with LINQ
- Phrase search: quoted terms in `CONTAINS`
- Exclusion: `AND NOT` in `CONTAINS`

### MariaDB (`MariaDbSearchProvider`)

- Full-text: `MATCH() AGAINST()` in `BOOLEAN MODE`
- Fallback: `Contains()` with LINQ
- Phrase search: quoted `+"phrase"` in boolean mode
- Exclusion: `-term` prefix in boolean mode

### Database Schema

#### `SearchIndexEntry`

| Column | Type | Constraints |
|---|---|---|
| `Id` | `long` | PK, auto-increment |
| `ModuleId` | `string(50)` | Required, indexed |
| `EntityId` | `string(64)` | Required, unique with ModuleId |
| `EntityType` | `string(100)` | Required, indexed |
| `Title` | `string(500)` | Required |
| `Content` | `string(102400)` | Max 100KB |
| `Summary` | `string(1000)` | Optional |
| `OwnerId` | `Guid` | Required, indexed (permission scoping) |
| `OrganizationId` | `Guid?` | Optional, indexed |
| `CreatedAt` | `DateTimeOffset` | |
| `UpdatedAt` | `DateTimeOffset` | Indexed |
| `IndexedAt` | `DateTimeOffset` | |
| `MetadataJson` | `string(4000)` | Optional, serialized JSON |

#### `IndexingJob`

| Column | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `ModuleId` | `string?` | Null = global reindex |
| `Type` | `IndexJobType` | Full / Incremental |
| `Status` | `IndexJobStatus` | Pending / Running / Completed / Failed |
| `StartedAt` | `DateTimeOffset?` | |
| `CompletedAt` | `DateTimeOffset?` | |
| `DocumentsProcessed` | `int` | |
| `DocumentsTotal` | `int` | |
| `ErrorMessage` | `string?` | |

## Searchable Modules

Each module implements `ISearchableModule` and publishes `SearchIndexRequestEvent` on CRUD operations:

| Module | Entity Types | CRUD Events |
|---|---|---|
| **Files** | FileNode | CreateFolder, Rename, Move → Index; Delete → Remove |
| **Notes** | Note | Create, Update → Index; Delete → Remove |
| **Chat** | Message | Send, Edit → Index; Delete → Remove |
| **Contacts** | Contact | Create, Update → Index; Delete → Remove |
| **Calendar** | CalendarEvent | Create, Update → Index; Delete → Remove |
| **Photos** | Photo | Create → Index; Delete → Remove |
| **Music** | Track, Artist, Album | IndexFile → Index; Delete → Remove |
| **Video** | Video | Create → Index; Delete → Remove |
| **Tracks** | Card, Board, Label | Create, Update, Move → Index; Delete → Remove |

## Configuration

### Search Module Host (`appsettings.json`)

Database connection is configured per provider. The module auto-selects the appropriate `ISearchProvider` implementation.

### Client Configuration

Modules that consume search use `SearchFtsClientOptions`:

```json
{
  "SearchModule": {
    "SearchModuleAddress": "unix:///var/run/dotnetcloud/search.sock",
    "Timeout": "00:00:10"
  }
}
```

| Key | Type | Default | Description |
|---|---|---|---|
| `SearchModuleAddress` | `string?` | `null` | gRPC address. Supports `http://`, `https://`, `unix://`. Null disables. |
| `Timeout` | `TimeSpan` | 10 seconds | gRPC call deadline |

Registration:

```csharp
services.AddSearchFtsClient(configuration);
// or with explicit address:
services.AddSearchFtsClient("unix:///var/run/dotnetcloud/search.sock");
```

## Blazor UI Components

### Global Search Bar (`GlobalSearchBar.razor`)

- Activated with **Ctrl+K** / **Cmd+K** keyboard shortcut
- Modal overlay with debounced input (300ms)
- Live suggestions from `/api/v1/search/suggest`
- Keyboard navigation: ↑↓ to select, Enter to open, Esc to close
- Recent searches stored in `localStorage`
- Per-module icons and badges in suggestion results

### Search Results Page (`SearchResults.razor`)

- Route: `/search?q=...`
- Left sidebar with faceted module filters and counts
- Sort toggle: Relevance or Date
- Pagination with URL state management
- Loading, empty, and error states

### Search Result Card (`SearchResultCard.razor`)

- Per-module result rendering with rich metadata display
- XSS-safe highlight sanitizer (only allows `<mark>` tags)
- Deep-link URL generation for all 10 modules
- Module-specific metadata: file size, MIME type, contact details, event dates, etc.

## API Reference

See [docs/api/search.md](../api/search.md) for the complete API reference.

## Admin Operations

### Triggering a Reindex

**Full reindex (all modules):**
```bash
curl -X POST https://your-instance/api/v1/search/admin/reindex \
  -H "Authorization: Bearer <admin-token>"
```

**Module-specific reindex:**
```bash
curl -X POST https://your-instance/api/v1/search/admin/reindex/files \
  -H "Authorization: Bearer <admin-token>"
```

### Monitoring Index Health

```bash
curl https://your-instance/api/v1/search/stats \
  -H "Authorization: Bearer <admin-token>"
```

Returns:
```json
{
  "success": true,
  "data": {
    "totalDocuments": 12345,
    "documentsPerModule": {
      "files": 8000,
      "notes": 2500,
      "chat": 1200,
      "contacts": 450,
      "calendar": 195
    },
    "lastFullReindexAt": "2026-01-15T03:00:00Z",
    "lastIncrementalIndexAt": "2026-01-15T14:32:00Z"
  }
}
```

## Tests

| Test File | Phase | Tests | Coverage |
|---|---|---|---|
| `SqlServerSearchProviderTests` | 2 | 32 | Index, upsert, remove, search, pagination, facets, permission scoping |
| `MariaDbSearchProviderTests` | 2 | 10 | Index, upsert, remove, search, permission scoping, facets |
| `SearchQueryServiceTests` | 2 | 5 | Empty query, delegation, stats, reindex |
| `ContentExtractionServiceTests` | 2 | 10 | MIME routing, truncation, error handling |
| `PlainTextExtractorTests` | 2 | 9 | text/plain, text/csv, Unicode, empty |
| `MarkdownContentExtractorTests` | 2 | 17 | Syntax stripping, metadata |
| `SearchIndexingServiceTests` | 2 | 8 | Queue, process, lifecycle |
| `SearchIndexRequestEventHandlerTests` | 2 | 3 | Remove, index, null provider |
| `SearchModuleTests` | 2 | 10 | Lifecycle, manifest, events |
| `SearchModuleManifestTests` | 2 | 9 | All manifest properties |
| `SearchDbContextTests` | 2 | 9 | CRUD for both entities |
| Phase 4 tests (5 files) | 4 | 43 | Indexing pipeline, reindex, event handler, content extraction |
| `SearchQueryParserTests` | 5 | 28 | Keyword, phrase, filter, exclusion parsing |
| `ParsedSearchQueryTests` | 5 | 20 | Provider-specific query builders |
| `SnippetGeneratorTests` | 5 | 18 | Highlighting, XSS, edge cases |
| Phase 5 integration tests (3 files) | 5 | 59 | Query engine, cross-module aggregation |
| Phase 6 tests (7 files) | 6 | 89 | REST controller, gRPC, FTS client, module integration |
| Phase 7 tests (6 files) | 7 | 159 | URL generation, sanitizer, display format, metadata, sort |
| Phase 8 tests (4 files) | 8 | 40 | Permission scoping, E2E integration, multi-DB, performance |
| **Total** | | **631** | |

## Documentation

| Document | Audience | Path |
|---|---|---|
| Module Documentation | Developers | [docs/modules/SEARCH.md](SEARCH.md) (this file) |
| API Reference | Developers | [docs/api/search.md](../api/search.md) |
| Architecture | Developers | [docs/architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md) (Section 25) |
| Implementation Plan | Developers | [docs/FULL_TEXT_SEARCH_IMPLEMENTATION_PLAN.md](../FULL_TEXT_SEARCH_IMPLEMENTATION_PLAN.md) |
