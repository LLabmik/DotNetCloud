# DotNetCloud.Modules.Example.Host

> **Purpose:** Example module gRPC host process — process-isolated server for the Example module
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Example module. Runs as a separate process, communicates with the core server via gRPC over Unix sockets / Named Pipes. Implements the module lifecycle gRPC service, exposes module-specific gRPC services, and provides health checks. This is the entry point that `ProcessSupervisor` launches.

## Key Features

- **gRPC Services** — `ExampleGrpcService` for CRUD operations, `ExampleLifecycleService` for lifecycle management
- **Health Checks** — `ExampleHealthCheck` for module readiness reporting
- **Proto Definitions** — `example_service.proto` defines the module's gRPC contract
- **Process Isolation** — Runs independently, managed by `ProcessSupervisor`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Example` — Module logic and Blazor components
- `DotNetCloud.Modules.Example.Data` — Data access layer

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
