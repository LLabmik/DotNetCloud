# DotNetCloud.Modules.Notes.Host

> **Purpose:** Notes module gRPC host process — process-isolated server for notes operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Notes module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for note CRUD, Markdown rendering, folder/tag management, full-text search, and note sharing.

## Key Features

- **gRPC Note Services** — CRUD for notes, folders, and tags
- **Markdown Processing** — Server-side Markdown-to-HTML rendering
- **Full-Text Search** — gRPC endpoint for searching notes
- **Note Sharing** — gRPC endpoints for share management
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Notes` — Module logic and Blazor components
- `DotNetCloud.Modules.Notes.Data` — Data access layer
- `DotNetCloud.Modules.Notes.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
