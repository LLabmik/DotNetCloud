# DotNetCloud.UI.Shared

> **Purpose:** Shared Blazor UI component library — reusable Razor components and services used by all DotNetCloud frontends
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

`DotNetCloud.UI.Shared` is the common UI component library for the DotNetCloud platform. It provides reusable Blazor components, services, and resources that are shared across the web UI (`DotNetCloud.UI.Web`), the WASM client (`DotNetCloud.UI.Web.Client`), the Android MAUI Blazor app (`DotNetCloud.UI.Android`), and all module Razor Class Libraries.

## Key Features

- **MaterialIcon Component** — Renders Google Material Icons as inline SVGs with configurable size (Sm/Md/Lg/Xl). No font dependency.
- **MaterialSvgIcons Registry** — Central SVG path mapping for all used Material Icons
- **ModuleIconProvider** — Maps module IDs to Material Icons for navigation and search
- **FileTypeIconProvider** — Maps MIME types to Material Icons for file browsing
- **Form Controls** — Shared input components, validation, and form layouts
- **Layout Components** — Common page layouts, cards, grids, and containers
- **Markdown Rendering** — Markdig-based Markdown-to-HTML with sanitization
- **Localization** — Shared resource strings with `Microsoft.Extensions.Localization`
- **Theming** — CSS variables and theme switching infrastructure
- **Shared wwwroot** — Static assets (CSS, JS, images) available to all consumers

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces (module system for `ModuleIconProvider`)

### Dependent Projects (Projects that reference this one)
- `DotNetCloud.UI.Web` — Blazor Server UI shell
- `DotNetCloud.UI.Web.Client` — Blazor WASM client
- `DotNetCloud.UI.Android` — MAUI Android Blazor app
- Every module `.csproj` (RCL) — Module Blazor components use shared components and icons

## Key Files

| File | Purpose |
|------|---------|
| `Components/DataDisplay/MaterialIcon.razor` | Inline SVG Material Icon component |
| `Components/DataDisplay/MaterialSvgIcons.cs` | SVG path registry for all Material Icons |
| `Services/ModuleIconProvider.cs` | Maps module IDs → Material Icon names |
| `Services/FileTypeIconProvider.cs` | Maps MIME types → Material Icon names |
| `Resources/` | Shared localization strings |
| `wwwroot/` | Shared static assets (CSS, JS, images) |
