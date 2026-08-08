# DotNetCloud.Modules.Tracks.Host

> **Purpose:** Tracks module gRPC host process — process-isolated server for issue tracking operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Tracks module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for work item CRUD, Kanban board operations, sprint management, and project dashboards.

## Key Features

- **gRPC Work Item Services** — CRUD for issues, tasks, bugs, and epics
- **Board Operations** — Column and swimlane management
- **Sprint Management** — Sprint lifecycle and backlog operations
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Tracks` — Module logic and Blazor components
- `DotNetCloud.Modules.Tracks.Data` — Data access layer
- `DotNetCloud.Modules.Tracks.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
