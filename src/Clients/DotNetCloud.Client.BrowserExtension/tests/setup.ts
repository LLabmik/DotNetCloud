// ─── Test Setup — Chrome API Mocks ──────────────────────────────────────────
// Provides minimal chrome.* mocks for unit tests in node environment.

import { type TokenSet } from '../src/api/types';

// ─── Storage Mock ────────────────────────────────────────────────────────────

class StorageAreaMock {
  private store: Record<string, unknown> = {};

  async get(keys?: string | string[] | Record<string, unknown> | null): Promise<Record<string, unknown>> {
    if (keys === undefined || keys === null) {
      return { ...this.store };
    }
    if (typeof keys === 'string') {
      const val = this.store[keys];
      return val !== undefined ? { [keys]: val } : {};
    }
    if (Array.isArray(keys)) {
      const result: Record<string, unknown> = {};
      for (const key of keys) {
        if (key in this.store) {
          result[key] = this.store[key];
        }
      }
      return result;
    }
    // Record<string, unknown> — return matching keys, defaulting to their values
    const result: Record<string, unknown> = {};
    for (const [key, defaultValue] of Object.entries(keys)) {
      result[key] = key in this.store ? this.store[key] : defaultValue;
    }
    return result;
  }

  async set(items: Record<string, unknown>): Promise<void> {
    Object.assign(this.store, items);
  }

  async remove(keys: string | string[]): Promise<void> {
    const keyList = Array.isArray(keys) ? keys : [keys];
    for (const key of keyList) {
      delete this.store[key];
    }
  }

  async clear(): Promise<void> {
    this.store = {};
  }

  /** Test helper: seed data directly */
  _seed(data: Record<string, unknown>): void {
    Object.assign(this.store, data);
  }
}

// ─── Alarms Mock ─────────────────────────────────────────────────────────────

interface AlarmMock {
  name: string;
  delayInMinutes?: number;
  periodInMinutes?: number;
  scheduledTime?: number;
}

class AlarmsMock {
  private alarms: Map<string, AlarmMock> = new Map();
  private listeners: Array<(alarm: AlarmMock) => void> = [];

  async create(name: string, options: { delayInMinutes?: number; periodInMinutes?: number }): Promise<void> {
    const alarm: AlarmMock = {
      name,
      ...options,
      scheduledTime: Date.now() + (options.delayInMinutes ?? 0) * 60_000,
    };
    this.alarms.set(name, alarm);
  }

  async clear(name: string): Promise<boolean> {
    return this.alarms.delete(name);
  }

  async clearAll(): Promise<boolean> {
    const count = this.alarms.size;
    this.alarms.clear();
    return count > 0;
  }

  get(name: string): AlarmMock | undefined {
    return this.alarms.get(name);
  }

  onAlarm = {
    _listeners: new Set<(alarm: AlarmMock) => void>(),
    addListener(cb: (alarm: AlarmMock) => void): void {
      this._listeners.add(cb);
    },
    removeListener(cb: (alarm: AlarmMock) => void): void {
      this._listeners.delete(cb);
    },
    /** Test helper: fire an alarm by name */
    _fire(name: string): void {
      // No-op in mock; tests call handleAlarm directly
      void name;
    },
  };
}

// ─── Tabs Mock ───────────────────────────────────────────────────────────────

class TabsMock {
  async create(_options: { url: string }): Promise<void> {
    // no-op in tests
  }
  async query(_options: { active: boolean; currentWindow: boolean }): Promise<Array<{ url?: string; title?: string }>> {
    return [{ url: 'https://example.com/test', title: 'Test Page' }];
  }
}

// ─── Bookmarks Mock ──────────────────────────────────────────────────────────

interface BookmarkChangeCallbacks {
  onCreated: Array<(id: string, node: chrome.bookmarks.BookmarkTreeNode) => void>;
  onRemoved: Array<(id: string, removeInfo: chrome.bookmarks.BookmarkRemoveInfo) => void>;
  onChanged: Array<(id: string, changeInfo: chrome.bookmarks.BookmarkChangeInfo) => void>;
  onMoved: Array<(id: string, moveInfo: chrome.bookmarks.BookmarkMoveInfo) => void>;
}

let bookmarkIdCounter = 100;

class BookmarksMock {
  private nodes: Map<string, chrome.bookmarks.BookmarkTreeNode> = new Map();
  private callbacks: BookmarkChangeCallbacks = {
    onCreated: [],
    onRemoved: [],
    onChanged: [],
    onMoved: [],
  };

  onCreated = {
    addListener: (cb: (id: string, node: chrome.bookmarks.BookmarkTreeNode) => void): void => {
      this.callbacks.onCreated.push(cb);
    },
    removeListener: (cb: (id: string, node: chrome.bookmarks.BookmarkTreeNode) => void): void => {
      this.callbacks.onCreated = this.callbacks.onCreated.filter((f) => f !== cb);
    },
  };

  onRemoved = {
    addListener: (cb: (id: string, removeInfo: chrome.bookmarks.BookmarkRemoveInfo) => void): void => {
      this.callbacks.onRemoved.push(cb);
    },
    removeListener: (cb: (id: string, removeInfo: chrome.bookmarks.BookmarkRemoveInfo) => void): void => {
      this.callbacks.onRemoved = this.callbacks.onRemoved.filter((f) => f !== cb);
    },
  };

  onChanged = {
    addListener: (cb: (id: string, changeInfo: chrome.bookmarks.BookmarkChangeInfo) => void): void => {
      this.callbacks.onChanged.push(cb);
    },
    removeListener: (cb: (id: string, changeInfo: chrome.bookmarks.BookmarkChangeInfo) => void): void => {
      this.callbacks.onChanged = this.callbacks.onChanged.filter((f) => f !== cb);
    },
  };

  onMoved = {
    addListener: (cb: (id: string, moveInfo: chrome.bookmarks.BookmarkMoveInfo) => void): void => {
      this.callbacks.onMoved.push(cb);
    },
    removeListener: (cb: (id: string, moveInfo: chrome.bookmarks.BookmarkMoveInfo) => void): void => {
      this.callbacks.onMoved = this.callbacks.onMoved.filter((f) => f !== cb);
    },
  };

  /** Test helper: seed the bookmark tree with initial data */
  _seed(nodes: chrome.bookmarks.BookmarkTreeNode[]): void {
    const walk = (list: chrome.bookmarks.BookmarkTreeNode[]): void => {
      for (const node of list) {
        if (node.id) this.nodes.set(node.id, node);
        if (node.children) walk(node.children);
      }
    };
    walk(nodes);
  }

  /** Test helper: reset the bookmark tree */
  _reset(): void {
    this.nodes.clear();
    this.callbacks.onCreated = [];
    this.callbacks.onRemoved = [];
    this.callbacks.onChanged = [];
    this.callbacks.onMoved = [];
    bookmarkIdCounter = 100;
  }

  async getTree(): Promise<chrome.bookmarks.BookmarkTreeNode[]> {
    const root = this.nodes.get('0');
    if (!root) return [];

    // Recursively expand children with full node data
    const expandNode = (node: chrome.bookmarks.BookmarkTreeNode): chrome.bookmarks.BookmarkTreeNode => {
      const full = this.nodes.get(node.id);
      if (!full) return { ...node };
      const expanded: chrome.bookmarks.BookmarkTreeNode = {
        id: full.id,
        parentId: full.parentId,
        title: full.title,
        dateAdded: full.dateAdded,
        dateGroupModified: full.dateGroupModified,
      };
      if (full.url) expanded.url = full.url;
      if (full.children && full.children.length > 0) {
        expanded.children = full.children.map((c) => expandNode(c));
      }
      return expanded;
    };

    return [expandNode(root)];
  }

  async getChildren(id: string): Promise<chrome.bookmarks.BookmarkTreeNode[]> {
    const node = this.nodes.get(id);
    if (!node || !node.children) return [];
    // Return direct children (not recursively expanded)
    return node.children.map((child) => {
      const full = this.nodes.get(child.id);
      // Return a copy without children to simulate Chrome API behavior
      if (full) {
        return { ...full, children: undefined } as chrome.bookmarks.BookmarkTreeNode;
      }
      return child;
    });
  }

  async get(id: string): Promise<chrome.bookmarks.BookmarkTreeNode[]> {
    const node = this.nodes.get(id);
    if (!node) {
      throw new Error(`Bookmark node not found: ${id}`);
    }
    return [{ ...node, children: undefined } as chrome.bookmarks.BookmarkTreeNode];
  }

  async create(details: {
    parentId?: string;
    title: string;
    url?: string;
  }): Promise<chrome.bookmarks.BookmarkTreeNode> {
    const nodeId = String(++bookmarkIdCounter);
    const parentId = details.parentId ?? '1';
    const node: chrome.bookmarks.BookmarkTreeNode = {
      id: nodeId,
      parentId,
      title: details.title,
      ...(details.url ? { url: details.url } : {}),
      dateAdded: Date.now(),
      dateGroupModified: Date.now(),
    };

    this.nodes.set(nodeId, node);

    // Add to parent's children list
    const parent = this.nodes.get(parentId);
    if (parent) {
      if (!parent.children) parent.children = [];
      parent.children.push({ id: nodeId, title: node.title } as chrome.bookmarks.BookmarkTreeNode);
    }

    // Fire onCreated
    for (const cb of this.callbacks.onCreated) {
      cb(nodeId, { ...node });
    }

    return { ...node };
  }

  async update(id: string, changes: { title?: string; url?: string }): Promise<chrome.bookmarks.BookmarkTreeNode> {
    const node = this.nodes.get(id);
    if (!node) throw new Error(`Bookmark node not found: ${id}`);

    if (changes.title !== undefined) node.title = changes.title;
    if (changes.url !== undefined) node.url = changes.url;

    const changeInfo: chrome.bookmarks.BookmarkChangeInfo = {
      title: node.title,
      url: node.url ?? '',
    };

    for (const cb of this.callbacks.onChanged) {
      cb(id, changeInfo);
    }

    return { ...node, children: undefined } as chrome.bookmarks.BookmarkTreeNode;
  }

  async move(id: string, destination: { parentId: string }): Promise<chrome.bookmarks.BookmarkTreeNode> {
    const node = this.nodes.get(id);
    if (!node) throw new Error(`Bookmark node not found: ${id}`);

    const oldParentId = node.parentId;
    const oldIndex = 0;

    // Remove from old parent
    if (oldParentId) {
      const oldParent = this.nodes.get(oldParentId);
      if (oldParent?.children) {
        oldParent.children = oldParent.children.filter((c) => c.id !== id);
      }
    }

    // Add to new parent
    node.parentId = destination.parentId;
    const newParent = this.nodes.get(destination.parentId);
    if (newParent) {
      if (!newParent.children) newParent.children = [];
      newParent.children.push({ id, title: node.title } as chrome.bookmarks.BookmarkTreeNode);
    }

    const moveInfo: chrome.bookmarks.BookmarkMoveInfo = {
      parentId: destination.parentId,
      index: 0,
      oldParentId: oldParentId ?? '',
      oldIndex,
    };

    for (const cb of this.callbacks.onMoved) {
      cb(id, moveInfo);
    }

    return { ...node, children: undefined } as chrome.bookmarks.BookmarkTreeNode;
  }

  async remove(id: string): Promise<void> {
    const node = this.nodes.get(id);
    if (!node) throw new Error(`Bookmark node not found: ${id}`);

    // Remove from parent
    if (node.parentId) {
      const parent = this.nodes.get(node.parentId);
      if (parent?.children) {
        parent.children = parent.children.filter((c) => c.id !== id);
      }
    }

    this.nodes.delete(id);

    const removeInfo: chrome.bookmarks.BookmarkRemoveInfo = {
      parentId: node.parentId ?? '',
      index: 0,
      node: { ...node },
    };

    for (const cb of this.callbacks.onRemoved) {
      cb(id, removeInfo);
    }
  }

  async removeTree(id: string): Promise<void> {
    const node = this.nodes.get(id);
    if (!node) throw new Error(`Bookmark node not found: ${id}`);

    // Recursively collect all descendant IDs
    const toRemove: string[] = [];
    const collect = (n: chrome.bookmarks.BookmarkTreeNode): void => {
      toRemove.push(n.id);
      if (n.children) {
        for (const child of n.children) {
          const fullChild = this.nodes.get(child.id);
          if (fullChild) collect(fullChild);
        }
      }
    };
    collect(node);

    for (const removeId of toRemove) {
      this.nodes.delete(removeId);
    }

    // Remove from parent
    if (node.parentId) {
      const parent = this.nodes.get(node.parentId);
      if (parent?.children) {
        parent.children = parent.children.filter((c) => c.id !== id);
      }
    }

    const removeInfo: chrome.bookmarks.BookmarkRemoveInfo = {
      parentId: node.parentId ?? '',
      index: 0,
      node: { ...node, children: undefined },
    };

    for (const cb of this.callbacks.onRemoved) {
      cb(id, removeInfo);
    }
  }
}

// ─── Global chrome mock ──────────────────────────────────────────────────────

const storageMock = new StorageAreaMock();
const alarmsMock = new AlarmsMock();
const tabsMock = new TabsMock();
const bookmarksMock = new BookmarksMock();

(globalThis as Record<string, unknown>).chrome = {
  storage: {
    local: storageMock,
  },
  alarms: alarmsMock,
  tabs: tabsMock,
  bookmarks: bookmarksMock,
  runtime: {
    onInstalled: {
      addListener: () => {},
    },
    onStartup: {
      addListener: () => {},
    },
  },
};

// ─── Mock fetch for device flow tests ────────────────────────────────────────

let mockFetchResponses: Array<{
  ok: boolean;
  status: number;
  statusText: string;
  json: () => Promise<unknown>;
}> = [];

export interface FetchCall {
  url: string;
  method: string | undefined;
  body: string | undefined;
}

let recordedCalls: FetchCall[] = [];

export function getLastFetchCall(): FetchCall | undefined {
  return recordedCalls[recordedCalls.length - 1];
}

export function clearFetchCalls(): void {
  recordedCalls = [];
}

export function setMockFetchResponses(
  responses: Array<{
    ok: boolean;
    status: number;
    statusText?: string;
    json: unknown;
  }>,
): void {
  mockFetchResponses = responses.map((r) => ({
    ok: r.ok,
    status: r.status,
    statusText: r.statusText ?? 'OK',
    json: async () => r.json,
  }));
}

const originalFetch = globalThis.fetch;
globalThis.fetch = async (
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<Response> => {
  recordedCalls.push({
    url: input instanceof Request ? input.url : String(input),
    method: init?.method ?? 'GET',
    body: init?.body?.toString(),
  });

  const response = mockFetchResponses.shift();
  if (!response) {
    throw new Error('Unexpected fetch call — no mock response set up');
  }
  return response as unknown as Response;
};

// Re-export for test cleanup
export function resetMockFetch(): void {
  mockFetchResponses = [];
  recordedCalls = [];
}

export { storageMock, alarmsMock, bookmarksMock };
