# DotNetCloud.Modules.Calendar.Data

> **Purpose:** Calendar module data access layer — EF Core DbContext, entity configurations, and PostgreSQL migrations
> **Type:** Library
> **Target Framework:** net10.0

## Overview

The data access layer for the Calendar module. Manages calendars, events, recurrence rules, reminders, and calendar shares in the database. Provides `CalendarDbContext` with entity configurations and PostgreSQL migrations.

## Key Features

- **Module DbContext** — `CalendarDbContext` for calendar and event data
- **Entity Configurations** — EF Core Fluent API configurations for events, recurrence, reminders
- **PostgreSQL Migrations** — Database migrations for PostgreSQL provider

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Base data infrastructure
- `DotNetCloud.Modules.Calendar` — Module domain models

### Dependent Projects
- `DotNetCloud.Modules.Calendar.Host` — Uses DbContext at runtime
- `DotNetCloud.Modules.Calendar.Data.SqlServer` — SQL Server migration assembly
- `DotNetCloud.Core.Schema` — Discovers DbContext for migration coordination
