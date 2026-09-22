# Chat Module — Admin Settings (Limits, Retention & Archiving)

> **Created:** 2026-09-22
> **Branch:** `feature/new-admin-settings`
> **Status:** Implemented (build + unit/integration tests green); live verification pending deploy
> **Scope:** Server (Chat module + core admin settings + Blazor admin UI)

---

## Goal

Give administrators control over how much chat data the server keeps:

| Requirement                               | Setting(s)                                                            |
| ----------------------------------------- | --------------------------------------------------------------------- |
| Max messages                              | `Limits:MaxMessagesPerChannel`                                        |
| Lifetime of messages                      | `Retention:MessageLifetimeDays`                                       |
| Maximum attachments per channel           | `Limits:MaxAttachmentsPerChannel`                                     |
| Maximum size of attachments per channel   | `Limits:MaxAttachmentStoragePerChannelMb`                             |
| Archiving of old messages and attachments | `Retention:Enabled`, `Retention:Mode`, `Retention:ArchiveAttachments` |

Supporting limits were added where the same mechanism already needed them: maximum message
length, attachments per message, and size of a single attachment.

## Settings reference

All values live in the core `SystemSettings` table under module `dotnetcloud.chat`, are edited on
the **`/admin/chat`** page, and are read by `ChatSettingsProvider`. Row values are strings; the
provider parses, clamps and defaults them.

| Key                                       | Type | Default   | Meaning                                                          |
| ----------------------------------------- | ---- | --------- | ---------------------------------------------------------------- |
| `Limits:MaxMessageLength`                 | int  | `10000`   | Characters per message (1–10000).                                |
| `Limits:MaxMessagesPerChannel`            | int  | `0`       | Live messages kept per channel. `0` = unlimited.                 |
| `Limits:MaxAttachmentsPerMessage`         | int  | `10`      | Attachments on one message (1–100).                              |
| `Limits:MaxAttachmentsPerChannel`         | int  | `0`       | Attachments in a channel. `0` = unlimited.                       |
| `Limits:MaxAttachmentSizeMb`              | int  | `10`      | Size of one attachment (1–64 MB).                                |
| `Limits:MaxAttachmentStoragePerChannelMb` | int  | `0`       | Total attachment bytes per channel. `0` = unlimited.             |
| `Retention:Enabled`                       | bool | `false`   | Master switch for the automatic sweep.                           |
| `Retention:MessageLifetimeDays`           | int  | `0`       | Age at which messages expire. `0` = keep forever.                |
| `Retention:Mode`                          | enum | `Archive` | `Archive` (hide, keep) or `Purge` (delete permanently).          |
| `Retention:ArchiveAttachments`            | bool | `true`    | Keep attachments of archived messages; when false their rows go. |
| `Retention:SweepIntervalMinutes`          | int  | `60`      | How often the sweep runs (1–1440).                               |

Defaults are seeded by `DbInitializer.SeedSystemSettingsAsync` (insert-only, so existing
installs keep their current values).

### Resolution order

1. `SystemSettings` row for `dotnetcloud.chat` (the admin value — source of truth).
2. `Chat:*` configuration (e.g. `Chat:Limits:MaxMessageLength`), for standalone module hosts.
3. Built-in default in `ChatSettings`.

Resolved values are clamped by `ChatSettings.Normalized()`; the `/admin/chat` page shows the
_effective_ policy (post-clamp) via `GET /api/v1/chat/admin/settings/effective`.

## Enforcement

### Write time (`MessageService`, `LocalChatImageStore`)

- Content longer than `MaxMessageLength` → rejected (send **and** edit).
- More than `MaxAttachmentsPerMessage` → rejected.
- A single attachment larger than `MaxAttachmentSizeMb` → rejected (both inline attachments and
  image uploads, which resolve the same limit).
- Writes that would push the channel past `MaxAttachmentsPerChannel` or
  `MaxAttachmentStoragePerChannelMb` → rejected.

Failures surface as `400 VALIDATION_ERROR` through the existing controller mapping
(`ArgumentException`). The per-channel **message** cap is deliberately _not_ enforced at write
time: a channel that reaches it stays writable and the sweep expires its oldest messages instead.

Only attachments on **live** messages count toward the channel attachment budget, so archiving or
purging old messages frees capacity again.

### Sweep time (`ChatRetentionService`)

Runs in the process-isolated Chat module host only (`ChatRetentionBackgroundService`), so the
policy is applied exactly once per deployment even though Core.Server also builds an in-process
chat container for the Blazor UI. The sweep:

1. Skips entirely unless `Retention:Enabled` and at least one of the lifetime / message-count
   limits is set.
2. Finds candidate channels with two aggregate queries (aged messages, over-cap channels) rather
   than one pass per channel.
3. For each candidate channel collects the messages that are older than the lifetime cutoff or
   beyond the newest `MaxMessagesPerChannel`, bounded to 5 000 messages per channel per sweep.
4. Removes pins for expired messages (the pin FK is `RESTRICT` and a pin pointing at a hidden
   message would render as a broken entry).
5. Applies the mode:
   - **Archive** — sets `Message.ArchivedAt`; the message stays in the table but drops out of
     every normal read and search (global query filter), so it can be recovered. When
     `Retention:ArchiveAttachments` is false, the attachment rows are deleted and only the text
     is kept.
   - **Purge** — deletes pin rows, detaches replies (`ReplyToMessageId` → `null`, because the
     self-FK is `RESTRICT`), deletes attachment rows and then the messages. Irreversible.
6. Publishes `SearchIndexRequestEvent` (`Remove`) per expired message so the search index follows.

Bulk `ExecuteUpdate`/`ExecuteDelete` are used throughout, so a large sweep does not materialize
every message in the change tracker. The interval is re-read each pass, so changing it (or
disabling retention) applies on the next tick without restarting the module.

## Data model change

One nullable column plus one index, migrated for both providers:

- `Message.ArchivedAt` (`timestamp with time zone` / `datetime2`, nullable).
- `MessageConfiguration` query filter extended to `!IsDeleted && ArchivedAt == null`.
- Index `ix_chat_messages_channel_archived_sent` on `(ChannelId, ArchivedAt, SentAt)`.

Migrations: `AddMessageArchiving` (PostgreSQL, `Chat.Data/Migrations`) and
`AddMessageArchiving_SqlServer` (`Chat.Data.SqlServer/Migrations`).

## API surface

| Method | Route                                   | Policy         | Purpose                             |
| ------ | --------------------------------------- | -------------- | ----------------------------------- |
| GET    | `/api/v1/chat/admin/settings/effective` | `RequireAdmin` | Effective (clamped) policy readout. |
| POST   | `/api/v1/chat/admin/retention/sweep`    | `RequireAdmin` | Run the sweep now.                  |

Both are proxied to the Chat module by Core.Server's module proxy. The rest of the settings
surface is the existing generic core admin API (`/api/v1/core/admin/settings/...`).

## Admin UI

`src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/ChatSettings.razor` (`/admin/chat`), plus a nav
entry next to Video. Three sections — Message Limits, Attachment Limits, Retention & Archiving —
with client-side range validation, a warning when retention is enabled but nothing can expire, a
"Currently Enforced" readout, and a "Run retention sweep now" button. The page uses
`DotNetCloudApiClient` (not `IAdminSettingsService`) so it works in both server and WASM render
paths, and degrades gracefully when the Chat module is not reachable.

`/admin/push-notifications` cross-links to `/admin/chat` and the generic key/value editor.

## Deliberate non-changes

- **No schema change for attachments.** Archiving is message-level; attachments follow their
  message. A per-attachment archive column would have added a second migration for no additional
  capability.
- **No core → chat gRPC RPC.** The sweep endpoint is served by the module host and reached through
  the existing module proxy, so no new gRPC contract was needed.
- **`ChatSettingsProvider` caches for 30 s** (configurable via the internal constructor) rather
  than caching for the lifetime of the DI scope, because a Blazor circuit can hold a scope for
  hours. `Invalidate()` forces a re-read.
- **Retention runs only in the module host.** Running it in Core.Server too would double-sweep the
  same database.

## Verification

- `ChatSettingsProviderTests` — defaults, DB-over-config precedence, config fallback when the
  service is missing or throws, cross-module row rejection, clamping, `Invalidate`, cache TTL.
- `ChatRetentionServiceTests` — SQLite-backed so the bulk statements actually execute: lifetime
  archive/purge, count-based cap keeping the newest, attachment retention vs removal, pin removal,
  reply detach on purge, search-index events, skip-when-disabled.
- `MessageServiceChatLimitTests` — every write-time limit, boundary values, default limits when no
  provider is wired, and that settings are read once per send.
- Full solution build (Release) and the Chat / Core.Data / Core.Server / Core.Auth / UI.Shared /
  Chat-integration suites are green.
