# DotNetCloud Bookmarks — Browser Extension

Sync your browser bookmarks bidirectionally with your DotNetCloud server.  
Supports **Chrome** (Manifest V3) and **Firefox** (Manifest V3, Firefox ≥ 109).

---

## Prerequisites

- **Node.js** ≥ 18 and **npm** ≥ 9
- A running DotNetCloud server with the Bookmarks module enabled
- (The server must have the `dotnetcloud-browser-extension` OIDC client registered — this is set up automatically during server initialization)

---

## Building

```bash
# From the extension directory
cd src/Clients/DotNetCloud.Client.BrowserExtension

# Install dependencies (first time only)
npm install

# Build for both browsers
npm run build

# Or build individually
npm run build:chrome   # → dist/chrome/
npm run build:firefox  # → dist/firefox/
```

### Quick build + package

**Linux:**
```bash
./build-extension.sh
```

**Windows (PowerShell):**
```powershell
.\build-extension.ps1
```

Both scripts produce ZIP archives in `dist/`:
- `dist/dotnetcloud-bookmarks-chrome.zip`
- `dist/dotnetcloud-bookmarks-firefox.zip`

---

## Loading in Chrome

1. Open `chrome://extensions`
2. Enable **Developer mode** (toggle top-right)
3. Click **Load unpacked**
4. Select the `dist/chrome/` folder

The extension icon appears in the toolbar. Click it to open the popup.

### Packaging for distribution

The ZIP file (`dist/dotnetcloud-bookmarks-chrome.zip`) can be uploaded to the Chrome Web Store or distributed manually. To install from a ZIP:

1. Unzip to a folder
2. Follow steps 1–3 above, pointing to the unzipped folder

---

## Loading in Firefox

### Temporary add-on (development)

1. Open `about:debugging#/runtime/this-firefox`
2. Click **Load Temporary Add-on…**
3. Select `dist/firefox/manifest.json`

The extension loads until Firefox is restarted.

### Permanent installation (signed)

The ZIP file (`dist/dotnetcloud-bookmarks-firefox.zip`) can be submitted to Mozilla Add-ons (AMO) for signing. Once signed, users install from `about:addons`.

---

## First Run

1. Click the toolbar icon → the **Connect to Server** screen appears
2. Enter your DotNetCloud server URL (e.g. `https://mint22:5443`)
3. Click **Connect to Server**
4. A browser tab opens for authorization — log in to your server and approve the device
5. The popup transitions to the main UI and initial sync begins automatically

Bookmarks from your DotNetCloud server appear in your browser's bookmark tree within a few minutes. Changes you make in the browser are synced back to the server.

---

## Development

```bash
# Type-check without emitting
npm run typecheck

# Run tests
npm test

# Watch mode
npm run test:watch

# Dev servers with HMR (limited extension support)
npm run dev:chrome
npm run dev:firefox
```

### Project layout

```
src/
  api/          # Typed REST client for the Bookmarks API
  auth/         # OAuth2 Device Flow (RFC 8628) + token management
  sync/         # Sync engine (initial sync, push, pull, ID mapping)
  background/   # Service worker (alarms, auth listener, sync lifecycle)
  popup/        # Popup UI (Save, Browse, Search panels)
tests/          # Jest unit tests (79 tests, 7 suites)
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| "Not authenticated" on popup | No token stored. Click "Connect" and complete the device flow. |
| Bookmarks not syncing | Check that the Bookmarks module is enabled on your server. Verify the server URL is correct. |
| "Failed to fetch" errors | Server unreachable or TLS certificate issue. Ensure the server is running and reachable. |
| Initial sync hangs | Large bookmark trees may take a moment. Check the background service worker console for errors. |
| Extension doesn't load in Firefox | Firefox must be version 109 or later (MV3 support). |

### Checking background logs

**Chrome:** `chrome://extensions` → find DotNetCloud Bookmarks → **Service Worker** link → Console  
**Firefox:** `about:debugging#/runtime/this-firefox` → find DotNetCloud Bookmarks → **Inspect** → Console
