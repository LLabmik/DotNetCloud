# DotNetCloud.Modules.Video.Data

> **Purpose:** Video module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Video module. Manages series, seasons, episodes, video files, TMDB metadata, and watch history in the database. Provides `VideoDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `VideoDbContext` for video library data
- **Entity Configurations** — EF Core Fluent API configurations for series, episodes, metadata
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Video` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Video.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Video.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
