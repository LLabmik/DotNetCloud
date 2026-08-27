# Android App — User Guide

> **Last Updated:** 2026-08-27

---

## Welcome

The DotNetCloud Android app gives you mobile access to your DotNetCloud server — chat, files, and notifications from your phone or tablet. It supports OAuth2/OIDC secure sign-in, real-time messaging, push notifications, and offline message caching.

---

## Installation

The Android app is available from:

| Channel         | App ID                          | Push Notifications                   |
| --------------- | ------------------------------- | ------------------------------------ |
| **Google Play** | `net.dotnetcloud.client`        | Firebase Cloud Messaging (FCM)       |
| **F-Droid**     | `net.dotnetcloud.client.fdroid` | UnifiedPush (no Google dependencies) |

Both flavors can be installed side-by-side on the same device.

### Direct APK

Signed APKs are also published on [GitHub Releases](https://github.com/LLabmik/DotNetCloud/releases):

1. Download the APK for your preferred flavor
2. Verify the SHA-256 checksum against `checksums-sha256.txt` (optional but recommended)
3. Enable sideloading: **Settings → Apps → Special app access → Install unknown apps**
4. Open the APK and tap **Install**

> **Security:** The app does not download or install updates directly — updates are always obtained through your app store.

---

## Signing In

1. Open the app
2. Enter your DotNetCloud server URL (e.g., `https://cloud.example.com`)
3. Tap **Sign In**
4. Your browser opens — log in with your DotNetCloud credentials and approve the device
5. The app receives the login and opens your chat

Your login tokens are stored securely in the Android Keystore.

---

## Features

| Feature                | Description                                                     |
| ---------------------- | --------------------------------------------------------------- |
| **Real-Time Chat**     | Instant message delivery via a persistent connection            |
| **Files**              | Browse and access your files                                    |
| **Photo Auto-Upload**  | Automatically back up photos from your device                   |
| **Push Notifications** | Offline notifications via FCM (Play) or UnifiedPush (F-Droid)   |
| **Offline Cache**      | Read previously loaded messages without a connection            |
| **Multi-Server**       | Connect to multiple DotNetCloud servers and switch between them |

---

## Multi-Server Accounts

You can connect to more than one DotNetCloud instance:

1. Open **Settings**
2. Add another server
3. Switch the active server from the settings screen

Each server keeps its own login tokens and connection.

---

## Push Notifications

- **Google Play build:** uses Firebase Cloud Messaging (FCM)
- **F-Droid build:** uses UnifiedPush — install a distributor app (e.g., ntfy, Gotify UP) and the app registers with it on first launch

If notifications aren't arriving, check that push is enabled for the server and that your device allows notifications for the app.

---

## Troubleshooting

| Issue                      | What to Do                                                                             |
| -------------------------- | -------------------------------------------------------------------------------------- |
| Can't sign in              | Verify the server URL includes `https://` (and the port if non-standard)               |
| Messages not loading       | Check your connection; cached messages are available offline                           |
| Notifications not arriving | Confirm push is enabled and the distributor (F-Droid) or Firebase (Play) is configured |
| Update banner shown        | Update through your app store — the app never self-installs APKs                       |

---

## Related Guides

- [Chat](CHAT.md) — using chat features
- [Getting Started](GETTING_STARTED.md) — files and platform basics
- [Auto-Updates](AUTO_UPDATES.md) — how app updates work
