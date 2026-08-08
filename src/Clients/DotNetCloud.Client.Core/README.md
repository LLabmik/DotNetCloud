# DotNetCloud.Client.Core

> **Purpose:** Core client library — shared sync engine, virtual filesystem, and API client used by all DotNetCloud desktop/mobile clients
> **Type:** Library
> **Target Framework:** net10.0

## Overview

`DotNetCloud.Client.Core` is the shared client-side library that powers all DotNetCloud desktop and mobile clients. It provides the sync engine that keeps local filesystems in sync with the DotNetCloud server, a local SQLite database for sync state, a virtual filesystem abstraction layer, conflict detection and resolution, selective sync rules, and a gRPC client for the Files module API. Both the SyncTray desktop app and Android mobile client depend on this library.

## Key Features

- **Sync Engine** — Bidirectional file synchronization between local filesystem and DotNetCloud server
- **Virtual Filesystem** — `VirtualFiles/` abstraction for platform-specific filesystem access (Windows Cloud Filter API, Linux FUSE)
- **Local SQLite Database** — Offline-capable local state storage for sync metadata
- **gRPC Files Client** — Generated client from `files_service.proto` for file operations
- **Conflict Detection** — Diff-based conflict detection (using DiffPlex) with resolution strategies
- **Selective Sync** — Rule-based filtering to include/exclude paths from synchronization
- **Transfer Management** — Chunked upload/download with resume support
- **API Client** — REST API client for authentication and server communication
- **Cross-Platform** — Windows (Cloud Filter API via cfapi.dll P/Invoke) and Linux (FUSE via LTRData.FuseDotNet) support
- **Authentication** — Token management and refresh for server API calls
- **SignalR Client** — Real-time sync notifications via `IChatSignalRClient`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and DTOs

### Dependent Projects
- `DotNetCloud.Client.SyncTray` — Desktop system tray application
- `DotNetCloud.Client.Android` — Android mobile client

## Key Files

| File | Purpose |
|------|---------|
| `ClientCoreServiceExtensions.cs` | DI registration for all client services |
| `Sync/` | Sync engine: file watcher, change detection, upload/download queue |
| `VirtualFiles/` | Platform filesystem abstraction (Windows cfapi, Linux FUSE) |
| `Transfer/` | Chunked file transfer with resume support |
| `Conflict/` | Conflict detection and resolution |
| `SelectiveSync/` | Path-based sync filtering rules |
| `Api/` | REST API client for server communication |
| `Auth/` | OAuth2 token management |
| `LocalState/` | SQLite database for sync metadata |
| `Platform/` | Platform-specific implementations (Windows/Linux) |
