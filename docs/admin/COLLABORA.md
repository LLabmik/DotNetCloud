# Files Module — Collabora CODE Administration

> **Last Updated:** 2026-03-03

---

## Overview

DotNetCloud integrates with [Collabora Online](https://www.collaboraonline.com/) for browser-based editing of office documents (Word, Excel, PowerPoint, and more). This guide covers installation, configuration, and troubleshooting of the Collabora integration.

---

## Deployment Options

### Option 1: Built-In Collabora CODE

DotNetCloud can manage a local Collabora CODE instance automatically.

**Advantages:**
- Zero external dependencies
- Automatic process management (start, restart, health check)
- Simplified setup

**Configuration:**

```json
{
  "Files": {
    "Collabora": {
      "Enabled": true,
      "UseBuiltInCollabora": true,
      "CollaboraInstallDirectory": "/opt/collaboraoffice",
      "WopiBaseUrl": "https://cloud.example.com",
      "TokenSigningKey": "your-secret-key-at-least-32-characters"
    }
  }
}
```

**Installation via CLI:**

```bash
dotnetcloud install collabora
```

Or during initial setup:

```bash
dotnetcloud setup
# Select "Yes" when prompted for Collabora CODE installation
```

### Option 2: External Collabora Server

Point DotNetCloud to an existing Collabora Online server (e.g., a Docker container or a dedicated host).

**Configuration:**

```json
{
  "Files": {
    "Collabora": {
      "Enabled": true,
      "UseBuiltInCollabora": false,
      "ServerUrl": "https://collabora.example.com",
      "WopiBaseUrl": "https://cloud.example.com",
      "TokenSigningKey": "your-secret-key-at-least-32-characters"
    }
  }
}
```

**Docker example:**

```bash
docker run -d \
  --name collabora \
  -p 9980:9980 \
  -e "aliasgroup1=https://cloud.example.com:443" \
  -e "username=admin" \
  -e "password=admin" \
  --restart always \
  collabora/code:latest
```

Set `ServerUrl` to `https://collabora.example.com:9980`.

---

## Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `false` | Enable Collabora integration |
| `ServerUrl` | `""` | URL of external Collabora server |
| `WopiBaseUrl` | `""` | Public URL of this DotNetCloud instance |
| `TokenSigningKey` | `""` | HMAC-SHA256 signing key for WOPI tokens (≥32 chars) |
| `TokenLifetimeMinutes` | `480` | Token validity (8 hours) |
| `AutoSaveIntervalSeconds` | `300` | Collabora auto-save interval (5 minutes) |
| `MaxConcurrentSessions` | `20` | Max simultaneous editing sessions (0 = unlimited) |
| `EnableProofKeyValidation` | `true` | Validate Collabora proof key signatures |
| `SupportedMimeTypes` | `[]` | Filter allowed MIME types (empty = all) |
| `UseBuiltInCollabora` | `false` | Manage local CODE process |
| `CollaboraInstallDirectory` | `""` | Path to CODE installation |
| `CollaboraExecutablePath` | `""` | Path to `coolwsd` executable |
| `CollaboraMaxRestartAttempts` | `5` | Max restart attempts before giving up |
| `CollaboraRestartBackoffSeconds` | `5` | Base delay for exponential restart backoff |

---

## WOPI Base URL

The `WopiBaseUrl` must be the public-facing URL of your DotNetCloud instance. Collabora uses this URL to call back to DotNetCloud for file operations.

**Requirements:**
- Must be HTTPS in production
- Must be accessible from the Collabora server
- Must not include a trailing slash

**Example:**

```
WopiBaseUrl: "https://cloud.example.com"
```

Collabora will call endpoints like:

```
https://cloud.example.com/api/v1/wopi/files/{fileId}?access_token=...
```

---

## Token Signing Key

The `TokenSigningKey` is used to sign WOPI access tokens with HMAC-SHA256. It must be:

- At least 32 characters long
- Kept secret (do not commit to version control)
- Consistent across restarts (tokens become invalid if the key changes)

**Generate a secure key:**

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

Or on Linux:

```bash
openssl rand -base64 48
```

If `TokenSigningKey` is empty, DotNetCloud generates one automatically on startup and stores it in the database. This works for single-instance deployments but not for load-balanced setups.

---

## Reverse Proxy Configuration

Collabora requires WebSocket support for real-time editing. DotNetCloud generates reverse proxy templates automatically.

### nginx

```nginx
# Collabora WebSocket support
location /cool/ {
    proxy_pass https://collabora.example.com:9980;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "Upgrade";
    proxy_set_header Host $host;
    proxy_read_timeout 36000s;
}

# WOPI endpoints
location /api/v1/wopi/ {
    proxy_pass https://127.0.0.1:5001;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

### Apache

```apache
# Enable required modules
LoadModule proxy_module modules/mod_proxy.so
LoadModule proxy_wstunnel_module modules/mod_proxy_wstunnel.so
LoadModule proxy_http_module modules/mod_proxy_http.so

# Collabora WebSocket
ProxyPass "/cool/" "wss://collabora.example.com:9980/cool/"
ProxyPassReverse "/cool/" "wss://collabora.example.com:9980/cool/"
```

---

## Concurrent Session Management

The `MaxConcurrentSessions` setting limits how many documents can be edited simultaneously. This prevents resource exhaustion on the Collabora server.

- Default: 20 concurrent sessions
- Set to `0` for unlimited
- When the limit is reached, new edit requests return HTTP 503

Sessions are tracked per file + user pair. A session begins when Collabora calls CheckFileInfo and ends when the user closes the editor (via `DELETE /api/v1/wopi/token/{fileId}`).

### Monitoring Sessions

Check the number of active sessions via the admin dashboard (`/admin/collabora`) or the health check endpoint.

---

## Supported File Types

By default, Collabora supports all file types returned by its WOPI discovery endpoint. To restrict editing to specific types:

```json
{
  "Files": {
    "Collabora": {
      "SupportedMimeTypes": [
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.spreadsheet"
      ]
    }
  }
}
```

### Check Extension Support

```
GET /api/v1/wopi/discovery/supports/docx
```

---

## Built-In Process Management

When `UseBuiltInCollabora` is `true`, the `CollaboraProcessManager` (a `BackgroundService`) manages the Collabora CODE process:

### Lifecycle

1. **Startup:** Locates the `coolwsd` executable and starts the process
2. **Health monitoring:** Periodically checks Collabora's HTTP health endpoint
3. **Crash recovery:** Restarts the process with exponential backoff (up to `CollaboraMaxRestartAttempts`)
4. **Shutdown:** Sends a graceful stop signal when DotNetCloud stops

### Restart Backoff

| Attempt | Delay |
|---|---|
| 1 | 5 seconds |
| 2 | 10 seconds |
| 3 | 20 seconds |
| 4 | 40 seconds |
| 5 | 80 seconds |

After `CollaboraMaxRestartAttempts` failures, the process manager stops attempting restarts and logs an error. Manual intervention is required.

---

## Health Check

The `CollaboraHealthCheck` reports:

| Status | Condition |
|---|---|
| **Healthy** | Collabora responds within 5 seconds |
| **Degraded** | Collabora responds but slowly (>5s) |
| **Unhealthy** | Collabora is unreachable or returns an error |

Check via:

```
GET /health
```

---

## Troubleshooting

### Collabora Not Loading Documents

1. Verify `WopiBaseUrl` is correct and accessible from the Collabora server
2. Check that the WOPI endpoints are reachable: `curl https://cloud.example.com/api/v1/wopi/discovery`
3. Verify WebSocket support is enabled in the reverse proxy
4. Check Collabora logs: `docker logs collabora` or `/var/log/coolwsd.log`

### Token Validation Failures

1. Ensure `TokenSigningKey` is the same across all instances
2. Check system clock synchronization (token validation is time-sensitive)
3. Verify `TokenLifetimeMinutes` is long enough for editing sessions

### Session Limit Reached

1. Check current session count in admin dashboard
2. Increase `MaxConcurrentSessions` if resources allow
3. Ask users to close documents they are not actively editing

### Built-In Collabora Won't Start

1. Check `CollaboraInstallDirectory` path exists and contains Collabora CODE
2. Verify the process user has execute permission on `coolwsd`
3. Check logs for error details: look for `CollaboraProcessManager` entries
4. Try starting `coolwsd` manually to see the error output

---

## Related Documentation

- [WOPI Integration Details](../modules/WOPI.md)
- [Admin Configuration](CONFIGURATION.md)
- [Reverse Proxy Setup](../../development/REVERSE_PROXY.md)
