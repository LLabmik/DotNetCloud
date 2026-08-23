# DotNetCloud.UI.Web

> **Purpose:** Blazor Server web UI — the main web frontend for the DotNetCloud platform
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

`DotNetCloud.UI.Web` is the Blazor Server-side web application that provides the primary browser-based user interface for DotNetCloud. It is hosted by `DotNetCloud.Core.Server` and aggregates UI components from all module Razor Class Libraries, composing them into a unified navigation shell with sidebar, top bar, and content area. The server pre-renders pages for fast initial load, then hands off to the Blazor WebAssembly client (`DotNetCloud.UI.Web.Client`) for interactive operation.

## Key Features

- **Module UI Composition** — Aggregates Razor components from all 15 modules into a single shell
- **Navigation Shell** — Sidebar navigation, top bar, user menu, and search
- **Server-Side Pre-Rendering** — Fast initial page load with Blazor Server rendering
- **Layout System** — Shared layouts, page templates, and responsive design
- **Module Discovery** — Dynamically discovers and registers module Blazor pages and navigation items
- **Service Integration** — Authentication, localization, and theming services

## Projects This Interacts With

### Direct Dependencies (Core)
- `DotNetCloud.Core` — Core interfaces and DTOs
- `DotNetCloud.Core.Auth` — Authentication services
- `DotNetCloud.Core.Data` — Data access for UI-bound queries

### Direct Dependencies (UI)
- `DotNetCloud.UI.Shared` — Shared Blazor components (MaterialIcon, layouts, etc.)
- `DotNetCloud.UI.Web.Client` — Blazor WASM client project

### Direct Dependencies (Modules — all 15)
References every module's main RCL project for Blazor component registration:
- `DotNetCloud.Modules.Calendar`, `Chat`, `Contacts`, `Files`, `Music`, `Notes`, `Photos`, `Video`, `AI`, `Bookmarks`, `Email`, `About`, `Tracks`, `Example`, `Search`

### Dependent Projects
- `DotNetCloud.Core.Server` — Hosts this UI as its web frontend

## Key Files

| File | Purpose |
|------|---------|
| `Components/` | Blazor page components for the web shell |
| `Services/` | UI-specific services (navigation, module discovery) |
| `_Imports.razor` | Shared Razor directives and usings |
