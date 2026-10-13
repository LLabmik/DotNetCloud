# DotNetCloud Android Client

> **App ID:** `net.dotnetcloud.client` (Google Play) / `net.dotnetcloud.client.fdroid` (F-Droid)
> **Version:** 0.1.0-alpha
> **Framework:** .NET MAUI (net10.0-android)
> **Min SDK:** Android 8.0 (API 26)
> **Target SDK:** Android 14 (API 35)

---

## Overview

The DotNetCloud Android client is a .NET MAUI application providing mobile access to DotNetCloud chat and file services. It supports OAuth2/OIDC authentication with PKCE, real-time messaging via SignalR, background message alerts, offline message caching, and multi-server accounts.

## Features

| Feature                     | Description                                                                |
| --------------------------- | -------------------------------------------------------------------------- |
| **OAuth2/OIDC with PKCE**   | Secure authentication via system browser with authorization code flow      |
| **Real-Time Chat**          | SignalR-based persistent connection for instant message delivery           |
| **Files Browser**           | Browse folders, preview and manage files, upload, and create folders       |
| **Background Alerts**       | The app polls the server for new messages and posts a generic notification |
| **Offline Cache**           | SQLite-backed local message cache for offline reading                      |
| **Multi-Server**            | Connect to multiple DotNetCloud instances; switch active server            |
| **Secure Token Storage**    | Android Keystore-backed token persistence                                  |
| **Chat Connection Service** | Keeps the SignalR connection alive while the app process is running        |
| **Build Flavors**           | Separate Google Play and F-Droid builds with conditional compilation       |

## Project Structure

```
src/Clients/DotNetCloud.Client.Android/
├── Auth/                          # OAuth2/OIDC, Keystore token store, token refresh
│   ├── IOAuth2Service.cs / MauiOAuth2Service.cs
│   ├── ISecureTokenStore.cs / AndroidKeyStoreTokenStore.cs
│   └── ITokenRefreshService.cs / TokenRefreshService.cs / AuthenticatedHttpClientHandler.cs
├── Chat/                          # SignalR chat client, chat REST client, limits
├── Files/                         # IFileRestClient (browse/upload) + IWopiRestClient (document editing)
├── Music/ Calendar/ Notes/ Ai/    # One REST client per module (Calendar also has a SignalR client)
├── Messages/                      # In-app message contracts (unread count, calendar/note changes)
├── Services/                      # ~65 cross-cutting services: media auto-upload, chat alerts,
│                                  #   music playback, calendar reminders, offline cache/queue,
│                                  #   module discovery, app updates, quota gate, thumbnails
├── Converters/                    # Value converters (AppConverters, Markdown, LocalTime, …)
├── Controls/                      # Custom controls (AutoLinkLabel, MarkdownWebView)
├── ViewModels/                    # MVVM view models — one per screen
├── Views/                         # XAML pages (.xaml + .xaml.cs), one per screen:
│   ├── LoginPage / LandingPage / SettingsPage
│   ├── ChannelListPage / MessageListPage / ChannelDetailsPage /
│   │   ChatImageViewerPage / DmUserPickerPage
│   ├── FileBrowserPage / ImageViewerPage / DocumentEditorPage
│   ├── MusicPage
│   ├── CalendarPage / EventDetailPage / EventEditPage
│   ├── NotesPage / NoteEditPage
│   └── AiPage
├── Platforms/Android/             # MainActivity, MainApplication, ChatConnectionService,
│                                  #   JobServices and receivers, MusicPlaybackService,
│                                  #   AndroidManifest.xml, Resources/
│   └── iOS/ MacCatalyst/ Windows/ # Platform heads — stubs; Android is the shipping target
├── Resources/                     # Styles/, Fonts/, Images/, AppIcon/, Raw/
├── Properties/
├── App.xaml(.cs)
├── AppShell.xaml(.cs)
├── MainPage.xaml.cs
├── MauiProgram.cs
└── DotNetCloud.Client.Android.csproj
```

## Build & Run

### Prerequisites

- .NET 10 SDK with Android workload (`dotnet workload install android`)
- Android SDK (API 35)
- Visual Studio 2026 or JetBrains Rider with MAUI support

### Build Commands

```bash
# Google Play build (default)
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj

# F-Droid build
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj -p:BuildFlavor=fdroid

# Release APK
dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj -c Release -f net10.0-android
```

### Build Flavors

| Aspect                 | Google Play              | F-Droid                         |
| ---------------------- | ------------------------ | ------------------------------- |
| **Build Command**      | Default                  | `-p:BuildFlavor=fdroid`         |
| **Conditional Symbol** | `GOOGLEPLAY`             | `FDROID`                        |
| **App ID**             | `net.dotnetcloud.client` | `net.dotnetcloud.client.fdroid` |

Both flavors can be installed side-by-side on the same device (different app IDs). They share the
same package set and the same alert code path — neither uses a push service, because background
alerts come from the app's own poll of `GET /api/v1/chat/alerts`.

## Architecture

### MVVM with CommunityToolkit.MVVM

The app follows the MVVM pattern using `CommunityToolkit.Mvvm`:

- **Observable properties** via `[ObservableProperty]` source generator
- **Relay commands** via `[RelayCommand]` for async operations
- **Global IoC** via `Ioc.Default` for service resolution in ViewModels
- **Dependency injection** configured in `MauiProgram.cs`

### Shell Navigation

```
AppShell (flyout drawer — no bottom tab bar)
├── //Login                  # Unauthenticated route (hidden from the drawer)
└── authenticated drawer
    ├── ChannelList          # Chat
    ├── Files                # File browser
    ├── Music                # Shown once the server reports the Music module
    ├── Calendar
    ├── Notes
    ├── Ai                   # Shown once the server reports the AI module
    ├── Settings
    └── Rescan Modules       # Drawer footer action — re-reads module availability
```

### Service Lifetimes

| Service                  | Lifetime  | Notes                                         |
| ------------------------ | --------- | --------------------------------------------- |
| `IOAuth2Service`         | Singleton | OAuth flow coordinator                        |
| `ISecureTokenStore`      | Singleton | Android Keystore wrapper                      |
| `IServerConnectionStore` | Singleton | Preferences-backed                            |
| `IChatSignalRClient`     | Singleton | Long-lived hub connection                     |
| `ILocalMessageCache`     | Singleton | SQLite connection                             |
| `IChatAlertPoller`       | Transient | Typed HTTP client for the chat-alert endpoint |
| `IChatAlertScheduler`    | Singleton | Schedules the background alert poll           |
| ViewModels               | Transient | Fresh per navigation                          |

## Authentication

### OAuth2/OIDC with PKCE

| Setting          | Value                                                  |
| ---------------- | ------------------------------------------------------ |
| **Client ID**    | `dotnetcloud-mobile`                                   |
| **Redirect URI** | `net.dotnetcloud.client://oauth2redirect`              |
| **Scopes**       | `openid profile offline_access files:read files:write` |
| **Flow**         | Authorization Code with PKCE                           |

### Login Flow

1. User enters server URL on `LoginPage`.
2. `LoginViewModel.LoginCommand` calls `IOAuth2Service.AuthenticateAsync()`.
3. `MauiOAuth2Service` generates PKCE challenge and opens system browser.
4. User authenticates on the DotNetCloud server.
5. Server redirects to `net.dotnetcloud.client://oauth2redirect?code=...`.
6. `OAuthCallbackActivity` receives the deep link and signals `MauiOAuth2Service` via `TaskCompletionSource`.
7. `MauiOAuth2Service` exchanges the authorization code for tokens.
8. Tokens are stored securely via `ISecureTokenStore`.
9. Server connection metadata is saved to `IServerConnectionStore`.
10. App navigates to `//Main/ChannelList`.

### Token Storage

- Backend: MAUI `SecureStorage` → Android Keystore
- Keys namespaced by server URL: `dnc_at_{escaped_url}` (access), `dnc_rt_{escaped_url}` (refresh)
- Supports multiple server accounts simultaneously

### Token Refresh

```csharp
var result = await oAuth2Service.RefreshAsync(serverUrl, refreshToken, ct);
```

Tokens are refreshed transparently before expiration.

## Real-Time Chat

### SignalR Connection

- **Hub:** `{serverBaseUrl}/hubs/core`
- **Auth:** Bearer token in query string
- **Reconnect:** Automatic with exponential backoff
- **Background:** `ChatConnectionService` keeps the connection alive while the app process is running (not a foreground service)

### Events

| Event                | Handler                  |
| -------------------- | ------------------------ |
| `UnreadCountUpdated` | Updates channel badges   |
| `NewChatMessage`     | Shows notification popup |

### Chat Connection Service

`ChatConnectionService` keeps the SignalR connection alive while the app process is running. It is deliberately **not** a foreground service — a `dataSync` foreground service would spend the Android 15/16 rolling 24-hour budget. Background message delivery comes from the chat-alert poll instead, so losing the connection when the process is reclaimed is not a regression. The only foreground service in the app is music playback.

## Files Browser

The **Files** drawer entry hosts the file browser (`FileBrowserPage` / `FileBrowserViewModel`), which lists the current folder through `IFileRestClient` and names that folder in the title bar.

- **Toolbar** — **←** goes up one folder (the Android back gesture does the same, via `SystemBackNavigation`); **New folder** and **Upload** act on the current folder; **New document** is shown only when server discovery reports an editor with an editable format.
- **Path bar** — the folder trail (`BreadcrumbItem`) renders directly under the toolbar on its own full-width line. A trail deeper than the screen keeps its **last** segment in view (`ItemsUpdatingScrollMode="KeepLastItemInView"` plus a `ScrollTo(…, ScrollToPosition.End)` on `Breadcrumbs.CollectionChanged`), so the folder you are in is always the visible end of the path. Swipe it sideways for the earlier segments, and tap a segment to jump to that folder.
- **Rows** — thumbnail (images) or type icon, name, size and date. Tap a folder to open it, or a file to open or preview it. Swipe a row left to move it to the trash (confirmation first, permission permitting).
- **Refresh and quota** — pull the list down to reload the folder; a quota bar at the bottom of the screen shows storage usage.

## Offline Support

### Local Message Cache

- **Storage:** SQLite via `sqlite-net-pcl`
- **Database:** `message_cache.db3` in app data directory
- **Operations:**
  - `GetRecentAsync()` — Load cached messages for offline display
  - `UpsertAsync()` — Save/update received messages
  - `PruneAsync()` — Clean old cache entries
- **Strategy:** Display cached messages immediately, sync with server when online

## Background Alerts

The client registers no push device and holds no push transport, so the server never contacts a third party to reach the phone. New-message alerts come from the app's own poll of the server's aggregate endpoint (`GET /api/v1/chat/alerts`):

- `ChatAlertJobService` (JobScheduler, job id 3108) runs one poll and re-arms itself — a minute while unmuted messages are waiting, otherwise five. It uses no foreground service.
- `ChatAlertAlarmReceiver` adds an allow-while-idle alarm path so one poll still runs while the device is dozing.
- Notifications are always generic ("New message", "You were mentioned"): every string is chosen on the device by `NotificationPayloadContract`, so message text is never sent for an alert.

## Android Permissions

| Permission             | Purpose                                              |
| ---------------------- | ---------------------------------------------------- |
| `INTERNET`             | Network access                                       |
| `ACCESS_NETWORK_STATE` | Connectivity detection                               |
| `POST_NOTIFICATIONS`   | Notification display (Android 13+)                   |
| `FOREGROUND_SERVICE`   | Music playback service (the only foreground service) |
| `READ_MEDIA_IMAGES`    | Photo auto-upload                                    |
| `READ_MEDIA_VIDEO`     | Video auto-upload                                    |

## Distribution

### Google Play

Standard AAB/APK signed release via Google Play Console.

### F-Droid

F-Droid-compatible build with no Google dependencies:

- Separate app ID (`net.dotnetcloud.client.fdroid`) for co-installation
- Same package set as the Google Play build — nothing Google-provided in either
- No proprietary libraries

### Direct APK

Release APK available for sideloading from the project's distribution page.

## Dependencies

| Package                               | Version | Purpose                |
| ------------------------------------- | ------- | ---------------------- |
| `Microsoft.Maui.Controls`             | 10.0    | UI framework           |
| `CommunityToolkit.Mvvm`               | 8.4.0   | MVVM source generators |
| `Microsoft.AspNetCore.SignalR.Client` | 10.0.0  | Real-time messaging    |
| `Microsoft.Extensions.Http`           | 10.0.0  | HTTP client factory    |
| `sqlite-net-pcl`                      | 1.9.172 | Local message cache    |

## Test Coverage

| Test Project                       | Tests            | Description                                         |
| ---------------------------------- | ---------------- | --------------------------------------------------- |
| `DotNetCloud.Client.Android.Tests` | See test project | Unit tests for ViewModels, services, and converters |
