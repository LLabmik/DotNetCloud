# DotNetCloud.Modules.Calendar

> **Purpose:** Calendar module core library — calendar management, event scheduling, and recurrence Blazor UI components
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Calendar module. Provides Blazor UI components for calendar views (day, week, month, agenda), event creation and editing, recurring event management, reminders, and calendar sharing. Implements `IModuleLifecycle` and `IModuleManifest`.

## Key Features

- **Calendar Views** — Day, week, month, and agenda view components
- **Event Management** — Create, edit, delete, and drag-to-reschedule events
- **Recurring Events** — Daily, weekly, monthly, yearly recurrence rules with exceptions
- **Reminders** — Event reminders with notification integration
- **Calendar Sharing** — Share calendars with users and teams
- **Module Lifecycle** — Implements `IModuleLifecycle`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Calendar.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
