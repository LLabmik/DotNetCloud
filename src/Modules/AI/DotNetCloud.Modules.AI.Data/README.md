# DotNetCloud.Modules.AI.Data

> **Purpose:** AI module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the AI module. Manages AI model configurations, prompt templates, conversation history, and analysis results in the database. Provides `AiDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `AiDbContext` for AI configuration and history
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.AI` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.AI.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.AI.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
