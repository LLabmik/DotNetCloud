// ─── DotNetCloud Browser Extension — Auth Token Attachment ─────────────────
// Reads token from chrome.storage.local, attaches Authorization header.
// Delegates refresh to TokenManager before each request when needed.

import { TokenManager } from '../auth/token-manager';

export interface AuthHeaders {
  Authorization: string;
}

/**
 * Returns the Authorization header with a valid Bearer token.
 * Automatically refreshes the token if it's close to expiring.
 * Returns null if the user is not logged in.
 */
export async function getAuthHeaders(): Promise<AuthHeaders | null> {
  const token = await TokenManager.getAccessToken();
  if (!token) {
    return null;
  }
  return { Authorization: `Bearer ${token}` };
}

/**
 * Checks whether the user has valid credentials stored.
 */
export async function isAuthenticated(): Promise<boolean> {
  const headers = await getAuthHeaders();
  return headers !== null;
}
