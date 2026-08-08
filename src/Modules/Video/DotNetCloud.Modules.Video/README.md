# DotNetCloud.Modules.Video

> **Purpose:** Video module core library — video library browsing, series management, and video playback Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Video module. Provides Blazor UI components for browsing video content by series, season, and episode; video playback with HLS streaming; TMDB metadata enrichment; and watch history. Implements `IModuleLifecycle`.

## Key Features

- **Video Library** — Browse by series, season, episode with poster art
- **Video Playback** — HLS streaming player with seek and quality selection
- **Series Management** — Organize content into series and seasons
- **TMDB Enrichment** — Automatic metadata and poster retrieval from TMDB
- **Watch History** — Track watched episodes and resume playback

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Video.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
