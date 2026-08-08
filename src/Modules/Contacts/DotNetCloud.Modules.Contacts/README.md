# DotNetCloud.Modules.Contacts

> **Purpose:** Contacts module core library — contact management, address book, and contact group Blazor UI components
> **Type:** Library (Razor Class Library)
> **Target Framework:** net10.0

## Overview

The main library project for the Contacts module. Provides Blazor UI components for managing personal and shared contacts, contact groups, address book browsing, and contact import/export. Implements `IModuleLifecycle` and `IModuleManifest`.

## Key Features

- **Contact Management UI** — Create, edit, delete, and search contacts
- **Contact Groups** — Organize contacts into groups with sharing
- **Address Book** — Browse and search address book entries
- **Import/Export** — vCard import/export support
- **Module Lifecycle** — Implements `IModuleLifecycle` with capability resolution

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and module contracts
- `DotNetCloud.UI.Shared` — Shared Blazor components

### Dependent Projects
- `DotNetCloud.Modules.Contacts.Host` — Hosts this module as a gRPC process
- `DotNetCloud.Core.Server` — Registers Blazor components
- `DotNetCloud.UI.Web` — Aggregates Blazor pages
