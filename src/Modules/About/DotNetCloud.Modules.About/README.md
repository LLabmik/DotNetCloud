# DotNetCloud.Modules.About

> **Purpose:** About module core library — system information, version display, and about page Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the About module. Provides Blazor UI components for displaying system information, installed module versions, license information, and platform statistics. A lightweight module with no data layer — it reads information directly from the module registry and system APIs.

## Key Features

- **System Information** — Display server version, runtime info, and system stats
- **Module Registry** — List all installed modules with versions and status
- **License Information** — Display open-source licenses and attributions
- **About Page** — Platform branding and information

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module registry
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.About.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
