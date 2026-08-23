# DotNetCloud.Modules.Contacts.Data

> **Purpose:** Contacts module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Contacts module. Manages contacts, contact groups, addresses, phone numbers, and email addresses in the database. Provides `ContactsDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `ContactsDbContext` for contacts and contact group data
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Contacts` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Contacts.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Contacts.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
