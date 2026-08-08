# DotNetCloud.Modules.Music.Data

> **Purpose:** Music module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Music module. Manages artists, albums, tracks, playlists, and enrichment data in the database. Provides `MusicDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `MusicDbContext` for music library and playlist data
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider
- **MusicBrainz Integration** — Metadata enrichment storage

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Music` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Music.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Music.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
