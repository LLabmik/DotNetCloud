// ─── DotNetCloud Browser Extension — Popup Entry Point ─────────────────────
// Checks authentication state and renders the appropriate screen.
// Auth screen → Device flow initiation
// Main UI → Save/Browse/Search panels with tab switching and sync status

import { TokenManager } from '../auth/token-manager';
import { ApiClient } from '../api/client';
import { SavePanel } from './components/SavePanel';
import { BrowsePanel } from './components/BrowsePanel';
import { SearchPanel } from './components/SearchPanel';
import { mappingStore } from '../sync/mapping-store';

// ─── State ─────────────────────────────────────────────────────────────────

let currentPanel: SavePanel | BrowsePanel | SearchPanel | null = null;
let apiClient: ApiClient | null = null;
let statusRefreshInterval: ReturnType<typeof setInterval> | null = null;

// ─── Initialization ────────────────────────────────────────────────────────

async function init(): Promise<void> {
  const tokens = await TokenManager.getTokens();

  if (!tokens) {
    renderAuthScreen();
  } else {
    apiClient = new ApiClient(tokens.serverUrl);
    renderMainUI(tokens.serverUrl);
  }
}

// ─── Auth Screen ───────────────────────────────────────────────────────────

function renderAuthScreen(): void {
  const app = document.getElementById('app');
  if (!app) return;

  app.innerHTML = `
    <div class="auth-screen">
      <div class="auth-logo">
        <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
          <rect width="48" height="48" rx="8" fill="#2563eb"/>
          <text x="24" y="32" text-anchor="middle" fill="white" font-size="24" font-weight="bold">D</text>
        </svg>
      </div>
      <h1>DotNetCloud Bookmarks</h1>
      <p class="auth-subtitle">Sync your bookmarks with your server</p>
      <div class="auth-form">
        <label for="server-url">Server URL</label>
        <input type="url" id="server-url" placeholder="https://mint22:5443" />
        <button id="connect-btn" class="btn-primary">Connect to Server</button>
      </div>
      <p class="auth-hint">Opens a browser tab to complete sign in.</p>
    </div>
  `;

  document.getElementById('connect-btn')?.addEventListener('click', onConnect);
}

async function onConnect(): Promise<void> {
  const serverUrl = (document.getElementById('server-url') as HTMLInputElement)?.value.trim();
  if (!serverUrl) return;

  const app = document.getElementById('app');
  if (!app) return;

  app.innerHTML = `
    <div class="auth-screen">
      <div class="spinner"></div>
      <h2>Waiting for authorization...</h2>
      <p class="user-code" id="user-code-display">Loading...</p>
      <p class="auth-hint">A browser tab has been opened to complete sign in.</p>
    </div>
  `;

  try {
    const { initiateDeviceFlow, pollForToken } = await import('../auth/device-flow');
    const state = await initiateDeviceFlow(serverUrl);

    const userCodeDisplay = document.getElementById('user-code-display');
    if (userCodeDisplay) {
      userCodeDisplay.textContent = state.userCode;
    }

    // Open verification URI in a new tab
    const verificationUrl = `${state.verificationUri}?user_code=${state.userCode}`;
    await chrome.tabs.create({ url: verificationUrl });

    // Poll until user completes authorization
    await pollForToken(serverUrl, state);

    // Success — set API client and reload popup UI
    apiClient = new ApiClient(serverUrl);
    renderMainUI(serverUrl);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Unknown error';
    app.innerHTML = `
      <div class="auth-screen">
        <h2>Connection Failed</h2>
        <p class="error-message">${message}</p>
        <button id="retry-btn" class="btn-primary">Try Again</button>
      </div>
    `;
    document.getElementById('retry-btn')?.addEventListener('click', () => {
      renderAuthScreen();
    });
  }
}

// ─── Main UI ───────────────────────────────────────────────────────────────

function renderMainUI(serverUrl: string): void {
  const app = document.getElementById('app');
  if (!app) return;

  let hostname = '';
  try {
    hostname = new URL(serverUrl).hostname;
  } catch {
    hostname = serverUrl;
  }

  app.innerHTML = `
    <div class="main-ui">
      <header class="main-header">
        <span class="header-title">Bookmarks</span>
        <span class="header-server">${escapeHtml(hostname)}</span>
      </header>
      <nav class="tab-nav">
        <button class="tab active" data-tab="save">Save</button>
        <button class="tab" data-tab="browse">Browse</button>
        <button class="tab" data-tab="search">Search</button>
      </nav>
      <div class="tab-content" id="tab-content"></div>
      <footer class="status-footer" id="status-footer">
        <span class="status-dot synced" id="status-dot"></span>
        <span id="status-text">Syncing...</span>
      </footer>
    </div>
  `;

  // Activate default tab (Save)
  activateTab('save');

  // Bind tab switching
  document.querySelectorAll('.tab').forEach((tab) => {
    tab.addEventListener('click', () => {
      const tabName = (tab as HTMLElement).dataset['tab'];
      if (tabName) {
        document.querySelectorAll('.tab').forEach((t) => t.classList.remove('active'));
        tab.classList.add('active');
        activateTab(tabName);
      }
    });
  });

  // Start status footer refresh
  startStatusFooter();

  // Bind footer click → show sync details
  const footer = document.getElementById('status-footer');
  footer?.addEventListener('click', showSyncDetails);
}

// ─── Tab Activation ────────────────────────────────────────────────────────

function activateTab(tabName: string): void {
  const content = document.getElementById('tab-content');
  if (!content || !apiClient) return;

  // Destroy current panel
  if (currentPanel) {
    currentPanel.destroy();
    currentPanel = null;
  }

  // Create and render new panel
  switch (tabName) {
    case 'save':
      currentPanel = new SavePanel(content, apiClient);
      break;
    case 'browse':
      currentPanel = new BrowsePanel(content, apiClient);
      break;
    case 'search':
      currentPanel = new SearchPanel(content, apiClient);
      break;
    default:
      content.innerHTML = '<p class="placeholder-text">Unknown tab</p>';
      return;
  }

  currentPanel.render().catch((err) => {
    console.error(`Popup: ${tabName} panel render failed:`, err);
    content.innerHTML = `
      <div class="panel-error">
        <p>Failed to load ${tabName} panel.</p>
        <button type="button" class="btn-primary" id="panel-retry">Retry</button>
      </div>
    `;
    document.getElementById('panel-retry')?.addEventListener('click', () => {
      activateTab(tabName);
    });
  });
}

// ─── Sync Status Footer ────────────────────────────────────────────────────

function startStatusFooter(): void {
  // Clear any existing interval
  if (statusRefreshInterval) {
    clearInterval(statusRefreshInterval);
  }

  // Update immediately
  updateStatusFooter();

  // Refresh every 15 seconds
  statusRefreshInterval = setInterval(updateStatusFooter, 15_000);
}

async function updateStatusFooter(): Promise<void> {
  const dot = document.getElementById('status-dot');
  const text = document.getElementById('status-text');
  if (!dot || !text) return;

  try {
    const cursor = await mappingStore.getCursor();
    const tokens = await TokenManager.getTokens();

    if (!tokens) {
      dot.className = 'status-dot error';
      text.textContent = '⚠ Login required';
      return;
    }

    if (!cursor) {
      dot.className = 'status-dot warning';
      text.textContent = '↻ Initial sync in progress...';
      return;
    }

    const lastSync = new Date(cursor).getTime();
    const elapsed = Date.now() - lastSync;
    const minutesAgo = Math.floor(elapsed / 60_000);

    if (elapsed < 60_000) {
      dot.className = 'status-dot synced';
      text.textContent = '● Synced just now';
    } else if (elapsed < 3_600_000) {
      dot.className = 'status-dot synced';
      text.textContent = `● Synced ${minutesAgo} min ago`;
    } else {
      dot.className = 'status-dot warning';
      const hoursAgo = Math.floor(elapsed / 3_600_000);
      text.textContent = `● Synced ${hoursAgo} hours ago`;
    }
  } catch {
    dot.className = 'status-dot offline';
    text.textContent = '⚠ Offline — will sync when connected';
  }
}

// ─── Sync Details Overlay ──────────────────────────────────────────────────

async function showSyncDetails(): Promise<void> {
  const existing = document.getElementById('sync-details-overlay');
  if (existing) {
    existing.remove();
    return;
  }

  const cursor = await mappingStore.getCursor();
  const lastSync = cursor ? new Date(cursor).toLocaleString() : 'Never';

  let itemCount = 0;
  try {
    const data = await chrome.storage.local.get('idMap');
    const idMap = data['idMap'] as Record<string, unknown> | undefined;
    if (idMap) {
      const bookmarks = (idMap['bookmarks'] as Record<string, string>) ?? {};
      itemCount = Object.keys(bookmarks).length;
    }
  } catch {
    // Unable to read count
  }

  const overlay = document.createElement('div');
  overlay.className = 'sync-details-overlay';
  overlay.id = 'sync-details-overlay';
  overlay.innerHTML = `
    <div class="sync-details-content">
      <div class="sync-details-row">
        <span class="sync-details-label">Last sync</span>
        <span class="sync-details-value">${escapeHtml(lastSync)}</span>
      </div>
      <div class="sync-details-row">
        <span class="sync-details-label">Bookmarks synced</span>
        <span class="sync-details-value">${itemCount}</span>
      </div>
      <button type="button" class="btn-primary sync-now-btn" id="sync-now-btn">Sync Now</button>
    </div>
  `;

  document.body.appendChild(overlay);

  // Close on click outside
  setTimeout(() => {
    const closeHandler = (e: MouseEvent): void => {
      if (!overlay.contains(e.target as Node)) {
        overlay.remove();
        document.removeEventListener('click', closeHandler);
      }
    };
    document.addEventListener('click', closeHandler);
  }, 0);

  // Sync now button
  document.getElementById('sync-now-btn')?.addEventListener('click', async () => {
    const btn = document.getElementById('sync-now-btn') as HTMLButtonElement;
    if (btn) {
      btn.disabled = true;
      btn.textContent = 'Syncing...';
    }

    try {
      const { runPullCycle } = await import('../sync/pull-sync');
      await runPullCycle();
      overlay.remove();
      updateStatusFooter();
    } catch (err) {
      console.error('Popup: manual sync failed:', err);
      if (btn) {
        btn.disabled = false;
        btn.textContent = 'Sync Failed — Retry';
      }
    }
  });
}

// ─── Utility ───────────────────────────────────────────────────────────────

function escapeHtml(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// ─── Entry Point ───────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', init);
