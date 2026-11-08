/*
 * Email home-page widget: the five most recent inbox threads.
 *
 * The module's REST API has no cross-account "recent threads" endpoint, so the widget walks the
 * existing one: accounts → the first account's inbox → threads. Three requests behind the core's
 * proxy is cheap and keeps the module API surface unchanged.
 *
 * Every string from the server is user data and is written with textContent.
 */
(function () {
  'use strict';

  var API = 'api/v1/email';

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

  /** URL of the module app page, derived from this widget document's own address. */
  function appUrl(threadId) {
    var url = new URL('../', document.baseURI);
    if (threadId) {
      url.searchParams.set('threadId', threadId);
    }
    return url.href;
  }

  function openThread(threadId) {
    var url = appUrl(threadId);

    if (bridge && typeof bridge.openExternal === 'function') {
      bridge.openExternal(url);
      return;
    }

    window.open(url, '_blank', 'noopener,noreferrer');
  }

  function formatParticipants(json) {
    try {
      var addresses = JSON.parse(json || '[]');
      if (!Array.isArray(addresses) || addresses.length === 0) {
        return '';
      }

      return addresses.slice(0, 3).map(function (address) {
        return address.name && String(address.name).trim() ? address.name : address.email;
      }).join(', ');
    } catch (error) {
      return '';
    }
  }

  async function getJson(path) {
    var response = await fetch(path, {
      credentials: 'include',
      headers: { Accept: 'application/json' }
    });

    if (response.status === 401) {
      var unauthorized = new Error('unauthorized');
      unauthorized.status = 401;
      throw unauthorized;
    }

    if (!response.ok) {
      throw new Error('request failed');
    }

    var payload = await response.json();
    return payload && Object.prototype.hasOwnProperty.call(payload, 'data') ? payload.data : payload;
  }

  function render(threads) {
    if (!threads || threads.length === 0) {
      list.hidden = true;
      setMessage('No recent email.');
      return;
    }

    setMessage('');
    list.hidden = false;
    list.textContent = '';

    threads.forEach(function (thread) {
      var item = document.createElement('li');
      item.className = 'widget-list-item';

      var link = document.createElement('a');
      link.className = 'widget-link';
      link.href = '#';
      link.rel = 'noopener noreferrer';
      link.textContent = (thread.subject && thread.subject.trim()) || '(No subject)';

      var meta = document.createElement('span');
      meta.className = 'widget-meta';
      meta.textContent = formatParticipants(thread.participantsJson);
      if (meta.textContent) {
        link.appendChild(meta);
      }

      link.addEventListener('click', function (event) {
        event.preventDefault();
        openThread(thread.id);
      });

      item.appendChild(link);
      list.appendChild(item);
    });

    if (bridge && typeof bridge.reportHeight === 'function') {
      bridge.reportHeight();
    }
  }

  async function load() {
    try {
      var accounts = await getJson(API + '/accounts');
      if (!Array.isArray(accounts) || accounts.length === 0) {
        setMessage('No account configured.');
        return;
      }

      var mailboxes = await getJson(API + '/accounts/' + accounts[0].id + '/mailboxes');
      if (!Array.isArray(mailboxes) || mailboxes.length === 0) {
        setMessage('No folders synced yet.');
        return;
      }

      var inbox = mailboxes.filter(function (mailbox) {
        return (Number(mailbox.syncFlags) || 0) & 1;
      })[0] || mailboxes[0];

      var threads = await getJson(
        API + '/accounts/' + accounts[0].id + '/mailboxes/' + inbox.id + '/threads');

      var recent = (Array.isArray(threads) ? threads : [])
        .slice()
        .sort(function (left, right) {
          return new Date(right.lastMessageAt || 0) - new Date(left.lastMessageAt || 0);
        })
        .slice(0, 5);

      render(recent);
    } catch (error) {
      if (error && error.status === 401) {
        setMessage('Sign in to see your email.');
        return;
      }

      setMessage('Unable to load email.');
    }
  }

  if (bridge) {
    bridge
      .on('theme', function (info) {
        document.documentElement.setAttribute('data-theme', info && info.theme === 'dark' ? 'dark' : 'light');
      })
      .start({ title: 'Email', role: 'widget' });
  }

  load();
})();
