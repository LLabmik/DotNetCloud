# DotNetCloud.Modules.Search

> **Purpose:** Search module core library — unified full-text search across all modules with Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Search module. Provides Blazor UI components for global search across all DotNetCloud modules, search result aggregation, faceted filtering, and search suggestions. Implements `IModuleLifecycle`.

## Key Features

- **Global Search** — Search across files, contacts, notes, calendar, music, video, and more
- **Faceted Filtering** — Filter results by module, type, date, and relevance
- **Search Suggestions** — Type-ahead suggestions and recent searches
- **Result Aggregation** — Unified results from all module search providers
- **Module Lifecycle** — Implements `IModuleLifecycle`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Search.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Modules.Search.Client` — gRPC client library for other modules
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
