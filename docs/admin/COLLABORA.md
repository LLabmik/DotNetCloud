# Files Module — Collabora CODE Administration

> **Last Updated:** 2026-04-01

---

## Overview

DotNetCloud integrates with [Collabora Online](https://www.collaboraonline.com/) for browser-based editing of office documents (Word, Excel, PowerPoint, and more). This guide covers installation, configuration, and troubleshooting of the Collabora integration.

---

## CODE (Free) vs Collabora Online (Paid)

Collabora offers two editions:

| | **Collabora CODE** | **Collabora Online** |
|---|---|---|
| **Cost** | Free / open source | Paid subscription |
| **Concurrent editors** | ~10–20 users | Unlimited (per license) |
| **Support** | Community only | Commercial support |
| **Docker image** | `collabora/code` | `collabora/online` (requires license key) |
| **APT package** | `coolwsd` + `code-brand` (CODE repo) | `coolwsd` + enterprise packages (partner repo) |
| **Use case** | Small teams, home labs, development | Business, education, large deployments |

Both editions use the same WOPI protocol and integrate identically with DotNetCloud. The only differences are user limits and support.

### When to upgrade from CODE to paid

- You regularly exceed ~10–20 concurrent document editors
- You need vendor support / SLA guarantees
- You want enterprise features (admin console, clustering, etc.)

### Upgrade path

1. Purchase a license at [collaboraonline.com](https://www.collaboraonline.com/)
2. Switch your DotNetCloud config from **BuiltIn** to **External** mode
3. Deploy the paid Collabora Online instance (Docker, VM, or bare-metal)
4. Point `collaboraUrl` in `config.json` to the paid instance
5. Restart DotNetCloud — no other changes are needed

---

## Deployment Options

### Option 1: Built-In Collabora CODE (Free, ~20 users)

DotNetCloud can manage a local Collabora CODE instance automatically. The CLI installs coolwsd via APT and configures a **built-in YARP reverse proxy** so all Collabora traffic flows through the DotNetCloud port — only one firewall port is needed.

> **Note:** CODE is limited to approximately 10–20 concurrent document editors.
> If you need more, see [Option 2: External Collabora Server](#option-2-external-collabora-server-paid-or-self-hosted) below.

**Advantages:**
- Zero external dependencies
- Single port exposure (no need to open port 9980)
- Automatic reverse proxy via YARP (`/hosting`, `/browser`, `/cool`, `/lool`)
- Simplified setup — the CLI handles all environment variable bridging

**How it works:**

The CLI (`dotnetcloud start`) reads `config.json` and sets environment variables:
- `ServerUrl` = public origin (e.g., `https://mint22:5443`) — discovery URLs rewrite to this
- `ProxyUpstreamUrl` = `https://localhost:9980` — internal coolwsd target for the YARP proxy
- `WopiBaseUrl` = same public origin — Collabora uses this for WOPI callbacks

Browsers load Collabora via `https://yourhost:5443/browser/...` which the YARP proxy forwards internally to `localhost:9980`.

**Configuration in `config.json`:**

```json
{
  "collaboraMode": "BuiltIn",
  "collaboraDirectory": "/usr/share/coolwsd"
}
```

The CLI bridges this to server configuration automatically. No manual `Files:Collabora:*` settings needed.

**Installation via CLI:**

```bash
dotnetcloud install collabora
```

Or during initial setup:

```bash
dotnetcloud setup
# Select "Yes" when prompted for Collabora CODE installation
```

### Option 2: External Collabora Server (Paid or Self-Hosted)

Point DotNetCloud to an existing Collabora Online server — either a paid enterprise instance or a self-managed Docker/VM deployment. This is the recommended path when you need more concurrent users than CODE allows.

**Configuration in `config.json`:**

```json
{
  "collaboraMode": "External",
  "collaboraUrl": "https://collabora.example.com:9980"
}
```

The CLI bridges this to the server environment automatically (`Files__Collabora__ServerUrl`, `WopiBaseUrl`, `TokenSigningKey`).

> **Note:** For advanced or manual deployments, you can set the raw `Files:Collabora:*` settings directly instead:
>
> ```json
> {
>   "Files": {
>     "Collabora": {
>       "Enabled": true,
>       "UseBuiltInCollabora": false,
>       "ServerUrl": "https://collabora.example.com:9980",
>       "WopiBaseUrl": "https://cloud.example.com",
>       "TokenSigningKey": "your-secret-key-at-least-32-characters"
>     }
>   }
> }
> ```

**Docker examples:**

Free CODE image (same functionality as Built-In, but you manage the container yourself):

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

Paid Collabora Online image (requires a license key from [collaboraonline.com](https://www.collaboraonline.com/)):

```bash
docker run -d \
  --name collabora \
  -p 9980:9980 \
  -e "aliasgroup1=https://cloud.example.com:443" \
  -e "username=admin" \
  -e "password=admin" \
  --restart always \
  collabora/online:latest
```

Set `collaboraUrl` to `https://<collabora-host>:9980` (or whatever port your instance uses).

---

## Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `false` | Enable Collabora integration |
| `ServerUrl` | `""` | Public-facing URL for Collabora (iframe src origin) |
| `WopiBaseUrl` | `""` | Public URL of this DotNetCloud instance |
| `ProxyUpstreamUrl` | `""` | Internal Collabora endpoint for YARP proxy (e.g., `https://localhost:9980`) |
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

### Built-In YARP Proxy (Default — Recommended)

DotNetCloud includes a built-in YARP reverse proxy that routes Collabora traffic through the main DotNetCloud port. **No separate reverse proxy is needed** for Collabora when using Built-In mode.

The proxy maps these URL spaces from the DotNetCloud port to coolwsd on `localhost:9980`:

| Path | Purpose |
|---|---|
| `/hosting/**` | WOPI discovery |
| `/browser/**` | Collabora editor static assets + `cool.html` |
| `/cool/**` | WebSocket real-time editing sessions |
| `/lool/**` | Legacy editing sessions |

**Key settings that control this:**

| Setting | Value | Purpose |
|---|---|---|
| `ServerUrl` | Public origin (e.g., `https://mint22:5443`) | Discovery URLs rewrite to this |
| `ProxyUpstreamUrl` | `https://localhost:9980` | Internal proxy target |
| `AllowInsecureTls` | `true` | Accept coolwsd's self-signed cert |

When `ServerUrl` and `WopiBaseUrl` share the same origin, `ProxyUpstreamUrl` **must** be set to avoid self-proxy loops.

### External Reverse Proxy (nginx / Apache)

If you need a separate reverse proxy (e.g., for External Collabora mode or TLS termination), Collabora requires WebSocket support for real-time editing.

#### nginx

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

#### Apache

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
