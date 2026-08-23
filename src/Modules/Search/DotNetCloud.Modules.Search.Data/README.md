# DotNetCloud.Modules.Search.Data

> **Purpose:** Search module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Search module. Manages search indexes, index configurations, search history, and ranking data in the database. Provides `SearchDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `SearchDbContext` for search indexes and configuration
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider
- **Full-Text Indexing** — Database-level full-text search infrastructure

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Search` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Search.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Search.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
