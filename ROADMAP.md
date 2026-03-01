# DotNetCloud — Implementation Roadmap

> **Version:** 1.0  
> **Status:** Approved  
> **Last Updated:** 2025-07-14  
> **Full Architecture:** [ARCHITECTURE.md](docs/architecture/ARCHITECTURE.md)

---

## Phases Overview

| Phase | Deliverable | Key Milestone |
|---|---|---|
| **0** | Foundation | Architecture proven end-to-end |
| **1** | Files + Sync Client | **Public launch** |
| **2** | Chat + Notifications + Android | Real-time messaging across all platforms |
| **3** | Contacts + Calendar + Notes | Full PIM suite, CalDAV/CardDAV |
| **4** | Project Management (Deck) | Kanban + Jira-like tracking |
| **5** | Media (Photos, Music, Video) | Media management + streaming playback |
| **6** | Email + Bookmarks | Integrated email, browser sync |
| **7** | Video Calling | WebRTC P2P + LiveKit SFU |
| **8** | Search + Updates + E2EE | Feature-complete platform |

---

## Phase 0: Foundation

### Goal
Core platform boots, authenticates a user, loads a module, serves the Blazor UI. Nothing useful yet — but the architecture works end to end.

### Deliverables

- [ ] `DotNetCloud.Core` — Capability interfaces (`IUserDirectory`, `IStorageProvider`, `IEventBus`, `INotificationService`, `ISearchProvider`, `IRealtimeBroadcaster`), module contracts (`IModule`, `IModuleManifest`, `CapabilityRequest`), `CallerContext`, event base types, DTOs
- [ ] `DotNetCloud.Core.Data` — `CoreDbContext` with ASP.NET Core Identity tables (`ApplicationUser`, `ApplicationRole`), `Organization`, `Team`, `Group`, `Permission`, `InstalledModule`, `ModuleCapabilityGrant`, `SystemSetting`, `OrganizationSetting`, `UserSetting`, `UserDevice`. Multi-provider tested (PostgreSQL, SQL Server, MariaDB).
- [ ] `DotNetCloud.Core.Server` — Process supervisor (spawn, monitor, restart module processes), gRPC capability proxy, OpenIddict configuration, ASP.NET Core Identity setup with MFA (TOTP), SignalR hub shell, module UI plugin host
- [ ] `DotNetCloud.Core.ServiceDefaults` — Serilog structured logging, OpenTelemetry metrics/tracing, ASP.NET Core health checks
- [ ] `DotNetCloud.UI.Web` — Blazor app shell (login page, MFA enrollment, admin dashboard skeleton, module navigation, plugin host for module UI extensions)
- [ ] `DotNetCloud.CLI` — `dotnetcloud setup` (interactive wizard: DB, admin account, domain, TLS, reverse proxy, modules), `dotnetcloud serve`, `dotnetcloud status`, `dotnetcloud module list/start/stop/restart`
- [ ] `DotNetCloud.Modules.Example` — Fully working, well-commented reference module demonstrating: manifest declaration, capability usage, own DbContext with migrations, API endpoints, Blazor UI page, dashboard widget, event publishing/subscribing, background job
- [ ] i18n infrastructure — `IStringLocalizer` with `.resx`, user locale/timezone support
- [ ] Tests — Core unit tests, integration tests (module loaded via gRPC, multi-DB provider matrix)
- [ ] Documentation — Architecture doc, module developer guide (chapters 1-10), example walkthrough

### Milestone
Run `dotnetcloud setup`, create admin with MFA, log in to Blazor UI, see Example module's page and dashboard widget. Module loaded in separate process via gRPC. Architecture proven.

---

## Phase 1: Files (Public Launch)

### Goal
Users can upload, download, browse, and share files through the web UI. Desktop sync client works on Windows and Linux.

### Deliverables

- [ ] `DotNetCloud.Modules.Files` — Upload (chunked, resumable), download, browse (folder tree), rename, move, delete, share (user/team/public link with expiry), storage quotas, trash/recycle bin, file versioning (configurable history depth)
- [ ] `DotNetCloud.Modules.Files.Data` — `FilesDbContext`: file metadata, chunk records (SHA-256 content hashes), share links, version history, quota tracking
- [ ] `DotNetCloud.UI.Modules.Files` — File browser (grid + list view), drag-and-drop upload, sharing dialog, file preview (images, text, PDF, video), breadcrumb navigation, context menus
- [ ] `DotNetCloud.Client.Core` — Sync engine (hybrid change detection, chunked upload with content hashing, conflict detection + copy with notification), API client (`HttpClient` wrapper for REST API), OAuth2 PKCE token management, local SQLite state database
- [ ] `DotNetCloud.Client.SyncService` — .NET Worker Service: background file watcher, sync worker, IPC status server (named pipe/unix socket). Runs as Windows Service / systemd user service.
- [ ] `DotNetCloud.Client.SyncTray` — Avalonia tray app: tray icon with sync status (syncing/up-to-date/error), settings window (folder selection, account, selective sync per folder), IPC client to SyncService
- [ ] REST API — `/api/v1/files/*` with full CRUD, bulk operations (move/delete/copy), sync reconcile endpoint
- [ ] `IStorageProvider` implementation — Local filesystem (primary), namespace isolation per module
- [ ] Tests — Files module unit tests, sync engine tests, API integration tests, conflict resolution tests
- [ ] Documentation — Admin guide (installation Linux + Windows), API reference, user guide (web UI + sync client)

### Milestone
Install DotNetCloud on a Linux server. Install sync client on Windows desktop. Select folders. Files sync bidirectionally. Upload via web UI appears on desktop. Edit on desktop updates on server. Conflicts detected and resolved with notification. **This is the public launch.**

---

## Phase 2: Chat & Notifications

### Goal
Real-time messaging between users. Android app launched.

### Deliverables

- [ ] `DotNetCloud.Modules.Chat` — Channels (team/private/public), direct messages, message history with pagination, typing indicators, user presence (online/away/offline), file sharing in chat (via `IFileService`), message editing/deletion, message search, emoji reactions
- [ ] `DotNetCloud.Modules.Announcements` — Admin broadcasts to users/teams/org, read receipts, priority levels
- [ ] `DotNetCloud.UI.Modules.Chat` — Chat interface (channel list sidebar, message thread, message composer with file attach, emoji picker, typing indicator, presence dots)
- [ ] `DotNetCloud.Client.Desktop` — Chat panel in full Avalonia app, notification integration with OS
- [ ] `DotNetCloud.Client.Mobile` — Android MAUI app: chat, push notifications (FCM + UnifiedPush), file browsing, account setup via OAuth2 PKCE
- [ ] SignalR integration — Real-time message delivery, presence tracking, typing indicators. Core routes SignalR events to/from Chat module via gRPC.
- [ ] Push notifications — `IPushNotificationService` with FCM and UnifiedPush implementations. Android build flavors (`googleplay`/`fdroid`).
- [ ] Tests — Chat module tests, SignalR integration tests, Android UI tests
- [ ] Documentation — Chat user guide, Android installation, push notification configuration

### Milestone
Users chat in real-time through web UI, desktop app, and Android app. Push notifications work on Android.

---

## Phase 3: Contacts, Calendar & Notes

### Goal
Personal information management. Standards compliance for interoperability.

### Deliverables

- [ ] `DotNetCloud.Modules.Contacts` — Contact CRUD, scoped to user/team/org, vCard import/export, groups, search, avatar support, **CardDAV server** (interop with Thunderbird, Android contacts)
- [ ] `DotNetCloud.Modules.Calendar` — Events, recurring events (RRULE), invitations with RSVP, shared calendars, timezone handling, reminders, **CalDAV server** (interop with Thunderbird, GNOME Calendar, Android calendar)
- [ ] `DotNetCloud.Modules.Notes` — Markdown notes, folders/tags organization, full-text search within notes, note sharing (user/team/link), version history
- [ ] UI + Client extensions — Web UI, desktop panels, Android views for all three modules
- [ ] NextCloud migration tool — `dotnetcloud migrate --from nextcloud`: import users, files, calendars, contacts, bookmarks
- [ ] Tests — Per-module tests, CalDAV/CardDAV compliance tests
- [ ] Documentation — PIM user guide, CalDAV/CardDAV configuration, NextCloud migration guide

### Milestone
Full PIM suite. CalDAV/CardDAV means existing apps (Thunderbird, Android contacts/calendar) sync with DotNetCloud.

---

## Phase 4: Project Management (Deck)

### Goal
Kanban boards with Jira-like project tracking.

### Deliverables

- [ ] `DotNetCloud.Modules.Deck` — Boards, lists, cards, labels/tags, due dates, user assignments, comments, checklists, card attachments (via `IFileService`), sprint planning, time tracking, card dependencies, card templates, board templates
- [ ] Cross-module integration — Cards reference files (Files module), card activity posts to chat (Chat module via event bus)
- [ ] UI — Drag-and-drop board UI, card detail modal, filters/search, timeline/Gantt view, sprint board view
- [ ] Tests — Deck module tests, cross-module integration tests
- [ ] Documentation — Deck user guide, project management workflows

### Milestone
Teams manage projects with boards. Cards integrate with Files and Chat.

---

## Phase 5: Media (Photos, Music, Video)

### Goal
Media management, playback, and smart features.

### Deliverables

- [ ] `DotNetCloud.Modules.Photos` — Photo/video library auto-organized by date/location, albums, thumbnail generation, slideshow, basic editing (crop, rotate, filters), EXIF metadata extraction
- [ ] `DotNetCloud.Modules.Music` — Library scanning, ID3/metadata extraction, artist/album/genre organization, playlists (manual + smart), audio playback in browser, **audio equalizer**, music analysis for listening recommendations
- [ ] Android — Auto-upload photos/videos from camera roll (MediaStore observer), photo gallery view
- [ ] UI — Photo grid with lightbox viewer, music player with playback controls + equalizer + playlist management
- [ ] Tests — Media module tests, thumbnail generation tests, audio playback tests
- [ ] Documentation — Media user guide, Android auto-upload configuration

### Milestone
Google Photos-like experience for photos. Streaming music player with equalizer and playlists.

---

## Phase 6: Email & Bookmarks

### Goal
Integrated email client and cross-browser bookmark sync.

### Deliverables

- [ ] `DotNetCloud.Modules.Email` — SMTP send, IMAP receive, Gmail API integration, multiple account support, conversation threading, search, attachments (via `IStorageProvider`), HTML + plain text compose, signature management. Future: Microsoft email integration.
- [ ] `DotNetCloud.Modules.Bookmarks` — Bookmark storage, folders/tags, import/export (HTML standard), **browser extension** (Chrome + Firefox) for bidirectional sync
- [ ] UI — Email client interface (inbox, compose, folders, search), bookmark manager
- [ ] Tests — Email module tests (IMAP/SMTP mocking), bookmark sync tests
- [ ] Documentation — Email setup guide (IMAP/SMTP config, Gmail API), browser extension installation

### Milestone
Read/send email from DotNetCloud web UI. Bookmarks sync across browser instances.

---

## Phase 7: Video Calling & Screen Sharing

### Goal
Full video conferencing integrated with Chat.

### Deliverables

- [ ] LiveKit integration — Automated download/config during `dotnetcloud setup`, managed as component under process supervisor
- [ ] WebRTC signaling — SIPSorcery-based signaling server in Chat module
- [ ] Hybrid calling — P2P for 1-3 participants, automatic switch to LiveKit SFU for 4+
- [ ] Screen sharing — Browser-based (Screen Capture API), desktop app (Avalonia screen capture)
- [ ] UI — Call UI (video grid, mute/unmute, screen share toggle, participant list), call initiation from Chat
- [ ] Tests — Signaling tests, call lifecycle tests
- [ ] Documentation — Video calling admin guide, LiveKit configuration, firewall/NAT requirements

### Milestone
Video calls and screen sharing directly from Chat or standalone.

---

## Phase 8: Search, Auto-Updates & Polish

### Goal
Cross-module search, automated updates, encryption, production hardening.

### Deliverables

- [ ] Full-text search — `ISearchProvider` implementations (per DB engine + optional external like Lucene.NET). Search across files (content + metadata), chat messages, notes, contacts, calendar, email.
- [ ] Auto-updates — `dotnetcloud update` CLI command, admin UI update panel, desktop client auto-updater, Android via Play Store/F-Droid
- [ ] Zero-knowledge encryption (E2EE) — Optional per-user/per-folder. Client-side encrypt/decrypt. Server stores ciphertext. Key management UX. Documentation of feature limitations when E2EE is enabled.
- [ ] Performance optimization — Caching strategy, query optimization, load testing
- [ ] Security audit — Dependency audit, penetration testing, CVE monitoring
- [ ] Tests — Search accuracy tests, E2EE round-trip tests, upgrade path tests
- [ ] Documentation — Search configuration, E2EE user guide, update procedures, security practices

### Milestone
Feature-complete platform. Cross-module search works. Updates are seamless. E2EE available for privacy-conscious users.

---

## Cross-Cutting (Every Phase)

These are not phases — they are continuous requirements:

- [ ] **Documentation** — Every phase ships with complete docs for what was built
- [ ] **Unit tests** — Business logic coverage per module
- [ ] **Integration tests** — API endpoints, cross-module, multi-DB provider matrix
- [ ] **End-to-end tests** — Playwright for Blazor UI (added as appropriate)
- [ ] **CI/CD** — Gitea Actions (local) + GitHub Actions (community). Build → Test → Package → Publish.
- [ ] **Security** — Dependency scanning, code signing, capability enforcement validation
- [ ] **i18n** — New UI strings always go through `IStringLocalizer`
