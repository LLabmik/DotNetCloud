# DotNetCloud Server — Configuration Reference

> **Last Updated:** 2026-03-07  
> **Applies To:** DotNetCloud 1.0.x  
> **Audience:** System administrators

---

## Table of Contents

1. [Configuration Sources](#configuration-sources)
2. [Connection Strings](#connection-strings)
3. [Kestrel (Web Server)](#kestrel-web-server)
4. [Authentication](#authentication)
5. [CORS](#cors)
6. [Rate Limiting](#rate-limiting)
7. [SignalR (Real-Time)](#signalr-real-time)
8. [Logging (Serilog)](#logging-serilog)
9. [Telemetry (OpenTelemetry)](#telemetry-opentelemetry)
10. [API Versioning](#api-versioning)
11. [Security Headers](#security-headers)
12. [Files Module](#files-module)
13. [Chat Module](#chat-module)
14. [Environment Variable Reference](#environment-variable-reference)

---

## Configuration Sources

DotNetCloud loads configuration in this order (later sources override earlier ones):

1. `appsettings.json` — built-in defaults (do not edit)
2. `appsettings.{ENVIRONMENT}.json` — environment-specific overrides
3. Environment variables
4. Command-line arguments

### Recommended Approach

**Linux:** Use environment variables in the systemd unit file or create `/etc/dotnetcloud/appsettings.Production.json`.

**Windows:** Create `appsettings.Production.json` in the server directory or set environment variables on the Windows Service.

**Docker:** Use environment variables in `docker-compose.yml` or mount a config file.

### Environment Variable Naming

ASP.NET Core uses `__` (double underscore) as a section separator:

```bash
# appsettings.json:  { "Kestrel": { "HttpPort": 5080 } }
# Environment var:
export Kestrel__HttpPort=8080
```

---

## Connection Strings

### PostgreSQL (Recommended)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dotnetcloud;Username=dotnetcloud;Password=your-password;Include Error Detail=true"
  }
}
```

| Parameter | Default | Description |
|---|---|---|
| `Host` | `localhost` | Database server hostname |
| `Port` | `5432` | PostgreSQL port |
| `Database` | `dotnetcloud` | Database name |
| `Username` | — | Database user |
| `Password` | — | Database password |
| `Include Error Detail` | `false` | Include query details in errors (dev only) |
| `Pooling` | `true` | Enable connection pooling |
| `Maximum Pool Size` | `100` | Max connections in pool |
| `SSL Mode` | `Prefer` | TLS mode: `Disable`, `Prefer`, `Require`, `VerifyFull` |

### SQL Server

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=dotnetcloud;User Id=dotnetcloud;Password=your-password;TrustServerCertificate=True"
  }
}
```

**Windows Authentication:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=dotnetcloud;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### MariaDB

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=dotnetcloud;User=dotnetcloud;Password=your-password"
  }
}
```

> **Note:** MariaDB support requires the Pomelo EF Core provider, which may lag behind .NET releases.

---

## Kestrel (Web Server)

```json
{
  "Kestrel": {
    "HttpPort": 5080,
    "HttpsPort": 5443,
    "EnableHttps": true,
    "EnableHttp2": true,
    "MaxRequestBodySize": 52428800,
    "RequestHeaderTimeoutSeconds": 30,
    "KeepAliveTimeoutSeconds": 120,
    "ListenAddresses": []
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `HttpPort` | `5080` | HTTP listening port |
| `HttpsPort` | `5443` | HTTPS listening port |
| `EnableHttps` | `true` | Enable HTTPS listener |
| `EnableHttp2` | `true` | Enable HTTP/2 protocol |
| `MaxRequestBodySize` | `52428800` (50 MB) | Max request body size. Set to `0` for unlimited (recommended when using chunked uploads). |
| `RequestHeaderTimeoutSeconds` | `30` | Timeout for receiving request headers |
| `KeepAliveTimeoutSeconds` | `120` | Keep-alive connection timeout |
| `ListenAddresses` | `[]` (localhost only) | Additional listen addresses. Example: `["0.0.0.0"]` for all interfaces. |

### Listening on All Interfaces

By default, Kestrel listens only on `127.0.0.1` (localhost). To accept external connections directly (without a reverse proxy):

```json
{
  "Kestrel": {
    "ListenAddresses": ["0.0.0.0"]
  }
}
```

Or via environment variable:

```bash
export ASPNETCORE_URLS="http://0.0.0.0:5080;https://0.0.0.0:5443"
```

### TLS Certificate (Direct Kestrel)

When running without a reverse proxy:

```json
{
  "Kestrel": {
    "EnableHttps": true,
    "CertificatePath": "/etc/letsencrypt/live/cloud.example.com/fullchain.pem",
    "CertificateKeyPath": "/etc/letsencrypt/live/cloud.example.com/privkey.pem"
  }
}
```

---

## Authentication

```json
{
  "Auth": {
    "AccessTokenLifetime": 3600,
    "RefreshTokenLifetime": 604800
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `AccessTokenLifetime` | `3600` (1 hour) | Access token validity in seconds |
| `RefreshTokenLifetime` | `604800` (7 days) | Refresh token validity in seconds |

Token signing keys are managed by OpenIddict. In development, ephemeral keys are used. In production, keys are persisted in the database automatically.

### External OAuth2 Providers

To enable "Sign in with Google/GitHub/etc.", configure external providers in `appsettings.Production.json`:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret"
    },
    "GitHub": {
      "ClientId": "your-github-client-id",
      "ClientSecret": "your-github-client-secret"
    }
  }
}
```

### Built-In OIDC Client Seeding (First-Party Desktop)

On startup, DotNetCloud seeds required first-party OpenIddict applications if they do not already exist.
This currently includes the desktop SyncTray public client used for OAuth2 Authorization Code + PKCE.

| Property | Value |
|---|---|
| `client_id` | `dotnetcloud-desktop` |
| `client_type` | `public` |
| Redirect URI | `http://localhost:52701/oauth/callback` |
| Grant types | `authorization_code`, `refresh_token` |
| Response type | `code` |
| Required feature | `PKCE` |
| Default scopes | `openid`, `offline_access`, `profile`, `files:read`, `files:write` |

Operational notes:

- Seeding runs during server startup database initialization.
- If `dotnetcloud-desktop` already exists, it is left unchanged.
- If it is missing (for example after a manual DB cleanup), it is recreated automatically on next successful startup.

---

## CORS

```json
{
  "Cors": {
    "AllowedOrigins": [],
    "AllowedMethods": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"],
    "AllowedHeaders": ["Authorization", "Content-Type", "Accept", "X-Requested-With", "X-Api-Version"],
    "ExposedHeaders": ["X-Api-Version", "X-Api-Deprecated", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "Retry-After"],
    "AllowCredentials": true,
    "PreflightMaxAgeSeconds": 600
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `AllowedOrigins` | `[]` (all origins) | Restrict to specific origins. Example: `["https://cloud.example.com"]` |
| `AllowCredentials` | `true` | Allow cookies and auth headers in cross-origin requests |
| `PreflightMaxAgeSeconds` | `600` | Cache preflight responses for 10 minutes |

### Production Recommendation

Lock down CORS to your domain:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://cloud.example.com"]
  }
}
```

---

## Rate Limiting

```json
{
  "RateLimiting": {
    "Enabled": true,
    "GlobalPermitLimit": 100,
    "GlobalWindowSeconds": 60,
    "AuthenticatedPermitLimit": 200,
    "AuthenticatedWindowSeconds": 60,
    "IncludeHeaders": true,
    "QueueLimit": 0,
    "ModuleLimits": {}
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `true` | Enable rate limiting |
| `GlobalPermitLimit` | `100` | Max requests per window (anonymous) |
| `GlobalWindowSeconds` | `60` | Window size in seconds |
| `AuthenticatedPermitLimit` | `200` | Max requests per window (authenticated) |
| `IncludeHeaders` | `true` | Include `X-RateLimit-*` headers in responses |
| `QueueLimit` | `0` | Requests to queue when limit reached (0 = reject immediately) |
| `ModuleLimits` | `{}` | Per-module overrides |

### Per-Module Rate Limits

```json
{
  "RateLimiting": {
    "ModuleLimits": {
      "files": {
        "PermitLimit": 300,
        "WindowSeconds": 60
      },
      "chat": {
        "PermitLimit": 500,
        "WindowSeconds": 60
      }
    }
  }
}
```

---

## SignalR (Real-Time)

```json
{
  "SignalR": {
    "HubPath": "/hubs/core",
    "KeepAliveIntervalSeconds": 15,
    "ClientTimeoutSeconds": 30,
    "HandshakeTimeoutSeconds": 15,
    "MaximumParallelInvocationsPerClient": 10,
    "MaximumReceiveMessageSize": 32768,
    "MaxConnections": 0,
    "EnableDetailedErrors": false,
    "WebSocketKeepAliveSeconds": 30,
    "EnableWebSockets": true,
    "EnableServerSentEvents": true,
    "EnableLongPolling": true,
    "PresenceCleanupIntervalSeconds": 60
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `HubPath` | `/hubs/core` | URL path for the SignalR hub |
| `KeepAliveIntervalSeconds` | `15` | Server-side keep-alive ping interval |
| `ClientTimeoutSeconds` | `30` | Disconnect client after this period of silence |
| `MaximumReceiveMessageSize` | `32768` (32 KB) | Max message size from clients |
| `MaxConnections` | `0` (unlimited) | Max concurrent WebSocket connections |
| `EnableDetailedErrors` | `false` | Send detailed error messages to clients (dev only) |
| `PresenceCleanupIntervalSeconds` | `60` | How often to clean up stale presence entries |

---

## Logging (Serilog)

```json
{
  "Serilog": {
    "ConsoleMinimumLevel": "Information",
    "FileMinimumLevel": "Warning",
    "FilePath": "logs/dotnetcloud-.log",
    "RollingDaily": true,
    "RetainedFileCountLimit": 31,
    "FileSizeLimitBytes": 104857600,
    "UseStructuredFormat": true,
    "ExcludedModules": [],
    "ModuleLogLevels": {}
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `ConsoleMinimumLevel` | `Information` | Minimum level for console output |
| `FileMinimumLevel` | `Warning` | Minimum level for file output |
| `FilePath` | `logs/dotnetcloud-.log` | Log file path (date appended automatically) |
| `RollingDaily` | `true` | Create a new log file each day |
| `RetainedFileCountLimit` | `31` | Keep 31 days of log files |
| `FileSizeLimitBytes` | `104857600` (100 MB) | Max log file size before rolling |
| `UseStructuredFormat` | `true` | JSON structured logging |
| `ExcludedModules` | `[]` | Module names to exclude from logging |
| `ModuleLogLevels` | `{}` | Per-module minimum levels |

### Per-Module Log Levels

```json
{
  "Serilog": {
    "ModuleLogLevels": {
      "files": "Debug",
      "chat": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Production Recommendation

```json
{
  "Serilog": {
    "ConsoleMinimumLevel": "Warning",
    "FileMinimumLevel": "Information",
    "FilePath": "/var/log/dotnetcloud/dotnetcloud-.log",
    "RetainedFileCountLimit": 90,
    "UseStructuredFormat": true
  }
}
```

---

## Telemetry (OpenTelemetry)

```json
{
  "Telemetry": {
    "ServiceName": "DotNetCloud",
    "ServiceVersion": "1.0.0",
    "EnableMetrics": true,
    "EnableTracing": true,
    "EnableConsoleExporter": false,
    "EnablePrometheusExporter": false,
    "OtlpEndpoint": "",
    "AdditionalSources": [],
    "AdditionalMeters": []
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `EnableMetrics` | `true` | Collect and export metrics |
| `EnableTracing` | `true` | Collect distributed traces |
| `EnableConsoleExporter` | `false` | Export to console (dev only) |
| `EnablePrometheusExporter` | `false` | Expose `/metrics` endpoint for Prometheus |
| `OtlpEndpoint` | `""` | OTLP collector endpoint (e.g., `http://otel-collector:4317`) |

### Prometheus Setup

```json
{
  "Telemetry": {
    "EnablePrometheusExporter": true
  }
}
```

Metrics are available at `http://localhost:5080/metrics`.

Add to `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: dotnetcloud
    static_configs:
      - targets: ['localhost:5080']
    metrics_path: /metrics
```

---

## API Versioning

```json
{
  "ApiVersioning": {
    "CurrentVersion": "1",
    "MinimumVersion": "1",
    "DeprecatedVersions": [],
    "RoutePrefix": "api/v{version}"
  }
}
```

All API endpoints are versioned under `/api/v1/`. When breaking changes are introduced, a new version is added and old versions are marked deprecated.

---

## Security Headers

```json
{
  "Security": {
    "SecurityHeaders": {
      "ContentSecurityPolicy": "default-src 'self'",
      "XFrameOptions": "SAMEORIGIN"
    }
  }
}
```

These headers are applied to all responses by the `SecurityHeadersMiddleware`. Additional headers (`X-Content-Type-Options`, `Strict-Transport-Security`) are added automatically.

### Collabora CSP Exception

When Collabora is enabled, the Content-Security-Policy must allow framing from the Collabora server:

```json
{
  "Security": {
    "SecurityHeaders": {
      "ContentSecurityPolicy": "default-src 'self'; frame-src 'self' https://collabora.example.com"
    }
  }
}
```

---

## Files Module

See [Files Module Configuration](../files/CONFIGURATION.md) for the complete reference. Key settings:

```json
{
  "Files": {
    "StorageRoot": "/var/lib/dotnetcloud/files",
    "Quota": {
      "DefaultQuotaBytes": 10737418240,
      "WarnAtPercent": 80.0,
      "CriticalAtPercent": 95.0
    },
    "TrashRetention": {
      "RetentionDays": 30
    },
    "Collabora": {
      "Enabled": false,
      "ServerUrl": "",
      "WopiBaseUrl": ""
    }
  }
}
```

---

## Chat Module

Key settings for the chat module:

```json
{
  "Chat": {
    "MaxMessageLength": 4000,
    "MaxChannelNameLength": 80,
    "TypingIndicatorTimeoutSeconds": 5,
    "DefaultSystemChannels": ["general", "announcements"]
  }
}
```

---

## Environment Variable Reference

Quick reference for the most common settings configured via environment variables:

| Variable | Example | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Production` in production |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5080` | Override listen URLs |
| `ConnectionStrings__DefaultConnection` | `Host=...` | Database connection string |
| `Kestrel__HttpPort` | `5080` | HTTP port |
| `Kestrel__EnableHttps` | `false` | Disable HTTPS (when behind reverse proxy) |
| `Files__StorageRoot` | `/data/files` | File storage directory |
| `Serilog__FilePath` | `/var/log/dotnetcloud/dnc-.log` | Log file path |
| `Serilog__FileMinimumLevel` | `Information` | File log level |
| `Telemetry__EnablePrometheusExporter` | `true` | Enable Prometheus metrics |
| `RateLimiting__Enabled` | `true` | Enable rate limiting |
| `Cors__AllowedOrigins__0` | `https://cloud.example.com` | First allowed CORS origin |
| `Files__Collabora__Enabled` | `true` | Enable Collabora |
| `Files__Collabora__ServerUrl` | `https://collabora:9980` | Collabora server URL |

---

## Related Documentation

- [Installation Guide](INSTALLATION.md)
- [Upgrading DotNetCloud](UPGRADING.md)
- [Files Module Configuration](../files/CONFIGURATION.md)
- [Collabora Administration](../files/COLLABORA.md)
- [Architecture Overview](../../architecture/ARCHITECTURE.md)
- [Observability Guide](../../architecture/observability.md)
