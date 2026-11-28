# DotNetCloud.Modules.AI.Host

> **Purpose:** AI module gRPC host process — process-isolated server for AI operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the AI module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for AI chat completion, content analysis, embeddings generation, and model management.

## Key Features

- **AI Completion** — gRPC streaming for chat and completion requests
- **Content Analysis** — Text classification, sentiment analysis
- **Embeddings** — Vector embeddings for semantic search
- **Model Management** — AI model configuration and lifecycle
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.AI` — Module logic and Blazor components
- `DotNetCloud.Modules.AI.Data` — Data access layer
- `DotNetCloud.Modules.AI.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
