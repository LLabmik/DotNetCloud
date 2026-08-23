# DotNetCloud.Modules.About.Host

> **Purpose:** About module gRPC host process — process-isolated server for system information
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the About module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for querying system information, module versions, and platform statistics. A lightweight host with no database dependency.

## Key Features

- **System Info gRPC** — Endpoints for server version, runtime, and system stats
- **Module Registry** — Query installed modules, versions, and health status
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.About` — Module logic and Blazor components

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
