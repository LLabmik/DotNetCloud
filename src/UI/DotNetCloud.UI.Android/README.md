# DotNetCloud.UI.Android

> **Purpose:** Android MAUI Blazor application — the mobile client for the DotNetCloud platform
> **Type:** Client Application (MAUI Blazor Hybrid)
> **Target Framework:** net10.0-android

## Overview

`DotNetCloud.UI.Android` is the Android mobile client for DotNetCloud, built as a .NET MAUI Blazor Hybrid application. It embeds a Blazor WebView within a native Android shell, providing access to native device APIs (camera, storage, notifications) while reusing the same Blazor UI components from `DotNetCloud.UI.Shared`. It connects to the DotNetCloud server via REST APIs and SignalR for real-time updates.

## Key Features

- **MAUI Blazor Hybrid** — Native Android app with Blazor WebView for UI
- **Shared UI Components** — Reuses `DotNetCloud.UI.Shared` Material Icons and layout components
- **Native Device Access** — Camera, file picker, notifications via MAUI Essentials
- **SignalR Client** — Real-time push notifications and data sync
- **Chat Integration** — Uses `DotNetCloud.Modules.Chat` for messaging UI
- **Deep Linking** — Android intent handling for deep links

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces and DTOs
- `DotNetCloud.UI.Shared` — Shared Blazor components (MaterialIcon, layouts, services)
- `DotNetCloud.Modules.Chat` — Chat module Blazor components for messaging

### Dependent Projects
- (None — this is a leaf application project)

## Key Files

| File | Purpose |
|------|---------|
| `MainActivity.cs` | Android entry point and MAUI initialization |
| `MauiProgram.cs` | MAUI app builder: services, fonts, handlers |
| `App.xaml` | MAUI application definition |
| `wwwroot/` | Blazor WebView static assets |
