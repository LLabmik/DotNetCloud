// ─── DotNetCloud Browser Extension — Initial Sync ──────────────────────────
// Called once when the extension first connects to a server (no cursor in storage).
// Server-first approach: pulls full server tree, creates mapped structure in
// browser, then uploads any browser-only bookmarks via batch.

import { ApiClient } from '../api/client';
import { type BookmarkItem, type BookmarkFolder, type CreateBookmarkRequest, type BatchRequest } from '../api/types';
import { mappingStore } from './mapping-store';
import { TokenManager } from '../auth/token-manager';

// Set to true while initial sync is running so push-sync can filter events
export let isInitialSyncInProgress = false;

// Chrome fixed root node IDs
const ROOT_NODE_ID = '0';
const BOOKMARKS_BAR_ID = '1';
const CHROME_ROOT_IDS = new Set(['0', '1', '2', '3']);

// ─── Public API ───────────────────────────────────────────────────────────

/**
 * Runs the initial sync process.
 * Should only be called when no sync cursor exists.
 */
export async function runInitialSync(): Promise<void> {
  const tokens = await TokenManager.getTokens();
  if (!tokens) {
    throw new Error('Not authenticated');
  }

  isInitialSyncInProgress = true;
  const client = new ApiClient(tokens.serverUrl);

  try {
    // 1. Fetch full server tree
    const serverFolders = await fetchAllFolders(client);
    const serverBookmarks = await fetchAllBookmarks(client);

    // 2. Get browser bookmark tree and flatten it
    const browserTree = await chrome.bookmarks.getTree();
    const browserFolderMap = buildBrowserFolderMap(browserTree);

    // 3. Sort server folders so parents come before children
    const sortedServerFolders = topSortFolders(serverFolders);

    // 4. Create or match server folders in browser, record mappings
    for (const folder of sortedServerFolders) {
      const browserId = await findOrCreateBrowserFolder(
        folder, serverFolders, browserFolderMap, client,
      );
      if (browserId) {
        await mappingStore.setMapping(browserId, folder.id, 'folder');
      }
    }

    // 5. Create or match server bookmarks in browser, record mappings
    for (const bookmark of serverBookmarks) {
      const browserId = await findOrCreateBrowserBookmark(bookmark, client);
      if (browserId) {
        await mappingStore.setMapping(browserId, bookmark.id, 'bookmark');
      }
    }

    // 6. Find browser-only bookmarks and batch-create them on server
    const browserOnlyLeaves = findBrowserOnlyBookmarks(browserTree);
    if (browserOnlyLeaves.length > 0) {
      const batchRequest = buildBatchRequest(browserOnlyLeaves, client);
      const batchResponse = await client.batch(batchRequest);

      for (const result of batchResponse.results) {
        if (result.success && result.clientRef && result.serverId) {
          await mappingStore.setMapping(result.clientRef, result.serverId, 'bookmark');
        }
      }
    }

    // 7. Set cursor to now
    const cursor = new Date().toISOString();
    await mappingStore.setCursor(cursor);
  } finally {
    isInitialSyncInProgress = false;
  }
}

// ─── Server Data Fetching ─────────────────────────────────────────────────

async function fetchAllFolders(client: ApiClient): Promise<BookmarkFolder[]> {
  // The server returns all folders when no parentId filter is specified.
  // If the API returns top-level only, we recurse. For now, assume flat list.
  const folders = await client.getFolders();

  // Defensive: if only top-level returned, recurse
  const topLevel = folders.filter((f) => f.parentId === null);
  if (topLevel.length === folders.length && folders.length > 0) {
    // Server returned only top-level; fetch children recursively
    const allFolders = [...folders];
    for (const folder of folders) {
      const children = await fetchFolderSubtree(client, folder.id);
      for (const child of children) {
        if (!allFolders.some((f) => f.id === child.id)) {
          allFolders.push(child);
        }
      }
    }
    return allFolders;
  }

  return folders;
}

async function fetchFolderSubtree(client: ApiClient, parentId: string): Promise<BookmarkFolder[]> {
  const result: BookmarkFolder[] = [];
  const children = await client.getFolders(parentId);
  for (const child of children) {
    result.push(child);
    const grandchildren = await fetchFolderSubtree(client, child.id);
    result.push(...grandchildren);
  }
  return result;
}

async function fetchAllBookmarks(client: ApiClient): Promise<BookmarkItem[]> {
  const all: BookmarkItem[] = [];
  let skip = 0;
  const take = 100;

  // eslint-disable-next-line no-constant-condition
  while (true) {
    const batch = await client.getBookmarks({ skip, take });
    all.push(...batch);
    if (batch.length < take) break;
    skip += take;
  }

  return all;
}

// ─── Browser Tree Utilities ───────────────────────────────────────────────

interface BrowserFolderInfo {
  id: string;
  parentId: string | null;
  title: string;
  path: string[]; // titles from root to this folder
}

/**
 * Flattens the browser bookmark tree into a map of folder info keyed by ID.
 */
function buildBrowserFolderMap(
  nodes: chrome.bookmarks.BookmarkTreeNode[],
  parentPath: string[] = [],
): Map<string, BrowserFolderInfo> {
  const map = new Map<string, BrowserFolderInfo>();

  for (const node of nodes) {
    const currentPath = [...parentPath, node.title];
    // A folder node has no `url` property
    if (!node.url && node.id) {
      map.set(node.id, {
        id: node.id,
        parentId: node.parentId ?? null,
        title: node.title,
        path: currentPath,
      });
    }
    if (node.children) {
      const childMap = buildBrowserFolderMap(node.children, currentPath);
      childMap.forEach((v, k) => map.set(k, v));
    }
  }

  return map;
}

/**
 * Collects all bookmark leaf nodes (nodes with a URL) from the browser tree
 * that are NOT in any existing mapping (both forward and reverse).
 */
function findBrowserOnlyBookmarks(
  nodes: chrome.bookmarks.BookmarkTreeNode[],
  results: chrome.bookmarks.BookmarkTreeNode[] = [],
): chrome.bookmarks.BookmarkTreeNode[] {
  for (const node of nodes) {
    if (node.url && node.id && !CHROME_ROOT_IDS.has(node.id)) {
      results.push(node);
    }
    if (node.children) {
      findBrowserOnlyBookmarks(node.children, results);
    }
  }
  return results;
}

// ─── Server Folder Sorting (topological) ─────────────────────────────────

function topSortFolders(folders: BookmarkFolder[]): BookmarkFolder[] {
  const sorted: BookmarkFolder[] = [];
  const added = new Set<string>();

  // Parent IDs of root-level folders
  const rootIds = folders.filter((f) => f.parentId === null).map((f) => f.id);

  // Process roots first, then children whose parent was already added
  let queue = [...rootIds];
  while (queue.length > 0) {
    const id = queue.shift()!;
    if (added.has(id)) continue;

    const folder = folders.find((f) => f.id === id);
    if (!folder) continue;

    // Ensure parent is added first
    if (folder.parentId && !added.has(folder.parentId)) {
      // Push parent to front of queue
      queue.unshift(folder.parentId);
      continue;
    }

    sorted.push(folder);
    added.add(id);

    // Add children to queue
    const children = folders.filter((f) => f.parentId === id).map((f) => f.id);
    queue.push(...children);
  }

  return sorted;
}

// ─── Browser Folder Creation / Matching ───────────────────────────────────

/**
 * Finds or creates a browser folder matching the given server folder.
 * Returns the Chrome browser node ID.
 */
async function findOrCreateBrowserFolder(
  folder: BookmarkFolder,
  allServerFolders: BookmarkFolder[],
  browserFolderMap: Map<string, BrowserFolderInfo>,
  _client: ApiClient,
): Promise<string | null> {
  // Check if we already have a mapping for this folder
  const existingBrowserIds = await findMappedBrowserFolder(folder.id);
  if (existingBrowserIds.length > 0) return existingBrowserIds[0]!;

  // Determine parent browser ID
  let parentBrowserId: string | null = null;
  if (folder.parentId) {
    const parentMapping = await mappingStore.getBrowserNodeId(folder.parentId, 'folder');
    if (parentMapping) {
      parentBrowserId = parentMapping;
    } else {
      // Parent not yet mapped; try to map it first
      const parentFolder = allServerFolders.find((f) => f.id === folder.parentId);
      if (parentFolder) {
        parentBrowserId = await findOrCreateBrowserFolder(
          parentFolder, allServerFolders, browserFolderMap, _client,
        );
        if (parentBrowserId) {
          await mappingStore.setMapping(parentBrowserId, parentFolder.id, 'folder');
        }
      }
    }
  }

  // If we have no parent mapping, default to Bookmarks Bar
  const effectiveParentId = parentBrowserId ?? BOOKMARKS_BAR_ID;

  // Try to find a matching folder by name under the parent
  const browserFolder = await findMatchingBrowserFolder(
    folder.name, effectiveParentId, browserFolderMap,
  );

  if (browserFolder) {
    return browserFolder.id;
  }

  // Create the folder in browser
  try {
    const created = await chrome.bookmarks.create({
      parentId: effectiveParentId,
      title: folder.name,
    });
    return created.id;
  } catch (err) {
    console.error('Failed to create browser folder:', folder.name, err);
    return null;
  }
}

/**
 * Searches for an existing browser folder with matching name under the given parent.
 */
async function findMatchingBrowserFolder(
  name: string,
  parentId: string,
  browserFolderMap: Map<string, BrowserFolderInfo>,
): Promise<BrowserFolderInfo | null> {
  // Look through the flat map for folders under parentId with matching name
  const parentChildren = Array.from(browserFolderMap.values())
    .filter((f) => f.parentId === parentId && f.title === name);

  if (parentChildren.length > 0) return parentChildren[0]!;

  // Fallback: search Chrome API directly
  const children = await chrome.bookmarks.getChildren(parentId);
  const match = children.find((c) => !c.url && c.title === name);
  if (match) {
    // Add to map for future lookups
    browserFolderMap.set(match.id, {
      id: match.id,
      parentId: match.parentId ?? null,
      title: match.title,
      path: [],
    });
    return browserFolderMap.get(match.id)!;
  }

  return null;
}

/**
 * Finds browser node IDs that are already mapped to a server folder ID.
 */
async function findMappedBrowserFolder(serverFolderId: string): Promise<string[]> {
  const browserId = await mappingStore.getBrowserNodeId(serverFolderId, 'folder');
  return browserId ? [browserId] : [];
}

// ─── Browser Bookmark Creation / Matching ─────────────────────────────────

async function findOrCreateBrowserBookmark(
  bookmark: BookmarkItem,
  _client: ApiClient,
): Promise<string | null> {
  // Check existing forward mapping
  const existingBrowserId = await mappingStore.getBrowserNodeId(bookmark.id, 'bookmark');
  if (existingBrowserId) return existingBrowserId;

  // Determine parent browser folder ID
  let parentBrowserId: string | null = BOOKMARKS_BAR_ID;
  if (bookmark.folderId) {
    const mappedParent = await mappingStore.getBrowserNodeId(bookmark.folderId, 'folder');
    if (mappedParent) {
      parentBrowserId = mappedParent;
    }
  }

  // Search for an existing browser bookmark matching URL + title under the parent
  try {
    const children = parentBrowserId
      ? await chrome.bookmarks.getChildren(parentBrowserId)
      : [];

    const match = children.find(
      (c) => c.url === bookmark.url && c.title === bookmark.title,
    );
    if (match) return match.id;

    // Create the bookmark in browser
    const created = await chrome.bookmarks.create({
      parentId: parentBrowserId ?? BOOKMARKS_BAR_ID,
      title: bookmark.title,
      url: bookmark.url,
    });
    return created.id;
  } catch (err) {
    console.error('Failed to create browser bookmark:', bookmark.title, err);
    return null;
  }
}

// ─── Batch Request Builder ─────────────────────────────────────────────────

function buildBatchRequest(
  browserOnlyNodes: chrome.bookmarks.BookmarkTreeNode[],
  _client: ApiClient,
): BatchRequest {
  const creates: CreateBookmarkRequest[] = [];

  for (const node of browserOnlyNodes) {
    // Skip root nodes and nodes without URL
    if (!node.url || !node.id || CHROME_ROOT_IDS.has(node.id)) continue;

    creates.push({
      url: node.url,
      title: node.title,
      // We can't determine the server folderId here; leave it null
      // The server will place root-level bookmarks in the default folder
      folderId: null,
    });
  }

  return { creates };
}
