# DotNetCloud Photos Module

Photo library management — albums, favourites, timeline, sharing and non-destructive editing — as a
process-isolated, sandboxed module with its own web UI and Home-page widget.

## Overview

- **Own data store.** `dataStore: "isolated"`: the module self-migrates into the schema and principal
  the core provisions. `PhotosDbContext` and both provider migration sets are schema-neutral — the
  scoped connection string's `search_path` (PostgreSQL) or default schema (SQL Server) decides where
  `Photos`, `Albums` and `__EFMigrationsHistory` land. Never name a schema in a sandboxed module.
- **No filesystem access.** The module never reads the Files module's storage. An original image is
  read through the core's module API (`IFileDirectory`, user consent `files:read`) on the signed-in
  user's behalf, which is also why thumbnails are generated lazily on first view: a background
  thread has no user to assert.
- **Own UI.** `DotNetCloud.Modules.Photos.Host/wwwroot` serves the framed app and the widget; the
  core frames `/apps/dotnetcloud.photos/` and proxies it, stripping the prefix, so every asset and
  API reference in the documents is relative.
- **Core-driven indexing.** The module cannot subscribe to another module's events, so the core's
  media import calls `IndexPhoto` / `GetIndexedFileNodeIds` / `RemoveDeletedPhotos` with a
  core-signed user assertion.

## Projects

| Project                        | Purpose                                                        |
| ------------------------------ | -------------------------------------------------------------- |
| `DotNetCloud.Modules.Photos`   | Contracts: service interfaces, events, module lifecycle         |
| `...Photos.Data`               | EF Core model, migrations (PostgreSQL), services, initializer   |
| `...Photos.Data.SqlServer`     | SQL Server migrations                                           |
| `...Photos.Host`               | The supervised process: gRPC, REST, UI, erasure, health checks  |

## Capabilities and consent

`IAuditLogger`, `ISearchProvider`, `ITeamDirectory`, `IFileDirectory`, `INotificationService`,
`IUserDirectory`; user consent `files:read`, `notifications:send`, `profile`. The module never
requests `files:write` — edits are non-destructive.

## API

REST under `api/v1/photos` and `api/v1/albums` (relative to the module), plus a gRPC surface for the
core (`PhotosGrpcService`, including the core-initiated indexing and shared-with-me RPCs). The acting
user always comes from the core-signed assertion, never from a request field.

## Building

```bash
dotnet build src/Modules/Photos/DotNetCloud.Modules.Photos.Host/DotNetCloud.Modules.Photos.Host.csproj
dotnet test tests/DotNetCloud.Modules.Photos.Tests/DotNetCloud.Modules.Photos.Tests.csproj
```

Packaging uses the repository tool (`src/Tools/DotNetCloud.ModulePackaging`); see
`docs/modules/MODULE_PACKAGING.md`.
