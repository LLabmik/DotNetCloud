# Bookmarks Browser Extension — User Guide

> **Last Updated:** 2026-08-27

---

## Welcome

The DotNetCloud Bookmarks browser extension syncs your browser bookmarks bidirectionally with your DotNetCloud server. Save pages while you browse and keep your bookmarks available everywhere — in the [Bookmarks](BOOKMARKS.md) web app and across devices.

The extension supports **Chrome** (Manifest V3) and **Firefox** (Manifest V3, Firefox 109 or later).

---

## Installation

### Chrome

1. Open the extension's listing in the **Chrome Web Store** (or load it from your administrator's distribution)
2. Click **Add to Chrome**
3. The extension icon appears in the toolbar

### Firefox

1. Open the extension's listing on **Firefox Add-ons (AMO)**
2. Click **Add to Firefox**
3. The extension icon appears in the toolbar

> **Note:** If your organization distributes the extension privately, install it from the ZIP provided by your administrator.

---

## Connecting to Your Server

1. Click the extension icon → the **Connect to Server** screen appears
2. Enter your DotNetCloud server URL (e.g., `https://cloud.example.com`)
3. Click **Connect to Server**
4. A browser tab opens for authorization — log in to your server and approve the device
5. The popup transitions to the main UI and initial sync begins automatically

Bookmarks from your DotNetCloud server appear in your browser's bookmark tree within a few minutes. Changes you make in the browser are synced back to the server.

---

## Using the Extension

The extension popup provides:

- **Save** — save the current page as a bookmark
- **Browse** — browse your saved bookmarks
- **Search** — search your bookmarks by title, URL, or tags

---

## How Sync Works

- Bookmarks sync **bidirectionally** — changes in the browser go to the server and vice versa
- Initial sync happens when you connect; after that, sync runs automatically
- Bookmarks from the server appear in your browser's bookmark tree

---

## Troubleshooting

| Issue                            | What to Do                                                                          |
| -------------------------------- | ----------------------------------------------------------------------------------- |
| "Not authenticated" on the popup | Click **Connect** and complete the device-flow login again                          |
| Bookmarks not syncing            | Confirm the Bookmarks module is enabled on the server and the server URL is correct |
| "Failed to fetch" errors         | The server is unreachable or there's a TLS certificate issue                        |
| Initial sync hangs               | Large bookmark trees may take a moment; check the extension's console for errors    |

---

## Related Guides

- [Bookmarks](BOOKMARKS.md) — manage bookmarks in the web app
- [Getting Started](GETTING_STARTED.md) — general platform basics
