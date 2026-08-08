# DotNetCloud.Modules.Photos.Host

> **Purpose:** Photos module gRPC host process — process-isolated server for photo operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Photos module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for photo upload/download, thumbnail generation, album management, and photo sharing.

## Key Features

- **gRPC Photo Services** — Upload, download, and manage photos
- **Thumbnail Generation** — Server-side thumbnail and preview generation
- **Album Management** — CRUD for albums and photo organization
- **Photo Sharing** — gRPC endpoints for share management
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Photos` — Module logic and Blazor components
- `DotNetCloud.Modules.Photos.Data` — Data access layer
- `DotNetCloud.Modules.Photos.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
