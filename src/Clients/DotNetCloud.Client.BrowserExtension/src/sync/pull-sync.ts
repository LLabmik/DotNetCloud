// ─── DotNetCloud Browser Extension — Incremental Pull Sync ──────────────────
// Fetches server-side bookmark changes (5-min interval) and applies them to
// the browser bookmark tree. Server state always wins on conflict.
// Supports multiple pull cycles when hasMore is true (no delay between pages).

import { ApiClient } from '../api/client';
import { type BookmarkItem, type BookmarkFolder } from '../api/types';
import { mappingStore } from './mapping-store';
import { TokenManager } from '../auth/token-manager';

// ─── Constants ─────────────────────────────────────────────────────────────

const PULL_ALARM_NAME = 'bookmark-pull';
const PULL_INTERVAL_MINUTES = 5;
const BATCH_LIMIT = 100;
const CHROME_ROOT_IDS = new Set(['0', '1', '2', '3']);

// ─── Public API ────────────────────────────────────────────────────────────

/**
 * Registers the periodic pull alarm.
 * Should be called on extension startup (after successful auth).
 */
export function startPullSync(): void {
  chrome.alarms.create(PULL_ALARM_NAME, {
    periodInMinutes: PULL_INTERVAL_MINUTES,
  });
}

/**
 * Clears the periodic pull alarm.
 */
export function stopPullSync(): void {
  chrome.alarms.clear(PULL_ALARM_NAME).catch(() => {
    // Ignore errors if alarm doesn't exist
  });
}

/**
 * Runs a single pull cycle: fetches changes from the server and applies them
 * to the browser bookmark tree. If hasMore is true, immediately runs another
 * cycle (for pagination).
 */
export async function runPullCycle(): Promise<void> {
  const tokens = await TokenManager.getTokens();
  if (!tokens) return;

  const cursor = await mappingStore.getCursor();
  if (!cursor) {
    // No cursor means initial sync hasn't run yet; skip
    console.log('Pull sync: skipping (no cursor — initial sync not complete)');
    return;
  }

  const client = new ApiClient(tokens.serverUrl);

  try {
    await executePullCycle(client, cursor);
  } catch (err) {
    console.error('Pull sync: cycle failed:', err);
  }
}

// ─── Pull Cycle Implementation ────────────────────────────────────────────

async function executePullCycle(client: ApiClient, since: string): Promise<void> {
  const response = await client.getSyncChanges(since, BATCH_LIMIT);

  // 1. Apply folder changes (create/update)
  for (const folder of response.folders) {
    await applyFolderChange(folder);
  }

  // 2. Apply bookmark changes (create/update/move)
  for (const item of response.items) {
    await applyBookmarkChange(item);
  }

  // 3. Remove deleted bookmarks
  for (const deletedId of response.deletedIds) {
    await applyDeletion(deletedId, 'bookmark');
  }

  // 4. Remove deleted folders
  for (const deletedFolderId of response.deletedFolderIds) {
    await applyDeletion(deletedFolderId, 'folder');
  }

  // 5. Update cursor
  await mappingStore.setCursor(response.nextCursor);

  // 6. If there are more pages, immediately run another cycle
  if (response.hasMore) {
    await executePullCycle(client, response.nextCursor);
  }
}

// ─── Folder Application ────────────────────────────────────────────────────

async function applyFolderChange(folder: BookmarkFolder): Promise<void> {
  const browserNodeId = await mappingStore.getBrowserNodeId(folder.id, 'folder');

  if (browserNodeId) {
    // Update existing folder title
    try {
      await chrome.bookmarks.update(browserNodeId, { title: folder.name });
    } catch (err) {
      console.error(`Pull sync: failed to update folder ${folder.id}:`, err);
    }
  } else {
    // Create new folder in browser
    try {
      const parentBrowserId = folder.parentId
        ? await mappingStore.getBrowserNodeId(folder.parentId, 'folder')
        : null;

      const created = await chrome.bookmarks.create({
        parentId: parentBrowserId ?? '1', // Default to Bookmarks Bar
        title: folder.name,
      });

      await mappingStore.setMapping(created.id, folder.id, 'folder');
    } catch (err) {
      console.error(`Pull sync: failed to create folder ${folder.name}:`, err);
    }
  }
}

// ─── Bookmark Application ──────────────────────────────────────────────────

async function applyBookmarkChange(item: BookmarkItem): Promise<void> {
  const browserNodeId = await mappingStore.getBrowserNodeId(item.id, 'bookmark');

  if (browserNodeId) {
    // Update existing bookmark
    try {
      await chrome.bookmarks.update(browserNodeId, {
        title: item.title,
        url: item.url,
      });

      // If folderId changed, move it
      if (item.folderId) {
        const newParentBrowserId = await mappingStore.getBrowserNodeId(item.folderId, 'folder');
        if (newParentBrowserId) {
          const node = await chrome.bookmarks.get(browserNodeId);
          const currentParentId = node[0]?.parentId;
          if (currentParentId && currentParentId !== newParentBrowserId) {
            await chrome.bookmarks.move(browserNodeId, { parentId: newParentBrowserId });
          }
        }
      }
    } catch (err) {
      console.error(`Pull sync: failed to update bookmark ${item.id}:`, err);
    }
  } else {
    // Create new bookmark in browser
    try {
      const parentBrowserId = item.folderId
        ? await mappingStore.getBrowserNodeId(item.folderId, 'folder')
        : null;

      const created = await chrome.bookmarks.create({
        parentId: parentBrowserId ?? '1', // Default to Bookmarks Bar
        title: item.title,
        url: item.url,
      });

      await mappingStore.setMapping(created.id, item.id, 'bookmark');
    } catch (err) {
      console.error(`Pull sync: failed to create bookmark ${item.title}:`, err);
    }
  }
}

// ─── Deletion Handling ─────────────────────────────────────────────────────

async function applyDeletion(
  serverId: string,
  type: 'bookmark' | 'folder',
): Promise<void> {
  const browserNodeId = await mappingStore.getBrowserNodeId(serverId, type);
  if (!browserNodeId) {
    console.warn(`Pull sync: no browser mapping for deleted ${type} ${serverId}`);
    return;
  }

  try {
    if (type === 'folder') {
      await chrome.bookmarks.removeTree(browserNodeId);
    } else {
      await chrome.bookmarks.remove(browserNodeId);
    }
    await mappingStore.removeMapping(browserNodeId, type);
  } catch (err) {
    console.error(`Pull sync: failed to remove ${type} ${serverId}:`, err);
  }
}
