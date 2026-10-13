# DotNetCloud.Client.Android

> **Purpose:** Android MAUI client — native Android app for DotNetCloud chat, files, music, calendar, notes and AI
> **Type:** Client Application (MAUI Android)
> **Target Framework:** net10.0-android

## Overview

`DotNetCloud.Client.Android` is the native Android client for DotNetCloud, built with .NET MAUI. It is a **server-backed client**: chat, files, music, calendar, notes and AI all talk to the server's REST APIs and SignalR hub. There is **no server→device file sync** on Android — files are browsed and managed where they live on the server, images are previewed in-app, and office documents open in an embedded Collabora editor whose saves go straight back to the server.

Shared infrastructure comes from `DotNetCloud.Client.Core`: the OAuth2/OIDC helpers (`Auth/`), the API models and HTTP pipeline handlers (`Api/` — timeout, throttling, correlation id, device identity) and the client update service (`Services/`). The desktop sync engine that also lives in that project is not used here.

## Key Features

- **Chat** — SignalR real-time messaging with channels, direct messages, mute state and an offline message cache
- **Files** — folder browser with a tappable path bar, upload, folder creation, image preview, swipe-to-delete (to trash) and quota display
- **Document editing** — office documents open in an in-app Collabora editor (WOPI); nothing is mirrored to the device
- **Photo/video auto-upload** — a MediaStore watcher backs up new media through the chunked-upload pipeline, with Wi-Fi / battery / size / quota gates
- **Music** — library browsing and background playback (the app's only foreground service) with an equalizer
- **Calendar, Notes and AI** — module tabs that appear only when the server reports those modules
- **Background alerts** — in-process poll of the server's alert endpoint; no push service and no Google dependency in either build flavor

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Client.Core` — shared OAuth2/OIDC helpers, API models and HTTP handlers, client update service

### Dependent Projects
- (None — this is a leaf application project)

## Key Files

| File | Purpose |
|------|---------|
| `MauiProgram.cs` | MAUI app builder: DI registration, HTTP clients, page/view-model wiring |
| `App.xaml(.cs)` | MAUI application definition and startup |
| `AppShell.xaml(.cs)` | Flyout drawer, routes, and the module-gated Music/AI entries |
| `Platforms/Android/MainActivity.cs` | Android entry point |
| `Platforms/Android/MainApplication.cs` | Process-level startup (logcat logger, auto-upload watcher) |
| `Views/` | XAML pages — one per screen (`FileBrowserPage`, `ChannelListPage`, …) |
| `ViewModels/` | MVVM view models — one per screen |
| `Services/` | Cross-cutting services (auto-upload, alerts, playback, reminders, offline queue) |

See [docs/clients/android/README.md](../../../docs/clients/android/README.md) for the full architecture, build instructions and feature detail.
