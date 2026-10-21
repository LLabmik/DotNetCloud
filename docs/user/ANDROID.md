# Android App — User Guide

> **Last Updated:** 2026-10-21

---

## Welcome

The DotNetCloud Android app gives you mobile access to your DotNetCloud server — chat, files, music, calendar, notes, and AI — from your phone or tablet. Sign-in uses OAuth2/OIDC in your browser; messages arrive in real time while the app is open and through the app's own background check when it is closed.

**Requirements:** Android 8.0 (API 26) or later, and network access to your DotNetCloud server (HTTPS recommended). The Music and AI screens appear only when your server has those modules enabled.

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
3. Tap **Connect**
4. Your browser opens — log in with your DotNetCloud credentials and approve the device
5. The app receives the login and opens your chat

Your login tokens are stored securely in the Android Keystore.

---

## Getting Around

Open the drawer (☰) in the top-left corner to move between screens:

| Screen       | What it is for                                                 |
| ------------ | -------------------------------------------------------------- |
| **Chat**     | Channels and direct messages                                   |
| **Files**    | Your file storage                                              |
| **Music**    | Streaming player — shown only when the Music module is enabled |
| **Calendar** | Events, reminders, and shared calendars                        |
| **Notes**    | Markdown notes and folders                                     |
| **AI**       | Chat with the server's AI assistant — shown only when enabled  |
| **Settings** | Account, auto-upload, permissions, notifications, updates      |

**Rescan Modules** sits at the bottom of the drawer (and in Settings). If a module was just installed or enabled on the server, tap it to refresh which screens appear.

The phone's back gesture or button moves back one step inside a screen — closing the search panel, leaving a note preview, or going up a folder — and only leaves the app when there is nothing left to go back to.

---

## Features

| Feature                    | Description                                                                 |
| -------------------------- | --------------------------------------------------------------------------- |
| **Real-Time Chat**         | Channels and DMs over a persistent connection, with unread badges            |
| **Files**                  | Browse folders, preview files, upload, create folders, and edit documents    |
| **Photo & Video Upload**   | Back up your camera roll automatically, with Wi-Fi and date-folder options   |
| **Music**                  | Stream your library with background playback and notification controls       |
| **Calendar**               | Month, week, and day views; events with reminders and recurrence             |
| **Notes**                  | Markdown editor with folders, search, pinning, and favorites                 |
| **AI Assistant**           | Streaming conversations with the server's AI model                           |
| **Background Alerts**      | New-message alerts posted by the app's own background check — no push service |
| **Offline Cache**          | Read previously loaded messages and queue outgoing messages without a connection |
| **Secure Token Storage**   | Sign-in tokens are kept in the Android Keystore                              |

---

## Chat

Open **Chat** from the drawer. The list is grouped into **Channels** and **Direct Messages**.

- Unread counts appear on the right of each row.
- A muted conversation shows a muted-bell icon. Tap the bell (🔔 / 🔕) on any row to mute or unmute it without opening the conversation.
- Direct messages show a presence dot next to the name: green when the person is online, yellow when away, red when they have set Do Not Disturb, and gray when offline.
- Tap **+** in the title bar and search for a person to start a new direct message.

### Conversations

- Messages arrive in real time. A typing indicator shows when other people are writing.
- Use the search icon (🔍) to search messages in the conversation.
- **Details** lists the members and lets you mute notifications or leave a channel.
- The composer supports text, an emoji picker, and photo attachments (📎). A photo is uploaded before it is sent and must be within the server's attachment size limit.
- Links in messages show a preview card.
- While offline, text messages are queued and sent when you reconnect; photo attachments need a connection.

---

## Files

Open **Files** from the menu to browse your files. The folder you are in is named in the title bar, and the toolbar carries the actions that apply to it.

### The path bar

Directly under the toolbar, a trail shows where you are — for example `My Files › Documents › Writing`.

- **Tap a segment** to jump straight back to that folder, instead of walking back one folder at a time.
- **Swipe the trail sideways** to scroll it. However deep you are, the trail stays scrolled to the folder you are currently in, so the end of the path is the part you see.

### Toolbar actions

| Action              | What it does                                                                              |
| ------------------- | ----------------------------------------------------------------------------------------- |
| **←**               | Goes up one folder. The phone's back gesture does the same.                                |
| **📄+** (New document) | Creates an editable document in the current folder. Only shown when your server offers the document editor. |
| **📁+** (New folder)   | Creates a folder inside the current folder. Only shown where you can write.               |
| **📤** (Upload)        | Sends one or more files from your device into the current folder.                          |

### Working with items

- **Tap a folder** to open it, or **tap a file** to open or preview it. Images open in a full-screen viewer; office documents open in the in-app editor.
- Items you can only read — for example a folder shared with you as read-only — show a **view only** label, and the create/upload buttons are hidden while you are inside them.
- **Pull the list down** to reload the current folder.
- **Swipe a row to the left** to move that file or folder to the trash; you are asked to confirm first.
- While an upload is running, its filename and progress appear above the list.

At the top of the screen an auto-upload strip shows whether camera-roll backup is on and when it last ran. The bar at the bottom of the screen shows how much of your storage you are using.

---

## Photo & Video Auto-Upload

Open **Settings → FILE SYNC** to control camera-roll backup:

| Setting                 | What it does                                                        |
| ----------------------- | ------------------------------------------------------------------- |
| **Auto-Upload Media**   | Backs up new photos and videos from your device                     |
| **WiFi Only**           | Uploads only while connected to Wi-Fi (on by default)               |
| **Organize by Date**    | Files upload into `YYYY/MM` folders (on by default)                 |
| **Upload Folder**       | Destination folder name — `AutoUpload` unless you change it          |
| **Sync Now**            | Runs an upload pass immediately                                     |

On first use Android asks for permission to read your photos and videos. If you declined, the **PHOTOS ACCESS** card in Settings shows a **Fix** button that opens the permission prompt. Auto-upload also respects your storage quota; when the quota is full it stops and posts a notification.

For reliable background uploads, use **BACKGROUND ACTIVITY → Battery Optimization → Fix** so Android does not pause the app when the screen is off.

---

## Accounts and Servers

The app connects to one server at a time. The **ACCOUNT** card in Settings shows the server name, your email address, and the server URL, plus the app version.

To switch to a different DotNetCloud server:

1. Open **Settings** and tap **Log Out**. This removes the sign-in tokens for the current server.
2. On the sign-in screen, enter the other server's address. The last address you used is filled in for you.
3. Sign in again through your browser.

---

## Music

Open **Music** from the drawer. The screen has four tabs — **Artists**, **Albums**, **Tracks**, and **Playlists** — and an alphabet rail for jumping through long lists. The search icon in the title bar searches the active Artists, Albums, or Tracks tab.

- **Tap a track** to start playback. The rest of the list you are looking at is queued, so playback continues in the order shown.
- The now-playing bar has album art, track and artist, a seek slider, and play/pause and previous/next buttons.
- While a track is loaded, the title bar also shows the repeat button. Tap it to cycle **Off → One → All**. Off stops at the end of the queue; One replays the current track; All wraps back to the start.
- Playback continues in the background with a media notification, including controls on the lock screen.
- Tracks are streamed from your server; nothing is downloaded for offline play. Playback uses your phone's audio output as-is — the app has no built-in equalizer.

If the Music entry is missing from the drawer, the server does not have the Music module enabled. Tap **Rescan Modules** at the bottom of the drawer after your administrator enables it.

---

## Calendar

Open **Calendar** from the drawer. Switch between **Month**, **Week**, and **Day** views; use the arrows to change period and **Today** to jump back.

- Tap a day to see its events, or tap **+ New Event** to create one.
- An event can have a title, Markdown description (with preview), location, start and end date/time, all-day flag, calendar, URL (for example a video-call link), reminder, and recurrence pattern.
- Reminders arrive as notifications. If they are late or missing, use the **CALENDAR REMINDERS → Exact Alarm Permission → Fix** and **NOTIFICATIONS → Fix** cards in Settings.

---

## Notes

Open **Notes** from the drawer.

- Type in the search box to filter notes, and use the folder chips to show a single folder or **All Notes**.
- Tap **+** to create a note. The editor has Markdown formatting buttons (bold, italic, heading, lists, link, code, quote) and an **Edit / Preview** toggle.
- Choose a folder for the note at the bottom of the editor, or leave it unfiled.
- Tap a note to preview it, then **Edit** or **Delete**.
- Each note row has **📌** (pin), **⭐** (favorite), and **🗑️** (delete) actions. Pinned and favorited notes are marked in the list.

---

## AI Assistant

Open **AI** from the drawer (shown only when the server has the AI module). Each conversation keeps its own history.

- Tap **New chat** to start a conversation. The model used is shown at the top.
- Responses stream in as they are generated; a **Thought process** section can be expanded while the assistant is reasoning.
- Tap **Abort** to stop a long response.
- Swipe a conversation in the list to **rename** or **delete** it.

If the screen shows that AI is unavailable, the server's model backend (Ollama) is not reachable — ask your administrator to check it.

---

## Settings and Permissions

Settings is split into cards:

| Card                    | What it controls                                                             |
| ----------------------- | ---------------------------------------------------------------------------- |
| **Update**              | A newer app version is offered with **Update** and **Dismiss**               |
| **Account**             | Server, email, URL, and the installed app version                            |
| **File Sync**           | Auto-upload options and **Sync Now** (see above)                             |
| **Photos Access**       | Fix a denied photos/videos permission                                        |
| **Background Activity** | Ask Android to stop restricting the app's background work                    |
| **Calendar Reminders**  | Fix the exact-alarm permission so reminders are not delayed                  |
| **Notifications**       | Fix a blocked notification permission                                        |
| **Chat Notifications**  | **Do Not Disturb** and the in-app **Message Sound**                          |
| **Modules**             | **Rescan Modules** after a server module changes                             |
| **Log Out**             | Removes this server's sign-in tokens                                         |

**Do Not Disturb** blocks notifications for new direct messages. It only silences alerts — it does not change your status for other people.

---

## Background Alerts

There is nothing extra to install — the app contains no push service and nothing from Google.

While the app is closed, Android periodically wakes DotNetCloud, which checks your server for new chat alerts and posts a generic notification, such as **New message** or **You were mentioned**. The notification never carries the message text or the sender name: turning the app on and opening the chat is what reads the message from your server.

If alerts aren't arriving, check that your device allows notifications for the app and that the phone can reach your server. These checks are periodic, so an alert may take a few minutes. **Settings → CHAT NOTIFICATIONS → Do Not Disturb** stops new-DM alerts while you want quiet; **Message Sound** controls the in-app ding while the app is open.

---

## Offline Use

Chat messages you have already loaded stay readable without a connection. If you send a message while offline, it is queued and sent automatically when you reconnect. Files, music, and the AI assistant need a live connection to your server.

---

## Troubleshooting

| Issue                        | What to Do                                                                                  |
| ---------------------------- | ------------------------------------------------------------------------------------------- |
| Can't sign in                | Verify the server URL includes `https://` (and the port if non-standard) and that the phone can reach the server |
| Music or AI missing from the drawer | The server module is not enabled — tap **Rescan Modules** after your administrator enables it |
| Auto-upload not running      | Allow photos/videos access (**Photos Access → Fix**), set battery use to unrestricted, and check **WiFi Only** if you are on mobile data |
| Auto-upload stopped          | Your storage quota may be full — free space, then tap **Sync Now**                          |
| Calendar reminders are late  | Allow notifications and fix the exact-alarm permission in Settings                          |
| Notifications not arriving   | Allow notifications for the app, check **Do Not Disturb**, and check the phone can reach your server |
| Messages not loading         | Check your connection; cached messages are available offline                                |
| Attachment rejected          | The image is larger than the server's attachment size limit                                 |
| Update banner shown          | Update through your app store — the app never self-installs APKs                            |

---

## Related Guides

- [Chat](CHAT.md) — using chat features
- [Getting Started](GETTING_STARTED.md) — files and platform basics
- [Auto-Updates](AUTO_UPDATES.md) — how app updates work
