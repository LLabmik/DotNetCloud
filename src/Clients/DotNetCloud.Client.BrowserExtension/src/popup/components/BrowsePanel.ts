// ─── DotNetCloud Browser Extension — Browse Panel ──────────────────────────
// Folder tree navigation, bookmark list with favicons, infinite scroll.
// Users navigate the bookmark tree and open bookmarks in new tabs.

import { ApiClient } from '../../api/client';
import { type BookmarkItem, type BookmarkFolder } from '../../api/types';

// ─── Constants ─────────────────────────────────────────────────────────────

const PAGE_SIZE = 20;

// ─── BrowsePanel ───────────────────────────────────────────────────────────

export class BrowsePanel {
  private container: HTMLElement;
  private api: ApiClient;
  private destroyed = false;

  // Navigation state
  private currentFolderId: string | null = null;
  private breadcrumbs: Array<{ id: string | null; name: string }> = [
    { id: null, name: 'Bookmarks' },
  ];

  // Pagination state
  private bookmarks: BookmarkItem[] = [];
  private folders: BookmarkFolder[] = [];
  private skip = 0;
  private hasMore = true;
  private isLoading = false;

  constructor(container: HTMLElement, api: ApiClient) {
    this.container = container;
    this.api = api;
  }

  async render(): Promise<void> {
    this.container.innerHTML = `
      <div class="browse-panel">
        <div class="browse-header">
          <nav class="browse-breadcrumb" id="browse-breadcrumb"></nav>
          <button type="button" class="browse-refresh" id="browse-refresh" title="Refresh">
            ↻
          </button>
        </div>
        <div class="browse-content" id="browse-content">
          <div class="browse-loading"><div class="spinner"></div></div>
        </div>
      </div>
    `;

    this.bindEvents();
    await this.loadCurrentFolder();
  }

  destroy(): void {
    this.destroyed = true;
    this.container.innerHTML = '';
  }

  // ─── Navigation ────────────────────────────────────────────────────────

  private async navigateToFolder(folderId: string | null, folderName: string): Promise<void> {
    // Add to breadcrumbs (or update if going back)
    if (this.currentFolderId !== null || folderId !== null) {
      const existingIndex = this.breadcrumbs.findIndex(
        (b) => b.id === folderId,
      );
      if (existingIndex >= 0) {
        this.breadcrumbs = this.breadcrumbs.slice(0, existingIndex + 1);
      } else {
        this.breadcrumbs.push({ id: folderId, name: folderName });
      }
    }

    this.currentFolderId = folderId;
    this.resetPagination();
    await this.loadCurrentFolder();
  }

  private async navigateToBreadcrumb(index: number): Promise<void> {
    this.breadcrumbs = this.breadcrumbs.slice(0, index + 1);
    const target = this.breadcrumbs[index];
    if (!target) return;
    this.currentFolderId = target.id;
    this.resetPagination();
    await this.loadCurrentFolder();
  }

  private resetPagination(): void {
    this.bookmarks = [];
    this.folders = [];
    this.skip = 0;
    this.hasMore = true;
  }

  // ─── Data Loading ──────────────────────────────────────────────────────

  private async loadCurrentFolder(): Promise<void> {
    if (this.isLoading) return;
    this.isLoading = true;

    const content = document.getElementById('browse-content');
    if (!content) return;

    try {
      // Load sub-folders and bookmarks in parallel
      const [subFolders, bookmarkItems] = await Promise.all([
        this.currentFolderId
          ? this.api.getFolders(this.currentFolderId)
          : Promise.resolve([] as BookmarkFolder[]),
        this.getBookmarksWithFolder(this.currentFolderId, this.skip),
      ]);

      if (this.destroyed) return;

      this.folders = subFolders;
      this.bookmarks.push(...bookmarkItems);
      this.hasMore = bookmarkItems.length >= PAGE_SIZE;
      this.skip += bookmarkItems.length;

      this.renderBreadcrumb();
      this.renderContent(content);
    } catch (err) {
      console.error('BrowsePanel: failed to load:', err);
      if (!this.destroyed) {
        content.innerHTML = `
          <div class="browse-empty">
            <p>Failed to load bookmarks.</p>
            <button type="button" class="btn-primary" id="browse-retry">Retry</button>
          </div>
        `;
        document.getElementById('browse-retry')?.addEventListener('click', () => {
          this.loadCurrentFolder();
        });
      }
    } finally {
      this.isLoading = false;
    }
  }

  private async loadMore(): Promise<void> {
    if (!this.hasMore || this.isLoading) return;
    this.isLoading = true;

    try {
      const bookmarkItems = await this.getBookmarksWithFolder(this.currentFolderId, this.skip);

      if (this.destroyed) return;

      this.bookmarks.push(...bookmarkItems);
      this.hasMore = bookmarkItems.length >= PAGE_SIZE;
      this.skip += bookmarkItems.length;

      const content = document.getElementById('browse-content');
      if (content) {
        this.renderBookmarkList(content);
      }
    } catch (err) {
      console.error('BrowsePanel: failed to load more:', err);
    } finally {
      this.isLoading = false;
    }
  }

  // ─── Rendering ─────────────────────────────────────────────────────────

  private renderBreadcrumb(): void {
    const nav = document.getElementById('browse-breadcrumb');
    if (!nav) return;

    nav.innerHTML = this.breadcrumbs
      .map((crumb, i) => {
        const isLast = i === this.breadcrumbs.length - 1;
        if (isLast) {
          return `<span class="breadcrumb-current">${this.escapeHtml(crumb.name)}</span>`;
        }
        return `<button type="button" class="breadcrumb-link" data-index="${i}">${this.escapeHtml(crumb.name)}</button>`;
      })
      .join(' <span class="breadcrumb-sep">&rsaquo;</span> ');

    // Bind breadcrumb click events
    nav.querySelectorAll('.breadcrumb-link').forEach((el) => {
      el.addEventListener('click', (e) => {
        const index = parseInt((e.currentTarget as HTMLElement).dataset['index'] ?? '0', 10);
        this.navigateToBreadcrumb(index);
      });
    });
  }

  private renderContent(content: HTMLElement): void {
    const hasFolders = this.folders.length > 0;
    const hasBookmarks = this.bookmarks.length > 0;

    if (!hasFolders && !hasBookmarks) {
      content.innerHTML = `
        <div class="browse-empty">
          <p class="browse-empty-text">This folder is empty.</p>
          <p class="browse-empty-hint">Save a bookmark to get started.</p>
        </div>
      `;
      return;
    }

    let html = '<div class="browse-list" id="browse-list">';

    // Render folders
    for (const folder of this.folders) {
      html += `
        <div class="browse-item browse-folder" data-folder-id="${this.escapeHtml(folder.id)}">
          <span class="browse-item-icon browse-folder-icon">📁</span>
          <div class="browse-item-info">
            <span class="browse-item-title">${this.escapeHtml(folder.name)}</span>
          </div>
          <span class="browse-item-arrow">&rarr;</span>
        </div>
      `;
    }

    // Separator between folders and bookmarks
    if (hasFolders && hasBookmarks) {
      html += '<hr class="browse-separator" />';
    }

    // Render bookmarks
    for (const bm of this.bookmarks) {
      html += this.renderBookmarkItem(bm);
    }

    html += '</div>';

    // Loading indicator for infinite scroll
    if (this.hasMore) {
      html += '<div class="browse-more-loading" id="browse-more"><div class="spinner"></div></div>';
    }

    content.innerHTML = html;

    // Bind folder click events
    content.querySelectorAll('.browse-folder').forEach((el) => {
      el.addEventListener('click', (e) => {
        const folderId = (e.currentTarget as HTMLElement).dataset['folderId'];
        const folderName = (e.currentTarget as HTMLElement).querySelector('.browse-item-title')?.textContent ?? 'Folder';
        if (folderId) {
          this.navigateToFolder(folderId, folderName);
        }
      });
    });

    // Bind bookmark click events
    content.querySelectorAll('.browse-bookmark').forEach((el) => {
      el.addEventListener('click', (e) => {
        const url = (e.currentTarget as HTMLElement).dataset['url'];
        if (url) {
          chrome.tabs.create({ url });
          window.close();
        }
      });
    });

    // Bind bookmark context menu (right-click)
    content.querySelectorAll('.browse-bookmark').forEach((el) => {
      el.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        this.showContextMenu(e as MouseEvent, el as HTMLElement);
      });
    });

    // Infinite scroll
    content.addEventListener('scroll', () => {
      if (
        content.scrollHeight - content.scrollTop - content.clientHeight < 100 &&
        this.hasMore &&
        !this.isLoading
      ) {
        this.loadMore();
      }
    });
  }

  private renderBookmarkItem(bm: BookmarkItem): string {
    let domain = '';
    try {
      domain = new URL(bm.url).hostname;
    } catch {
      domain = bm.url;
    }

    const faviconUrl = `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=16`;

    return `
      <div class="browse-item browse-bookmark" data-url="${this.escapeHtml(bm.url)}" data-id="${this.escapeHtml(bm.id)}">
        <img class="browse-item-favicon" src="${faviconUrl}" alt=""
          onerror="this.style.display='none'" />
        <div class="browse-item-info">
          <span class="browse-item-title">${this.escapeHtml(bm.title || domain)}</span>
          <span class="browse-item-url">${this.escapeHtml(domain)}</span>
        </div>
      </div>
    `;
  }

  private renderBookmarkList(container: HTMLElement): void {
    // Append new bookmarks for infinite scroll
    const list = document.getElementById('browse-list');
    if (!list) return;

    for (const bm of this.bookmarks.slice(-PAGE_SIZE)) {
      const temp = document.createElement('div');
      temp.innerHTML = this.renderBookmarkItem(bm);
      const el = temp.firstElementChild as HTMLElement;
      if (el) {
        el.addEventListener('click', () => {
          chrome.tabs.create({ url: bm.url });
          window.close();
        });
        el.addEventListener('contextmenu', (e) => {
          e.preventDefault();
          this.showContextMenu(e as MouseEvent, el);
        });
        list.appendChild(el);
      }
    }

    // Hide "more loading" indicator
    const more = document.getElementById('browse-more');
    if (!this.hasMore && more) {
      more.remove();
    }
  }

  // ─── Context Menu ──────────────────────────────────────────────────────

  private showContextMenu(event: MouseEvent, element: HTMLElement): void {
    const url = element.dataset['url'] ?? '';
    const id = element.dataset['id'] ?? '';

    // Remove any existing context menu
    this.removeContextMenu();

    const menu = document.createElement('div');
    menu.className = 'browse-context-menu';
    menu.innerHTML = `
      <button type="button" class="context-item" data-action="open">Open in New Tab</button>
      <button type="button" class="context-item" data-action="copy">Copy URL</button>
      <button type="button" class="context-item context-item-danger" data-action="delete">Delete</button>
    `;

    // Position menu
    const viewportW = 380;
    const x = Math.min(event.clientX, viewportW - 160);
    const y = event.clientY;
    menu.style.left = `${Math.max(0, x)}px`;
    menu.style.top = `${y}px`;

    document.body.appendChild(menu);

    // Bind actions
    menu.querySelectorAll('.context-item').forEach((btn) => {
      btn.addEventListener('click', async () => {
        const action = (btn as HTMLElement).dataset['action'];
        switch (action) {
          case 'open':
            chrome.tabs.create({ url });
            window.close();
            break;
          case 'copy':
            try {
              await navigator.clipboard.writeText(url);
            } catch {
              // Fallback for extension context
              const textarea = document.createElement('textarea');
              textarea.value = url;
              document.body.appendChild(textarea);
              textarea.select();
              document.execCommand('copy');
              document.body.removeChild(textarea);
            }
            this.removeContextMenu();
            break;
          case 'delete':
            try {
              await this.api.deleteBookmark(id);
              element.remove();
              // Check if list is now empty
              const list = document.getElementById('browse-list');
              if (list && list.children.length === 0) {
                const content = document.getElementById('browse-content');
                if (content) {
                  content.innerHTML = `
                    <div class="browse-empty">
                      <p class="browse-empty-text">This folder is empty.</p>
                    </div>
                  `;
                }
              }
            } catch (err) {
              console.error('BrowsePanel: delete failed:', err);
            }
            this.removeContextMenu();
            break;
        }
      });
    });

    // Close on click outside
    const closeHandler = (e: MouseEvent): void => {
      if (!menu.contains(e.target as Node)) {
        this.removeContextMenu();
        document.removeEventListener('click', closeHandler);
      }
    };
    setTimeout(() => {
      document.addEventListener('click', closeHandler);
    }, 0);
  }

  private removeContextMenu(): void {
    document.querySelectorAll('.browse-context-menu').forEach((m) => m.remove());
  }

  // ─── Event Binding ─────────────────────────────────────────────────────

  private bindEvents(): void {
    const refreshBtn = document.getElementById('browse-refresh');
    refreshBtn?.addEventListener('click', () => {
      this.resetPagination();
      this.loadCurrentFolder();
    });
  }

  // ─── Bookmarks Fetch Helper (avoids exactOptionalPropertyTypes issues) ──

  private async getBookmarksWithFolder(
    folderId: string | null,
    skip: number,
  ): Promise<BookmarkItem[]> {
    if (folderId) {
      return this.api.getBookmarks({ folderId, skip, take: PAGE_SIZE });
    }
    return this.api.getBookmarks({ skip, take: PAGE_SIZE });
  }

  // ─── Utility ───────────────────────────────────────────────────────────

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}
