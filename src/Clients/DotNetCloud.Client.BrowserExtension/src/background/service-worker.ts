// ─── DotNetCloud Browser Extension — Service Worker (Background) ───────────
// Handles token refresh alarms, sync scheduling, and bookmark event listeners.
// Entry point for the Chrome MV3 / Firefox MV3 background script.

import { TokenManager } from '../auth/token-manager';

// ─── Alarm Handler ─────────────────────────────────────────────────────────

chrome.alarms.onAlarm.addListener((alarm) => {
  void TokenManager.handleAlarm(alarm);
});

// ─── Extension Install / Update ────────────────────────────────────────────

chrome.runtime.onInstalled.addListener((details) => {
  if (details.reason === 'install') {
    console.log('DotNetCloud Bookmarks extension installed.');
  } else if (details.reason === 'update') {
    console.log('DotNetCloud Bookmarks extension updated.');
  }

  // Schedule token refresh if already authenticated
  TokenManager.getTokens().then((tokens) => {
    if (tokens) {
      TokenManager.scheduleRefresh(tokens);
    }
  }).catch(console.error);
});

// ─── Startup ───────────────────────────────────────────────────────────────

// Schedule refresh on service worker startup (wake from idle)
TokenManager.getTokens().then((tokens) => {
  if (tokens) {
    TokenManager.scheduleRefresh(tokens);
  }
}).catch(console.error);

export {};
