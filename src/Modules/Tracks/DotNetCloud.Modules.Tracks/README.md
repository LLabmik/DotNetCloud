# DotNetCloud.Modules.Tracks

> **Purpose:** Tracks module core library — issue tracking, project management, and work item Blazor UI components
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Tracks module. Provides Blazor UI components for issue tracking, Kanban boards, sprint planning, work item management, and project dashboards. Implements `IModuleLifecycle` and `IModuleManifest`.

## Key Features

- **Work Item Management** — Create, edit, and track issues, tasks, bugs, and epics
- **Kanban Board** — Drag-and-drop Kanban board with customizable columns
- **Sprint Planning** — Sprint creation, backlog grooming, and velocity tracking
- **Project Dashboards** — Overview of project health and progress
- **Module Lifecycle** — Implements `IModuleLifecycle`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Tracks.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
