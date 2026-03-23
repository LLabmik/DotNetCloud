# Notes API Reference

> **Base URL:** `/api/v1/notes`  
> **Authentication:** Bearer token (OpenIddict)  
> **Response Format:** Standard envelope (see [RESPONSE_FORMAT.md](RESPONSE_FORMAT.md))

---

## REST Endpoints — Notes

### List Notes

```
GET /api/v1/notes?folderId={id}&skip={n}&take={n}
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `folderId` | GUID | — | Filter by folder (omit for all notes) |
| `skip` | int | 0 | Pagination offset |
| `take` | int | 50 | Page size |

**Response:** Paginated array of `NoteDto`

---

### Get Note

```
GET /api/v1/notes/{noteId}
```

**Response:** `NoteDto`

**Errors:** `404` NOTE_NOT_FOUND

---

### Create Note

```
POST /api/v1/notes
```

**Request Body:** `CreateNoteDto`

```json
{
  "folderId": "...",
  "title": "Project Ideas",
  "content": "# Ideas\n\n- Build a notification system\n- Add dark mode support",
  "format": "Markdown",
  "tags": ["project", "ideas"],
  "links": [
    {
      "linkType": "Contact",
      "targetId": "...",
      "displayLabel": "Project Lead"
    }
  ]
}
```

**Required Fields:** `title`

**Format Values:** `Markdown` (default), `PlainText`

**Response:** `201` with created `NoteDto`

---

### Update Note

```
PUT /api/v1/notes/{noteId}
```

**Request Body:** `UpdateNoteDto` — all fields optional (patch semantics).

```json
{
  "title": "Updated Title",
  "content": "# Updated Content\n\nNew ideas added.",
  "isPinned": true,
  "tags": ["project", "ideas", "priority"],
  "expectedVersion": 3
}
```

**Optimistic Concurrency:** Set `expectedVersion` to the note's current version number. If the note has been modified since (version mismatch), the server returns `409` NOTE_VERSION_CONFLICT. Set to `0` to skip version checking.

**Response:** Updated `NoteDto`

**Errors:** `404` NOTE_NOT_FOUND, `409` NOTE_VERSION_CONFLICT, `403` insufficient permissions

---

### Delete Note

```
DELETE /api/v1/notes/{noteId}
```

Soft-deletes the note. Only the owner can delete.

**Response:** `204` No Content

---

### Search Notes

```
GET /api/v1/notes/search?q={query}&skip={n}&take={n}
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `q` | string | — | Search query (title, content, tags) |
| `skip` | int | 0 | Pagination offset |
| `take` | int | 50 | Page size |

**Response:** Paginated array of `NoteDto` ranked by relevance

---

## Markdown Rendering

### Preview Note

```
GET /api/v1/notes/{noteId}/preview
```

Renders a saved note's Markdown content to sanitized HTML.

**Response:**

```json
{
  "data": {
    "noteId": "...",
    "title": "Project Ideas",
    "renderedHtml": "<h1>Ideas</h1>\n<ul>\n<li>Build a notification system</li>\n</ul>",
    "format": "Markdown",
    "version": 5
  }
}
```

### Render Markdown

```
POST /api/v1/notes/render
```

Renders raw Markdown to sanitized HTML without saving. Useful for live preview in editors.

**Request Body:**

```json
{
  "content": "# Hello\n\n**Bold text** and [a link](https://example.com)",
  "format": "Markdown"
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `content` | string | — | Raw content to render (required) |
| `format` | string | `Markdown` | Content format (`Markdown` or `PlainText`) |

**Response:**

```json
{
  "data": {
    "html": "<h1>Hello</h1>\n<p><strong>Bold text</strong> and <a href=\"https://example.com\">a link</a></p>"
  }
}
```

**Security:** All rendered HTML is sanitized server-side using HtmlSanitizer. Script tags, event handlers, `javascript:` URLs, iframes, forms, and other XSS vectors are stripped. Only safe HTML elements and attributes are preserved.

---

## Version History

### Get Version History

```
GET /api/v1/notes/{noteId}/versions
```

**Response:** Array of `NoteVersionDto` ordered by version number (descending)

```json
[
  {
    "id": "...",
    "noteId": "...",
    "versionNumber": 5,
    "title": "Project Ideas",
    "content": "# Ideas\n\n...",
    "editedByUserId": "...",
    "createdAt": "2026-03-20T14:30:00Z"
  },
  {
    "id": "...",
    "noteId": "...",
    "versionNumber": 4,
    "title": "Project Ideas",
    "content": "# Ideas\n\n(previous content)",
    "editedByUserId": "...",
    "createdAt": "2026-03-18T10:00:00Z"
  }
]
```

---

### Restore Version

```
POST /api/v1/notes/{noteId}/versions/{versionId}/restore
```

Restores the note to the specified version. This creates a **new version** (non-destructive — the current state is preserved in history).

**Response:** Updated `NoteDto` with incremented version number

---

## Folders

### List Folders

```
GET /api/v1/notes/folders?parentId={id}
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `parentId` | GUID | — | Parent folder ID (omit for root folders) |

**Response:** Array of `NoteFolderDto`

---

### Get Folder

```
GET /api/v1/notes/folders/{folderId}
```

**Response:** `NoteFolderDto`

```json
{
  "id": "...",
  "ownerId": "...",
  "parentId": null,
  "name": "Work Notes",
  "color": "#4285F4",
  "sortOrder": 0,
  "noteCount": 15,
  "createdAt": "2026-01-15T10:00:00Z",
  "updatedAt": "2026-03-01T14:30:00Z"
}
```

---

### Create Folder

```
POST /api/v1/notes/folders
```

**Request Body:** `CreateNoteFolderDto`

```json
{
  "parentId": null,
  "name": "Work Notes",
  "color": "#4285F4"
}
```

**Required Fields:** `name`

**Response:** `201` with created `NoteFolderDto`

---

### Update Folder

```
PUT /api/v1/notes/folders/{folderId}
```

**Request Body:** `UpdateNoteFolderDto` — all fields optional.

```json
{
  "name": "Renamed Folder",
  "color": "#0F9D58",
  "parentId": "..."
}
```

**Response:** Updated `NoteFolderDto`

---

### Delete Folder

```
DELETE /api/v1/notes/folders/{folderId}
```

Deletes the folder. Notes in the folder become unfiled (their `folderId` is set to null). Notes are **not** deleted.

**Response:** `204` No Content

---

## Sharing

### List Shares

```
GET /api/v1/notes/{noteId}/shares
```

**Response:** Array of `NoteShare`

---

### Share Note

```
POST /api/v1/notes/{noteId}/shares
```

**Request Body:**

```json
{
  "userId": "...",
  "permission": "ReadWrite"
}
```

Notes can only be shared with individual users (team sharing is not supported).

**Permissions:** `ReadOnly`, `ReadWrite`

**Response:** `201` with created share

---

### Remove Share

```
DELETE /api/v1/notes/shares/{shareId}
```

**Response:** `204` No Content

---

## NoteDto Schema

```json
{
  "id": "...",
  "ownerId": "...",
  "folderId": "...",
  "title": "Project Ideas",
  "content": "# Ideas\n\n- Build a notification system\n- Add dark mode support",
  "format": "Markdown",
  "isPinned": false,
  "isFavorite": true,
  "isDeleted": false,
  "createdAt": "2026-01-15T10:00:00Z",
  "updatedAt": "2026-03-20T14:30:00Z",
  "version": 5,
  "etag": "\"v5hash\"",
  "tags": ["project", "ideas"],
  "links": [
    {
      "linkType": "Contact",
      "targetId": "...",
      "displayLabel": "Project Lead"
    }
  ],
  "contentLength": 85
}
```

---

## Enums

### Note Format

| Value | Description |
|---|---|
| `Markdown` | Markdown formatted text (default) |
| `PlainText` | Unformatted plain text |

### Link Type

| Value | Description |
|---|---|
| `File` | Links to a file in the Files module |
| `CalendarEvent` | Links to a calendar event |
| `Contact` | Links to a contact |
| `Note` | Links to another note |

### Share Permission

| Value | Description |
|---|---|
| `ReadOnly` | Can view the note |
| `ReadWrite` | Can view and edit the note |

---

## Error Codes

| Code | HTTP | Description |
|---|---|---|
| `NOTE_NOT_FOUND` | 404 | Note does not exist or is not accessible |
| `NOTE_VERSION_CONFLICT` | 409 | Note was modified by another user since last read |
| `NOTE_FOLDER_NOT_FOUND` | 404 | Folder does not exist |
| `NOTE_SHARE_NOT_FOUND` | 404 | Share does not exist |
