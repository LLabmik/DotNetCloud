// ─── DotNetCloud Browser Extension — ID Mapping Store ─────────────────────
// Persists bidirectional mappings between browser bookmark node IDs and
// server-side UUIDs using chrome.storage.local.
// Also stores the last successful sync cursor.

const STORAGE_KEY = 'idMap';

interface IdMapData {
  bookmarks: Record<string, string>;      // browserNodeId → serverId
  folders: Record<string, string>;         // browserNodeId → serverId
  reverseBookmarks: Record<string, string>; // serverId → browserNodeId
  reverseFolders: Record<string, string>;   // serverId → browserNodeId
  syncCursor: string | null;
}

type MapType = 'bookmark' | 'folder';

async function getData(): Promise<IdMapData> {
  const result = await chrome.storage.local.get(STORAGE_KEY);
  const data = result[STORAGE_KEY] as Partial<IdMapData> | undefined;

  return {
    bookmarks: data?.bookmarks ?? {},
    folders: data?.folders ?? {},
    reverseBookmarks: data?.reverseBookmarks ?? {},
    reverseFolders: data?.reverseFolders ?? {},
    syncCursor: data?.syncCursor ?? null,
  };
}

async function saveData(data: IdMapData): Promise<void> {
  await chrome.storage.local.set({ [STORAGE_KEY]: data });
}

export const mappingStore = {
  async getServerId(browserNodeId: string, type: MapType): Promise<string | null> {
    const data = await getData();
    const map = type === 'bookmark' ? data.bookmarks : data.folders;
    return map[browserNodeId] ?? null;
  },

  async getBrowserNodeId(serverId: string, type: MapType): Promise<string | null> {
    const data = await getData();
    const map = type === 'bookmark' ? data.reverseBookmarks : data.reverseFolders;
    return map[serverId] ?? null;
  },

  async setMapping(browserNodeId: string, serverId: string, type: MapType): Promise<void> {
    const data = await getData();

    if (type === 'bookmark') {
      data.bookmarks[browserNodeId] = serverId;
      data.reverseBookmarks[serverId] = browserNodeId;
    } else {
      data.folders[browserNodeId] = serverId;
      data.reverseFolders[serverId] = browserNodeId;
    }

    await saveData(data);
  },

  async removeMapping(browserNodeId: string, type: MapType): Promise<void> {
    const data = await getData();
    const map = type === 'bookmark' ? data.bookmarks : data.folders;
    const reverseMap = type === 'bookmark' ? data.reverseBookmarks : data.reverseFolders;

    const serverId = map[browserNodeId];
    if (serverId) {
      delete reverseMap[serverId];
    }
    delete map[browserNodeId];

    await saveData(data);
  },

  async getCursor(): Promise<string | null> {
    const data = await getData();
    return data.syncCursor;
  },

  async setCursor(cursor: string): Promise<void> {
    const data = await getData();
    data.syncCursor = cursor;
    await saveData(data);
  },

  async clearAll(): Promise<void> {
    await saveData({
      bookmarks: {},
      folders: {},
      reverseBookmarks: {},
      reverseFolders: {},
      syncCursor: null,
    });
  },
};
