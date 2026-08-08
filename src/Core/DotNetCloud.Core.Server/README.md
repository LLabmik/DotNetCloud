# DotNetCloud.Core.Server

> **Purpose:** Main server host process — the central orchestrator that launches and manages all DotNetCloud modules
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

`DotNetCloud.Core.Server` is the entry point and central nervous system of the DotNetCloud platform. It is an ASP.NET Core web application that:

- **Hosts the Blazor web UI** via `DotNetCloud.UI.Web` and `DotNetCloud.UI.Web.Client` (WASM)
- **Launches and supervises** all process-isolated modules via `ProcessSupervisor`
- **Routes gRPC calls** to the correct module host process via `GrpcChannelManager`
- **Exposes REST APIs** for OpenIddict-based authentication and module health checks
- **Manages client connectivity** via SignalR real-time hubs and push notifications
- **Reverse-proxies** module HTTP endpoints via YARP

All inter-module communication is mediated through this server; modules never talk to each other directly.

## Key Features

- **Process Supervisor** — Launches each module as a separate process, monitors health, restarts on failure
- **gRPC Routing** — `GrpcChannelManager` maintains per-module gRPC channels over Unix sockets / Named Pipes
- **Module Loading** — Discovers modules from filesystem manifests, validates capability grants, initializes lifecycle
- **Blazor Host** — Serves the Blazor WebAssembly client and Blazor Server pre-rendering
- **REST API** — OpenIddict OAuth2 endpoints, health check endpoints (`/health`, `/health/ready`, `/health/live`)
- **SignalR / Real-Time** — Push notifications and real-time data synchronization
- **YARP Reverse Proxy** — Routes HTTP requests to module hosts' internal HTTP endpoints
- **Health Checks** — Aggregates health from all modules and databases
- **Security Middleware** — CSP, HSTS, X-Frame-Options, and other security headers from `ServiceDefaults`
- **OpenTelemetry** — Distributed tracing and metrics export

## Projects This Interacts With

### Direct Dependencies (Core)
- `DotNetCloud.Core` — Core interfaces and DTOs
- `DotNetCloud.Core.Auth` — Authentication and authorization pipeline
- `DotNetCloud.Core.Data` — EF Core data access and migrations
- `DotNetCloud.Core.Data.SqlServer` — SQL Server migration assembly (runtime resolution)
- `DotNetCloud.Core.Grpc` — gRPC proto definitions and client generation
- `DotNetCloud.Core.Schema` — Database schema coordination across modules
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks, security middleware

### Direct Dependencies (UI)
- `DotNetCloud.UI.Web` — Blazor Server UI
- `DotNetCloud.UI.Web.Client` — Blazor WebAssembly client

### Direct Dependencies (Modules — all 15 modules)
Every module's main RCL, Data, and Data.SqlServer project is referenced for:
- Blazor UI component registration
- EF Core migration assembly discovery at runtime
- Assembly.Load resolution for `GetPendingMigrationsAsync()`

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point: service registration, middleware pipeline |
| `Supervisor/ProcessSupervisor.cs` | Launches and monitors module host processes |
| `Supervisor/GrpcChannelManager.cs` | Manages per-module gRPC channels |
| `Supervisor/ModuleProcessHandle.cs` | Per-module process tracking and lifecycle |
| `Supervisor/ResourceLimiter.cs` | CPU and memory limits per module process |
| `Grpc/` | gRPC client proxies, interceptors, and routing configuration |
| `ModuleLoading/` | Module discovery from filesystem manifests |
| `Middleware/` | Custom ASP.NET Core middleware |
| `Initialization/` | Startup initialization and database migration runner |
| `PushNotifications/` | Push notification infrastructure |
| `RealTime/` | SignalR hubs for real-time client updates |
