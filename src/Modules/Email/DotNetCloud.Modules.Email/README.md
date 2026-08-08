# DotNetCloud.Modules.Email

> **Purpose:** Email module core library — email client, inbox management, and compose/send Blazor UI
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library for the Email module. Provides Blazor UI components for email inbox browsing, message reading, compose/send with rich text, attachment management, and folder organization. Implements `IModuleLifecycle`.

## Key Features

- **Inbox Management** — Browse, search, and filter email messages
- **Compose & Send** — Rich text email composition with attachments
- **Folder Organization** — Custom folders, labels, and filtering
- **Attachment Management** — Upload, download, and preview email attachments
- **Module Lifecycle** — Implements `IModuleLifecycle`

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Email.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
