# Client/Server Mediation Handoff

Last updated: 2026-03-08

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (22 resolved issues from initial sync milestone) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `8e02b52`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.

## Current Status

**Completed milestone:** End-to-end file sync with directory hierarchy (Issues #1–#22, all resolved).

Open issue: Sync Improvement Batch 1 Task 1.1b (server audit logging on mint22) — client side VALIDATED.

## Environment

| | Machine | Detail |
|---|---------|--------|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Architecture Decisions (Carry Forward)

- **Auth:** OpenIddict bearer on all files/sync endpoints via `FilesControllerBase` `[Authorize]`. Persistent RSA keys in `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`. `DisableAccessTokenEncryption()`.
- **API contract:** All endpoints use `GetAuthenticatedCaller()` (no `userId` query param). All return raw payloads — `ResponseEnvelopeMiddleware` wraps automatically. Client unwraps envelope via `ReadEnvelopeDataAsync<T>()`.
- **Sync flow:** changes → tree → reconcile → chunk manifest → chunk download → file assembly. `since` param converted to UTC kind. Client builds `nodeId→path` map from folder tree.
- **Token handling:** Client uses `DateTimeOffset` for expiry. `RefreshTokenAsync` sends `client_id`. `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```

## Active Handoff

### Issue #23: Batch 1 Task 1.1 - Sync Service Logging (Client only)

**Server-side status:** Not applicable (client-only task).
**Client-side status:** ✅ VALIDATED on `Windows11-TestDNC` at commit `c69aeac` (2026-03-08).

**Validation results from Windows11-TestDNC:**
- Commit: `c69aeac`
- Restore/build: no errors
- Log file: `%APPDATA%\DotNetCloud\logs\sync-service20260308.log` (3258 bytes, date suffix normal for `RollingInterval.Day`)
- JSON entries confirmed:
	- `DotNetCloud Sync Service starting.`
	- `Loading 1 persisted sync context(s).`
	- `Sync engine started for context ... (C:\Users\benk\Documents\synctray)`
	- `DotNetCloud Sync Service running — 1 context(s) active.`
	- `IPC server started (Named Pipe).`
	- Full graceful shutdown sequence logged
- **Task 1.1 (client): PASS**

---

### Issue #24: Batch 1 Task 1.1b - Sync Audit Logging (Server only)

**Server-side status:** ✅ COMPLETE — commit `c585dae` (2026-03-08).
**Client-side status:** Not applicable (server-only task).

---

### Issue #25: Batch 1 Task 1.2 - Request Correlation IDs (Client side)

**Server-side status:** ✅ COMPLETE — commit `16dd7df` (2026-03-08).
Server now has `RequestCorrelationMiddleware` that reads `X-Request-ID` from every incoming request (or generates one), sets it as `TraceIdentifier`, and echoes it back on the response.

**Client-side status:** Pending implementation on `Windows11-TestDNC`.

**What needs to happen on Windows11-TestDNC:**

Pull `main`, then implement a `DelegatingHandler` that attaches `X-Request-ID` to every outgoing API call.

1. Create `src/Clients/DotNetCloud.Client.Core/Api/CorrelationIdHandler.cs`:

```csharp
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// DelegatingHandler that attaches a unique X-Request-ID header to every outgoing HTTP request
/// and logs it so client and server logs can be correlated.
/// </summary>
public class CorrelationIdHandler : DelegatingHandler
{
    private readonly ILogger<CorrelationIdHandler> _logger;

    public CorrelationIdHandler(ILogger<CorrelationIdHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        request.Headers.TryAddWithoutValidation("X-Request-ID", requestId);
        _logger.LogInformation("API call {Method} {Url} RequestId={RequestId}",
            request.Method, request.RequestUri, requestId);

        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("API call failed. RequestId={RequestId}, Status={StatusCode}",
                requestId, (int)response.StatusCode);
        }

        return response;
    }
}
```

2. Register the handler on the `HttpClient` used by `DotNetCloudApiClient`. Find where `HttpClient` / `DotNetCloudApiClient` is registered in DI (likely `Program.cs` or a service registration extension). Add:

```csharp
services.AddTransient<CorrelationIdHandler>();
services.AddHttpClient<DotNetCloudApiClient>()
    .AddHttpMessageHandler<CorrelationIdHandler>();
```

If `DotNetCloudApiClient` constructs its own `HttpClient` rather than using DI, add the handler manually:
```csharp
var handler = new CorrelationIdHandler(logger);
handler.InnerHandler = new HttpClientHandler();
var httpClient = new HttpClient(handler);
```

3. Build with `dotnet build`. Run a sync pass. Confirm:
   - Client log shows lines like: `API call POST https://mint22:15443/api/v1/files/sync/reconcile RequestId=abc123...`
   - Failure lines show: `API call failed. RequestId=abc123..., Status=401`

**Request back from client agent:**
- commit hash
- sample log line showing `RequestId=` on an outgoing call
- confirmation build succeeded (0 errors)
