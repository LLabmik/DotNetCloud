// ─── DotNetCloud Browser Extension — Token Manager ─────────────────────────
// Manages OAuth2 token storage, retrieval, refresh, and lifecycle.
// Uses chrome.storage.local for persistence.
// Schedules automatic refresh via chrome.alarms.

import { type TokenSet } from '../api/types';

const STORAGE_KEY = 'auth';
const ALARM_NAME = 'token-refresh';
const REFRESH_MARGIN_MS = 60_000; // Refresh 60s before expiry

export class TokenManager {
  // ─── Storage ────────────────────────────────────────────────────────────

  /**
   * Stores the TokenSet in chrome.storage.local and schedules the first refresh.
   */
  static async storeTokens(tokens: TokenSet): Promise<void> {
    await chrome.storage.local.set({ [STORAGE_KEY]: tokens });
    TokenManager.scheduleRefresh(tokens);
  }

  /**
   * Retrieves the stored TokenSet, or null if not logged in.
   */
  static async getTokens(): Promise<TokenSet | null> {
    const result = await chrome.storage.local.get(STORAGE_KEY);
    return (result[STORAGE_KEY] as TokenSet | undefined) ?? null;
  }

  /**
   * Returns a valid access token, refreshing if necessary.
   * Returns null if no token is stored.
   */
  static async getAccessToken(): Promise<string | null> {
    const tokens = await TokenManager.getTokens();
    if (!tokens) return null;

    // Refresh if within margin of expiry
    if (tokens.expiresAt - Date.now() < REFRESH_MARGIN_MS) {
      try {
        await TokenManager.refresh();
        const refreshed = await TokenManager.getTokens();
        return refreshed?.accessToken ?? null;
      } catch {
        // Refresh failed; clear tokens and notify
        await TokenManager.clearTokens();
        return null;
      }
    }

    return tokens.accessToken;
  }

  /**
   * Refreshes the access token using the stored refresh token.
   * On failure (invalid_grant/revoked), clears tokens.
   */
  static async refresh(): Promise<void> {
    const tokens = await TokenManager.getTokens();
    if (!tokens?.refreshToken) {
      throw new Error('No refresh token available');
    }

    const response = await fetch(`${tokens.serverUrl}/connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'refresh_token',
        refresh_token: tokens.refreshToken,
        client_id: 'dotnetcloud-browser-extension',
      }),
    });

    if (!response.ok) {
      const body = (await response.json().catch(() => ({}))) as Record<string, unknown>;
      const error = body['error'] as string | undefined;

      if (error === 'invalid_grant' || error === 'revoked') {
        await TokenManager.clearTokens();
      }

      throw new Error(`Token refresh failed: ${error ?? response.statusText}`);
    }

    const body = (await response.json()) as Record<string, unknown>;
    const newTokens: TokenSet = {
      accessToken: body['access_token'] as string,
      refreshToken: (body['refresh_token'] as string) ?? tokens.refreshToken,
      expiresAt: Date.now() + ((body['expires_in'] as number) ?? 3600) * 1000,
      serverUrl: tokens.serverUrl,
    };

    await TokenManager.storeTokens(newTokens);
  }

  /**
   * Clears all stored tokens and removes the refresh alarm.
   */
  static async clearTokens(): Promise<void> {
    await chrome.storage.local.remove(STORAGE_KEY);
    try {
      await chrome.alarms.clear(ALARM_NAME);
    } catch {
      // Alarm may not exist; ignore
    }
  }

  // ─── Alarm-based Refresh ────────────────────────────────────────────────

  /**
   * Schedules a chrome.alarms alarm to refresh the token before expiry.
   */
  static scheduleRefresh(tokens?: TokenSet): void {
    const expiresAt = tokens?.expiresAt ?? 0;
    const delayMs = Math.max(10_000, expiresAt - Date.now() - REFRESH_MARGIN_MS);

    chrome.alarms.create(ALARM_NAME, { delayInMinutes: delayMs / 60_000 });
  }

  /**
   * Handles the chrome.alarms.onAlarm event for token refresh.
   * Call this from the background service worker's alarm listener.
   */
  static async handleAlarm(alarm: chrome.alarms.Alarm): Promise<void> {
    if (alarm.name === ALARM_NAME) {
      try {
        await TokenManager.refresh();
      } catch {
        // Refresh failed; tokens already cleared if invalid_grant
      }
    }
  }
}
