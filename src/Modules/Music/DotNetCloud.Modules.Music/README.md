# DotNetCloud.Modules.Music

> **Purpose:** Music module core library — music library browsing, playlist management, and audio playback Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Music module. Provides Blazor UI components for browsing music by artist, album, genre, and track; creating and managing playlists; audio playback controls; and MusicBrainz metadata enrichment. Implements `IModuleLifecycle`.

## Key Features

- **Music Library** — Browse by artist, album, genre, and track with cover art
- **Playlist Management** — Create, edit, and share playlists
- **Audio Playback** — Persistent playbar with play/pause/skip/volume controls
- **Metadata Enrichment** — MusicBrainz integration for artist bios and album info
- **Search** — Full-text search across the music library

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Music.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
