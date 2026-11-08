/*
 * Example module UI — notes page logic.
 *
 * Runs inside a sandboxed frame with an OPAQUE ORIGIN (no allow-same-origin), so:
 *   * no document.cookie / localStorage / sessionStorage — all state lives on the module's server
 *     and is reached through the same proxied path this document was loaded from;
 *   * event.origin is "null" for every message — the parent is identified by the SDK bridge, never by
 *     an origin check, so never add your own window.message listener;
 *   * alert()/confirm()/prompt() are blocked (no allow-modals) — report outcomes inline or through
 *     dncModuleBridge.notify().
 *
 * All requests are RELATIVE on purpose (see index.html).
 */
(function () {
  'use strict';

  var ENDPOINT = 'api/notes';

  var elements = {
    newNote: document.getElementById('new-note'),
    form: document.getElementById('note-form'),
    title: document.getElementById('note-title'),
    content: document.getElementById('note-content'),
    save: document.getElementById('note-save'),
    cancel: document.getElementById('note-cancel'),
    formError: document.getElementById('form-error'),
    status: document.getElementById('status'),
    notes: document.getElementById('notes'),
    empty: document.getElementById('empty'),
    navLinks: document.querySelectorAll('.nav-link'),
    panels: document.querySelectorAll('[data-panel]')
  };

  var bridge = window.dncModuleBridge;

  /* ---------------------------------------------------------------- helpers */

  function setStatus(message) {
    if (!message) {
      elements.status.hidden = true;
      elements.status.textContent = '';
      return;
    }

    elements.status.textContent = message;
    elements.status.hidden = false;
  }

  function showFormError(message) {
    if (!message) {
      elements.formError.hidden = true;
      elements.formError.textContent = '';
      return;
    }

    elements.formError.textContent = message;
    elements.formError.hidden = false;
  }

  function formatDate(value) {
    var date = new Date(value);
    if (isNaN(date.getTime())) {
      return '';
    }

    return date.toLocaleString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  function showPanel(name) {
    elements.panels.forEach(function (panel) {
      panel.hidden = panel.getAttribute('data-panel') !== name;
    });

    elements.navLinks.forEach(function (link) {
      link.classList.toggle('is-active', link.getAttribute('data-view') === name);
    });

    if (bridge && typeof bridge.setTitle === 'function') {
      bridge.setTitle(name === 'help' ? 'Example help' : 'Example notes');
    }
  }

  /* ------------------------------------------------------------------ render */

  function renderNotes(notes) {
    elements.notes.textContent = '';

    if (!notes || notes.length === 0) {
      elements.empty.hidden = false;
      return;
    }

    elements.empty.hidden = true;

    notes.forEach(function (note) {
      var card = document.createElement('article');
      card.className = 'note-card';

      var title = document.createElement('h3');
      // textContent, never innerHTML: note text is user data.
      title.textContent = note.title || '(untitled)';
      card.appendChild(title);

      if (note.content) {
        var content = document.createElement('p');
        content.className = 'note-content';
        content.textContent = note.content;
        card.appendChild(content);
      }

      var meta = document.createElement('p');
      meta.className = 'note-meta';
      meta.textContent = formatDate(note.createdAt);
      card.appendChild(meta);

      var actions = document.createElement('div');
      actions.className = 'note-actions';

      var remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'btn btn-danger btn-small';
      remove.textContent = 'Delete';
      remove.addEventListener('click', function () {
        deleteNote(note.id, remove);
      });

      actions.appendChild(remove);
      card.appendChild(actions);

      elements.notes.appendChild(card);
    });
  }

  /* -------------------------------------------------------------------- data */

  async function readError(response, fallback) {
    try {
      var payload = await response.json();
      if (payload && typeof payload.error === 'string' && payload.error.length > 0) {
        return payload.error;
      }
    } catch (error) {
      /* not JSON */
    }

    return fallback;
  }

  async function loadNotes() {
    setStatus('Loading notes...');

    try {
      var response = await fetch(ENDPOINT, {
        method: 'GET',
        credentials: 'include',
        headers: { Accept: 'application/json' }
      });

      if (response.status === 401) {
        renderNotes([]);
        setStatus('Sign in to DotNetCloud to use this module.');
        return;
      }

      if (!response.ok) {
        setStatus('Could not load notes (' + response.status + ').');
        return;
      }

      var payload = await response.json();
      renderNotes(payload.notes || []);
      setStatus('');
    } catch (error) {
      setStatus('Could not reach the module. Try again.');
    }
  }

  async function createNote(event) {
    event.preventDefault();
    showFormError('');

    var title = elements.title.value.trim();
    if (title.length === 0) {
      showFormError('A title is required.');
      elements.title.focus();
      return;
    }

    elements.save.disabled = true;
    elements.save.textContent = 'Saving...';

    try {
      var response = await fetch(ENDPOINT, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json'
        },
        body: JSON.stringify({ title: title, content: elements.content.value })
      });

      if (response.status === 401) {
        showFormError('Your session expired. Reload the page and sign in again.');
        return;
      }

      if (response.status === 400) {
        showFormError(await readError(response, 'The note could not be saved.'));
        return;
      }

      if (!response.ok) {
        showFormError('The note could not be saved (' + response.status + ').');
        return;
      }

      closeForm();
      await loadNotes();

      if (bridge && typeof bridge.notify === 'function') {
        bridge.notify('Note created');
      }
    } catch (error) {
      showFormError('Could not reach the module. Try again.');
    } finally {
      elements.save.disabled = false;
      elements.save.textContent = 'Save';
    }
  }

  async function deleteNote(noteId, button) {
    button.disabled = true;

    try {
      var response = await fetch(ENDPOINT + '/' + encodeURIComponent(noteId), {
        method: 'DELETE',
        credentials: 'include',
        headers: { Accept: 'application/json' }
      });

      if (response.status === 401) {
        setStatus('Sign in to DotNetCloud to use this module.');
        return;
      }

      if (response.status !== 204 && !response.ok) {
        setStatus('The note could not be deleted (' + response.status + ').');
        button.disabled = false;
        return;
      }

      await loadNotes();

      if (bridge && typeof bridge.notify === 'function') {
        bridge.notify('Note deleted');
      }
    } catch (error) {
      setStatus('Could not reach the module. Try again.');
      button.disabled = false;
    }
  }

  /* -------------------------------------------------------------------- form */

  function openForm() {
    elements.form.hidden = false;
    showFormError('');
    elements.title.focus();
  }

  function closeForm() {
    elements.form.hidden = true;
    elements.title.value = '';
    elements.content.value = '';
    showFormError('');
  }

  /* -------------------------------------------------------------------- boot */

  if (bridge) {
    bridge
      .on('theme', function (info) {
        // The bridge already sets data-theme on <html>; this is where extra theme work would go.
        document.documentElement.setAttribute('data-theme', info.theme === 'dark' ? 'dark' : 'light');
      })
      .on('init', function (info) {
        document.documentElement.setAttribute('lang', info.locale || 'en');
      })
      .start({ title: 'Example notes' });
  }

  elements.newNote.addEventListener('click', openForm);
  elements.cancel.addEventListener('click', closeForm);
  elements.form.addEventListener('submit', createNote);

  elements.navLinks.forEach(function (link) {
    link.addEventListener('click', function () {
      showPanel(link.getAttribute('data-view'));
    });
  });

  loadNotes();
})();
