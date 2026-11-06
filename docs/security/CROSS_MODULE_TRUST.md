# Cross-Module Trust & Event Subscription Policy

**Last Updated:** November 6, 2026 (transport and identity claims reconciled with the deployed code)

---

## Principle

DotNetCloud uses a **module-isolated architecture**: each module runs in its own OS process and communicates with the core exclusively via gRPC. No module directly accesses another module's database.

**What ships today (v0.7.0):** the module↔core gRPC channel is cleartext HTTP/2 (h2c) over a **loopback TCP port** that the supervisor assigns per module. Identity is enforced per launch:

- The `ProcessSupervisor` mints a 32-byte CSPRNG secret for each module start (`ModuleCredentialStore`) and passes it in `DOTNETCLOUD_MODULE_SECRET`.
- Every module→core call carries `module-id` + `module-secret` metadata; `AuthenticationInterceptor` compares the secret in constant time and rejects anything else with `Unauthenticated`.
- Core→module calls are authenticated by `ModulePeerAuthenticationInterceptor` using the same secret (presented by `GrpcChannelManager`), and fail closed when the supervisor did not launch the host.
- A module never asserts an end-user identity itself: the core mints a short-lived ECDSA P-256 `caller-user-assertion` for the acting user, scoped to that module, and `CallerContextInterceptor` re-validates it and re-resolves roles from the identity store.

**Per-module Unix sockets (0600) / ACL'd Named Pipes are *planned*, not implemented** (phase A4 of `docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md`). Until then, any process running as the core service account can reach the loopback listener — the credential, not the transport, is the API-layer identity control. See [Trust-boundary limitations](#trust-boundary-limitations) below.

---

## Inter-Module Dependency Map

| Module    | Depends On                    | Mechanism          | Required For                               |
| --------- | ----------------------------- | ------------------ | ------------------------------------------ |
| Bookmarks | Search.Client                 | gRPC (client lib)  | Full-text search of bookmarks              |
| Chat      | Search.Client                 | gRPC (client lib)  | Full-text search of messages               |
| Email     | Search.Client                 | gRPC (client lib)  | Full-text search of emails                 |
| Files     | Search.Client                 | gRPC (client lib)  | Full-text search of files                  |
| Notes     | Search.Client                 | gRPC (client lib)  | Full-text search of notes                  |
| Music     | Files (events)                | Event bus          | React to file uploads for music scan       |
| Photos    | Files (events)                | Event bus          | React to file uploads for thumbnail gen    |
| Video     | Files (events)                | Event bus          | React to file uploads for video processing |
| Tracks    | Files (events), Chat (events) | Event bus          | React to file uploads + chat mentions      |
| Contacts  | Calendar.Data, Notes.Data     | Direct project ref | Shared contact picker UI                   |

> **Note (2026-11-06):** rows naming `Search.Client` describe the earlier standalone search module. Full-text search is now a core capability reached through the core (`SubmitSearchIndex` / `ISearchProvider`); no module references another module's client library today, and there is no module-to-module transport.

---

## Event Subscription Rules

All event subscriptions follow a **need-to-receive** principle:

| Rule                          | Description                                                                       |
| ----------------------------- | --------------------------------------------------------------------------------- |
| **1. Declared only**          | Modules must declare subscribed events in their `ModuleManifest.SubscribedEvents` |
| **2. No secrets in payloads** | Event DTOs must never contain passwords, tokens, PII, or raw file content         |
| **3. Subscriber validates**   | Subscribing modules must validate event data before acting on it                  |
| **4. No ordering dependency** | No security decision may depend on event ordering (ordering is not guaranteed)    |
| **5. Stale event safety**     | Replayed stale events must not leak data across tenants or teams                  |

### Current Event Subscriptions

| Event            | Publisher | Subscribers                  | Purpose                         |
| ---------------- | --------- | ---------------------------- | ------------------------------- |
| `FileUploaded`   | Files     | Music, Photos, Video, Tracks | Trigger media processing        |
| `FileDeleted`    | Files     | Music, Photos, Video         | Clean up associated media       |
| `MessageCreated` | Chat      | Tracks                       | Create work items from messages |

---

## gRPC Security Model

| Property                  | Setting (v0.7.0)                                                                        |
| ------------------------- | --------------------------------------------------------------------------------------- |
| **Transport**             | Loopback TCP, cleartext HTTP/2 (h2c); per-module sockets/pipes planned (phase A4)        |
| **Module authentication** | Per-launch secret (`module-id` + `module-secret`), constant-time compare, rotates on start |
| **Core authentication**   | `ModulePeerAuthenticationInterceptor` verifies the same secret on core→module calls      |
| **User identity**         | Core-signed `caller-user-assertion` (ECDSA P-256, short-lived, module-scoped)             |
| **Authorization**         | `CapabilityGrantInterceptor` enforces `ModuleCapabilityGrants` per RPC                    |
| **Max message size**      | 16 MB receive/send on the module listener                                                |
| **Detailed errors**       | Disabled outside Development                                                              |
| **Reflection**            | Disabled in production                                                                   |

### CallerContext Propagation

Every gRPC call is executed with a `CallerContext` derived **server-side**:

- `CallerType`: `User` (core-signed user assertion) or `Module` (module acting for itself). `System` is never reachable over the module transport.
- `UserId`: the asserted user's id for `User`; a stable pseudo-identity derived from the verified module id for `Module`.
- `Roles`: re-resolved from the identity store for the asserted user; empty for module callers.

Module-supplied `caller-user-id`, `caller-type` and `caller-roles` headers are **not read**. Subscribers must authorize against this derived context, never against fields in the request payload.

### Trust-boundary limitations

While every module runs under the same OS user as the core, this is an **API, configuration and data-layer boundary — not an OS boundary**. A malicious module can read a sibling process's environment and files and can connect to its socket; the per-launch credential bounds impersonation to the lifetime of a process but does not eliminate it. Compensating controls are credential rotation, capability grants, audit logging and (for third-party code) store review. Per-module OS isolation is re-evaluated as a public-store launch gate.

---

## File System Isolation

| Area                     | Policy                                                                                                     |
| ------------------------ | ---------------------------------------------------------------------------------------------------------- |
| Module data directory    | Modules currently share `DOTNETCLOUD_DATA_DIR`; per-module `<dataDir>/modules/<id>/{config,data,run}` (0700) lands in phase A1 |
| Cross-module file access | Prohibited — modules must use gRPC to request file data                                                     |
| Temporary files          | Cleaned up in `finally` blocks and on process exit                                                          |
| Avatar storage           | Centralized in Core server, not per-module                                                                  |
