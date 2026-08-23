# DotNetCloud.Modules.Notes

> **Purpose:** Notes module core library — rich text note-taking, Markdown editing, and note organization Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Notes module. Provides Blazor UI components for creating and editing rich text notes with Markdown support, note organization with folders and tags, note sharing, and full-text search. Implements `IModuleLifecycle` and `IModuleManifest`.

## Key Features

- **Rich Text Editing** — Markdown editor with live preview
- **Note Organization** — Folders, tags, favoriting, and pinning
- **Note Sharing** — Share notes with users and teams
- **Full-Text Search** — Search across all notes
- **Module Lifecycle** — Implements `IModuleLifecycle`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Notes.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
