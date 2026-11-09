# DotNetCloud.Modules.Bookmarks.Host

> **Purpose:** Bookmarks module host process — sandboxed gRPC + REST server that also serves the module's own web UI
> **Type:** Host Process (ASP.NET Core Web Application)
> **Target Framework:** net10.0

## Overview

The host process for the Bookmarks module. Runs as a separate, sandboxed process managed by
`ProcessSupervisor`: it receives a scoped connection string for its own schema, its own DataProtection
key ring and a sanitized `module.json` — never the core's `config.json`.

Since Track C it also serves the module's **own web UI** from `wwwroot`, which the core frames at
`/apps/dotnetcloud.bookmarks/` through its reverse proxy.

## Key Features

- **gRPC bookmark services** — CRUD, folders, search and the lifecycle/health contract
- **REST API** (`/api/v1/bookmarks…`) — the data plane behind the module's own UI page; the acting user
  always comes from the verified `dnc-user-assertion`, never from the payload
- **Module-hosted UI** — `wwwroot/index.html` (app) and `wwwroot/widget/index.html` (Home-page card),
  with `assets/` and `widget/` beside them; served by `UseDefaultFiles`/`UseStaticFiles`
- **Data store** — self-migrating through the injected `DOTNETCLOUD_MODULE_DB_CONNECTION_STRING`
- **User erasure** — `BookmarksUserErasureHandler` (`handlesUserErasure: true`)
- **Health checks** — `/health` and the `/api/info` version probe

## UI notes

- `dncModuleBridge.js` is copied from the SDK at build time — one copy of the protocol, no npm.
- Every asset and API reference in the framed documents is **relative**: the proxy strips
  `/apps/dotnetcloud.bookmarks` before forwarding, so an absolute path would leave the module.
- The frame is opaque-origin: no cookies, no `localStorage`. State lives on this server.
- Bookmarks are user data, so the page builds DOM with `textContent`/`createElement` only, never
  `innerHTML`.

## Projects This Interacts With

### Direct Dependencies

- `DotNetCloud.Core` — core interfaces
- `DotNetCloud.Core.Grpc` — gRPC protocol definitions and the user-assertion pipeline
- `DotNetCloud.Modules.Bookmarks` — module contracts and the HTTP client
- `DotNetCloud.Modules.Bookmarks.Data` — data access layer (PostgreSQL migrations); SQL Server migrations live in the separate `DotNetCloud.Modules.Bookmarks.Data.SqlServer` project

### Dependent Projects

- `DotNetCloud.Core.Server` — launches and manages this process via `ProcessSupervisor` (no compile-time reference to the module)
