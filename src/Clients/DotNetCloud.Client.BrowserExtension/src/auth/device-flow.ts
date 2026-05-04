// ─── DotNetCloud Browser Extension — OAuth2 Device Flow ────────────────────
// Initiates the OAuth2 Device Authorization Grant flow and polls for the token.
// https://datatracker.ietf.org/doc/html/rfc8628

import { type DeviceFlowState, type TokenSet } from '../api/types';
import { TokenManager } from './token-manager';

const SCOPE = 'openid profile email offline_access bookmarks:read bookmarks:write';
const CLIENT_ID = 'dotnetcloud-browser-extension';

/**
 * Initiates the OAuth2 device authorization flow against the given server URL.
 * Returns a DeviceFlowState containing the user_code and verification URI.
 */
export async function initiateDeviceFlow(serverUrl: string): Promise<DeviceFlowState> {
  const baseUrl = serverUrl.replace(/\/+$/, '');

  const response = await fetch(`${baseUrl}/connect/device`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      client_id: CLIENT_ID,
      scope: SCOPE,
    }),
  });

  if (!response.ok) {
    throw new Error(`Device authorization failed: ${response.status} ${response.statusText}`);
  }

  const body = (await response.json()) as Record<string, unknown>;

  return {
    deviceCode: body['device_code'] as string,
    userCode: body['user_code'] as string,
    verificationUri: body['verification_uri'] as string,
    expiresIn: (body['expires_in'] as number) ?? 300,
    interval: (body['interval'] as number) ?? 5,
  };
}

/**
 * Polls the token endpoint at the specified interval until the user completes
 * authorization or the flow expires/cancels.
 *
 * Resolves with a TokenSet on success.
 * Throws on `access_denied`, `expired_token`, or network errors.
 */
export async function pollForToken(
  serverUrl: string,
  state: DeviceFlowState,
  signal?: AbortSignal,
): Promise<TokenSet> {
  const baseUrl = serverUrl.replace(/\/+$/, '');
  const deadline = Date.now() + state.expiresIn * 1000;
  let interval = state.interval;

  // eslint-disable-next-line no-constant-condition
  while (true) {
    if (signal?.aborted) {
      throw new DOMException('Polling aborted', 'AbortError');
    }

    if (Date.now() >= deadline) {
      throw new Error('expired_token');
    }

    await sleep(interval * 1000, signal);

    const response = await fetch(`${baseUrl}/connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'urn:ietf:params:oauth:grant-type:device_code',
        device_code: state.deviceCode,
        client_id: CLIENT_ID,
      }),
    });

    if (response.ok) {
      const body = (await response.json()) as Record<string, unknown>;

      const tokenSet: TokenSet = {
        accessToken: body['access_token'] as string,
        refreshToken: body['refresh_token'] as string,
        expiresAt: Date.now() + ((body['expires_in'] as number) ?? 3600) * 1000,
        serverUrl: baseUrl,
      };

      await TokenManager.storeTokens(tokenSet);
      return tokenSet;
    }

    const errorBody = (await response.json().catch(() => ({}))) as Record<string, unknown>;
    const error = errorBody['error'] as string | undefined;

    switch (error) {
      case 'authorization_pending':
        // Keep polling with the current interval
        break;
      case 'slow_down':
        // Increase interval by 5 seconds as per RFC 8628 §3.3
        interval += 5;
        break;
      case 'access_denied':
        throw new Error('access_denied');
      case 'expired_token':
        throw new Error('expired_token');
      default:
        if (response.status >= 400) {
          throw new Error(`token_poll_error: ${error ?? response.statusText}`);
        }
    }
  }
}

function sleep(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener('abort', () => {
      clearTimeout(timer);
      reject(new DOMException('Polling aborted', 'AbortError'));
    }, { once: true });
  });
}
