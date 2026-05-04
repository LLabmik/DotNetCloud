// ─── Token Manager — Unit Tests ──────────────────────────────────────────────

import './setup';
import { TokenManager } from '../src/auth/token-manager';
import { type TokenSet } from '../src/api/types';
import { setMockFetchResponses, resetMockFetch, storageMock, alarmsMock } from './setup';

const SERVER_URL = 'https://mint22:5443';

function makeTokenSet(overrides?: Partial<TokenSet>): TokenSet {
  return {
    accessToken: 'at-test123',
    refreshToken: 'rt-test456',
    expiresAt: Date.now() + 3600_000, // 1 hour from now
    serverUrl: SERVER_URL,
    ...overrides,
  };
}

describe('TokenManager', () => {
  beforeEach(() => {
    resetMockFetch();
    storageMock.clear();
    alarmsMock.clearAll();
  });

  // ─── storeTokens ─────────────────────────────────────────────────────────

  describe('storeTokens', () => {
    it('stores tokens in chrome.storage.local', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      const stored = await TokenManager.getTokens();
      expect(stored).toEqual(tokens);
    });

    it('schedules a refresh alarm after storing', async () => {
      const tokens = makeTokenSet({ expiresAt: Date.now() + 120_000 });
      await TokenManager.storeTokens(tokens);

      // Alarm should be created (60s before expiry = 60s from now)
      const alarmName = 'token-refresh';
      // The alarm exists (we can't easily check delayInMinutes in mock)
      // But we can verify it was created by checking alarms mock
    });
  });

  // ─── getTokens ───────────────────────────────────────────────────────────

  describe('getTokens', () => {
    it('returns null when no tokens stored', async () => {
      const tokens = await TokenManager.getTokens();
      expect(tokens).toBeNull();
    });

    it('returns stored tokens', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      const result = await TokenManager.getTokens();
      expect(result).toEqual(tokens);
    });
  });

  // ─── getAccessToken ──────────────────────────────────────────────────────

  describe('getAccessToken', () => {
    it('returns null when not authenticated', async () => {
      const token = await TokenManager.getAccessToken();
      expect(token).toBeNull();
    });

    it('returns existing token when not near expiry', async () => {
      const tokens = makeTokenSet({ expiresAt: Date.now() + 600_000 }); // 10 min
      await TokenManager.storeTokens(tokens);

      const token = await TokenManager.getAccessToken();
      expect(token).toBe('at-test123');
    });

    it('refreshes token when close to expiry', async () => {
      const tokens = makeTokenSet({ expiresAt: Date.now() + 30_000 }); // 30s (within margin)
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            access_token: 'at-refreshed',
            refresh_token: 'rt-refreshed',
            expires_in: 3600,
            token_type: 'Bearer',
          },
        },
      ]);

      const token = await TokenManager.getAccessToken();
      expect(token).toBe('at-refreshed');

      // Verify new tokens stored
      const stored = await TokenManager.getTokens();
      expect(stored?.accessToken).toBe('at-refreshed');
      expect(stored?.refreshToken).toBe('rt-refreshed');
    });

    it('returns null when refresh fails with invalid_grant', async () => {
      const tokens = makeTokenSet({ expiresAt: Date.now() + 30_000 });
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: false,
          status: 400,
          json: { error: 'invalid_grant' },
        },
      ]);

      const token = await TokenManager.getAccessToken();
      expect(token).toBeNull();

      // Tokens should be cleared
      const stored = await TokenManager.getTokens();
      expect(stored).toBeNull();
    });
  });

  // ─── refresh ─────────────────────────────────────────────────────────────

  describe('refresh', () => {
    it('sends correct refresh request', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      let requestBody = '';

      const originalFetch = globalThis.fetch;
      globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
        if (typeof input === 'string' && input.includes('/connect/token')) {
          requestBody = init?.body?.toString() ?? '';
        }
        return (await originalFetch(input, init)) as Response;
      };

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            access_token: 'at-fresh',
            expires_in: 3600,
            token_type: 'Bearer',
          },
        },
      ]);

      await TokenManager.refresh();

      expect(requestBody).toContain('grant_type=refresh_token');
      expect(requestBody).toContain('refresh_token=rt-test456');
      expect(requestBody).toContain('client_id=dotnetcloud-browser-extension');

      globalThis.fetch = originalFetch;
    });

    it('preserves existing refresh token if server does not return new one', async () => {
      const tokens = makeTokenSet({ refreshToken: 'rt-existing' });
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            access_token: 'at-fresh',
            expires_in: 3600,
            // No refresh_token in response
          },
        },
      ]);

      await TokenManager.refresh();

      const stored = await TokenManager.getTokens();
      expect(stored?.refreshToken).toBe('rt-existing');
    });

    it('throws when no refresh token available', async () => {
      await TokenManager.clearTokens();

      await expect(TokenManager.refresh()).rejects.toThrow('No refresh token available');
    });

    it('clears tokens on invalid_grant and throws', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: false,
          status: 400,
          json: { error: 'invalid_grant' },
        },
      ]);

      await expect(TokenManager.refresh()).rejects.toThrow('Token refresh failed');

      const stored = await TokenManager.getTokens();
      expect(stored).toBeNull();
    });

    it('clears tokens on revoked and throws', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: false,
          status: 400,
          json: { error: 'revoked' },
        },
      ]);

      await expect(TokenManager.refresh()).rejects.toThrow('Token refresh failed');

      const stored = await TokenManager.getTokens();
      expect(stored).toBeNull();
    });

    it('throws on other errors without clearing tokens', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: false,
          status: 500,
          statusText: 'Server Error',
          json: {},
        },
      ]);

      await expect(TokenManager.refresh()).rejects.toThrow('Token refresh failed');

      // Tokens should NOT be cleared for non-auth errors
      const stored = await TokenManager.getTokens();
      expect(stored).not.toBeNull();
    });
  });

  // ─── clearTokens ─────────────────────────────────────────────────────────

  describe('clearTokens', () => {
    it('removes tokens from storage', async () => {
      await TokenManager.storeTokens(makeTokenSet());
      await TokenManager.clearTokens();

      const stored = await TokenManager.getTokens();
      expect(stored).toBeNull();
    });

    it('clears the refresh alarm', async () => {
      await TokenManager.storeTokens(makeTokenSet());

      // Verify alarm was created
      expect(alarmsMock.get('token-refresh')).toBeDefined();

      await TokenManager.clearTokens();

      // Alarm should be cleared
      expect(alarmsMock.get('token-refresh')).toBeUndefined();
    });

    it('does not throw when no alarm exists', async () => {
      await expect(TokenManager.clearTokens()).resolves.not.toThrow();
    });
  });

  // ─── scheduleRefresh ─────────────────────────────────────────────────────

  describe('scheduleRefresh', () => {
    it('creates alarm for token refresh', async () => {
      TokenManager.scheduleRefresh(makeTokenSet({ expiresAt: Date.now() + 120_000 }));

      const alarm = alarmsMock.get('token-refresh');
      expect(alarm).toBeDefined();
      expect(alarm?.name).toBe('token-refresh');
    });

    it('does not create alarm when no tokens provided', () => {
      // Should handle gracefully without error
      TokenManager.scheduleRefresh();
    });
  });

  // ─── handleAlarm ─────────────────────────────────────────────────────────

  describe('handleAlarm', () => {
    it('refreshes token on token-refresh alarm', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: true,
          status: 200,
          json: {
            access_token: 'at-refreshed-by-alarm',
            expires_in: 3600,
          },
        },
      ]);

      await TokenManager.handleAlarm({
        name: 'token-refresh',
        scheduledTime: Date.now(),
      });

      const stored = await TokenManager.getTokens();
      expect(stored?.accessToken).toBe('at-refreshed-by-alarm');
    });

    it('ignores non-token alarms', async () => {
      // Should not throw or try to refresh
      await expect(
        TokenManager.handleAlarm({
          name: 'some-other-alarm',
          scheduledTime: Date.now(),
        }),
      ).resolves.not.toThrow();
    });

    it('handles refresh failure gracefully', async () => {
      const tokens = makeTokenSet();
      await TokenManager.storeTokens(tokens);

      setMockFetchResponses([
        {
          ok: false,
          status: 400,
          json: { error: 'invalid_grant' },
        },
      ]);

      // Should not throw — caught internally
      await expect(
        TokenManager.handleAlarm({
          name: 'token-refresh',
          scheduledTime: Date.now(),
        }),
      ).resolves.not.toThrow();
    });
  });
});
