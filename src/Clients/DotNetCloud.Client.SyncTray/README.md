# DotNetCloud.Client.SyncTray

> **Purpose:** Desktop system tray application — provides sync status, settings, and file management for DotNetCloud on desktop
> **Type:** Client Application (Avalonia Desktop)
> **Target Framework:** net10.0

## Overview

`DotNetCloud.Client.SyncTray` is the desktop system tray application for DotNetCloud, built with Avalonia UI. It runs in the system tray (Windows/Linux) providing at-a-glance sync status, recent file activity, settings access, and quick actions. It uses `DotNetCloud.Client.Core` for the sync engine and communicates with the DotNetCloud server via REST APIs and gRPC.

## Key Features

- **System Tray Icon** — Lives in the notification area with sync status indication
- **Sync Status Display** — Shows current sync state (idle, syncing, error, paused)
- **Recent Activity** — List of recently synced, added, or modified files
- **Settings Window** — Configure sync folders, selective sync rules, bandwidth limits
- **Notifications** — Desktop notifications for sync events, conflicts, and errors
- **Avalonia UI** — Cross-platform desktop UI framework with Fluent theme
- **Serilog Logging** — Structured logging to console and file

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Client.Core` — Sync engine, API client, local state

### Dependent Projects
- (None — this is a leaf application project)

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point: Avalonia initialization, DI setup |
| `TrayIconManager.cs` | System tray icon management and context menu |
| `App.axaml` | Avalonia application definition with Fluent theme |
| `ViewModels/` | MVVM view models for settings, sync status, activity |
| `Views/` | Avalonia XAML views (settings window, popups) |
| `Services/` | Platform services (notifications, autostart) |
| `Startup/` | Application startup and configuration |
