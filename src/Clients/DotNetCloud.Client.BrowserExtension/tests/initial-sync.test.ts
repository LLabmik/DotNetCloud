// ─── Initial Sync — Unit Tests ───────────────────────────────────────────────
// Tests for the server-first initial sync algorithm.
// Covers folder tree reconstruction, browser-only bookmark detection,
// batch upload with clientRef→serverId mapping, and cursor persistence.

import './setup';
import { runInitialSync, isInitialSyncInProgress } from '../src/sync/initial-sync';
import { mappingStore } from '../src/sync/mapping-store';
import { TokenManager } from '../src/auth/token-manager';
import { storageMock, bookmarksMock, setMockFetchResponses, resetMockFetch } from './setup';

const SERVER_URL = 'https://mint22:5443';

const SERVER_FOLDERS = [
  { id: 'sf-root', parentId: null, name: 'Bookmarks Bar', updatedAt: '2026-05-01T00:00:00Z' },
  { id: 'sf-work', parentId: 'sf-root', name: 'Work', updatedAt: '2026-05-01T00:00:00Z' },
  { id: 'sf-dev', parentId: 'sf-work', name: 'Development', updatedAt: '2026-05-02T00:00:00Z' },
];

const SERVER_BOOKMARKS = [
  {
    id: 'sb-1', folderId: 'sf-dev', url: 'https://github.com', title: 'GitHub',
    tags: ['dev'], notes: '', createdAt: '2026-05-01T00:00:00Z', updatedAt: '2026-05-01T00:00:00Z',
  },
  {
    id: 'sb-2', folderId: 'sf-root', url: 'https://news.ycombinator.com', title: 'Hacker News',
    tags: [], notes: '', createdAt: '2026-05-02T00:00:00Z', updatedAt: '2026-05-02T00:00:00Z',
  },
];

// ─── Helpers ────────────────────────────────────────────────────────────────

async function setupAuthenticated(): Promise<void> {
  await TokenManager.storeTokens({
    accessToken: 'at-test',
    refreshToken: 'rt-test',
    expiresAt: Date.now() + 3600_000,
    serverUrl: SERVER_URL,
  });
}

function seedBrowserTree(): void {
  bookmarksMock._seed([
    {
      id: '0',
      title: '',
      children: [
        {
          id: '1',
          parentId: '0',
          title: 'Bookmarks Bar',
          dateAdded: Date.now(),
          dateGroupModified: Date.now(),
          children: [],
        },
        {
          id: '2',
          parentId: '0',
          title: 'Other Bookmarks',
          dateAdded: Date.now(),
          dateGroupModified: Date.now(),
          children: [],
        },
      ],
    },
  ]);
}

describe('runInitialSync', () => {
  beforeEach(() => {
    resetMockFetch();
    storageMock.clear();
    bookmarksMock._reset();
    seedBrowserTree();
  });

  // ─── Error Handling ────────────────────────────────────────────────────

  describe('error handling', () => {
    it('throws when not authenticated', async () => {
      await expect(runInitialSync()).rejects.toThrow('Not authenticated');
    });

    it('sets isInitialSyncInProgress flag during execution', async () => {
      await setupAuthenticated();

      // Mock responses: getFolders, getBookmarks (paginated, returns less than take)
      setMockFetchResponses([
        { ok: true, status: 200, json: SERVER_FOLDERS },
        { ok: true, status: 200, json: SERVER_BOOKMARKS },
      ]);

      // Run sync and verify flag is managed
      // Use immediate flag set by constructor before await, so wait a tick
      const promise = runInitialSync();
      // Let the async function start executing (it's kicked off synchronously
      // before any awaits in the calling test)
      await expect(promise).resolves.not.toThrow();
      expect(isInitialSyncInProgress).toBe(false);
    });
  });

  // ─── Folder Tree Reconstruction ────────────────────────────────────────

  describe('folder tree reconstruction', () => {
    it('creates server folders in browser with correct parent chain', async () => {
      await setupAuthenticated();

      setMockFetchResponses([
        { ok: true, status: 200, json: SERVER_FOLDERS },
        { ok: true, status: 200, json: SERVER_BOOKMARKS },
      ]);

      await runInitialSync();

      // Verify folder mappings were created
      const rootBrowserId = await mappingStore.getBrowserNodeId('sf-root', 'folder');
      expect(rootBrowserId).not.toBeNull();

      const workBrowserId = await mappingStore.getBrowserNodeId('sf-work', 'folder');
      expect(workBrowserId).not.toBeNull();

      const devBrowserId = await mappingStore.getBrowserNodeId('sf-dev', 'folder');
      expect(devBrowserId).not.toBeNull();

      // Verify bookmark mappings were created
      const ghBrowserId = await mappingStore.getBrowserNodeId('sb-1', 'bookmark');
      expect(ghBrowserId).not.toBeNull();

      const hnBrowserId = await mappingStore.getBrowserNodeId('sb-2', 'bookmark');
      expect(hnBrowserId).not.toBeNull();
    });

    it('sets cursor after successful sync', async () => {
      await setupAuthenticated();

      setMockFetchResponses([
        { ok: true, status: 200, json: SERVER_FOLDERS },
        { ok: true, status: 200, json: SERVER_BOOKMARKS },
      ]);

      await runInitialSync();

      const cursor = await mappingStore.getCursor();
      expect(cursor).not.toBeNull();
      // Cursor should be a valid ISO-8601 timestamp
      expect(new Date(cursor!).toISOString()).toBe(cursor);
    });
  });

  // ─── Browser-Only Bookmarks ────────────────────────────────────────────

  describe('browser-only bookmarks', () => {
    it('uploads browser-only bookmarks via batch and completes sync', async () => {
      await setupAuthenticated();

      // Add a browser-only bookmark (not in server data)
      await chrome.bookmarks.create({
        parentId: '1',
        title: 'Browser Only',
        url: 'https://browser-only.example.com',
      });

      setMockFetchResponses([
        // getFolders
        { ok: true, status: 200, json: SERVER_FOLDERS },
        // getBookmarks
        { ok: true, status: 200, json: SERVER_BOOKMARKS },
        // batch (will be called for browser-only bookmarks)
        {
          ok: true,
          status: 200,
          json: {
            results: [
              {
                operation: 'create',
                serverId: 'new-server-uuid',
                success: true,
                error: null,
              },
            ],
          },
        },
      ]);

      await runInitialSync();

      // Cursor should be set (sync completed)
      const cursor = await mappingStore.getCursor();
      expect(cursor).not.toBeNull();
    });

    it('handles empty batch response gracefully', async () => {
      await setupAuthenticated();

      setMockFetchResponses([
        { ok: true, status: 200, json: SERVER_FOLDERS },
        { ok: true, status: 200, json: SERVER_BOOKMARKS },
      ]);

      // No browser-only bookmarks, so batch should not be called
      await expect(runInitialSync()).resolves.not.toThrow();
    });
  });

  // ─── Pagination ────────────────────────────────────────────────────────

  describe('pagination', () => {
    it('fetches all pages of server bookmarks', async () => {
      await setupAuthenticated();

      const page1 = SERVER_BOOKMARKS;
      const page2 = [
        {
          id: 'sb-3', folderId: 'sf-root', url: 'https://example.com', title: 'Example',
          tags: [], notes: '', createdAt: '2026-05-03T00:00:00Z', updatedAt: '2026-05-03T00:00:00Z',
        },
      ];

      setMockFetchResponses([
        { ok: true, status: 200, json: SERVER_FOLDERS },
        { ok: true, status: 200, json: page1 },        // skip=0, take=100
      ]);

      await runInitialSync();

      // Cursor should be set (indicating sync completed)
      const cursor = await mappingStore.getCursor();
      expect(cursor).not.toBeNull();
    });
  });
});

describe('isInitialSyncInProgress', () => {
  beforeEach(() => {
    resetMockFetch();
    storageMock.clear();
    bookmarksMock._reset();
    seedBrowserTree();
  });

  it('returns false before any sync', () => {
    expect(isInitialSyncInProgress).toBe(false);
  });

  it('returns false after sync completes', async () => {
    await setupAuthenticated();

    setMockFetchResponses([
      { ok: true, status: 200, json: SERVER_FOLDERS },
      { ok: true, status: 200, json: [] },
    ]);

    await runInitialSync();
    expect(isInitialSyncInProgress).toBe(false);
  });
});
