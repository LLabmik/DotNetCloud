# Files Module — WOPI & Collabora Integration

> **Last Updated:** 2026-03-03

---

## Overview

DotNetCloud integrates with [Collabora Online](https://www.collaboraonline.com/) (based on LibreOffice) for browser-based document editing. The integration uses the [WOPI protocol](https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/online/) (Web Application Open Platform Interface) to allow Collabora to fetch and save documents.

---

## Architecture

```
Browser                  DotNetCloud Server              Collabora Online
  |                            |                              |
  |  Click "Edit" on file      |                              |
  |--------------------------->|                              |
  |                            |  Generate WOPI token          |
  |  <iframe src=collabora     |                              |
  |    with token + file ID>   |                              |
  |--------------------------->|------------------------------>|
  |                            |                              |
  |                            |  GET /wopi/files/{id}         |
  |                            |  (CheckFileInfo)              |
  |                            |<-----------------------------|
  |                            |  Return file metadata         |
  |                            |------------------------------>|
  |                            |                              |
  |                            |  GET /wopi/files/{id}/contents|
  |                            |  (GetFile)                    |
  |                            |<-----------------------------|
  |                            |  Return file bytes            |
  |                            |------------------------------>|
  |                            |                              |
  |  User edits document in    |                              |
  |  Collabora iframe          |                              |
  |                            |                              |
  |                            |  POST /wopi/files/{id}/contents
  |                            |  (PutFile — auto-save)       |
  |                            |<-----------------------------|
  |                            |  Create new version           |
  |                            |------------------------------>|
```

---

## WOPI Endpoints

All WOPI endpoints are authenticated via `access_token` query parameter (not standard HTTP auth).

### CheckFileInfo

```
GET /api/v1/wopi/files/{fileId}?access_token={token}
```

Returns file metadata that Collabora needs:

- File name, size, owner
- User permissions (can edit, can rename)
- Version tracking information
- User identity for co-editing attribution — `UserId` is the user's GUID and `UserFriendlyName` is their display name, resolved through the `IUserDirectory` capability (gRPC) and truncated to the WOPI limit of 128 characters; when the directory is unreachable or the user is unknown the GUID is reported instead, so a lookup can never fail opening a document

Also begins a session slot for concurrent session tracking.

#### CheckFileInfo request contract

`{fileId}` in the route is constrained to a **GUID**. A request whose path segment is not a GUID
(for example the access token accidentally placed in the path) does **not** match the route and returns an
empty-body `404` from the ASP.NET pipeline — no WOPI code runs, so no `WopiController` log line appears.
Distinguish the outcomes:

| Situation | Status |
| --- | --- |
| Path segment is not a GUID (route not matched) | `404`, empty body, no application code |
| Missing/invalid/expired `access_token` | `401` |
| Token valid but the node or the caller's permission cannot be resolved | `404` |
| `X-WOPI-Proof` present and the signature does not verify | `401` |
| Concurrent-session cap reached | `503` (slot not consumed) |
| OK | `200` |

Proof-key validation is **skipped** when a request carries no `X-WOPI-Proof`/`X-WOPI-TimeStamp` headers
(the documented "allows non-Collabora clients" path), so a plain `curl` with a valid `access_token`
against a correctly-formed URL returns `200`.

### GetFile

```
GET /api/v1/wopi/files/{fileId}/contents?access_token={token}
```

Returns the file content as a byte stream. Reads from the storage engine via the download service.

> **`Size` vs. streamed bytes.** `CheckFileInfo.Size` is `FileNode.Size`; `GetFile` streams the **latest
> `FileVersion`**. Both are written in the same commit, so they always agree *at a given point in time*.
> They can legitimately differ when Collabora saves (`PutFile`) **between** the two calls: the metadata
> was read against the previous version while the content stream already reflects the new one (a one-byte
> difference such as `5545` vs `5546` is exactly this race, not an off-by-one). Hosts should compare
> `Version`/`LastModifiedTime` rather than assume `Size` is stable across calls.

### PutFile

```
POST /api/v1/wopi/files/{fileId}/contents?access_token={token}
```

Saves the edited document content using the chunked upload pipeline (deduplication + version history).

A new `FileVersion` is created **only when the content actually changed**: a byte-identical save (e.g. a
Collabora autosave or close-flush with no edits) skips the version, leaves the version counter, size and
content hash untouched, publishes no change events, and only refreshes `LastModifiedTime`. See
`docs/modules/VERSIONING.md` → *Saves that skip versioning*.

---

## Token Management

### Token Generation

```
POST /api/v1/wopi/token/{fileId}?userId={guid}
```

Generates a WOPI access token that is:

- **Scoped** to a specific file and user
- **Signed** with HMAC-SHA256 using the configured signing key
- **Time-limited** (default: 8 hours, configurable via `TokenLifetimeMinutes`)

### Token Validation

On every WOPI request, the token is validated:

1. Signature verification (HMAC-SHA256)
2. Expiration check
3. File ID match (token is bound to a specific file)
4. User extraction (CallerContext is built from the token)

### End Session

```
DELETE /api/v1/wopi/token/{fileId}?userId={guid}
```

Releases the session slot in the concurrent session tracker.

---

## Collabora Discovery

### Check Availability

```
GET /api/v1/wopi/discovery
```

Queries the Collabora WOPI discovery endpoint to verify connectivity and retrieve supported file types.

### Check Extension Support

```
GET /api/v1/wopi/discovery/supports/{extension}
```

Returns whether Collabora supports editing files with the given extension (e.g., `docx`, `xlsx`, `pptx`).

---

## Proof Key Validation

When `EnableProofKeyValidation` is `true` (default in production), incoming WOPI requests from Collabora are validated using proof keys:

1. Collabora signs requests with its private key
2. DotNetCloud validates the signature using Collabora's public key (from the discovery endpoint)
3. This prevents forged WOPI requests

---

## Concurrent Session Tracking

The `IWopiSessionTracker` limits how many documents can be edited simultaneously:

- Default limit: 20 concurrent sessions (configurable via `MaxConcurrentSessions`, `0` = unlimited)
- **The cap is server-wide and keyed on the `(fileId, userId)` pair** — it is a Collabora *resource
  guard*, not a per-document lock. One user holds at most one slot per file, but a second user opening
  the same file consumes a second slot, so a busy instance can refuse the 21st *distinct* session even
  when every document has a single editor.
- When the limit is reached, `CheckFileInfo` returns `503 Service Unavailable` (the slot is **not**
  consumed).
- Sessions expire after `TokenLifetimeMinutes + 30` of inactivity (8 h 30 m by default); a module-host
  restart clears the in-memory dictionary.
- Sessions are released when editing ends (via `DELETE /api/v1/wopi/token/{fileId}`).

> **Load-testing the cap:** because the key includes the file, reaching the cap requires ~
> `MaxConcurrentSessions` distinct `(file, user)` sessions — a single editor holds at most one slot per file.

---

## Configuration

```json
{
  "Files": {
    "Collabora": {
      "Enabled": true,
      "ServerUrl": "https://collabora.example.com",
      "WopiBaseUrl": "https://cloud.example.com",
      "TokenSigningKey": "your-secret-key-at-least-32-characters",
      "TokenLifetimeMinutes": 480,
      "AutoSaveIntervalSeconds": 300,
      "MaxConcurrentSessions": 20,
      "EnableProofKeyValidation": true,
      "SupportedMimeTypes": [],
      "UseBuiltInCollabora": false,
      "CollaboraInstallDirectory": "",
      "CollaboraExecutablePath": "",
      "CollaboraMaxRestartAttempts": 5,
      "CollaboraRestartBackoffSeconds": 5
    }
  }
}
```

### Configuration Reference

| Setting                          | Default | Description                                                             |
| -------------------------------- | ------- | ----------------------------------------------------------------------- |
| `Enabled`                        | `false` | Enable Collabora integration                                            |
| `ServerUrl`                      | `""`    | URL of external Collabora server                                        |
| `WopiBaseUrl`                    | `""`    | Public URL of this DotNetCloud instance (for WOPI callbacks)            |
| `TokenSigningKey`                | `""`    | HMAC-SHA256 key for signing tokens (≥32 chars, auto-generated if empty) |
| `TokenLifetimeMinutes`           | `480`   | Token validity period (8 hours)                                         |
| `AutoSaveIntervalSeconds`        | `300`   | Collabora auto-save interval (5 minutes)                                |
| `MaxConcurrentSessions`          | `20`    | Max simultaneous editing sessions (0 = unlimited)                       |
| `EnableProofKeyValidation`       | `true`  | Validate Collabora proof key signatures                                 |
| `SupportedMimeTypes`             | `[]`    | Filter allowed MIME types (empty = all from discovery)                  |
| `UseBuiltInCollabora`            | `false` | Use DotNetCloud-managed Collabora CODE process                          |
| `CollaboraInstallDirectory`      | `""`    | Path to Collabora CODE installation                                     |
| `CollaboraExecutablePath`        | `""`    | Path to `coolwsd` executable                                            |
| `CollaboraMaxRestartAttempts`    | `5`     | Max restart attempts before giving up                                   |
| `CollaboraRestartBackoffSeconds` | `5`     | Base delay for exponential restart backoff                              |

---

## Built-In Collabora CODE

DotNetCloud can manage a local Collabora CODE instance:

1. Set `UseBuiltInCollabora: true`
2. Set `CollaboraInstallDirectory` to the CODE installation path
3. The `CollaboraProcessManager` (BackgroundService) will:
   - Start the `coolwsd` process
   - Monitor health via HTTP health checks
   - Restart on crash with exponential backoff
   - Shut down gracefully when DotNetCloud stops

### Installation

Collabora CODE can be installed via:

- `dotnetcloud setup` wizard (interactive, downloads and configures CODE)
- `dotnetcloud install collabora` command (standalone installation)

---

## Reverse Proxy Configuration

When using a reverse proxy (nginx, Apache, IIS), WOPI requires specific configuration:

- WebSocket support must be enabled for Collabora
- The WOPI base URL must be accessible from the Collabora server
- TLS is required in production

Pre-configured templates are generated by DotNetCloud:

- `ReverseProxyTemplates.GenerateNginxConfigWithCollabora()`
- `ReverseProxyTemplates.GenerateApacheConfigWithCollabora()`

See [Reverse Proxy Setup](../../development/REVERSE_PROXY.md) for details.

---

## Blazor UI Integration

The `DocumentEditor.razor` component:

1. Checks if Collabora supports the file's MIME type
2. Generates a WOPI access token
3. Embeds Collabora in an `<iframe>` with the token and file ID
4. Shows co-editing indicators (who is currently editing)
5. For end-to-end encrypted files, displays a "download to edit locally" fallback

---

## Health Check

The `CollaboraHealthCheck` monitors Collabora availability and is included in the `/health` endpoint. It reports:

- `Healthy`: Collabora is responsive
- `Degraded`: Collabora is slow to respond
- `Unhealthy`: Collabora is unreachable

---

## Admin UI

The Collabora admin page (`/admin/collabora`) provides:

- Server URL configuration (built-in vs. external)
- Connection status and health
- Session count and limit
- Auto-save interval configuration
- Supported file type filtering
