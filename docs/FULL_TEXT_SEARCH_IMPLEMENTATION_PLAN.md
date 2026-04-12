# Full-Text Search Implementation Plan

**Status:** In Progress  
**Phase:** 8 (Search, Auto-Updates & Polish)  
**Created:** 2026-04-12  
**Last Updated:** 2026-06-11

---

## Overview

Implement a **process-isolated Search module** (`DotNetCloud.Modules.Search`) that provides cross-module full-text search using **native database FTS** (PostgreSQL `tsvector`/`tsquery`, SQL Server Full-Text Index, MariaDB `FULLTEXT INDEX`). Modules expose searchable data via new gRPC search RPCs. The Search module maintains a centralized search index, kept in sync via **event-driven indexing + scheduled full reindex**. Users get both a **global search bar** (Ctrl+K) and **enhanced per-module search**. **Document content extraction** (PDF, DOCX, etc.) uses .NET native libraries.

---

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Search backend** | Native database FTS (all 3 providers) | No external infrastructure; leverages existing DB. PostgreSQL `tsvector`, SQL Server Full-Text Index, MariaDB `FULLTEXT INDEX` |
| **Module type** | Standalone process-isolated module | Consistent with architecture; communicates via gRPC like all other modules |
| **Content extraction** | .NET native libraries | PdfPig (PDF), DocumentFormat.OpenXml (DOCX/XLSX), built-in Markdown/plaintext. No JVM dependency |
| **Indexing strategy** | Hybrid (event-driven + scheduled) | Real-time indexing on CRUD events, daily full reindex for consistency |
| **UX** | Global + per-module search | Unified global search bar (Ctrl+K) AND enhanced per-module search endpoints using FTS |
| **Content scope** | Metadata + document content | File names, titles, descriptions, tags, AND extracted text from PDF/DOCX/XLSX |
| **DB providers** | All three get native FTS | PostgreSQL, SQL Server, and MariaDB each use their native full-text search capabilities |

---

## Scope Boundaries

### Included

- `ISearchProvider` abstraction with PostgreSQL, SQL Server, MariaDB implementations
- `ISearchableModule` gRPC service contract for modules to expose searchable data
- Search module with own DbContext, search index tables, gRPC host
- Event-driven indexing via `IEventBus` subscriptions
- Scheduled full-reindex background service
- Content extraction pipeline for PDF, DOCX, XLSX, plain text, Markdown
- Global search REST + gRPC API endpoints
- Blazor global search bar (Ctrl+K) + results page
- Enhanced per-module search using FTS instead of LIKE
- Unit + integration tests across all DB providers

### Excluded (future enhancements)

- Semantic/AI-powered search
- OCR for image text extraction
- Search analytics/query logging dashboard
- Federated search across multiple DotNetCloud instances

---

## Searchable Data Inventory

| Module | Searchable Entities | Key Searchable Fields | Document Content Extraction |
|--------|--------------------|-----------------------|-----------------------------|
| **Files** | FileNode, FileComment | File/folder names, comments, MIME types | ✓ PDF, DOCX, XLSX, plain text |
| **Chat** | Message, Channel | Message content (Markdown), channel name/description/topic | — |
| **Notes** | Note, NoteFolder, NoteTag | Note title + content (Markdown/plain), folder names, tags | — |
| **Contacts** | Contact, ContactEmail, ContactPhone | Display name, first/last name, org, department, job title, notes, emails, phones | — |
| **Calendar** | CalendarEvent, Calendar | Event title, description, location, URL, calendar name | — |
| **Photos** | Photo, Album, PhotoTag | Filename, album name/description, tags, EXIF metadata (camera, GPS, date) | — |
| **Music** | Track, Artist, MusicAlbum, Playlist | Track title, artist name, album title, playlist name/description, genre | — |
| **Video** | Video, VideoCollection, Subtitle | Video title, collection name/description, **full subtitle text** | — |
| **Tracks** | Card, Board, Label, CardComment, Sprint | Card title + description, board name, label names, comments, sprint name/goal | — |
| **AI** | ConversationMessage | Conversation title, message content (user + assistant) | — |

---

## Implementation Phases

### Phase 1: Core Search Interfaces & DTOs

> **No dependencies. Start here.**

#### Step 1.1 — `ISearchProvider` Interface

- **Location:** `src/Core/DotNetCloud.Core/Capabilities/ISearchProvider.cs`
- **Tier:** Restricted capability interface (extends `ICapabilityInterface`)
- **Operations:**
  - `IndexDocumentAsync(SearchDocument doc)` — add/update a document in the index
  - `RemoveDocumentAsync(string moduleId, string entityId)` — remove from index
  - `SearchAsync(SearchQuery query)` — execute a full-text search
  - `ReindexModuleAsync(string moduleId)` — trigger full module reindex
  - `GetIndexStatsAsync()` — index health/statistics

#### Step 1.2 — Search DTOs

- **Location:** `src/Core/DotNetCloud.Core/DTOs/Search/`

**`SearchDocument`** — represents a single indexable item:
```
ModuleId        (string)    e.g., "files", "notes", "chat"
EntityId        (string)    Guid as string
EntityType      (string)    e.g., "Note", "Message", "FileNode"
Title           (string)    Primary searchable title
Content         (string)    Body text / extracted content
Summary         (string?)   Snippet for display
OwnerId         (Guid)      For permission scoping
OrganizationId  (Guid?)     For org-level scoping
CreatedAt       (DateTimeOffset)
UpdatedAt       (DateTimeOffset)
Metadata        (IReadOnlyDictionary<string, string>)  Tags, MIME type, etc.
```

**`SearchQuery`** — search request:
```
QueryText         (string)            User's search text
ModuleFilter      (string?)           null = all modules
EntityTypeFilter  (string?)           e.g., "Note", "Message"
UserId            (Guid)              Permission scoping
Page              (int)               Page number
PageSize          (int)               Results per page
SortOrder         (SearchSortOrder)   Relevance, DateDesc, DateAsc
```

**`SearchResultDto`** — aggregated response:
```
Items       (IReadOnlyList<SearchResultItem>)
TotalCount  (int)
Page        (int)
PageSize    (int)
FacetCounts (IReadOnlyDictionary<string, int>)  Results per module
```

**`SearchResultItem`** — individual result:
```
ModuleId        (string)
EntityId        (string)
EntityType      (string)
Title           (string)            Highlighted title
Snippet         (string)            Highlighted text excerpt
RelevanceScore  (double)
UpdatedAt       (DateTimeOffset)
Metadata        (IReadOnlyDictionary<string, string>)
```

#### Step 1.3 — `ISearchableModule` Capability Interface

- **Location:** `src/Core/DotNetCloud.Core/Capabilities/ISearchableModule.cs`
- Modules implement this to expose searchable data to the Search module:
  - `GetAllSearchableDocumentsAsync(Guid userId, CancellationToken ct)` → `IReadOnlyList<SearchDocument>` — for full reindex
  - `GetSearchableDocumentAsync(string entityId, CancellationToken ct)` → `SearchDocument?` — for single-item reindex
  - `ModuleId` property — module identifier string
  - `SupportedEntityTypes` property — entity types this module provides

#### Step 1.4 — Search Events

- **Location:** `src/Core/DotNetCloud.Core/Events/Search/`

**`SearchIndexRequestEvent`** — published by any module when content changes:
```
ModuleId   (string)
EntityId   (string)
Action     (SearchIndexAction)   Index | Remove
```

**`SearchIndexCompletedEvent`** — published by Search module after processing:
```
Status              (IndexCompletionStatus)
DocumentsProcessed  (int)
```

#### Step 1.5 — `IContentExtractor` Interface

- **Location:** `src/Core/DotNetCloud.Core/Capabilities/IContentExtractor.cs`
- `ExtractAsync(Stream fileStream, string mimeType, CancellationToken ct)` → `ExtractedContent?`
- `CanExtract(string mimeType)` → `bool` — checks if this extractor handles the MIME type

**`ExtractedContent`** record:
```
Text      (string)                               Extracted plain text
Metadata  (IReadOnlyDictionary<string, string>)  Author, title, page count, etc.
```

---

### Phase 2: Search Module Scaffold ✅

> **Depends on Phase 1.** Status: **COMPLETED**

#### Step 2.1 — Project Structure

Create the standard three-project module structure:

```
src/Modules/Search/
├── DotNetCloud.Modules.Search/                # Business logic, services
│   ├── SearchModuleManifest.cs
│   ├── SearchModule.cs                        # IModule lifecycle
│   ├── Services/
│   │   ├── SearchIndexingService.cs           # Processes indexing requests
│   │   ├── SearchQueryService.cs              # Executes searches
│   │   └── ContentExtractionService.cs        # Orchestrates extractors
│   ├── Extractors/
│   │   ├── PdfContentExtractor.cs             # UglyToad.PdfPig
│   │   ├── DocxContentExtractor.cs            # DocumentFormat.OpenXml
│   │   ├── XlsxContentExtractor.cs            # DocumentFormat.OpenXml
│   │   ├── PlainTextExtractor.cs              # UTF-8 text files
│   │   └── MarkdownContentExtractor.cs        # Strip markdown → plain text
│   └── Events/
│       └── SearchEventHandlers.cs             # Subscribes to module events
├── DotNetCloud.Modules.Search.Data/           # EF Core, DbContext, migrations
│   ├── SearchDbContext.cs
│   ├── Models/
│   │   ├── SearchIndexEntry.cs                # Main search index table
│   │   └── IndexingJob.cs                     # Reindex job tracking
│   ├── Configuration/
│   │   ├── SearchIndexEntryConfiguration.cs
│   │   └── IndexingJobConfiguration.cs
│   └── Migrations/
│       ├── PostgreSql/
│       ├── SqlServer/
│       └── MariaDb/
└── DotNetCloud.Modules.Search.Host/           # gRPC host + REST controllers
    ├── Program.cs
    ├── Protos/
    │   └── search_service.proto
    ├── Controllers/
    │   └── SearchController.cs
    └── Services/
        └── SearchGrpcService.cs
```

#### Step 2.2 — SearchDbContext & Index Table Model

**`SearchIndexEntry`** entity:
```
Id               (long)               PK, auto-increment
ModuleId         (string)             Indexed
EntityId         (string)             Composite unique with ModuleId
EntityType       (string)
Title            (string)
Content          (string)             The indexed text body
Summary          (string?)
OwnerId          (Guid)               Indexed — for permission-scoped queries
OrganizationId   (Guid?)              Indexed
CreatedAt        (DateTimeOffset)
UpdatedAt        (DateTimeOffset)
IndexedAt        (DateTimeOffset)
MetadataJson     (string?)            Serialized metadata dictionary
```

**Provider-specific FTS columns:**

| Provider | Column/Index | EF Core Configuration |
|----------|-------------|----------------------|
| **PostgreSQL** | `tsvector SearchVector` (stored computed column) | `HasGeneratedTsVectorColumn("SearchVector", "english", e => new { e.Title, e.Content })` + GIN index |
| **SQL Server** | Full-Text Catalog + Full-Text Index | `HasAnnotation` or raw migration SQL for Full-Text Catalog on `Title` + `Content` |
| **MariaDB** | `FULLTEXT INDEX` on `(Title, Content)` | Raw migration SQL for `ALTER TABLE ... ADD FULLTEXT INDEX` |

**`IndexingJob`** entity:
```
Id                  (Guid)
ModuleId            (string)
Type                (IndexJobType)      Full | Incremental
Status              (IndexJobStatus)    Pending | Running | Completed | Failed
StartedAt           (DateTimeOffset?)
CompletedAt         (DateTimeOffset?)
DocumentsProcessed  (int)
DocumentsTotal      (int)
ErrorMessage        (string?)
```

#### Step 2.3 — Provider-Specific `ISearchProvider` Implementations

| Implementation | FTS Query Syntax | Ranking | Selection |
|---------------|-----------------|---------|-----------|
| `PostgreSqlSearchProvider` | `plainto_tsquery('english', ...)`, `to_tsquery()` | `ts_rank()`, `ts_rank_cd()` | Auto-selected when PostgreSQL is configured |
| `SqlServerSearchProvider` | `FREETEXT()`, `FREETEXTTABLE()`, `CONTAINSTABLE()` | `RANK` column from FREETEXTTABLE | Auto-selected when SQL Server is configured |
| `MariaDbSearchProvider` | `MATCH(...) AGAINST(... IN BOOLEAN MODE)` | `MATCH()` relevance score | Auto-selected when MariaDB is configured |

All share the same `SearchDbContext` but use provider-specific query syntax. Selection follows the same pattern as `ITableNamingStrategy`.

#### Step 2.4 — SearchModuleManifest

```
Module ID:            "search"
Required capabilities: ISearchableModule (from each module), IEventBus, IStorageProvider
Published events:      SearchIndexCompletedEvent
Subscribed events:     SearchIndexRequestEvent, FileUploadedEvent, FileDeletedEvent,
                       MessageSentEvent, MessageEditedEvent, MessageDeletedEvent,
                       NoteCreatedEvent, NoteDeletedEvent, CalendarEventCreatedEvent,
                       PhotoUploadedEvent, PhotoDeletedEvent, ...
```

#### Step 2.5 — gRPC Proto Definition

**`search_service.proto`:**
```protobuf
rpc Search(SearchRequest) returns (SearchResponse);
rpc IndexDocument(IndexDocumentRequest) returns (IndexDocumentResponse);
rpc RemoveDocument(RemoveDocumentRequest) returns (RemoveDocumentResponse);
rpc ReindexModule(ReindexModuleRequest) returns (ReindexModuleResponse);
rpc GetIndexStats(GetIndexStatsRequest) returns (IndexStatsResponse);
```

Messages mirror the DTO structures from Phase 1.

---

### Phase 3: Module Search API Integration

> **Depends on Phase 1. Can run in parallel with Phase 2.**

Each module needs gRPC search RPCs so the Search module can pull searchable data. Also add `SearchIndexRequestEvent` publishing on CRUD operations.

#### Step 3.1 — Add Search RPCs to Module Protos

Add to each of the 10 module proto files:
```protobuf
rpc GetSearchableDocuments(GetSearchableDocumentsRequest) returns (stream SearchableDocument);
rpc GetSearchableDocument(GetSearchableDocumentRequest) returns (SearchableDocument);
```

**Affected protos:**
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Protos/files_service.proto`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Protos/chat_service.proto`
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Protos/notes_service.proto`
- `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/Protos/contacts_service.proto`
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Protos/calendar_service.proto`
- `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/Protos/photos_service.proto`
- `src/Modules/Music/DotNetCloud.Modules.Music.Host/Protos/music_service.proto`
- `src/Modules/Video/DotNetCloud.Modules.Video.Host/Protos/video_service.proto`
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Protos/tracks_service.proto`
- AI module (if proto-based)

#### Step 3.2 — Implement Search RPCs Per Module

Each module maps its entities to `SearchableDocument` proto messages:

| Module | Entity → SearchableDocument Mapping |
|--------|-------------------------------------|
| **Files** | `FileNode` → Title=Name, Content=extracted file content (via content extraction), Metadata={MimeType, Path, Size} |
| **Chat** | `Message` → Title=Channel.Name, Content=Message.Content, Metadata={ChannelId, SenderId} |
| **Notes** | `Note` → Title=Title, Content=Content (strip Markdown), Metadata={Format, FolderId} |
| **Contacts** | `Contact` → Title=DisplayName, Content=concat(FirstName, LastName, Org, Notes, Emails, Phones), Metadata={ContactType} |
| **Calendar** | `CalendarEvent` → Title=Title, Content=Description+Location, Metadata={StartUtc, EndUtc, CalendarId} |
| **Photos** | `Photo` → Title=FileName, Content=EXIF metadata text, Metadata={AlbumId, TakenAt, Camera} |
| **Music** | `Track`+`Artist`+`Album` → Title=Track.Title, Content=concat(Artist.Name, Album.Title), Metadata={Genre, Year} |
| **Video** | `Video` → Title=Title, Content=Subtitle text, Metadata={Duration, Resolution, CollectionId} |
| **Tracks** | `Card` → Title=Card.Title, Content=Card.Description+Comments, Metadata={BoardId, Status, Labels} |
| **AI** | `ConversationMessage` → Title=Conversation title, Content=Message.Content, Metadata={Role, ConversationId} |

#### Step 3.3 — Publish `SearchIndexRequestEvent` on CRUD Operations

Each module publishes `SearchIndexRequestEvent` when searchable entities are created, updated, or deleted. Wire into existing service methods following the existing event patterns (e.g., `FileUploadedEvent` in Files module).

**Affected services:**
- `FilesService` — on upload, rename, move, delete, restore
- `MessageService` — on send, edit, delete
- `NoteService` — on create, update, delete
- `ContactService` — on create, update, delete
- `CalendarEventService` — on create, update, delete
- `PhotoService` — on upload, edit, delete
- `MusicService` — on library scan, track metadata update, delete
- `VideoService` — on upload, metadata update, delete
- `CardService` (Tracks) — on create, update, delete, comment add/edit/delete
- `ConversationService` (AI) — on message create

---

### Phase 4: Indexing Engine

> **Depends on Phase 2 + Phase 3.**

#### Step 4.1 — Event-Driven Indexing Service

- `SearchIndexingService` subscribes to `SearchIndexRequestEvent` via `IEventBus`
- On receive: calls the originating module's `GetSearchableDocument` gRPC RPC to get fresh data
- For file content: checks MIME type, runs through `IContentExtractor` pipeline if applicable
- Upserts or deletes from `SearchIndexEntry` table via `ISearchProvider`
- Uses a **background channel** (`System.Threading.Channels.Channel<T>`) for backpressure — events are queued and processed sequentially to avoid DB contention

#### Step 4.2 — Scheduled Full-Reindex Service

- `SearchReindexBackgroundService` (implements `BackgroundService`)
- Configurable schedule via module settings (default: daily at 2 AM)
- Iterates each registered module, calls `GetSearchableDocuments` gRPC streaming RPC
- Batch-upserts into search index (chunks of 100–500 documents)
- Creates `IndexingJob` records for progress tracking
- Admin API endpoint to trigger manual reindex: `POST /api/v1/search/admin/reindex`

#### Step 4.3 — Content Extraction Pipeline

`ContentExtractionService` orchestrates registered `IContentExtractor` implementations:

| Extractor | NuGet Package | MIME Types |
|-----------|--------------|------------|
| `PdfContentExtractor` | `UglyToad.PdfPig` | `application/pdf` |
| `DocxContentExtractor` | `DocumentFormat.OpenXml` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| `XlsxContentExtractor` | `DocumentFormat.OpenXml` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| `PlainTextExtractor` | Built-in | `text/plain`, `text/csv` |
| `MarkdownContentExtractor` | Built-in | `text/markdown` |

**Pipeline flow:** Check MIME type → select extractor → extract text → truncate to max indexable length (configurable, default **100KB** text) → return `ExtractedContent`.

Extracted content stored in `SearchIndexEntry.Content`.  
File content fetched via module's gRPC file download RPC.

#### Step 4.4 — Index Management

- **Stale entry cleanup:** entries with no matching module entity → removed during full reindex
- **Index statistics:** total documents per module, last index time, index size
- **Admin endpoints:** reindex, stats, clear module index

---

### Phase 5: Search Query Engine

> **Depends on Phase 2.**

#### Step 5.1 — Query Parsing

`SearchQueryParser` parses user input into a structured query:

| Syntax | Example | Meaning |
|--------|---------|---------|
| Keywords | `quarterly report` | Full-text search for both terms |
| Quoted phrase | `"quarterly report"` | Exact phrase match |
| Module filter | `in:notes budget` | Restrict to notes module |
| Type filter | `type:pdf annual` | Filter by entity type/MIME |
| Exclusion | `-draft` | Exclude term from results |

Output: `ParsedSearchQuery` with terms, phrases, filters, exclusions.

#### Step 5.2 — Provider-Specific Query Translation

| Provider | Query Translation | Ranking |
|----------|------------------|---------|
| **PostgreSQL** | `plainto_tsquery('english', ...)` or `to_tsquery()` for advanced syntax | `ts_rank()` scoring |
| **SQL Server** | `FREETEXTTABLE(SearchIndex, (Title, Content), ...)` | `RANK` column |
| **MariaDB** | `MATCH(Title, Content) AGAINST(... IN BOOLEAN MODE)` | `MATCH()` relevance score |

All providers apply:
- `WHERE OwnerId = @userId` (permission scoping)
- Module/type filters
- Pagination
- Sort order (relevance or date)

#### Step 5.3 — Cross-Module Result Aggregation

- `SearchQueryService.SearchAsync()` — single entry point
- Calls `ISearchProvider.SearchAsync()` which queries the unified index table
- Groups results by `ModuleId` for facet counts
- Returns `SearchResultDto` with items + facet counts

#### Step 5.4 — Snippet Generation

| Provider | Snippet Method | Notes |
|----------|---------------|-------|
| **PostgreSQL** | `ts_headline()` | Built-in automatic highlighting |
| **SQL Server** | Manual extraction | Locate match position in stored content, extract surrounding text |
| **MariaDB** | Manual extraction | Same as SQL Server approach |

All snippets use HTML-safe highlighting with `<mark>` tags (sanitized to prevent XSS).

---

### Phase 6: REST + gRPC API

> **Depends on Phase 5.**

#### Step 6.1 — REST SearchController

**Location:** `src/Modules/Search/DotNetCloud.Modules.Search.Host/Controllers/SearchController.cs`

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `GET` | `/api/v1/search?q={query}&module={moduleId}&type={entityType}&page={page}&pageSize={size}&sort={relevance\|date}` | Global search | Authenticated |
| `GET` | `/api/v1/search/suggest?q={prefix}` | Autocomplete suggestions (top 5–10) | Authenticated |
| `GET` | `/api/v1/search/stats` | Index statistics | Admin |
| `POST` | `/api/v1/search/admin/reindex` | Trigger full reindex | Admin |
| `POST` | `/api/v1/search/admin/reindex/{moduleId}` | Reindex specific module | Admin |

Standard envelope response format (matches existing API pattern). `CallerContext` for permission scoping.

#### Step 6.2 — gRPC SearchGrpcService

- Implements proto from Step 2.5
- Used by other modules to query search results programmatically
- Same underlying `SearchQueryService`

#### Step 6.3 — Enhanced Per-Module Search Endpoints

Upgrade existing module search endpoints to use FTS:

| Module | Endpoint | Current | Upgraded |
|--------|----------|---------|----------|
| **Files** | `GET /api/v1/files/search` | LIKE query | FTS via Search module gRPC |
| **Chat** | `GET /api/v1/chat/channels/{id}/messages/search` | LIKE query | FTS via Search module gRPC |
| **Notes** | `GET /api/v1/notes/search` | Case-insensitive substring | FTS via Search module gRPC |

Each module calls the Search module's gRPC with a module filter for consistency.

---

### Phase 7: Blazor UI

> **Depends on Phase 6. Can run in parallel with Phase 8.**

#### Step 7.1 — Global Search Bar Component

**Location:** `src/UI/DotNetCloud.UI.Shared/Components/Search/`

- `GlobalSearchBar.razor` — keyboard shortcut (Ctrl+K / Cmd+K) opens modal overlay
- Debounced input (300ms) → calls `/api/v1/search/suggest` for live suggestions
- Enter/submit → navigates to full search results page
- Shows recent searches (stored in browser localStorage)

#### Step 7.2 — Search Results Page

- `SearchResults.razor` — full results page at `/search?q=...`
- **Left sidebar:** module facet filters with counts (Files: 23, Notes: 5, Chat: 12, ...)
- **Main content:** result cards in flat list (sortable)
- **Each result card:** icon (per module/type), title (highlighted), snippet (highlighted), module badge, timestamp
- Pagination (page-based, matching existing patterns)
- Sort toggle: Relevance / Date

#### Step 7.3 — Per-Module Search Result Renderers

Rich, module-specific result card components:

| Component | Module | Rich Features |
|-----------|--------|---------------|
| `FileSearchResult.razor` | Files | File icon by MIME type, path breadcrumb, file size |
| `NoteSearchResult.razor` | Notes | Note icon, folder path, content preview |
| `ChatMessageSearchResult.razor` | Chat | Channel name, sender avatar, message preview |
| `ContactSearchResult.razor` | Contacts | Contact avatar, email, phone |
| `CalendarEventSearchResult.razor` | Calendar | Event date/time, location |
| `PhotoSearchResult.razor` | Photos | Thumbnail, album name |
| `MusicSearchResult.razor` | Music | Track/artist/album, cover art |
| `VideoSearchResult.razor` | Video | Video thumbnail, duration |
| `TrackCardSearchResult.razor` | Tracks | Board name, card status, labels |
| `AiConversationSearchResult.razor` | AI | Conversation title, message preview |

Click on any result → deep-links to the entity in its module.

---

### Phase 8: Testing & Documentation

> **Parallel with Phases 6–7.**

#### Step 8.1 — Unit Tests

**Location:** `tests/DotNetCloud.Modules.Search.Tests/`

- `SearchQueryParser` — keyword, phrase, filter, exclusion parsing
- Each `ISearchProvider` implementation with test DB
- `ContentExtractionService` — PDF, DOCX, XLSX, plain text, Markdown
- `SearchIndexingService` — event processing, upsert, delete
- Permission scoping — user A cannot see user B's results

#### Step 8.2 — Integration Tests

- **Multi-database:** run search tests against PostgreSQL, SQL Server, MariaDB
- **End-to-end:** create entity in module → verify it appears in search results
- **Reindex:** trigger full reindex → verify all documents indexed
- **Content extraction:** upload PDF → verify extracted text is searchable

#### Step 8.3 — Performance Benchmarks

- Benchmark search with 10K, 100K, 1M documents
- Measure indexing throughput (documents/second)
- Measure query latency (p50, p95, p99)
- Identify and document scaling limits per DB provider

#### Step 8.4 — Documentation

- `docs/modules/SEARCH.md` — module documentation
- `docs/api/search.md` — API reference
- `docs/architecture/ARCHITECTURE.md` — add search architecture section
- Admin guide: configuring search, triggering reindex, monitoring
- Update `MASTER_PROJECT_PLAN.md` and `IMPLEMENTATION_CHECKLIST.md`

---

## Phase Dependency Graph

```
Phase 1: Core Interfaces
    │
    ├──────────────────────┐
    ▼                      ▼
Phase 2: Module Scaffold   Phase 3: Module Search APIs
    │                      │
    ├──── Phase 5: Query ──┤
    │     Engine           │
    │                      │
    └──────────┬───────────┘
               ▼
         Phase 4: Indexing Engine
               │
               ▼
         Phase 6: REST + gRPC API
               │
         ┌─────┴─────┐
         ▼           ▼
  Phase 7: UI    Phase 8: Testing
```

---

## Key Files Reference

### Create New — Core Interfaces

| File | Purpose |
|------|---------|
| `src/Core/DotNetCloud.Core/Capabilities/ISearchProvider.cs` | Provider-agnostic FTS abstraction |
| `src/Core/DotNetCloud.Core/Capabilities/ISearchableModule.cs` | Modules expose searchable data |
| `src/Core/DotNetCloud.Core/Capabilities/IContentExtractor.cs` | Document text extraction |
| `src/Core/DotNetCloud.Core/DTOs/Search/SearchDocument.cs` | Indexable document DTO |
| `src/Core/DotNetCloud.Core/DTOs/Search/SearchQuery.cs` | Search request DTO |
| `src/Core/DotNetCloud.Core/DTOs/Search/SearchResultDto.cs` | Search response DTO |
| `src/Core/DotNetCloud.Core/DTOs/Search/SearchResultItem.cs` | Individual result DTO |
| `src/Core/DotNetCloud.Core/DTOs/Search/ExtractedContent.cs` | Extraction result DTO |
| `src/Core/DotNetCloud.Core/Events/Search/SearchIndexRequestEvent.cs` | Module → Search indexing event |
| `src/Core/DotNetCloud.Core/Events/Search/SearchIndexCompletedEvent.cs` | Search → modules completion event |

### Create New — Search Module (3 projects)

| Project | Purpose |
|---------|---------|
| `src/Modules/Search/DotNetCloud.Modules.Search/` | Business logic, content extractors, event handlers |
| `src/Modules/Search/DotNetCloud.Modules.Search.Data/` | SearchDbContext, models, EF configurations, migrations |
| `src/Modules/Search/DotNetCloud.Modules.Search.Host/` | gRPC host, REST controller, proto definition |

### Extend — Module Protos (add search RPCs)

| File |
|------|
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/Protos/files_service.proto` |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Protos/chat_service.proto` |
| `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Protos/notes_service.proto` |
| `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/Protos/contacts_service.proto` |
| `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Protos/calendar_service.proto` |
| `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/Protos/photos_service.proto` |
| `src/Modules/Music/DotNetCloud.Modules.Music.Host/Protos/music_service.proto` |
| `src/Modules/Video/DotNetCloud.Modules.Video.Host/Protos/video_service.proto` |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Protos/tracks_service.proto` |

### Modify — Module Services (publish `SearchIndexRequestEvent`)

Each module's main service class — add event publishing on CRUD operations.

### Create New — Blazor UI

| File | Purpose |
|------|---------|
| `src/UI/DotNetCloud.UI.Shared/Components/Search/GlobalSearchBar.razor` | Ctrl+K search overlay |
| `src/UI/DotNetCloud.UI.Shared/Components/Search/SearchResults.razor` | Full results page |
| `src/UI/DotNetCloud.UI.Shared/Components/Search/*SearchResult.razor` | 10 module-specific result renderers |

### Create New — Tests

| File | Purpose |
|------|---------|
| `tests/DotNetCloud.Modules.Search.Tests/` | Unit + integration tests |

### Reference — Existing Patterns

| File | Pattern |
|------|---------|
| `src/Core/DotNetCloud.Core/Capabilities/IMediaSearchService.cs` | Cross-module search aggregation |
| `src/Core/DotNetCloud.Core/DTOs/MediaSearchResultDto.cs` | Aggregated result DTO |
| `src/Core/DotNetCloud.Core/Capabilities/ITableNamingStrategy.cs` | Multi-DB provider abstraction |
| `src/Modules/Files/DotNetCloud.Modules.Files/FilesModuleManifest.cs` | Module manifest pattern |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesDbContext.cs` | Module DbContext pattern |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` | Existing search endpoint pattern |

---

## NuGet Packages

| Package | Purpose | Used By |
|---------|---------|---------|
| `UglyToad.PdfPig` | PDF text extraction | `PdfContentExtractor` |
| `DocumentFormat.OpenXml` | DOCX/XLSX text extraction | `DocxContentExtractor`, `XlsxContentExtractor` |

No additional packages needed for FTS — native to EF Core providers (Npgsql, Microsoft.EntityFrameworkCore.SqlServer, Pomelo.EntityFrameworkCore.MySql).

---

## Verification Checklist

| # | Test | Expected Result |
|---|------|----------------|
| 1 | `dotnet build` | Zero errors/warnings after each phase |
| 2 | `dotnet test tests/DotNetCloud.Modules.Search.Tests/` | All unit tests pass |
| 3 | Create a Note → global search | Note appears in search within 5 seconds (event-driven) |
| 4 | Same test on SQL Server | FTS abstraction works identically |
| 5 | Same test on MariaDB | FULLTEXT INDEX works identically |
| 6 | Upload a PDF → search for its text | Extracted content appears in results |
| 7 | User A searches → user B's data | User B's data does NOT appear (permission scoping) |
| 8 | Press Ctrl+K → type → suggestions → results → click | Full flow works, deep-links correctly |
| 9 | Files search uses FTS | Improved relevance ranking vs. old LIKE queries |
| 10 | Admin triggers full reindex | All module data re-indexed, counts match |
| 11 | Search "meeting" | Facet counts shown: Notes: 3, Calendar: 5, Chat: 12, etc. |
| 12 | Search result snippets | Matched terms highlighted with `<mark>` tags |

---

## Further Considerations

### Language-Aware Stemming

PostgreSQL supports language configs (`english`, `french`, etc.). SQL Server Full-Text also supports language settings.

**Recommendation:** Default to `english`, make configurable at instance level via admin settings. Defer per-user language selection to the internationalization phase.

### Index Storage Growth

With document content extraction, the search index can grow significantly.

**Recommendation:** Max **100KB** text per document (configurable). Full content remains in each module's own storage; search index stores truncated searchable text only.

### Real-Time Streaming Search (SignalR)

The global search bar could use SignalR for streaming results as the user types.

**Recommendation:** Defer. Start with debounced REST calls (300ms). Add SignalR streaming as a follow-up enhancement for smoother UX.

### Existing Per-Module Search Event Integration

Many modules already publish CRUD events (e.g., `FileUploadedEvent`, `MessageSentEvent`, `NoteCreatedEvent`). The Search module can subscribe directly to these existing events rather than requiring modules to also publish `SearchIndexRequestEvent`. This reduces the amount of module-side changes needed.

**Recommendation:** Subscribe to existing module events where available. Only require `SearchIndexRequestEvent` from modules that don't already publish CRUD events.
