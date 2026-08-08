# DotNetCloud.Modules.AI

> **Purpose:** AI module core library — AI-powered features, chat assistant, and content analysis Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the AI module. Provides Blazor UI components for AI-powered chat assistant, content analysis, smart search suggestions, and automated tagging. Implements `IModuleLifecycle`.

## Key Features

- **AI Assistant** — Chat-based AI interface for platform-wide assistance
- **Content Analysis** — AI-powered content classification and tagging
- **Smart Suggestions** — Context-aware recommendations
- **Module Lifecycle** — Implements `IModuleLifecycle`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.AI.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
