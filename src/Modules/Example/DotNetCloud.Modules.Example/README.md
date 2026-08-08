# DotNetCloud.Modules.Example

> **Purpose:** Example module core library — reference implementation demonstrating module patterns (Blazor UI, events, services)
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library project for the Example module. It contains the module lifecycle implementation (`ExampleModule`), module manifest, domain models, domain events, event handlers, and Blazor UI components. Serves as the canonical reference for all module developers.

## Key Features

- **Module Lifecycle** — `ExampleModule` demonstrates `IModuleLifecycle` (Initialize, Start, Stop, Dispose)
- **Module Manifest** — `ExampleModuleManifest` declares capabilities and event contracts
- **Domain Events** — `NoteCreatedEvent`, `NoteDeletedEvent` with corresponding event handlers
- **Blazor UI** — Example notes page with create form and note display components
- **Capability Usage** — Demonstrates resolving `INotificationService` and `IStorageProvider` capabilities

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Example.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
