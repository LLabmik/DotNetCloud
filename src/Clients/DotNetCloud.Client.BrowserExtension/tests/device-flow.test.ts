// ─── Device Flow — Unit Tests ────────────────────────────────────────────────

import './setup';
import { initiateDeviceFlow, pollForToken } from '../src/auth/device-flow';
import { type DeviceFlowState } from '../src/api/types';
import { setMockFetchResponses, resetMockFetch, getLastFetchCall, clearFetchCalls } from './setup';
import { TokenManager } from '../src/auth/token-manager';

const SERVER_URL = 'https://mint22:5443';
const DEVICE_CODE = 'dc-abc123';
const USER_CODE = 'XYZ-123';
const VERIFICATION_URI = 'https://mint22:5443/connect/device';

function makeDeviceFlowResponse(): Record<string, unknown> {
  return {
    device_code: DEVICE_CODE,
    user_code: USER_CODE,
    verification_uri: VERIFICATION_URI,
    expires_in: 300,
    interval: 5,
  };
}

function makeTokenResponse(overrides?: Partial<Record<string, unknown>>): Record<string, unknown> {
  return {
    access_token: 'at-test123',
    refresh_token: 'rt-test456',
    expires_in: 3600,
    token_type: 'Bearer',
    ...overrides,
  };
}

describe('initiateDeviceFlow', () => {
  beforeEach(() => {
    resetMockFetch();
  });

  it('sends POST to /connect/device with correct body', async () => {
    setMockFetchResponses([
      {
        ok: true,
        status: 200,
        json: makeDeviceFlowResponse(),
      },
    ]);

    const result = await initiateDeviceFlow(SERVER_URL);

    const lastCall = getLastFetchCall();
    expect(lastCall).toBeDefined();
    expect(lastCall!.url).toContain('/connect/device');
    expect(lastCall!.body).toContain('client_id=dotnetcloud-browser-extension');
    expect(lastCall!.body).toContain('scope=openid');
    // URLSearchParams encodes colon as %3A
    expect(lastCall!.body).toContain('bookmarks%3Aread');
    expect(lastCall!.body).toContain('bookmarks%3Awrite');

    expect(result.deviceCode).toBe(DEVICE_CODE);
    expect(result.userCode).toBe(USER_CODE);
    expect(result.verificationUri).toBe(VERIFICATION_URI);
    expect(result.expiresIn).toBe(300);
    expect(result.interval).toBe(5);
  });

  it('strips trailing slash from server URL', async () => {
    setMockFetchResponses([
      {
        ok: true,
        status: 200,
        json: makeDeviceFlowResponse(),
      },
    ]);

    const result = await initiateDeviceFlow('https://mint22:5443/');
    expect(result.deviceCode).toBe(DEVICE_CODE);
  });

  it('throws on non-ok response', async () => {
    setMockFetchResponses([
      {
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: { error: 'invalid_request' },
      },
    ]);

    await expect(initiateDeviceFlow(SERVER_URL)).rejects.toThrow(
      'Device authorization failed: 400 Bad Request',
    );
  });
});

describe('pollForToken', () => {
  beforeEach(() => {
    resetMockFetch();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  const state: DeviceFlowState = {
    deviceCode: DEVICE_CODE,
    userCode: USER_CODE,
    verificationUri: VERIFICATION_URI,
    expiresIn: 300,
    interval: 0.01, // Very short interval for fast tests
  };

  it('polls and resolves on successful authorization', async () => {
    setMockFetchResponses([
      {
        ok: false,
        status: 400,
        json: { error: 'authorization_pending' },
      },
      {
        ok: true,
        status: 200,
        json: makeTokenResponse(),
      },
    ]);

    const pollPromise = pollForToken(SERVER_URL, state);

    // Advance past first poll (authorization_pending)
    await jest.advanceTimersByTimeAsync(15);
    // Advance past second poll (success)
    await jest.advanceTimersByTimeAsync(15);

    const tokenSet = await pollPromise;

    expect(tokenSet.accessToken).toBe('at-test123');
    expect(tokenSet.refreshToken).toBe('rt-test456');
    expect(tokenSet.serverUrl).toBe('https://mint22:5443');
    expect(typeof tokenSet.expiresAt).toBe('number');
    expect(tokenSet.expiresAt).toBeGreaterThan(Date.now());

    // Verify token was stored
    const storedTokens = await TokenManager.getTokens();
    expect(storedTokens?.accessToken).toBe('at-test123');
  });

  it('handles slow_down by increasing interval', async () => {
    setMockFetchResponses([
      {
        ok: false,
        status: 400,
        json: { error: 'slow_down' },
      },
      {
        ok: true,
        status: 200,
        json: makeTokenResponse(),
      },
    ]);

    const pollPromise = pollForToken(SERVER_URL, state);

    // First poll: slow_down (interval 0.01 + 5 = 5.01s)
    await jest.advanceTimersByTimeAsync(15);
    // Second poll: success (now at increased interval ~5.01s)
    await jest.advanceTimersByTimeAsync(5020);

    const tokenSet = await pollPromise;
    expect(tokenSet.accessToken).toBe('at-test123');
  });

  it('throws on access_denied', async () => {
    jest.useRealTimers();

    setMockFetchResponses([
      {
        ok: false,
        status: 400,
        json: { error: 'access_denied' },
      },
    ]);

    // Use a very short interval so the fetch fires quickly
    const fastState: DeviceFlowState = { ...state, interval: 0.001 };
    await expect(pollForToken(SERVER_URL, fastState)).rejects.toThrow('access_denied');
  }, 10_000);

  it('throws on expired_token', async () => {
    jest.useRealTimers();

    setMockFetchResponses([
      {
        ok: false,
        status: 400,
        json: { error: 'expired_token' },
      },
    ]);

    const fastState: DeviceFlowState = { ...state, interval: 0.001 };
    await expect(pollForToken(SERVER_URL, fastState)).rejects.toThrow('expired_token');
  }, 10_000);

  it('throws on unknown error', async () => {
    jest.useRealTimers();

    setMockFetchResponses([
      {
        ok: false,
        status: 400,
        json: { error: 'invalid_grant' },
      },
    ]);

    const fastState: DeviceFlowState = { ...state, interval: 0.001 };
    await expect(pollForToken(SERVER_URL, fastState)).rejects.toThrow(/token_poll_error/);
  }, 10_000);

  it('respects AbortSignal', async () => {
    const abortController = new AbortController();

    const pollPromise = pollForToken(SERVER_URL, state, abortController.signal);

    // Abort before first poll fires
    abortController.abort();

    await expect(pollPromise).rejects.toThrow('Polling aborted');
  });

  it('throws when deadline is exceeded', async () => {
    // Use real timers. Deadline is 100ms, interval is 20ms.
    // Each poll cycle: sleep(20ms) → fetch → loop. Deadline fires after ~5 cycles.
    jest.useRealTimers();

    // Set up enough authorization_pending responses to keep polling until deadline fires
    const pendingResponse = { ok: false, status: 400, json: { error: 'authorization_pending' } };
    setMockFetchResponses(Array(10).fill(pendingResponse));

    const expiredState: DeviceFlowState = {
      deviceCode: DEVICE_CODE,
      userCode: USER_CODE,
      verificationUri: VERIFICATION_URI,
      expiresIn: 0.1,  // 100ms expiry — deadline fires after ~5 polls (100/20)
      interval: 0.02,   // 20ms poll interval
    };

    await expect(pollForToken(SERVER_URL, expiredState)).rejects.toThrow('expired_token');
  }, 10_000);
});
