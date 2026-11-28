# DotNetCloud.Modules.Example.Data

> **Purpose:** Example module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Example module. Provides `ExampleDbContext`, entity configurations for domain models (e.g., `ExampleNote`), and PostgreSQL migrations. Follows the standard module data pattern used by all DotNetCloud modules.

## Key Features

- **Module DbContext** — `ExampleDbContext` for module-specific data
- **Entity Configurations** — EF Core Fluent API configurations
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider
- **Design-Time Factory** — `ExampleDbContextDesignTimeFactory` for `dotnet ef` tooling
- **Service Registration** — `ExampleServiceRegistration` for DI setup

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure, naming strategies
- `DotNetCloud.Modules.Example` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Example.Host` — Uses DbContext at runtime
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
