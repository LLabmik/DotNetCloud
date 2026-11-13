# DotNetCloud Bookmarks Module

Bookmarks with folders, rich previews, import/export and full-text search, running as a
process-isolated, sandboxed module with its own web UI and Home-page widget.

## Overview

- **Own data store.** `dataStore: "isolated"` — the module self-migrates into the schema and
  principal the core provisions for it. `BookmarksDbContext` and both provider migration sets are
  schema-neutral: the scoped connection string's `search_path` (PostgreSQL) or default schema
  (SQL Server) decides where the tables and `__EFMigrationsHistory` land. Never name a schema in a
  sandboxed module's model or migrations.
- **Own UI.** `wwwroot/index.html` + `assets/bookmarks-ui.js` are served by this host and framed by
  the core at `/apps/dotnetcloud.bookmarks/`; `wwwroot/widget/` is the Home-page card. Every asset
  path is relative and every API call is same-origin to the frame.
- **Identity.** There is no cookie scheme. The core proxy strips the browser's session and injects a
  `dnc-user-assertion`; a request without one is answered with 401 (never 500).
- **Erasure.** `BookmarksUserErasureHandler` deletes the deleted user's items, folders and previews.

## Manifest

| Field | Value |
| ----- | ----- |
| `id` | `dotnetcloud.bookmarks` |
| `dataStore` | `isolated` |
| `capabilities` | `IAuditLogger` |
| `userConsentScopes` | none — the module only reads and writes its own data; search indexing goes through the event bus |
| `ui` | iframe app with a sidebar entry and a Home-page widget |

## Project structure

```
src/Modules/Bookmarks/
├── manifest.json                              # The manifest that ships (single source of truth)
├── DotNetCloud.Modules.Bookmarks/             # Domain, service contracts, module lifecycle
├── DotNetCloud.Modules.Bookmarks.Data/        # EF Core context, migrations (PostgreSQL), services
├── DotNetCloud.Modules.Bookmarks.Data.SqlServer/  # Migrations (SQL Server)
└── DotNetCloud.Modules.Bookmarks.Host/        # The sandboxed host: gRPC + REST + its own wwwroot
```

## Building and packaging

```bash
# Build
dotnet build src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/DotNetCloud.Modules.Bookmarks.Host.csproj -c Release

# Package (signed with the publisher key)
dotnet src/Tools/DotNetCloud.ModulePackaging/bin/Release/net10.0/dotnetcloud-pack.dll pack \
  --project src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/DotNetCloud.Modules.Bookmarks.Host.csproj \
  --rid linux-x64 --rid win-x64 \
  --sign-key ~/.keys/dotnetcloud-official-2026.key.json \
  --output /tmp/dotnetcloud.bookmarks-1.2.0.dncpkg
```

The package must contain `manifest.json`, `LICENSE`, `README.md` and `CHANGELOG.md` beside it; the
packer copies whatever documents sit next to the manifest.

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
