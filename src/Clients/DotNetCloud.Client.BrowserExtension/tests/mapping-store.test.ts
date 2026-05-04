// ─── Mapping Store — Unit Tests ──────────────────────────────────────────────
// Tests for the bidirectional browser↔server ID mapping store.
// Covers set/get/remove/clear operations for both bookmark and folder maps,
// as well as cursor persistence.

import './setup';
import { mappingStore } from '../src/sync/mapping-store';
import { storageMock } from './setup';

describe('mappingStore', () => {
  beforeEach(() => {
    storageMock.clear();
  });

  // ─── setMapping / getServerId / getBrowserNodeId ─────────────────────────

  describe('setMapping and getServerId', () => {
    it('stores a bookmark mapping and retrieves server ID by browser node ID', async () => {
      await mappingStore.setMapping('browser-1', 'server-uuid-1', 'bookmark');
      const serverId = await mappingStore.getServerId('browser-1', 'bookmark');
      expect(serverId).toBe('server-uuid-1');
    });

    it('stores a folder mapping and retrieves server ID by browser node ID', async () => {
      await mappingStore.setMapping('browser-folder-1', 'server-folder-uuid-1', 'folder');
      const serverId = await mappingStore.getServerId('browser-folder-1', 'folder');
      expect(serverId).toBe('server-folder-uuid-1');
    });

    it('returns null for unmapped browser node ID', async () => {
      const serverId = await mappingStore.getServerId('nonexistent', 'bookmark');
      expect(serverId).toBeNull();
    });

    it('stores separate maps for bookmarks and folders', async () => {
      await mappingStore.setMapping('b-node', 's-bookmark', 'bookmark');
      await mappingStore.setMapping('f-node', 's-folder', 'folder');

      // Bookmark map should not contain folder mapping
      const folderAsBookmark = await mappingStore.getServerId('f-node', 'bookmark');
      expect(folderAsBookmark).toBeNull();

      // Folder map should not contain bookmark mapping
      const bookmarkAsFolder = await mappingStore.getServerId('b-node', 'folder');
      expect(bookmarkAsFolder).toBeNull();

      // Correct lookups
      expect(await mappingStore.getServerId('b-node', 'bookmark')).toBe('s-bookmark');
      expect(await mappingStore.getServerId('f-node', 'folder')).toBe('s-folder');
    });
  });

  describe('getBrowserNodeId (reverse lookup)', () => {
    it('returns browser node ID by server ID for bookmarks', async () => {
      await mappingStore.setMapping('browser-1', 'server-uuid-1', 'bookmark');
      const browserId = await mappingStore.getBrowserNodeId('server-uuid-1', 'bookmark');
      expect(browserId).toBe('browser-1');
    });

    it('returns browser node ID by server ID for folders', async () => {
      await mappingStore.setMapping('browser-folder-1', 'server-folder-uuid-1', 'folder');
      const browserId = await mappingStore.getBrowserNodeId('server-folder-uuid-1', 'folder');
      expect(browserId).toBe('browser-folder-1');
    });

    it('returns null for unmapped server ID', async () => {
      const browserId = await mappingStore.getBrowserNodeId('unknown-server-id', 'bookmark');
      expect(browserId).toBeNull();
    });
  });

  // ─── removeMapping ──────────────────────────────────────────────────────

  describe('removeMapping', () => {
    it('removes both forward and reverse bookmark mapping', async () => {
      await mappingStore.setMapping('browser-1', 'server-uuid-1', 'bookmark');

      // Verify both directions exist
      expect(await mappingStore.getServerId('browser-1', 'bookmark')).toBe('server-uuid-1');
      expect(await mappingStore.getBrowserNodeId('server-uuid-1', 'bookmark')).toBe('browser-1');

      await mappingStore.removeMapping('browser-1', 'bookmark');

      // Both directions should now be null
      expect(await mappingStore.getServerId('browser-1', 'bookmark')).toBeNull();
      expect(await mappingStore.getBrowserNodeId('server-uuid-1', 'bookmark')).toBeNull();
    });

    it('removes both forward and reverse folder mapping', async () => {
      await mappingStore.setMapping('b-folder-1', 's-folder-1', 'folder');
      await mappingStore.removeMapping('b-folder-1', 'folder');

      expect(await mappingStore.getServerId('b-folder-1', 'folder')).toBeNull();
      expect(await mappingStore.getBrowserNodeId('s-folder-1', 'folder')).toBeNull();
    });

    it('does not throw when removing non-existent mapping', async () => {
      await expect(
        mappingStore.removeMapping('nonexistent', 'bookmark'),
      ).resolves.not.toThrow();
    });

    it('only removes the specified type mapping', async () => {
      await mappingStore.setMapping('shared-node', 's-bookmark', 'bookmark');
      await mappingStore.setMapping('shared-node', 's-folder', 'folder');

      await mappingStore.removeMapping('shared-node', 'bookmark');

      // Bookmark mapping should be gone
      expect(await mappingStore.getServerId('shared-node', 'bookmark')).toBeNull();
      // Folder mapping should still exist
      expect(await mappingStore.getServerId('shared-node', 'folder')).toBe('s-folder');
    });
  });

  // ─── Cursor ─────────────────────────────────────────────────────────────

  describe('cursor', () => {
    it('returns null when no cursor has been set', async () => {
      const cursor = await mappingStore.getCursor();
      expect(cursor).toBeNull();
    });

    it('stores and retrieves cursor', async () => {
      const testCursor = '2026-05-04T12:00:00Z';
      await mappingStore.setCursor(testCursor);
      const cursor = await mappingStore.getCursor();
      expect(cursor).toBe(testCursor);
    });

    it('overwrites previous cursor value', async () => {
      await mappingStore.setCursor('2026-05-04T10:00:00Z');
      await mappingStore.setCursor('2026-05-04T12:00:00Z');
      const cursor = await mappingStore.getCursor();
      expect(cursor).toBe('2026-05-04T12:00:00Z');
    });
  });

  // ─── clearAll ───────────────────────────────────────────────────────────

  describe('clearAll', () => {
    it('clears all mappings and cursor', async () => {
      await mappingStore.setMapping('b1', 's1', 'bookmark');
      await mappingStore.setMapping('b2', 's2', 'folder');
      await mappingStore.setCursor('2026-05-04T12:00:00Z');

      await mappingStore.clearAll();

      expect(await mappingStore.getServerId('b1', 'bookmark')).toBeNull();
      expect(await mappingStore.getServerId('b2', 'folder')).toBeNull();
      expect(await mappingStore.getBrowserNodeId('s1', 'bookmark')).toBeNull();
      expect(await mappingStore.getBrowserNodeId('s2', 'folder')).toBeNull();
      expect(await mappingStore.getCursor()).toBeNull();
    });

    it('works correctly when already empty', async () => {
      await expect(mappingStore.clearAll()).resolves.not.toThrow();
      expect(await mappingStore.getCursor()).toBeNull();
    });
  });

  // ─── Persistence ────────────────────────────────────────────────────────

  describe('persistence across calls', () => {
    it('retains mappings across multiple get/set operations', async () => {
      await mappingStore.setMapping('b1', 's1', 'bookmark');
      await mappingStore.setMapping('b2', 's2', 'bookmark');
      await mappingStore.setMapping('b3', 's3', 'bookmark');

      expect(await mappingStore.getServerId('b1', 'bookmark')).toBe('s1');
      expect(await mappingStore.getServerId('b2', 'bookmark')).toBe('s2');
      expect(await mappingStore.getServerId('b3', 'bookmark')).toBe('s3');
    });

    it('survives storage clear and re-initializes', async () => {
      await mappingStore.setMapping('b1', 's1', 'bookmark');
      storageMock.clear();

      // After manual clear(), the store should return defaults
      expect(await mappingStore.getServerId('b1', 'bookmark')).toBeNull();
      expect(await mappingStore.getCursor()).toBeNull();
    });
  });
});
