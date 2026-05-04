// ─── DotNetCloud Browser Extension — Save Panel ─────────────────────────────
// Auto-fills URL/title from the active tab, provides folder picker, tags, notes,
// and saves/updates bookmarks to the DotNetCloud server.

import { ApiClient } from '../../api/client';
import { type BookmarkFolder, type BookmarkItem } from '../../api/types';
import { mappingStore } from '../../sync/mapping-store';

// ─── Constants ─────────────────────────────────────────────────────────────

const LAST_FOLDER_KEY = 'lastUsedFolder';
const DEBOUNCE_MS = 300;

// ─── SavePanel ─────────────────────────────────────────────────────────────

export class SavePanel {
  private container: HTMLElement;
  private api: ApiClient;
  private folders: BookmarkFolder[] = [];
  private existingBookmark: BookmarkItem | null = null;
  private destroyed = false;

  constructor(container: HTMLElement, api: ApiClient) {
    this.container = container;
    this.api = api;
  }

  async render(): Promise<void> {
    // Get active tab info
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    const tab = tabs[0];
    const currentUrl = tab?.url ?? '';
    const currentTitle = tab?.title ?? '';

    this.container.innerHTML = `
      <div class="save-panel">
        <div class="save-field">
          <label for="save-url">URL</label>
          <input type="url" id="save-url" class="save-input" value="${this.escapeHtml(currentUrl)}" placeholder="https://example.com" />
        </div>
        <div class="save-field">
          <label for="save-title">Title</label>
          <input type="text" id="save-title" class="save-input" value="${this.escapeHtml(currentTitle)}" placeholder="Bookmark title" />
        </div>
        <div class="save-field">
          <label for="save-folder">Folder</label>
          <select id="save-folder" class="save-select">
            <option value="">Loading folders...</option>
          </select>
        </div>
        <div class="save-field">
          <label for="save-tags">Tags</label>
          <div class="tags-input-container" id="tags-container">
            <input type="text" id="save-tags" class="tags-input" placeholder="Type a tag and press Enter or comma" />
            <div class="tags-chips" id="tags-chips"></div>
          </div>
        </div>
        <div class="save-field">
          <button type="button" class="save-notes-toggle" id="notes-toggle">
            ▶ Add notes
          </button>
          <textarea id="save-notes" class="save-notes-textarea hidden" placeholder="Optional notes about this bookmark..." rows="3"></textarea>
        </div>
        <div class="save-actions">
          <button type="button" class="btn-primary" id="save-btn">Save Bookmark</button>
          <div class="save-toast hidden" id="save-toast">✓ Saved</div>
        </div>
      </div>
    `;

    // Load folders
    await this.loadFolders();

    // Check if URL is already bookmarked
    await this.checkExistingBookmark(currentUrl);

    // Bind events
    this.bindEvents();
  }

  destroy(): void {
    this.destroyed = true;
    this.container.innerHTML = '';
  }

  // ─── Folder Loading ────────────────────────────────────────────────────

  private async loadFolders(): Promise<void> {
    try {
      this.folders = await this.api.getFolders();
      const select = document.getElementById('save-folder') as HTMLSelectElement;
      if (!select) return;

      // Remember last-used folder
      const lastFolder = await this.getLastUsedFolder();

      // Build indented options from flat folder list
      const sorted = this.buildFolderTree(this.folders);
      select.innerHTML = '<option value="">No folder (root level)</option>';
      this.renderFolderOptions(select, sorted, 0);

      // Select last used or first folder
      if (lastFolder && this.folders.some((f) => f.id === lastFolder)) {
        select.value = lastFolder;
      }
    } catch (err) {
      console.error('SavePanel: failed to load folders:', err);
    }
  }

  private buildFolderTree(folders: BookmarkFolder[]): BookmarkFolder[] {
    // Topological sort: parents before children
    const sorted: BookmarkFolder[] = [];
    const added = new Set<string>();
    const byParent = new Map<string | null, BookmarkFolder[]>();

    for (const f of folders) {
      const key = f.parentId ?? '__root__';
      if (!byParent.has(key)) byParent.set(key, []);
      byParent.get(key)!.push(f);
    }

    const addChildren = (parentId: string | null, depth: number): void => {
      const children = byParent.get(parentId ?? '__root__') ?? [];
      for (const child of children) {
        if (added.has(child.id)) continue;
        added.add(child.id);
        (child as BookmarkFolder & { _depth?: number })._depth = depth;
        sorted.push(child);
        addChildren(child.id, depth + 1);
      }
    };

    addChildren(null, 0);
    return sorted;
  }

  private renderFolderOptions(
    select: HTMLSelectElement,
    folders: BookmarkFolder[],
    _depth: number,
  ): void {
    for (const folder of folders) {
      const depth = (folder as BookmarkFolder & { _depth?: number })._depth ?? 0;
      const indent = '\u00A0\u00A0'.repeat(depth);
      const option = document.createElement('option');
      option.value = folder.id;
      option.textContent = `${indent}${folder.name}`;
      select.appendChild(option);
    }
  }

  private async getLastUsedFolder(): Promise<string | null> {
    const result = await chrome.storage.local.get(LAST_FOLDER_KEY);
    return (result[LAST_FOLDER_KEY] as string) ?? null;
  }

  private async setLastUsedFolder(folderId: string): Promise<void> {
    await chrome.storage.local.set({ [LAST_FOLDER_KEY]: folderId });
  }

  // ─── Existing Bookmark Detection ───────────────────────────────────────

  private async checkExistingBookmark(url: string): Promise<void> {
    if (!url) return;

    try {
      // Search for existing bookmark by URL
      const results = await this.api.searchBookmarks(url, 0, 5);
      const exact = results.find((b) => b.url === url);
      if (exact) {
        this.existingBookmark = exact;
        const saveBtn = document.getElementById('save-btn') as HTMLButtonElement;
        const urlInput = document.getElementById('save-url') as HTMLInputElement;
        const titleInput = document.getElementById('save-title') as HTMLInputElement;
        const notesTextarea = document.getElementById('save-notes') as HTMLTextAreaElement;

        if (saveBtn) saveBtn.textContent = 'Update Bookmark';
        if (urlInput) urlInput.value = exact.url;
        if (titleInput) titleInput.value = exact.title;

        if (notesTextarea && exact.notes) {
          notesTextarea.value = exact.notes;
          this.showNotes();
        }

        // Load tags
        if (exact.tags.length > 0) {
          this.setTags(exact.tags);
        }

        // Pre-select folder
        if (exact.folderId) {
          const select = document.getElementById('save-folder') as HTMLSelectElement;
          if (select) select.value = exact.folderId;
        }
      }
    } catch (err) {
      // Search may fail if not authenticated or network error
      console.error('SavePanel: failed to check existing bookmark:', err);
    }
  }

  // ─── Tags Handling ─────────────────────────────────────────────────────

  private tags: string[] = [];

  private setTags(tags: string[]): void {
    this.tags = tags;
    this.renderChips();
  }

  private renderChips(): void {
    const chipsContainer = document.getElementById('tags-chips');
    if (!chipsContainer) return;

    chipsContainer.innerHTML = this.tags
      .map(
        (tag) =>
          `<span class="tag-chip">${this.escapeHtml(tag)}<button type="button" class="tag-chip-remove" data-tag="${this.escapeHtml(tag)}">&times;</button></span>`,
      )
      .join('');

    // Bind remove events
    chipsContainer.querySelectorAll('.tag-chip-remove').forEach((btn) => {
      btn.addEventListener('click', (e) => {
        const tag = (e.currentTarget as HTMLElement).dataset['tag'];
        if (tag) {
          this.tags = this.tags.filter((t) => t !== tag);
          this.renderChips();
        }
      });
    });
  }

  // ─── Notes Toggle ──────────────────────────────────────────────────────

  private showNotes(): void {
    const textarea = document.getElementById('save-notes') as HTMLTextAreaElement;
    const toggle = document.getElementById('notes-toggle') as HTMLButtonElement;
    if (textarea) textarea.classList.remove('hidden');
    if (toggle) toggle.textContent = '▼ Hide notes';
  }

  // ─── Event Binding ─────────────────────────────────────────────────────

  private bindEvents(): void {
    if (this.destroyed) return;

    // Tags input: split on comma or Enter
    const tagsInput = document.getElementById('save-tags') as HTMLInputElement;
    tagsInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ',') {
        e.preventDefault();
        const value = tagsInput.value.trim();
        if (value && !this.tags.includes(value)) {
          this.tags.push(value);
          this.renderChips();
        }
        tagsInput.value = '';
      }
    });

    // Tags input: comma paste handling
    tagsInput?.addEventListener('blur', () => {
      const value = tagsInput.value.trim();
      if (value && !this.tags.includes(value)) {
        this.tags.push(value);
        this.renderChips();
      }
      tagsInput.value = '';
    });

    // Notes toggle
    const notesToggle = document.getElementById('notes-toggle') as HTMLButtonElement;
    notesToggle?.addEventListener('click', () => {
      const textarea = document.getElementById('save-notes') as HTMLTextAreaElement;
      if (textarea) {
        const isHidden = textarea.classList.toggle('hidden');
        notesToggle.textContent = isHidden ? '▶ Add notes' : '▼ Hide notes';
      }
    });

    // Save button
    const saveBtn = document.getElementById('save-btn') as HTMLButtonElement;
    saveBtn?.addEventListener('click', () => this.handleSave());
  }

  // ─── Save Handler ──────────────────────────────────────────────────────

  private async handleSave(): Promise<void> {
    const urlInput = document.getElementById('save-url') as HTMLInputElement;
    const titleInput = document.getElementById('save-title') as HTMLInputElement;
    const folderSelect = document.getElementById('save-folder') as HTMLSelectElement;
    const notesTextarea = document.getElementById('save-notes') as HTMLTextAreaElement;
    const saveBtn = document.getElementById('save-btn') as HTMLButtonElement;
    const toast = document.getElementById('save-toast');

    if (!urlInput || !titleInput) return;

    const url = urlInput.value.trim();
    const title = titleInput.value.trim();
    const folderId = folderSelect?.value || null;
    const notes = notesTextarea?.value.trim() || undefined;

    if (!url || !title) {
      // Highlight empty fields
      if (!url) urlInput.style.borderColor = 'var(--color-error)';
      if (!title) titleInput.style.borderColor = 'var(--color-error)';
      return;
    }

    // Disable button during save
    if (saveBtn) saveBtn.disabled = true;

    try {
      if (this.existingBookmark) {
        // Update existing bookmark
        const updateParams: Record<string, unknown> = {};
        updateParams['url'] = url;
        updateParams['title'] = title;
        if (folderId !== undefined) updateParams['folderId'] = folderId;
        if (this.tags.length > 0) updateParams['tags'] = this.tags;
        if (notes !== undefined) updateParams['notes'] = notes;
        await this.api.updateBookmark(
          this.existingBookmark.id,
          updateParams as unknown as import('../../api/types').UpdateBookmarkRequest,
        );
      } else {
        // Create new bookmark
        const createParams: Record<string, unknown> = {};
        createParams['url'] = url;
        createParams['title'] = title;
        if (folderId !== undefined) createParams['folderId'] = folderId;
        if (this.tags.length > 0) createParams['tags'] = this.tags;
        if (notes !== undefined) createParams['notes'] = notes;
        const created = await this.api.createBookmark(
          createParams as unknown as import('../../api/types').CreateBookmarkRequest,
        );

        // Store mapping if browser node exists (shouldn't for new saves, but safe)
        // Trigger preview fetch (fire-and-forget)
        this.api.triggerPreview(created.id).catch(() => {});
      }

      // Remember last-used folder
      if (folderId) {
        await this.setLastUsedFolder(folderId);
      }

      // Show success toast
      if (toast) {
        toast.classList.remove('hidden');
        toast.textContent = this.existingBookmark ? '✓ Updated' : '✓ Saved';
        setTimeout(() => {
          window.close();
        }, 800);
      }
    } catch (err) {
      console.error('SavePanel: save failed:', err);
      if (saveBtn) {
        saveBtn.disabled = false;
        saveBtn.textContent = this.existingBookmark ? 'Update Failed — Retry' : 'Save Failed — Retry';
      }
    }
  }

  // ─── Utility ───────────────────────────────────────────────────────────

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}
