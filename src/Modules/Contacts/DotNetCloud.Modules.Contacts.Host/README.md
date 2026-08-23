# DotNetCloud.Modules.Contacts.Host

> **Purpose:** Contacts module gRPC host process — process-isolated server for contacts operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Contacts module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for contact CRUD, group management, vCard import/export, and search. Communicates exclusively via gRPC over Unix sockets / Named Pipes.

## Key Features

- **gRPC Contact Services** — CRUD operations for contacts and groups
- **vCard Support** — Import and export contacts in vCard format
- **Search** — gRPC endpoint for contact search
- **Health Checks** — Module readiness and liveness endpoints
- **JWT Authentication** — Bearer token validation for gRPC calls

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Contacts` — Module logic and Blazor components
- `DotNetCloud.Modules.Contacts.Data` — Data access layer
- `DotNetCloud.Modules.Contacts.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
