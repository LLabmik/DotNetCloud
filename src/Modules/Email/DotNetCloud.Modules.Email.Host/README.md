# DotNetCloud.Modules.Email.Host

> **Purpose:** Email module host process — sandboxed gRPC + web host for email operations and its own UI
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The host process for the Email module. Runs as a separate process managed by `ProcessSupervisor`, and
since Track C it is fully sandboxed: it receives no core credentials, owns its own migrations and
serves its own web UI.

## Sandbox model (Track C)

- **Configuration.** A sandboxed launch reads the non-secret topology from
  `DOTNETCLOUD_MODULE_CONFIG_DIR/module.json`; the legacy shared `config.json` is only read when the
  supervisor still starts the module in compatibility mode.
- **Database.** The connection string arrives in `DOTNETCLOUD_MODULE_DB_CONNECTION_STRING` (its own
  principal, its own schema, `search_path` pinned). The model **and** both provider migrations are
  schema-neutral, so unqualified DDL — including `__EFMigrationsHistory` — lands in the schema the
  core provisioned. `EmailDbInitializer` applies the module's own migrations with retry, then keeps
  running in degraded mode if the database is unavailable.
- **Identity.** No cookie scheme. The core proxy strips the caller's session and mints a short-lived,
  module-audience `dnc-user-assertion`; `EmailControllerBase.GetAuthenticatedCaller()` reads it, and
  a request without one is answered `401` (never `500`).
- **DataProtection.** Keys live under `DOTNETCLOUD_MODULE_DATA_DIR/data-protection-keys`, private to
  the module. They protect stored provider credentials and sign the Gmail OAuth `state` value.
- **Files.** Attachment blobs are written under `DOTNETCLOUD_MODULE_DATA_DIR/email-attachments`
  (content-addressed by SHA-256). Browsing the user's cloud files and saving an attachment back
  into them goes through the **core**, never to the Files module: `IFileDirectory` (list/read) and
  `IFileWriter` (save) are declared in `manifest.json`, reached with `IDncModuleApi`, and gated by
  the user's `files:read` / `files:write` consent. The module holds no Files credential, no Files
  socket and no Files schema.
- **Erasure.** `EmailUserErasureHandler` deletes the deleted user's accounts, mailboxes, threads,
  messages, attachments (and their blobs) and rules.

## UI plane

`wwwroot/` is this module's own web app, framed by the core at `/apps/dotnetcloud.email/`:

| Path                    | Contents                                                       |
| ----------------------- | -------------------------------------------------------------- |
| `index.html`            | Shell: sidebar (accounts, folders, tools) + panel               |
| `assets/email-ui.js`    | The app (accounts, threads, compose, rules, help)               |
| `assets/email-ui.css`   | Tokens, light/dark themes, layout                               |
| `widget/index.html`     | Home-page card: the five most recent inbox threads              |
| `widget/widget.js/.css` | Widget rendering and height reporting                           |
| `dncModuleBridge.js`    | Copied from the SDK at build time (single copy of the protocol) |

Every reference is **relative** — the core strips the prefix before forwarding. Requests use
`credentials: 'include'` because the frame's origin is opaque; message HTML is rendered inside a
nested `sandbox=""` frame, which is the sanitizer.

## Key Features

- **IMAP/POP3 sync** — poll external servers and store threads/messages in the module's schema
- **SMTP/Gmail sending** — send from a configured account, with attachments
- **Attach from your files** — `GET /api/v1/email/files` browses the user's Files tree (brokered by
  the core), `POST /api/v1/email/attachments/from-file` copies the chosen node into the attachment
  store, and `POST /api/v1/email/attachments/{id}/save-to-files` writes a received attachment back
  into a folder of the user's choice
- **Gmail OAuth** — web-server flow with a DataProtection-signed `state` (no cookie, because the proxy
  strips `Set-Cookie`); redirects return to `/apps/dotnetcloud.email/?gmail=…`
- **Rules engine** — conditions and actions, run in priority order on sync or on demand
- **Health checks** — `/health`, plus `/api/info`

## Projects This Interacts With

### Direct Dependencies

- `DotNetCloud.Core`, `DotNetCloud.Core.Grpc`, `DotNetCloud.Core.Security`
- `DotNetCloud.Modules.Email` — models and service contracts
- `DotNetCloud.Modules.Email.Data` — data access and business services
- `DotNetCloud.Modules.Email.Data.SqlServer` — SQL Server migration assembly (runtime)
- `DotNetCloud.Modules.Sdk` — `IDncModuleApi`, the only sanctioned channel to the core (used for the
  file capabilities)

### Dependent Projects

- `DotNetCloud.Core.Server` — launches and manages this process via `ProcessSupervisor` and proxies
  `/apps/dotnetcloud.email/**` to it
