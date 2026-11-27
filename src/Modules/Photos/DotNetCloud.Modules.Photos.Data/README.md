# DotNetCloud.Modules.Photos.Data

> **Purpose:** Photos module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Photos module. Manages photos, albums, tags, and shares in the database. Provides `PhotosDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `PhotosDbContext` for photos, albums, and metadata
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Photos` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Photos.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Photos.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
