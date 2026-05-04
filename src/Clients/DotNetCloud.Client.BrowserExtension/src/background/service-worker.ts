// ─── DotNetCloud Browser Extension — Service Worker (Background) ───────────
// Handles token refresh alarms, sync scheduling, and bookmark event listeners.
// Entry point for the Chrome MV3 / Firefox MV3 background script.

import { TokenManager } from '../auth/token-manager';
import { mappingStore } from '../sync/mapping-store';
import { runInitialSync } from '../sync/initial-sync';
import { startPushSync, stopPushSync, flushPendingOperations } from '../sync/push-sync';
import { startPullSync, stopPullSync, runPullCycle } from '../sync/pull-sync';

// ─── State ─────────────────────────────────────────────────────────────────

let syncInitialized = false;

// ─── Alarm Handler ─────────────────────────────────────────────────────────

chrome.alarms.onAlarm.addListener((alarm) => {
  // Route to appropriate handler based on alarm name
  if (alarm.name === 'bookmark-pull') {
    void runPullCycle();
  } else {
    void TokenManager.handleAlarm(alarm);
  }
});

// ─── Auth Change Listener ─────────────────────────────────────────────────

// Listen for auth changes from popup or other extension pages
chrome.storage.onChanged.addListener((changes, areaName) => {
  if (areaName !== 'local') return;

  if ('auth' in changes) {
    const newValue = changes['auth']?.newValue;
    const oldValue = changes['auth']?.oldValue;

    if (newValue && !oldValue) {
      // Auth was added — user logged in
      void initializeSync();
    } else if (!newValue && oldValue) {
      // Auth was removed — user logged out
      deinitializeSync();
    }
  }
});

// ─── Extension Install / Update ────────────────────────────────────────────

chrome.runtime.onInstalled.addListener((details) => {
  if (details.reason === 'install') {
    console.log('DotNetCloud Bookmarks extension installed.');
  } else if (details.reason === 'update') {
    console.log('DotNetCloud Bookmarks extension updated.');
  }

  // Initialize sync if already authenticated
  initializeSyncIfAuthenticated().catch(console.error);
});

// ─── Startup ───────────────────────────────────────────────────────────────

// Initialize on service worker startup (wake from idle)
initializeSyncIfAuthenticated().catch(console.error);

// ─── Sync Initialization ───────────────────────────────────────────────────

/**
 * Checks if the user is authenticated and initializes sync if so.
 */
async function initializeSyncIfAuthenticated(): Promise<void> {
  const tokens = await TokenManager.getTokens();
  if (tokens) {
    TokenManager.scheduleRefresh(tokens);
    await initializeSync();
  }
}

/**
 * Starts push sync, checks if initial sync is needed, and starts pull sync.
 */
async function initializeSync(): Promise<void> {
  if (syncInitialized) return;
  syncInitialized = true;

  // Start listening to bookmark events
  startPushSync();

  // Flush any pending operations queued while offline
  await flushPendingOperations();

  // Check if initial sync is needed
  const cursor = await mappingStore.getCursor();
  if (!cursor) {
    console.log('Service worker: no cursor found — running initial sync');
    try {
      await runInitialSync();
    } catch (err) {
      console.error('Service worker: initial sync failed:', err);
    }
  }

  // Start periodic pull sync
  startPullSync();

  // Run an immediate pull cycle
  await runPullCycle();
}

/**
 * Stops all sync activity.
 */
function deinitializeSync(): void {
  syncInitialized = false;
  stopPushSync();
  stopPullSync();
}

export {};
