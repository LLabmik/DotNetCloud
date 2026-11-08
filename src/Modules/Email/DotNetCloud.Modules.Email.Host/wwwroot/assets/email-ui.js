/*
 * Email module UI.
 *
 * Runs inside a sandboxed frame with an OPAQUE ORIGIN (no allow-same-origin), so:
 *   * no document.cookie / localStorage / sessionStorage — every request goes to this module's own
 *     server through the same proxied path this document was loaded from;
 *   * event.origin is "null" for every message — the parent is identified by the SDK bridge, so never
 *     add your own window.message listener;
 *   * every fetch is cross-origin, so requests use credentials: 'include' and the core proxy answers
 *     the preflight;
 *   * modals are available (allow-modals). Confirmation uses a <dialog> modal;
 *   * downloads are not permitted, so attachments and the Gmail consent flow open in a core-controlled
 *     tab via the bridge.
 *
 * All requests are RELATIVE on purpose (see index.html): the core strips /apps/dotnetcloud.email
 * before forwarding, so "api/v1/email" resolves to this process's own REST API.
 *
 * Every string that comes from the server is user data and is written with textContent (or an
 * attribute setter), never innerHTML. The one exception is an email's own HTML body, which is
 * rendered inside a nested, fully sandboxed frame (sandbox="") — that frame is the sanitizer.
 */
(function () {
  'use strict';

  var API = 'api/v1/email';

  /* ---------------------------------------------------------------- icons */

  var ICONS = {
    mail: 'M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4-8 5-8-5V6l8 5 8-5v2z',
    add: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z',
    edit: 'M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.996.996 0 0 0 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z',
    person_add: 'M15 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm-9-2V7H4v3H1v2h3v3h2v-3h3v-2H6zm9 4c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z',
    settings: 'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z',
    sync: 'M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z',
    delete: 'M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z',
    help: 'M11 18h2v-2h-2v2zm1-16C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-2.21 0-4 1.79-4 4h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4-4z',
    close: 'M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z',
    chevron_left: 'M15.41 7.41 14 6l-6 6 6 6 1.41-1.41L10.83 12z',
    chevron_right: 'M10 6 8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z',
    arrow_back: 'M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z',
    send: 'M2.01 21 23 12 2.01 3 2 10l15 2-15 2z',
    attach_file: 'M16.5 6v11.5c0 2.21-1.79 4-4 4s-4-1.79-4-4V5c0-1.38 1.12-2.5 2.5-2.5s2.5 1.12 2.5 2.5v10.5c0 .55-.45 1-1 1s-1-.45-1-1V6H10v9.5c0 1.38 1.12 2.5 2.5 2.5s2.5-1.12 2.5-2.5V5c0-2.21-1.79-4-4-4S7 2.79 7 5v12.5c0 3.04 2.46 5.5 5.5 5.5s5.5-2.46 5.5-5.5V6h-1.5z',
    move_to_inbox: 'M19 3H4.99c-1.11 0-1.98.9-1.98 2L3 19c0 1.1.88 2 1.99 2H19c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 12h-4c0 1.66-1.35 3-3 3s-3-1.34-3-3H4.99V5H19v10zm-3-5h-2V7h-4v3H8l4 4 4-4z',
    outbox: 'M19 3H4.99c-1.11 0-1.98.9-1.98 2L3 19c0 1.1.88 2 1.99 2H19c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 12h-4c0 1.66-1.35 3-3 3s-3-1.34-3-3H4.99V5H19v10zM8 11h2V8h4v3h2l-4 4-4-4z',
    draft: 'M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.996.996 0 0 0 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z',
    folder: 'M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z',
    block: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zM4 12c0-4.42 3.58-8 8-8 1.85 0 3.55.63 4.9 1.69L5.69 16.9A7.902 7.902 0 0 1 4 12zm8 8c-1.85 0-3.55-.63-4.9-1.69L18.31 7.1A7.902 7.902 0 0 1 20 12c0 4.42-3.58 8-8 8z',
    inventory_2: 'M20 2H4c-1 0-2 .9-2 2v3.01c0 .72.43 1.34 1 1.69V20c0 1.1 1.1 2 2 2h14c.9 0 2-.9 2-2V8.7c.57-.35 1-.97 1-1.69V4c0-1.1-1-2-2-2zm-5 12H9v-2h6v2zm5-7H4V4h16v3z',
    image: 'M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z',
    picture_as_pdf: 'M20 2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-8.5 7.5c0 .83-.67 1.5-1.5 1.5H9v2H7.5V7H10c.83 0 1.5.67 1.5 1.5v1zm5 2c0 .83-.67 1.5-1.5 1.5h-2.5V7H15c.83 0 1.5.67 1.5 1.5v3zm4-3H19v1h1.5V11H19v2h-1.5V7h3v1.5zM9 9.5h1v-1H9v1zM4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6zm10 5.5h1v-3h-1v3z',
    description: 'M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z',
    text_snippet: 'M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zM8 15h8v2H8v-2zm0-4h8v2H8v-2zm3-5h5v2h-5V6z',
    music_note: 'M12 3v10.55A4 4 0 1 0 14 17V7h4V3h-6z',
    movie: 'M18 4l2 4h-3l-2-4h-2l2 4h-3l-2-4H8l2 4H7L5 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4h-4z',
    table_chart: 'M10 10.02h5V21h-5zM17 21h3c1.1 0 2-.9 2-2v-9h-5v11zM20 3H5c-1.1 0-2 .9-2 2v3h19V5c0-1.1-.9-2-2-2zM3 19c0 1.1.9 2 2 2h3V10H3v9z',
    slideshow: 'M10 8v8l5-4-5-4zm9-5H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14z',
    article: 'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z'
  };

  function buildIcon(name, className) {
    var path = ICONS[name] || ICONS.mail;
    var ns = 'http://www.w3.org/2000/svg';
    var svg = document.createElementNS(ns, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
    svg.setAttribute('class', 'icon' + (className ? ' ' + className : ''));
    var node = document.createElementNS(ns, 'path');
    node.setAttribute('d', path);
    svg.appendChild(node);
    return svg;
  }

  /** Fills every element carrying data-icon with its Material icon. */
  function paintIcons(root) {
    var scope = root || document;
    var targets = scope.querySelectorAll('[data-icon]:not([data-icon-painted])');
    Array.prototype.forEach.call(targets, function (target) {
      target.setAttribute('data-icon-painted', 'yes');
      target.appendChild(buildIcon(target.getAttribute('data-icon')));
    });
  }

  /** Creates an element with an optional class and text. */
  function node(tag, className, text) {
    var element = document.createElement(tag);
    if (className) {
      element.className = className;
    }
    if (text !== undefined && text !== null) {
      element.textContent = String(text);
    }
    return element;
  }

  function iconButton(icon, label, onClick, extraClass) {
    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'icon-btn' + (extraClass ? ' ' + extraClass : '');
    button.title = label;
    button.setAttribute('aria-label', label);
    button.appendChild(buildIcon(icon));
    button.addEventListener('click', function (event) {
      event.stopPropagation();
      onClick();
    });
    return button;
  }

  /* ------------------------------------------------------------------- api */

  /** Error carrying the HTTP status so a 404 can be told apart from a failure. */
  function ApiError(status, message) {
    this.name = 'ApiError';
    this.status = status;
    this.message = message;
  }

  ApiError.prototype = Object.create(Error.prototype);

  async function apiFetch(path, init) {
    var options = {
      method: (init && init.method) || 'GET',
      // Every call from this frame is cross-origin (opaque origin), so credentials are included
      // explicitly; the core proxy authenticates the call from the session cookie.
      credentials: 'include',
      headers: { Accept: 'application/json' }
    };

    if (init && init.body !== undefined) {
      options.body = init.body;
    }

    if (init && init.contentType) {
      options.headers['Content-Type'] = init.contentType;
    }

    var response;

    try {
      response = await fetch(path, options);
    } catch (error) {
      throw new ApiError(0, 'Could not reach the Email module. Try again.');
    }

    var text = await response.text();
    var payload = null;

    if (text) {
      try {
        payload = JSON.parse(text);
      } catch (error) {
        payload = null;
      }
    }

    if (response.status === 401) {
      throw new ApiError(401, 'Sign in to DotNetCloud to use this module.');
    }

    if (!response.ok) {
      var message = 'Request failed (' + response.status + ').';
      if (payload && payload.error && typeof payload.error.message === 'string' && payload.error.message) {
        message = payload.error.message;
      }

      throw new ApiError(response.status, message);
    }

    if (payload && Object.prototype.hasOwnProperty.call(payload, 'data')) {
      return payload.data;
    }

    return payload;
  }

  function apiJson(path, method, body) {
    return apiFetch(path, {
      method: method,
      contentType: 'application/json',
      body: body === undefined ? undefined : JSON.stringify(body)
    });
  }

  /** Absolute URL for a path served by this module, for handing to the core. */
  function absoluteUrl(path) {
    return new URL(path, document.baseURI).href;
  }

  /* ----------------------------------------------------------------- state */

  var state = {
    accounts: [],
    rules: [],
    gmailConfigured: false,
    selectedAccountId: null,
    mailboxes: [],
    selectedMailboxId: null,
    threads: [],
    selectedThread: null,
    messages: [],
    view: 'welcome',
    rule: null,
    compose: null,
    busy: false,
    sidebarCollapsed: false,
    error: null,
    notice: null
  };

  var el = {};
  var bridge = window.dncModuleBridge || null;

  /* ------------------------------------------------------------- formatting */

  function parseAddresses(json) {
    if (!json) {
      return [];
    }

    try {
      var parsed = JSON.parse(json);
      return Array.isArray(parsed) ? parsed : [];
    } catch (error) {
      return [];
    }
  }

  function displayAddress(address) {
    if (!address) {
      return '';
    }

    return address.name && String(address.name).trim() ? address.name : address.email;
  }

  function formatFrom(json) {
    var addresses = parseAddresses(json);
    return addresses.length > 0 ? displayAddress(addresses[0]) : 'Unknown sender';
  }

  function formatParticipants(json) {
    var addresses = parseAddresses(json);
    if (addresses.length === 0) {
      return 'Unknown';
    }

    return addresses.slice(0, 3).map(displayAddress).join(', ');
  }

  function formatDate(value) {
    if (!value) {
      return '';
    }

    var date = new Date(value);
    if (isNaN(date.getTime())) {
      return '';
    }

    return date.toLocaleString(undefined, {
      year: 'numeric',
      month: 'short',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  function formatSize(bytes) {
    var size = Number(bytes) || 0;
    if (size < 1024) {
      return size + ' B';
    }
    if (size < 1024 * 1024) {
      return (size / 1024).toFixed(1) + ' KB';
    }
    if (size < 1024 * 1024 * 1024) {
      return (size / (1024 * 1024)).toFixed(1) + ' MB';
    }
    return (size / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  function mailboxIcon(mailbox) {
    var flags = Number(mailbox.syncFlags) || 0;
    if (flags & 1) {
      return 'move_to_inbox';
    }
    if (flags & 2) {
      return 'outbox';
    }
    if (flags & 8) {
      return 'draft';
    }
    if (flags & 4) {
      return 'delete';
    }
    if (flags & 32) {
      return 'block';
    }
    if (flags & 16) {
      return 'inventory_2';
    }
    return 'folder';
  }

  function fileTypeIcon(contentType) {
    var type = String(contentType || '').toLowerCase();
    if (!type) {
      return 'attach_file';
    }
    if (type.indexOf('image/') === 0) {
      return 'image';
    }
    if (type === 'application/pdf') {
      return 'picture_as_pdf';
    }
    if (type.indexOf('text/') === 0) {
      return 'text_snippet';
    }
    if (type.indexOf('audio/') === 0) {
      return 'music_note';
    }
    if (type.indexOf('video/') === 0) {
      return 'movie';
    }
    if (type.indexOf('spreadsheet') >= 0 || type.indexOf('excel') >= 0 || type.indexOf('csv') >= 0) {
      return 'table_chart';
    }
    if (type.indexOf('presentation') >= 0 || type.indexOf('powerpoint') >= 0) {
      return 'slideshow';
    }
    if (type.indexOf('word') >= 0 || type.indexOf('document') >= 0) {
      return 'article';
    }
    if (type.indexOf('zip') >= 0 || type.indexOf('rar') >= 0 || type.indexOf('gzip') >= 0 || type.indexOf('tar') >= 0) {
      return 'folder';
    }
    return 'attach_file';
  }

  /** Parses "Name <a@b>, c@d" into the API's address shape. */
  function parseRecipients(input) {
    var result = [];
    String(input || '').split(',').forEach(function (part) {
      var value = part.trim();
      if (!value) {
        return;
      }

      var match = value.match(/^(.*?)\s*<(.+?)>$/);
      if (match) {
        result.push({ name: match[1].trim().replace(/^"|"$/g, '') || null, email: match[2].trim() });
      } else {
        result.push({ name: null, email: value });
      }
    });

    return result;
  }

  function escapeHtml(value) {
    return String(value === undefined || value === null ? '' : value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  /** Converts an escaped plain-text body into paragraphs, keeping > quotes. */
  function plainTextToHtml(text) {
    if (!text) {
      return '';
    }

    var out = [];
    var depth = 0;

    String(text).split('\n').forEach(function (rawLine) {
      var line = rawLine.replace(/\r$/, '');
      var quoteData = { depth: 0, content: line };

      while (quoteData.content.charAt(0) === '>') {
        quoteData.depth += 1;
        quoteData.content = quoteData.content.slice(1).replace(/^\s+/, '');
      }

      if (quoteData.depth > 0) {
        while (depth > quoteData.depth) {
          out.push('</blockquote>');
          depth -= 1;
        }
        while (depth < quoteData.depth) {
          out.push('<blockquote>');
          depth += 1;
        }
        out.push('<p>' + escapeHtml(quoteData.content) + '</p>');
        return;
      }

      while (depth > 0) {
        out.push('</blockquote>');
        depth -= 1;
      }

      if (/^[-=_]{3,}$/.test(line)) {
        out.push('<hr>');
      } else if (!line.trim()) {
        out.push('<br>');
      } else {
        out.push('<p>' + escapeHtml(line) + '</p>');
      }
    });

    while (depth > 0) {
      out.push('</blockquote>');
      depth -= 1;
    }

    return out.join('');
  }

  /* ------------------------------------------------------------------ modal */

  /**
   * Shows a modal confirmation and resolves to true when the user accepts.
   *
   * Uses <dialog>.showModal(), which is available inside the frame (the core grants allow-modals):
   * unlike window.confirm() it does not freeze the rest of the page, and a browser "suppress
   * dialogs" setting cannot turn it into a silent "no".
   */
  function confirmDialog(options) {
    var settings = options || {};

    return new Promise(function (resolve) {
      var dialog = node('dialog', 'modal');
      dialog.setAttribute('aria-labelledby', 'modal-title');

      var title = node('h3', 'modal-title', settings.title || 'Confirm');
      title.id = 'modal-title';

      var body = node('p', 'modal-text', settings.message || '');

      var actions = node('div', 'modal-actions');
      var cancel = node('button', 'btn btn-secondary', settings.cancelLabel || 'Cancel');
      cancel.type = 'button';
      var confirm = node('button', settings.danger ? 'btn btn-danger' : 'btn btn-primary', settings.confirmLabel || 'OK');
      confirm.type = 'button';

      function close(result) {
        if (dialog.open) {
          dialog.close();
        }
        if (dialog.parentNode) {
          dialog.parentNode.removeChild(dialog);
        }
        resolve(result);
      }

      cancel.addEventListener('click', function () {
        close(false);
      });
      confirm.addEventListener('click', function () {
        close(true);
      });
      dialog.addEventListener('cancel', function (event) {
        event.preventDefault();
        close(false);
      });

      actions.appendChild(cancel);
      actions.appendChild(confirm);
      dialog.appendChild(title);
      dialog.appendChild(body);
      dialog.appendChild(actions);
      document.body.appendChild(dialog);
      dialog.showModal();
      confirm.focus();
    });
  }

  /* ------------------------------------------------------------------ data */

  function selectedAccount() {
    return state.accounts.filter(function (account) {
      return account.id === state.selectedAccountId;
    })[0] || null;
  }

  function selectedMailbox() {
    return state.mailboxes.filter(function (mailbox) {
      return mailbox.id === state.selectedMailboxId;
    })[0] || null;
  }

  async function loadAccounts() {
    state.accounts = (await apiFetch(API + '/accounts')) || [];

    if (state.accounts.length === 0) {
      state.selectedAccountId = null;
      state.mailboxes = [];
      state.selectedMailboxId = null;
      state.threads = [];
      return;
    }

    var stillThere = state.accounts.some(function (account) {
      return account.id === state.selectedAccountId;
    });
    if (!stillThere) {
      state.selectedAccountId = state.accounts[0].id;
    }

    await loadMailboxes(true);
  }

  async function loadMailboxes(selectInbox) {
    var account = selectedAccount();
    if (!account) {
      state.mailboxes = [];
      state.selectedMailboxId = null;
      state.threads = [];
      return;
    }

    var mailboxes = (await apiFetch(API + '/accounts/' + account.id + '/mailboxes')) || [];

    state.mailboxes = mailboxes.slice().sort(function (left, right) {
      function rank(mailbox) {
        var flags = Number(mailbox.syncFlags) || 0;
        if (flags & 1) {
          return 0;
        }
        if (flags & 2) {
          return 1;
        }
        if (flags & 8) {
          return 2;
        }
        return 10;
      }

      return rank(left) - rank(right) || (left.sortOrder || 0) - (right.sortOrder || 0);
    });

    var stillThere = state.mailboxes.some(function (mailbox) {
      return mailbox.id === state.selectedMailboxId;
    });

    if (selectInbox || !stillThere) {
      var inbox = state.mailboxes.filter(function (mailbox) {
        return (Number(mailbox.syncFlags) || 0) & 1;
      })[0];

      state.selectedMailboxId = inbox ? inbox.id : (state.mailboxes[0] ? state.mailboxes[0].id : null);
      state.threads = [];
      state.selectedThread = null;
      state.messages = [];
      await loadThreads();
    }
  }

  async function loadThreads() {
    var account = selectedAccount();
    var mailbox = selectedMailbox();
    if (!account || !mailbox) {
      state.threads = [];
      return;
    }

    state.threads = (await apiFetch(
      API + '/accounts/' + account.id + '/mailboxes/' + mailbox.id + '/threads')) || [];
  }

  async function openThread(thread) {
    state.selectedThread = thread;
    state.messages = [];
    state.view = 'thread';
    render();

    var messages = (await apiFetch(API + '/threads/' + thread.id + '/messages')) || [];

    // Bodies are loaded up front: the module stores the full HTML separately from the list payload.
    for (var index = 0; index < messages.length; index += 1) {
      var message = messages[index];
      var body = null;

      try {
        var result = await apiFetch(API + '/messages/' + message.id + '/body');
        body = result && result.bodyHtml ? result.bodyHtml : null;
      } catch (error) {
        body = null;
      }

      state.messages.push({ message: message, bodyHtml: body });

      if (state.view === 'thread' && state.selectedThread === thread) {
        render();
      }
    }
  }

  async function reloadRules() {
    state.rules = (await apiFetch(API + '/rules')) || [];
  }

  /* --------------------------------------------------------------- actions */

  async function runAction(action) {
    state.error = null;
    state.notice = null;
    state.busy = true;
    render();

    try {
      await action();
    } catch (error) {
      state.error = error && error.message ? error.message : 'Something went wrong.';
    } finally {
      state.busy = false;
      render();
    }
  }

  function selectAccount(accountId) {
    state.selectedAccountId = accountId;
    state.selectedThread = null;
    state.messages = [];
    state.threads = [];
    state.selectedMailboxId = null;
    state.view = 'mailbox';
    runAction(loadMailboxes);
  }

  function selectMailbox(mailboxId) {
    state.selectedMailboxId = mailboxId;
    state.selectedThread = null;
    state.messages = [];
    state.view = 'mailbox';
    runAction(loadThreads);
  }

  function beginCompose(prefill) {
    var account = selectedAccount();
    if (!account) {
      state.notice = 'Add an email account before composing.';
      state.view = 'addAccount';
      render();
      return;
    }

    state.compose = {
      accountId: account.id,
      to: (prefill && prefill.to) || '',
      cc: '',
      bcc: '',
      subject: (prefill && prefill.subject) || '',
      body: '',
      attachments: [],
      uploading: 0
    };
    state.view = 'compose';
    render();
  }

  function replyToThread(thread, prefix) {
    var participants = parseAddresses(thread.participantsJson);
    var to = prefix === 'Fwd:'
      ? ''
      : participants.map(function (address) {
        return address.name ? address.name + ' <' + address.email + '>' : address.email;
      }).join(', ');

    beginCompose({ to: to, subject: prefix + (thread.subject || '') });
  }

  async function sendCompose() {
    var compose = state.compose;
    if (!compose || !compose.accountId) {
      return;
    }

    var recipients = parseRecipients(compose.to);
    if (recipients.length === 0) {
      state.error = 'Add at least one recipient.';
      render();
      return;
    }

    if (!compose.subject.trim()) {
      state.error = 'Add a subject.';
      render();
      return;
    }

    var payload = {
      to: recipients,
      cc: parseRecipients(compose.cc),
      bcc: parseRecipients(compose.bcc),
      subject: compose.subject,
      bodyPlainText: compose.body,
      bodyHtml: null,
      attachments: compose.attachments.map(function (attachment) {
        return {
          storageKey: attachment.storageKey,
          fileName: attachment.fileName,
          contentType: attachment.contentType,
          size: attachment.size
        };
      })
    };

    await runAction(async function () {
      await apiJson(API + '/accounts/' + compose.accountId + '/send', 'POST', payload);
      state.compose = null;
      state.view = 'mailbox';
      state.notice = 'Message sent.';
      await loadMailboxes(false);
    });
  }

  async function uploadAttachment(file) {
    var compose = state.compose;
    if (!compose) {
      return;
    }

    compose.uploading += 1;
    render();

    try {
      var form = new FormData();
      form.append('file', file, file.name);

      var response = await fetch(API + '/upload-attachment', {
        method: 'POST',
        credentials: 'include',
        body: form
      });

      var text = await response.text();
      var payload = text ? JSON.parse(text) : null;

      if (!response.ok) {
        throw new ApiError(response.status, payload && payload.error ? payload.error.message : 'Upload failed.');
      }

      var data = payload && payload.data ? payload.data : payload;
      compose.attachments.push({
        storageKey: data.storageKey,
        fileName: data.fileName,
        contentType: data.contentType,
        size: data.size
      });
    } catch (error) {
      state.error = 'Could not attach "' + file.name + '": ' + (error && error.message ? error.message : 'unknown error');
    } finally {
      compose.uploading -= 1;
      render();
    }
  }

  async function addImapAccount(fields) {
    var credentials = {
      imapHost: fields.imapHost,
      imapPort: Number(fields.imapPort) || 993,
      imapUseSsl: true,
      smtpHost: fields.smtpHost,
      smtpPort: Number(fields.smtpPort) || 587,
      smtpUseStartTls: Number(fields.smtpPort) !== 465,
      username: fields.username,
      password: fields.password
    };

    await runAction(async function () {
      await apiJson(API + '/accounts', 'POST', {
        providerType: 0,
        displayName: fields.displayName,
        emailAddress: fields.emailAddress,
        credentialsJson: JSON.stringify(credentials)
      });

      await loadAccounts();
      state.view = state.selectedMailboxId ? 'mailbox' : 'welcome';
      state.notice = 'Account added. Use "Sync now" to fetch messages.';
    });
  }

  function startGmailOAuth() {
    var url = absoluteUrl(API + '/gmail/oauth/start');

    if (bridge && typeof bridge.openExternal === 'function') {
      bridge.openExternal(url);
      return;
    }

    window.open(url, '_blank', 'noopener,noreferrer');
  }

  function openAttachment(attachmentId) {
    var url = absoluteUrl(API + '/attachments/' + attachmentId + '/download');

    if (bridge && typeof bridge.openExternal === 'function') {
      bridge.openExternal(url);
      return;
    }

    window.open(url, '_blank', 'noopener,noreferrer');
  }

  async function syncAccount() {
    var account = selectedAccount();
    if (!account) {
      return;
    }

    await runAction(async function () {
      await apiFetch(API + '/accounts/' + account.id + '/sync', { method: 'POST' });
      await loadMailboxes(true);
      state.notice = 'Sync started. New messages appear as they arrive.';
    });
  }

  async function deleteAccount() {
    var account = selectedAccount();
    if (!account) {
      return;
    }

    var confirmed = await confirmDialog({
      title: 'Delete account',
      message: 'Delete ' + account.emailAddress + ' and every synced message? This cannot be undone.',
      confirmLabel: 'Delete account',
      danger: true
    });

    if (!confirmed) {
      return;
    }

    await runAction(async function () {
      await apiFetch(API + '/accounts/' + account.id, { method: 'DELETE' });
      state.selectedThread = null;
      state.messages = [];
      state.threads = [];
      await loadAccounts();
      state.view = state.accounts.length > 0 ? 'mailbox' : 'welcome';
      state.notice = 'Account deleted.';
    });
  }

  function newRule() {
    state.rule = {
      id: null,
      name: '',
      accountId: state.selectedAccountId,
      isEnabled: true,
      priority: state.rules.length + 1,
      stopProcessing: false,
      conditionGroups: [],
      actions: []
    };
    state.view = 'rule';
    render();
  }

  function editRule(rule) {
    state.rule = {
      id: rule.id,
      name: rule.name,
      accountId: rule.accountId || null,
      isEnabled: rule.isEnabled !== false,
      priority: rule.priority || 0,
      stopProcessing: !!rule.stopProcessing,
      conditionGroups: (rule.conditionGroups || []).map(function (group) {
        return {
          matchMode: Number(group.matchMode) || 0,
          conditions: (group.conditions || []).map(function (condition) {
            return {
              field: Number(condition.field) || 0,
              operator: Number(condition.operator) || 0,
              value: condition.value || ''
            };
          })
        };
      }),
      actions: (rule.actions || []).map(function (action) {
        return { actionType: Number(action.actionType) || 0, targetValue: action.targetValue || '' };
      })
    };
    state.view = 'rule';
    render();
  }

  async function saveRule() {
    var rule = state.rule;
    if (!rule) {
      return;
    }

    if (!rule.name.trim()) {
      state.error = 'Give the rule a name.';
      render();
      return;
    }

    var groups = rule.conditionGroups.map(function (group) {
      return {
        matchMode: Number(group.matchMode) || 0,
        conditions: group.conditions.map(function (condition) {
          return {
            field: Number(condition.field) || 0,
            operator: Number(condition.operator) || 0,
            value: condition.value || ''
          };
        })
      };
    });

    var actions = rule.actions.map(function (action) {
      return {
        actionType: Number(action.actionType) || 0,
        targetValue: action.targetValue || null
      };
    });

    await runAction(async function () {
      if (rule.id) {
        await apiJson(API + '/rules/' + rule.id, 'PUT', {
          name: rule.name,
          isEnabled: rule.isEnabled,
          priority: Number(rule.priority) || 0,
          stopProcessing: rule.stopProcessing,
          conditionGroups: groups,
          actions: actions
        });
      } else {
        await apiJson(API + '/rules', 'POST', {
          name: rule.name,
          accountId: rule.accountId,
          isEnabled: rule.isEnabled,
          priority: Number(rule.priority) || 0,
          stopProcessing: rule.stopProcessing,
          conditionGroups: groups,
          actions: actions
        });
      }

      await reloadRules();
      state.rule = null;
      state.view = 'rules';
      state.notice = 'Rule saved.';
    });
  }

  async function deleteRule(rule) {
    var confirmed = await confirmDialog({
      title: 'Delete rule',
      message: 'Delete the rule "' + rule.name + '"?',
      confirmLabel: 'Delete',
      danger: true
    });

    if (!confirmed) {
      return;
    }

    await runAction(async function () {
      await apiFetch(API + '/rules/' + rule.id, { method: 'DELETE' });
      await reloadRules();
      state.notice = 'Rule deleted.';
    });
  }

  async function runRules() {
    await runAction(async function () {
      var account = selectedAccount();
      var query = account ? '?accountId=' + account.id : '';
      var result = await apiFetch(API + '/rules/run' + query, { method: 'POST' });
      state.notice = 'Rules ran on ' + (result && result.executed ? result.executed : 0) + ' message(s).';
    });
  }

  /* -------------------------------------------------------------- rendering */

  function render() {
    renderSidebar();
    renderStatus();
    renderPanel();
    paintIcons();
  }

  function renderSidebar() {
    el.sidebar.classList.toggle('is-collapsed', state.sidebarCollapsed);
    el.sidebarToggle.setAttribute('aria-label', state.sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar');
    el.sidebarToggle.title = state.sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar';
    el.sidebarToggle.textContent = '';
    el.sidebarToggle.appendChild(buildIcon(state.sidebarCollapsed ? 'chevron_right' : 'chevron_left'));

    el.accountSection.hidden = state.accounts.length === 0;
    el.accountSelect.textContent = '';
    state.accounts.forEach(function (account) {
      var option = document.createElement('option');
      option.value = account.id;
      option.textContent = account.displayName || account.emailAddress;
      option.selected = account.id === state.selectedAccountId;
      el.accountSelect.appendChild(option);
    });

    el.mailboxSection.hidden = state.mailboxes.length === 0;
    el.mailboxEmpty.hidden = state.mailboxes.length > 0;
    el.mailboxList.textContent = '';

    state.mailboxes.forEach(function (mailbox) {
      var button = node('button', 'nav-item' + (mailbox.id === state.selectedMailboxId && state.view !== 'rules' ? ' is-active' : ''));
      button.type = 'button';
      button.title = mailbox.displayName;
      var icon = node('span');
      icon.setAttribute('data-icon', mailboxIcon(mailbox));
      button.appendChild(icon);
      button.appendChild(node('span', 'nav-text', mailbox.displayName));
      button.addEventListener('click', function () {
        selectMailbox(mailbox.id);
      });
      el.mailboxList.appendChild(button);
    });

    el.navRules.classList.toggle('is-active', state.view === 'rules' || state.view === 'rule');
    el.navRules.disabled = state.busy;
    el.navSync.disabled = state.busy || !selectedAccount();
    el.navDeleteAccount.disabled = state.busy || !selectedAccount();
    paintIcons(el.sidebar);
  }

  function renderStatus() {
    if (state.notice) {
      el.banner.hidden = false;
      el.banner.className = 'banner is-success';
      el.bannerText.textContent = state.notice;
    } else if (state.error) {
      el.banner.hidden = false;
      el.banner.className = 'banner';
      el.bannerText.textContent = state.error;
    } else {
      el.banner.hidden = true;
      el.bannerText.textContent = '';
    }

    if (state.busy) {
      el.status.hidden = false;
      el.status.className = 'status is-info';
      el.status.textContent = 'Working…';
    } else {
      el.status.hidden = true;
      el.status.textContent = '';
    }
  }

  function emptyState(icon, title, message) {
    var wrapper = node('div', 'empty');
    var iconHolder = node('span', 'empty-icon');
    iconHolder.appendChild(buildIcon(icon));
    wrapper.appendChild(iconHolder);
    wrapper.appendChild(node('h3', '', title));
    if (message) {
      wrapper.appendChild(node('p', '', message));
    }
    return wrapper;
  }

  function renderPanel() {
    el.panel.textContent = '';

    switch (state.view) {
      case 'addAccount':
        el.panel.appendChild(renderAddAccount());
        break;
      case 'rules':
        el.panel.appendChild(renderRules());
        break;
      case 'rule':
        el.panel.appendChild(state.rule ? renderRuleEditor() : renderRules());
        break;
      case 'compose':
        el.panel.appendChild(state.compose ? renderCompose() : renderThreadList());
        break;
      case 'thread':
        el.panel.appendChild(state.selectedThread ? renderThreadDetail() : renderThreadList());
        break;
      case 'help':
        el.panel.appendChild(renderHelp());
        break;
      case 'mailbox':
        el.panel.appendChild(renderThreadList());
        break;
      default:
        el.panel.appendChild(renderWelcome());
        break;
    }
  }

  function renderWelcome() {
    var wrapper = emptyState(
      'mail',
      state.accounts.length === 0 ? 'Welcome to Email' : 'No folder selected',
      state.accounts.length === 0
        ? 'Add your first email account to get started.'
        : 'Select a folder in the sidebar to view your mail.');

    if (state.accounts.length === 0) {
      var add = node('button', 'btn btn-primary', 'Add account');
      add.type = 'button';
      add.addEventListener('click', function () {
        state.view = 'addAccount';
        render();
      });
      wrapper.appendChild(add);
    }

    return wrapper;
  }

  function renderAddAccount() {
    var card = node('div', 'card');
    var header = node('div', 'card-header');
    header.appendChild(node('h3', '', 'Add email account'));
    header.appendChild(iconButton('close', 'Cancel', function () {
      state.view = selectedAccount() ? 'mailbox' : 'welcome';
      render();
    }));
    card.appendChild(header);

    var tabs = node('div', 'form-actions');
    var imapTab = node('button', 'btn btn-secondary', 'IMAP / SMTP');
    var gmailTab = node('button', 'btn btn-secondary', 'Gmail (OAuth)');
    imapTab.type = 'button';
    gmailTab.type = 'button';

    var container = node('div');
    card.appendChild(tabs);
    card.appendChild(container);

    function showImap() {
      container.textContent = '';
      var fields = {};

      function field(name, label, type, placeholder, value) {
        var wrapper = node('label', 'field');
        wrapper.appendChild(node('span', '', label));
        var input = document.createElement('input');
        input.className = 'input';
        input.type = type || 'text';
        input.placeholder = placeholder || '';
        if (value !== undefined) {
          input.value = value;
        }
        wrapper.appendChild(input);
        fields[name] = input;
        return wrapper;
      }

      container.appendChild(field('displayName', 'Display name', 'text', 'Work email'));
      container.appendChild(field('emailAddress', 'Email address', 'email', 'user@example.com'));
      container.appendChild(field('imapHost', 'IMAP host', 'text', 'imap.example.com'));
      container.appendChild(field('imapPort', 'IMAP port', 'number', '993', '993'));
      container.appendChild(field('smtpHost', 'SMTP host', 'text', 'smtp.example.com'));
      container.appendChild(field('smtpPort', 'SMTP port', 'number', '587', '587'));
      container.appendChild(field('username', 'Username', 'text', 'user@example.com'));
      container.appendChild(field('password', 'Password', 'password', ''));

      var actions = node('div', 'form-actions');
      var submit = node('button', 'btn btn-primary', 'Add account');
      submit.type = 'button';
      submit.disabled = state.busy;
      submit.addEventListener('click', function () {
        var values = {};
        Object.keys(fields).forEach(function (name) {
          values[name] = fields[name].value;
        });
        addImapAccount(values);
      });
      actions.appendChild(submit);
      container.appendChild(actions);
    }

    function showGmail() {
      container.textContent = '';

      if (!state.gmailConfigured) {
        var info = node('div');
        info.appendChild(node('p', '', 'Gmail is not configured on this server.'));
        info.appendChild(node(
          'p',
          'hint',
          'An administrator must register a Google OAuth client before accounts can be connected here.'));
        container.appendChild(info);
        return;
      }

      var text = node('p', '', 'Connect a Gmail account through Google. The consent page opens in a new tab.');
      container.appendChild(text);

      var actions = node('div', 'form-actions');
      var connect = node('button', 'btn btn-primary', 'Connect Gmail');
      connect.type = 'button';
      connect.addEventListener('click', startGmailOAuth);
      actions.appendChild(connect);
      container.appendChild(actions);
    }

    imapTab.addEventListener('click', showImap);
    gmailTab.addEventListener('click', showGmail);
    tabs.appendChild(imapTab);
    tabs.appendChild(gmailTab);

    showImap();

    return card;
  }

  function renderRules() {
    var wrapper = node('div');
    var header = node('div', 'card-header');
    header.appendChild(node('h3', '', 'Email rules'));
    var actions = node('div', 'form-actions');
    var add = node('button', 'btn btn-primary', 'New rule');
    add.type = 'button';
    add.addEventListener('click', newRule);
    actions.appendChild(add);
    header.appendChild(actions);
    wrapper.appendChild(header);
    wrapper.appendChild(node('p', 'hint', 'Rules run in priority order on newly synced mail.'));

    if (state.rules.length === 0) {
      wrapper.appendChild(emptyState('settings', 'No rules yet', 'Create a rule to sort incoming mail automatically.'));
    } else {
      state.rules
        .slice()
        .sort(function (left, right) {
          return (left.priority || 0) - (right.priority || 0);
        })
        .forEach(function (rule) {
          var card = node('div', 'rule-card' + (rule.isEnabled ? '' : ' is-disabled'));
          var info = node('div', 'rule-info');
          info.appendChild(node('strong', '', rule.name));

          var meta = node('span', 'rule-meta', 'Priority ' + (rule.priority || 0));
          info.appendChild(meta);

          if (!rule.isEnabled) {
            info.appendChild(node('span', 'rule-badge', 'Disabled'));
          }

          card.appendChild(info);

          var buttons = node('div', 'form-actions');
          var edit = node('button', 'btn btn-secondary', 'Edit');
          edit.type = 'button';
          edit.addEventListener('click', function () {
            editRule(rule);
          });

          var remove = node('button', 'btn btn-danger', 'Delete');
          remove.type = 'button';
          remove.addEventListener('click', function () {
            deleteRule(rule);
          });

          buttons.appendChild(edit);
          buttons.appendChild(remove);
          card.appendChild(buttons);
          wrapper.appendChild(card);
        });
    }

    var run = node('button', 'btn btn-secondary', 'Run rules now');
    run.type = 'button';
    run.disabled = state.busy;
    run.addEventListener('click', runRules);

    var footer = node('div', 'form-actions');
    footer.appendChild(run);
    wrapper.appendChild(footer);
    return wrapper;
  }

  var RULE_FIELDS = [
    { value: 0, label: 'From' },
    { value: 1, label: 'To' },
    { value: 2, label: 'Cc' },
    { value: 3, label: 'Subject' },
    { value: 4, label: 'Body' },
    { value: 5, label: 'Has attachment' }
  ];

  var RULE_OPERATORS = [
    { value: 0, label: 'contains' },
    { value: 1, label: 'equals' },
    { value: 2, label: 'starts with' },
    { value: 3, label: 'ends with' },
    { value: 4, label: 'matches regex' }
  ];

  var RULE_ACTIONS = [
    { value: 0, label: 'Mark as read' },
    { value: 1, label: 'Mark as unread' },
    { value: 2, label: 'Star' },
    { value: 3, label: 'Unstar' },
    { value: 4, label: 'Move to folder' },
    { value: 5, label: 'Apply label' },
    { value: 6, label: 'Archive' }
  ];

  function selectFrom(options, selected, onChange) {
    var select = document.createElement('select');
    select.className = 'select';
    options.forEach(function (option) {
      var item = document.createElement('option');
      item.value = String(option.value);
      item.textContent = option.label;
      item.selected = String(option.value) === String(selected === undefined || selected === null ? '' : selected);
      select.appendChild(item);
    });
    if (onChange) {
      select.addEventListener('change', function () {
        onChange(Number(select.value));
      });
    }
    return select;
  }

  function renderRuleEditor() {
    var rule = state.rule;
    var wrapper = node('div', 'card');

    var header = node('div', 'card-header');
    header.appendChild(node('h3', '', rule.id ? 'Edit rule' : 'New rule'));
    header.appendChild(iconButton('close', 'Cancel', function () {
      state.rule = null;
      state.view = 'rules';
      render();
    }));
    wrapper.appendChild(header);

    var nameField = node('label', 'field');
    nameField.appendChild(node('span', '', 'Rule name'));
    var nameInput = document.createElement('input');
    nameInput.className = 'input';
    nameInput.value = rule.name;
    nameInput.addEventListener('input', function () {
      rule.name = nameInput.value;
    });
    nameField.appendChild(nameInput);
    wrapper.appendChild(nameField);

    var row = node('div', 'rule-row');

    var accountField = node('label', 'field');
    accountField.appendChild(node('span', '', 'Applies to'));
    var accountOptions = [{ value: '', label: 'All accounts' }].concat(state.accounts.map(function (account) {
      return { value: account.id, label: account.displayName || account.emailAddress };
    }));
    var accountSelect = selectFrom(accountOptions, rule.accountId || '', function (value) {
      rule.accountId = value || null;
    });
    accountField.appendChild(accountSelect);
    row.appendChild(accountField);

    var priorityField = node('label', 'field');
    priorityField.appendChild(node('span', '', 'Priority'));
    var priorityInput = document.createElement('input');
    priorityInput.className = 'input';
    priorityInput.type = 'number';
    priorityInput.min = '0';
    priorityInput.value = String(rule.priority);
    priorityInput.addEventListener('input', function () {
      rule.priority = Number(priorityInput.value) || 0;
    });
    priorityField.appendChild(priorityInput);
    row.appendChild(priorityField);
    wrapper.appendChild(row);

    var toggles = node('div', 'form-actions');
    var enabledLabel = node('label', 'check-row');
    var enabledInput = document.createElement('input');
    enabledInput.type = 'checkbox';
    enabledInput.checked = rule.isEnabled;
    enabledInput.addEventListener('change', function () {
      rule.isEnabled = enabledInput.checked;
    });
    enabledLabel.appendChild(enabledInput);
    enabledLabel.appendChild(node('span', '', 'Enabled'));
    toggles.appendChild(enabledLabel);

    var stopLabel = node('label', 'check-row');
    var stopInput = document.createElement('input');
    stopInput.type = 'checkbox';
    stopInput.checked = rule.stopProcessing;
    stopInput.addEventListener('change', function () {
      rule.stopProcessing = stopInput.checked;
    });
    stopLabel.appendChild(stopInput);
    stopLabel.appendChild(node('span', '', 'Stop processing later rules'));
    toggles.appendChild(stopLabel);
    wrapper.appendChild(toggles);

    wrapper.appendChild(node('p', 'section-title', 'Conditions'));

    rule.conditionGroups.forEach(function (group, groupIndex) {
      var groupCard = node('div', 'rule-group');

      var groupRow = node('div', 'rule-row');
      groupRow.appendChild(selectFrom(
        [{ value: 0, label: 'Match ALL' }, { value: 1, label: 'Match ANY' }],
        group.matchMode,
        function (value) {
          group.matchMode = value;
        }));

      groupRow.appendChild(iconButton('delete', 'Remove group', function () {
        rule.conditionGroups.splice(groupIndex, 1);
        render();
      }));
      groupCard.appendChild(groupRow);

      group.conditions.forEach(function (condition, conditionIndex) {
        var conditionRow = node('div', 'rule-row');

        conditionRow.appendChild(selectFrom(RULE_FIELDS, condition.field, function (value) {
          condition.field = value;
        }));
        conditionRow.appendChild(selectFrom(RULE_OPERATORS, condition.operator, function (value) {
          condition.operator = value;
        }));

        var valueInput = document.createElement('input');
        valueInput.className = 'input';
        valueInput.placeholder = 'Value';
        valueInput.value = condition.value;
        valueInput.addEventListener('input', function () {
          condition.value = valueInput.value;
        });
        conditionRow.appendChild(valueInput);

        conditionRow.appendChild(iconButton('close', 'Remove condition', function () {
          group.conditions.splice(conditionIndex, 1);
          render();
        }, 'rule-remove'));

        groupCard.appendChild(conditionRow);
      });

      var addCondition = node('button', 'btn btn-secondary', 'Add condition');
      addCondition.type = 'button';
      addCondition.addEventListener('click', function () {
        group.conditions.push({ field: 3, operator: 0, value: '' });
        render();
      });
      groupCard.appendChild(addCondition);

      wrapper.appendChild(groupCard);
    });

    var addGroup = node('button', 'btn btn-secondary', 'Add condition group');
    addGroup.type = 'button';
    addGroup.addEventListener('click', function () {
      rule.conditionGroups.push({
        matchMode: 0,
        conditions: [{ field: 3, operator: 0, value: '' }]
      });
      render();
    });
    wrapper.appendChild(addGroup);

    wrapper.appendChild(node('p', 'section-title', 'Actions'));

    rule.actions.forEach(function (action, actionIndex) {
      var actionRow = node('div', 'rule-row');

      actionRow.appendChild(selectFrom(RULE_ACTIONS, action.actionType, function (value) {
        action.actionType = value;
        render();
      }));

      if (action.actionType === 4 || action.actionType === 5) {
        var targetInput = document.createElement('input');
        targetInput.className = 'input';
        targetInput.placeholder = 'Folder or label';
        targetInput.value = action.targetValue || '';
        targetInput.addEventListener('input', function () {
          action.targetValue = targetInput.value;
        });
        actionRow.appendChild(targetInput);
      }

      actionRow.appendChild(iconButton('close', 'Remove action', function () {
        rule.actions.splice(actionIndex, 1);
        render();
      }, 'rule-remove'));

      wrapper.appendChild(actionRow);
    });

    var addAction = node('button', 'btn btn-secondary', 'Add action');
    addAction.type = 'button';
    addAction.addEventListener('click', function () {
      rule.actions.push({ actionType: 0, targetValue: '' });
      render();
    });
    wrapper.appendChild(addAction);

    var footer = node('div', 'form-actions');
    var save = node('button', 'btn btn-primary', 'Save rule');
    save.type = 'button';
    save.disabled = state.busy;
    save.addEventListener('click', saveRule);
    footer.appendChild(save);
    wrapper.appendChild(footer);

    return wrapper;
  }

  function renderCompose() {
    var compose = state.compose;
    var card = node('div', 'compose-card');

    var header = node('div', 'compose-header');
    header.appendChild(node('h3', '', 'New message'));
    header.appendChild(iconButton('close', 'Discard', function () {
      state.compose = null;
      state.view = 'mailbox';
      render();
    }));
    card.appendChild(header);

    function row(labelText, control) {
      var wrapper = node('div', 'compose-row');
      wrapper.appendChild(node('span', 'compose-label', labelText));
      wrapper.appendChild(control);
      card.appendChild(wrapper);
    }

    var from = document.createElement('select');
    from.className = 'select';
    state.accounts.forEach(function (account) {
      var option = document.createElement('option');
      option.value = account.id;
      option.textContent = (account.displayName || account.emailAddress) + ' <' + account.emailAddress + '>';
      option.selected = account.id === compose.accountId;
      from.appendChild(option);
    });
    from.addEventListener('change', function () {
      compose.accountId = from.value;
    });
    row('From', from);

    function inputRow(labelText, field) {
      var input = document.createElement('input');
      input.className = 'input';
      input.value = compose[field] || '';
      input.placeholder = field === 'to' ? 'Recipient email address' : '';
      input.autocomplete = 'off';
      input.addEventListener('input', function () {
        compose[field] = input.value;
      });
      row(labelText, input);
      return input;
    }

    inputRow('To', 'to');
    inputRow('Cc', 'cc');
    inputRow('Bcc', 'bcc');
    inputRow('Subject', 'subject');

    var attachmentArea = node('div', 'compose-attachments');
    if (compose.attachments.length > 0) {
      var chips = node('div', 'chip-list');
      compose.attachments.forEach(function (attachment, index) {
        var chip = node('span', 'chip');
        chip.appendChild(buildIcon(fileTypeIcon(attachment.contentType)));
        chip.appendChild(node('span', '', attachment.fileName));
        chip.appendChild(node('span', 'attachment-size', formatSize(attachment.size)));
        chip.appendChild(iconButton('close', 'Remove attachment', function () {
          compose.attachments.splice(index, 1);
          render();
        }));
        chips.appendChild(chip);
      });
      attachmentArea.appendChild(chips);
    }

    var attachRow = node('div', 'form-actions');
    var attachLabel = node('label', 'btn btn-secondary');
    attachLabel.appendChild(node('span', '', 'Attach file'));
    var fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.multiple = true;
    fileInput.hidden = true;
    fileInput.addEventListener('change', function () {
      var files = Array.prototype.slice.call(fileInput.files || []);
      files.forEach(function (file) {
        uploadAttachment(file);
      });
      fileInput.value = '';
    });
    attachLabel.appendChild(fileInput);
    attachRow.appendChild(attachLabel);

    if (compose.uploading > 0) {
      attachRow.appendChild(node('span', 'hint', 'Uploading…'));
    }

    attachmentArea.appendChild(attachRow);
    card.appendChild(attachmentArea);

    var body = node('div', 'compose-body');
    var textarea = document.createElement('textarea');
    textarea.className = 'textarea';
    textarea.placeholder = 'Write your message…';
    textarea.value = compose.body || '';
    textarea.addEventListener('input', function () {
      compose.body = textarea.value;
    });
    body.appendChild(textarea);
    card.appendChild(body);

    var footer = node('div', 'compose-footer');
    var left = node('div', 'form-actions');
    var send = node('button', 'btn btn-primary', 'Send');
    send.type = 'button';
    send.disabled = state.busy || compose.uploading > 0;
    send.addEventListener('click', sendCompose);
    left.appendChild(send);

    var discard = node('button', 'btn btn-secondary', 'Discard');
    discard.type = 'button';
    discard.addEventListener('click', function () {
      state.compose = null;
      state.view = 'mailbox';
      render();
    });
    left.appendChild(discard);
    footer.appendChild(left);

    footer.appendChild(node(
      'span',
      'compose-hint',
      compose.to && compose.subject
        ? (compose.attachments.length > 0 ? compose.attachments.length + ' attachment(s) — ready to send' : 'Ready to send')
        : 'Enter a recipient and a subject to send'));

    card.appendChild(footer);

    return card;
  }

  function renderThreadList() {
    var mailbox = selectedMailbox();

    if (!mailbox) {
      return emptyState('mail', 'No folder selected', 'Choose a folder in the sidebar.');
    }

    if (state.threads.length === 0) {
      return emptyState('mail', 'No messages', 'This folder is empty, or messages have not synced yet.');
    }

    var list = node('div', 'thread-list');
    state.threads
      .slice()
      .sort(function (left, right) {
        return new Date(right.lastMessageAt || 0) - new Date(left.lastMessageAt || 0);
      })
      .forEach(function (thread) {
        var card = node('button', 'thread-card');
        card.type = 'button';
        card.appendChild(node('span', 'thread-subject', thread.subject || '(No subject)'));

        var meta = node('span', 'thread-meta');
        meta.appendChild(node('span', 'thread-participants', formatParticipants(thread.participantsJson)));
        meta.appendChild(node('span', 'thread-count', String(thread.messageCount || 0)));

        var date = thread.lastMessageAt ? new Date(thread.lastMessageAt) : null;
        meta.appendChild(node('span', '', date && !isNaN(date.getTime())
          ? date.toLocaleDateString(undefined, { month: 'short', day: '2-digit' })
          : ''));
        card.appendChild(meta);

        card.addEventListener('click', function () {
          runAction(function () {
            return openThread(thread);
          });
        });

        list.appendChild(card);
      });

    return list;
  }

  function renderMessageBody(entry) {
    var message = entry.message;

    // The email's own HTML is untrusted: it renders inside a nested frame with sandbox="" (no
    // scripts, no forms, no same-origin), which is the sanitizer. Nothing from the server is ever
    // assigned to this document's innerHTML.
    var frame = document.createElement('iframe');
    frame.className = 'message-body-frame';
    frame.setAttribute('sandbox', '');
    frame.setAttribute('title', 'Message content');

    var inner = entry.bodyHtml && entry.bodyHtml.trim()
      ? entry.bodyHtml
      : plainTextToHtml(message.bodyPreview || '');

    frame.srcdoc = '<!DOCTYPE html><html><head><meta charset="utf-8">'
      + '<base target="_blank">'
      + '<style>body{margin:0;padding:8px;font:14px/1.5 system-ui,sans-serif;color:#1f2933;'
      + 'background:#fff;overflow-wrap:anywhere}img{max-width:100%;height:auto}'
      + 'blockquote{margin:0 0 0 12px;padding-left:10px;border-left:2px solid #dfe3e8;color:#5f6b7a}'
      + 'a{color:#2563eb}</style></head><body>'
      + inner
      + '</body></html>';

    return frame;
  }

  function renderThreadDetail() {
    var thread = state.selectedThread;
    var wrapper = node('div');

    var header = node('div', 'thread-header');
    var back = node('button', 'btn btn-ghost');
    back.type = 'button';
    back.appendChild(buildIcon('arrow_back'));
    back.appendChild(node('span', '', 'Back'));
    back.addEventListener('click', function () {
      state.selectedThread = null;
      state.messages = [];
      state.view = 'mailbox';
      render();
    });
    header.appendChild(back);
    header.appendChild(node('h3', '', thread.subject || '(No subject)'));

    var reply = node('button', 'btn btn-primary', 'Reply');
    reply.type = 'button';
    reply.addEventListener('click', function () {
      replyToThread(thread, 'Re: ');
    });
    header.appendChild(reply);

    var forward = node('button', 'btn btn-secondary', 'Forward');
    forward.type = 'button';
    forward.addEventListener('click', function () {
      replyToThread(thread, 'Fwd: ');
    });
    header.appendChild(forward);
    wrapper.appendChild(header);

    if (state.messages.length === 0) {
      wrapper.appendChild(node('p', 'hint', 'Loading messages…'));
      return wrapper;
    }

    state.messages.forEach(function (entry) {
      var message = entry.message;
      var card = node('div', 'message');

      var messageHeader = node('div', 'message-header');
      messageHeader.appendChild(node('span', 'message-from', formatFrom(message.fromJson)));
      messageHeader.appendChild(node(
        'span',
        'message-date',
        formatDate(message.dateReceived || message.createdAt)));
      card.appendChild(messageHeader);

      card.appendChild(renderMessageBody(entry));

      if (message.attachments && message.attachments.length > 0) {
        var attachments = node('div', 'attachments');
        message.attachments.forEach(function (attachment) {
          var chip = node('button', 'attachment');
          chip.type = 'button';
          chip.title = attachment.fileName + ' (' + formatSize(attachment.size) + ')';
          chip.appendChild(buildIcon(fileTypeIcon(attachment.contentType)));
          chip.appendChild(node('span', '', attachment.fileName));
          chip.appendChild(node('span', 'attachment-size', formatSize(attachment.size)));
          chip.addEventListener('click', function () {
            openAttachment(attachment.id);
          });
          attachments.appendChild(chip);
        });
        card.appendChild(attachments);
      }

      wrapper.appendChild(card);
    });

    return wrapper;
  }

  function renderHelp() {
    var wrapper = node('div');

    function section(title, paragraphs) {
      var block = node('section', 'help-section');
      block.appendChild(node('h2', '', title));
      paragraphs.forEach(function (text) {
        block.appendChild(node('p', '', text));
      });
      wrapper.appendChild(block);
    }

    section('Overview', [
      'DotNetCloud Email is a web-based email client. Add one or more accounts (IMAP/SMTP or Gmail), read and organise your messages, and compose mail from your browser.'
    ]);
    section('Accounts', [
      'Add an account with "+ Account" in the sidebar. Choose IMAP/SMTP and enter your server, port, username and password, or Gmail if your administrator configured Google OAuth.',
      'Switch accounts with the account selector. "Sync now" fetches new mail; "Delete account" removes the account and its synced messages.'
    ]);
    section('Composing', [
      'Compose opens the editor. Pick the account to send from, add recipients (Cc and Bcc are available), a subject and the message body.',
      'Attach files with the "Attach file" button. Attachments are uploaded to this module before the message is sent.'
    ]);
    section('Reading', [
      'Click a folder to list its threads, then a thread to open it. Message content is rendered in an isolated frame, so a message cannot run scripts or reach your session.',
      'Attachments download through a tab opened by DotNetCloud itself.'
    ]);
    section('Rules', [
      'Rules sort incoming mail automatically. Add conditions (from, subject, body, attachments) and actions (mark read, star, move, archive); rules run in priority order and can stop later rules.'
    ]);

    var tips = node('ul', 'help-tips');
    ['Use rules to file newsletters automatically.', 'Reply and Forward are in the thread header.', 'Attachments are deduplicated, so the same file is stored once.']
      .forEach(function (tip) {
        tips.appendChild(node('li', '', tip));
      });
    wrapper.appendChild(tips);

    return wrapper;
  }

  /**
   * Opens the thread named by ?threadId=, scanning accounts and mailboxes when it is not in the
   * folder that happens to be selected. Used by the Home-page widget's deep link.
   */
  async function openThreadFromQuery(threadId) {
    if (!threadId) {
      return;
    }

    var current = state.threads.filter(function (thread) {
      return thread.id === threadId;
    })[0];

    if (current) {
      await openThread(current);
      return;
    }

    for (var accountIndex = 0; accountIndex < state.accounts.length; accountIndex += 1) {
      var account = state.accounts[accountIndex];
      var mailboxes;

      try {
        mailboxes = (await apiFetch(API + '/accounts/' + account.id + '/mailboxes')) || [];
      } catch (error) {
        continue;
      }

      for (var mailboxIndex = 0; mailboxIndex < mailboxes.length; mailboxIndex += 1) {
        var mailbox = mailboxes[mailboxIndex];
        var threads;

        try {
          threads = (await apiFetch(
            API + '/accounts/' + account.id + '/mailboxes/' + mailbox.id + '/threads')) || [];
        } catch (error) {
          continue;
        }

        var match = threads.filter(function (thread) {
          return thread.id === threadId;
        })[0];

        if (match) {
          state.selectedAccountId = account.id;
          state.mailboxes = mailboxes;
          state.selectedMailboxId = mailbox.id;
          state.threads = threads;
          await openThread(match);
          return;
        }
      }
    }

    state.notice = 'That message could not be found.';
    render();
  }

  /* ------------------------------------------------------------------ boot */

  /**
   * Reads the one-shot parameters the OAuth callback and the widget deep link append, and removes
   * them from the address bar so a reload does not replay them.
   */
  function readStatusParams() {
    var params = new URLSearchParams(window.location.search);
    var result = { threadId: params.get('threadId') || null, notice: null, error: null };
    var status = params.get('gmail');

    if (status === 'connected') {
      result.notice = 'Gmail account connected.';
    } else if (status === 'denied') {
      result.notice = 'Gmail authorisation was cancelled.';
    } else if (status === 'error') {
      var reason = params.get('reason');
      result.error = reason === 'not_configured'
        ? 'Gmail OAuth is not configured by the server administrator.'
        : reason === 'not_signed_in'
          ? 'Sign in to DotNetCloud, then try connecting Gmail again.'
          : 'Gmail authorisation failed. Try again.';
    }

    if (status || result.threadId) {
      try {
        window.history.replaceState(null, '', window.location.pathname);
      } catch (error) {
        /* an opaque-origin document may refuse history updates; the banner is harmless */
      }
    }

    return result;
  }

  async function boot() {
    el.sidebar = document.getElementById('sidebar');
    el.sidebarToggle = document.getElementById('sidebar-toggle');
    el.accountSection = document.getElementById('account-section');
    el.accountSelect = document.getElementById('account-select');
    el.mailboxSection = document.getElementById('mailbox-section');
    el.mailboxList = document.getElementById('mailbox-list');
    el.mailboxEmpty = document.getElementById('mailbox-empty');
    el.navRules = document.getElementById('nav-rules');
    el.navSync = document.getElementById('nav-sync');
    el.navDeleteAccount = document.getElementById('nav-delete-account');
    el.banner = document.getElementById('banner');
    el.bannerText = document.getElementById('banner-text');
    el.status = document.getElementById('status');
    el.panel = document.getElementById('panel');

    if (bridge) {
      bridge
        .on('theme', function (info) {
          document.documentElement.setAttribute('data-theme', info && info.theme === 'dark' ? 'dark' : 'light');
        })
        .start({ title: 'Email' });
    }

    el.sidebarToggle.addEventListener('click', function () {
      state.sidebarCollapsed = !state.sidebarCollapsed;
      render();
    });

    document.getElementById('compose-button').addEventListener('click', function () {
      beginCompose(null);
    });
    document.getElementById('account-button').addEventListener('click', function () {
      state.view = 'addAccount';
      render();
    });
    el.accountSelect.addEventListener('change', function () {
      selectAccount(el.accountSelect.value);
    });
    el.navRules.addEventListener('click', function () {
      runAction(async function () {
        await reloadRules();
        state.view = 'rules';
      });
    });
    el.navSync.addEventListener('click', syncAccount);
    el.navDeleteAccount.addEventListener('click', deleteAccount);
    document.getElementById('nav-help').addEventListener('click', function () {
      state.view = 'help';
      render();
    });
    document.getElementById('banner-close').addEventListener('click', function () {
      state.notice = null;
      state.error = null;
      render();
    });

    var params = readStatusParams();
    render();

    await runAction(async function () {
      await loadAccounts();
      await reloadRules();
      state.view = state.accounts.length > 0 ? 'mailbox' : 'welcome';
    });

    if (params.threadId) {
      await openThreadFromQuery(params.threadId);
    }

    if (params.notice || params.error) {
      state.notice = params.notice;
      state.error = params.error;
      render();
    }

    try {
      var status = await apiFetch(API + '/gmail/oauth/status');
      state.gmailConfigured = !!(status && status.configured);
    } catch (error) {
      state.gmailConfigured = false;
    }

    render();
  }

  document.addEventListener('DOMContentLoaded', boot);
})();
