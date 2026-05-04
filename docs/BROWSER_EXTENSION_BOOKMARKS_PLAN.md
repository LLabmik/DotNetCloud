# Browser Bookmarks Extension — Implementation Plan

**Date:** 2026-05-04  
**Status:** Phase 2 Complete — Extension Project Scaffold Done  
**Phase:** Post-Phase-6 (Bookmarks server module complete)

---

## Overview

Build a cross-browser (Chrome MV3 + Firefox MV3/MV2) browser extension that syncs bookmarks bidirectionally with the DotNetCloud server. Users authenticate via OAuth2 Device Authorization Grant, manage bookmarks from a full-featured popup UI (save tab, browse, quick search), and their bookmarks stay in sync automatically in the background.

### Goals

- Bidirectional sync: DotNetCloud server ↔ browser bookmark tree
- Chrome (Manifest V3) + Firefox (MV3, MV2 fallback) from a single TypeScript codebase
- OAuth2 Device Flow authentication (no passwords stored in extension)
- Full popup UI: save current tab, browse recent bookmarks, quick search
- Conflict resolution: server state wins
- SSRF-safe preview fetching already implemented server-side (reused)

### Non-Goals

- No Safari support (different extension API; defer)
- No bookmarks sharing/collaboration (private-only, same as Phase 6)
- No sync of browser history or open tabs
- No offline editing queue longer than one session (online required for initial sync)

---

## Architecture

```
┌────────────────────────────────────────────────────────────────┐
│  Browser Extension (TypeScript + Vite)                         │
│                                                                │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │  Popup UI   │  │ Service Worker│  │  WebExtension APIs   │  │
│  │  (popup.ts) │  │ (background) │  │  chrome.bookmarks    │  │
│  │  Save tab   │  │ Token refresh│  │  chrome.storage      │  │
│  │  Browse     │  │ Pull sync    │  │  chrome.alarms       │  │
│  │  Search     │  │ (5-min alarm)│  │  chrome.tabs         │  │
│  └──────┬──────┘  └──────┬───────┘  └──────────────────────┘  │
│         │                │                                      │
│         └────────────────┘                                      │
│                  │  API Client (src/api/client.ts)              │
└──────────────────┼─────────────────────────────────────────────┘
                   │  HTTPS  (Bearer token)
┌──────────────────▼─────────────────────────────────────────────┐
│  DotNetCloud Server                                            │
│                                                                │
│  OpenIddict: /connect/device  /connect/token                   │
│  Bookmarks API: /api/v1/bookmarks/*                            │
│   ├── CRUD (existing)                                          │
│   ├── GET /sync/changes?since=...    ← NEW (Phase 1, step 3)   │
│   └── POST /batch                    ← NEW (Phase 1, step 4)   │
└────────────────────────────────────────────────────────────────┘
```

### Sync Model

```
Browser Bookmarks Tree ◄──────────────────► DotNetCloud Server
         │                                          │
    onCreated/Removed/                         GET /sync/changes
    Changed/Moved                               (cursor-based, 5-min alarm)
         │                                          │
    push-sync.ts ─── POST /api/v1/bookmarks ──►     │
                                                    │
    pull-sync.ts ◄── GET /sync/changes?since=T ─────┘
```

**Initial sync:** Server-first. Pull full server tree → recreate in browser → upload any browser-only bookmarks to server via batch create.  
**Conflicts:** Server wins. If both sides changed the same bookmark since the last cursor, the server version is kept.

---

## Project Structure

```
src/Clients/DotNetCloud.Client.BrowserExtension/
├── package.json
├── tsconfig.json
├── vite.config.ts
├── manifest.chrome.json          ← MV3
├── manifest.firefox.json         ← MV3 (FF ≥ 109) + MV2 fallback
├── build-extension.ps1           ← Zip Chrome + Firefox dist
├── src/
│   ├── api/
│   │   ├── client.ts             ← Typed fetch wrapper
│   │   ├── types.ts              ← TypeScript DTOs matching server
│   │   └── auth.ts               ← Token storage + attach to requests
│   ├── auth/
│   │   ├── device-flow.ts        ← OAuth2 device flow initiator
│   │   └── token-manager.ts      ← Auto-refresh via chrome.alarms
│   ├── sync/
│   │   ├── mapping-store.ts      ← browserBookmarkId ↔ serverBookmarkId
│   │   ├── initial-sync.ts       ← First-connect full sync
│   │   ├── push-sync.ts          ← Browser changes → server
│   │   └── pull-sync.ts          ← Server changes → browser (5-min poll)
│   ├── background/
│   │   └── service-worker.ts     ← Chrome MV3 / Firefox background script
│   └── popup/
│       ├── popup.html
│       ├── popup.ts
│       ├── components/
│       │   ├── SavePanel.ts
│       │   ├── BrowsePanel.ts
│       │   └── SearchPanel.ts
│       └── styles/
│           └── popup.css
├── tests/
│   ├── mapping-store.test.ts
│   ├── initial-sync.test.ts
│   ├── push-sync.test.ts
│   └── conflict-resolution.test.ts
└── dist/
    ├── chrome/                   ← Built Chrome extension (gitignored)
    └── firefox/                  ← Built Firefox extension (gitignored)
```

---

## Phase 1: Server-Side Extension Support

**STATUS:** ✅ COMPLETED (verified 2026-05-04)

> All server changes passed `dotnet build DotNetCloud.CI.slnf` and `dotnet test DotNetCloud.CI.slnf`.

**Deliverables:**
- ✓ Device Authorization Grant enabled (`AllowDeviceCodeFlow()` in `AuthServiceExtensions.cs`)
- ✓ `bookmarks:read` and `bookmarks:write` scopes registered
- ✓ Browser extension OIDC client registered (`dotnetcloud-browser-extension` in `OidcClientSeeder.cs`)
- ✓ Delta sync endpoint: `GET /api/v1/bookmarks/sync/changes?since=...` with `BookmarkSyncChangesResult`
- ✓ Batch operations endpoint: `POST /api/v1/bookmarks/batch` with `BatchRequest`/`BatchResponse`
- ✓ `IBookmarkService.GetSyncChangesAsync()` and `IBookmarkService.BatchAsync()` implemented

### Step 1.1 — Enable Device Authorization Grant

**Status:** completed ✅

**File:** `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs`

Add `AllowDeviceCodeFlow()` to the OpenIddict server builder alongside the existing `AllowAuthorizationCodeFlow()`. Add the device endpoint URI.

```csharp
// In AddDotNetCloudAuth(), inside the .AddServer(...) block:
options.AllowDeviceCodeFlow();
options.SetDeviceEndpointUris("/connect/device");
```

**File:** `src/Core/DotNetCloud.Core.Server/Extensions/OpenIddictEndpointsExtensions.cs`

Register the device endpoint handler:

```csharp
// In MapOpenIddictEndpoints() or equivalent:
endpoints.MapGet("/connect/device", ...).RequireAuthorization(); // built-in OpenIddict handler
```

OpenIddict provides built-in endpoint handlers for the device flow — enable `HandleDeviceEndpoint()` and `HandleDeviceVerificationEndpoint()` in the server options.

**Also add scopes** `bookmarks:read` and `bookmarks:write` to the allowed scopes list in `AuthServiceExtensions.cs`:

```csharp
options.RegisterScopes(
    Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess,
    "files:read", "files:write",
    "bookmarks:read", "bookmarks:write"   // ← ADD
);
```

---

### Step 1.2 — Register Extension OIDC Client

**Status:** completed ✅

**File:** `src/Core/DotNetCloud.Core.Server/Initialization/OidcClientSeeder.cs`

Add a new client registration alongside `dotnetcloud-desktop` and `dotnetcloud-mobile`:

```csharp
// dotnetcloud-browser-extension
await manager.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId = "dotnetcloud-browser-extension",
    ClientType = ClientTypes.Public,
    DisplayName = "DotNetCloud Browser Extension",
    ConsentType = ConsentTypes.Explicit,
    Permissions =
    {
        Permissions.Endpoints.Token,
        Permissions.Endpoints.Device,
        Permissions.GrantTypes.DeviceCode,
        Permissions.GrantTypes.RefreshToken,
        Permissions.Prefixes.Scope + "openid",
        Permissions.Prefixes.Scope + "profile",
        Permissions.Prefixes.Scope + "email",
        Permissions.Prefixes.Scope + "offline_access",
        Permissions.Prefixes.Scope + "bookmarks:read",
        Permissions.Prefixes.Scope + "bookmarks:write",
    }
}, cancellationToken);
```

---

### Step 1.3 — Delta Sync Endpoint

**Status:** completed ✅

**File:** `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Controllers/BookmarksController.cs`

Add a new endpoint for incremental sync. Uses the existing soft-delete `DeletedAt` field to find removed bookmarks.

```
GET /api/v1/bookmarks/sync/changes?since=2026-04-01T00:00:00Z&limit=100
```

**Request query parameters:**
- `since` (ISO-8601 UTC) — return only items changed after this timestamp
- `limit` (int, default 100, max 500)

**Response:**
```json
{
  "items": [
    {
      "id": "...",
      "folderId": "...",
      "url": "...",
      "title": "...",
      "description": "...",
      "tags": [],
      "notes": "...",
      "updatedAt": "2026-04-15T10:00:00Z"
    }
  ],
  "deletedIds": ["uuid1", "uuid2"],
  "folders": [
    {
      "id": "...",
      "parentId": "...",
      "name": "...",
      "updatedAt": "..."
    }
  ],
  "deletedFolderIds": ["uuid3"],
  "nextCursor": "2026-05-04T12:00:00Z",
  "hasMore": false
}
```

**Implementation notes:**
- Query `BookmarkItems` where `UpdatedAt > since || (DeletedAt != null && DeletedAt > since)`
- Soft-deleted items appear only in `deletedIds`, not in `items`
- `nextCursor` = `DateTimeOffset.UtcNow` at query time (not the last item's timestamp, to avoid missed updates)
- Add `[Authorize(Policy = "bookmarks:read")]` attribute
- Add `ETag` header based on `nextCursor` value for HTTP caching; respond `304 Not Modified` if client sends matching `If-None-Match`

**New service interface method** to add to `IBookmarkService`:
```csharp
Task<BookmarkSyncChangesResult> GetSyncChangesAsync(
    DateTimeOffset since, int limit, CallerContext caller, CancellationToken ct = default);
```

---

### Step 1.4 — Batch Operations Endpoint

**Status:** completed ✅

**File:** `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Controllers/BookmarksController.cs`

Add a batch endpoint for efficient initial sync and bulk operations.

```
POST /api/v1/bookmarks/batch
```

**Request body:**
```json
{
  "creates": [
    { "url": "...", "title": "...", "folderId": "...", "tags": [], "notes": "..." }
  ],
  "updates": [
    { "id": "...", "url": "...", "title": "...", "folderId": "...", "tags": [], "notes": "..." }
  ],
  "deletes": ["uuid1", "uuid2"],
  "folderCreates": [
    { "name": "...", "parentId": "..." }
  ],
  "folderDeletes": ["uuid3"]
}
```

**Response:**
```json
{
  "results": [
    { "operation": "create", "clientRef": "...", "serverId": "...", "success": true, "error": null },
    { "operation": "delete", "id": "uuid1", "success": false, "error": "not_found" }
  ]
}
```

**Implementation notes:**
- `clientRef` is an opaque string the extension sends on creates (its `chrome.bookmarks` node ID); returned in results so extension can build the ID mapping
- Process operations in order: folderCreates → creates → updates → deletes → folderDeletes
- Each operation is independent; partial failure is allowed (per-item error in results)
- Limit: 500 total operations per request; return `400` if exceeded
- Add `[Authorize(Policy = "bookmarks:write")]` attribute
- Wrap in a single database transaction where possible; roll back entire batch only on infrastructure error, not on per-item business rule failures

---

## Phase 2: Extension Project Scaffold

**STATUS:** ✅ COMPLETED (2026-05-04)

**Deliverables:**
- ✓ Project structure: `package.json`, `tsconfig.json`, `jest.config.js`, `.gitignore`
- ✓ Dual manifests: `manifest.chrome.json` (MV3), `manifest.firefox.json` (MV3)
- ✓ API types layer: `src/api/types.ts` — all DTOs matching server (BookmarkItem, BookmarkFolder, SyncChangesResponse, BatchRequest, etc.)
- ✓ API client: `src/api/client.ts` — typed fetch wrapper with all CRUD + sync + batch methods, AbortSignal support, ApiError on non-2xx
- ✓ Auth attachment: `src/api/auth.ts` — `getAuthHeaders()` + `isAuthenticated()`
- ✓ Vite config: `vite.config.ts` — dual-browser output via `--mode chrome`/`--mode firefox`, manifest + icon copy plugins
- ✓ Build scripts: `build-extension.ps1` (PowerShell/Windows), `build-extension.sh` (Bash/Linux)
- ✓ Background service worker: `src/background/service-worker.ts` — alarm handler, install/update hooks
- ✓ Auth modules: `src/auth/device-flow.ts` (RFC 8628 device flow initiator + poller), `src/auth/token-manager.ts` (storage, refresh, alarm scheduling)
- ✓ Sync engine: `src/sync/mapping-store.ts` — bidirectional bookmark↔server ID maps via `chrome.storage.local`
- ✓ Popup scaffold: `popup.html`, `popup.ts` (auth screen + main UI router), `styles/popup.css` (full design system)
- ✓ Placeholder icons: 16×16, 48×48, 128×128 PNG

**Notes:** Phase 2 complete. All scaffold files created. Extension can be built with `npm install && npm run build` (both Chrome and Firefox). The popup shows an auth screen that initiates OAuth2 Device Flow, opens the verification tab, and polls for the token. On success, transitions to a placeholder main UI. Full popup panels (Save/Browse/Search) deferred to Phase 5.

---

### Step 2.1 — Project Initialization

**Status:** completed ✅

Create `src/Clients/DotNetCloud.Client.BrowserExtension/`:

**`package.json`:**
```json
{
  "name": "dotnetcloud-browser-extension",
  "version": "1.0.0",
  "private": true,
  "scripts": {
    "dev:chrome": "vite --mode chrome",
    "dev:firefox": "vite --mode firefox",
    "build:chrome": "vite build --mode chrome",
    "build:firefox": "vite build --mode firefox",
    "build": "npm run build:chrome && npm run build:firefox",
    "test": "jest",
    "test:watch": "jest --watch",
    "typecheck": "tsc --noEmit"
  },
  "devDependencies": {
    "@types/chrome": "^0.0.268",
    "@types/firefox-webext-browser": "^120.0.4",
    "@types/jest": "^29.5.12",
    "jest": "^29.7.0",
    "ts-jest": "^29.1.4",
    "typescript": "^5.4.5",
    "vite": "^5.2.11"
  },
  "dependencies": {
    "webextension-polyfill": "^0.10.0"
  }
}
```

**`tsconfig.json`:**
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "lib": ["ES2022", "DOM"],
    "outDir": "dist",
    "baseUrl": ".",
    "paths": {
      "@/*": ["src/*"]
    }
  },
  "include": ["src/**/*"],
  "exclude": ["node_modules", "dist", "tests"]
}
```

---

### Step 2.2 — Dual Manifests

**Status:** completed ✅

**`manifest.chrome.json`** (MV3):
```json
{
  "manifest_version": 3,
  "name": "DotNetCloud Bookmarks",
  "version": "1.0.0",
  "description": "Sync your browser bookmarks with your DotNetCloud server.",
  "permissions": ["bookmarks", "storage", "alarms", "tabs", "notifications"],
  "host_permissions": [],
  "background": {
    "service_worker": "background/service-worker.js",
    "type": "module"
  },
  "action": {
    "default_popup": "popup/popup.html",
    "default_icon": {
      "16": "icons/icon-16.png",
      "48": "icons/icon-48.png",
      "128": "icons/icon-128.png"
    }
  },
  "icons": {
    "16": "icons/icon-16.png",
    "48": "icons/icon-48.png",
    "128": "icons/icon-128.png"
  },
  "content_security_policy": {
    "extension_pages": "default-src 'self'; img-src 'self' data: https:; connect-src 'self' https:"
  }
}
```

**`manifest.firefox.json`** (MV3, Firefox ≥ 109):
```json
{
  "manifest_version": 3,
  "name": "DotNetCloud Bookmarks",
  "version": "1.0.0",
  "description": "Sync your browser bookmarks with your DotNetCloud server.",
  "permissions": ["bookmarks", "storage", "alarms", "tabs", "notifications"],
  "background": {
    "scripts": ["background/service-worker.js"],
    "type": "module"
  },
  "action": {
    "default_popup": "popup/popup.html"
  },
  "browser_specific_settings": {
    "gecko": {
      "id": "bookmarks@dotnetcloud.net",
      "strict_min_version": "109.0"
    }
  }
}
```

`vite.config.ts` reads `process.env.BROWSER` (`chrome` | `firefox`) and copies the corresponding manifest as `manifest.json` in the output.

---

### Step 2.3 — API Client Layer

**Status:** completed ✅

**`src/api/types.ts`** — TypeScript interfaces matching server DTOs:

```typescript
export interface BookmarkItem {
  id: string;
  folderId: string | null;
  url: string;
  title: string;
  description?: string;
  tags: string[];
  notes?: string;
  createdAt: string;   // ISO-8601
  updatedAt: string;
}

export interface BookmarkFolder {
  id: string;
  parentId: string | null;
  name: string;
  updatedAt: string;
}

export interface SyncChangesResponse {
  items: BookmarkItem[];
  deletedIds: string[];
  folders: BookmarkFolder[];
  deletedFolderIds: string[];
  nextCursor: string;
  hasMore: boolean;
}

export interface BatchRequest {
  creates?: CreateBookmarkRequest[];
  updates?: UpdateBookmarkRequest[];
  deletes?: string[];
  folderCreates?: CreateFolderRequest[];
  folderDeletes?: string[];
}

export interface BatchResult {
  operation: 'create' | 'update' | 'delete' | 'folderCreate' | 'folderDelete';
  clientRef?: string;
  serverId?: string;
  id?: string;
  success: boolean;
  error?: string;
}

export interface BatchResponse {
  results: BatchResult[];
}
```

**`src/api/client.ts`** — Typed fetch wrapper. All methods accept an optional `signal: AbortSignal`. Throws `ApiError` (with `status` and `code`) on non-2xx responses.

Key methods:
```typescript
getBookmarks(params: { folderId?: string; skip?: number; take?: number }): Promise<BookmarkItem[]>
getBookmark(id: string): Promise<BookmarkItem>
createBookmark(req: CreateBookmarkRequest): Promise<BookmarkItem>
updateBookmark(id: string, req: UpdateBookmarkRequest): Promise<BookmarkItem>
deleteBookmark(id: string): Promise<void>
searchBookmarks(q: string, skip?: number, take?: number): Promise<BookmarkItem[]>
getFolders(parentId?: string): Promise<BookmarkFolder[]>
createFolder(req: CreateFolderRequest): Promise<BookmarkFolder>
updateFolder(id: string, req: UpdateFolderRequest): Promise<BookmarkFolder>
deleteFolder(id: string): Promise<void>
getSyncChanges(since: string, limit?: number): Promise<SyncChangesResponse>
batch(req: BatchRequest): Promise<BatchResponse>
triggerPreview(bookmarkId: string): Promise<void>
```

**`src/api/auth.ts`** — Reads token from `chrome.storage.local`, attaches `Authorization: Bearer <token>` header. Calls `TokenManager.getAccessToken()` before each request (which handles refresh if needed).

---

## Phase 3: Authentication

### Step 3.1 — Device Flow Initiator

**`src/auth/device-flow.ts`**

```typescript
export interface DeviceFlowState {
  deviceCode: string;
  userCode: string;
  verificationUri: string;
  expiresIn: number;
  interval: number;
}

export async function initiateDeviceFlow(serverUrl: string): Promise<DeviceFlowState>
export async function pollForToken(serverUrl: string, state: DeviceFlowState): Promise<TokenSet>
```

**Flow:**
1. `POST {serverUrl}/connect/device` with `client_id=dotnetcloud-browser-extension&scope=openid profile email offline_access bookmarks:read bookmarks:write`
2. Receive `device_code`, `user_code`, `verification_uri`, `expires_in`, `interval`
3. Open `verification_uri?user_code={userCode}` in new tab via `chrome.tabs.create`
4. Begin polling `{serverUrl}/connect/token` every `interval` seconds:
   - Body: `grant_type=urn:ietf:params:oauth:grant-type:device_code&device_code={device_code}&client_id=dotnetcloud-browser-extension`
   - Handle: `authorization_pending` → keep polling; `slow_down` → increase interval by 5s; `access_denied` / `expired_token` → throw; success → return `TokenSet`
5. On success: store `TokenSet` in `chrome.storage.local` under key `auth`

**`TokenSet` shape:**
```typescript
interface TokenSet {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;   // Unix ms timestamp
  serverUrl: string;
}
```

---

### Step 3.2 — Token Manager

**`src/auth/token-manager.ts`**

```typescript
export async function getAccessToken(): Promise<string | null>
export async function refreshToken(): Promise<void>
export async function clearTokens(): Promise<void>
export function scheduleRefresh(): void    // called on extension startup
```

- `getAccessToken()`: reads from storage; if `expiresAt - Date.now() < 60_000`, refreshes first; returns `accessToken` or `null` if not logged in
- `refreshToken()`: POST to `/connect/token` with `grant_type=refresh_token`; on `invalid_grant`/`revoked` → `clearTokens()` → dispatch `auth:required` event to popup
- `scheduleRefresh()`: registers a `chrome.alarms` alarm named `token-refresh` to fire 60s before `expiresAt`; alarm handler calls `refreshToken()`
- Background service worker listens for `chrome.alarms.onAlarm` and routes `token-refresh` to `refreshToken()`

---

## Phase 4: Sync Engine

### Step 4.1 — ID Mapping Store

**`src/sync/mapping-store.ts`**

Persists two maps in `chrome.storage.local`:
- `idMap.bookmarks`: `{ [browserNodeId: string]: string }` → server UUID
- `idMap.folders`: `{ [browserNodeId: string]: string }` → server UUID
- `idMap.reverseBookmarks`: `{ [serverUUID: string]: string }` → browser node ID
- `idMap.reverseFolders`: `{ [serverUUID: string]: string }` → browser node ID
- `idMap.syncCursor`: ISO-8601 string of last successful pull

```typescript
export const mappingStore = {
  async getServerId(browserNodeId: string, type: 'bookmark' | 'folder'): Promise<string | null>
  async getBrowserNodeId(serverId: string, type: 'bookmark' | 'folder'): Promise<string | null>
  async setMapping(browserNodeId: string, serverId: string, type: 'bookmark' | 'folder'): Promise<void>
  async removeMapping(browserNodeId: string, type: 'bookmark' | 'folder'): Promise<void>
  async getCursor(): Promise<string | null>
  async setCursor(cursor: string): Promise<void>
  async clearAll(): Promise<void>
}
```

---

### Step 4.2 — Initial Sync

**`src/sync/initial-sync.ts`**

Called once when the extension first connects to a server (no cursor in storage). Also callable from the popup "Re-sync" action.

**Algorithm:**
```
1. Fetch full server bookmark tree (paginated GET /api/v1/bookmarks/folders + GET /api/v1/bookmarks)
2. Get current browser bookmark tree via chrome.bookmarks.getTree()
3. Build server folder tree (sorted: parents before children)
4. For each server folder (root → leaves):
   a. Find or create matching browser folder (match by name + parent path)
   b. Record mapping
5. For each server bookmark:
   a. Find or create browser bookmark node
   b. Record mapping
6. For each browser bookmark NOT in mapping (browser-only):
   a. Add to batch.creates list (including clientRef = browser node ID)
7. POST /api/v1/bookmarks/batch with all creates
8. Map returned serverIds back to browser node IDs via clientRef
9. Set cursor = current UTC ISO-8601
```

**Root node handling:** Browser trees have fixed roots (`"1"` = Bookmarks Bar, `"2"` = Other Bookmarks, `"3"` = Mobile Bookmarks in Chrome). Map the DotNetCloud top-level folders to these roots where names match; otherwise place under Bookmarks Bar.

---

### Step 4.3 — Incremental Push (Browser → Server)

**`src/sync/push-sync.ts`**

Listens to `chrome.bookmarks` events and translates them to API calls.

```typescript
export function startPushSync(): void   // registers all listeners
export function stopPushSync(): void    // removes all listeners
```

**Event handlers (all debounced 500ms per node ID to coalesce rapid changes):**

| Chrome Event | API Call |
|---|---|
| `onCreated(id, node)` | `POST /api/v1/bookmarks` or `POST /api/v1/bookmarks/folders` — store mapping on response |
| `onRemoved(id, removeInfo)` | `DELETE /api/v1/bookmarks/{serverId}` or folder delete — remove from mapping |
| `onChanged(id, changeInfo)` | `PUT /api/v1/bookmarks/{serverId}` — update title/url |
| `onMoved(id, moveInfo)` | `PUT /api/v1/bookmarks/{serverId}` with new `folderId` |

**Guards:**
- Skip events for browser root nodes (IDs `"0"`, `"1"`, `"2"`, `"3"`)
- Skip events fired while initial sync is in progress (`isSyncing` flag)
- If `getAccessToken()` returns null → enqueue the operation in `chrome.storage.local` under `pendingPush`; retry on next successful auth

---

### Step 4.4 — Incremental Pull (Server → Browser)

**`src/sync/pull-sync.ts`**

```typescript
export function startPullSync(): void   // registers chrome.alarms alarm
export function stopPullSync(): void    // clears alarm
export async function runPullCycle(): Promise<void>   // single pull+apply cycle
```

**`chrome.alarms` alarm:** `bookmark-pull`, period = 5 minutes.

**Pull cycle algorithm:**
```
1. cursor = await mappingStore.getCursor()
2. If no cursor → skip (initial sync not done yet)
3. response = GET /api/v1/bookmarks/sync/changes?since={cursor}&limit=100
4. For each folder in response.folders:
   a. If mapping exists → chrome.bookmarks.update(browserNodeId, { title: folder.name })
   b. If no mapping → chrome.bookmarks.create({ parentId: ..., title: ... }) + store mapping
5. For each item in response.items:
   a. If mapping exists → chrome.bookmarks.update(browserNodeId, { title, url })
      If folderId changed → chrome.bookmarks.move(browserNodeId, { parentId: newBrowserFolderId })
   b. If no mapping → chrome.bookmarks.create({ parentId: ..., title, url }) + store mapping
6. For each id in response.deletedIds:
   a. browserNodeId = await mappingStore.getBrowserNodeId(id, 'bookmark')
   b. If found → chrome.bookmarks.remove(browserNodeId) + removeMapping
7. For each id in response.deletedFolderIds (same, for folders)
8. Update cursor = response.nextCursor
9. If response.hasMore → immediately run another cycle (without waiting for alarm)
```

**Conflict rule:** During pull, always apply server state regardless of local browser state. Since push-sync sends changes immediately (debounced 500ms), by the time the pull runs (5 min), any conflicting local change has already been pushed to the server, and the server's version is the authoritative state.

---

## Phase 5: Popup UI

The popup is 380px wide × 520px tall (fixed), built with plain TypeScript + CSS (no framework dependency). Follows DotNetCloud design language: clean sans-serif, minimal chrome, consistent with the web UI color scheme.

### Step 5.1 — Scaffold

**`popup/popup.html`:**
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=380" />
  <link rel="stylesheet" href="styles/popup.css" />
  <title>DotNetCloud Bookmarks</title>
</head>
<body>
  <div id="app">
    <!-- auth-screen or main-ui injected by popup.ts -->
  </div>
  <script type="module" src="popup.js"></script>
</body>
</html>
```

**`popup/popup.ts`** — Entry point. Checks `TokenManager.getAccessToken()`:
- If null → renders auth screen (server URL input + "Connect" button)
- If present → renders main popup with three panels

### Step 5.2 — Auth Screen

Shown when not logged in or token revoked.

```
┌─────────────────────────────┐
│  [DotNetCloud logo]         │
│                             │
│  Server URL                 │
│  ┌─────────────────────┐   │
│  │ https://my.server   │   │
│  └─────────────────────┘   │
│                             │
│  [ Connect to Server ]      │
│                             │
│  ─────────────────────────  │
│  Opens a browser tab to     │
│  complete sign in.          │
└─────────────────────────────┘
```

On "Connect": save server URL to `chrome.storage.local`, call `initiateDeviceFlow()`, open verification tab. Show "Waiting for authorization..." state with the user code displayed prominently. On success: transition to main UI.

### Step 5.3 — Main Popup Structure

```
┌─────────────────────────────┐
│ [logo] My Server  [avatar]  │  ← header (48px)
├─────────────────────────────┤
│ [Save] [Browse] [Search]    │  ← tab nav (40px)
├─────────────────────────────┤
│                             │
│   {active panel content}    │  ← scrollable panel (380px)
│                             │
├─────────────────────────────┤
│ ● Synced 3 min ago          │  ← status footer (32px)
└─────────────────────────────┘
```

Total: ~500px visible content area.

### Step 5.4 — Save Panel

**`popup/components/SavePanel.ts`**

Displayed by default when popup opens on a bookmarkable page.

```
URL:    [https://example.com/article  ]  (auto-filled, editable)
Title:  [Example Article Title        ]  (auto-filled, editable)
Folder: [ Bookmarks Bar ▼             ]  (lazy-loaded folder tree)
Tags:   [work, reading                ]  (comma-separated chips)
        [▶ Add notes                  ]  (collapsed by default)

         [ Save Bookmark ]
```

- URL + title auto-populated from `chrome.tabs.query({ active: true, currentWindow: true })`
- Folder picker: `<select>` rendered as indented flat list from `GET /api/v1/bookmarks/folders`; remembers last-used folder in `chrome.storage.local`
- Tags: free-text; split on comma + Enter; rendered as removable chips
- Notes: `<textarea>` revealed by expanding "Add notes" toggle
- "Save Bookmark" → `POST /api/v1/bookmarks` → show ✓ success toast → auto-trigger preview fetch (fire-and-forget) → close popup after 800ms
- If URL already bookmarked (match in local mapping): button changes to "Update Bookmark"; form pre-filled with existing data

### Step 5.5 — Browse Panel

**`popup/components/BrowsePanel.ts`**

```
[← Back]  Bookmarks Bar / Work             ← breadcrumb

📁 Projects                  →
📁 Articles                  →
─────────────────────────────
🔖 GitHub — DotNetCloud      [favicon]
   github.com/...
🔖 MDN Web Docs              [favicon]
   developer.mozilla.org
```

- Root view shows top-level folders + recent 20 bookmarks
- Click folder → navigate into it (breadcrumb updates)
- Click bookmark → `chrome.tabs.create({ url })` + close popup
- Right-click (or long-press on touch) bookmark → context menu: "Delete", "Open in New Tab", "Copy URL"
- Pull-to-refresh icon (↻) in header right → re-fetch current folder contents
- Favicons from `https://www.google.com/s2/favicons?domain={domain}&sz=16` (with `img` onerror fallback to generic icon)
- Infinite scroll: load next 20 on scroll to bottom

### Step 5.6 — Search Panel

**`popup/components/SearchPanel.ts`**

```
🔍 [Search bookmarks...                  ]

DotNetCloud — GitHub          [favicon]
   github.com/LLabmik/DotNetCloud
   in: Projects / DotNetCloud

MDN: Array.prototype.map()    [favicon]
   developer.mozilla.org/...
   in: References

────────────
  No more results
```

- Debounced 300ms → `GET /api/v1/bookmarks/search?q={query}&take=20`
- Each result: favicon + title + URL domain (line 2) + folder path (line 3, greyed)
- Click → `chrome.tabs.create({ url })` + close popup
- Empty query → show "recently added" (GET bookmarks, sorted by createdAt desc, take 10)
- "No results" state with hint: "Try a shorter search or check your DotNetCloud server."

### Step 5.7 — Sync Status Footer

Reads `idMap.syncCursor` and `syncStatus` key from `chrome.storage.local`.

| State | Display |
|---|---|
| Synced < 1 min ago | ● Synced just now (green dot) |
| Synced 1–60 min ago | ● Synced {N} min ago (green dot) |
| Synced > 60 min ago | ● Synced {N} hours ago (amber dot) |
| Actively syncing | ↻ Syncing... (spinning icon) |
| Offline / API error | ⚠ Offline — will sync when connected (grey) |
| Not logged in | ⚠ Login required [Connect] (orange) |

Footer is 32px tall, fixed to bottom of popup. Clicking the status row → opens a small overlay with last sync time, item count, and a "Sync now" button.

---

## Phase 6: Build, Tests & Docs

### Step 6.1 — Build Pipeline

**`vite.config.ts`:**

```typescript
import { defineConfig } from 'vite';
import { resolve } from 'path';

export default defineConfig(({ mode }) => ({
  build: {
    outDir: `dist/${mode}`,
    rollupOptions: {
      input: {
        popup: resolve(__dirname, 'popup/popup.html'),
        background: resolve(__dirname, 'src/background/service-worker.ts'),
      },
      output: {
        entryFileNames: '[name]/[name].js',
        chunkFileNames: 'chunks/[name].js',
        assetFileNames: 'assets/[name].[ext]',
      },
    },
  },
  plugins: [
    // copy manifest.{mode}.json → dist/{mode}/manifest.json
    // copy icons/ → dist/{mode}/icons/
  ],
}));
```

**`build-extension.ps1`:**
```powershell
npm run build
Compress-Archive -Path dist\chrome\* -DestinationPath dist\dotnetcloud-bookmarks-chrome.zip -Force
Compress-Archive -Path dist\firefox\* -DestinationPath dist\dotnetcloud-bookmarks-firefox.zip -Force
Write-Host "Built: dist\dotnetcloud-bookmarks-chrome.zip"
Write-Host "Built: dist\dotnetcloud-bookmarks-firefox.zip"
```

---

### Step 6.2 — Unit Tests

**`tests/mapping-store.test.ts`**
- `setMapping` / `getServerId` / `getBrowserNodeId` round-trip
- `removeMapping` clears both forward and reverse maps
- `clearAll` leaves cursor but clears maps (or clears everything — test both behaviors)

**`tests/initial-sync.test.ts`**
- Folder tree reconstruction: given server folders [A, B(child of A)], browser result has correct parent chain
- Browser-only bookmarks end up in `batch.creates` with correct `clientRef`
- Batch response maps `clientRef → serverId` into mapping store

**`tests/push-sync.test.ts`**
- `onCreated` event → API `createBookmark` called with correct URL/title/folderId
- `onRemoved` event → API `deleteBookmark` called with correct server ID
- `onChanged` for root node → ignored (no API call)
- Rapid create + update within 500ms → coalesced to single `createBookmark` call (not create + update)

**`tests/conflict-resolution.test.ts`**
- Pull cycle applies server item even when browser has a different title for the same mapping
- `deletedIds` in pull → `chrome.bookmarks.remove` called; mapping removed
- Missing `browserNodeId` in mapping for a deletedId → no crash, logged warning only

Use `jest-chrome` or a manual `chrome` mock object for WebExtension API simulation.

---

### Step 6.3 — Documentation Updates

After implementation is complete, update both tracking documents using targeted edits:

- **`docs/IMPLEMENTATION_CHECKLIST.md`**: Add a new "Phase: Browser Extension" section with tasks for each step above, marked `☐` initially, updated to `✓` as implemented.
- **`docs/MASTER_PROJECT_PLAN.md`**: Add a new Phase row in the Quick Status Summary table; add step entries for each phase 1–6.

---

## Verification Checklist

After full implementation:

- [ ] `dotnet build DotNetCloud.CI.slnf` passes (no new server errors)
- [ ] `dotnet test DotNetCloud.CI.slnf` passes (all existing bookmark tests green; new endpoint tests green)
- [ ] `npm test` in `src/Clients/DotNetCloud.Client.BrowserExtension/` — all Jest tests pass
- [ ] `npm run typecheck` — zero TypeScript errors
- [ ] Load `dist/chrome/` as unpacked extension in `chrome://extensions` — no errors in background service worker console
- [ ] Popup renders all three tabs without JS errors
- [ ] Device flow: click "Connect" → new tab opens DotNetCloud `/connect/device` → approve → popup transitions to logged-in state with user name shown
- [ ] Save current tab → open DotNetCloud web UI → bookmark appears immediately
- [ ] Create a bookmark in DotNetCloud web UI → click "Sync now" in popup footer → bookmark appears in Chrome Bookmarks Bar
- [ ] Delete a bookmark in Chrome → verify it is soft-deleted on server (check via web UI or `GET /api/v1/bookmarks`)
- [ ] Move a bookmark to a different folder in Chrome → server record reflects new `folderId`
- [ ] Load `dist/firefox/` as temporary extension in `about:debugging` — same smoke tests pass
- [ ] `build-extension.ps1` produces `dotnetcloud-bookmarks-chrome.zip` and `dotnetcloud-bookmarks-firefox.zip`

---

## Decisions Log

| Decision | Rationale |
|---|---|
| Server-first initial sync | Server is the authoritative store; avoids polluting server with browser's accumulated bookmark noise |
| Server wins on conflict | Simpler to reason about; avoids merge complexity; users who edit server via web UI expect their changes to stick |
| Device flow authentication | No password stored in extension; works with existing OpenIddict; no custom redirect URI needed |
| 5-minute pull interval | Balances freshness with server load; configurable via `chrome.storage` setting in future |
| `ETag` on delta endpoint | Reduces server load when many extension instances poll with no changes |
| No offline edit queue | Simplifies conflict handling; extension requires connectivity; acceptable for a bookmark manager |
| Firefox MV3 primary target | Firefox 109+ (released Jan 2023); MV3 is the future; MV2 fallback manifest available but not tested as primary |
| `webextension-polyfill` | Provides consistent Promise-based API across Chrome + Firefox without conditional branches throughout codebase |

---

## Dependencies

### New npm packages

| Package | Purpose |
|---|---|
| `webextension-polyfill` | Chrome/Firefox API unification |
| `@types/chrome` | Chrome extension type definitions |
| `@types/firefox-webext-browser` | Firefox extension type definitions |
| `vite` | Build tool (multi-entry, multi-mode) |
| `typescript` | Language |
| `jest` + `ts-jest` | Unit testing |
| `@types/jest` | Jest type definitions |

### New server NuGet packages

None — OpenIddict device flow is included in the existing `OpenIddict.AspNetCore` package. Only configuration changes are required.

### New server scopes

`bookmarks:read` and `bookmarks:write` added to OpenIddict scope registry.
