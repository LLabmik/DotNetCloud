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
- User identity for co-editing attribution

Also begins a session slot for concurrent session tracking.

### GetFile

```
GET /api/v1/wopi/files/{fileId}/contents?access_token={token}
```

Returns the file content as a byte stream. Reads from the storage engine via the download service.

### PutFile

```
POST /api/v1/wopi/files/{fileId}/contents?access_token={token}
```

Saves the edited document content. Creates a new file version using the chunked upload pipeline to maintain deduplication and version history.

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

- Default limit: 20 concurrent sessions (configurable via `MaxConcurrentSessions`)
- Sessions are tracked per file + user pair
- When the limit is reached, new CheckFileInfo requests return `503 Service Unavailable`
- Sessions are released when editing ends (via `DELETE /api/v1/wopi/token/{fileId}`)

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

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `false` | Enable Collabora integration |
| `ServerUrl` | `""` | URL of external Collabora server |
| `WopiBaseUrl` | `""` | Public URL of this DotNetCloud instance (for WOPI callbacks) |
| `TokenSigningKey` | `""` | HMAC-SHA256 key for signing tokens (≥32 chars, auto-generated if empty) |
| `TokenLifetimeMinutes` | `480` | Token validity period (8 hours) |
| `AutoSaveIntervalSeconds` | `300` | Collabora auto-save interval (5 minutes) |
| `MaxConcurrentSessions` | `20` | Max simultaneous editing sessions (0 = unlimited) |
| `EnableProofKeyValidation` | `true` | Validate Collabora proof key signatures |
| `SupportedMimeTypes` | `[]` | Filter allowed MIME types (empty = all from discovery) |
| `UseBuiltInCollabora` | `false` | Use DotNetCloud-managed Collabora CODE process |
| `CollaboraInstallDirectory` | `""` | Path to Collabora CODE installation |
| `CollaboraExecutablePath` | `""` | Path to `coolwsd` executable |
| `CollaboraMaxRestartAttempts` | `5` | Max restart attempts before giving up |
| `CollaboraRestartBackoffSeconds` | `5` | Base delay for exponential restart backoff |

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
