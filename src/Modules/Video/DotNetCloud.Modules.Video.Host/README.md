# DotNetCloud.Modules.Video.Host

> **Purpose:** Video module gRPC host process — process-isolated server for video operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Video module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for media scanning, HLS transcoding and streaming, TMDB metadata enrichment, and watch history tracking.

## Key Features

- **Media Scanning** — Filesystem scanner for video library population
- **HLS Transcoding** — Server-side HLS segment generation for streaming
- **TMDB Enrichment** — Automatic metadata and poster retrieval
- **Watch History** — Track and sync watch progress across devices
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Video` — Module logic and Blazor components
- `DotNetCloud.Modules.Video.Data` — Data access layer
- `DotNetCloud.Modules.Video.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
