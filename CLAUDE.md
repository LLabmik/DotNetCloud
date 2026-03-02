# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotNetCloud is a self-hosted, open-source cloud platform (.NET 10 / C#) — a modern alternative to NextCloud. It uses a **modular monolith with process-isolated modules** communicating via gRPC over Unix sockets or Named Pipes. The project is in **Phase 0 (Foundation)**, with Phase 1 (Files + Sync Client) as the first public launch milestone.

## Commands

```bash
# Build
dotnet restore
dotnet build
dotnet build -c Release

# Test
dotnet test
dotnet test tests/DotNetCloud.Core.Tests/
dotnet test tests/DotNetCloud.Core.Data.Tests/
dotnet test --collect:"XPlat Code Coverage"
dotnet watch test

# Format & lint (code style is enforced at build time via Directory.Build.props)
dotnet format
dotnet build /p:EnforceCodeStyleInBuild=true
```

### Database Migrations

The `--project` flag targets `DotNetCloud.Core.Data`; output dir differentiates providers.

```bash
# PostgreSQL (default)
dotnet ef migrations add <MigrationName> \
  --project src/Core/DotNetCloud.Core.Data \
  --context CoreDbContext

# SQL Server
dotnet ef migrations add <MigrationName>_SqlServer \
  --project src/Core/DotNetCloud.Core.Data \
  --context CoreDbContext \
  --output-dir Migrations/SqlServer

# Apply
dotnet ef database update --context CoreDbContext
```

## Architecture

### Module System

Each feature (Files, Chat, Calendar, etc.) is a separate process. The core acts as a supervisor. Modules communicate with the core exclusively via gRPC; they cannot access each other's data directly. Third-party modules use the exact same interface as first-party ones.

```
dotnetcloud (core process — supervisor)
├── dotnetcloud-module files      (gRPC)
├── dotnetcloud-module chat       (gRPC)
└── ...
```

### Project Layout

```
src/
  Core/
    DotNetCloud.Core/               # Interfaces & DTOs only (SDK — Apache 2.0)
    DotNetCloud.Core.Data/          # EF Core models, DbContext, migrations
    DotNetCloud.Core.ServiceDefaults/  # Serilog, OpenTelemetry, health checks, security middleware
  Modules/                          # Feature modules (not yet implemented)
  Clients/                          # Desktop (Avalonia) and mobile (MAUI) clients
  UI/                               # Blazor web UI
tests/
  DotNetCloud.Core.Tests/
  DotNetCloud.Core.Data.Tests/
docs/
  architecture/ARCHITECTURE.md      # Full system design
  MASTER_PROJECT_PLAN.md            # Phase-by-phase plan with status
  IMPLEMENTATION_CHECKLIST.md       # Task checklist across all phases
```

### Core Abstractions (`DotNetCloud.Core`)

- **Capability system** — Hierarchical tier model: Public → Restricted → Privileged → Forbidden. All module access is mediated through typed capability interfaces (`ICapabilityInterface`).
- **Module lifecycle** — `IModule`: `InitializeAsync` → `StartAsync` → `StopAsync`. `IModuleManifest` declares capabilities and events a module needs/produces.
- **Event bus** — Loosely coupled pub/sub (`IEvent`, `IEventHandler<T>`, `IEventBus`) for inter-module communication without direct references.
- **Caller context** — Every operation is tagged with a `CallerContext` (User / System / Module) for auditability and authorization.

### Data Layer (`DotNetCloud.Core.Data`)

- `CoreDbContext` is the single EF Core DbContext. Entity configurations live in `Configuration/`.
- **Multi-database**: PostgreSQL (default), SQL Server, MariaDB (pending Pomelo .NET 10 release). Naming differs by provider: PostgreSQL/SQL Server use schemas (`core.users`), MariaDB uses prefixes (`core_users`), controlled by `ITableNamingStrategy`.
- Built-in: soft delete (query filters), automatic timestamp interceptors, ASP.NET Core Identity extension, OpenIddict OAuth2/OIDC entities.

### Shared Infrastructure (`DotNetCloud.Core.ServiceDefaults`)

Configures Serilog (structured JSON logging with sensitive-data masking), OpenTelemetry (OTLP export for Prometheus/Jaeger), health check endpoints (`/health`, `/health/ready`, `/health/live`), and security headers middleware (CSP, HSTS, X-Frame-Options, etc.).

## Documentation Update Requirement

**After completing any implementation task, you MUST update both tracking files using targeted edits (not full-file replacement):**

1. **`/docs/IMPLEMENTATION_CHECKLIST.md`** — Mark completed items `✓`, pending items `☐`.
2. **`/docs/MASTER_PROJECT_PLAN.md`** — Update the Quick Status Summary table AND the step's Status, Deliverables (`✓`/`☐`), and Notes.

Always use **targeted edits** (minimal context around changed lines) to preserve Git history. Full file replacement is a last resort only.

**Checkbox format:** Use `✓` (completed) and `☐` (pending). Never use `[x]` / `[ ]`.

## Code Conventions

- **File-scoped namespaces**, nullable reference types enabled, `TreatWarningsAsErrors` — all enforced via `Directory.Build.props`.
- XML doc comments required on all public members.
- Test method naming: `MethodName_Condition_ExpectedResult` (Arrange-Act-Assert).
- Interfaces prefixed with `I`; event types suffixed with `Event`; capability interfaces named `I<CapabilityName>`.
- Current version: `0.1.0-alpha`.

## Development Environment

This project is developed on **Windows 11**. When running shell commands in scripts or docs, prefer PowerShell syntax (`Get-Content`, `Get-ChildItem`, backslash paths). The Claude Code shell itself is bash and uses forward-slash paths.
