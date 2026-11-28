# Module Development Guide

> **Purpose:** Guide for building DotNetCloud modules
> **Audience:** First-party and third-party module developers
> **Status:** Current for manifest v2 / the module sandbox
> **Last Updated:** 2026-11-27 (adds the installed-SDK starting point, the development loop, publishing and the compatibility promise)

---

## Table of Contents

1. [Overview](#overview)
2. [Getting started](#getting-started)
3. [The three planes](#the-three-planes)
4. [Project layout](#project-layout)
5. [Module manifest (v2)](#module-manifest-v2)
6. [The host process](#the-host-process)
7. [Module lifecycle](#module-lifecycle)
8. [Core API: capabilities, consent, settings](#core-api-capabilities-consent-settings)
9. [Event plane](#event-plane)
10. [Data store](#data-store)
11. [gRPC communication](#grpc-communication)
12. [UI plane](#ui-plane)
13. [The development loop](#the-development-loop)
14. [Packaging and install](#packaging-and-install)
15. [Publishing to the store](#publishing-to-the-store)
16. [The compatibility promise](#the-compatibility-promise)
17. [Testing](#testing)
18. [Reference implementation](#reference-implementation)

---

## Overview

A DotNetCloud module is a **separate process** that the core launches, supervises and talks to over
one authenticated gRPC channel. It extends the platform without being linked into it: the core never
holds a `<ProjectReference>` to a module, never resolves its services, and never reads its database.

What the sandbox gives a module:

- **Its own process** with a CPU/memory/pids quota and a restart policy.
- **Its own private directories** — `<dataDir>/modules/<moduleId>/{config,data,run}` (0700) — and no
  access to the core's `config.json` or DataProtection key ring.
- **Its own database principal** when it declares `dataStore: "isolated"`: a generated schema the
  module migrates itself, reachable only through a scoped connection string the supervisor injects.
- **Its own UI**, served by its own process and framed by the core at
  `/apps/{moduleId}/` (an opaque-origin iframe with a message bridge).
- **No ambient authority.** It can only call the core APIs its manifest declares *and* an
  administrator grants, and it only reaches a user's personal data when that user has consented.

### Key principles

- **The manifest is the contract.** It is the only source of identity, capabilities, consent scopes,
  events, UI and resource requests. Anything not declared is not available.
- **Two independent approvals.** The admin grants capabilities once; each user consents to their
  own data per scope. Neither implies the other.
- **Identity is server-side.** The core mints a per-launch module credential and a short-lived,
  module-scoped user assertion. A module never asserts who the end user is.
- **Dogfood.** Bundled modules go through the same sandbox, manifest and API as a module installed
  from the store.
- **No module-to-module calls.** Anything shared goes through the core (an API, an event, or the
  search index).

### Where the detail lives

| Topic                                  | Document                                                        |
| -------------------------------------- | --------------------------------------------------------------- |
| Trust tiers, consent, transport, audit | [`../modules/MODULE_SECURITY.md`](../modules/MODULE_SECURITY.md) |
| Framing, bridge protocol, widgets      | [`../modules/MODULE_UI_HOSTING.md`](../modules/MODULE_UI_HOSTING.md) |
| `.dncpkg` format and signing           | [`../modules/MODULE_PACKAGING.md`](../modules/MODULE_PACKAGING.md) |
| Store client, installer, rollback      | [`../modules/MODULE_STORE.md`](../modules/MODULE_STORE.md)       |
| The design and its decisions           | [`../THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md`](../THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md) |

---

## Getting started

### The SDK is part of the platform

There is no feed to request access to and no credential to hold. Every release carries the module
contracts as a folder of `.nupkg` files beside the server:

| What            | Where                                   |
| --------------- | --------------------------------------- |
| Server and CLI  | `<install>/`                            |
| Module contracts | `<install>/sdk-feed/`                  |
| Module template | `<install>/templates/dotnetcloud-module/` |

Use the official release, and check it against the `.sha256` published next to the tarball. Treat the
feed as **build input**, not as a package cache: a `.nupkg` can carry MSBuild targets and analyzers
that run inside your build, so a mirrored or re-hosted feed is a different trust anchor from the
release you installed. Point `NuGet.config` at the folder and leave it where the platform put it.

`DotNetCloudSdkVersion` is therefore not a shared constant: it is the version of the platform you
are developing against, and it moves when you upgrade that platform.

### Scaffold from the template

```bash
dotnet new install <install>/templates/dotnetcloud-module
dotnet new dotnetcloud-module -n MyModule --sdkFeed <install>/sdk-feed
```

`--moduleId` sets the identity (`org.example.mymodule` by default): it is the directory name the
supervisor discovers, the host assembly name, and the `id` in `manifest.json`. Change all three
together or none. `--sdkVersion` defaults to the platform version the template shipped with.

The template writes the safe configuration as the default one rather than as advice:

- `NuGet.config` lists the SDK feed and nuget.org, and pins `DotNetCloud.*` to the SDK feed alone
  with `packageSourceMapping`, so a same-name package on a public feed can never win the resolve.
- `Directory.Packages.props` sets `DotNetCloudSdkVersion` once and every contract package follows it.
- `manifest.json` declares `dataStore: "none"`, so the skeleton needs no database, and a lifecycle
  service plus a health check answer the supervisor's calls.

The skeleton is deliberately smaller than the [Example module](#reference-implementation): add a data
store, events, capabilities or a framed UI from that reference as you need them.

### Build and test it

```bash
dotnet build        # restores the contracts from the SDK feed
dotnet publish -c Release
```

The publish output is already discovery-shaped: `<id>.dll`, `<id>.deps.json` and `manifest.json` in
one directory. That is what `module dev` deploys.

---

## The three planes

A module is easiest to reason about as three independent planes, each with its own boundary:

| Plane      | Runs in                        | Boundary                                                                                  |
| ---------- | ------------------------------ | ----------------------------------------------------------------------------------------- |
| **Process** | The module host (`dotnetcloud-module <id>`) | Authenticated gRPC on a Unix socket / Named Pipe (TCP fallback); per-call capability check |
| **Data**    | The module's own schema         | Scoped connection string + a principal that has no access to `core` or a sibling's schema |
| **UI**      | An iframe inside the core shell | Opaque-origin document served by the module and proxied at `/apps/{moduleId}/`            |

A problem in one plane cannot widen the others: the UI cannot call the gRPC plane directly (it goes
through the proxy, which the core authenticates), and the data plane cannot be reached from the
browser at all.

---

## Project layout

```
src/Modules/MyModule/
├── manifest.json                              # the contract (manifestVersion: 2)
├── LICENSE / README.md / CHANGELOG.md          # required by the packer
├── DotNetCloud.Modules.MyModule/               # domain logic, events, erasure handler
├── DotNetCloud.Modules.MyModule.Data/          # EF Core context, entities, migrations
├── DotNetCloud.Modules.MyModule.Data.SqlServer/# SQL Server mirror of the migrations
├── DotNetCloud.Modules.MyModule.Host/          # entry point: gRPC, static UI, self-migration
│   ├── Program.cs
│   ├── Protos/
│   ├── Services/
│   └── wwwroot/                                # the published UI lands here
├── DotNetCloud.Modules.MyModule.Web/           # Blazor WebAssembly UI (Razor components only)
└── DotNetCloud.Modules.MyModule.Web.Services/  # browser-side services (API client), unit-testable
```

| Project          | References                                                                                   |
| ---------------- | -------------------------------------------------------------------------------------------- |
| Core logic       | `DotNetCloud.Core` (the SDK: interfaces, DTOs, events)                                       |
| Data             | `DotNetCloud.Core.Data` (naming strategy, `ModuleSchemaInitializer`)                         |
| Host             | Core + Data + the SDK, plus its own `.Web` project (which publishes the UI into `wwwroot`)     |
| `.Web`           | `DotNetCloud.Core`, `DotNetCloud.UI.Shared`, `DotNetCloud.Modules.Sdk.Web`, `.Web.Services`   |
| `.Web.Services`  | `DotNetCloud.Modules.Sdk.Web` only — plain C#, no Razor, so it can be tested in a normal test project |

**The host must not reference another module, and the core must never reference the host.** If you
find yourself needing data another module owns, call the core API for it.

### The SDK

| Package / project                | What it gives you                                                                 |
| -------------------------------- | --------------------------------------------------------------------------------- |
| `DotNetCloud.Modules.Sdk`        | `IDncModuleApi` + `DncModuleApiClient` (the canonical core API), `ModuleEnvironment`, `ModuleManifestV2` / `ModuleManifestReader`, `InMemoryDncModuleApi` for tests |
| `DotNetCloud.Modules.Sdk.Web`    | `ModuleApiClient`, `ModuleFrameOptions`, `ModuleFrameBridge`, `ModuleStreamClient`, the frame JS |
| `DotNetCloud.Core.Grpc`          | Interceptors and helpers the host wires up: `ModuleGrpcEndpointBinding`, `ModulePeerAuthenticationInterceptor`, `AddModuleUserErasureHandler`, `AddModuleUserAssertionAccessor`, `UseModuleUserAssertion` |

Use the SDK's client rather than hand-rolling one: it carries the module credential, attaches the
ambient user assertion, and uses the same JSON options as the core.

---

## Module manifest (v2)

`manifest.json` sits at the root of the module (and inside the `.dncpkg`). It is parsed **strictly**
for v2: unknown members, comments and trailing commas are rejected rather than ignored, so a typo is
a load error instead of a silently missing capability.

```json
{
  "manifestVersion": 2,
  "id": "org.example.receipts",
  "name": "Receipt Scanner",
  "version": "1.0.0",
  "description": "Scans uploaded receipts and files them by month.",
  "publisher": { "id": "org.example", "name": "Example Org", "url": "https://example.org" },
  "moduleApiVersion": 1,
  "minCoreVersion": "0.7.0",
  "dataStore": "isolated",
  "handlesUserErasure": true,
  "capabilities": ["IUserDirectory", "IStorageProvider", "IAuditLogger"],
  "userConsentScopes": ["profile", "files:read"],
  "events": { "publishes": ["ReceiptScannedEvent"], "subscribes": [] },
  "ui": {
    "type": "iframe",
    "entryPath": "/",
    "navItems": [ { "id": "receipts", "label": "Receipts", "icon": "receipt_long", "path": "/" } ],
    "widget": { "path": "/widget/index.html" }
  },
  "settings": [
    { "key": "ocr.endpoint", "value": "", "description": "Optional OCR service URL." }
  ],
  "resources": { "memoryLimitMb": 256, "cpuPercent": 50, "databaseMb": 512, "connectionLimit": 10 }
}
```

### Field reference

| Field                        | Required | Rules                                                                                                                        |
| ---------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `manifestVersion`            | yes      | `2` for anything non-required. `1` is only accepted for legacy bundled modules and is not a target for new work.               |
| `id`                         | yes      | Lowercase reverse-DNS, dot-separated, and it must match the module's directory name.                                           |
| `name`, `version`            | yes      | Display name; semantic version.                                                                                               |
| `publisher`                  | for v2   | `id` is required when the block is present; `url` must be absolute `https:`.                                                   |
| `moduleApiVersion`           | no       | Only `1` exists today; any other value is rejected.                                                                            |
| `minCoreVersion`             | yes      | Enforced at start; an incompatible module is recorded as `Failed` with the reason instead of being spawned.                     |
| `maxCoreVersion`             | no       | Upper compatibility bound.                                                                                                     |
| `dataStore`                  | yes\*    | `"isolated"` (own schema + principal) or `"none"` (settings/files only). \*Required modules must **not** declare it.           |
| `handlesUserErasure`         | yes\*    | Must be `true` when `dataStore != "none"` or you declare consent scopes. \*Otherwise optional.                                 |
| `capabilities`               | no       | Capability interface names the module calls. Nothing is granted automatically at store tier.                                   |
| `userConsentScopes`          | no       | Names from `ModuleScopes.ConsentScopes` only. Declaring one enables the F18 rule below.                                        |
| `events.publishes`           | no       | Event type names. Publishing anything undeclared is rejected and audited.                                                      |
| `events.subscribes`          | no       | Event type names you want delivered.                                                                                          |
| `events.realtime`            | no       | SignalR broadcast names you may raise.                                                                                        |
| `ui`                         | no       | `type` must be `"iframe"`; `entryPath` and every `path` start with `/`; `icon` is a Material icon name (`[a-z0-9_]`).          |
| `settings`                   | no       | Defaults seeded once on install, and never over an operator-set value. `secret: true` is **rejected** — a packaged default is public. |
| `resources`                  | no       | Memory / CPU / data-dir / database / connection requests. Every value must be positive; the admin policy caps them.            |

### Validation rules that catch real mistakes

- **Consent coverage (F18).** Once you declare any `userConsentScopes`, every capability that reaches
  personal data must be covered: `IUserDirectory`/`ICurrentUserContext` → `profile`,
  `IStorageProvider` → `files:read`, `INotificationService` → `notifications:send`. Otherwise the user
  could be handing over data through a scope nobody approved.
- **Erasure contract.** A module that stores user data must implement `EraseUserData`; declaring
  `handlesUserErasure: true` without an `AddModuleUserErasureHandler<T>()` registration fails the
  account-deletion flow at runtime, not at install time — register it.
- **Required modules are core parts.** They may not declare `dataStore` or `ui`; their pages are core
  components and their tables live in the `core` schema.
- **Unknown enum-ish strings are rejected.** An unknown consent scope, an unknown `dataStore` value,
  or an unsupported `moduleApiVersion` fails validation with a message naming the field.

### Naming conventions

- **Module id:** reverse-DNS, lowercase (`dotnetcloud.tracks`, `org.example.receipts`). This is the
  identity the credential, the schema, the directories and the store key are all derived from.
- **Version:** semantic versioning. A package version must match the manifest version.
- **Event types:** suffixed `Event` (`ReceiptScannedEvent`).

---

## The host process

The supervisor launches the host with a minimal, secret-free environment. Your `Program.cs` is
responsible for wiring exactly these things:

| Env var                                    | Meaning                                                                 |
| ------------------------------------------ | ----------------------------------------------------------------------- |
| `DOTNETCLOUD_MODULE_ID`                    | Your verified module id. Never take it from a request.                  |
| `DOTNETCLOUD_GRPC_ENDPOINT`                | The endpoint your host must bind (Unix socket, Named Pipe or TCP).      |
| `DOTNETCLOUD_CORE_ENDPOINT`                | Where to dial the core's gRPC API.                                      |
| `DOTNETCLOUD_CORE_REST_ENDPOINT`           | Core REST facade base URL, when available.                              |
| `DOTNETCLOUD_MODULE_TOKEN`                 | Per-launch credential. The SDK attaches it to every core call.          |
| `DOTNETCLOUD_MODULE_CONFIG_DIR`            | `<dataDir>/modules/<id>/config` — holds the non-secret `module.json`.   |
| `DOTNETCLOUD_MODULE_DATA_DIR`              | `<dataDir>/modules/<id>/data` — your private writable directory.        |
| `DOTNETCLOUD_MODULE_DB_CONNECTION_STRING`  | Scoped connection string, only when `dataStore: "isolated"`.            |
| `DOTNETCLOUD_ASSERTION_PUBLIC_KEY`         | Public key for verifying core-signed user assertions.                   |

A sandboxed host never receives `DOTNETCLOUD_CONFIG_DIR` or `DOTNETCLOUD_DATA_DIR`. If you read
those, you are looking at a legacy host that has not been converted.

```csharp
public static async Task Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // 1. Bind the endpoint the supervisor assigned — without this the core cannot reach you.
    ModuleGrpcEndpointBinding.ConfigureModuleGrpcEndpoint(builder);

    // 2. Your own DataProtection ring, under your own data directory.
    var dataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DATA_DIR")!;
    var keys = Path.Combine(dataDir, "data-protection-keys");
    Directory.CreateDirectory(keys);
    builder.Services.AddDataProtection()
        .SetApplicationName("DotNetCloud.Modules.MyModule")
        .PersistKeysToFileSystem(new DirectoryInfo(keys));

    // 3. Non-secret topology (databaseProvider, databaseSchema) comes from module.json.
    var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_CONFIG_DIR");
    if (!string.IsNullOrEmpty(configDir))
        builder.Configuration.AddJsonFile(Path.Combine(configDir, "module.json"), optional: false);

    // 4. The SDK contract for the supervisor-provided values.
    ModuleEnvironment.TryFromEnvironment(out var environment);

    // 5. One client, one credential, the canonical API surface.
    builder.Services.AddSingleton<IDncModuleApi>(
        environment is null ? new InMemoryDncModuleApi() : new DncModuleApiClient(environment));

    // 6. gRPC with the core-facing peer-authentication interceptor.
    builder.Services.AddGrpc(o => o.Interceptors.Add<ModulePeerAuthenticationInterceptor>());
    builder.Services.AddHealthChecks().AddCheck<MyHealthCheck>("mymodule");

    // 7. The erasure contract and the ambient user assertion.
    builder.Services.AddModuleUserErasureHandler<MyUserErasureHandler>();
    builder.Services.AddModuleUserAssertionAccessor();

    var app = builder.Build();
    app.UseModuleUserAssertion();

    // 8. Serve your own UI (the core frames /apps/{id}/ and proxies here).
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // 9. Self-migrate: `dataStore: "isolated"` means the schema is yours.
    //    Retry with backoff and keep running degraded if the database is briefly gone.
    await MyDbInitializer.RunAsync(app.Services);

    app.MapGrpcService<MyGrpcService>();
    app.MapGrpcService<MyLifecycleService>();
    app.MapHealthChecks("/health");
    await app.RunAsync();
}
```

Two details that are easy to get wrong:

- **Never name the schema in your migrations.** The provisioned connection string pins `search_path`
  to your generated schema, so unqualified DDL — including `__EFMigrationsHistory` — lands there.
  Naming it requires `CREATE` on the database, which your principal deliberately does not have.
- **`/` belongs to your UI document.** Expose machine-readable status on a distinct path such as
  `/api/info`, or it will be shadowed by `index.html`.

---

## Module lifecycle

Your module's own class implements `IModuleLifecycle`. The **core** drives it: the supervisor spawns
the host, then calls `Initialize` → `Start` on the lifecycle gRPC service, and `Stop` before the
process is terminated. A host that was never launched by the supervisor still runs (so you can debug
with `dotnet run`), it just never receives those calls.

```
[Discovery] → [Spawn] → [Initialize] → [Start] → [Running] → [Stop] → [Exit]
```

### Interface

```csharp
public interface IModule
{
    IModuleManifest Manifest { get; }
    Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public interface IModuleLifecycle : IModule, IAsyncDisposable
{
    new Task DisposeAsync();
}
```

Implement the base the SDK ships (`TrackedModuleLifecycleBase` in `DotNetCloud.Core.Grpc`) so the
`lifecycle.proto` surface, idempotency and the failure reporting the supervisor expects are handled
for you; override `Initialize`/`Start`/`Stop` rather than hand-rolling the gRPC service.

### Lifecycle guarantees

| Phase             | Called                                    | Purpose                                              |
| ----------------- | ----------------------------------------- | ---------------------------------------------------- |
| `InitializeAsync` | Once, before `StartAsync`                 | Build your services, read settings, subscribe locally |
| `StartAsync`      | Once, after `InitializeAsync` succeeded   | Begin accepting work; fail here to be restarted        |
| `StopAsync`       | Once, on shutdown or uninstall            | Drain work, close connections                          |
| `DisposeAsync`    | Once, after `StopAsync`                   | Release resources                                      |

If `InitializeAsync` throws, the core records `Failed` with your reason and applies the restart
policy; it does not quietly leave you `Running`.

### Initialization context

```csharp
public record ModuleInitializationContext(
    string ModuleId,
    IServiceProvider Services,
    IReadOnlyDictionary<string, object> Configuration,
    CallerContext SystemCaller
);
```

**Usage:**

```csharp
public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
{
    var logger = context.Services.GetService<ILogger<MyModule>>();

    // Your own process's DI: your DbContext, your services, your local event bus.
    var eventBus = context.Services.GetRequiredService<IEventBus>();

    // Settings come from the core settings plane, not from a config file.
    var endpoint = await _api.GetSettingAsync("ocr.endpoint", cancellationToken);

    // There is no ambient capability object to resolve from DI: every core capability is a call
    // on IDncModuleApi, and a denied capability throws — degrade explicitly.
    try
    {
        await _api.LogAuditAsync(new DncAuditEntry { Action = "started" }, cancellationToken);
    }
    catch (ModuleApiException ex) when (ex.ErrorCode == ErrorCodes.CapabilityNotGranted)
    {
        logger?.LogWarning("IAuditLogger not granted; continuing without audit");
    }

    await eventBus.SubscribeAsync(_localHandler, cancellationToken);
}
```

> **`context.Services.GetService<IStorageProvider>()` is the old in-process model.** A sandboxed
> module has no core services in its container — it has an `IDncModuleApi`. If you are porting a
> module, that is the line to rewrite first.

---

## Core API: capabilities, consent, settings

Everything a module may do on the core goes through one interface, `IDncModuleApi`, implemented by
`DncModuleApiClient` over gRPC with your per-launch credential.

| Area          | Calls                                                                                              |
| ------------- | -------------------------------------------------------------------------------------------------- |
| Identity      | `GetCurrentUserAsync`, `GetUserAsync`, `SearchUsersAsync`                                          |
| Notifications | `SendNotificationAsync`                                                                            |
| Events        | `PublishEventAsync`                                                                                |
| Search        | `SubmitSearchIndexAsync`                                                                           |
| Settings      | `GetSettingAsync`, `SetSettingAsync`                                                               |
| Audit         | `LogAuditAsync`                                                                                    |
| Files         | `ListFilesAsync`, `DownloadFileAsync`, `UploadFileAsync` (consent-checked)                          |

Each call is authorized twice: the **capability** must be granted by an administrator, and any call
that touches one user's data additionally needs that user's **consent** for the matching scope. See
[`../modules/MODULE_SECURITY.md`](../modules/MODULE_SECURITY.md) for the full matrix.

### Capability tiers

The tier describes how much damage a wrongly-granted capability could do, which is what drives the
approval step in the admin UI:

| Tier           | Approval                 | Examples                                                                                 |
| -------------- | ------------------------ | ---------------------------------------------------------------------------------------- |
| **Public**     | Automatic                | `IUserDirectory`, `ICurrentUserContext`, `INotificationService`. `IEventBus`             |
| **Restricted** | Admin approval           | `IStorageProvider`, `IModuleSettings`, `ITeamDirectory`, `IFileDirectory`, `IFileWriter` |
| **Privileged** | Admin approval + warning | `IUserManager`, `IBackupProvider`                                                        |
| **Forbidden**  | Never granted            | Direct database access, process management                                               |

A capability name is only a *name* in the manifest. The call itself is still gated per RPC on the
core side, so an ungranted capability fails at the boundary rather than being absent from DI.

**Trust tier changes the default.** A bundled module's declared capabilities are auto-granted when it
is first discovered; a **store-tier** package never is — its capabilities start `Pending` and need an
explicit operator grant. A capped grant that the store policy narrows is re-reviewed on upgrade.

### The user's files

A module that needs the user's real files does **not** call the Files module. It calls the core, which
brokers the request and enforces the user's consent:

| Capability       | Scope         | RPCs (via `IDncModuleApi`)            |
| ---------------- | ------------- | ------------------------------------- |
| `IFileDirectory` | `files:read`  | `ListFilesAsync`, `DownloadFileAsync` |
| `IFileWriter`    | `files:write` | `UploadFileAsync`                     |

Both are **Restricted** and per-user consent is required on top of the grant. Declare the capability
and the scope in the manifest:

```json
"capabilities": ["IFileDirectory", "IFileWriter"],
"userConsentScopes": ["files:read", "files:write"]
```

A call without the user's consent fails with `MODULE_CONSENT_REQUIRED` (REST: `403` plus
`details.consentScopes`); ask for consent (`bridge.requestConsent([...])`) and retry. The acting user
comes from the verified assertion — a module never passes a user id, and there is no module-scope
form. Saves are capped at 100 MiB, listings at 500 entries per page, and the file name is reduced to
a single sanitized path segment by the core.

### Requesting capabilities

Declare them in `manifest.json` — that file is what the core reads, what the installer shows the
operator, and what the admin grants against:

```json
"capabilities": ["INotificationService", "IStorageProvider"],
"userConsentScopes": ["files:read"]
```

Keep the C# `IModuleManifest` mirror in sync if you implement `IModule.Manifest`. It is what
`IModule.Manifest` returns to lifecycle callers; it is **not** what authorizes anything, so a drift
between the two is a silent bug (the JSON wins).

### Handling a denied capability

A call for an ungranted capability throws — nothing is injected as `null`:

```csharp
using var stream = await _api.DownloadFileAsync(nodeId, cancellationToken);
    // ModuleApiException with error code CAP_NOT_GRANTED, or
    // MODULE_CONSENT_REQUIRED (with the missing scopes) when the user has not consented.
```

Handle both explicitly, and only gate on consent when a user is actually acting:

```csharp
catch (ModuleApiException ex) when (ex.ErrorCode == ErrorCodes.CapabilityNotGranted)
{
    _fileFeaturesEnabled = false;   // admin has not granted IStorageProvider
}
catch (ModuleConsentRequiredException ex) when (ex.Scopes.Contains(ModuleScopes.FilesRead))
{
    _needsFilesConsent = true;      // the user must approve files:read in the UI
}
```

### Granting capabilities

Operators grant through the admin UI (`/admin/modules`) or the admin API — there is no `dotnetcloud
module grant` CLI command:

```
GET    /api/v1/core/admin/modules/{moduleId}/capabilities
POST   /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}/grant
POST   /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}/reject
DELETE /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}
```

Every grant and revocation is audited and invalidates the 60-second grant cache, so a new grant takes
effect without restarting the module.

### Settings

Per-module settings are the replacement for the shared `config.json`. Read and write them through the
API, keyed per module (the core ignores any module id in your request — you are identified by your
credential):

```csharp
var endpoint = await _api.GetSettingAsync("ocr.endpoint", cancellationToken);
await _api.SetSettingAsync("ocr.endpoint", "https://ocr.example.org", cancellationToken);
```

Defaults declared in the manifest's `settings` array are seeded on first install and never overwrite
a value an operator has already set. Keys are 1–128 characters matching `[A-Za-z0-9][A-Za-z0-9._:-]*`.

### Account erasure

If you store user data (`dataStore != "none"` or you declare consent scopes) the manifest must say
`handlesUserErasure: true`, and the host must register a handler:

```csharp
builder.Services.AddModuleUserErasureHandler<MyUserErasureHandler>();
```

The core calls `EraseUserData` when an account is deleted and retries; a module that fails erasure is
reported to the operator rather than silently skipped. Delete every row you stored for that user,
including soft-deleted ones, and delete the consent rows you own.

---

## Event plane

The event bus is **two things** and they must not be confused:

| Bus                                        | Scope               | What it does                                                                |
| ------------------------------------------ | ------------------- | --------------------------------------------------------------------------- |
| `IDncModuleApi.PublishEventAsync`          | Core (cross-module) | Publishes an event type your manifest declares. The core fans it out.       |
| `IEventBus` in your host                    | Your process only   | Local fan-out between your components, and the hook the SDK bridges use     |

The `IEventBus` the host registers (`InProcessEventBus`) never crosses the process boundary. If you
need another module to react, publish to the core.

> **Declared subscriptions are not delivered to module hosts yet.** `events.subscribes` is validated
> and recorded, but there is no core→module delivery channel, so no first-party module declares one.
> Do not build a feature on it — publish an event instead, or call the API that owns the data.

### Defining Events

Events implement the `IEvent` marker interface:

```csharp
public record ItemCreatedEvent(Guid ItemId, string Title, Guid CreatedByUserId) : IEvent;
public record ItemDeletedEvent(Guid ItemId, Guid DeletedByUserId) : IEvent;
```

### Publishing Events

Publish through the core API, with the acting user's assertion attached automatically:

```csharp
await _api.PublishEventAsync(new DncEvent
{
    EventType = nameof(ItemCreatedEvent),
    PayloadJson = JsonSerializer.Serialize(new ItemCreatedEvent(item.Id, item.Title, userId)),
}, cancellationToken);
```

The event type must be declared in `events.publishes`. An undeclared type is rejected with
`MODULE_EVENT_REJECTED` and audited, so this fails loudly rather than silently broadcasting
something nobody approved.

### Subscribing within your own process

Create an event handler and register it with the **local** bus:

```csharp
public sealed class ItemCreatedEventHandler : IEventHandler<ItemCreatedEvent>
{
    private readonly ILogger<ItemCreatedEventHandler> _logger;

    public ItemCreatedEventHandler(ILogger<ItemCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(ItemCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Item created: {ItemId} - {Title}", @event.ItemId, @event.Title);
        return Task.CompletedTask;
    }
}
```

Register during initialization:

```csharp
await eventBus.SubscribeAsync(_itemCreatedHandler, cancellationToken);
```

Unsubscribe during stop:

```csharp
await eventBus.UnsubscribeAsync(_itemCreatedHandler, cancellationToken);
```

---

## Data store

`dataStore` in the manifest picks one of two models:

| Value        | The core does                                               | You do                                                        |
| ------------ | ----------------------------------------------------------- | ------------------------------------------------------------- |
| `isolated`   | Creates a schema and a database principal; injects a scoped connection string | Migrate and query your own schema; own your data lifecycle    |
| `none`       | Nothing                                                     | Persist through module settings (`IDncModuleApi`) and files    |

### What you get

On install the core provisions `<generated-schema>` and a login principal that owns it, then injects
`DOTNETCLOUD_MODULE_DB_CONNECTION_STRING`. That connection string pins `search_path` to your schema
(PostgreSQL) or defaults you into it (SQL Server), which is why your migrations must not name a schema:

```csharp
public class MyModuleDbContext : DbContext
{
    public DbSet<MyItem> Items => Set<MyItem>();

    public MyModuleDbContext(DbContextOptions<MyModuleDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Unqualified table names: the connection string decides the schema. Naming "mymodule"
        // here would need CREATE on the database, which the module principal does not have.
        modelBuilder.Entity<MyItem>(entity =>
        {
            entity.ToTable("Items");
            entity.HasKey(e => e.Id);
        });
    }
}
```

### Migrating yourself

Call `ModuleSchemaInitializer` (or `Database.MigrateAsync`) at startup, **with retries**: a database
that is briefly unavailable must not crash-loop the module. Keep the module running in a degraded
state and let the next start retry.

Ship migrations for both engines: `DotNetCloud.Modules.MyModule.Data` (PostgreSQL) and
`DotNetCloud.Modules.MyModule.Data.SqlServer`, exactly as the bundled modules do.

### What you do not get

- No access to `core` or to another module's schema — the principal is denied (`42501`).
- No `create database`, no schema creation, no role management.
- No connection beyond your quota's `CONNECTION LIMIT`.

---

## gRPC Communication

> **MANDATORY: gRPC is the only inter-process mechanism.** No in-process calls, no shared DI
> container, no cross-module database access. A module never calls another module.

Every module **MUST** have:

- A `manifest.json` at the module root declaring identity, capabilities, consent, events and UI.
- A `Program.cs` in the host that binds the supervisor-assigned endpoint
  (`ModuleGrpcEndpointBinding.ConfigureModuleGrpcEndpoint`) and reads the `DOTNETCLOUD_MODULE_*`
  environment — not `DOTNETCLOUD_CONFIG_DIR`.
- A `.proto` file under `Host/Protos/` for the RPC surface your own UI needs.
- A gRPC service class implementing the generated base, plus a lifecycle service derived from
  `TrackedModuleLifecycleBase` so the core can drive `Initialize`/`Start`/`Stop`.
- A `/health` endpoint that reports `Serving` once the module can do useful work; the supervisor
  restarts a module that fails consecutive probes.
- **No** `<ProjectReference>` from `DotNetCloud.Core.Server` to the host, and no entry in the core's
  schema-provider or UI-registration tables. (Converted modules drop out of both.)

Note there is no `IXxxApiClient` in `Core.Server/Grpc/Clients/` any more. That pattern belonged to
modules whose services the core hosted in-process. A sandboxed module owns its API; the core reaches
it through the proxied REST/UI path and the lifecycle/health RPCs.

### Defining a service

Create a `.proto` file:

```protobuf
syntax = "proto3";

package dotnetcloud.modules.mymodule;

service MyModuleService {
  rpc GetItem (GetItemRequest) returns (GetItemResponse);
  rpc CreateItem (CreateItemRequest) returns (CreateItemResponse);
}

message GetItemRequest {
  string item_id = 1;
}

message GetItemResponse {
  string item_id = 1;
  string title = 2;
  string content = 3;
}
```

### Health check

Expose the standard gRPC health service so the supervisor can probe you, and map an HTTP health
endpoint for the container/orchestrator path:

```csharp
public class HealthServiceImpl : Health.HealthBase
{
    public override Task<HealthCheckResponse> Check(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new HealthCheckResponse
        {
            // Report NOT_SERVING until your database is reachable; the supervisor uses this to
            // decide whether to restart you.
            Status = HealthCheckResponse.Types.ServingStatus.Serving
        });
    }
}
```

---

## UI plane

A module's UI is **its own** web app, served by its own process, and framed by the core shell. The
core does not load your components into its Blazor tree.

```
Browser → https://<host>/apps/dotnetcloud.mymodule/anything
        → core proxy (authenticates, strips cookies, injects the user assertion)
        → http://<module endpoint>/anything
```

### Project shape

- `DotNetCloud.Modules.MyModule.Web` — a Blazor WebAssembly project (`Microsoft.NET.Sdk.BlazorWebAssembly`).
- `DotNetCloud.Modules.MyModule.Web.Services` — plain C# **API client** so it can be unit tested
  without a browser (the `TracksApi` / `ExampleApi` pattern).
- `wwwroot/index.html` — loads the bridge **before** Blazor so every request carries the frame
  ticket:

```html
<base href="./" />
<script src="js/dncModuleBridge.js"></script>
<script src="js/dncModuleFrame.js"></script>
<script src="_framework/blazor.webassembly.js" autostart="false"></script>
<script src="js/dncModuleBoot.js"></script>
```

The three JS files are linked from the SDK, never copied — a conversion that vendors them will drift.
`autostart="false"` is required: an opaque-origin frame cannot resolve the loader's relative
`import("./dotnet.js")`, so `dncModuleBoot.js` starts Blazor with absolute boot URLs.

The host `ProjectReference`s the `.Web` project, so `dotnet publish` puts the compiled UI into the
host's `wwwroot` and `app.UseStaticFiles()` serves it. There is no `UseBlazorFrameworkFiles` here —
that maps a *client* project from a *server* project, which is not this shape.

### Navigation and widgets

Both come from the manifest's `ui` block; there is no registration call in the core:

```json
"ui": {
  "type": "iframe",
  "entryPath": "/",
  "navItems": [ { "id": "mymodule", "label": "My Module", "icon": "receipt_long", "path": "/" } ],
  "widget": { "path": "/widget/index.html" }
}
```

Icons are Material icon names (see `MaterialIcon`); an unknown name fails manifest validation rather
than rendering blank.

### What the frame can and cannot do

The frame is `sandbox="allow-scripts allow-forms allow-popups allow-modals"`, so it has an **opaque
origin**:

- ❌ No cookies, no `localStorage` shared with the core, no same-origin fetch to core routes.
- ❌ No `window.top` access; a link opened in a new tab inherits the opaque origin and cannot carry
  the session — so do not build "open in new tab" links to core pages.
- ✅ All API calls through the bridge (`bridge.api(...)`), which attaches the frame ticket; the proxy
  adds the user assertion on the module's behalf.
- ✅ `bridge.mediaUrl(url)` for subresources the browser fetches itself (images, audio, video).
- ✅ `bridge.requestConsent([...])` when the core answers `MODULE_CONSENT_REQUIRED`.

The full protocol, theme handling and the widget role are in
[`../modules/MODULE_UI_HOSTING.md`](../modules/MODULE_UI_HOSTING.md).

---

## Packaging and install

A distributable module is a `.dncpkg`: a deterministic zip with `manifest.json`, your published
binaries per RID under `files/<rid>/`, `LICENSE`, `README.md`, `CHANGELOG.md`, and a detached
signature. Pack and verify with the CLI's `module` verbs:

```bash
dotnetcloud module keygen --key-id org.example-2026 --output mykey.json
dotnetcloud module pack --project src/Modules/MyModule/DotNetCloud.Modules.MyModule.Host \
                        --rid linux-x64 --rid win-x64 --sign-key mykey.json
dotnetcloud module verify MyModule-1.0.0.dncpkg --key org.example-2026=<public-key>
dotnetcloud module inspect MyModule-1.0.0.dncpkg
```

Install with the CLI (`--file` for a local package, `--from-feed` for a directory of packages,
`--from-store` for the configured catalogue):

```bash
dotnetcloud module install --file MyModule-1.0.0.dncpkg --allow-unverified --yes
dotnetcloud module info org.example.mymodule       # trust tier, sandbox state, receipt
dotnetcloud module rollback org.example.mymodule    # back to the retained version
```

What to expect on install:

- The package is verified (hash, signature, archive safety) and you are shown its capabilities and
  consent scopes **before** anything is written; `--yes`/the admin review acknowledges them by hash.
- The data store is provisioned **before** the binary swap, so a failure never leaves new binaries on
  a missing schema.
- Store-tier capabilities start `Pending`; grant them, then start the module.
- A package whose manifest does not validate is refused — the installer runs the core's own loader,
  so a package can never be installable but unstartable.

Details: [`../modules/MODULE_PACKAGING.md`](../modules/MODULE_PACKAGING.md) (format, signing,
hardening) and [`../modules/MODULE_STORE.md`](../modules/MODULE_STORE.md) (state machine, rollback,
remote index, air-gapped installs).

---

## The development loop

A module is a separate process, so building it does not put it in front of a user. With a DotNetCloud
server running in Development from the same shell:

```bash
dotnetcloud module dev <module-root>            # publish, deploy, health-check once
dotnetcloud module dev <module-root> --watch    # again on every save
```

One iteration:

1. `dotnet publish` into `<modules>/.dev/<id>/staging/`, never over the live directory.
2. The module is stopped through its lifecycle state, the supervisor stops the process, the binaries
   are swapped, the module is enabled again and the command waits for it to report healthy.
3. A build whose health check fails is rolled back to the previous dev build automatically.

Things that matter in that loop:

- **Development only, and audited.** The command refuses unless the server's environment is
  Development. It deploys unsigned code while keeping the module's identity — schema, data directory,
  Data Protection keys and settings all survive — and writes an audit entry per deploy, naming the
  source path and the content hash. `dotnetcloud module info` shows the deployed build as local.
- **The first run installs.** A module that is not installed yet is installed from the build's
  `manifest.json` (validated by the core's own loader), with an isolated schema when it declares
  `dataStore: "isolated"`, and a trust tier of `Bundled` — nothing was verified, so it never claims
  the store tier. A failed first install removes the row again.
- **Required modules are refused.** Their code runs in the core, so there is no process to stop and
  nothing to swap.
- **Migrations are guarded.** A build that carries a *new* migration is refused without
  `--allow-schema-change`. An edit to an existing migration, or a model change with no migration, is
  not caught, so the backup step is not optional.
- **`--restore`** promotes the previous dev build and exits; **`--no-publish`** redeploys the existing
  build output.
- **`module rollback` never sees a dev build.** Dev builds live under `<modules>/.dev/<id>/previous/`;
  the release `.previous/` slot stays reserved for the installer's own rollback.

### Debugging a module

- **Under the supervisor.** The supervisor runs `dotnet <modules>/<id>/<id>.dll`. Attach the debugger
  to that `dotnet` process by its command line. A rebuild with `--watch` swaps the binaries and
  detaches the debugger by design; use `--no-publish` when you want the running build held still.
- **Without the supervisor.** `dotnet run --project DotNetCloud.Modules.MyModule.Host` starts the
  host with `InMemoryDncModuleApi`, so module code can be stepped through with no platform running
  and with canned answers from the core API.
- **Logs.** Module stdout and stderr go to the server's log sink, not to the CLI. A failed health
  check names the server log instead of printing a tail.

---

## Publishing to the store

The store is the distribution and update path: it holds the catalogue, the review and the signed
index. A module published there is fetched and installed by `dotnetcloud module install <id>
--from-store`, and updated the same way.

What a submission carries:

| Item          | Notes                                                                                       |
| ------------- | ------------------------------------------------------------------------------------------- |
| `.dncpkg`     | `dotnetcloud module pack`, signed with **your** publisher key                               |
| `.dncpkg.sig` | the detached signature, uploaded beside the package                                          |
| source archive | a `.zip` of the source; **required** for the official trust tier                            |
| description   | Markdown, rendered by the store; the first submission must have one                        |
| release notes | required for every update after the first                                                    |

Before the first upload, register the publisher and prove ownership of its DNS name or GitHub
organisation in the store dashboard; the id you pack under (`org.example-2026` above) must belong to
that publisher. The process, the policy and the review checklist live in the store repository
(`docs/SUBMISSION.md`, `policy/STORE_POLICY.md`, `policy/REVIEW_CHECKLIST.md`) and on
`https://modules.dotnetcloud.net`.

The review approves a **hash**, not a version: the package bytes you uploaded are the bytes that are
signed by CI and served from the catalogue, so a fix is a new version and the old one is yanked.
Capabilities and consent scopes are reviewed against what the package actually uses, and the
architecture rules (own schema, no sibling database, no peer module calls) are scanned as well.

A vulnerability in a **published** module is handled by the store, not by this repository: see
[`../../SECURITY.md`](../../SECURITY.md) and the store's `policy/REVOCATION_RUNBOOK.md`.

---

## The compatibility promise

The promise is short and it is the contract on which a third-party module can depend:

- `moduleApiVersion` has **one** supported value (`1`). The loader refuses anything else, so a bump
  is a delivery event, not a flag an author can opt into.
- **Within a `moduleApiVersion`, the seven contract packages change additively only**: new members,
  no signature change and no semantic change to an existing member. A member is removed or changed
  only by moving the constant.
- **Anything outside those seven packages is not contract.** `DotNetCloud.Core.Server`, the supervisor
  and internals stay off-limits — the package-reference rule already enforces that, because a module
  only ever sees what it references from the feed.
- **An author who needs a newer contract raises `minCoreVersion`.** The core enforces that on
  install and refuses a module built for a later platform; it is the only knob an author needs.

Bumping `moduleApiVersion` is therefore one planned change across everything: the constant, the
`moduleApiVersion` in every first-party manifest, a repack of the six optional modules, a catalogue
publishing round and release notes. That is why it does not happen casually.

---

## Testing

### Test project structure

```
tests/DotNetCloud.Modules.MyModule.Tests/
├── MyModuleTests.cs                 # domain logic, lifecycle transitions
├── Web/MyApiClientTests.cs          # the browser-side client, exercised with a fake HttpMessageHandler
├── Events/MyEventHandlerTests.cs
└── Erasure/MyUserErasureHandlerTests.cs
```

Keep the API client in `.Web.Services` precisely so the UI can be tested without a browser — the
converted modules all follow this split.

### Key test areas

1. **Domain logic** — CRUD, validation, quota/limit edges.
2. **Lifecycle** — Initialize → Start → Stop → Dispose, including the failure path (Start throwing).
3. **API client** — request shape, enum/JSON handling against **the same** `JsonSerializerOptions` the
   host uses (`AddModuleJsonOptions()`). A client and host that disagree on enum encoding is the
   defect class that has bitten this codebase most often.
4. **Erasure** — `EraseUserData` leaves zero rows for that user and removes consent rows.
5. **Denied paths** — `CAP_NOT_GRANTED` and `MODULE_CONSENT_REQUIRED` are handled, not
   swallowed.

Test naming: `MethodName_Condition_ExpectedResult`, arrange-act-assert.

### Example test

```csharp
[TestClass]
public class MyModuleManifestTests
{
    [TestMethod]
    public void Id_ReturnsExpectedValue()
    {
        var manifest = new MyModuleManifest();
        Assert.AreEqual("dotnetcloud.mymodule", manifest.Id);
    }

    [TestMethod]
    public void RequiredCapabilities_ContainsExpectedCapabilities()
    {
        var manifest = new MyModuleManifest();
        CollectionAssert.Contains(
            manifest.RequiredCapabilities.ToList(),
            nameof(INotificationService));
    }
}
```

---

## Reference implementation

The **Example module** is the canonical reference: the smallest module that is fully sandboxed
(manifest v2, `dataStore: "isolated"`, self-migration, own UI, erasure handler) and small enough to
read end to end.

| Area                    | Where                                                                        |
| ----------------------- | ---------------------------------------------------------------------------- |
| Manifest v2             | `src/Modules/Example/manifest.json`                                          |
| Domain + lifecycle      | `src/Modules/Example/DotNetCloud.Modules.Example/`                           |
| DbContext + migrations  | `src/Modules/Example/DotNetCloud.Modules.Example.Data{,.SqlServer}/`         |
| Host wiring             | `src/Modules/Example/DotNetCloud.Modules.Example.Host/Program.cs`            |
| UI (Blazor WASM)        | `src/Modules/Example/DotNetCloud.Modules.Example.Web/`                       |
| UI API client           | `src/Modules/Example/DotNetCloud.Modules.Example.Web.Services/`              |
| Tests                   | `tests/DotNetCloud.Modules.Example.Tests/`                                   |

`Tracks` (`src/Modules/Tracks/`) is the reference for the *other* end of the range: a large module
with a rich framed UI, 47 request helpers sharing one `TracksApi.JsonOptions`, a widget, and a
`.dncpkg` published to the store. If you are converting an existing module rather than writing a new
one, read `docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md` §7 for the conversion checklist and the
six modules that have already been through it.

---

**See also:**

- [Core Abstractions](../architecture/core-abstractions.md) — capability and event model reference
- [Architecture Overview](../architecture/ARCHITECTURE.md) — system architecture and module planes
- [Module Security](../modules/MODULE_SECURITY.md) — trust tiers, consent, transport, audit
- [Module UI Hosting](../modules/MODULE_UI_HOSTING.md) — frame protocol, widgets, limits
- [`.dncpkg` packaging](../modules/MODULE_PACKAGING.md) — format, signing, verification
- [Module store](../modules/MODULE_STORE.md) — installer, rollback, remote index
- [Cross-module trust](../security/CROSS_MODULE_TRUST.md) — events and caller identity
