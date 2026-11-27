# DotNetCloud.Modules.Photos

> **Purpose:** Photos module contract library — models, service interfaces, events and the indexing seam
> **Type:** Library (plain class library — no Razor, no ASP.NET Core reference)
> **Target Framework:** net10.0

## Overview

The contracts half of the Photos module. The UI is served by the module host
(`DotNetCloud.Modules.Photos.Host/wwwroot`) and framed by the core at
`/apps/dotnetcloud.photos/`, so this project carries no Razor SDK and never references the core's
Blazor component library.

A sandboxed module (Track C) owns its schema, reaches the platform only through capabilities, and
reaches the user's files only through the core — never by reading a storage path.

## Key contracts

- `IPhotoService`, `IAlbumService`, `IPhotoShareService`, `IPhotoEditService`, `IPhotoGeoService`,
  `ISlideshowService`, `IPhotoThumbnailService` — the services the module host registers.
- `IPhotoIndexingCallback` — implemented in `DotNetCloud.Modules.Photos.Data`; the core's media
  import reaches it through the host's `IndexPhoto` / `GetIndexedFileNodeIds` /
  `RemoveDeletedPhotos` gRPC methods.
- `PhotosModuleManifest` + `PhotosModule` — the `IModuleLifecycle` implementation the host registers.

## Non-obvious rules for contributors

1. **Never name a schema.** `PhotosDbContext` and both migration sets are schema-neutral; the
   provisioned connection string pins `search_path` (PostgreSQL) or the default schema (SQL Server).
   `PhotosDbContextModelTests` fails the build otherwise.
2. **Never read a file by path.** Originals are fetched through the core's `IDncModuleApi`
   (`IFileDirectory`, per-user consent `files:read`) on a signed-in user's behalf. A background
   thread has no acting user, which is why thumbnails are generated lazily on first view.
3. **Only the owner holds a request that can generate.** A photo merely shared with a caller is
   served from the owner's cached previews.
4. **The module never subscribes to another module's events.** Indexing comes from the core's
   `IndexPhoto` call; the module publishes only its own events.

## Projects this interacts with

- `DotNetCloud.Modules.Photos.Data` — the EF model, migrations, initializer and services.
- `DotNetCloud.Modules.Photos.Data.SqlServer` — SQL Server migrations.
- `DotNetCloud.Modules.Photos.Host` — the process the supervisor launches; serves the UI + REST.
- `DotNetCloud.Modules.Sdk` — the module API client used to read files and write settings/audit.
