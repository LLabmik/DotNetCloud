# DotNetCloud.Modules.Bookmarks.Host

> **Purpose:** Bookmarks module gRPC host process — process-isolated server for bookmark operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Bookmarks module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for bookmark CRUD, collection management, browser extension sync, and import/export.

## Key Features

- **gRPC Bookmark Services** — CRUD for bookmarks, collections, and tags
- **Browser Sync** — gRPC endpoints for browser extension synchronization
- **Import/Export** — Bookmark import from browsers and HTML bookmark files
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Bookmarks` — Module logic and Blazor components
- `DotNetCloud.Modules.Bookmarks.Data` — Data access layer (SQL Server migrations in Migrations/SqlServer/)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
