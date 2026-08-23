# DotNetCloud.Modules.Search.Host

> **Purpose:** Search module gRPC host process — process-isolated server for search operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Search module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for full-text search, index management, search result aggregation from all modules, and search ranking.

## Key Features

- **Full-Text Search** — gRPC endpoint for global search queries
- **Index Management** — Create, update, and rebuild search indexes
- **Cross-Module Aggregation** — Coordinates search across all module search providers
- **Ranking** — Relevance scoring and result ranking
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Search` — Module logic and Blazor components
- `DotNetCloud.Modules.Search.Data` — Data access layer
- `DotNetCloud.Modules.Search.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
