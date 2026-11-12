# DotNetCloud Tracks Module

Project management for DotNetCloud — products, work items, kanban boards, sprints, backlogs,
reviews and reporting — as a process-isolated, sandboxed module with its own Blazor WebAssembly UI
and Home-page widget.

## Overview

- **Own data store.** `dataStore: "isolated"`: the module self-migrates into the schema and principal
  the core provisions. `TracksDbContext` and both provider migration sets are schema-neutral — the
  scoped connection string's `search_path` (PostgreSQL) or default schema (SQL Server) decides where
  the 41 business tables and `__EFMigrationsHistory` land. Never name a schema in a sandboxed module.
- **Own UI.** `DotNetCloud.Modules.Tracks.Web` is a Blazor WebAssembly app that `Tracks.Host`
  publishes into its `wwwroot`; the core frames `/apps/dotnetcloud.tracks/` and proxies it, stripping
  the prefix, so every asset reference in the document is relative. `wwwroot/widget/` is the
  manifest-declared Home-page card.
- **No core references.** The module talks to the core only through gRPC plus the capability
  interfaces it declares — `IAuditLogger`, `IUserDirectory`, `INotificationService`,
  `IOrganizationDirectory` and `IRealtimeBroadcaster` — so display names, organizations and
  notifications are resolved on the signed-in user's behalf. Consent scopes: `profile`,
  `notifications:send`.
- **Deep links.** The shell forwards its query string into the frame: `?kanban=<workItemId>` opens a
  board and `?card=<workItemId>` opens the item panel. Search results and the widget use the same
  contract.

## Projects

| Project                                 | Purpose                                                          |
| --------------------------------------- | ---------------------------------------------------------------- |
| `DotNetCloud.Modules.Tracks`            | Contracts: models, service interfaces, events, module lifecycle    |
| `...Tracks.Data`                        | EF Core model, migrations (PostgreSQL), services, erasure handler  |
| `...Tracks.Data.SqlServer`              | SQL Server migrations                                              |
| `...Tracks.Shared`                      | UI contracts shared by the host and the browser (feeds, palette)   |
| `...Tracks.Web`                         | Blazor WebAssembly app (pages, components, scoped CSS)             |
| `...Tracks.Web.Services`                | Browser-side services: REST client, bridge, preferences            |
| `...Tracks.Host`                        | The supervised process: gRPC, REST, UI, widget, health checks      |

## Building the package

The module root needs `LICENSE`, `README.md` and `CHANGELOG.md` beside `manifest.json`; the packer
copies them next to the manifest in the archive.

```bash
dotnet run --project src/Tools/DotNetCloud.ModulePackaging -- pack \
  --project src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/DotNetCloud.Modules.Tracks.Host.csproj \
  --rid linux-x64 --rid win-x64 \
  --sign-key ~/.keys/dotnetcloud-official-2026.key.json \
  --output ~/artifacts/dotnetcloud.tracks-2.0.0.dncpkg
```

The packer verifies its own output; `dotnetcloud-pack verify <file> --key-file <key>` re-checks a
detached signature after download.

## Requirements

- DotNetCloud core `0.7.0` or later with the Tracks module installed.
- PostgreSQL or SQL Server, reachable with the credential the core provisions for the module.
- An organization to own the first product — a product is always scoped to one.

## Licence

AGPL-3.0 — see `LICENSE`.
