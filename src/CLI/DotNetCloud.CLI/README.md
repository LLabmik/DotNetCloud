# DotNetCloud.CLI

> **Purpose:** Command-line interface (`dotnetcloud`) — administration, migration, and management tool for the DotNetCloud platform
> **Type:** Console Application
> **Target Framework:** net10.0

## Overview

`DotNetCloud.CLI` is the `dotnetcloud` command-line tool used for platform administration. It provides commands for running database migrations across all modules, generating TLS certificates, managing telemetry, and performing system-level operations. Built with `System.CommandLine`, it supports nested subcommands with full `--help` documentation.

## Key Features

- **Database Migrations** — `dotnetcloud migrate` runs pending EF Core migrations across all modules (Core + all 14 module Data.SqlServer projects)
- **TLS Certificate Management** — Generate and manage SSL/TLS certificates via Certes (Let's Encrypt ACME client)
- **Telemetry** — OpenTelemetry configuration and export
- **System Administration** — Configuration management and system health checks
- **Command-Line Help** — Full `--help` for all commands and subcommands via `System.CommandLine`

## Projects This Interacts With

### Direct Dependencies (Core)
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Data` — Core data access and EF Core setup
- `DotNetCloud.Core.Schema` — DbContext discovery and schema coordination
- `DotNetCloud.Core.ServiceDefaults` — Logging configuration

### Direct Dependencies (SQL Server Migration Assemblies — all 15)
Every module's `.Data.SqlServer` project plus `DotNetCloud.Core.Data.SqlServer` is referenced for runtime migration discovery and execution.

### Dependent Projects
- (None — this is a leaf application project)

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | CLI entry point: command registration and DI setup |
| `Commands/` | Command implementations (migrate, cert, etc.) |
| `Infrastructure/` | Shared CLI infrastructure (logging, DI, configuration) |
