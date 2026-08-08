# DotNetCloud.Modules.Bookmarks

> **Purpose:** Bookmarks module core library — bookmark management, collection organization, and browser extension sync Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Bookmarks module. Provides Blazor UI components for managing bookmarks, organizing them into collections, tagging, and syncing with browser extensions. Implements `IModuleLifecycle`.

## Key Features

- **Bookmark Management** — Create, edit, delete, and search bookmarks
- **Collections** — Organize bookmarks into folders and collections
- **Tagging** — Tag-based bookmark organization
- **Browser Extension Sync** — Sync bookmarks with browser extensions
- **Import/Export** — Import bookmarks from browsers and other services

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Bookmarks.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
