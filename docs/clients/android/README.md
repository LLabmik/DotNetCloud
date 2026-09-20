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
| **Background Alerts**       | The app polls the server for new messages and posts a generic notification |
| **Offline Cache**           | SQLite-backed local message cache for offline reading                      |
| **Multi-Server**            | Connect to multiple DotNetCloud instances; switch active server            |
| **Secure Token Storage**    | Android Keystore-backed token persistence                                  |
| **Chat Connection Service** | Keeps the SignalR connection alive while the app process is running        |
| **Build Flavors**           | Separate Google Play and F-Droid builds with conditional compilation       |

## Project Structure

```
src/Clients/DotNetCloud.Client.Android/
├── Auth/                          # OAuth2 & token management
│   ├── IOAuth2Service.cs
│   ├── MauiOAuth2Service.cs
│   ├── ISecureTokenStore.cs
│   └── AndroidKeyStoreTokenStore.cs
├── Chat/                          # SignalR real-time messaging
│   ├── IChatSignalRClient.cs
│   ├── SignalRChatClient.cs
│   ├── IChatRestClient.cs
│   └── HttpChatRestClient.cs
├── Services/                      # Core services
│   ├── IChatAlertPoller.cs
│   ├── IChatAlertScheduler.cs
│   ├── NotificationPayloadContract.cs
│   ├── IServerConnectionStore.cs
│   ├── PreferenceServerConnectionStore.cs
│   ├── ILocalMessageCache.cs
│   ├── SqliteMessageCache.cs
│   └── AccessTokenUserIdExtractor.cs
├── ViewModels/                    # MVVM ViewModels
│   ├── LoginViewModel.cs
│   ├── ChannelListViewModel.cs
│   ├── MessageListViewModel.cs
│   └── SettingsViewModel.cs
├── Views/                         # XAML pages
│   ├── LoginPage.xaml(.cs)
│   ├── ChannelListPage.xaml(.cs)
│   ├── MessageListPage.xaml(.cs)
│   └── SettingsPage.xaml(.cs)
├── Converters/
│   └── AppConverters.cs
├── Platforms/Android/
│   ├── MainActivity.cs
│   ├── MainApplication.cs
│   ├── OAuthCallbackActivity.cs
│   └── Resources/values/colors.xml
├── Resources/
│   ├── Styles/Colors.xaml, Styles.xaml
│   ├── Fonts/OpenSans-Regular.ttf, OpenSans-Semibold.ttf
│   ├── Images/
│   ├── AppIcon/
│   └── Splash/
├── App.xaml(.cs)
├── AppShell.xaml(.cs)
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
AppShell
├── //Login              # Unauthenticated route
└── //Main               # Authenticated tab bar
    ├── //Main/ChannelList   # Tab 1: Channel list
    ├── //Main/Settings      # Tab 2: Settings
    └── MessageList          # Detail route (push navigation)
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
