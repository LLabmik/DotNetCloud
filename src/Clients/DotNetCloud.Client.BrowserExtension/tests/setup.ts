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
}

// ─── Global chrome mock ──────────────────────────────────────────────────────

const storageMock = new StorageAreaMock();
const alarmsMock = new AlarmsMock();
const tabsMock = new TabsMock();

(globalThis as Record<string, unknown>).chrome = {
  storage: {
    local: storageMock,
  },
  alarms: alarmsMock,
  tabs: tabsMock,
  runtime: {
    onInstalled: {
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

export { storageMock, alarmsMock };
