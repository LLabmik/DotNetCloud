/**
 * WYSIWYG rich-text editor for the chat MessageComposer.
 * Uses contenteditable with formatting commands and Markdown conversion.
 * Invoked via JS interop from MessageComposer.razor.cs.
 */

window.wysiwygEditor = {
    _editors: {},

    /**
     * Initialises the WYSIWYG editor on a contenteditable element.
     * @param {string} elementId - The id of the contenteditable div
     * @param {object} dotNetRef - .NET object reference for callbacks
     */
    init: function (elementId, dotNetRef, channelId) {
        const element = document.getElementById(elementId);
        if (!element || !dotNetRef) return;

        this.dispose(elementId);

        const editor = { element, dotNetRef, handlers: {}, channelId: channelId || null };

        editor.handlers.input = () => this._notifyContentChanged(elementId);
        element.addEventListener('input', editor.handlers.input);

        editor.handlers.keydown = (e) => this._handleKeyDown(elementId, e);
        element.addEventListener('keydown', editor.handlers.keydown);

        editor.handlers.paste = (e) => this._handlePaste(elementId, e);
        element.addEventListener('paste', editor.handlers.paste);

        this._editors[elementId] = editor;
    },

    /**
     * Updates the channel ID for HTTP image uploads.
     * Called when the user switches channels.
     * @param {string} elementId
     * @param {string} channelId
     */
    setChannelId: function (elementId, channelId) {
        const editor = this._editors[elementId];
        if (editor) editor.channelId = channelId || null;
    },

    /**
     * Applies a formatting command.
     * @param {string} elementId
     * @param {string} command - bold|italic|strikethrough|inlineCode|codeBlock|link|bulletList|numberedList|blockquote|heading
     */
    format: function (elementId, command) {
        const editor = this._editors[elementId];
        if (!editor) return;

        editor.element.focus();

        switch (command) {
            case 'bold':
                document.execCommand('bold', false);
                break;
            case 'italic':
                document.execCommand('italic', false);
                break;
            case 'strikethrough':
                document.execCommand('strikeThrough', false);
                break;
            case 'inlineCode':
                this._toggleInlineCode(editor.element);
                break;
            case 'codeBlock':
                this._insertCodeBlock(editor.element);
                break;
            case 'link':
                this._insertLink(editor.element);
                break;
            case 'bulletList':
                document.execCommand('insertUnorderedList', false);
                break;
            case 'numberedList':
                document.execCommand('insertOrderedList', false);
                break;
            case 'blockquote':
                this._toggleBlockquote(editor.element);
                break;
            case 'heading':
                this._toggleHeading(editor.element);
                break;
        }

        this._notifyContentChanged(elementId);
    },

    /**
     * Returns the editor content as Markdown.
     * @param {string} elementId
     * @returns {string}
     */
    getMarkdown: function (elementId) {
        const editor = this._editors[elementId];
        if (!editor) return '';
        return this._htmlToMarkdown(editor.element).trim();
    },

    /**
     * Sets the editor content from Markdown (for editing existing messages).
     * @param {string} elementId
     * @param {string} markdown
     */
    setContent: function (elementId, markdown) {
        const editor = this._editors[elementId];
        if (!editor) return;
        editor.element.innerHTML = this._markdownToHtml(markdown || '');
        this._notifyContentChanged(elementId);
    },

    /**
     * Clears the editor.
     * @param {string} elementId
     */
    clear: function (elementId) {
        const editor = this._editors[elementId];
        if (!editor) return;
        editor.element.innerHTML = '';
        this._notifyContentChanged(elementId);
    },

    /**
     * Whether the editor has no meaningful content.
     * @param {string} elementId
     * @returns {boolean}
     */
    isEmpty: function (elementId) {
        const editor = this._editors[elementId];
        if (!editor) return true;
        return !(editor.element.innerText || '').trim();
    },

    /**
     * Focuses the editor.
     * @param {string} elementId
     */
    focus: function (elementId) {
        const editor = this._editors[elementId];
        if (editor) editor.element.focus();
    },

    /**
     * Inserts text at the cursor.
     * @param {string} elementId
     * @param {string} text
     */
    insertText: function (elementId, text) {
        const editor = this._editors[elementId];
        if (!editor) return;
        editor.element.focus();
        document.execCommand('insertText', false, text);
        this._notifyContentChanged(elementId);
    },

    /**
     * Replaces the @query near the cursor with a mention.
     * @param {string} elementId
     * @param {string} mentionText - e.g. "@DisplayName"
     * @param {number} queryLength - length of the query after @
     */
    insertMention: function (elementId, mentionText, queryLength) {
        const editor = this._editors[elementId];
        if (!editor) return;

        editor.element.focus();
        const sel = window.getSelection();
        if (!sel.rangeCount) return;

        const range = sel.getRangeAt(0);
        let node = range.startContainer;
        if (node.nodeType !== Node.TEXT_NODE) return;

        const text = node.textContent;
        const cursor = range.startOffset;
        const atIdx = cursor - queryLength - 1;
        if (atIdx < 0 || text[atIdx] !== '@') return;

        const before = text.substring(0, atIdx);
        const after = text.substring(cursor);
        node.textContent = before + mentionText + ' ' + after;

        const pos = (before + mentionText + ' ').length;
        const r = document.createRange();
        r.setStart(node, Math.min(pos, node.textContent.length));
        r.collapse(true);
        sel.removeAllRanges();
        sel.addRange(r);

        this._notifyContentChanged(elementId);
    },

    /**
     * Cleans up listeners for an editor instance.
     * @param {string} elementId
     */
    dispose: function (elementId) {
        const editor = this._editors[elementId];
        if (!editor) return;

        for (const [event, handler] of Object.entries(editor.handlers)) {
            editor.element.removeEventListener(event, handler);
        }
        delete this._editors[elementId];
    },

    /* ── Internal Handlers ─────────────────────────────────── */

    _handleKeyDown: function (elementId, event) {
        const editor = this._editors[elementId];
        if (!editor) return;

        // Enter → send (unless inside a block element or Shift held)
        if (event.key === 'Enter' && !event.shiftKey) {
            const sel = window.getSelection();
            if (sel.rangeCount > 0) {
                let node = sel.anchorNode;
                while (node && node !== editor.element) {
                    const tag = node.nodeName?.toLowerCase();
                    if (tag === 'ul' || tag === 'ol' || tag === 'blockquote' || tag === 'pre') {
                        return; // allow default Enter in block containers
                    }
                    node = node.parentNode;
                }
            }
            event.preventDefault();
            editor.dotNetRef.invokeMethodAsync('HandleEnterKey');
            return;
        }

        if (event.key === 'Escape') {
            event.preventDefault();
            editor.dotNetRef.invokeMethodAsync('HandleEscapeKey');
            return;
        }

        // Keyboard shortcuts
        if (event.ctrlKey || event.metaKey) {
            switch (event.key.toLowerCase()) {
                case 'b':
                    event.preventDefault();
                    document.execCommand('bold', false);
                    this._notifyContentChanged(elementId);
                    break;
                case 'i':
                    event.preventDefault();
                    document.execCommand('italic', false);
                    this._notifyContentChanged(elementId);
                    break;
                case 'k':
                    event.preventDefault();
                    this._insertLink(editor.element);
                    this._notifyContentChanged(elementId);
                    break;
            }
        }
    },

    _handlePaste: function (elementId, event) {
        const editor = this._editors[elementId];
        if (!editor) return;

        const clip = event.clipboardData;
        if (!clip) return;

        // Images take priority
        for (const item of clip.items) {
            if (item.type?.startsWith('image/')) {
                event.preventDefault();
                this._handlePastedImage(editor, item);
                return;
            }
        }

        // Strip external HTML — paste as plain text only
        if (clip.types.includes('text/plain')) {
            event.preventDefault();
            document.execCommand('insertText', false, clip.getData('text/plain') || '');
        }
    },

    _handlePastedImage: async function (editor, item) {
        const file = item.getAsFile();
        if (!file) return;

        // Upload via HTTP to bypass SignalR message size limits
        if (editor.channelId && window.chatImageUpload?.uploadImageFile) {
            const result = await window.chatImageUpload.uploadImageFile(file, editor.channelId);
            if (result) {
                await editor.dotNetRef.invokeMethodAsync(
                    'HandlePastedImageUploaded',
                    result.url,
                    result.fileName,
                    result.mimeType,
                    result.fileSize
                );
                return;
            }
        }

        // Fallback: send data URL via SignalR (works only for very small images)
        const dataUrl = await new Promise((resolve) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result || '');
            reader.onerror = () => resolve('');
            reader.readAsDataURL(file);
        });
        if (!dataUrl) return;

        await editor.dotNetRef.invokeMethodAsync(
            'HandlePastedImageFromJs',
            file.name || 'pasted-image.png',
            file.type || 'image/png',
            dataUrl,
            file.size || 0
        );
    },

    _notifyContentChanged: function (elementId) {
        const editor = this._editors[elementId];
        if (!editor) return;

        const text = editor.element.innerText || '';
        const empty = !text.trim();
        editor.dotNetRef.invokeMethodAsync('HandleContentChanged', text, empty);
    },

    /* ── Format Helpers ────────────────────────────────────── */

    _toggleInlineCode: function (editorEl) {
        const sel = window.getSelection();
        if (!sel.rangeCount) return;

        const range = sel.getRangeAt(0);

        // Check if cursor is already inside <code>
        let parent = range.commonAncestorContainer;
        while (parent && parent !== editorEl) {
            if (parent.nodeName?.toLowerCase() === 'code' &&
                parent.parentElement?.tagName?.toLowerCase() !== 'pre') {
                // Unwrap
                const text = document.createTextNode(parent.textContent);
                parent.parentNode.replaceChild(text, parent);
                const r = document.createRange();
                r.selectNodeContents(text);
                sel.removeAllRanges();
                sel.addRange(r);
                return;
            }
            parent = parent.parentNode;
        }

        // Wrap in <code>
        const code = document.createElement('code');
        if (range.collapsed) {
            code.textContent = '\u200B';
            range.insertNode(code);
            const r = document.createRange();
            r.setStart(code.firstChild, 1);
            r.collapse(true);
            sel.removeAllRanges();
            sel.addRange(r);
        } else {
            try {
                range.surroundContents(code);
            } catch {
                const frag = range.extractContents();
                code.appendChild(frag);
                range.insertNode(code);
            }
            sel.collapseToEnd();
        }
    },

    _insertCodeBlock: function (editorEl) {
        const sel = window.getSelection();
        const range = sel.rangeCount ? sel.getRangeAt(0) : null;

        const pre = document.createElement('pre');
        const code = document.createElement('code');

        if (range && !range.collapsed) {
            code.textContent = range.toString();
            range.deleteContents();
        } else {
            code.innerHTML = '<br>';
        }
        pre.appendChild(code);

        if (range) {
            range.insertNode(pre);
            // Paragraph after for continued typing
            const after = document.createElement('div');
            after.innerHTML = '<br>';
            if (pre.nextSibling) {
                pre.parentNode.insertBefore(after, pre.nextSibling);
            } else {
                pre.parentNode.appendChild(after);
            }
            const r = document.createRange();
            r.setStart(code, 0);
            r.collapse(true);
            sel.removeAllRanges();
            sel.addRange(r);
        }
    },

    _insertLink: function (editorEl) {
        const sel = window.getSelection();
        const range = sel.rangeCount ? sel.getRangeAt(0) : null;
        const selectedText = range ? range.toString() : '';

        const url = prompt('Enter URL:', 'https://');
        if (!url) return;

        // Only allow http/https
        if (!/^https?:\/\//i.test(url)) {
            return;
        }

        const a = document.createElement('a');
        a.href = url;
        a.target = '_blank';
        a.rel = 'noopener noreferrer';

        if (selectedText) {
            a.textContent = selectedText;
            range.deleteContents();
        } else {
            a.textContent = url;
        }

        if (range) {
            range.insertNode(a);
            sel.collapseToEnd();
        }
    },

    _toggleBlockquote: function (editorEl) {
        const sel = window.getSelection();
        if (!sel.rangeCount) return;

        let node = sel.anchorNode;
        while (node && node !== editorEl) {
            if (node.nodeName?.toLowerCase() === 'blockquote') {
                document.execCommand('formatBlock', false, 'div');
                return;
            }
            node = node.parentNode;
        }
        document.execCommand('formatBlock', false, 'blockquote');
    },

    _toggleHeading: function (editorEl) {
        const sel = window.getSelection();
        if (!sel.rangeCount) return;

        let node = sel.anchorNode;
        while (node && node !== editorEl) {
            if (/^h[1-6]$/.test(node.nodeName?.toLowerCase() || '')) {
                document.execCommand('formatBlock', false, 'div');
                return;
            }
            node = node.parentNode;
        }
        document.execCommand('formatBlock', false, 'h3');
    },

    /* ── HTML → Markdown ───────────────────────────────────── */

    _htmlToMarkdown: function (element) {
        let result = '';

        for (const node of element.childNodes) {
            if (node.nodeType === Node.TEXT_NODE) {
                result += node.textContent;
                continue;
            }
            if (node.nodeType !== Node.ELEMENT_NODE) continue;

            const tag = node.tagName.toLowerCase();
            const inner = this._htmlToMarkdown(node);

            switch (tag) {
                case 'strong':
                case 'b':
                    result += '**' + inner + '**';
                    break;
                case 'em':
                case 'i':
                    result += '*' + inner + '*';
                    break;
                case 's':
                case 'del':
                case 'strike':
                    result += '~~' + inner + '~~';
                    break;
                case 'code':
                    if (node.parentElement?.tagName?.toLowerCase() === 'pre') {
                        result += node.textContent;
                    } else {
                        result += '`' + node.textContent + '`';
                    }
                    break;
                case 'pre': {
                    const codeEl = node.querySelector('code');
                    const txt = codeEl ? codeEl.textContent : node.textContent;
                    result += '\n```\n' + txt + '\n```\n';
                    break;
                }
                case 'a': {
                    const href = node.getAttribute('href') || '';
                    if (href && /^https?:\/\//i.test(href)) {
                        result += '[' + inner + '](' + href + ')';
                    } else {
                        result += inner;
                    }
                    break;
                }
                case 'ul':
                    for (const li of node.children) {
                        if (li.tagName?.toLowerCase() === 'li') {
                            result += '\n- ' + this._htmlToMarkdown(li).trim();
                        }
                    }
                    result += '\n';
                    break;
                case 'ol': {
                    let num = 1;
                    for (const li of node.children) {
                        if (li.tagName?.toLowerCase() === 'li') {
                            result += '\n' + num + '. ' + this._htmlToMarkdown(li).trim();
                            num++;
                        }
                    }
                    result += '\n';
                    break;
                }
                case 'blockquote': {
                    const lines = inner.split('\n').filter(l => l.trim());
                    result += lines.map(l => '\n> ' + l.trim()).join('') + '\n';
                    break;
                }
                case 'h1': result += '\n# ' + inner + '\n'; break;
                case 'h2': result += '\n## ' + inner + '\n'; break;
                case 'h3': result += '\n### ' + inner + '\n'; break;
                case 'h4': result += '\n#### ' + inner + '\n'; break;
                case 'h5': result += '\n##### ' + inner + '\n'; break;
                case 'h6': result += '\n###### ' + inner + '\n'; break;
                case 'hr':
                    result += '\n---\n';
                    break;
                case 'br':
                    result += '\n';
                    break;
                case 'div':
                case 'p': {
                    const content = inner;
                    if (content.trim()) {
                        result += (result && !result.endsWith('\n') ? '\n' : '') + content;
                    } else if (result && !result.endsWith('\n')) {
                        result += '\n';
                    }
                    break;
                }
                default:
                    result += inner;
                    break;
            }
        }

        return result;
    },

    /* ── Markdown → HTML (for editing existing messages) ──── */

    _markdownToHtml: function (markdown) {
        if (!markdown) return '';

        const lines = markdown.split('\n');
        let html = '';
        let inCodeBlock = false;
        let codeBlockContent = '';
        let inList = false;
        let listType = '';

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmed = line.trimStart();

            // Code fence toggle
            if (trimmed.startsWith('```')) {
                if (inCodeBlock) {
                    html += '<pre><code>' + this._escapeHtml(codeBlockContent) + '</code></pre>';
                    codeBlockContent = '';
                    inCodeBlock = false;
                } else {
                    if (inList) { html += '</' + listType + '>'; inList = false; }
                    inCodeBlock = true;
                }
                continue;
            }
            if (inCodeBlock) {
                codeBlockContent += (codeBlockContent ? '\n' : '') + line;
                continue;
            }

            // Horizontal rule
            if (/^(-{3,}|\*{3,}|_{3,})$/.test(trimmed)) {
                if (inList) { html += '</' + listType + '>'; inList = false; }
                html += '<hr>';
                continue;
            }

            const isUL = /^[-*]\s/.test(trimmed);
            const olMatch = trimmed.match(/^(\d+)\.\s+(.+)$/);
            const isOL = !!olMatch;

            // Close list if this line isn't a list item
            if (inList && !isUL && !isOL) {
                html += '</' + listType + '>';
                inList = false;
            }

            // Heading
            const hMatch = trimmed.match(/^(#{1,6})\s+(.+)$/);
            if (hMatch) {
                if (inList) { html += '</' + listType + '>'; inList = false; }
                const lvl = hMatch[1].length;
                html += '<h' + lvl + '>' + this._inlineFmt(hMatch[2]) + '</h' + lvl + '>';
                continue;
            }

            // Blockquote
            if (trimmed.startsWith('> ')) {
                if (inList) { html += '</' + listType + '>'; inList = false; }
                html += '<blockquote>' + this._inlineFmt(trimmed.substring(2)) + '</blockquote>';
                continue;
            }

            // Unordered list
            if (isUL) {
                if (!inList || listType !== 'ul') {
                    if (inList) html += '</' + listType + '>';
                    html += '<ul>';
                    inList = true;
                    listType = 'ul';
                }
                html += '<li>' + this._inlineFmt(trimmed.substring(2)) + '</li>';
                continue;
            }

            // Ordered list
            if (isOL) {
                if (!inList || listType !== 'ol') {
                    if (inList) html += '</' + listType + '>';
                    html += '<ol>';
                    inList = true;
                    listType = 'ol';
                }
                html += '<li>' + this._inlineFmt(olMatch[2]) + '</li>';
                continue;
            }

            // Blank line
            if (!trimmed) {
                html += '<br>';
                continue;
            }

            // Normal text
            html += '<div>' + this._inlineFmt(line) + '</div>';
        }

        if (inList) html += '</' + listType + '>';
        if (inCodeBlock) html += '<pre><code>' + this._escapeHtml(codeBlockContent) + '</code></pre>';

        // Remove trailing <br>
        if (html.endsWith('<br>')) html = html.slice(0, -4);
        return html;
    },

    /** Apply inline Markdown formatting on already-escaped HTML. */
    _inlineFmt: function (text) {
        let h = this._escapeHtml(text);
        // Inline code first (protect from other patterns)
        h = h.replace(/`([^`]+)`/g, '<code>$1</code>');
        // Links
        h = h.replace(/\[([^\]]+)\]\((https?:\/\/[^)]+)\)/gi,
            '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');
        // Bold + italic
        h = h.replace(/\*\*\*([^*]+)\*\*\*/g, '<strong><em>$1</em></strong>');
        // Bold
        h = h.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        // Italic (*…*)
        h = h.replace(/\*([^*]+)\*/g, '<em>$1</em>');
        // Italic (_…_)
        h = h.replace(/_([^_]+)_/g, '<em>$1</em>');
        // Strikethrough
        h = h.replace(/~~([^~]+)~~/g, '<s>$1</s>');
        return h;
    },

    _escapeHtml: function (text) {
        const d = document.createElement('div');
        d.textContent = text;
        return d.innerHTML;
    }
};
