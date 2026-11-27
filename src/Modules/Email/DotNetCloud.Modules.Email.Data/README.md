# DotNetCloud.Modules.Email.Data

> **Purpose:** Email module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Email module. Manages email accounts, messages, folders, attachments, and send queue in the database. Provides `EmailDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `EmailDbContext` for email messages, accounts, and folders
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Email` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Email.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Email.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
