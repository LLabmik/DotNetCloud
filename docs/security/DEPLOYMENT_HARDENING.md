# Deployment Hardening Guide

**Last Updated:** May 14, 2026

---

## Overview

This guide provides security hardening recommendations for self-hosted DotNetCloud deployments. Follow these guidelines to ensure your instance is configured securely.

---

## Prerequisites

- Linux server (Debian 12+ or Ubuntu 22.04+ recommended)
- Reverse proxy (nginx — auto-configured; or bring your own)
- PostgreSQL 16+
- Valid TLS certificate (Let's Encrypt recommended)

---

## 1. TLS Configuration

### Certificate Validation

- **Never disable TLS validation in production.** The `AllowInsecureTls` option exists for development environments with self-signed certificates only.
- Ensure your TLS certificate is from a trusted CA (Let's Encrypt, ZeroSSL, etc.)
- HSTS is enabled by default with a 1-year max-age — this is appropriate for production.

### Configuration

```json
{
  "Kestrel": {
    "EnableHttps": true,
    "HttpsPort": 5443,
    "CertificatePath": "/path/to/your/certificate.pfx"
  }
}
```

**Do not** set `Files:Collabora:AllowInsecureTls` to `true` in production unless you fully understand the MITM risk.

---

## 2. Database Security

### Connection Strings

Production database credentials must be provided via the `DOTNETCLOUD_DB_CONNECTION` environment variable:

```bash
export DOTNETCLOUD_DB_CONNECTION="Host=db.example.com;Database=dotnetcloud;Username=dotnetcloud;Password=your-strong-password"
```

**Never** store production credentials in `appsettings.json` or any file in the repository.

### PostgreSQL Hardening

- Use a dedicated database user (not `postgres` superuser)
- Restrict network access — bind PostgreSQL to localhost or private network
- Enable TLS for database connections
- Set strong password (30+ characters, mixed case, special chars)
- Regularly backup and test restoration

---

## 3. Rate Limiting

Default limits are intentionally permissive for self-hosted deployments. For high-security environments, reduce these limits:

```json
{
  "RateLimiting": {
    "Enabled": true,
    "GlobalPermitLimit": 100,
    "GlobalWindowSeconds": 60,
    "AuthenticatedPermitLimit": 500,
    "AuthenticatedWindowSeconds": 60
  }
}
```

Recommended minimums for shared hosting:

- Authenticated: 500 requests per 60 seconds
- Unauthenticated: 100 requests per 60 seconds
- Auth endpoints (login, register): 10 requests per 60 seconds per IP

---

## 4. Host Header Validation

**The `AllowedHosts` setting was hardened in the security review.** Default configuration:

```json
{
  "AllowedHosts": "localhost;*.dotnetcloud.net;*.local"
}
```

Update this to match your actual domain(s):

```json
{
  "AllowedHosts": "cloud.example.com;*.example.com"
}
```

---

## 5. CORS Configuration

CORS is configured with explicit origins only — `AllowAnyOrigin` is never used.

For production, set your actual domains:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://cloud.example.com"],
    "AllowCredentials": true
  }
}
```

---

## 6. File Upload Security

### Current Status

File extension whitelisting and magic byte validation are not yet implemented (scheduled for Beta). In the meantime:

1. Configure reverse proxy to block dangerous file types at the ingress level
2. Monitor upload directories for suspicious files
3. Consider ClamAV integration for malware scanning (planned for GA)

### nginx Configuration (Built-in Templates)

The auto-generated nginx configuration includes:

- `X-Content-Type-Options: nosniff` (except video streaming endpoints)
- Request body size limits (matching Kestrel MaxRequestBodySize)
- Proper CSP headers

---

## 7. Logging & Monitoring

### Sensitive Data

The `RequestResponseLoggingMiddleware` automatically masks:

- `access_token`, `token`, `api_key`, `apikey`, `secret`, `password`

### Recommended Production Log Level

```json
{
  "Serilog": {
    "ConsoleMinimumLevel": "Warning",
    "FileMinimumLevel": "Information"
  }
}
```

### Monitoring

- Enable OpenTelemetry metrics and tracing for production monitoring
- Configure health check endpoints: `/health`, `/health/ready`, `/health/live`
- Set up Prometheus + Grafana dashboards for long-term observability

---

## 8. Secret Management

### What NOT to Do

- ❌ Store secrets in `appsettings.json`
- ❌ Store secrets in source control
- ❌ Use default/dev passwords in production

### What to Do

- ✅ Use `DOTNETCLOUD_DB_CONNECTION` environment variable for database credentials
- ✅ Use `.NET User Secrets` for local development (`dotnet user-secrets set`)
- ✅ Use a secrets manager (Hashicorp Vault, Azure Key Vault, etc.) for production
- ✅ Rotate secrets periodically

---

## 9. Module Isolation

Each module runs as a separate OS process. In production:

- Each module should run under its own Linux user account
- Unix socket files should have permissions `0600` (owner read/write only)
- File system access should be restricted per module (use systemd `ReadWritePaths`, `ProtectSystem`, etc.)
- No module should directly access another module's database

The auto-generated systemd service files handle most of this automatically.

---

## 10. Content Security Policy (CSP)

### Current Policy

The default CSP header sent on all responses:

```
default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' ws: wss:; frame-ancestors 'self';
```

### Why `unsafe-inline` and `unsafe-eval` Are Required

These directives are **required by Blazor WebAssembly** and cannot be removed without breaking the application:

| Directive            | Why It's Needed                                                                          |
| -------------------- | ---------------------------------------------------------------------------------------- |
| `'unsafe-inline'`    | Blazor uses inline `<script>` blocks for boot configuration and initializer registration |
| `'unsafe-eval'`      | The Mono WASM runtime uses `eval()` for JIT compilation of C# IL to WebAssembly          |
| `'wasm-unsafe-eval'` | Required for WebAssembly execution in modern browsers                                    |

### Risk Mitigation

While these directives broaden the XSS attack surface compared to a strict CSP, the risk is partially mitigated by:

1. **Blazor's built-in antiforgery** — all interactive components go through the Blazor circuit with automatic antiforgery token validation
2. **API authentication** — all API endpoints require OAuth2 bearer tokens
3. **Server-side rendering** — the initial page load is server-rendered; the CSP applies to the SPA runtime
4. **HtmlSanitizer** — all user-supplied HTML is sanitized before rendering (configurable via `DotNetCloud:HtmlSanitizer`)

### Future CSP Hardening

If non-Blazor pages are added (static pages, marketing, docs), they should use a separate stricter CSP:

- `script-src 'self'` (no `unsafe-inline` or `unsafe-eval`)
- Nonce-based CSP for injected scripts

## 11. OIDC Key Rotation

DotNetCloud automatically rotates OpenIddict signing and encryption keys.

### How It Works

1. **Key storage:** RSA-2048 keys are stored as PEM files in `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`
2. **Versioned filenames:** After rotation, keys use date-stamped filenames (e.g., `signing-key-2026-05-14.pem`)
3. **Multiple keys:** On startup, the server loads ALL keys from the directory — the most recent key signs new tokens, older keys are accepted for verification during the grace period
4. **Background service:** `OidcKeyRotationService` runs daily and checks if the newest key is older than the rotation interval

### Default Schedule

| Setting           | Default  | Description                                                 |
| ----------------- | -------- | ----------------------------------------------------------- |
| Rotation interval | 90 days  | How often a new key is generated                            |
| Key retention     | 120 days | How long old keys are kept (must exceed max token lifetime) |
| Check interval    | 24 hours | How often the background service checks                     |

### Configuration

Override defaults in `appsettings.json`:

```json
{
  "Auth": {
    "KeyRotation": {
      "RotationInterval": "90.00:00:00",
      "KeyRetentionPeriod": "120.00:00:00",
      "CheckInterval": "1.00:00:00"
    }
  }
}
```

### Manual Rotation

To force immediate rotation:

1. Delete the oldest key files from `oidc-keys/`
2. Restart the server — it will generate new keys automatically

### Emergency Key Revocation

In case of a suspected key compromise:

1. Delete or move the entire `oidc-keys/` directory
2. Restart the server — new keys will be generated
3. **All existing sessions will be invalidated** — users must re-login

---

## 12. Quick Checklist

### If You Suspect a Breach

1. **Isolate:** Take the server offline if possible
2. **Rotate secrets:** Change all database passwords, API keys, and OAuth client secrets
3. **Revoke tokens:** Clear the `oidc-keys/` directory and restart the server to force new signing keys
4. **Audit logs:** Review logs for unauthorized access patterns
5. **Update:** Apply the latest security patches

### Reporting a Vulnerability

See [VULNERABILITY_DISCLOSURE.md](./VULNERABILITY_DISCLOSURE.md).

---

## Quick Checklist

- [ ] TLS certificate from trusted CA installed
- [ ] `AllowInsecureTls` not set or set to `false`
- [ ] Database credentials from environment variable, not config file
- [ ] `AllowedHosts` configured with actual domain(s)
- [ ] CORS origins configured with actual domain(s)
- [ ] Rate limits reviewed and adjusted
- [ ] Logging level set to Warning for console
- [ ] File upload monitoring in place
- [ ] Regular backup schedule configured
- [ ] Security patches applied promptly
