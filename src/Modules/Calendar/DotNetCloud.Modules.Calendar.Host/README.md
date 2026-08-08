# DotNetCloud.Modules.Calendar.Host

> **Purpose:** Calendar module gRPC host process — process-isolated server for calendar operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Calendar module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for event CRUD, recurrence rule management, reminder scheduling, and calendar sharing. Communicates exclusively via gRPC over Unix sockets / Named Pipes.

## Key Features

- **gRPC Calendar Services** — CRUD for events, calendars, and recurrence rules
- **Reminder Engine** — Scheduled reminders with push notification integration
- **Recurrence Expansion** — Server-side expansion of recurring event instances
- **Calendar Sharing** — gRPC endpoints for share management
- **Health Checks** — Module readiness and liveness endpoints
- **JWT Authentication** — Bearer token validation

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Calendar` — Module logic and Blazor components
- `DotNetCloud.Modules.Calendar.Data` — Data access layer
- `DotNetCloud.Modules.Calendar.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
