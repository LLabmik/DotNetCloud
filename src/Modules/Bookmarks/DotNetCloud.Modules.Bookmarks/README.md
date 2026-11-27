# DotNetCloud.Modules.Bookmarks

> **Purpose:** Bookmarks module contracts — models, service interfaces, domain events and the HTTP client
> **Type:** Library (plain class library, `Microsoft.NET.Sdk`)
> **Target Framework:** net10.0

## Overview

The module's shared surface. It is deliberately **not** a Razor Class Library and has no dependency on
the core's Blazor components: since Track C the module serves its own web UI from
`DotNetCloud.Modules.Bookmarks.Host/wwwroot`, and the core frames it at
`/apps/dotnetcloud.bookmarks/` (see `docs/modules/MODULE_UI_HOSTING.md`).

## Key Features

- **Bookmark management** — models for bookmarks, folders and rich previews, plus the service contracts
- **Domain events** — `BookmarkCreatedEvent`, `BookmarkUpdatedEvent`, `BookmarkDeletedEvent`
- **HTTP client** — `BookmarksApiClient` (`IBookmarksApiClient`), used by the module's own UI page
- **Module lifecycle** — `BookmarksModule` (`IModuleLifecycle`) and `BookmarksModuleManifest`

## Projects This Interacts With

### Direct Dependencies

- `DotNetCloud.Core` — core interfaces and module contracts
- `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`

### Dependent Projects

- `DotNetCloud.Modules.Bookmarks.Data` — EF Core models, migrations, services
- `DotNetCloud.Modules.Bookmarks.Host` — runs the module as a sandboxed process and serves its UI
- `DotNetCloud.Modules.Bookmarks.Tests`

The core (`DotNetCloud.Core.Server`) holds **no** project reference to this module; it talks to it over
gRPC only, and reads the module's `manifest.json` for its capability, consent and UI declarations.

## Data store

`BookmarksDbContext` and both providers' migrations are **schema-neutral**: the provisioned connection
string's `search_path` (PostgreSQL) / default schema (SQL Server) decides where tables land, and the
module self-migrates at start-up. Migrations must never emit `CREATE SCHEMA` or a schema-qualified
table name — the module's principal is not allowed to create schemas.
