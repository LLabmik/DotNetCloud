# DotNetCloud.Modules.Notes.Data

> **Purpose:** Notes module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Notes module. Manages notes, folders, tags, and note shares in the database. Provides `NotesDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `NotesDbContext` for notes and note metadata
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider
- **Full-Text Search** — Database-level full-text search indexing

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Notes` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Notes.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Notes.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
