# Files Module — REST API Reference

> **Base URL:** `/api/v1/files`  
> **Authentication:** All endpoints require authentication unless noted otherwise.  
> **Response Format:** Standard DotNetCloud envelope (see [Response Format](../../api/RESPONSE_FORMAT.md))

---

## Table of Contents

1. [File & Folder Operations](#file--folder-operations)
2. [Upload Endpoints](#upload-endpoints)
3. [Download Endpoints](#download-endpoints)
4. [Version Endpoints](#version-endpoints)
5. [Share Endpoints](#share-endpoints)
6. [Trash Endpoints](#trash-endpoints)
7. [Quota Endpoints](#quota-endpoints)
8. [Tag Endpoints](#tag-endpoints)
9. [Comment Endpoints](#comment-endpoints)
10. [Bulk Operation Endpoints](#bulk-operation-endpoints)
11. [Sync Endpoints](#sync-endpoints)
12. [WOPI Endpoints](#wopi-endpoints)
13. [Storage Metrics Endpoints](#storage-metrics-endpoints)
14. [Public Share Endpoints](#public-share-endpoints)
15. [Configuration Endpoint](#configuration-endpoint)
16. [Interactive API Explorer](#interactive-api-explorer)

---

## File & Folder Operations

### List Files/Folders

```
GET /api/v1/files?parentId={guid}&userId={guid}
```

Lists files and folders in a directory. Omit `parentId` to list root-level nodes.

**Response:**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Documents",
      "nodeType": "Folder",
      "mimeType": null,
      "size": 0,
      "parentId": null,
      "ownerId": "...",
      "currentVersion": 1,
      "isFavorite": false,
      "contentHash": null,
      "createdAt": "2026-03-01T12:00:00Z",
      "updatedAt": "2026-03-01T12:00:00Z",
      "childCount": 5,
      "tags": []
    }
  ]
}
```

### Get File/Folder

```
GET /api/v1/files/{nodeId}?userId={guid}
```

Returns a single file or folder by ID.

### Create Folder

```
POST /api/v1/files/folders?userId={guid}
```

**Request Body:**

```json
{
  "name": "New Folder",
  "parentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response:** `201 Created` with the new folder DTO.

### Rename File/Folder

```
PUT /api/v1/files/{nodeId}/rename?userId={guid}
```

**Request Body:**

```json
{
  "name": "Renamed File.docx"
}
```

### Move File/Folder

```
PUT /api/v1/files/{nodeId}/move?userId={guid}
```

**Request Body:**

```json
{
  "targetParentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Copy File/Folder

```
POST /api/v1/files/{nodeId}/copy?userId={guid}
```

**Request Body:**

```json
{
  "targetParentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response:** `201 Created` with the copied node DTO. Folders are deep-copied; file chunks are reused (reference count only).

### Delete File/Folder (Soft-Delete)

```
DELETE /api/v1/files/{nodeId}?userId={guid}
```

Moves the item to the trash bin. Use the [Trash endpoints](#trash-endpoints) to restore or permanently delete.

### Toggle Favorite

```
POST /api/v1/files/{nodeId}/favorite?userId={guid}
```

### List Favorites

```
GET /api/v1/files/favorites?userId={guid}
```

### List Recent Files

```
GET /api/v1/files/recent?userId={guid}&count=20
```

### Search Files

```
GET /api/v1/files/search?query={text}&userId={guid}&page=1&pageSize=20
```

**Response:** Paginated results with `pagination` metadata.

---

## Upload Endpoints

DotNetCloud uses a chunked upload protocol with SHA-256 deduplication.

### Initiate Upload

```
POST /api/v1/files/upload/initiate?userId={guid}
```

**Request Body:**

```json
{
  "fileName": "report.pdf",
  "parentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalSize": 10485760,
  "mimeType": "application/pdf",
  "chunkHashes": [
    "a3f2b8c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1",
    "b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5"
  ]
}
```

**Response:** `201 Created`

```json
{
  "success": true,
  "data": {
    "sessionId": "...",
    "existingChunks": ["a3f2b8c1..."],
    "missingChunks": ["b4c5d6e7..."],
    "expiresAt": "2026-03-02T12:00:00Z"
  }
}
```

Only chunks listed in `missingChunks` need to be uploaded. Chunks in `existingChunks` are already stored on the server (deduplication).

### Upload Chunk

```
PUT /api/v1/files/upload/{sessionId}/chunks/{chunkHash}?userId={guid}
```

**Request Body:** Raw binary chunk data in the request body.

The server verifies the SHA-256 hash of the received data matches `{chunkHash}`.

### Complete Upload

```
POST /api/v1/files/upload/{sessionId}/complete?userId={guid}
```

Assembles the file from chunks, creates a file version, updates the node, and publishes `FileUploadedEvent`.

### Cancel Upload

```
DELETE /api/v1/files/upload/{sessionId}?userId={guid}
```

### Get Upload Session Status

```
GET /api/v1/files/upload/{sessionId}?userId={guid}
```

---

## Download Endpoints

### Download File

```
GET /api/v1/files/{nodeId}/download?userId={guid}
```

Returns the file content as a stream. Supports HTTP range requests for partial downloads via `Range` header.

### Download Specific Version

```
GET /api/v1/files/{nodeId}/download?userId={guid}&version={versionNumber}
```

### Get Chunk Manifest

```
GET /api/v1/files/{nodeId}/chunks?userId={guid}
```

Returns the ordered list of chunk hashes for sync clients to perform delta downloads.

### Download Chunk by Hash

```
GET /api/v1/files/chunks/{chunkHash}?userId={guid}
```

Downloads a single raw chunk by its SHA-256 hash. Used by sync clients for efficient chunk-level delta sync.

---

## Version Endpoints

### List Versions

```
GET /api/v1/files/{nodeId}/versions?userId={guid}
```

Returns all versions of a file, newest first.

### Get Specific Version

```
GET /api/v1/files/{nodeId}/versions/{versionNumber}?userId={guid}
```

### Restore Version

```
POST /api/v1/files/{nodeId}/versions/{versionNumber}/restore?userId={guid}
```

Creates a new version with the content of the specified version (non-destructive restore).

### Delete Version

```
DELETE /api/v1/files/{nodeId}/versions/{versionNumber}?userId={guid}
```

Decrements chunk reference counts. Labeled versions are protected from auto-deletion.

### Label Version

```
PUT /api/v1/files/{nodeId}/versions/{versionNumber}/label?userId={guid}
```

**Request Body:**

```json
{
  "label": "Final draft"
}
```

---

## Share Endpoints

### Create Share

```
POST /api/v1/files/{nodeId}/shares?userId={guid}
```

**Request Body:**

```json
{
  "shareType": "User",
  "sharedWithUserId": "...",
  "permission": "ReadWrite",
  "expiresAt": "2026-06-01T00:00:00Z",
  "note": "Please review this document"
}
```

**Share types:** `User`, `Team`, `Group`, `PublicLink`  
**Permission levels:** `Read`, `ReadWrite`, `Full`

### List Shares

```
GET /api/v1/files/{nodeId}/shares?userId={guid}
```

### Update Share

```
PUT /api/v1/files/{nodeId}/shares/{shareId}?userId={guid}
```

### Delete Share

```
DELETE /api/v1/files/{nodeId}/shares/{shareId}?userId={guid}
```

### Shared With Me

```
GET /api/v1/files/shared-with-me?userId={guid}
```

### Public Link Access

```
GET /api/v1/files/public/{linkToken}?password={optional}
```

**No authentication required.** Validates the link token, checks expiration, download limits, and optional password.

---

## Trash Endpoints

### List Trash

```
GET /api/v1/files/trash?userId={guid}
```

### Get Trash Size

```
GET /api/v1/files/trash/size?userId={guid}
```

### Restore from Trash

```
POST /api/v1/files/trash/{nodeId}/restore?userId={guid}
```

Restores to the original parent folder. If the parent was deleted, restores to root.

### Permanently Delete

```
DELETE /api/v1/files/trash/{nodeId}?userId={guid}
```

Permanently deletes the item, its versions, chunks (if unreferenced), shares, tags, and comments.

### Empty Trash

```
DELETE /api/v1/files/trash?userId={guid}
```

---

## Quota Endpoints

### Get Current User Quota

```
GET /api/v1/files/quota?userId={guid}
```

**Response:**

```json
{
  "success": true,
  "data": {
    "userId": "...",
    "maxBytes": 10737418240,
    "usedBytes": 5368709120,
    "remainingBytes": 5368709120,
    "usagePercent": 50.0
  }
}
```

### Get User Quota (Admin)

```
GET /api/v1/files/quota/{userId}
```

### Set User Quota (Admin)

```
PUT /api/v1/files/quota/{userId}
```

**Request Body:**

```json
{
  "maxBytes": 21474836480
}
```

### Force Quota Recalculation (Admin)

```
POST /api/v1/files/quota/{userId}/recalculate
```

---

## Tag Endpoints

### Add Tag

```
POST /api/v1/files/{nodeId}/tags?userId={guid}
```

**Request Body:**

```json
{
  "name": "Important",
  "color": "#FF0000"
}
```

### Remove Tag

```
DELETE /api/v1/files/{nodeId}/tags/{tagName}?userId={guid}
```

### List User Tags

```
GET /api/v1/files/tags?userId={guid}
```

### List Files by Tag

```
GET /api/v1/files/tags/{tagName}?userId={guid}
```

---

## Comment Endpoints

### Add Comment

```
POST /api/v1/files/{nodeId}/comments?userId={guid}
```

**Request Body:**

```json
{
  "content": "Great work on this document!",
  "parentCommentId": null
}
```

Set `parentCommentId` for threaded replies.

### List Comments

```
GET /api/v1/files/{nodeId}/comments?userId={guid}
```

### Edit Comment

```
PUT /api/v1/files/comments/{commentId}?userId={guid}
```

### Delete Comment

```
DELETE /api/v1/files/comments/{commentId}?userId={guid}
```

---

## Bulk Operation Endpoints

### Bulk Move

```
POST /api/v1/files/bulk/move?userId={guid}
```

**Request Body:**

```json
{
  "nodeIds": ["...", "..."],
  "targetParentId": "..."
}
```

**Response:** Per-node success/failure results.

### Bulk Copy

```
POST /api/v1/files/bulk/copy?userId={guid}
```

### Bulk Delete (to Trash)

```
POST /api/v1/files/bulk/delete?userId={guid}
```

### Bulk Permanent Delete

```
POST /api/v1/files/bulk/permanent-delete?userId={guid}
```

---

## Sync Endpoints

### Get Changes Since

```
GET /api/v1/files/sync/changes?since={timestamp}&folderId={guid}&userId={guid}
```

Returns all file/folder changes since the given timestamp. Used by sync clients for incremental sync.

### Get Folder Tree

```
GET /api/v1/files/sync/tree?folderId={guid}&userId={guid}
```

Returns a full folder tree snapshot with content hashes for reconciliation.

### Reconcile

```
POST /api/v1/files/sync/reconcile?userId={guid}
```

**Request Body:**

```json
{
  "folderId": null,
  "clientNodes": [
    {
      "nodeId": "...",
      "name": "report.docx",
      "contentHash": "...",
      "lastModified": "2026-03-01T12:00:00Z"
    }
  ]
}
```

Returns actions the client should take (upload, download, delete, conflict).

---

## WOPI Endpoints

WOPI endpoints enable Collabora Online to fetch and save documents for browser-based editing.

### CheckFileInfo

```
GET /api/v1/wopi/files/{fileId}?access_token={token}
```

Returns file metadata for Collabora. Authenticated via WOPI access token.

### GetFile

```
GET /api/v1/wopi/files/{fileId}/contents?access_token={token}
```

Returns the file content stream for Collabora to load into the editor.

### PutFile

```
POST /api/v1/wopi/files/{fileId}/contents?access_token={token}
```

Saves edited content from Collabora. Creates a new file version.

### Generate Token

```
POST /api/v1/wopi/token/{fileId}?userId={guid}
```

Generates a time-limited, signed WOPI access token for the specified file and user.

### End Session

```
DELETE /api/v1/wopi/token/{fileId}?userId={guid}
```

### Check Collabora Availability

```
GET /api/v1/wopi/discovery
```

### Check Extension Support

```
GET /api/v1/wopi/discovery/supports/{extension}
```

---

## Storage Metrics Endpoints

### Get Storage Metrics

```
GET /api/v1/files/storage/metrics?userId={guid}
```

Returns deduplication savings, total storage used, and chunk statistics.

---

## Public Share Endpoints

### Access Public Share

```
GET /api/v1/files/public-share/{linkToken}?password={optional}
```

**No authentication required.** Returns the shared file/folder metadata if the link is valid.

### Download from Public Share

```
GET /api/v1/files/public-share/{linkToken}/download?password={optional}
```

**No authentication required.** Returns the file content stream.

---

## Error Responses

All endpoints return errors in the standard envelope format:

```json
{
  "success": false,
  "error": {
    "code": "FILES_QUOTA_EXCEEDED",
    "message": "Storage quota exceeded. Used: 10.0 GB / 10.0 GB."
  }
}
```

### Common Error Codes

| Code | HTTP Status | Description |
|---|---|---|
| `not_found` | 404 | File, folder, or resource not found |
| `FILES_QUOTA_EXCEEDED` | 400 | User's storage quota would be exceeded |
| `FILES_NAME_CONFLICT` | 400 | A file/folder with the same name already exists in the target |
| `FILES_PERMISSION_DENIED` | 403 | Caller lacks permission for the operation |
| `FILES_SHARE_EXPIRED` | 410 | Public link has expired |
| `FILES_SHARE_DOWNLOAD_LIMIT` | 410 | Public link download limit reached |
| `FILES_SHARE_PASSWORD_REQUIRED` | 401 | Public link requires a password |
| `FILES_UPLOAD_SESSION_EXPIRED` | 410 | Upload session has expired |
| `FILES_CHUNK_HASH_MISMATCH` | 400 | Uploaded chunk hash does not match expected hash |

---

## Configuration Endpoint

### Get Upload Configuration

```
GET /api/v1/files/config
```

**No authentication required.** Returns the current upload configuration for client-side validation.

**Response:**

```json
{
  "maxUploadSizeBytes": 16106127360
}
```

| Field | Type | Description |
|---|---|---|
| `maxUploadSizeBytes` | `long` | Maximum allowed file size in bytes (default: 15 GB) |

The web UI calls this endpoint on page load to enforce size validation before uploading.

---

## Interactive API Explorer

When the Files module host is running in development mode, an interactive API explorer is available at:

```
https://localhost:{files-port}/scalar/v1
```

The explorer is powered by [Scalar](https://github.com/scalar/scalar) and auto-generated from controller XML documentation. It provides a try-it-out interface for all 80+ Files API endpoints.

The raw OpenAPI specification is available at `/openapi/v1.json`.

> **Note:** The API explorer is only available when `ASPNETCORE_ENVIRONMENT=Development`.
