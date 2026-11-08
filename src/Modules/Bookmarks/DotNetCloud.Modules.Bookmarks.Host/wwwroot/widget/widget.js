/*
 * Bookmarks home-page widget.
 *
 * A widget frame is the same opaque-origin sandbox as the module app, framed inside a shell card.
 * It reports its own height through the bridge (widget role) and opens links through the shell,
 * because a sandboxed frame may not navigate a top-level window.
 */
(function () {
  'use strict';

  var ENDPOINT = '../api/v1/bookmarks?skip=0&take=5';

  var message = document.getElementById('message');
  var list = document.getElementById('items');
  var bridge = window.dncModuleBridge || null;

  function setMessage(text) {
    if (!text) {
      message.hidden = true;
      message.textContent = '';
      return;
    }

    message.textContent = text;
    message.hidden = false;
  }

  function openUrl(url) {
    if (!url) {
      return;
    }

    if (bridge && typeof bridge.openExternal === 'function') {
      bridge.openExternal(url);
      return;
    }

    window.open(url, '_blank', 'noopener,noreferrer');
  }

  function render(items) {
    if (!items || items.length === 0) {
      list.hidden = true;
      setMessage('No bookmarks yet.');
      return;
    }

    setMessage('');
    list.hidden = false;
    list.textContent = '';

    items.forEach(function (item) {
      var row = document.createElement('li');
      row.className = 'widget-list-item';

      var link = document.createElement('a');
      link.className = 'widget-link';
      link.href = item.url || '#';
      link.rel = 'noopener noreferrer';
      // textContent: a bookmark title is user data.
      link.textContent = (item.title && item.title.trim()) || item.url || '(untitled)';
      link.addEventListener('click', function (event) {
        event.preventDefault();
        openUrl(item.url);
      });

      row.appendChild(link);
      list.appendChild(row);
    });

    if (bridge && typeof bridge.reportHeight === 'function') {
      bridge.reportHeight();
    }
  }

  async function load() {
    try {
      var response = await fetch(ENDPOINT, {
        credentials: 'same-origin',
        headers: { Accept: 'application/json' }
      });

      if (response.status === 401) {
        setMessage('Sign in to see your bookmarks.');
        return;
      }

      if (!response.ok) {
        setMessage('Unable to load bookmarks.');
        return;
      }

      var payload = await response.json();
      var data = payload && Object.prototype.hasOwnProperty.call(payload, 'data') ? payload.data : payload;
      render(Array.isArray(data) ? data : []);
    } catch (error) {
      setMessage('Unable to load bookmarks.');
    }
  }

  if (bridge) {
    bridge
      .on('theme', function (info) {
        document.documentElement.setAttribute('data-theme', info && info.theme === 'dark' ? 'dark' : 'light');
      })
      .start({ title: 'Bookmarks', role: 'widget' });
  }

  load();
})();
