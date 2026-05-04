// ─── DotNetCloud Browser Extension — API DTOs ───────────────────────────────
// TypeScript interfaces matching the DotNetCloud Bookmarks server DTOs.
// These are used by api/client.ts for typed request/response handling.

// ─── Bookmark Types ──────────────────────────────────────────────────────────

export interface BookmarkItem {
  id: string;
  folderId: string | null;
  url: string;
  title: string;
  description?: string;
  tags: string[];
  notes?: string;
  createdAt: string; // ISO-8601
  updatedAt: string;
}

export interface CreateBookmarkRequest {
  folderId?: string | null;
  url: string;
  title: string;
  description?: string;
  tags?: string[];
  notes?: string;
}

export interface UpdateBookmarkRequest {
  folderId?: string | null;
  url?: string;
  title?: string;
  description?: string;
  tags?: string[];
  notes?: string;
}

// ─── Folder Types ───────────────────────────────────────────────────────────

export interface BookmarkFolder {
  id: string;
  parentId: string | null;
  name: string;
  updatedAt: string;
}

export interface CreateFolderRequest {
  parentId?: string | null;
  name: string;
}

export interface UpdateFolderRequest {
  parentId?: string | null;
  name?: string;
}

// ─── Sync Types ─────────────────────────────────────────────────────────────

export interface SyncChangesResponse {
  items: BookmarkItem[];
  deletedIds: string[];
  folders: BookmarkFolder[];
  deletedFolderIds: string[];
  nextCursor: string;
  hasMore: boolean;
}

// ─── Batch Types ────────────────────────────────────────────────────────────

export interface BatchRequest {
  creates?: CreateBookmarkRequest[];
  updates?: UpdateBookmarkRequest[];
  deletes?: string[];
  folderCreates?: CreateFolderRequest[];
  folderDeletes?: string[];
}

export interface BatchResult {
  operation: 'create' | 'update' | 'delete' | 'folderCreate' | 'folderDelete';
  clientRef?: string;
  serverId?: string;
  id?: string;
  success: boolean;
  error?: string;
}

export interface BatchResponse {
  results: BatchResult[];
}

// ─── Auth Types ─────────────────────────────────────────────────────────────

export interface TokenSet {
  accessToken: string;
  refreshToken: string;
  expiresAt: number; // Unix ms timestamp
  serverUrl: string;
}

export interface DeviceFlowState {
  deviceCode: string;
  userCode: string;
  verificationUri: string;
  expiresIn: number;
  interval: number;
}

// ─── API Error ──────────────────────────────────────────────────────────────

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message?: string,
  ) {
    super(message ?? `API Error ${status}: ${code}`);
    this.name = 'ApiError';
  }
}
