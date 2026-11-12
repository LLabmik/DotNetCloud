# Cross-Module Trust & Event Subscription Policy

**Last Updated:** November 12, 2026 (transport, inter-module map and event-delivery claims re-verified against the code)

---

## Principle

DotNetCloud uses a **module-isolated architecture**: each module runs in its own OS process and
communicates with the core exclusively via gRPC. No module directly accesses another module's database
or process.

**What ships today (v0.7.0):** the module↔core gRPC channel is cleartext HTTP/2 (h2c). The default
transport is a **per-module Unix domain socket** (`<dataDir>/modules/<moduleId>/run/module.sock`,
`0600`, directory `0700`) on Linux/macOS and a **per-module Named Pipe**
(`PipeOptions.CurrentUserOnly`) on Windows; `Modules:Transport=Tcp` selects a supervisor-assigned
**loopback TCP port** instead. Identity is enforced per launch:

- The `ProcessSupervisor` mints a 32-byte CSPRNG token for each module start and passes it in `DOTNETCLOUD_MODULE_TOKEN`.
- Every module→core call carries `dnc-module-id` + `dnc-module-token` metadata; `AuthenticationInterceptor` compares the token in constant time and rejects anything else with `Unauthenticated`.
- Core→module calls are authenticated by `ModulePeerAuthenticationInterceptor` using the same token (presented by `GrpcChannelManager`), and fail closed when the supervisor did not launch the host.
- A module never asserts an end-user identity itself: the core mints a short-lived ECDSA P-256 `dnc-user-assertion` for the acting user, scoped to that module, and `CallerContextInterceptor` re-validates it and re-resolves roles from the identity store.

See [`../modules/MODULE_SECURITY.md`](../modules/MODULE_SECURITY.md) §9 for the transport matrix and §7
for the consent plane.

---

## Inter-Module Dependency Map

**There is no module-to-module dependency.** No module references another module's assembly, resolves
another module's services, or dials another module's endpoint — the supervisor only allocates a
core↔module transport, and a module is never given another module's address.

Those relationships are now expressed as calls to the core:

| Module    | Needs                                  | Mechanism                                                       |
| --------- | -------------------------------------- | --------------------------------------------------------------- |
| Any       | Full-text search                      | `IDncModuleApi.SubmitSearchIndexAsync` → core search index       |
| Any       | Another module's data                  | A core capability RPC (e.g. files via `IFileDirectory`)          |
| Any       | Reacting to another module's activity  | `IDncModuleApi.PublishEventAsync` on the core event bus          |
| Tracks    | File and chat context                  | Core-brokered file RPCs and published events                     |
| Contacts  | Calendar/Notes UI                      | None — contacts owns its own data and UI (`UI.Shared` is core-owned) |

> A module's UI project may reference `DotNetCloud.UI.Shared` (the core-owned Razor class library:
> `MaterialIcon`, layout primitives). That is a dependency on the *core*, not on another module.

---

## Event Subscription Rules

All event declarations follow a **need-to-know** principle:

| Rule                          | Description                                                                       |
| ----------------------------- | --------------------------------------------------------------------------------- |
| **1. Declared only**          | A module may only publish event types listed in its manifest `events.publishes`    |
| **2. No secrets in payloads** | Event DTOs must never contain passwords, tokens, PII, or raw file content         |
| **3. Subscriber validates**   | A consumer must validate event data before acting on it                            |
| **4. No ordering dependency** | No security decision may depend on event ordering (ordering is not guaranteed)    |
| **5. Stale event safety**     | Replayed stale events must not leak data across tenants or teams                   |
| **6. Consent filtering**      | Delivery of a personal-data event to a per-user consumer is filtered by that user's consent |

### Current state of subscriptions

`events.subscribes` is validated and recorded (`ModuleManifestRegistry`), but **no core→module event
delivery channel exists yet**, so no first-party module declares a subscription and the earlier
`FileUploaded` → Music/Photos/Video/Tracks table is no longer accurate. Modules react to file activity
by calling the core, not by consuming a cross-module event.

What *is* enforced today is the publishing side: `PublishEvent` checks the event type against the
deciding module's manifest, rejects an undeclared type (`MODULE_EVENT_REJECTED`) and audits the
attempt. Core-side `ModuleEventInbox` records subscriptions with consent filtering for the delivery
path as it lands.

---

## gRPC Security Model

| Property                  | Setting (v0.7.0)                                                                                                                                          |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Transport**             | Per-module Unix socket (Linux/macOS) or Named Pipe (`CurrentUserOnly`, Windows); loopback TCP only when `Modules:Transport=Tcp`                            |
| **Module authentication** | Per-launch token (`dnc-module-id` + `dnc-module-token`), constant-time compare, rotates on start                                                          |
| **Core authentication**   | `ModulePeerAuthenticationInterceptor` verifies the same token on core→module calls                                                                        |
| **User identity**         | Core-signed `dnc-user-assertion` (ECDSA P-256, short-lived, module-scoped)                                                                                |
| **Authorization**         | `ModuleAccessInterceptor` -> `ModuleAccessEvaluator` enforces `ModuleCapabilityGrants` per RPC (60 s cache, invalidated on grant/revoke, denials audited) |
| **Consent**               | `ModuleConsentPolicy` requires a live `ModuleUserConsent` row for personal-data RPCs; refusal is `MODULE_CONSENT_REQUIRED` + the missing scopes               |
| **Max message size**      | 16 MB receive/send on the module listener                                                                                                                 |
| **Detailed errors**       | Disabled outside Development                                                                                                                              |
| **Reflection**            | Disabled in production                                                                                                                                    |

### CallerContext Propagation

Every gRPC call is executed with a `CallerContext` derived **server-side**:

- `CallerType`: `User` (core-signed user assertion) or `Module` (module acting for itself). `System` is never reachable over the module transport.
- `UserId`: the asserted user's id for `User`; a stable pseudo-identity derived from the verified module id for `Module`.
- `Roles`: re-resolved from the identity store for the asserted user; empty for module callers.

Module-supplied `caller-user-id`, `caller-type` and `caller-roles` headers are **not read**. Subscribers must authorize against this derived context, never against fields in the request payload.

### Trust-boundary limitations

Module hosts run as the same OS user as the core and key on that user, so a malicious module can read
a sibling process's environment and files and can connect to its socket. This is an **API, configuration
and data-layer boundary — not an OS boundary**. Per-module OS isolation is re-evaluated as a
public-store launch gate. Compensating controls today: per-launch credentials (bounding impersonation
to a process lifetime), capability grants, per-user consent, per-module database principals, audit
logging and store review.

---

## File System Isolation

| Area                     | Policy                                                                                                                                                                                                                                                           |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Module data directory    | Sandboxed modules (manifest v2, non-required) get `<dataDir>/modules/<id>/{config,data,run}` (0700) and never see `DOTNETCLOUD_CONFIG_DIR`/`DOTNETCLOUD_DATA_DIR`; v1 modules keep the shared directories only while the `Modules:AllowLegacyAccess` override is on |
| Cross-module file access | Prohibited — modules must use gRPC to request file data                                                                                                                                                                                                          |
| Storage namespacing      | `IStorageProvider` is namespaced by the **verified** module id and acting user; callers cannot express another module's or user's path, and every resolved path is re-checked with `PathContainment` against the module root                                    |
| Temporary files          | Cleaned up in `finally` blocks and on process exit                                                                                                                                                                                                               |
| Avatar storage           | Centralized in Core server, not per-module                                                                                                                                                                                                                       |

---

## Browser-side boundary (module UI plane)

A module's UI is a separate document in an **opaque-origin** iframe
(`sandbox="allow-scripts allow-forms allow-popups allow-modals"`), served by the module's own process
and proxied at `/apps/{moduleId}/**`.

| Concern                    | Control                                                                                                     |
| -------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Reading core resources     | Impossible by origin — the frame is not same-origin with the shell                                            |
| Ambient identity           | None: no cookies reach the module; the proxy injects the user assertion on the module's behalf                |
| `postMessage` spoofing     | The bridge validates `event.source`/`event.origin` against the registered frame handle, not the message body  |
| Cross-module UI access     | One frame per module; a module's document never loads another module's UI                                    |
| Embedded third-party pages  | Blocked by CSP `frame-ancestors`/`frame-src`; a module cannot frame arbitrary origins                         |

Protocol, consent banner and widget role: [`../modules/MODULE_UI_HOSTING.md`](../modules/MODULE_UI_HOSTING.md).

---
