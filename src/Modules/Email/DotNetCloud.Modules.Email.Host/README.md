# DotNetCloud.Modules.Email.Host

> **Purpose:** Email module gRPC host process — process-isolated server for email operations
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The gRPC host process for the Email module. Runs as a separate process managed by `ProcessSupervisor`. Implements gRPC services for IMAP/SMTP email fetching and sending, message management, folder synchronization, and attachment handling.

## Key Features

- **IMAP Sync** — Fetch and sync email from external IMAP servers
- **SMTP Sending** — Send email via external SMTP servers
- **Message Management** — CRUD for email messages and folders
- **Attachment Handling** — Upload, download, and serve email attachments
- **Health Checks** — Module readiness and liveness endpoints

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions
- `DotNetCloud.Core.ServiceDefaults` — Logging, telemetry, health checks
- `DotNetCloud.Modules.Email` — Module logic and Blazor components
- `DotNetCloud.Modules.Email.Data` — Data access layer
- `DotNetCloud.Modules.Email.Data.SqlServer` — SQL Server migration assembly (runtime)

### Dependent Projects
- `DotNetCloud.Core.Server` — Launches and manages this process via `ProcessSupervisor`
