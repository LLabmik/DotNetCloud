// ─── DotNetCloud Browser Extension — Search Panel ──────────────────────────
// Debounced bookmark search with result display showing favicon, title, URL,
// and folder path. Shows recent bookmarks when query is empty.

import { ApiClient } from '../../api/client';
import { type BookmarkItem, type BookmarkFolder } from '../../api/types';

// ─── Constants ─────────────────────────────────────────────────────────────

const DEBOUNCE_MS = 300;
const RESULT_LIMIT = 20;

// ─── SearchPanel ───────────────────────────────────────────────────────────

export class SearchPanel {
  private container: HTMLElement;
  private api: ApiClient;
  private destroyed = false;

  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private currentQuery = '';
  private allFolders: BookmarkFolder[] = [];
  private foldersLoaded = false;

  constructor(container: HTMLElement, api: ApiClient) {
    this.container = container;
    this.api = api;
  }

  async render(): Promise<void> {
    this.container.innerHTML = `
      <div class="search-panel">
        <div class="search-input-container">
          <span class="search-icon">🔍</span>
          <input
            type="text"
            class="search-input"
            id="search-input"
            placeholder="Search bookmarks..."
            autocomplete="off"
            spellcheck="false"
          />
        </div>
        <div class="search-results" id="search-results">
          <div class="search-loading"><div class="spinner"></div></div>
        </div>
      </div>
    `;

    // Load folders for path display
    this.loadFolders();

    // Show recent bookmarks on initial load
    await this.showRecentBookmarks();

    this.bindEvents();
  }

  destroy(): void {
    this.destroyed = true;
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
    this.container.innerHTML = '';
  }

  // ─── Folder Loading ────────────────────────────────────────────────────

  private async loadFolders(): Promise<void> {
    try {
      this.allFolders = await this.api.getFolders();
      this.foldersLoaded = true;
    } catch {
      // Non-critical; folder paths just won't show
      this.foldersLoaded = true;
    }
  }

  // ─── Recent Bookmarks ──────────────────────────────────────────────────

  private async showRecentBookmarks(): Promise<void> {
    const resultsContainer = document.getElementById('search-results');
    if (!resultsContainer) return;

    try {
      const recent = await this.api.getBookmarks({
        skip: 0,
        take: 10,
      });

      if (this.destroyed) return;

      if (recent.length === 0) {
        resultsContainer.innerHTML = `
          <div class="search-empty">
            <p class="search-empty-text">No bookmarks yet</p>
            <p class="search-empty-hint">Save a bookmark using the Save tab.</p>
          </div>
        `;
        return;
      }

      let html = '<div class="search-recent-header">Recently Added</div>';
      html += '<div class="search-results-list">';

      for (const bm of recent) {
        html += this.renderResultItem(bm);
      }

      html += '</div>';
      resultsContainer.innerHTML = html;
      this.bindResultClicks(resultsContainer);
    } catch {
      if (!this.destroyed) {
        resultsContainer.innerHTML = '';
      }
    }
  }

  // ─── Search ────────────────────────────────────────────────────────────

  private async performSearch(query: string): Promise<void> {
    const resultsContainer = document.getElementById('search-results');
    if (!resultsContainer) return;

    if (!query.trim()) {
      await this.showRecentBookmarks();
      return;
    }

    resultsContainer.innerHTML = `
      <div class="search-loading"><div class="spinner"></div></div>
    `;

    try {
      const results = await this.api.searchBookmarks(
        query.trim(),
        0,
        RESULT_LIMIT,
      );

      if (this.destroyed) return;

      if (results.length === 0) {
        resultsContainer.innerHTML = `
          <div class="search-empty">
            <p class="search-empty-text">No results found</p>
            <p class="search-empty-hint">Try a shorter search or check your DotNetCloud server.</p>
          </div>
        `;
        return;
      }

      let html = `<div class="search-results-list">`;
      for (const bm of results) {
        html += this.renderResultItem(bm);
      }
      html += '</div>';

      if (results.length < RESULT_LIMIT) {
        html += '<div class="search-end">No more results</div>';
      }

      resultsContainer.innerHTML = html;
      this.bindResultClicks(resultsContainer);
    } catch (err) {
      console.error('SearchPanel: search failed:', err);
      if (!this.destroyed) {
        resultsContainer.innerHTML = `
          <div class="search-empty">
            <p class="search-empty-text">Search failed</p>
            <p class="search-empty-hint">Check your connection and try again.</p>
          </div>
        `;
      }
    }
  }

  // ─── Result Rendering ──────────────────────────────────────────────────

  private renderResultItem(bm: BookmarkItem): string {
    let domain = '';
    try {
      domain = new URL(bm.url).hostname;
    } catch {
      domain = bm.url;
    }

    const faviconUrl = `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=16`;
    const folderPath = this.getFolderPath(bm.folderId);

    return `
      <div class="search-result" data-url="${this.escapeHtml(bm.url)}">
        <img class="search-result-favicon" src="${faviconUrl}" alt=""
          onerror="this.style.display='none'" />
        <div class="search-result-info">
          <span class="search-result-title">${this.escapeHtml(bm.title || domain)}</span>
          <span class="search-result-url">${this.escapeHtml(domain)}</span>
          ${folderPath ? `<span class="search-result-folder">in: ${this.escapeHtml(folderPath)}</span>` : ''}
        </div>
      </div>
    `;
  }

  private getFolderPath(folderId: string | null): string {
    if (!folderId || !this.foldersLoaded) return '';

    const parts: string[] = [];
    let currentId: string | null = folderId;

    while (currentId) {
      const folder = this.allFolders.find((f) => f.id === currentId);
      if (!folder) break;
      parts.unshift(folder.name);
      currentId = folder.parentId;
    }

    return parts.join(' / ');
  }

  private bindResultClicks(container: HTMLElement): void {
    container.querySelectorAll('.search-result').forEach((el) => {
      el.addEventListener('click', () => {
        const url = (el as HTMLElement).dataset['url'];
        if (url) {
          chrome.tabs.create({ url });
          window.close();
        }
      });
    });
  }

  // ─── Event Binding ─────────────────────────────────────────────────────

  private bindEvents(): void {
    if (this.destroyed) return;

    const input = document.getElementById('search-input') as HTMLInputElement;
    if (!input) return;

    input.addEventListener('input', () => {
      const query = input.value;

      if (this.debounceTimer) {
        clearTimeout(this.debounceTimer);
      }

      this.debounceTimer = setTimeout(() => {
        if (query !== this.currentQuery) {
          this.currentQuery = query;
          this.performSearch(query);
        }
      }, DEBOUNCE_MS);
    });

    // Focus the input when the panel becomes visible
    setTimeout(() => input.focus(), 50);
  }

  // ─── Utility ───────────────────────────────────────────────────────────

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}
