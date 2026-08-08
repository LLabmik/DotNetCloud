# DotNetCloud.Client.Android

> **Purpose:** Android MAUI client — native Android application for DotNetCloud with file sync capabilities
> **Type:** Client Application (MAUI Android)
> **Target Framework:** net10.0-android

## Overview

`DotNetCloud.Client.Android` is the native Android client for DotNetCloud, built with .NET MAUI. It provides file synchronization, browsing, and management on Android devices, using `DotNetCloud.Client.Core` for the sync engine. It integrates with Android's file system, notifications, and sharing intents to provide a seamless mobile experience.

## Key Features

- **File Sync** — Bidirectional sync of selected folders to/from the DotNetCloud server
- **Native Android Integration** — File picker, notifications, sharing intents
- **Offline Support** — Local SQLite database for offline access to file metadata
- **Background Sync** — Android work manager for background file synchronization
- **MAUI UI** — Native Android UI with MAUI controls

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Client.Core` — Sync engine, API client, local state

### Dependent Projects
- (None — this is a leaf application project)

## Key Files

| File | Purpose |
|------|---------|
| `MainActivity.cs` | Android entry point |
| `MauiProgram.cs` | MAUI app builder and service registration |
| `App.xaml` | MAUI application definition |
