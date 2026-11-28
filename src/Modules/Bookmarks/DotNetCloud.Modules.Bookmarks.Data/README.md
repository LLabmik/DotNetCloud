# DotNetCloud.Modules.Bookmarks.Data

> **Purpose:** Bookmarks module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Bookmarks module. Manages bookmarks, collections, tags, and sync state in the database. Provides `BookmarksDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `BookmarksDbContext` for bookmarks, collections, and tags
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Bookmarks` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Bookmarks.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Bookmarks.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
