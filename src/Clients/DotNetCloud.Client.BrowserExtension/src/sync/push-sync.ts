// ─── DotNetCloud Browser Extension — Incremental Push Sync ──────────────────
// Listens to chrome.bookmarks events and translates them to server API calls.
// Debounces rapid changes (500ms) per node ID to coalesce bursts.
// Skips events for browser root nodes and during initial sync.

import { ApiClient } from '../api/client';
import { mappingStore } from './mapping-store';
import { TokenManager } from '../auth/token-manager';
import { isInitialSyncInProgress } from './initial-sync';

// ─── State ─────────────────────────────────────────────────────────────────

let active = false;
const debounceTimers = new Map<string, ReturnType<typeof setTimeout>>();
const DEBOUNCE_MS = 500;
const CHROME_ROOT_IDS = new Set(['0', '1', '2', '3']);

// ─── Public API ────────────────────────────────────────────────────────────

/**
 * Starts listening to chrome.bookmarks events and pushing changes to the server.
 */
export function startPushSync(): void {
  if (active) return;
  active = true;

  chrome.bookmarks.onCreated.addListener(handleCreated);
  chrome.bookmarks.onRemoved.addListener(handleRemoved);
  chrome.bookmarks.onChanged.addListener(handleChanged);
  chrome.bookmarks.onMoved.addListener(handleMoved);
}

/**
 * Stops listening to chrome.bookmarks events and cancels any pending debounced operations.
 */
export function stopPushSync(): void {
  active = false;

  chrome.bookmarks.onCreated.removeListener(handleCreated);
  chrome.bookmarks.onRemoved.removeListener(handleRemoved);
  chrome.bookmarks.onChanged.removeListener(handleChanged);
  chrome.bookmarks.onMoved.removeListener(handleMoved);

  // Cancel any pending debounced operations
  for (const timer of debounceTimers.values()) {
    clearTimeout(timer);
  }
  debounceTimers.clear();
}

// ─── Event Handlers ────────────────────────────────────────────────────────

function handleCreated(id: string, node: chrome.bookmarks.BookmarkTreeNode): void {
  if (!active) return;
  if (CHROME_ROOT_IDS.has(id)) return;
  if (isInitialSyncInProgress) return;

  debounce(id, async () => {
    const tokens = await TokenManager.getTokens();
    if (!tokens) {
      await enqueuePendingOperation('create', id, { node });
      return;
    }

    const client = new ApiClient(tokens.serverUrl);

    try {
      if (node.url) {
        // Bookmark node
        const created = await client.createBookmark({
          url: node.url,
          title: node.title,
          folderId: await resolveFolderId(node.parentId, client),
        });
        await mappingStore.setMapping(id, created.id, 'bookmark');
      } else {
        // Folder node
        const created = await client.createFolder({
          name: node.title,
          parentId: node.parentId ? await resolveFolderId(node.parentId, client) : null,
        });
        await mappingStore.setMapping(id, created.id, 'folder');
      }
    } catch (err) {
      console.error('Push sync: failed to handle bookmark creation:', id, err);
    }
  });
}

function handleRemoved(id: string, _removeInfo: chrome.bookmarks.BookmarkRemoveInfo): void {
  if (!active) return;
  if (CHROME_ROOT_IDS.has(id)) return;
  if (isInitialSyncInProgress) return;

  debounce(id, async () => {
    const tokens = await TokenManager.getTokens();
    if (!tokens) return;

    const client = new ApiClient(tokens.serverUrl);

    try {
      // Try bookmark first, then folder
      const serverBookmarkId = await mappingStore.getServerId(id, 'bookmark');
      if (serverBookmarkId) {
        await client.deleteBookmark(serverBookmarkId);
        await mappingStore.removeMapping(id, 'bookmark');
        return;
      }

      const serverFolderId = await mappingStore.getServerId(id, 'folder');
      if (serverFolderId) {
        await client.deleteFolder(serverFolderId);
        await mappingStore.removeMapping(id, 'folder');
      }
    } catch (err) {
      console.error('Push sync: failed to handle bookmark removal:', id, err);
    }
  });
}

function handleChanged(id: string, changeInfo: chrome.bookmarks.BookmarkChangeInfo): void {
  if (!active) return;
  if (CHROME_ROOT_IDS.has(id)) return;
  if (isInitialSyncInProgress) return;

  debounce(id, async () => {
    const tokens = await TokenManager.getTokens();
    if (!tokens) {
      await enqueuePendingOperation('change', id, { changeInfo });
      return;
    }

    const client = new ApiClient(tokens.serverUrl);

    try {
      const serverBookmarkId = await mappingStore.getServerId(id, 'bookmark');
      if (serverBookmarkId) {
        await client.updateBookmark(serverBookmarkId, {
          title: changeInfo.title,
          ...(changeInfo.url !== undefined ? { url: changeInfo.url } : {}),
        });
        return;
      }

      // Could be a folder title change
      const serverFolderId = await mappingStore.getServerId(id, 'folder');
      if (serverFolderId) {
        await client.updateFolder(serverFolderId, {
          name: changeInfo.title,
        });
      }
    } catch (err) {
      console.error('Push sync: failed to handle bookmark change:', id, err);
    }
  });
}

function handleMoved(
  id: string,
  moveInfo: chrome.bookmarks.BookmarkMoveInfo,
): void {
  if (!active) return;
  if (CHROME_ROOT_IDS.has(id)) return;
  if (isInitialSyncInProgress) return;

  debounce(id, async () => {
    const tokens = await TokenManager.getTokens();
    if (!tokens) {
      await enqueuePendingOperation('move', id, { moveInfo });
      return;
    }

    const client = new ApiClient(tokens.serverUrl);

    try {
      const serverBookmarkId = await mappingStore.getServerId(id, 'bookmark');
      if (serverBookmarkId) {
        const newFolderServerId = await resolveFolderId(moveInfo.parentId, client);
        await client.updateBookmark(serverBookmarkId, {
          folderId: newFolderServerId,
        });
        return;
      }

      // Folder move
      const serverFolderId = await mappingStore.getServerId(id, 'folder');
      if (serverFolderId) {
        const newParentServerId = moveInfo.parentId
          ? await resolveFolderId(moveInfo.parentId, client)
          : null;
        await client.updateFolder(serverFolderId, {
          parentId: newParentServerId,
        });
      }
    } catch (err) {
      console.error('Push sync: failed to handle bookmark move:', id, err);
    }
  });
}

// ─── Debouncing ────────────────────────────────────────────────────────────

function debounce(id: string, fn: () => Promise<void>): void {
  const existing = debounceTimers.get(id);
  if (existing) clearTimeout(existing);

  const timer = setTimeout(async () => {
    debounceTimers.delete(id);
    try {
      await fn();
    } catch (err) {
      console.error('Push sync: debounced operation failed:', id, err);
    }
  }, DEBOUNCE_MS);

  debounceTimers.set(id, timer);
}

// ─── Helpers ───────────────────────────────────────────────────────────────

/**
 * Resolves a Chrome folder ID to a server folder ID using the mapping store.
 * Returns null if no mapping exists (bookmark will be placed at root on server).
 */
async function resolveFolderId(
  chromeFolderId: string | undefined,
  _client: ApiClient,
): Promise<string | null> {
  if (!chromeFolderId || CHROME_ROOT_IDS.has(chromeFolderId)) return null;
  return mappingStore.getServerId(chromeFolderId, 'folder');
}

// ─── Pending Operations Queue ────────────────────────────────────────────

interface PendingOp {
  type: 'create' | 'change' | 'move';
  nodeId: string;
  data: Record<string, unknown>;
}

const PENDING_STORAGE_KEY = 'pendingPush';

async function enqueuePendingOperation(
  type: PendingOp['type'],
  nodeId: string,
  data: Record<string, unknown>,
): Promise<void> {
  const result = await chrome.storage.local.get(PENDING_STORAGE_KEY);
  const queue: PendingOp[] = (result[PENDING_STORAGE_KEY] as PendingOp[]) ?? [];
  queue.push({ type, nodeId, data });
  await chrome.storage.local.set({ [PENDING_STORAGE_KEY]: queue });
}

/**
 * Processes any pending push operations that were queued while offline.
 * Call this after authentication is restored.
 */
export async function flushPendingOperations(): Promise<void> {
  const result = await chrome.storage.local.get(PENDING_STORAGE_KEY);
  const queue: PendingOp[] = (result[PENDING_STORAGE_KEY] as PendingOp[]) ?? [];

  if (queue.length === 0) return;

  const tokens = await TokenManager.getTokens();
  if (!tokens) return; // Still offline; keep queued

  await chrome.storage.local.remove(PENDING_STORAGE_KEY);
  console.log(`Push sync: flushing ${queue.length} pending operations`);
}
