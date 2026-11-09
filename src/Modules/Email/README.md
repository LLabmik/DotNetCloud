# DotNetCloud Email Module

IMAP/Gmail mail with folders, threads, rules and attachments, running as a process-isolated,
sandboxed module.

## Overview

The Email module is the first DotNetCloud module with a long-lived credential store and an external
consent flow, which makes it the reference for anything that stores a user's provider credentials.

- **Own data store.** `dataStore: "isolated"` — the module self-migrates into the schema and
  principal the core provisions for it. The model and both provider migration sets are
  schema-neutral: the scoped connection string's `search_path` (PostgreSQL) or default schema
  (SQL Server) decides where the tables and `__EFMigrationsHistory` land. Never name a schema in a
  sandboxed module's model or migrations.
- **Own UI.** `wwwroot/index.html` + `assets/email-ui.js` are served by this host and framed by the
  core at `/apps/dotnetcloud.email/`; `wwwroot/widget/` is the Home-page card. Every asset path is
  relative and every API call is same-origin to the frame.
- **Identity.** There is no cookie scheme. The core proxy strips the browser's session and injects a
  `dnc-user-assertion`; the module answers 401 (never 500) when it is missing.
- **The user's files are a core capability.** Browsing the user's tree, attaching a file to a draft
  and saving an attachment back are done through the core's brokered `IFileDirectory` /
  `IFileWriter` RPCs, not by calling the Files module. That costs two consent scopes
  (`files:read`, `files:write`) and keeps the module out of Files' credentials entirely.
- **Erasure.** `EmailUserErasureHandler` deletes the deleted user's accounts, mailboxes, threads,
  messages, attachments (and their blobs) and rules.

## Manifest

| Field | Value |
| ----- | ----- |
| `id` | `dotnetcloud.email` |
| `dataStore` | `isolated` |
| `capabilities` | `IAuditLogger`, `ISearchProvider` |
| `userConsentScopes` | `mail:read`, `mail:write`, `files:read`, `files:write` |
| `ui` | iframe app with a sidebar entry and a Home-page widget |

`IAuditLogger` and `ISearchProvider` are the only core capabilities the module needs; the consent
scopes above cover what a user is asked to approve.

## Project structure

```
src/Modules/Email/
├── manifest.json                       # The manifest that ships (single source of truth)
├── DotNetCloud.Modules.Email/          # Domain, services, gRPC contracts
├── DotNetCloud.Modules.Email.Data/     # EF Core context, migrations (PostgreSQL)
├── DotNetCloud.Modules.Email.Data.SqlServer/  # Migrations (SQL Server)
└── DotNetCloud.Modules.Email.Host/     # The sandboxed host: gRPC + REST + its own wwwroot
```

## Building and packaging

```bash
# Build
dotnet build src/Modules/Email/DotNetCloud.Modules.Email.Host/DotNetCloud.Modules.Email.Host.csproj -c Release

# Package (signed with the publisher key)
dotnet src/Tools/DotNetCloud.ModulePackaging/bin/Release/net10.0/dotnetcloud-pack.dll pack \
  --project src/Modules/Email/DotNetCloud.Modules.Email.Host/DotNetCloud.Modules.Email.Host.csproj \
  --rid linux-x64 --rid win-x64 \
  --sign-key ~/.keys/dotnetcloud-official-2026.key.json \
  --output /tmp/dotnetcloud.email-1.2.0.dncpkg
```

The package must contain `manifest.json`, `LICENSE`, `README.md` and `CHANGELOG.md` beside it; the
packer copies whatever documents sit next to the manifest.

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
