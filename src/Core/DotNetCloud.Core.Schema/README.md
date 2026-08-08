# DotNetCloud.Core.Schema

> **Purpose:** Database schema coordination — inspects all module DbContexts to provide unified migration and schema management
> **Type:** Library
> **Target Framework:** net10.0

## Overview

`DotNetCloud.Core.Schema` serves as the central schema registry for the DotNetCloud platform. It holds direct project references to every module's `.Data` project, allowing it to inspect all EF Core `DbContext` types across the entire codebase. This enables the core server to run pending migrations, verify schema consistency, and coordinate database initialization across all modules from a single entry point.

## Key Features

- **Module DbContext Discovery** — References all module `.Data` projects to enumerate every `DbContext` type
- **Unified Migration Runner** — Provides `DbContextSchemaProvider` that discovers all pending migrations across all modules
- **Schema Validation** — Verifies that all module schemas are consistent and migrations are applied
- **Database Initialization** — Coordinates initial database creation and seeding across modules

## Projects This Interacts With

### Direct Dependencies (Project References)
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Modules.AI.Data` — AI module DbContext
- `DotNetCloud.Modules.Bookmarks.Data` — Bookmarks module DbContext
- `DotNetCloud.Modules.Calendar.Data` — Calendar module DbContext
- `DotNetCloud.Modules.Chat.Data` — Chat module DbContext
- `DotNetCloud.Modules.Contacts.Data` — Contacts module DbContext
- `DotNetCloud.Modules.Email.Data` — Email module DbContext
- `DotNetCloud.Modules.Files.Data` — Files module DbContext
- `DotNetCloud.Modules.Music.Data` — Music module DbContext
- `DotNetCloud.Modules.Notes.Data` — Notes module DbContext
- `DotNetCloud.Modules.Photos.Data` — Photos module DbContext
- `DotNetCloud.Modules.Search.Data` — Search module DbContext
- `DotNetCloud.Modules.Tracks.Data` — Tracks module DbContext
- `DotNetCloud.Modules.Video.Data` — Video module DbContext

### Dependent Projects
- `DotNetCloud.Core.Server` — Uses `DbContextSchemaProvider` at startup to run pending migrations

## Key Files

| File | Purpose |
|------|---------|
| `Services/DbContextSchemaProvider.cs` | Discovers all DbContext types and pending migrations across all modules |
