# DotNetCloud.Modules.Music.Host

> **Purpose:** Music module gRPC host process — process-isolated server for music operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Music module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for media scanning, metadata enrichment, audio streaming, playlist management, and search.

## Key Features

- **Media Scanning** — Filesystem scanner for music library population
- **Audio Streaming** — gRPC streaming for audio playback
- **Metadata Enrichment** — MusicBrainz API integration for artist/album metadata
- **Playlist Management** — gRPC endpoints for playlist CRUD
- **Deduplication** — Media content deduplication engine
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Music` — Module logic and Blazor components
- `DotNetCloud.Modules.Music.Data` — Data access layer
- `DotNetCloud.Modules.Music.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
