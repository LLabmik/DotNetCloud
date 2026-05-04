// ─── Auth API Attachment — Unit Tests ────────────────────────────────────────

import './setup';
import { getAuthHeaders, isAuthenticated } from '../src/api/auth';
import { TokenManager } from '../src/auth/token-manager';
import { setMockFetchResponses, resetMockFetch, storageMock } from './setup';

describe('getAuthHeaders', () => {
  beforeEach(() => {
    resetMockFetch();
    storageMock.clear();
  });

  it('returns Authorization header when authenticated', async () => {
    await TokenManager.storeTokens({
      accessToken: 'at-test',
      refreshToken: 'rt-test',
      expiresAt: Date.now() + 3600_000,
      serverUrl: 'https://mint22:5443',
    });

    const headers = await getAuthHeaders();
    expect(headers).toEqual({ Authorization: 'Bearer at-test' });
  });

  it('returns null when not authenticated', async () => {
    const headers = await getAuthHeaders();
    expect(headers).toBeNull();
  });

  it('returns null after tokens cleared', async () => {
    await TokenManager.storeTokens({
      accessToken: 'at-test',
      refreshToken: 'rt-test',
      expiresAt: Date.now() + 3600_000,
      serverUrl: 'https://mint22:5443',
    });
    await TokenManager.clearTokens();

    const headers = await getAuthHeaders();
    expect(headers).toBeNull();
  });
});

describe('isAuthenticated', () => {
  beforeEach(() => {
    resetMockFetch();
    storageMock.clear();
  });

  it('returns true when authenticated', async () => {
    await TokenManager.storeTokens({
      accessToken: 'at-test',
      refreshToken: 'rt-test',
      expiresAt: Date.now() + 3600_000,
      serverUrl: 'https://mint22:5443',
    });

    const authed = await isAuthenticated();
    expect(authed).toBe(true);
  });

  it('returns false when not authenticated', async () => {
    const authed = await isAuthenticated();
    expect(authed).toBe(false);
  });
});
