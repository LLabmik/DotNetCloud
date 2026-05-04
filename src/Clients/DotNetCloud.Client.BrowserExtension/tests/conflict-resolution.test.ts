// ─── Conflict Resolution — Unit Tests ────────────────────────────────────────
// Tests for the pull sync conflict resolution behavior.
// Server state always wins on conflict.
// Covers: pull cycle applies server item even when browser has different title,
// deletedIds processing, missing browserNodeId handling, and folder deletions.

import './setup';
import { runPullCycle, startPullSync, stopPullSync } from '../src/sync/pull-sync';
import { mappingStore } from '../src/sync/mapping-store';
import { TokenManager } from '../src/auth/token-manager';
import { storageMock, bookmarksMock, resetMockFetch, setMockFetchResponses, clearFetchCalls } from './setup';

const SERVER_URL = 'https://mint22:5443';

// ─── Helpers ────────────────────────────────────────────────────────────────

async function setupAuthenticated(): Promise<void> {
  await TokenManager.storeTokens({
    accessToken: 'at-conflict',
    refreshToken: 'rt-conflict',
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

describe('conflict resolution', () => {
  beforeEach(() => {
    resetMockFetch();
    storageMock.clear();
    bookmarksMock._reset();
    seedBrowserTree();
    clearFetchCalls();
  });

  afterEach(() => {
    stopPullSync();
  });

  // ─── Server Always Wins ─────────────────────────────────────────────────

  describe('server always wins', () => {
    it('applies server title when browser has a different title for same bookmark', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      // Create a browser bookmark and map it to a server bookmark
      const created = await chrome.bookmarks.create({
        parentId: '1',
        title: 'Browser Title',
        url: 'https://example.com',
      });
      await mappingStore.setMapping(created.id, 'server-bookmark-1', 'bookmark');

      // Pull sync returns a different title from server
      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [
              {
                id: 'server-bookmark-1',
                folderId: null,
                url: 'https://example.com',
                title: 'Server Title', // Different from browser
                tags: [],
                notes: '',
                createdAt: '2026-05-01T00:00:00Z',
                updatedAt: '2026-05-04T00:00:00Z',
              },
            ],
            deletedIds: [],
            folders: [],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      // Verify browser bookmark now has server's title
      const updated = await chrome.bookmarks.get(created.id);
      expect(updated[0]?.title).toBe('Server Title');
      expect(updated[0]?.url).toBe('https://example.com');
    });

    it('applies server URL when browser has a different URL', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      const created = await chrome.bookmarks.create({
        parentId: '1',
        title: 'My Bookmark',
        url: 'https://old-url.example.com',
      });
      await mappingStore.setMapping(created.id, 'server-bm-2', 'bookmark');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [{
              id: 'server-bm-2',
              folderId: null,
              url: 'https://new-url.example.com',
              title: 'My Bookmark',
              tags: [], notes: '',
              createdAt: '2026-05-01T00:00:00Z',
              updatedAt: '2026-05-04T00:00:00Z',
            }],
            deletedIds: [],
            folders: [],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      const updated = await chrome.bookmarks.get(created.id);
      expect(updated[0]?.url).toBe('https://new-url.example.com');
    });
  });

  // ─── Deletion Handling ──────────────────────────────────────────────────

  describe('deletion handling', () => {
    it('removes browser bookmark when server sends deletedIds', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      const created = await chrome.bookmarks.create({
        parentId: '1',
        title: 'Doomed Bookmark',
        url: 'https://doomed.example.com',
      });
      await mappingStore.setMapping(created.id, 'server-doomed', 'bookmark');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [],
            deletedIds: ['server-doomed'],
            folders: [],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      // Bookmark should be removed from browser
      let stillExists = false;
      try {
        await chrome.bookmarks.get(created.id);
        stillExists = true;
      } catch {
        stillExists = false;
      }
      expect(stillExists).toBe(false);

      // Mapping should be removed
      const serverId = await mappingStore.getServerId(created.id, 'bookmark');
      expect(serverId).toBeNull();
    });

    it('removes browser folder when server sends deletedFolderIds', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      const folder = await chrome.bookmarks.create({
        parentId: '1',
        title: 'Doomed Folder',
      });
      await mappingStore.setMapping(folder.id, 'server-doomed-folder', 'folder');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [],
            deletedIds: [],
            folders: [],
            deletedFolderIds: ['server-doomed-folder'],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      // Folder should be removed
      let stillExists = false;
      try {
        await chrome.bookmarks.get(folder.id);
        stillExists = true;
      } catch {
        stillExists = false;
      }
      expect(stillExists).toBe(false);

      const serverId = await mappingStore.getServerId(folder.id, 'folder');
      expect(serverId).toBeNull();
    });
  });

  // ─── Missing Mapping Handling ──────────────────────────────────────────

  describe('missing mapping handling', () => {
    it('does not crash when deletedId has no browser mapping', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [],
            deletedIds: ['server-id-with-no-mapping'],
            folders: [],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      // Should not throw even though the deleted ID has no mapping
      await expect(runPullCycle()).resolves.not.toThrow();
    });

    it('does not crash when deletedFolderId has no browser mapping', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [],
            deletedIds: [],
            folders: [],
            deletedFolderIds: ['unknown-folder-id'],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await expect(runPullCycle()).resolves.not.toThrow();
    });
  });

  // ─── Folder Application ────────────────────────────────────────────────

  describe('folder application', () => {
    it('creates new server folders in browser', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      // Seed mapping for parent folder
      await mappingStore.setMapping('1', 'sf-bookmarks-bar', 'folder');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [],
            deletedIds: [],
            folders: [
              { id: 'sf-new-folder', parentId: 'sf-bookmarks-bar', name: 'New Server Folder', updatedAt: '2026-05-04T00:00:00Z' },
            ],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      // Verify folder was created in browser
      const children = await chrome.bookmarks.getChildren('1');
      const newFolder = children.find((c) => c.title === 'New Server Folder' && !c.url);
      expect(newFolder).toBeDefined();

      // Verify mapping was stored
      if (newFolder) {
        const serverId = await mappingStore.getServerId(newFolder.id, 'folder');
        expect(serverId).toBe('sf-new-folder');
      }
    });

    it('updates existing folder title when server sends a different name', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      const folder = await chrome.bookmarks.create({
        parentId: '1',
        title: 'Old Folder Name',
      });
      await mappingStore.setMapping(folder.id, 'sf-updated-folder', 'folder');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [],
            deletedIds: [],
            folders: [
              { id: 'sf-updated-folder', parentId: null, name: 'New Folder Name', updatedAt: '2026-05-04T00:00:00Z' },
            ],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      const updated = await chrome.bookmarks.get(folder.id);
      expect(updated[0]?.title).toBe('New Folder Name');
    });
  });

  // ─── Bookmark Application ──────────────────────────────────────────────

  describe('bookmark application', () => {
    it('creates new server bookmarks in browser', async () => {
      await setupAuthenticated();
      await mappingStore.setCursor('2026-05-01T00:00:00Z');
      await mappingStore.setMapping('1', 'sf-root', 'folder');

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            items: [{
              id: 'sb-new',
              folderId: 'sf-root',
              url: 'https://server-new.example.com',
              title: 'Server New',
              tags: [], notes: '',
              createdAt: '2026-05-04T00:00:00Z',
              updatedAt: '2026-05-04T00:00:00Z',
            }],
            deletedIds: [],
            folders: [],
            deletedFolderIds: [],
            nextCursor: '2026-05-04T12:00:00Z',
            hasMore: false,
          },
        },
      ]);

      await runPullCycle();

      // Verify bookmark was created in browser
      const children = await chrome.bookmarks.getChildren('1');
      const newBm = children.find((c) => c.title === 'Server New');
      expect(newBm).toBeDefined();
      expect(newBm?.url).toBe('https://server-new.example.com');

      // Verify mapping was stored
      if (newBm) {
        const serverId = await mappingStore.getServerId(newBm.id, 'bookmark');
        expect(serverId).toBe('sb-new');
      }
    });
  });

  // ─── Pull Cycle Guards ─────────────────────────────────────────────────

  describe('pull cycle guards', () => {
    it('skips pull cycle when no cursor exists (initial sync not done)', async () => {
      await setupAuthenticated();
      // No cursor set

      await expect(runPullCycle()).resolves.not.toThrow();
    });

    it('skips pull cycle when not authenticated', async () => {
      // No auth set up
      await mappingStore.setCursor('2026-05-01T00:00:00Z');

      await expect(runPullCycle()).resolves.not.toThrow();
    });
  });
});
