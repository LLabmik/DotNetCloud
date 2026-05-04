// ─── DotNetCloud Browser Extension — API Client ────────────────────────────
// Typed fetch wrapper for the DotNetCloud Bookmarks REST API.
// All methods accept an optional AbortSignal for cancellation.
// Throws ApiError on non-2xx responses.

import {
  type BookmarkItem,
  type BookmarkFolder,
  type CreateBookmarkRequest,
  type UpdateBookmarkRequest,
  type CreateFolderRequest,
  type UpdateFolderRequest,
  type SyncChangesResponse,
  type BatchRequest,
  type BatchResponse,
  ApiError,
} from './types';
import { TokenManager } from '../auth/token-manager';

export class ApiClient {
  private readonly baseUrl: string;

  constructor(baseUrl: string) {
    // Strip trailing slash for consistent URL building
    this.baseUrl = baseUrl.replace(/\/+$/, '');
  }

  // ─── Helpers ────────────────────────────────────────────────────────────

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    signal?: AbortSignal,
  ): Promise<T> {
    const token = await TokenManager.getAccessToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : null,
      signal: signal ?? null,
    });

    if (!response.ok) {
      let code = 'unknown';
      let message: string | undefined;

      try {
        const errorBody = (await response.json()) as Record<string, unknown>;
        code = (errorBody['code'] as string) ?? 'unknown';
        message = errorBody['message'] as string | undefined;
      } catch {
        // Response body not JSON; use status text
        message = response.statusText || undefined;
      }

      throw new ApiError(response.status, code, message);
    }

    // 204 No Content
    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  // ─── Bookmarks ──────────────────────────────────────────────────────────

  async getBookmarks(
    params?: { folderId?: string; skip?: number; take?: number },
    signal?: AbortSignal,
  ): Promise<BookmarkItem[]> {
    const query = new URLSearchParams();
    if (params?.folderId) query.set('folderId', params.folderId);
    if (params?.skip !== undefined) query.set('skip', String(params.skip));
    if (params?.take !== undefined) query.set('take', String(params.take));
    const qs = query.toString();
    return this.request<BookmarkItem[]>('GET', `/api/v1/bookmarks${qs ? `?${qs}` : ''}`, undefined, signal);
  }

  async getBookmark(id: string, signal?: AbortSignal): Promise<BookmarkItem> {
    return this.request<BookmarkItem>('GET', `/api/v1/bookmarks/${encodeURIComponent(id)}`, undefined, signal);
  }

  async createBookmark(req: CreateBookmarkRequest, signal?: AbortSignal): Promise<BookmarkItem> {
    return this.request<BookmarkItem>('POST', '/api/v1/bookmarks', req, signal);
  }

  async updateBookmark(id: string, req: UpdateBookmarkRequest, signal?: AbortSignal): Promise<BookmarkItem> {
    return this.request<BookmarkItem>('PUT', `/api/v1/bookmarks/${encodeURIComponent(id)}`, req, signal);
  }

  async deleteBookmark(id: string, signal?: AbortSignal): Promise<void> {
    return this.request<void>('DELETE', `/api/v1/bookmarks/${encodeURIComponent(id)}`, undefined, signal);
  }

  async searchBookmarks(
    q: string,
    skip?: number,
    take?: number,
    signal?: AbortSignal,
  ): Promise<BookmarkItem[]> {
    const query = new URLSearchParams({ q });
    if (skip !== undefined) query.set('skip', String(skip));
    if (take !== undefined) query.set('take', String(take));
    return this.request<BookmarkItem[]>('GET', `/api/v1/bookmarks/search?${query.toString()}`, undefined, signal);
  }

  // ─── Folders ────────────────────────────────────────────────────────────

  async getFolders(parentId?: string, signal?: AbortSignal): Promise<BookmarkFolder[]> {
    const query = parentId ? `?parentId=${encodeURIComponent(parentId)}` : '';
    return this.request<BookmarkFolder[]>('GET', `/api/v1/bookmarks/folders${query}`, undefined, signal);
  }

  async createFolder(req: CreateFolderRequest, signal?: AbortSignal): Promise<BookmarkFolder> {
    return this.request<BookmarkFolder>('POST', '/api/v1/bookmarks/folders', req, signal);
  }

  async updateFolder(id: string, req: UpdateFolderRequest, signal?: AbortSignal): Promise<BookmarkFolder> {
    return this.request<BookmarkFolder>('PUT', `/api/v1/bookmarks/folders/${encodeURIComponent(id)}`, req, signal);
  }

  async deleteFolder(id: string, signal?: AbortSignal): Promise<void> {
    return this.request<void>('DELETE', `/api/v1/bookmarks/folders/${encodeURIComponent(id)}`, undefined, signal);
  }

  // ─── Sync ───────────────────────────────────────────────────────────────

  async getSyncChanges(since: string, limit?: number, signal?: AbortSignal): Promise<SyncChangesResponse> {
    const query = new URLSearchParams({ since });
    if (limit !== undefined) query.set('limit', String(limit));
    return this.request<SyncChangesResponse>('GET', `/api/v1/bookmarks/sync/changes?${query.toString()}`, undefined, signal);
  }

  async batch(req: BatchRequest, signal?: AbortSignal): Promise<BatchResponse> {
    return this.request<BatchResponse>('POST', '/api/v1/bookmarks/batch', req, signal);
  }

  // ─── Preview ────────────────────────────────────────────────────────────

  async triggerPreview(bookmarkId: string, signal?: AbortSignal): Promise<void> {
    return this.request<void>('POST', `/api/v1/bookmarks/${encodeURIComponent(bookmarkId)}/preview`, undefined, signal);
  }
}
