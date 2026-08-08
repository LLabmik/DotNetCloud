# DotNetCloud.UI.Web.Client

> **Purpose:** Blazor WebAssembly (WASM) client — the browser-side interactive runtime for the DotNetCloud web UI
> **Type:** Client Application (Blazor WebAssembly)
> **Target Framework:** net10.0

## Overview

`DotNetCloud.UI.Web.Client` is the Blazor WebAssembly client that runs in the user's browser. After the initial server-side pre-render by `DotNetCloud.UI.Web`, this WASM app takes over for full interactivity. It handles client-side routing, API calls, authentication token management, and real-time SignalR connections — providing a responsive single-page application experience.

## Key Features

- **Blazor WebAssembly Runtime** — Full .NET runtime in the browser for interactive UI
- **Client-Side Routing** — Page navigation without full page reloads
- **Authentication** — OAuth 2.0 / OIDC token management via `Microsoft.AspNetCore.Components.WebAssembly.Authentication`
- **API Client** — HTTP client for REST API calls to the core server
- **Localization** — Client-side string localization
- **Shared Components** — Uses `DotNetCloud.UI.Shared` for consistent UI components
- **Module Pages** — Renders module-provided Blazor pages in the client shell

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and DTOs
- `DotNetCloud.UI.Shared` — Shared Blazor components (MaterialIcon, form controls, layouts)

### Dependent Projects
- `DotNetCloud.UI.Web` — References this project as its WASM client companion
- `DotNetCloud.Core.Server` — Serves the WASM assets to browsers

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | WASM application entry point: service registration, auth setup |
| `Pages/` | Client-side page components |
| `Services/` | Client-side services (API client, state management) |
| `Shared/` | Layout components for the WASM shell |
