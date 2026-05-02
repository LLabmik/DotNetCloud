# Phase 6 Implementation Plan — Email & Bookmarks (Gmail + Rules + Rich Previews)

## Scope (what Phase 6 delivers)

**Goal (per architecture):** Integrated email + browser bookmark sync.

### Email (Phase 6)

- ☐ Email module with **multiple accounts** per user
- ☐ Providers: **IMAP (read/sync)** + **SMTP (send)** + **Gmail API** (read + send)
- ☐ **Threading** support (Gmail `threadId` + RFC headers for IMAP)
- ☐ **Search integration** (indexes email threads/messages into Search module)
- ☐ **Rules/filters engine** (server-side, first-class)
- ☐ Attachments stored via `IStorageProvider` (not in DB)
- ☐ Blazor UI for inbox, thread view, compose, account setup, and rules management

### Bookmarks (Phase 6)

- ☐ Bookmarks module with **server/web first** (browser extension deferred)
- ☐ **Private-only** bookmarks initially (owner-only access; no sharing)
- ☐ Folder hierarchy, basic CRUD
- ☐ **Import/export** (browser HTML export format as first target)
- ☐ **Rich preview scraping** (title/description/site name/favicon/preview image) with SSRF-safe constraints
- ☐ Search integration (indexes bookmarks)

## Explicit Non-Goals (Phase 6)

- ☐ No bookmarks sharing/collaboration (private-only)
- ☐ No browser extension implementation (design API seams only)
- ☐ No CalDAV/CardDAV/Exchange integration
- ☐ No full offline-first email store on clients (server-first)
- ☐ No AI drafting/summarization features (those are cross-module later)

## Key Constraints / Assumptions

- The repository already has:
  - Data Protection configured and persisted (use for encrypting secrets at rest)
  - Search module contract (`ISearchableModule`) + indexing request event pattern
  - Module patterns (Manifest + Host + Data + UI)
- All new public APIs/types must follow existing conventions (nullable, file-scoped namespaces, XML docs for public members).

## Repository Touch Points (expected new projects/files)

> Names mirror existing module patterns (Notes/Chat/Calendar).

### Email module

- ☐ `src/Modules/Email/DotNetCloud.Modules.Email/` (module core + lifecycle)
- ☐ `src/Modules/Email/DotNetCloud.Modules.Email.Data/` (EF entities, DbContext config, services)
- ☐ `src/Modules/Email/DotNetCloud.Modules.Email.Host/` (REST controllers + DI registration)
- ☐ `src/Modules/Email/DotNetCloud.Modules.Email.UI/` (Blazor pages/components)

### Bookmarks module

- ☐ `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks/`
- ☐ `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Data/`
- ☐ `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/`
- ☐ `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.UI/`

### Shared infrastructure additions (likely)

- ☐ `DotNetCloud.Core` additions for shared DTOs/events if needed (avoid leaking module internals)
- ☐ `Directory.Packages.props` additions (MailKit/MimeKit, Google APIs, HTML parser)

## Dependencies (NuGet) — candidate set

> Final package picks should match existing stack and trimming/AOT constraints.

### Email

- ☐ `MailKit` + `MimeKit` (IMAP/SMTP + MIME parsing)
- ☐ Google Gmail API client library (OAuth + Gmail endpoints)

### Bookmarks preview scraping

- ☐ HTML parser (prefer a robust one like AngleSharp) for meta tags, icons, OpenGraph

## Security Model

### Secrets at rest

- ☐ Store OAuth refresh tokens / IMAP passwords encrypted using ASP.NET Core Data Protection
- ☐ Use per-user additional entropy (e.g., user ID) when protecting blobs
- ☐ Never log secrets; ensure structured logs mask sensitive fields

### SSRF-safe preview fetching

- ☐ Validate URL scheme: `http`/`https` only
- ☐ Block private/loopback/link-local/multicast IP ranges (including IPv6)
- ☐ Resolve DNS safely and re-check resolved IPs before connect
- ☐ Disable automatic proxy usage unless explicitly configured
- ☐ Enforce strict timeouts (connect + overall)
- ☐ Enforce response size limit (e.g., HTML ≤ 1 MB)
- ☐ Restrict redirects (e.g., max 5; each redirect re-validated)
- ☐ Restrict content-types for HTML parsing (`text/html`, `application/xhtml+xml`)
- ☐ Fetch images/icons via the same safety pipeline (or store only URLs initially)

## Email — Architecture & Data Model

### Entities (Email.Data)

#### Email accounts

- ☐ `EmailAccount`
  - Provider: `ImapSmtp` | `Gmail`
  - Display name, email address
  - Encrypted credentials blob (provider-specific)
  - Sync state (last sync watermark/cursor)
  - Flags: enabled/disabled

#### Mailboxes / Labels

- ☐ `EmailMailbox` (IMAP folders / Gmail system labels)
  - Provider mailbox id/name
  - Display name
  - Sync flags

#### Messages & Threads

- ☐ `EmailThread`
  - Provider thread id (Gmail)
  - Computed thread key (IMAP): derived from `Message-Id`/`References`/`In-Reply-To`
  - Subject canonicalization fields
  - Timestamps (first/last message)

- ☐ `EmailMessage`
  - Provider message id (IMAP UID + mailbox + UIDVALIDITY; Gmail message id)
  - Thread FK
  - Headers summary (from/to/cc/bcc), subject
  - Snippet/preview text
  - Date received/sent
  - Flags (read/starred/important)
  - Body storage strategy:
    - Option A: store normalized plain-text body (size-capped) for search
    - Option B: store body in storage provider as separate object (safer for DB size)

#### Attachments

- ☐ `EmailAttachment`
  - Message FK
  - Filename, content-type, size
  - Storage object key (via `IStorageProvider`)
  - Optional: content hash

### Provider abstraction

- ☐ Define `IEmailProvider` (internal to Email module)
  - `SyncAsync(accountId, cancellationToken)`
  - `SendAsync(accountId, EmailSendRequest, cancellationToken)`
  - `ListMailboxesAsync(...)`
  - `ApplyActionsAsync(...)` (mark read, move, label, etc.)

- ☐ Implement `ImapSmtpEmailProvider` using MailKit
- ☐ Implement `GmailEmailProvider` using Gmail API

### Sync strategy

- ☐ Background sync service per account (or per user) using `BackgroundService` + `PeriodicTimer`
- ☐ Use incremental sync:
  - IMAP: track UIDVALIDITY + last seen UID per mailbox
  - Gmail: track `historyId` and use History API if available; fallback to query windows
- ☐ Normalize inbound messages to module entities
- ☐ Emit search indexing events for affected messages/threads
- ☐ Run rules engine on new/changed messages after ingest

## Email — Rules/Filters Engine

### Rule model

- ☐ `EmailRule`
  - Owner user id
  - Target account (optional: all accounts)
  - Enabled
  - Priority/order
  - Stop-processing flag

- ☐ `EmailRuleConditionGroup`
  - Match mode: ALL / ANY

- ☐ `EmailRuleCondition`
  - From contains / equals
  - To contains / equals
  - Subject contains
  - Body contains (optional; only if body indexed)
  - Has attachment
  - Size greater-than
  - Mailbox/label matches
  - Received time window

- ☐ `EmailRuleAction`
  - Mark read/unread
  - Star/unstar
  - Move to mailbox (IMAP) / Apply label (Gmail)
  - Archive (Gmail)
  - Delete (optional; consider deferring destructive actions initially)

### Execution points

- ☐ On sync ingest: evaluate rules for each new message
- ☐ Manual re-run: “Run rules now” endpoint on account/mailbox
- ☐ On user action: when user moves/labels a message, optionally skip rules to avoid loops

### Provider capability mapping

- ☐ Define a capability matrix so UI only shows actions supported by the provider
- ☐ For unsupported actions, block at API validation time

### Safety & observability

- ☐ Keep a rule execution log (per message) with outcome + provider errors (no content)
- ☐ Avoid infinite loops: track message last-rule-run watermark; don’t re-apply within same sync cycle

## Email — REST API (Host)

> Names are illustrative; follow existing API versioning patterns.

### Account endpoints

- ☐ `POST /api/v1/email/accounts` (create account)
- ☐ `GET /api/v1/email/accounts` (list)
- ☐ `PATCH /api/v1/email/accounts/{id}` (enable/disable, rename)
- ☐ `DELETE /api/v1/email/accounts/{id}`

### OAuth (Gmail)

- ☐ `POST /api/v1/email/gmail/oauth/start` (returns auth URL + state)
- ☐ `POST /api/v1/email/gmail/oauth/complete` (exchange code, store refresh token)

### Mailboxes & messages

- ☐ `GET /api/v1/email/accounts/{id}/mailboxes`
- ☐ `GET /api/v1/email/accounts/{id}/mailboxes/{mailboxId}/threads` (paged)
- ☐ `GET /api/v1/email/threads/{threadId}` (thread details)
- ☐ `POST /api/v1/email/threads/{threadId}/actions` (mark read, move, label)

### Compose / send

- ☐ `POST /api/v1/email/accounts/{id}/send`

### Rules

- ☐ `GET /api/v1/email/rules`
- ☐ `POST /api/v1/email/rules`
- ☐ `PATCH /api/v1/email/rules/{ruleId}`
- ☐ `DELETE /api/v1/email/rules/{ruleId}`
- ☐ `POST /api/v1/email/rules/run` (optional: scoped to account/mailbox)

## Email — UI (Blazor)

### Pages (Email)

- ☐ Accounts management (add IMAP/SMTP, connect Gmail)
- ☐ Inbox list per account/mailbox
- ☐ Thread view (messages + attachments)
- ☐ Compose view (reply/forward/new)
- ☐ Rules editor (conditions/actions + priority ordering)

### UX requirements (baseline)

- ☐ Fast perceived load: skeletons for thread list/thread view
- ☐ Clear account/mailbox switching
- ☐ Safe error states: auth expired, provider unreachable, partial sync

## Bookmarks — Architecture & Data Model

### Entities (Bookmarks.Data)

- ☐ `BookmarkFolder`
  - Owner user id
  - Parent folder id (nullable for root)
  - Name
  - Sort order

- ☐ `BookmarkItem`
  - Owner user id
  - Folder FK
  - URL (normalized)
  - Title (user override + scraped title)
  - Description/notes
  - Created/updated

- ☐ `BookmarkPreview`
  - Bookmark FK
  - Fetched at, status (Ok/Failed/NotFetched)
  - Canonical URL
  - Site name
  - Resolved title/description
  - Favicon URL and/or stored blob key
  - Preview image URL and/or stored blob key
  - ETag/Last-Modified (optional for conditional fetch)

### Preview scraping service

- ☐ `BookmarkPreviewFetchService`
  - Fetch HTML via safe HTTP pipeline (see SSRF rules)
  - Parse:
    - `<title>`
    - `meta[name=description]`
    - OpenGraph: `og:title`, `og:description`, `og:site_name`, `og:image`
    - Twitter cards (fallback)
    - Icon links (`rel=icon`, `rel=apple-touch-icon`)
  - Prefer canonical URL (`link[rel=canonical]`)
  - Persist results and emit search indexing event

### Refresh strategy

- ☐ Fetch on create/update URL
- ☐ Background refresh for stale previews (e.g., weekly) with backoff on failures
- ☐ Manual “Refresh preview” action

## Bookmarks — REST API (Host)

### Folders

- ☐ `GET /api/v1/bookmarks/folders`
- ☐ `POST /api/v1/bookmarks/folders`
- ☐ `PATCH /api/v1/bookmarks/folders/{id}`
- ☐ `DELETE /api/v1/bookmarks/folders/{id}`

### Items

- ☐ `GET /api/v1/bookmarks` (paged; optional folder filter)
- ☐ `POST /api/v1/bookmarks`
- ☐ `PATCH /api/v1/bookmarks/{id}`
- ☐ `DELETE /api/v1/bookmarks/{id}`

### Preview

- ☐ `POST /api/v1/bookmarks/{id}/preview/refresh`

### Import/export

- ☐ `POST /api/v1/bookmarks/import/html` (browser export)
- ☐ `GET /api/v1/bookmarks/export/html`

### Future extension seam (design only)

- ☐ Define but do not implement: delta sync endpoints using cursor/versioning
  - `GET /api/v1/bookmarks/sync/changes?cursor=...`
  - `POST /api/v1/bookmarks/sync/commit`

## Bookmarks — UI (Blazor)

### Pages (Bookmarks)

- ☐ Folder tree navigation
- ☐ Bookmarks list/grid with preview tiles (title, site, favicon, optional image)
- ☐ Add/edit bookmark modal/page
- ☐ Import/export UI

### Preview UI states

- ☐ Not fetched yet / fetching / failed (with retry)
- ☐ Show favicon even if preview image unavailable

## Search Integration (Email + Bookmarks)

### Search documents

- ☐ Email: index per thread and/or per message (pick one, document rationale)
  - Thread indexing generally reduces duplication and matches user mental model
- ☐ Bookmarks: index per bookmark

### Indexing triggers

- ☐ Publish `SearchIndexRequestEvent` on:
  - Email message/thread created/updated
  - Bookmark created/updated
  - Preview fetched/updated

## Testing Strategy

### Unit tests

- ☐ Rules engine: condition matching + action planning + priority/stop-processing
- ☐ URL safety validator: blocks private IPs, weird schemes, redirect validation
- ☐ Preview parsing: extracts OG/meta correctly from representative HTML fixtures

### Integration tests

- ☐ Bookmarks API CRUD + preview refresh (with mocked HTTP)
- ☐ Email sync pipeline pieces with fakes (provider interface mocked)

### Manual verification checklist

- ☐ Gmail connect and token refresh
- ☐ IMAP sync incremental behavior
- ☐ Send mail via SMTP + Gmail
- ☐ Rules apply correctly and do not loop
- ☐ Bookmark preview fetch respects SSRF constraints

## Milestone Breakdown (implementation sequence)

### Milestone 6.1 — Skeletons + Contracts

- ☐ Create Email + Bookmarks module project structure
- ☐ Define DTOs + initial controllers returning stub data
- ☐ Wire DI registrations and module manifests

### Milestone 6.2 — Bookmarks CRUD (private-only)

- ☐ EF entities + migrations
- ☐ Folder + bookmark CRUD APIs
- ☐ Minimal UI for folder tree + list

### Milestone 6.3 — Bookmarks rich previews (SSRF-safe)

- ☐ Safe HTTP fetch pipeline + HTML parsing
- ☐ Preview persistence + UI states
- ☐ Background refresh + manual refresh

### Milestone 6.4 — Email account management

- ☐ EF entities for accounts + encrypted secret blobs
- ☐ IMAP/SMTP account setup flow
- ☐ Gmail OAuth start/complete endpoints

### Milestone 6.5 — Email sync ingest

- ☐ Background sync service
- ☐ Normalize messages/threads + attachments storage
- ☐ Thread list + thread view UI

### Milestone 6.6 — Send/compose

- ☐ SMTP send
- ☐ Gmail send
- ☐ Compose UI

### Milestone 6.7 — Rules/filters

- ☐ Rules data model + CRUD
- ☐ Rule evaluation during sync + manual run
- ☐ UI rule editor

### Milestone 6.8 — Search integration

- ☐ Implement `ISearchableModule` for Email + Bookmarks
- ☐ Publish indexing events on changes
- ☐ Verify cross-module search results quality

## Open Design Choices (resolve early)

- ☐ Email body storage (DB vs storage provider) + search implications
- ☐ Gmail incremental sync approach (History API vs time-window polling)
- ☐ Rules destructive actions (delete) in initial release vs deferred
- ☐ Preview assets storage: store URLs only vs store icon/image blobs

## Deliverable Output

When Phase 6 implementation begins, update tracking docs:

- ☐ `docs/MASTER_PROJECT_PLAN.md` — add Phase 6 step breakdown and statuses
- ☐ `docs/IMPLEMENTATION_CHECKLIST.md` — add granular Phase 6 tasks
