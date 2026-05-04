// ─── DotNetCloud Browser Extension — Popup Entry Point ─────────────────────
// Checks authentication state and renders the appropriate screen.
// Auth screen → Device flow initiation
// Main UI → Save/Browse/Search panels (Phase 5)

import { TokenManager } from '../auth/token-manager';

async function init(): Promise<void> {
  const tokens = await TokenManager.getTokens();

  if (!tokens) {
    renderAuthScreen();
  } else {
    renderMainUI(tokens.serverUrl);
  }
}

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

    // Success — reload popup UI
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
    document.getElementById('retry-btn')?.addEventListener('click', renderAuthScreen);
  }
}

function renderMainUI(serverUrl: string): void {
  const app = document.getElementById('app');
  if (!app) return;

  // Phase 5 will provide full panel UI. For now, show a placeholder.
  app.innerHTML = `
    <div class="main-ui">
      <header class="main-header">
        <span class="header-title">Bookmarks</span>
        <span class="header-server">${new URL(serverUrl).hostname}</span>
      </header>
      <nav class="tab-nav">
        <button class="tab active" data-tab="save">Save</button>
        <button class="tab" data-tab="browse">Browse</button>
        <button class="tab" data-tab="search">Search</button>
      </nav>
      <div class="tab-content" id="tab-content">
        <p class="placeholder-text">Extension connected. Full UI coming in Phase 5.</p>
      </div>
      <footer class="status-footer">
        <span class="status-dot synced"></span>
        <span>Synced just now</span>
      </footer>
    </div>
  `;
}

document.addEventListener('DOMContentLoaded', init);
