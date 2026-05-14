# Cross-Module Trust & Event Subscription Policy

**Last Updated:** May 14, 2026

---

## Principle

DotNetCloud uses a **module-isolated architecture** where modules communicate exclusively via gRPC over Unix sockets, Named Pipes (Windows), or the in-process event bus. No module directly accesses another module's database.

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

| Property               | Setting                                      |
| ---------------------- | -------------------------------------------- |
| **Transport**          | Unix sockets (Linux) / Named Pipes (Windows) |
| **Socket permissions** | `0600` (owner read/write)                    |
| **Authentication**     | Mutual TLS via Unix socket credentials       |
| **Authorization**      | CallerContext propagated with every call     |
| **Max message size**   | Configured per service (reasonable limits)   |
| **Deadline**           | Required on all client calls                 |
| **Reflection**         | Disabled in production                       |

### CallerContext Propagation

Every gRPC call includes a `CallerContext` with:

- `CallerType`: `User`, `System`, or `Module`
- `UserId`: The originating user (if user-initiated)
- `ModuleName`: The originating module (if module-initiated)

Subscribers must verify the caller is authorized for the requested operation.

---

## File System Isolation

| Area                     | Policy                                                             |
| ------------------------ | ------------------------------------------------------------------ |
| Module data directory    | Each module gets its own subdirectory under `DOTNETCLOUD_DATA_DIR` |
| Cross-module file access | Prohibited — modules must use gRPC to request file data            |
| Temporary files          | Cleaned up in `finally` blocks and on process exit                 |
| Avatar storage           | Centralized in Core server, not per-module                         |
