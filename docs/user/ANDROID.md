# Android App — User Guide

> **Last Updated:** 2026-09-20

---

## Welcome

The DotNetCloud Android app gives you mobile access to your DotNetCloud server — chat, files, and notifications from your phone or tablet. It supports OAuth2/OIDC secure sign-in, real-time messaging, background message alerts, and offline message caching.

---

## Installation

The Android app is available from:

| Channel         | App ID                          |
| --------------- | ------------------------------- |
| **Google Play** | `net.dotnetcloud.client`        |
| **F-Droid**     | `net.dotnetcloud.client.fdroid` |

Both flavors can be installed side-by-side on the same device. They differ only in app ID — neither uses a push service, and message alerts work the same way in both.

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
| **Background Alerts**  | New-message alert posted by the app's own background check       |
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

## Background Alerts

There is nothing extra to install — the app contains no push service and nothing from Google.

While the app is closed, Android periodically wakes DotNetCloud, which checks your server for new chat alerts and posts a generic notification, such as **New message** or **You were mentioned**. The notification never carries the message text or the sender name: turning the app on and opening the chat is what reads the message from your server.

If alerts aren't arriving, check that your device allows notifications for the app and that the phone can reach your server. These checks are periodic, so an alert may take a few minutes.

---

## Troubleshooting

| Issue                      | What to Do                                                                             |
| -------------------------- | -------------------------------------------------------------------------------------- |
| Can't sign in              | Verify the server URL includes `https://` (and the port if non-standard)               |
| Messages not loading       | Check your connection; cached messages are available offline                           |
| Notifications not arriving | Allow notifications for the app and check the phone can reach your server               |
| Update banner shown        | Update through your app store — the app never self-installs APKs                       |

---

## Related Guides

- [Chat](CHAT.md) — using chat features
- [Getting Started](GETTING_STARTED.md) — files and platform basics
- [Auto-Updates](AUTO_UPDATES.md) — how app updates work
