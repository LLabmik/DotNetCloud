# DotNetCloud.Modules.Photos

> **Purpose:** Photos module core library — photo gallery, album management, and image viewing Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Photos module. Provides Blazor UI components for photo browsing, album management, image viewing with zoom, slideshows, photo sharing, and metadata display. Implements `IModuleLifecycle`.

## Key Features

- **Photo Gallery** — Grid and timeline views for photo browsing
- **Album Management** — Create, edit, and share photo albums
- **Image Viewer** — Full-screen viewer with zoom and navigation
- **Slideshow** — Automatic slideshow with transitions
- **Photo Sharing** — Share individual photos or albums
- **Metadata Display** — EXIF data, location, and date information

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Photos.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
