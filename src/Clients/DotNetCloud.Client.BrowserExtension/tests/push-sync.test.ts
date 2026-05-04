// ─── Push Sync — Unit Tests ──────────────────────────────────────────────────
// Tests for the incremental push sync engine.
// Covers lifecycle (start/stop), listener registration, and guard conditions.
// Debounced API call verification is covered by initial-sync and conflict-resolution tests.

import './setup';
import { startPushSync, stopPushSync } from '../src/sync/push-sync';
import { bookmarksMock, resetMockFetch, clearFetchCalls } from './setup';

describe('pushSync', () => {
  beforeEach(() => {
    resetMockFetch();
    bookmarksMock._reset();
    clearFetchCalls();
  });

  afterEach(() => {
    stopPushSync();
  });

  // ─── Lifecycle ─────────────────────────────────────────────────────────

  describe('lifecycle', () => {
    it('can be started and stopped without errors', () => {
      startPushSync();
      expect(() => startPushSync()).not.toThrow(); // Idempotent
      stopPushSync();
      expect(() => stopPushSync()).not.toThrow(); // Idempotent
    });

    it('registers bookmarks.onCreated listener on start', () => {
      const addSpy = jest.spyOn(chrome.bookmarks.onCreated, 'addListener');
      startPushSync();
      expect(addSpy).toHaveBeenCalled();
      addSpy.mockRestore();
      stopPushSync();
    });

    it('removes listeners on stop', () => {
      const removeSpy = jest.spyOn(chrome.bookmarks.onCreated, 'removeListener');
      startPushSync();
      stopPushSync();
      expect(removeSpy).toHaveBeenCalled();
      removeSpy.mockRestore();
    });

    it('registers all four bookmark event listeners', () => {
      const createdSpy = jest.spyOn(chrome.bookmarks.onCreated, 'addListener');
      const removedSpy = jest.spyOn(chrome.bookmarks.onRemoved, 'addListener');
      const changedSpy = jest.spyOn(chrome.bookmarks.onChanged, 'addListener');
      const movedSpy = jest.spyOn(chrome.bookmarks.onMoved, 'addListener');

      startPushSync();

      expect(createdSpy).toHaveBeenCalledTimes(1);
      expect(removedSpy).toHaveBeenCalledTimes(1);
      expect(changedSpy).toHaveBeenCalledTimes(1);
      expect(movedSpy).toHaveBeenCalledTimes(1);

      createdSpy.mockRestore();
      removedSpy.mockRestore();
      changedSpy.mockRestore();
      movedSpy.mockRestore();
      stopPushSync();
    });
  });
});
