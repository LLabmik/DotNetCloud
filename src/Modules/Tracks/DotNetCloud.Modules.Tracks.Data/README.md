# DotNetCloud.Modules.Tracks.Data

> **Purpose:** Tracks module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Tracks module. Manages work items, projects, sprints, boards, and workflow states in the database. Provides `TracksDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `TracksDbContext` for work items, projects, and sprints
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Tracks` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Tracks.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Tracks.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
