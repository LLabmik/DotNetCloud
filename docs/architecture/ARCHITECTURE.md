<p align="center">
  <img src="../../assets/logo.png" alt="DotNetCloud" width="64" />
</p>

# DotNetCloud — Architecture Document

> **Version:** 1.0  
> **Status:** Approved  
> **Last Updated:** 2026-03-06  
> **Domain:** dotnetcloud.net  
> **Repository:** https://github.com/LLabmik/DotNetCloud

---

## Table of Contents

1. [Vision & Goals](#1-vision--goals)
2. [Architecture Pattern](#2-architecture-pattern)
3. [Database Strategy](#3-database-strategy)
4. [Identity & Authentication](#4-identity--authentication)
5. [Module System & Security](#5-module-system--security)
6. [Real-Time Communication](#6-real-time-communication)
7. [File Synchronization](#7-file-synchronization)
8. [AI & LLM Integration](#8-ai--llm-integration)
9. [Solution Structure](#9-solution-structure)
10. [Platform Support](#10-platform-support)
11. [Client Architecture](#11-client-architecture)
12. [API Design](#12-api-design)
13. [Core Data Model](#13-core-data-model)
14. [Deployment & Infrastructure](#14-deployment--infrastructure)
15. [Backup & Disaster Recovery](#15-backup--disaster-recovery)
16. [Logging & Observability](#16-logging--observability)
17. [Internationalization](#17-internationalization)
18. [CI/CD Pipeline](#18-cicd-pipeline)
19. [NextCloud Migration](#19-nextcloud-migration)
20. [Privacy & GDPR](#20-privacy--gdpr)
21. [Licensing](#21-licensing)
22. [Dependencies](#22-dependencies)
23. [Implementation Roadmap](#23-implementation-roadmap)
24. [Security Hardening Notes (2026-03)](#24-security-hardening-notes-2026-03)

---

## 1. Vision & Goals

DotNetCloud is a self-hosted, open-source cloud platform built on .NET 10/C#. It aims to be a modern alternative to NextCloud/OwnCloud, leveraging .NET's async, multithreaded capabilities to overcome the limitations of PHP-based implementations.

### Core Goals

- **Self-hosted:** Users install DotNetCloud on their own servers and domains
- **Open source:** Fully open source, funded through service and support
- **Extensible:** Third-party developers can build modules with the same power as first-party features
- **Cross-platform:** Windows, Linux (Debian-derived), Android
- **Secure:** Zero-knowledge encryption option, granular permissions, process-isolated modules
- **Easy to install:** One-command setup for inexperienced users

### Target Features

| Feature                  | Inspired By                                |
| ------------------------ | ------------------------------------------ |
| File sync & sharing      | NextCloud Files                            |
| Online document editing  | Collabora Online (LibreOffice-based)       |
| Chat & video calls       | NextCloud Talk                             |
| Project management       | NextCloud Deck + Jira                      |
| Calendar & contacts      | CalDAV/CardDAV                             |
| Email client             | SMTP/IMAP/Gmail                            |
| Notes                    | Markdown-based                             |
| Music player & library   | With equalizer, playlists, recommendations |
| Photo & video management | Auto-upload, albums, editing               |
| Browser bookmark sync    | Browser extensions                         |
| Announcements            | User/group/org broadcasts                  |
| Full-text search         | Across all modules                         |
| AI assistant             | Local (Ollama) or cloud LLM (Claude, etc.) |

---

## 2. Architecture Pattern

**Decision: Modular Monolith with Process-Isolated Modules**

### Overview

DotNetCloud runs as a single core process that supervises individual module processes. Modules communicate with the core over gRPC via Unix sockets (Linux) or Named Pipes (Windows).

```
dotnetcloud (core process — supervisor)
├── dotnetcloud-module files      (child process, gRPC)
├── dotnetcloud-module chat       (child process, gRPC)
├── dotnetcloud-module calendar   (child process, gRPC)
├── dotnetcloud-module music      (child process, gRPC)
├── collabora-code                (managed component, optional)
└── livekit                       (managed component, optional)
```

### Why This Pattern

- **Simple deployment:** Single install, single command to start (`dotnetcloud serve`)
- **Process isolation (Level 4):** Each module runs in its own process — a crash in Chat doesn't affect Files
- **Security boundary:** Modules cannot access core memory, databases, or other modules directly
- **Future extraction:** Modules already communicate over gRPC — moving one to a separate server is a configuration change
- **Eat your own dog food:** All first-party modules use the same module API as third-party modules

### Process Communication

| Platform          | Transport                                       |
| ----------------- | ----------------------------------------------- |
| Linux             | Unix domain sockets (`/run/dotnetcloud/*.sock`) |
| Windows           | Named Pipes                                     |
| Docker/Kubernetes | TCP localhost (fallback)                        |

### Process Supervisor Responsibilities

- Spawn module processes on startup based on enabled modules
- Health monitoring via periodic gRPC health checks
- Configurable restart policy per module (immediate, backoff, or alert-only)
- Graceful shutdown with drain on `dotnetcloud stop`
- Resource limits via cgroups (Linux) or Job Objects (Windows)
- Crash isolation — individual module failures don't cascade

---

## 3. Database Strategy

**Decision: EF Core with Pluggable Providers**

### Supported Database Engines

| Engine     | Status             | EF Core Provider                              |
| ---------- | ------------------ | --------------------------------------------- |
| PostgreSQL | ✅ Initial release | Npgsql (PostgreSQL License)                   |
| SQL Server | ✅ Initial release | Microsoft.EntityFrameworkCore.SqlServer (MIT) |
| MariaDB    | ✅ Initial release | Pomelo.EntityFrameworkCore.MySql (MIT)        |
| Oracle     | 🔜 Future          | Oracle.EntityFrameworkCore                    |

### Schema Isolation

Each module owns its own `DbContext` with its own tables. Modules never share database tables or access other modules' data directly.

| Approach   | Database support                                         |
| ---------- | -------------------------------------------------------- |
| PostgreSQL | Separate schemas (`files.*`, `chat.*`)                   |
| SQL Server | Separate schemas (`files.Documents`, `chat.Messages`)    |
| MariaDB    | Table name prefixes (`Files_Documents`, `Chat_Messages`) |

A configurable table naming strategy defaults to prefixes for universal compatibility, with real schemas on Postgres/SQL Server.

### Core DbContext

`CoreDbContext` owns cross-cutting tables: Users, Roles, Teams, Organizations, Permissions, Module registry, Settings. All modules reference these read-only through capability interfaces.

### Design Rules

- All module code stays within EF Core's LINQ abstraction — no raw SQL in business logic
- Provider-specific features (full-text search, JSON columns) abstracted behind interfaces (`ISearchProvider`, `IMetadataStore`)
- Provider-specific integration tests in CI (Docker containers for each DB engine)

---

## 4. Identity & Authentication

**Decision: ASP.NET Core Identity + OpenIddict**

### Stack

| Layer              | Technology                           | Purpose                                                                        |
| ------------------ | ------------------------------------ | ------------------------------------------------------------------------------ |
| User/role storage  | ASP.NET Core Identity                | User management, password hashing, MFA TOTP, lockout, email confirmation       |
| OAuth2/OIDC server | OpenIddict (Apache 2.0)              | Issues access/refresh/ID tokens, authorization code + PKCE, client credentials |
| Federation         | ASP.NET Core External Authentication | Sign in with Google/Microsoft/GitHub, enterprise SAML/OIDC                     |
| MFA                | Identity TOTP + Fido2NetLib (MIT)    | Authenticator apps + hardware keys/passkeys                                    |

### Why OpenIddict Over Duende

OpenIddict is Apache 2.0 (free for all uses). Duende IdentityServer requires a paid license for production revenue over $1M — incompatible with an open-source project that others will self-host commercially.

### Auth Flows by Client

| Client           | Flow                                                    |
| ---------------- | ------------------------------------------------------- |
| Blazor UI        | Cookie-based session via OpenIddict                     |
| Avalonia Desktop | OAuth2 Authorization Code + PKCE (opens system browser) |
| Android MAUI     | OAuth2 Authorization Code + PKCE (Chrome Custom Tab)    |
| Sync Service     | Refresh token (long-lived, initial auth via PKCE)       |
| Third-party apps | OAuth2 Client Credentials or Auth Code                  |

### What This Replaces

The current hand-rolled auth (manual salt/hash via `PasswordHelper`, manual JWT via `JwtSecurityTokenHandler`) is replaced entirely by Identity + OpenIddict.

---

## 5. Module System & Security

### Module Manifest

Every module declares its identity, capabilities, and events upfront:

```csharp
public class ChatModuleManifest : IModuleManifest
{
    public string Id => "dotnetcloud.chat";
    public string Name => "Chat & Video";
    public Version Version => new(1, 0, 0);

    public IReadOnlyList<CapabilityRequest> RequiredCapabilities => [ ... ];
    public IReadOnlyList<string> PublishedEvents => [ ... ];
    public IReadOnlyList<string> SubscribedEvents => [ ... ];
}
```

### Capability Interface Tiers

| Tier           | Examples                                                                     | Access                                   |
| -------------- | ---------------------------------------------------------------------------- | ---------------------------------------- |
| **Public**     | `IUserDirectory`, `ICurrentUserContext`, `INotificationService`, `IEventBus` | Auto-granted                             |
| **Restricted** | `IStorageProvider` (scoped), `IModuleSettings`, `ITeamDirectory`             | Admin must approve                       |
| **Privileged** | `IUserManager` (create/disable users)                                        | Admin must approve, flagged as sensitive |
| **Forbidden**  | `CoreDbContext`, `IConfiguration`                                            | Never injected into modules              |

### Security Principles

1. **Structural impossibility:** Modules interact only through capability interfaces. Sensitive data (password hashes, MFA secrets, signing keys) is not exposed through any interface — it's not about trusting developers, it's about making the wrong thing impossible at the type system level.

2. **Admin approval for capability escalation:** Modules declare capabilities in their manifest. Public tier is auto-granted. Restricted and Privileged require admin approval.

3. **Update with degraded mode (Option C):** Module updates install immediately (security patches flow through). New capability requests are disabled until admin approves. Modules handle "capability not granted" gracefully with null injection.

4. **Explicit caller context:** Every capability interface method requires a `CallerContext` parameter — user identity always flows explicitly. No ambient/implicit context.

```csharp
public record CallerContext
{
    public Guid UserId { get; init; }
    public IReadOnlyList<string> Roles { get; init; }
    public required CallerType Type { get; init; } // User, System, Module
}
```

5. **Cross-module communication security:**
   - **Event bus:** Manifest-declared, admin-approved, strictly enforced at runtime. Tiered payloads — detail level depends on granted capabilities.
   - **Direct interfaces:** Cross-module calls go through declared public interfaces (e.g., `IFileService`). The owning module enforces its own permission checks using the `CallerContext`.

6. **Transport identity binding (REST + gRPC):** User-scoped endpoints require authenticated identity to match requested user scope. In practice, `request.user_id`/query `userId` must match token claims (`NameIdentifier`/`sub`) or the request is rejected.

7. **Tenant-scoped lookups:** Files module read/write operations are owner-scoped (or share-scoped where explicitly allowed) so cross-user probing cannot leak existence or metadata.

8. **Storage namespace isolation:** Each module's `IStorageProvider` is scoped to its own namespace. Path traversal attacks blocked by both path sanitization and OS-level directory isolation (defense in depth).

9. **Process isolation:** Each module runs in a separate process. Even reflection-based attacks can't access core memory.

10. **Code signing & review:** Modules are signed by trusted publishers. Official module registry with community review.

### Trust Levels

| Level                  | Who                                  | Treatment                                      |
| ---------------------- | ------------------------------------ | ---------------------------------------------- |
| Core                   | DotNetCloud platform                 | Full access                                    |
| First-Party Modules    | Official modules (Files, Chat, etc.) | Same sandbox as third-party (dog-fooding)      |
| Third-Party Verified   | Reviewed & signed community modules  | Full sandbox, all capabilities available       |
| Third-Party Unverified | Unreviewed modules                   | Warning shown, Privileged capabilities blocked |

### Zero-Knowledge Encryption (E2EE)

Optional, not default. When enabled per-user or per-folder:

- Server stores ciphertext only — admin cannot read user data
- Client-side encryption/decryption (Avalonia/MAUI via WebCrypto or .NET crypto APIs)
- Server-side features (search, thumbnails, analysis) do not work on encrypted content
- Key loss = data loss (true zero-knowledge)

---

## 6. Real-Time Communication

### Stack

| Layer                         | Technology                           | License      |
| ----------------------------- | ------------------------------------ | ------------ |
| Chat, presence, notifications | SignalR (.NET)                       | MIT          |
| WebRTC signaling              | SIPSorcery (.NET)                    | BSD-3-Clause |
| P2P calls (1-3 people)        | WebRTC peer-to-peer (browser-native) | N/A          |
| Group calls / SFU (4+ people) | LiveKit (optional, Go)               | Apache 2.0   |

### Architecture

SignalR lives in the core process. Modules tell the core what to broadcast via `IRealtimeBroadcaster` capability interface.

```
User sends message → SignalR hub (core) → Chat module (gRPC) → validates, stores
                                         → Core broadcasts via SignalR to channel members
```

### Video Calling

- **Hybrid approach:** P2P for small calls (1-3 people), SFU for larger groups (4+)
- **LiveKit is optional:** Instances without video calling don't install it
- **LiveKit installation is seamless:** `dotnetcloud setup` downloads and configures it automatically. Users never see LiveKit's configuration.
- **LiveKit runs as a managed component** under DotNetCloud's process supervisor

### Push Notifications (Android)

| Method                         | For whom                                              |
| ------------------------------ | ----------------------------------------------------- |
| FCM (Firebase Cloud Messaging) | Regular Android (free, Google Play Services required) |
| UnifiedPush                    | De-Googled Android (open-source, self-hostable)       |

App detects available push method at startup. Build flavors: `googleplay` (FCM) and `fdroid` (UnifiedPush only).

---

## 7. File Synchronization

### Change Detection

**Hybrid approach:** `FileSystemWatcher` for instant detection + periodic full scan (every 5 minutes) as safety net. Catches 99%+ of changes instantly, remaining caught by scan.

### Transfer Strategy

**Chunked upload with content-hash deduplication:**

- Files split into 4MB chunks
- Each chunk hashed with SHA-256
- Client sends chunk manifest to server
- Server responds with which chunks it already has
- Only missing/changed chunks are uploaded
- Identical chunks stored once (cross-user deduplication)
- Interrupted transfers resume at the last unfinished chunk

**Security invariants for chunked transfer:**

- Upload chunks are accepted only for active upload sessions
- Upload session ownership is enforced per caller identity (no cross-user session reuse)
- Uploaded bytes must match the declared chunk hash
- Chunk hashes must exist in the session manifest before acceptance
- Parent/target nodes are owner-scoped for create/move/copy/complete flows

### Conflict Resolution

**Option C: Conflict copy with guided resolution notification.**

- Both versions kept — conflicting file renamed: `report (conflict - Ben - 2025-07-14).docx`
- User notified in Blazor UI and desktop client
- Resolution UI shows both versions side by side
- No silent data loss, ever
- Future: Operational Transform/CRDT for text-based files (Markdown, code)

### Selective Sync

Users choose which folders sync to which devices. Device with limited storage doesn't need to sync the entire library.

### Local State

SQLite database per OS user per account on each client tracks sync state, chunk hashes, file metadata, pending operations. Databases are stored under the OS user's profile directory, fully isolated from other users on the same machine. See [Multi-User Client Support](#multi-user-client-support) in Section 11.

### Online Document Editing (Collabora)

**Decision: Collabora Online via WOPI, with Collabora CODE for local/small deployments**

DotNetCloud integrates with [Collabora Online](https://www.collaboraoffice.com/collabora-online/) to provide browser-based editing of documents, spreadsheets, and presentations — directly from the file browser without downloading.

#### Architecture

Collabora Online is an external managed component (like LiveKit). It runs as a separate process under DotNetCloud's process supervisor and communicates with the Files module via the **WOPI protocol** (Web Application Open Platform Interface).

```
User opens document → Blazor UI loads Collabora iframe
                       → Collabora fetches file via WOPI (Files module)
                       → User edits in browser
                       → Collabora saves via WOPI → Files module stores new version
```

#### Deployment Modes

| Mode                | Component                            | For whom                                                                                                |
| ------------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| **Built-in (CODE)** | Collabora CODE (Development Edition) | Small instances, home users, development. Auto-installed by `dotnetcloud setup`.                        |
| **External**        | Collabora Online (full)              | Organizations needing higher capacity. Admin points DotNetCloud at an external Collabora Online server. |
| **Docker**          | Collabora CODE container             | Docker Compose deployments. Included in auto-generated `docker-compose.yml`.                            |

#### Collabora CODE (Built-In)

- **Collabora CODE** is the free, community edition of Collabora Online — same LibreOffice-based engine, designed for smaller deployments
- `dotnetcloud setup` downloads and configures CODE automatically (same pattern as LiveKit)
- Runs under the process supervisor as a managed component
- Users never see Collabora's configuration — DotNetCloud handles WOPI discovery, URL routing, and TLS
- Suitable for personal/family instances and small teams

#### Collabora Online (External)

- For larger deployments, admin configures an external Collabora Online server URL in the admin UI
- DotNetCloud's WOPI host implementation works identically with CODE or full Collabora Online
- Enables clustering, higher concurrent user limits, and dedicated resources for document editing

#### WOPI Integration

The Files module implements a **WOPI host** endpoint:

- `GET /api/v1/wopi/files/{fileId}` — File info (name, size, permissions)
- `GET /api/v1/wopi/files/{fileId}/contents` — Download file content
- `POST /api/v1/wopi/files/{fileId}/contents` — Save edited file (creates new version)
- WOPI access tokens scoped per-user, per-file, time-limited
- All WOPI requests flow through the Files module's permission checks using `CallerContext`

#### Supported Formats

| Category      | Formats                 |
| ------------- | ----------------------- |
| Documents     | .docx, .odt, .doc, .rtf |
| Spreadsheets  | .xlsx, .ods, .xls, .csv |
| Presentations | .pptx, .odp, .ppt       |
| Other         | .txt, .html (basic)     |

#### Collaborative Editing

- **Real-time co-editing:** Multiple users edit the same document simultaneously (Collabora handles OT/conflict resolution internally)
- **Awareness:** Users see each other's cursors and selections
- **Auto-save:** Changes saved automatically at configurable intervals
- **Version integration:** Each save-back creates a new file version in DotNetCloud's versioning system

#### Privacy & E2EE

- Document content is processed by Collabora in-memory on the same server (CODE) or on a trusted internal server (external Collabora Online)
- No data sent to external services unless admin explicitly configures an external Collabora server
- **E2EE-incompatible:** Online editing requires the server to read file content. Encrypted files show a "download to edit locally" prompt instead.

#### CLI

```sh
dotnetcloud component status collabora    # Check Collabora CODE status
dotnetcloud component restart collabora   # Restart Collabora CODE
```

---

## 8. AI & LLM Integration

**Decision: Microsoft.Extensions.AI with Pluggable Providers (Ollama + Cloud)**

### Overview

DotNetCloud integrates LLM capabilities through a provider-agnostic abstraction layer, supporting both **local/network LLMs via Ollama** and **cloud-hosted LLMs** (Anthropic Claude, OpenAI, etc.). Users choose where their AI runs — privacy-conscious users keep everything local; others can opt into cloud providers for more capable models.

### Architecture

`Microsoft.Extensions.AI` provides the `IChatClient` abstraction — a first-party .NET interface that decouples application code from specific LLM providers. This follows the same multi-provider pattern used for databases (`ISearchProvider`) and storage (`IStorageProvider`).

```
DotNetCloud.Core (ILlmProvider capability interface)
        │
        ▼
DotNetCloud.Modules.AI (module process)
        │
        ├── OllamaProvider  ← Microsoft.Extensions.AI.Ollama
        │     └── Connects to http://localhost:11434 or LAN address
        │
        ├── AnthropicProvider ← Community IChatClient for Claude
        │     └── API key in encrypted settings
        │
        └── OpenAIProvider ← Microsoft.Extensions.AI.OpenAI
              └── API key in encrypted settings (optional)
```

### Capability Interface

```csharp
public interface ILlmProvider
{
    /// <summary>
    /// Send a chat completion request to the configured LLM.
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        CallerContext caller,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream a chat completion response token-by-token.
    /// </summary>
    IAsyncEnumerable<LlmResponseChunk> CompleteStreamingAsync(
        CallerContext caller,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List available models from the configured provider(s).
    /// </summary>
    Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(
        CallerContext caller,
        CancellationToken cancellationToken = default);
}
```

`ILlmProvider` is a **Restricted** capability — modules must declare it in their manifest, and admin must approve access.

### Provider Strategy

| Provider           | Transport                           | Models                           | License     | Cost               |
| ------------------ | ----------------------------------- | -------------------------------- | ----------- | ------------------ |
| Ollama (local/LAN) | HTTP to `localhost:11434` or LAN IP | Llama, Mistral, Phi, Gemma, etc. | MIT         | Free (self-hosted) |
| Anthropic Claude   | HTTPS API                           | Claude 4 Sonnet, Opus, Haiku     | Proprietary | Pay-per-token      |
| OpenAI             | HTTPS API                           | GPT-4o, GPT-4.1, etc.            | Proprietary | Pay-per-token      |
| Azure OpenAI       | HTTPS API                           | Same as OpenAI, Azure-hosted     | Proprietary | Pay-per-token      |

Admin configures one or more providers during `dotnetcloud setup` or via admin UI. Users can select their preferred provider/model per-session if multiple are configured.

### Ollama Integration

- **Local:** Ollama runs on the same machine as DotNetCloud (`http://localhost:11434`)
- **Network:** Ollama runs on another machine on the LAN (`http://ollama.local:11434`)
- **Model management:** Admin pulls/removes models via DotNetCloud admin UI, which proxies to Ollama's API (`/api/pull`, `/api/delete`, `/api/tags`)
- **No internet required:** Fully air-gapped operation possible with Ollama
- **GPU passthrough:** Docker deployments pass through GPU for hardware-accelerated inference

### Cloud Provider Integration

- **API keys stored encrypted** in `SystemSetting` (module-scoped, admin-only)
- **No data leaves the server unless admin explicitly configures a cloud provider**
- **Per-user opt-in:** Admin can allow users to bring their own API keys (stored in `UserSetting`, encrypted)
- **Rate limiting:** Cloud provider usage tracked and rate-limited per-user to control costs
- **Fallback chain:** Admin can configure fallback order (e.g., try Ollama first, fall back to Claude if Ollama is unavailable)

### Module Integration Points

Other modules can leverage AI through the `ILlmProvider` capability:

| Module | AI Use Case                                  |
| ------ | -------------------------------------------- |
| Notes  | Summarize, expand, translate, grammar check  |
| Chat   | Message summarization, smart replies         |
| Email  | Draft replies, summarize threads             |
| Files  | Content summarization, document Q&A          |
| Search | Semantic search, natural language queries    |
| Tracks | Generate card descriptions, sprint summaries |

### Privacy & Data Sovereignty

- **Local-first by default:** If Ollama is configured, all AI processing stays on-premise
- **Cloud providers are opt-in:** Admin must explicitly add API keys and enable cloud providers
- **E2EE compatibility:** AI features are unavailable for encrypted content (server can't read ciphertext)
- **No training:** Cloud API calls use provider APIs that don't train on user data (Anthropic API, OpenAI API)
- **Audit log:** All LLM requests logged with user, module, provider, and token count (content not logged)

### Blazor UI

- Chat-style AI assistant panel (slide-out or dedicated page)
- Streaming responses rendered token-by-token via SignalR
- Model selector dropdown (shows available models from all configured providers)
- Context-aware: modules can inject context into the assistant (e.g., "summarize this note")
- Conversation history stored per-user (optional, admin-configurable retention)

---

## 9. Solution Structure

```
DotNetCloud.sln
│
├── src/
│   ├── Core/
│   │   ├── DotNetCloud.Core/                    ← Shared abstractions, interfaces, DTOs
│   │   ├── DotNetCloud.Core.Data/               ← CoreDbContext (Identity, Orgs, Permissions)
│   │   ├── DotNetCloud.Core.Server/             ← Process supervisor, gRPC host, OpenIddict,
│   │   │                                          SignalR, module loader
│   │   └── DotNetCloud.Core.ServiceDefaults/    ← Logging, health checks, telemetry
│   │
│   ├── Modules/
│   │   ├── DotNetCloud.Modules.Files/
│   │   │   ├── DotNetCloud.Modules.Files/
│   │   │   ├── DotNetCloud.Modules.Files.Data/
│   │   │   └── DotNetCloud.Modules.Files.Host/
│   │   ├── DotNetCloud.Modules.Chat/
│   │   ├── DotNetCloud.Modules.Calendar/
│   │   ├── DotNetCloud.Modules.Contacts/
│   │   ├── DotNetCloud.Modules.Notes/
│   │   ├── DotNetCloud.Modules.Music/
│   │   ├── DotNetCloud.Modules.Email/
│   │   ├── DotNetCloud.Modules.Tracks/
│   │   ├── DotNetCloud.Modules.Photos/
│   │   ├── DotNetCloud.Modules.Bookmarks/
│   │   ├── DotNetCloud.Modules.Announcements/
│   │   ├── DotNetCloud.Modules.AI/              ← LLM integration (Ollama + cloud)
│   │   └── DotNetCloud.Modules.Example/         ← Developer reference module
│   │
│   ├── UI/
│   │   ├── DotNetCloud.UI.Web/                  ← Blazor app shell
│   │   ├── DotNetCloud.UI.Shared/               ← Shared components, themes
│   │   └── DotNetCloud.UI.Modules/              ← Per-module UI extensions
│   │
│   └── Clients/
│       ├── DotNetCloud.Client.Core/             ← Shared: sync engine, API, auth, SQLite
│       ├── DotNetCloud.Client.SyncService/      ← .NET Worker Service (background, no UI)
│       ├── DotNetCloud.Client.SyncTray/         ← Avalonia tray app
│       ├── DotNetCloud.Client.Desktop/          ← Avalonia full app
│       └── DotNetCloud.Client.Mobile/           ← .NET MAUI (Android)
│
├── tests/
│   ├── DotNetCloud.Core.Tests/
│   ├── DotNetCloud.Modules.Files.Tests/
│   ├── DotNetCloud.Modules.Chat.Tests/
│   ├── DotNetCloud.Integration.Tests/           ← Multi-DB, cross-module
│   └── DotNetCloud.Client.Tests/
│
├── tools/
│   ├── DotNetCloud.CLI/                         ← Setup wizard, module mgmt, backup
│   └── DotNetCloud.Installer/                   ← Package build scripts (deb, rpm, msi)
│
└── docs/
    ├── architecture/
    ├── module-development/                      ← 10-chapter developer guide + example walkthrough
    ├── admin-guide/
    ├── api-reference/
    ├── development/                             ← Android builds, push notifications, CI/CD
    └── distribution-infrastructure/
```

### Key Rules

- Modules only reference `DotNetCloud.Core` (the interfaces project) — never `Core.Data`, `Core.Server`, or other modules
- UI is separate from server logic
- Client shares no code with server (communicates over HTTPS only)
- Each module is independently deployable (`.Host` is the entry point)
- Tests mirror source structure

### Repository Strategy

Monorepo now (single Git repo), designed for easy split later. When split, module project references become NuGet package references (one line change per `.csproj`). `dotnet new dotnetcloud-module` template scaffolds new module repos.

---

## 10. Platform Support

| Platform               | Status             | Notes                                             |
| ---------------------- | ------------------ | ------------------------------------------------- |
| Windows                | ✅ Initial release | Primary development platform                      |
| Linux (Debian-derived) | ✅ Initial release | Linux Mint, Ubuntu, Debian. APT packages.         |
| Android                | ✅ Initial release | .NET MAUI. Play Store + F-Droid + direct APK.     |
| macOS                  | 🔜 Future          | Architecture supports it. Avalonia runs on macOS. |
| iOS                    | 🔜 Future          | Requires Mac to build.                            |

---

## 11. Client Architecture

### Three Desktop Components (Option D)

| Component                        | Technology          | Purpose                                                                               |
| -------------------------------- | ------------------- | ------------------------------------------------------------------------------------- |
| `DotNetCloud.Client.SyncService` | .NET Worker Service | Background file sync. Runs as Windows Service / systemd unit. No UI. Survives logout. |
| `DotNetCloud.Client.SyncTray`    | Avalonia tray app   | Tray icon, sync status, settings window. Communicates with SyncService via IPC.       |
| `DotNetCloud.Client.Desktop`     | Avalonia full app   | Chat, music, notes, calendar UI. Rich windowed application.                           |

### Android

| Component                   | Technology | Purpose                                               |
| --------------------------- | ---------- | ----------------------------------------------------- |
| `DotNetCloud.Client.Mobile` | .NET MAUI  | File browsing, photo auto-upload, chat, notifications |

### Shared Library

`DotNetCloud.Client.Core` — sync engine, API client, OAuth2 PKCE, local SQLite. Used by all clients. Written once.

### Multi-User Client Support

A single client machine may have multiple OS-level users (e.g., a family sharing a Windows PC), and each OS user may connect to one or more DotNetCloud server accounts (e.g., personal + work). The sync service handles both scenarios.

#### Architecture

The `SyncService` runs as a **single system-level service** (Windows Service / systemd unit) but manages **independent sync contexts** — one per OS-user-plus-account combination.

```
SyncService (system service, one process)
├── SyncContext: alice@pc → alice@home.dotnetcloud.net
│   ├── Auth: OAuth2 refresh token (encrypted, per-context)
│   ├── Sync folder: C:\Users\Alice\DotNetCloud\Home\
│   └── State DB: %LOCALAPPDATA%\DotNetCloud\sync-home.db
├── SyncContext: alice@pc → alice@work.dotnetcloud.net
│   ├── Auth: OAuth2 refresh token (encrypted, per-context)
│   ├── Sync folder: C:\Users\Alice\DotNetCloud\Work\
│   └── State DB: %LOCALAPPDATA%\DotNetCloud\sync-work.db
└── SyncContext: bob@pc → bob@home.dotnetcloud.net
    ├── Auth: OAuth2 refresh token (encrypted, per-context)
    ├── Sync folder: C:\Users\Bob\DotNetCloud\Home\
    └── State DB: %LOCALAPPDATA%\DotNetCloud\sync-home.db
```

#### Data Isolation

| Concern          | Approach                                                                                                      |
| ---------------- | ------------------------------------------------------------------------------------------------------------- |
| Configuration    | Stored under OS user profile (`%LOCALAPPDATA%\DotNetCloud\` / `~/.local/share/dotnetcloud/`)                  |
| SQLite state DBs | One per account, under the OS user's profile directory                                                        |
| Sync folders     | Default under OS user's home directory; user-configurable per account                                         |
| Auth tokens      | Encrypted per-context, stored in OS user's profile; inaccessible to other OS users via filesystem permissions |
| Selective sync   | Configured independently per account                                                                          |

OS-level filesystem permissions ensure one OS user cannot access another's sync data, tokens, or state databases.

#### SyncTray ↔ SyncService Communication

The `SyncTray` app runs per OS user session. On launch it connects to the shared `SyncService` over IPC (Named Pipe on Windows, Unix socket on Linux) and identifies itself with the current OS user identity. The `SyncService` responds with only the sync contexts belonging to that OS user — no cross-user information is exposed.

#### Account Management

- **Add account:** `SyncTray → Settings → Add Account` initiates OAuth2 PKCE in the system browser. On success, the `SyncService` creates a new sync context for the OS user.
- **Remove account:** Stops sync, deletes the refresh token and state DB for that context. Synced files on disk are optionally deleted.
- **Switch default:** When multiple accounts exist, the user picks a default for the tray icon status display.

#### Linux Considerations

On multi-user Linux systems the `SyncService` systemd unit runs as root (or a dedicated `dotnetcloud-sync` user) and drops privileges per sync context using the target OS user's UID/GID for all filesystem operations. This ensures files are created with correct ownership and prevents privilege escalation.

### Android Specifics

| Concern            | Approach                                            |
| ------------------ | --------------------------------------------------- |
| Background sync    | Android WorkManager                                 |
| Photo auto-upload  | MediaStore content observer                         |
| Push notifications | FCM (default) + UnifiedPush (de-Googled)            |
| Distribution       | Google Play Store + F-Droid + direct APK            |
| Build flavors      | `googleplay` (FCM) / `fdroid` (UnifiedPush only)    |
| Battery            | Respect Doze mode, batch uploads on WiFi + charging |

---

## 12. API Design

### Style

REST with OpenAPI auto-generation (primary). GraphQL as optional future module.

### Versioning

URL path versioning: `/api/v1/`, `/api/v2/`

### Module Namespacing

```
/api/v1/core/users
/api/v1/core/auth
/api/v1/files/list
/api/v1/chat/channels
/api/v1/calendar/events
/api/v1/music/library
```

Disabled modules return `404` for their entire namespace.

### Identity Binding Semantics

- REST and gRPC user-scoped operations are claim-bound: caller identity (`NameIdentifier`/`sub`) must match the requested user scope (`userId` / `request.user_id`)
- Mismatches are treated as authorization failures, preventing user impersonation via crafted request parameters
- Files read paths may intentionally return `404` for unauthorized callers to reduce metadata leakage (existence probing)

### Response Envelope

```json
{
  "success": true,
  "data": {},
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalItems": 1234,
    "totalPages": 25
  }
}
```

```json
{
  "success": false,
  "error": {
    "code": "FILES_QUOTA_EXCEEDED",
    "message": "Storage quota exceeded.",
    "details": {}
  }
}
```

### Bulk Operations

Supported from day one for file operations and batch notifications:

- `POST /api/v1/files/bulk/move`
- `POST /api/v1/files/bulk/delete`
- `POST /api/v1/files/bulk/copy`
- `POST /api/v1/files/sync/reconcile`

### Rate Limiting

Configurable rate limiting with generous defaults. Admins can tighten per-module or per-client.

---

## 13. Core Data Model

### Identity (ASP.NET Core Identity)

| Entity            | Extends              | Key additions                                                              |
| ----------------- | -------------------- | -------------------------------------------------------------------------- |
| `ApplicationUser` | `IdentityUser<Guid>` | DisplayName, AvatarUrl, Locale, Timezone, CreatedAt, LastLoginAt, IsActive |
| `ApplicationRole` | `IdentityRole<Guid>` | Description, IsSystemRole                                                  |

### Organization Hierarchy

```
Organization
├── Teams
│   └── TeamMembers (with team-scoped roles)
├── Groups (cross-team permission groups)
│   └── GroupMembers
└── OrganizationMembers (with org-scoped roles)
```

### Scope

Single organization per instance initially. Architecture supports multi-org. Future multi-org documented for when needed.

### Permissions

Granular permissions (`files.upload`, `chat.create_channel`, `admin.manage_users`) grouped into default roles. Most admins assign roles; power users customize individual permissions.

Permission scoping: Global → Organization → Team → Resource (inherits downward).

### Settings

Three scopes: `SystemSetting`, `OrganizationSetting`, `UserSetting`. Each keyed per-module.

### Devices & Modules

- `UserDevice` — registered sync clients and mobile devices
- `InstalledModule` — module registry with version, status, capabilities
- `ModuleCapabilityGrant` — admin-approved capabilities per module

---

## 14. Deployment & Infrastructure

### Deployment Modes

| Mode           | Description                                                  |
| -------------- | ------------------------------------------------------------ |
| Bare metal     | Built-in process supervisor. Single install, single command. |
| Docker Compose | Auto-generated `docker-compose.yml`. One command.            |
| Kubernetes     | Helm chart. Each module is a pod.                            |

### Reverse Proxy Support

| Server         | Platform    | Integration                                               |
| -------------- | ----------- | --------------------------------------------------------- |
| IIS            | Windows     | ASP.NET Core Module (ANCM), auto-generated `web.config`   |
| Apache         | Linux/macOS | `mod_proxy` + `mod_proxy_wstunnel`, auto-generated config |
| nginx          | Linux/macOS | Auto-generated config                                     |
| Direct Kestrel | Any         | Dev/small installs                                        |

### TLS / Let's Encrypt

`dotnetcloud setup` offers automatic Let's Encrypt certificate provisioning with auto-renewal via systemd timer or background task.

### Linux Installation

Three paths:

1. **One-line install script:** `curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash`
2. **Native package manager:** `apt install dotnetcloud` (requires adding APT repo)
3. **Unattended/headless:** `dotnetcloud setup --unattended` with all flags for automation (Ansible, Terraform, cloud-init)

### Linux File System Layout (FHS-compliant)

```
/opt/dotnetcloud/          ← Binaries
/etc/dotnetcloud/          ← Configuration
/var/lib/dotnetcloud/      ← Persistent data & file storage
/var/log/dotnetcloud/      ← Logs
/run/dotnetcloud/          ← Runtime (sockets, PIDs)
```

### systemd Integration

Auto-generated `dotnetcloud.service` unit with security hardening (`NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`).

### Windows Installation

MSI installer via WinGet with component checkboxes. Runs as Windows Service.

### CLI Management

```sh
dotnetcloud setup              # Interactive first-run wizard
dotnetcloud serve              # Start everything
dotnetcloud status             # Show service & module status
dotnetcloud module list        # List all modules
dotnetcloud module start X     # Start a module
dotnetcloud module stop X      # Stop a module
dotnetcloud module restart X   # Restart a module
dotnetcloud module install X   # Install a module
dotnetcloud component restart livekit  # Manage components
dotnetcloud logs               # View logs
dotnetcloud logs chat          # Module-specific logs
dotnetcloud backup             # Create backup
dotnetcloud restore            # Restore from backup
dotnetcloud update             # Check/apply updates
dotnetcloud help               # Full command reference
```

---

## 15. Backup & Disaster Recovery

### Built-In Backup

```sh
dotnetcloud backup --output /backups/dotnetcloud-2025-07-14.tar.gz
dotnetcloud restore --input /backups/dotnetcloud-2025-07-14.tar.gz
dotnetcloud backup --schedule daily --keep 30 --output /backups/
```

### What's Backed Up

- Database dump (provider-agnostic via EF Core)
- File storage snapshot
- Configuration export
- Module data (each module declares what needs backing up via `IBackupProvider` capability)

---

## 16. Logging & Observability

| Concern            | Technology                        | License    |
| ------------------ | --------------------------------- | ---------- |
| Structured logging | Serilog                           | Apache 2.0 |
| Health checks      | ASP.NET Core health checks        | MIT        |
| Metrics            | OpenTelemetry .NET SDK            | Apache 2.0 |
| Tracing            | OpenTelemetry distributed tracing | Apache 2.0 |

- Per-module configurable log levels
- `/health` endpoint per module process
- Admin UI dashboard shows module health status
- Optional export to Prometheus/Grafana
- Distributed tracing across core ↔ module gRPC calls

---

## 17. Internationalization

- Built in from Phase 0 using .NET's `IStringLocalizer` with `.resx` resource files
- Default language: English
- Community-contributed translations via Weblate or similar
- User-selectable locale and timezone (`ApplicationUser.Locale`, `ApplicationUser.Timezone`)
- All dates, times, numbers formatted per user's locale

---

## 18. CI/CD Pipeline

### Dual Pipeline

| Environment | Platform       | Purpose                                                     |
| ----------- | -------------- | ----------------------------------------------------------- |
| GitHub      | GitHub Actions | Primary public repository, community contributions, PRs, CI |

### Pipeline Stages

| Stage   | What                                                                                       |
| ------- | ------------------------------------------------------------------------------------------ |
| Build   | `dotnet build` all projects                                                                |
| Test    | Unit tests + integration tests against PostgreSQL, SQL Server, MariaDB (Docker containers) |
| Package | Build `.deb`, `.rpm`, Windows MSI, Docker images, Android APK (both flavors)               |
| Publish | Push to APT repo, Docker Hub, Google Play Store, F-Droid                                   |

---

## 19. NextCloud Migration

### Migration Tool

```sh
dotnetcloud migrate --from nextcloud --data-dir /var/www/nextcloud
```

Imports: users, files, calendars, contacts, bookmarks.

### Architecture

`IMigrationProvider` interface defined in Core. Each source platform (NextCloud, OwnCloud, future others) implements the interface. Planned for Phase 3-4.

---

## 20. Privacy & GDPR

| Requirement      | Implementation                                                  |
| ---------------- | --------------------------------------------------------------- |
| Data export      | `Settings → Privacy → Export My Data` (downloads all user data) |
| Account deletion | User can fully delete their account and all associated data     |
| Data location    | Clearly documented where all data is stored                     |
| Consent          | Cookie consent, privacy policy template                         |

---

## 21. Licensing

| Component                           | License      | Reason                                                                                    |
| ----------------------------------- | ------------ | ----------------------------------------------------------------------------------------- |
| Core server + first-party modules   | AGPL-3.0     | Prevents cloud providers from hosting without contributing back. Same model as NextCloud. |
| `DotNetCloud.Core` SDK (interfaces) | Apache 2.0   | Third-party developers can build proprietary modules if desired.                          |
| Documentation                       | CC BY-SA 4.0 | Standard open-source documentation license.                                               |

---

## 22. Dependencies

All dependencies are open source with permissive or compatible licenses. Zero cost. .NET-preferred.

| Dependency                              | Purpose                                      | License            | .NET?       |
| --------------------------------------- | -------------------------------------------- | ------------------ | ----------- |
| ASP.NET Core                            | Web framework, Kestrel, SignalR              | MIT                | ✅          |
| EF Core                                 | Database ORM                                 | MIT                | ✅          |
| Avalonia                                | Cross-platform desktop UI                    | MIT                | ✅          |
| .NET MAUI                               | Android mobile UI                            | MIT                | ✅          |
| OpenIddict                              | OAuth2/OIDC server                           | Apache 2.0         | ✅          |
| ASP.NET Core Identity                   | User management                              | MIT                | ✅          |
| Fido2NetLib                             | WebAuthn/passkey MFA                         | MIT                | ✅          |
| SignalR                                 | Real-time messaging                          | MIT                | ✅          |
| gRPC for .NET                           | Module process communication                 | Apache 2.0         | ✅          |
| SIPSorcery                              | WebRTC signaling                             | BSD-3-Clause       | ✅          |
| Serilog                                 | Structured logging                           | Apache 2.0         | ✅          |
| OpenTelemetry .NET                      | Metrics & tracing                            | Apache 2.0         | ✅          |
| Npgsql                                  | PostgreSQL provider                          | PostgreSQL License | ✅          |
| Pomelo.EntityFrameworkCore.MySql        | MariaDB provider                             | MIT                | ✅          |
| Microsoft.EntityFrameworkCore.SqlServer | SQL Server provider                          | MIT                | ✅          |
| LiveKit                                 | Video SFU (optional external)                | Apache 2.0         | ❌ (Go)     |
| Collabora CODE                          | Online document editing (optional, built-in) | MPL-2.0            | ❌ (C++/JS) |
| Collabora Online                        | Online document editing (optional, external) | MPL-2.0            | ❌ (C++/JS) |
| Microsoft.Extensions.AI                 | LLM abstraction layer                        | MIT                | ✅          |
| Microsoft.Extensions.AI.Ollama          | Ollama IChatClient                           | MIT                | ✅          |
| Microsoft.Extensions.AI.OpenAI          | OpenAI/Azure OpenAI IChatClient              | MIT                | ✅          |
| Ollama                                  | Local LLM inference (optional external)      | MIT                | ❌ (Go)     |

**Rules:**

- All dependencies must be open source with zero-cost licensing
- .NET-based solutions preferred; non-.NET only when no viable .NET alternative exists
- No NPM dependencies
- GPL-3.0 dependencies avoided due to copyleft incompatibility (AGPL-3.0 server is compatible, but GPL adds legal ambiguity for users)

---

## 23. Implementation Roadmap

### Phase 0: Foundation

**Goal:** Core platform boots, authenticates a user, loads a module, serves the Blazor UI.

- `DotNetCloud.Core` — capability interfaces, module contracts, CallerContext, event bus
- `DotNetCloud.Core.Data` — CoreDbContext with Identity, multi-provider
- `DotNetCloud.Core.Server` — process supervisor, gRPC host, OpenIddict, Identity, SignalR shell
- `DotNetCloud.UI.Web` — Blazor shell, login, admin dashboard skeleton, module plugin host
- `DotNetCloud.CLI` — `dotnetcloud setup`, `dotnetcloud serve`, `dotnetcloud module list`
- `DotNetCloud.Modules.Example` — working reference module
- i18n infrastructure
- Unit + integration tests

**Milestone:** Run `dotnetcloud setup`, create admin with MFA, log in, see Example module.

### Phase 1: Files (Public Launch)

**Goal:** File upload/download/browse/share + working desktop sync client.

- Files module (upload, download, browse, share, quotas, trash, versioning)
- File browser UI (grid/list, drag-drop upload, context menus, drag-drop move, upload queue management with pause/resume/cancel, paste-to-upload, client-side size validation, sharing, preview)
- Collabora CODE integration (WOPI host in Files module, managed component, browser-based document editing)
- Client.Core (sync engine, chunked upload, conflict detection)
- Client.SyncService + Client.SyncTray
- Full REST API with bulk operations
- Documentation: admin guide, API reference, user guide

**Milestone:** Install server on Linux, install sync client on Windows, files sync. **This is the public launch.**

### Phase 2: Chat & Notifications

**Goal:** Real-time messaging + Android app.

- Chat module (channels, DMs, typing, presence, file sharing in chat)
- Announcements module
- Chat UI (web, desktop, Android)
- Android MAUI app (chat, push notifications via FCM/UnifiedPush)
- SignalR real-time delivery

**Milestone:** Real-time chat across web, desktop, and Android.

### Phase 3: Contacts, Calendar & Notes

**Goal:** Personal information management + standards compliance.

- Contacts module (vCard, CardDAV)
- Calendar module (events, recurring, invitations, CalDAV)
- Notes module (Markdown, folders/tags, search)
- NextCloud migration tool (Phase 3-4)

**Milestone:** Full PIM suite. CalDAV/CardDAV compatibility with existing apps.

### Phase 4: Project Management (Tracks)

**Goal:** Kanban boards + Jira-like project tracking.

- Tracks module (boards, lists, cards, labels, due dates, assignments, sprints, time tracking, dependencies)
- Cross-module integration (cards reference files, chat messages)

**Milestone:** Teams manage projects with boards.

### Phase 5: Media (Photos, Music, Video)

**Goal:** Media management and playback.

- Photos module (library, albums, thumbnails, slideshow, basic editing)
- Music module (library scanning, metadata, playlists, playback, equalizer, recommendations)
- Android auto-upload (photos/videos from camera roll)

**Milestone:** Google Photos-like experience + streaming music player with equalizer.

### Phase 6: Email & Bookmarks

**Goal:** Integrated email + browser bookmark sync.

- Email module (SMTP, IMAP, Gmail API, multiple accounts, threading, search)
- Bookmarks module (storage, browser extension for Chrome/Firefox)

**Milestone:** Read/send email from DotNetCloud. Bookmarks sync across browsers.

### Phase 7: Video Calling & Screen Sharing

**Goal:** Full video conferencing.

- LiveKit integration (WebRTC signaling, P2P small calls, SFU group calls)
- Screen sharing (browser + desktop app)

**Milestone:** Video calls and screen sharing from Chat.

### Phase 8: Search, Auto-Updates & Polish

**Goal:** Cross-module search, automated updates, production hardening.

- Full-text search across all modules via `ISearchProvider`
- Auto-update CLI + admin UI + desktop client auto-update
- Performance optimization, security audit

**Milestone:** Feature-complete platform.

### Phase 9: AI Assistant

**Goal:** LLM-powered assistant with local and cloud provider support.

- AI module (`DotNetCloud.Modules.AI`) with `ILlmProvider` capability interface
- `Microsoft.Extensions.AI` abstraction with Ollama + Anthropic Claude + OpenAI providers
- Ollama integration (local or LAN, model management via admin UI)
- Cloud provider support (API keys encrypted in settings, per-user opt-in)
- Blazor chat-style assistant UI with streaming responses
- Cross-module AI features (Notes summarization, Email drafts, Search enhancement)
- Conversation history with configurable retention
- Admin controls: provider configuration, rate limiting, usage tracking, audit log

**Milestone:** Ask the AI assistant a question, get a streaming response from Ollama or Claude. Modules leverage AI for smart features.

### Phase 10: End-to-End Encryption (E2EE)

**Goal:** Optional zero-knowledge encryption for maximum privacy.

- Zero-knowledge encryption (E2EE) — optional per-user/per-folder
- Client-side encryption/decryption (Avalonia/MAUI via .NET crypto APIs)
- Server stores ciphertext only — admin cannot read user data
- Key management, recovery options, and key-loss warnings
- E2EE-incompatible features (search, thumbnails, online editing, AI) gracefully degrade

**Milestone:** Users can enable per-folder encryption with true zero-knowledge guarantees.

---

## 24. Security Hardening Notes (2026-03)

This section summarizes the March 2026 hardening pass focused on Files module tenant isolation and user impersonation resistance.

### Scope

- REST and gRPC user-scope binding to authenticated identity claims (`NameIdentifier`/`sub`)
- Owner/share permission enforcement in Files service layer
- Upload/session integrity controls for chunked transfer paths
- Regression coverage via unit and integration test suites

### Implemented Controls

- Claim-bound caller construction in REST controllers (`userId` must match authenticated claim)
- gRPC request-user validation (`request.user_id` must match authenticated claim)
- Owner-scoped lookups for sensitive node/share/trash/version operations
- Upload security gates:
  - active session required
  - session owner required
  - chunk hash must be present in manifest
  - uploaded bytes must match declared chunk hash (SHA-256)
- Service-layer permission checks expanded for comments, tags, versions, share enumeration, and chunk-hash retrieval

### Verification Artifacts

- Unit/security tests:
  - `tests/DotNetCloud.Modules.Files.Tests/Host/FilesGrpcServiceSecurityTests.cs`
- Integration tests (Files Host):
  - `tests/DotNetCloud.Integration.Tests/Api/FilesGrpcIsolationIntegrationTests.cs`
  - `tests/DotNetCloud.Integration.Tests/Api/FilesRestIsolationIntegrationTests.cs`
  - `tests/DotNetCloud.Integration.Tests/Infrastructure/FilesHostWebApplicationFactory.cs`

### Status

- gRPC isolation integration coverage: passing
- REST isolation integration coverage: passing
- Broader Files integration matrix remains tracked under `phase-1.19.2` in `docs/MASTER_PROJECT_PLAN.md`

---

## 25. Full-Text Search Architecture

The Search module provides cross-module full-text search with a three-layer architecture: **indexing pipeline**, **query engine**, and **API surface**.

### Design Goals

1. **Multi-database** — PostgreSQL (`tsvector`/`tsquery`), SQL Server (`FREETEXT`), MariaDB (`MATCH AGAINST`) with a single `ISearchProvider` interface.
2. **Event-driven indexing** — Modules publish `SearchIndexRequestEvent` on CRUD; search indexes incrementally via a bounded `Channel<T>` queue.
3. **Permission-scoped** — Every query is filtered by `OwnerId`. Users only see their own content.
4. **Module-agnostic** — Any module implementing `ISearchableModule` is automatically searchable. No compile-time dependency between modules.

### Module Structure

```
src/Modules/Search/
├── DotNetCloud.Modules.Search/           # Services, extractors, query parser, events
├── DotNetCloud.Modules.Search.Data/      # SearchDbContext, SearchIndexEntry, IndexingJob
├── DotNetCloud.Modules.Search.Host/      # REST + gRPC host (SearchController, SearchGrpcService)
└── DotNetCloud.Modules.Search.Client/    # gRPC client library (ISearchFtsClient)
```

### Indexing Pipeline

```
Module CRUD operation
  → Publishes SearchIndexRequestEvent (ModuleId, EntityId, Action)
  → SearchIndexRequestEventHandler routes to:
      Remove → ISearchProvider.RemoveDocumentAsync() (immediate)
      Index  → SearchIndexingService channel queue (bounded, 1000 capacity)
                → Fetches SearchDocument from ISearchableModule
                → ContentExtractionService extracts text (PDF, DOCX, XLSX, MD, plain text)
                → ISearchProvider.IndexDocumentAsync() (upsert by ModuleId + EntityId)
```

A `SearchReindexBackgroundService` runs scheduled full reindexes (default: 24 hours) and supports on-demand triggers via admin API. Batch size: 200 documents. Orphaned entries for unregistered modules are cleaned up automatically.

### Query Engine

`SearchQueryParser` parses user input into `ParsedSearchQuery`:

| Input | Parsed As |
|---|---|
| `quarterly report` | Terms: [quarterly, report] |
| `"project plan"` | Phrases: [project plan] |
| `in:notes` | ModuleFilter: notes |
| `type:pdf` | TypeFilter: pdf |
| `-draft` | Exclusions: [draft] |

`ParsedSearchQuery` generates provider-specific query strings:

| Provider | Format |
|---|---|
| PostgreSQL | `to_tsquery('quarterly & report & !draft')` |
| SQL Server | `CONTAINS(*, '"quarterly" AND "report" AND NOT "draft"')` |
| MariaDB | `MATCH() AGAINST('+quarterly +report -draft' IN BOOLEAN MODE)` |

`SnippetGenerator` produces XSS-safe highlighted snippets using `<mark>` tags with ~60 characters of context around matches.

### API Surface

**REST** (`/api/v1/search`): GET `/search`, GET `/suggest`, GET `/stats`, POST `/admin/reindex`, POST `/admin/reindex/{moduleId}`.

**gRPC** (`SearchService`): `Search`, `IndexDocument`, `RemoveDocument`, `ReindexModule`, `GetIndexStats`.

**Client library** (`ISearchFtsClient`): Lazy gRPC channel with graceful degradation. When unavailable, module controllers fall back to LIKE-based queries.

### Searchable Modules

Files, Notes, Chat, Contacts, Calendar, Photos, Music, Video, and Tracks all implement `ISearchableModule` and publish search index events on CRUD operations.

### Content Extraction

| Extractor | MIME Types | Library |
|---|---|---|
| PlainText | `text/plain`, `text/csv` | Built-in |
| Markdown | `text/markdown` | Regex-based |
| PDF | `application/pdf` | UglyToad.PdfPig |
| DOCX | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | DocumentFormat.OpenXml |
| XLSX | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | DocumentFormat.OpenXml |

Max extracted content: 100KB per document.

### Test Coverage

631 tests across 8 implementation phases covering providers, services, extractors, query parsing, snippet generation, API endpoints, UI logic, permission scoping, multi-database consistency, and performance benchmarks.

**Reference:** [docs/modules/SEARCH.md](../modules/SEARCH.md) | [docs/api/search.md](../api/search.md)

---

## Documentation Plan

Documentation is continuous — every phase ships with complete docs for what was built.

```
docs/
├── architecture/
│   └── ARCHITECTURE.md              ← This document
├── module-development/
│   ├── 01-getting-started.md
│   ├── 02-module-manifest.md
│   ├── 03-data-access.md
│   ├── 04-capability-interfaces.md
│   ├── 05-events.md
│   ├── 06-api-endpoints.md
│   ├── 07-ui-extensions.md
│   ├── 08-background-jobs.md
│   ├── 09-testing.md
│   ├── 10-packaging.md
│   └── example-walkthrough.md
├── admin-guide/
│   ├── installation-linux.md
│   ├── installation-windows.md
│   ├── reverse-proxy-apache.md
│   ├── reverse-proxy-iis.md
│   ├── reverse-proxy-nginx.md
│   ├── letsencrypt.md
│   ├── backup-restore.md
│   ├── module-management.md
│   ├── rate-limiting.md
│   ├── push-notifications.md
│   ├── multi-organization.md       ← Future: how to enable multi-org
│   └── troubleshooting.md
├── api-reference/
│   ├── envelope-format.md
│   ├── authentication.md
│   ├── rate-limiting.md
│   └── versioning.md
├── development/
│   ├── android-builds.md
│   ├── push-notifications.md
│   ├── ci-cd-setup.md
│   └── multi-db-testing.md
└── distribution-infrastructure/
    ├── apt-repo-hosting.md
    ├── gpg-key-management.md
    ├── install-script-hosting.md
    ├── livekit-binary-hosting.md
    └── ci-cd-package-publishing.md
```
