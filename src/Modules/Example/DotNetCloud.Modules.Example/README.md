# DotNetCloud.Modules.Example

> **Purpose:** Example module core library — reference implementation demonstrating module patterns (domain model, events, manifest, lifecycle)
> **Type:** Class library
> **Target Framework:** net10.0

## Overview

The main library project for the Example module. It contains the module lifecycle implementation (`ExampleModule`), module manifest, domain models, domain events and event handlers. Serves as the canonical reference for all module developers.

The UI is **not** here: a sandboxed module serves its own UI from its own host process (`DotNetCloud.Modules.Example.Host/wwwroot`), which is why this project depends on no core UI assembly.

## Key Features

- **Module Lifecycle** — `ExampleModule` demonstrates `IModuleLifecycle` (Initialize, Start, Stop, Dispose)
- **Module Manifest** — `ExampleModuleManifest` declares capabilities and event contracts
- **Domain Events** — `NoteCreatedEvent`, `NoteDeletedEvent` with corresponding event handlers
- **Capability Usage** — demonstrates requesting `INotificationService`/`IAuditLogger` and only what it uses (least privilege)

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`

### Dependent Projects
- `DotNetCloud.Modules.Example.Host` — Hosts this module as a gRPC process and serves its web UI

> The core does **not** reference this project: `DotNetCloud.Core.Server` and `DotNetCloud.UI.Web` contain no Example code or pages (Track C decoupling).
