# Search API Reference

> **Base URL:** `/api/v1/search`
> **Authentication:** Bearer token (OpenIddict)
> **Response Format:** Standard envelope (see [RESPONSE_FORMAT.md](RESPONSE_FORMAT.md))

---

## REST Endpoints

### Search

Execute a full-text search query across all modules.

```
GET /api/v1/search?q={query}&module={moduleId}&type={entityType}&page={n}&pageSize={n}&sort={order}
```

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `q` | string | — | Yes | Search query text. Supports advanced syntax (see below). |
| `module` | string | — | No | Filter results to a single module (e.g., `files`, `notes`, `chat`) |
| `type` | string | — | No | Filter results by entity type (e.g., `FileNode`, `Note`, `Message`) |
| `page` | int | 1 | No | Page number (1-based) |
| `pageSize` | int | 20 | No | Results per page |
| `sort` | string | `Relevance` | No | Sort order: `Relevance`, `DateDesc`, `DateAsc` |

**Response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "moduleId": "notes",
        "entityId": "a1b2c3d4-...",
        "entityType": "Note",
        "title": "Quarterly <mark>Report</mark>",
        "snippet": "...the <mark>quarterly</mark> <mark>report</mark> shows growth in...",
        "relevanceScore": 0.95,
        "updatedAt": "2026-01-15T10:30:00Z",
        "metadata": {
          "format": "Markdown",
          "tags": "finance,reports"
        }
      }
    ],
    "totalCount": 42,
    "page": 1,
    "pageSize": 20,
    "facetCounts": {
      "notes": 25,
      "files": 12,
      "chat": 5
    }
  }
}
```

**Errors:**

| Status | Code | Condition |
|---|---|---|
| `400` | — | Missing `q` parameter |

---

### Suggest (Autocomplete)

Get search suggestions for typeahead/autocomplete. Returns the top 10 results.

```
GET /api/v1/search/suggest?q={query}
```

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `q` | string | — | Yes | Partial search query (minimum 2 characters) |

**Response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "moduleId": "files",
        "entityId": "...",
        "entityType": "FileNode",
        "title": "quarterly-report-2026.pdf",
        "snippet": "...quarterly financial report...",
        "relevanceScore": 0.92,
        "updatedAt": "2026-01-10T08:00:00Z",
        "metadata": {}
      }
    ],
    "totalCount": 10,
    "page": 1,
    "pageSize": 10,
    "facetCounts": {}
  }
}
```

**Notes:**
- Returns empty results for queries shorter than 2 characters
- Used by the Global Search Bar for live suggestions

---

### Get Index Stats (Admin)

Get index health and statistics. Requires admin role.

```
GET /api/v1/search/stats
```

**Response:**

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

**Errors:**

| Status | Code | Condition |
|---|---|---|
| `403` | — | Caller is not an admin |

---

### Trigger Full Reindex (Admin)

Trigger a full reindex of all searchable modules. Requires admin role.

```
POST /api/v1/search/admin/reindex
```

**Response:**

```json
{
  "success": true,
  "data": {
    "message": "Full reindex triggered"
  }
}
```

**Errors:**

| Status | Code | Condition |
|---|---|---|
| `403` | — | Caller is not an admin |

**Notes:**
- Reindex runs in the background. Monitor progress via stats endpoint.
- Existing index remains searchable during reindex.

---

### Trigger Module Reindex (Admin)

Trigger a reindex for a specific module. Requires admin role.

```
POST /api/v1/search/admin/reindex/{moduleId}
```

| Parameter | Type | Description |
|---|---|---|
| `moduleId` | string | Module to reindex (e.g., `files`, `notes`, `chat`) |

**Response:**

```json
{
  "success": true,
  "data": {
    "message": "Reindex triggered for module: files"
  }
}
```

**Errors:**

| Status | Code | Condition |
|---|---|---|
| `403` | — | Caller is not an admin |

---

## Advanced Query Syntax

The `q` parameter supports advanced syntax parsed by `SearchQueryParser`:

| Syntax | Example | Description |
|---|---|---|
| Keywords | `quarterly report` | Matches documents containing both terms |
| Quoted phrase | `"quarterly report"` | Matches the exact phrase |
| Module filter | `in:notes` | Restricts results to a specific module |
| Type filter | `type:pdf` | Restricts results to a specific entity type |
| Exclusion | `-draft` | Excludes documents containing the term |
| Combined | `"project plan" in:files -template` | Phrase search in Files module, excluding "template" |

**Query filter precedence:**
- `in:module` in the query text overrides the `module` URL parameter
- `type:value` in the query text overrides the `type` URL parameter

---

## gRPC Service

**Proto file:** `search_service.proto`
**Service name:** `SearchService`

### RPCs

| RPC | Request | Response | Description |
|---|---|---|---|
| `Search` | `SearchRequest` | `SearchResponse` | Execute a search query |
| `IndexDocument` | `IndexDocumentRequest` | `IndexDocumentResponse` | Add or update a document in the index |
| `RemoveDocument` | `RemoveDocumentRequest` | `RemoveDocumentResponse` | Remove a document from the index |
| `ReindexModule` | `ReindexModuleRequest` | `ReindexModuleResponse` | Trigger a full reindex for a module |
| `GetIndexStats` | `GetIndexStatsRequest` | `IndexStatsResponse` | Get index health statistics |

### SearchRequest

```protobuf
message SearchRequest {
  string query_text = 1;
  string module_filter = 2;
  string entity_type_filter = 3;
  string user_id = 4;
  int32 page = 5;
  int32 page_size = 6;
  string sort_order = 7;
}
```

### SearchResponse

```protobuf
message SearchResponse {
  bool success = 1;
  string error_message = 2;
  repeated SearchResultItemMessage items = 3;
  int32 total_count = 4;
  int32 page = 5;
  int32 page_size = 6;
  map<string, int32> facet_counts = 7;
}

message SearchResultItemMessage {
  string module_id = 1;
  string entity_id = 2;
  string entity_type = 3;
  string title = 4;
  string snippet = 5;
  double relevance_score = 6;
  string updated_at = 7;
  map<string, string> metadata = 8;
}
```

### IndexDocumentRequest

```protobuf
message IndexDocumentRequest {
  string module_id = 1;
  string entity_id = 2;
  string entity_type = 3;
  string title = 4;
  string content = 5;
  string summary = 6;
  string owner_id = 7;
  string organization_id = 8;
  map<string, string> metadata = 9;
}
```

### RemoveDocumentRequest

```protobuf
message RemoveDocumentRequest {
  string module_id = 1;
  string entity_id = 2;
}
```

### ReindexModuleRequest

```protobuf
message ReindexModuleRequest {
  string module_id = 1;
}
```

---

## Client Library

The `DotNetCloud.Modules.Search.Client` package provides a gRPC client for modules that want to use search:

```csharp
// Register in DI
services.AddSearchFtsClient(configuration);

// Inject and use
public class MyService(ISearchFtsClient searchClient)
{
    public async Task<SearchResultDto?> SearchAsync(string query, Guid userId)
    {
        if (!searchClient.IsAvailable) return null;

        return await searchClient.SearchAsync(
            queryText: query,
            moduleFilter: null,
            entityTypeFilter: null,
            userId: userId,
            page: 1,
            pageSize: 20,
            sortOrder: SearchSortOrder.Relevance,
            cancellationToken: CancellationToken.None);
    }
}
```

**Configuration:**

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
| `SearchModuleAddress` | `string?` | `null` | gRPC address (`http://`, `https://`, `unix://`). Null = search unavailable. |
| `Timeout` | `TimeSpan` | `00:00:10` | gRPC call deadline |

**Graceful Degradation:**

When `SearchModuleAddress` is null or the search module is unreachable, `ISearchFtsClient.IsAvailable` returns `false` and `SearchAsync()` returns `null`. Module controllers (Files, Chat, Notes) automatically fall back to LIKE-based queries.

---

## Permission Model

All search results are **scoped by the authenticated user's identity**:

- REST endpoints extract `OwnerId` from the authenticated user's claims
- gRPC requests include `user_id` which must match the authenticated claim
- The search provider applies `WHERE OwnerId = @userId` to all queries
- Admin endpoints (`/stats`, `/admin/reindex`) require the `admin` role
- `SearchIndexStats` is not scoped — admins see global counts

---

## Related Documentation

| Document | Path |
|---|---|
| Module Documentation | [docs/modules/SEARCH.md](../modules/SEARCH.md) |
| Response Format Standard | [RESPONSE_FORMAT.md](RESPONSE_FORMAT.md) |
| Authentication | [AUTHENTICATION.md](AUTHENTICATION.md) |
| Architecture | [docs/architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md) |
