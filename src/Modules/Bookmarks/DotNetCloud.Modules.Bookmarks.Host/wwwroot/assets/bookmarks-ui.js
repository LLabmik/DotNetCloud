/*
 * Bookmarks module UI.
 *
 * Runs inside a sandboxed frame with an OPAQUE ORIGIN (no allow-same-origin), so:
 *   * no document.cookie / localStorage / sessionStorage — every request goes to this module's own
 *     server through the same proxied path this document was loaded from;
 *   * event.origin is "null" for every message — the parent is identified by the SDK bridge, so never
 *     add your own window.message listener;
 *   * every fetch is cross-origin (the frame's origin is opaque), so requests use
 *     credentials: 'include' and the core proxy answers the preflight;
 *   * modals are available (allow-modals), and downloads are not — export opens in a core-controlled
 *     tab via the bridge.
 *
 * All requests are RELATIVE on purpose (see index.html): the core strips /apps/dotnetcloud.bookmarks
 * before forwarding, so "api/v1/bookmarks" resolves to this process's own REST API.
 *
 * Every string that comes from the server is user data and is written with textContent (or an
 * attribute setter), never innerHTML.
 */
(function () {
  "use strict";

  var API = "api/v1/bookmarks";
  var PAGE_SIZE = 200;

  /* ---------------------------------------------------------------- icons */

  var ICONS = {
    add: "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z",
    folder:
      "M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z",
    bookmarks: "M17 3H7c-1.1 0-2 .9-2 2v16l7-3 7 3V5c0-1.1-.9-2-2-2z",
    star: "M12 17.27 18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z",
    star_border:
      "M22 9.24l-7.19-.62L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21 12 17.27 18.18 21l-1.63-7.03L22 9.24zM12 15.4l-3.76 2.27 1-4.28-3.32-2.88 4.38-.38L12 6.1l1.71 4.04 4.38.38-3.32 2.88 1 4.28L12 15.4z",
    delete:
      "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z",
    refresh:
      "M17.65 6.35A7.958 7.958 0 0 0 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0 1 12 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z",
    search:
      "M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z",
    grid_view: "M3 3h8v8H3zm10 0h8v8h-8zM3 13h8v8H3zm10 0h8v8h-8z",
    view_list:
      "M3 14h4v-4H3v4zm0 5h4v-4H3v4zM3 9h4V5H3v4zm5 5h13v-4H8v4zm0 5h13v-4H8v4zM8 5v4h13V5H8z",
    upload: "M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z",
    download: "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z",
    help: "M11 18h2v-2h-2v2zm1-16C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-2.21 0-4 1.79-4 4h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4-4z",
    close:
      "M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z",
    chevron_left: "M15.41 7.41 14 6l-6 6 6 6 1.41-1.41L10.83 12z",
    chevron_right: "M10 6 8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z",
    edit: "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.996.996 0 0 0 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z",
    arrow_back: "M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z",
  };

  function buildIcon(name, className) {
    var path = ICONS[name] || ICONS.bookmarks;
    var ns = "http://www.w3.org/2000/svg";
    var svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("aria-hidden", "true");
    svg.setAttribute("focusable", "false");
    svg.setAttribute("class", "icon" + (className ? " " + className : ""));
    var node = document.createElementNS(ns, "path");
    node.setAttribute("d", path);
    svg.appendChild(node);
    return svg;
  }

  /** Fills every element carrying data-icon with its Material icon. */
  function paintIcons(root) {
    var scope = root || document;
    var targets = scope.querySelectorAll(
      "[data-icon]:not([data-icon-painted])",
    );
    Array.prototype.forEach.call(targets, function (target) {
      target.setAttribute("data-icon-painted", "yes");
      target.appendChild(buildIcon(target.getAttribute("data-icon")));
    });
  }

  /** Builds an icon-only button. */
  function iconButton(icon, label, onClick, extraClass) {
    var button = document.createElement("button");
    button.type = "button";
    button.className = "icon-btn" + (extraClass ? " " + extraClass : "");
    button.title = label;
    button.setAttribute("aria-label", label);
    button.appendChild(buildIcon(icon));
    button.addEventListener("click", function (event) {
      event.stopPropagation();
      onClick();
    });
    return button;
  }

  /* ----------------------------------------------------------------- state */

  var state = {
    folders: [],
    bookmarks: [],
    searchQuery: null,
    searchResults: [],
    viewMode: "grid",
    selectedFolderId: null,
    showFavorites: false,
    selected: null,
    preview: null,
    editing: null,
    editingFolder: null,
    collapsed: false,
  };

  var el = {};
  var bridge = window.dncModuleBridge || null;

  /** Error carrying the HTTP status so a 404 can be treated as "no data" rather than a failure. */
  function ApiError(status, message) {
    this.name = "ApiError";
    this.status = status;
    this.message = message;
  }

  ApiError.prototype = Object.create(Error.prototype);

  /* ------------------------------------------------------------------- api */

  async function apiFetch(path, init) {
    var options = {
      method: (init && init.method) || "GET",
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    };

    if (init && init.method) {
      options.method = init.method;
    }

    if (init && init.body !== undefined) {
      options.body = init.body;
    }

    if (init && init.contentType) {
      options.headers["Content-Type"] = init.contentType;
    }

    var response;

    try {
      response = await fetch(path, options);
    } catch (error) {
      throw new ApiError(0, "Could not reach the module. Try again.");
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
      throw new ApiError(401, "Sign in to DotNetCloud to use this module.");
    }

    if (!response.ok) {
      var message = "Request failed (" + response.status + ").";
      if (
        payload &&
        payload.error &&
        typeof payload.error.message === "string" &&
        payload.error.message
      ) {
        message = payload.error.message;
      }

      throw new ApiError(response.status, message);
    }

    if (payload && Object.prototype.hasOwnProperty.call(payload, "data")) {
      return payload.data;
    }

    return payload;
  }

  /* --------------------------------------------------------------- helpers */

  function setStatus(message) {
    if (!message) {
      el.status.hidden = true;
      el.status.textContent = "";
      return;
    }

    el.status.textContent = message;
    el.status.hidden = false;
  }

  function showFormError(target, message) {
    if (!message) {
      target.hidden = true;
      target.textContent = "";
      return;
    }

    target.textContent = message;
    target.hidden = false;
  }

  /** Reports a failure inline; a lost session is reported once, in the status line. */
  function reportError(error) {
    if (error && error.status === 401) {
      setStatus(error.message);
      return;
    }

    setStatus((error && error.message) || "Something went wrong. Try again.");
  }

  function displayTitle(item) {
    var title = item && typeof item.title === "string" ? item.title.trim() : "";
    return title || (item && item.url) || "(untitled)";
  }

  function folderById(folderId) {
    if (!folderId) {
      return null;
    }

    for (var i = 0; i < state.folders.length; i++) {
      if (state.folders[i].id === folderId) {
        return state.folders[i];
      }
    }

    return null;
  }

  function folderName(folderId) {
    var folder = folderById(folderId);
    return folder && folder.name ? folder.name : "No folder";
  }

  function parseTags(tagsJson) {
    if (typeof tagsJson !== "string" || tagsJson.length === 0) {
      return [];
    }

    try {
      var parsed = JSON.parse(tagsJson);
      return Array.isArray(parsed)
        ? parsed.filter(function (tag) {
            return typeof tag === "string";
          })
        : [];
    } catch (error) {
      return [];
    }
  }

  function formatDate(value) {
    var date = new Date(value);
    if (!value || isNaN(date.getTime())) {
      return "";
    }

    return date.toLocaleString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  /** Normalizes the preview status, which may arrive as an enum number or a name. */
  function previewStatus(preview) {
    var value = preview && preview.status;
    if (typeof value === "number") {
      return ["notFetched", "ok", "failed", "fetching"][value] || "notFetched";
    }

    return typeof value === "string" ? value.toLowerCase() : "notFetched";
  }

  /** Absolute URL for a module-relative path (used for the export tab). */
  function absoluteUrl(relative) {
    return new URL(relative, window.location.href).href;
  }

  function openExternal(url) {
    if (!url) {
      return;
    }

    if (bridge && typeof bridge.openExternal === "function") {
      bridge.openExternal(url);
      return;
    }

    window.open(url, "_blank", "noopener,noreferrer");
  }

  /* ------------------------------------------------------------ navigation */

  var PANEL_TITLES = {
    library: "Bookmarks",
    detail: "Bookmark",
    edit: "Bookmark",
    folder: "Folder",
    import: "Import bookmarks",
    help: "Bookmarks help",
  };

  function showPanel(name) {
    var panels = document.querySelectorAll("[data-panel]");
    Array.prototype.forEach.call(panels, function (panel) {
      panel.hidden = panel.getAttribute("data-panel") !== name;
    });

    if (bridge && typeof bridge.setTitle === "function") {
      bridge.setTitle(PANEL_TITLES[name] || "Bookmarks");
    }

    if (el.main) {
      el.main.scrollTop = 0;
    }
  }

  function markView(activeId) {
    // el is keyed by camelCase property names (navAll/navFavorites); index it that way. Indexing it
    // with the DOM ids ("nav-all") yields undefined and threw on every view change, which aborted
    // start() before the first API call and left the UI on its empty state forever.
    if (el.navAll) {
      el.navAll.classList.toggle("is-active", activeId === "nav-all");
    }

    if (el.navFavorites) {
      el.navFavorites.classList.toggle("is-active", activeId === "nav-favorites");
    }

    Array.prototype.forEach.call(
      el.folderList.querySelectorAll(".nav-item"),
      function (row) {
        row.classList.toggle(
          "is-active",
          row.getAttribute("data-nav") === activeId,
        );
      },
    );

    el.navHelp.classList.toggle("is-active", activeId === "nav-help");
    el.navImport.classList.toggle("is-active", activeId === "nav-import");
  }

  /* ---------------------------------------------------------------- render */

  function renderFolderList() {
    el.folderList.textContent = "";

    state.folders.forEach(function (folder) {
      var row = document.createElement("div");
      row.className = "nav-row";

      var select = document.createElement("button");
      select.type = "button";
      select.className = "nav-item";
      select.setAttribute("data-nav", "folder:" + folder.id);
      select.title = folder.name || "Folder";

      var dot = document.createElement("span");
      dot.className = "folder-dot";
      dot.style.background = folder.color || "#6b7280";
      select.appendChild(dot);

      var text = document.createElement("span");
      text.className = "nav-text";
      text.textContent = folder.name || "Folder";
      select.appendChild(text);

      select.addEventListener("click", function () {
        selectFolder(folder.id);
      });

      row.appendChild(select);
      row.appendChild(
        iconButton(
          "edit",
          "Rename folder",
          function () {
            beginFolderEdit(folder);
          },
          "nav-row-action",
        ),
      );

      el.folderList.appendChild(row);
    });

    el.folderEmpty.hidden = state.folders.length > 0;
  }

  function renderFolderOptions(select, selectedId, includeNone, noneLabel) {
    select.textContent = "";

    if (includeNone) {
      var none = document.createElement("option");
      none.value = "";
      none.textContent = noneLabel || "No folder";
      select.appendChild(none);
    }

    state.folders.forEach(function (folder) {
      var option = document.createElement("option");
      option.value = folder.id;
      option.textContent = folder.name || "Folder";
      select.appendChild(option);
    });

    select.value = selectedId || "";
  }

  function visibleItems() {
    if (state.searchQuery !== null) {
      return state.searchResults;
    }

    if (state.showFavorites) {
      return state.bookmarks.filter(function (item) {
        return item.isFavorite === true;
      });
    }

    return state.bookmarks;
  }

  function renderLibrary() {
    var items = visibleItems();

    var title;
    if (state.searchQuery !== null) {
      title = "Results for “" + state.searchQuery + "”";
    } else if (state.showFavorites) {
      title = "Favorites";
    } else if (state.selectedFolderId) {
      title = folderName(state.selectedFolderId);
    } else {
      title = "All bookmarks";
    }

    el.libraryTitle.textContent = title;
    el.viewGrid.classList.toggle("is-active", state.viewMode === "grid");
    el.viewList.classList.toggle("is-active", state.viewMode === "list");

    var isEmpty = items.length === 0;
    el.empty.hidden = !isEmpty;
    el.grid.hidden = isEmpty || state.viewMode !== "grid";
    el.list.hidden = isEmpty || state.viewMode !== "list";

    if (isEmpty) {
      if (state.searchQuery !== null) {
        el.emptyTitle.textContent = "No matching bookmarks";
        el.emptyText.textContent =
          "Nothing matched “" +
          state.searchQuery +
          "”. Try a different search.";
        el.emptyAdd.hidden = true;
      } else if (state.showFavorites) {
        el.emptyTitle.textContent = "No favorites yet";
        el.emptyText.textContent = "Star a bookmark to keep it here.";
        el.emptyAdd.hidden = true;
      } else if (state.selectedFolderId) {
        el.emptyTitle.textContent = "This folder is empty";
        el.emptyText.textContent =
          "Add a bookmark to this folder, or move an existing one.";
        el.emptyAdd.hidden = false;
      } else {
        el.emptyTitle.textContent = "No bookmarks yet";
        el.emptyText.textContent =
          "Add your first bookmark to get started, or import one from your browser.";
        el.emptyAdd.hidden = false;
      }

      el.grid.textContent = "";
      el.list.textContent = "";
      return;
    }

    if (state.viewMode === "grid") {
      renderGrid(items);
    } else {
      renderList(items);
    }
  }

  function actionRow(item) {
    var actions = document.createElement("div");
    actions.className = "card-actions";

    actions.appendChild(
      iconButton(
        item.isFavorite ? "star" : "star_border",
        item.isFavorite ? "Remove from favorites" : "Add to favorites",
        function () {
          toggleFavorite(item);
        },
        item.isFavorite ? "is-favorite" : "",
      ),
    );

    actions.appendChild(
      iconButton("refresh", "Fetch preview", function () {
        fetchPreview(item);
      }),
    );
    actions.appendChild(
      iconButton(
        "delete",
        "Delete bookmark",
        function () {
          deleteBookmark(item);
        },
        "is-danger",
      ),
    );

    return actions;
  }

  function renderGrid(items) {
    el.grid.textContent = "";

    items.forEach(function (item) {
      var card = document.createElement("article");
      card.className = "card" + (item.isFavorite ? " is-favorite" : "");
      card.tabIndex = 0;
      card.setAttribute("role", "button");

      card.addEventListener("click", function () {
        openDetail(item);
      });
      card.addEventListener("keydown", function (event) {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          openDetail(item);
        }
      });

      var header = document.createElement("div");
      header.className = "card-header";

      var title = document.createElement("h3");
      title.className = "card-title";
      title.textContent = displayTitle(item);
      header.appendChild(title);
      header.appendChild(actionRow(item));
      card.appendChild(header);

      var url = document.createElement("p");
      url.className = "card-url";
      url.textContent = item.url || "";
      card.appendChild(url);

      if (item.description) {
        var description = document.createElement("p");
        description.className = "card-description";
        description.textContent = item.description;
        card.appendChild(description);
      }

      var meta = document.createElement("p");
      meta.className = "card-meta";
      var parts = [folderName(item.folderId)];
      var tags = parseTags(item.tagsJson);
      if (tags.length > 0) {
        parts.push(tags.join(", "));
      }

      meta.textContent = parts.join(" · ");
      card.appendChild(meta);

      el.grid.appendChild(card);
    });
  }

  function renderList(items) {
    el.list.textContent = "";

    var wrap = document.createElement("div");
    wrap.className = "table-wrap";

    var table = document.createElement("table");
    table.className = "table";

    var head = document.createElement("thead");
    var headRow = document.createElement("tr");
    ["Title", "URL", "Folder", ""].forEach(function (label) {
      var th = document.createElement("th");
      th.textContent = label;
      headRow.appendChild(th);
    });
    head.appendChild(headRow);
    table.appendChild(head);

    var body = document.createElement("tbody");

    items.forEach(function (item) {
      var row = document.createElement("tr");
      row.className = "table-row" + (item.isFavorite ? " is-favorite" : "");
      row.tabIndex = 0;
      row.addEventListener("click", function () {
        openDetail(item);
      });
      row.addEventListener("keydown", function (event) {
        if (event.key === "Enter") {
          event.preventDefault();
          openDetail(item);
        }
      });

      var titleCell = document.createElement("td");
      titleCell.textContent = displayTitle(item);
      row.appendChild(titleCell);

      var urlCell = document.createElement("td");
      urlCell.className = "table-url";
      urlCell.textContent = item.url || "";
      row.appendChild(urlCell);

      var folderCell = document.createElement("td");
      folderCell.textContent = folderName(item.folderId);
      row.appendChild(folderCell);

      var actionCell = document.createElement("td");
      actionCell.className = "table-actions";
      actionCell.appendChild(actionRow(item));
      row.appendChild(actionCell);

      body.appendChild(row);
    });

    table.appendChild(body);
    wrap.appendChild(table);
    el.list.appendChild(wrap);
  }

  function renderDetail() {
    var item = state.selected;
    if (!item) {
      return;
    }

    el.detailTitle.textContent = displayTitle(item);
    el.detailUrl.textContent = item.url || "";
    el.detailUrl.href = item.url || "#";
    el.detailUrl.hidden = !item.url;

    el.detailDescription.hidden = !item.description;
    el.detailDescription.textContent = item.description || "";

    var metaParts = [folderName(item.folderId)];
    var tags = parseTags(item.tagsJson);
    if (tags.length > 0) {
      metaParts.push("Tags: " + tags.join(", "));
    }

    if (item.updatedAt) {
      metaParts.push("Updated " + formatDate(item.updatedAt));
    }

    el.detailMeta.textContent = metaParts.join(" · ");

    el.detailNotes.hidden = !item.notes;
    el.detailNotesText.textContent = item.notes || "";

    el.detailFavorite.setAttribute(
      "aria-pressed",
      item.isFavorite ? "true" : "false",
    );
    el.detailFavorite.classList.toggle("is-favorite", item.isFavorite === true);
  }

  function renderPreview() {
    var preview = state.preview;

    if (!preview) {
      el.previewCard.hidden = true;
      el.previewBody.textContent = "";
      el.previewStatus.hidden = true;
      el.previewStatus.textContent = "";
      return;
    }

    el.previewCard.hidden = false;
    el.previewBody.textContent = "";

    var status = previewStatus(preview);

    if (status === "fetching") {
      el.previewStatus.hidden = false;
      el.previewStatus.textContent = "Fetching preview…";
      return;
    }

    if (status === "failed") {
      el.previewStatus.hidden = false;
      el.previewStatus.textContent = preview.errorMessage
        ? "Preview fetch failed: " + preview.errorMessage
        : "Preview fetch failed.";
      return;
    }

    if (status === "notfetched" || status === "notFetched") {
      el.previewStatus.hidden = false;
      el.previewStatus.textContent =
        "No preview yet. Select “Fetch preview” to load one.";
      return;
    }

    el.previewStatus.hidden = true;
    el.previewStatus.textContent = "";

    if (preview.previewImageUrl) {
      var image = document.createElement("img");
      image.className = "preview-image";
      image.alt = "";
      image.loading = "lazy";
      image.referrerPolicy = "no-referrer";
      image.src = preview.previewImageUrl;
      el.previewBody.appendChild(image);
    }

    var heading = document.createElement("div");
    heading.className = "preview-heading";

    if (preview.faviconUrl) {
      var favicon = document.createElement("img");
      favicon.className = "preview-favicon";
      favicon.alt = "";
      favicon.width = 16;
      favicon.height = 16;
      favicon.referrerPolicy = "no-referrer";
      favicon.src = preview.faviconUrl;
      heading.appendChild(favicon);
    }

    if (preview.siteName) {
      var site = document.createElement("span");
      site.className = "preview-site";
      site.textContent = preview.siteName;
      heading.appendChild(site);
    }

    if (heading.childNodes.length > 0) {
      el.previewBody.appendChild(heading);
    }

    if (preview.resolvedTitle) {
      var resolved = document.createElement("h5");
      resolved.textContent = preview.resolvedTitle;
      el.previewBody.appendChild(resolved);
    }

    if (preview.resolvedDescription) {
      var description = document.createElement("p");
      description.textContent = preview.resolvedDescription;
      el.previewBody.appendChild(description);
    }
  }

  /* ------------------------------------------------------------------ data */

  async function loadLibrary() {
    setStatus("Loading bookmarks…");

    try {
      var folders = await apiFetch(API + "/folders");
      state.folders = Array.isArray(folders) ? folders : [];
      renderFolderList();
      renderFolderOptions(
        el.editFolder,
        state.editing ? state.editing.folderId : null,
        true,
        "No folder",
      );
      renderFolderOptions(
        el.importFolder,
        state.selectedFolderId,
        true,
        "No folder",
      );

      await loadBookmarks();
      setStatus("");
    } catch (error) {
      reportError(error);
    }
  }

  async function loadBookmarks() {
    var path = API + "?skip=0&take=" + PAGE_SIZE;
    if (state.selectedFolderId) {
      path += "&folderId=" + encodeURIComponent(state.selectedFolderId);
    }

    var items = await apiFetch(path);
    state.bookmarks = Array.isArray(items) ? items : [];
    renderLibrary();
  }

  function selectAll() {
    state.selectedFolderId = null;
    state.showFavorites = false;
    state.searchQuery = null;
    el.searchInput.value = "";
    el.searchClear.hidden = true;
    markView("nav-all");
    showPanel("library");
    loadLibrary();
  }

  function selectFavorites() {
    state.selectedFolderId = null;
    state.showFavorites = true;
    state.searchQuery = null;
    el.searchInput.value = "";
    el.searchClear.hidden = true;
    markView("nav-favorites");
    showPanel("library");
    loadLibrary();
  }

  function selectFolder(folderId) {
    state.selectedFolderId = folderId;
    state.showFavorites = false;
    state.searchQuery = null;
    el.searchInput.value = "";
    el.searchClear.hidden = true;
    markView("folder:" + folderId);
    showPanel("library");
    loadLibrary();
  }

  async function runSearch(query) {
    state.searchQuery = query;
    setStatus("Searching…");

    try {
      var items = await apiFetch(
        API +
          "/search?q=" +
          encodeURIComponent(query) +
          "&skip=0&take=" +
          PAGE_SIZE,
      );
      state.searchResults = Array.isArray(items) ? items : [];
      el.searchClear.hidden = false;
      showPanel("library");
      renderLibrary();
      setStatus("");
    } catch (error) {
      reportError(error);
    }
  }

  function clearSearch() {
    state.searchQuery = null;
    state.searchResults = [];
    el.searchInput.value = "";
    el.searchClear.hidden = true;
    renderLibrary();
  }

  /* ---------------------------------------------------------------- detail */

  async function openDetail(item) {
    state.selected = item;
    state.preview = null;
    showPanel("detail");
    renderDetail();
    renderPreview();

    try {
      state.preview = await apiFetch(API + "/" + item.id + "/preview");
    } catch (error) {
      if (error.status !== 404) {
        reportError(error);
        return;
      }
    }

    renderPreview();
  }

  async function toggleFavorite(item) {
    try {
      var updated = await apiFetch(API + "/" + item.id, {
        method: "PUT",
        contentType: "application/json",
        body: JSON.stringify({ isFavorite: item.isFavorite !== true }),
      });

      replaceItem(
        updated && updated.id
          ? updated
          : Object.assign({}, item, { isFavorite: item.isFavorite !== true }),
      );

      if (state.selected && state.selected.id === item.id) {
        state.selected = itemById(item.id);
        renderDetail();
      }

      renderLibrary();
    } catch (error) {
      reportError(error);
    }
  }

  async function fetchPreview(item) {
    setStatus("Fetching preview…");

    try {
      var preview = await apiFetch(API + "/" + item.id + "/preview/fetch", {
        method: "POST",
      });

      if (state.selected && state.selected.id === item.id) {
        state.preview = preview;
        renderPreview();
      }

      setStatus("");
    } catch (error) {
      reportError(error);
    }
  }

  async function deleteBookmark(item) {
    try {
      await apiFetch(API + "/" + item.id, { method: "DELETE" });

      state.bookmarks = state.bookmarks.filter(function (candidate) {
        return candidate.id !== item.id;
      });
      state.searchResults = state.searchResults.filter(function (candidate) {
        return candidate.id !== item.id;
      });

      if (state.selected && state.selected.id === item.id) {
        state.selected = null;
        showPanel("library");
      }

      renderLibrary();

      if (bridge && typeof bridge.notify === "function") {
        bridge.notify("Bookmark deleted");
      }
    } catch (error) {
      reportError(error);
    }
  }

  function itemById(id) {
    var source =
      state.searchQuery !== null ? state.searchResults : state.bookmarks;
    for (var i = 0; i < source.length; i++) {
      if (source[i].id === id) {
        return source[i];
      }
    }

    return null;
  }

  function replaceItem(updated) {
    if (!updated || !updated.id) {
      return;
    }

    state.bookmarks = state.bookmarks.map(function (candidate) {
      return candidate.id === updated.id ? updated : candidate;
    });

    state.searchResults = state.searchResults.map(function (candidate) {
      return candidate.id === updated.id ? updated : candidate;
    });
  }

  /* ---------------------------------------------------------------- editing */

  function beginCreate() {
    state.editing = { id: null, folderId: state.selectedFolderId };

    el.editTitle.textContent = "Add bookmark";
    el.editUrl.value = "";
    el.editBookmarkTitle.value = "";
    el.editDescription.value = "";
    el.editNotes.value = "";
    el.editTags.value = "";
    renderFolderOptions(
      el.editFolder,
      state.selectedFolderId,
      true,
      "No folder",
    );
    showFormError(el.editError, "");
    showPanel("edit");
    el.editUrl.focus();
  }

  function beginEdit(item) {
    state.editing = item;

    el.editTitle.textContent = "Edit bookmark";
    el.editUrl.value = item.url || "";
    el.editBookmarkTitle.value = item.title || "";
    el.editDescription.value = item.description || "";
    el.editNotes.value = item.notes || "";
    el.editTags.value = parseTags(item.tagsJson).join(", ");
    renderFolderOptions(el.editFolder, item.folderId, true, "No folder");
    showFormError(el.editError, "");
    showPanel("edit");
    el.editUrl.focus();
  }

  function readTags(value) {
    return String(value || "")
      .split(",")
      .map(function (tag) {
        return tag.trim();
      })
      .filter(function (tag) {
        return tag.length > 0;
      });
  }

  function optional(value) {
    var trimmed = String(value || "").trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  async function submitEdit(event) {
    event.preventDefault();

    var url = el.editUrl.value.trim();
    if (url.length === 0) {
      showFormError(el.editError, "A URL is required.");
      el.editUrl.focus();
      return;
    }

    var isCreate = !state.editing || !state.editing.id;
    var payload = {
      url: url,
      title: optional(el.editBookmarkTitle.value),
      description: optional(el.editDescription.value),
      notes: optional(el.editNotes.value),
      tags: readTags(el.editTags.value),
      folderId: el.editFolder.value || null,
    };

    el.editSave.disabled = true;
    el.editSave.textContent = "Saving…";

    try {
      if (isCreate) {
        await apiFetch(API, {
          method: "POST",
          contentType: "application/json",
          body: JSON.stringify(payload),
        });
      } else {
        await apiFetch(API + "/" + state.editing.id, {
          method: "PUT",
          contentType: "application/json",
          body: JSON.stringify(payload),
        });
      }

      state.editing = null;
      await loadLibrary();
      showPanel("library");

      if (bridge && typeof bridge.notify === "function") {
        bridge.notify(isCreate ? "Bookmark created" : "Bookmark updated");
      }
    } catch (error) {
      showFormError(
        el.editError,
        (error && error.message) || "The bookmark could not be saved.",
      );
    } finally {
      el.editSave.disabled = false;
      el.editSave.textContent = "Save";
    }
  }

  function cancelEdit() {
    state.editing = null;
    showPanel("library");
  }

  /* ---------------------------------------------------------------- folders */

  function beginFolderCreate() {
    state.editingFolder = null;

    el.folderTitle.textContent = "New folder";
    el.folderName.value = "";
    el.folderColor.value = "#6b7280";
    el.folderDelete.hidden = true;
    showFormError(el.folderError, "");
    showPanel("folder");
    el.folderName.focus();
  }

  function beginFolderEdit(folder) {
    state.editingFolder = folder;

    el.folderTitle.textContent = "Edit folder";
    el.folderName.value = folder.name || "";
    el.folderColor.value = folder.color || "#6b7280";
    el.folderDelete.hidden = false;
    showFormError(el.folderError, "");
    showPanel("folder");
    el.folderName.focus();
  }

  async function submitFolder(event) {
    event.preventDefault();

    var name = el.folderName.value.trim();
    if (name.length === 0) {
      showFormError(el.folderError, "A folder name is required.");
      el.folderName.focus();
      return;
    }

    var payload = { name: name, color: el.folderColor.value || null };
    var editing = state.editingFolder;

    el.folderSave.disabled = true;
    el.folderSave.textContent = "Saving…";

    try {
      if (editing) {
        await apiFetch(API + "/folders/" + editing.id, {
          method: "PUT",
          contentType: "application/json",
          body: JSON.stringify(payload),
        });
      } else {
        await apiFetch(API + "/folders", {
          method: "POST",
          contentType: "application/json",
          body: JSON.stringify(payload),
        });
      }

      state.editingFolder = null;
      await loadLibrary();
      showPanel("library");
    } catch (error) {
      showFormError(
        el.folderError,
        (error && error.message) || "The folder could not be saved.",
      );
    } finally {
      el.folderSave.disabled = false;
      el.folderSave.textContent = "Save";
    }
  }

  async function deleteFolder() {
    if (!state.editingFolder) {
      return;
    }

    var deletedId = state.editingFolder.id;
    el.folderDelete.disabled = true;

    try {
      await apiFetch(API + "/folders/" + deletedId, { method: "DELETE" });

      state.editingFolder = null;
      state.selectedFolderId =
        state.selectedFolderId === deletedId ? null : state.selectedFolderId;
      await loadLibrary();
      markView(
        state.selectedFolderId ? "folder:" + state.selectedFolderId : "nav-all",
      );
      showPanel("library");
    } catch (error) {
      showFormError(
        el.folderError,
        (error && error.message) || "The folder could not be deleted.",
      );
    } finally {
      el.folderDelete.disabled = false;
    }
  }

  function cancelFolder() {
    state.editingFolder = null;
    showPanel("library");
  }

  /* ----------------------------------------------------------------- import */

  function openImport() {
    el.importFile.value = "";
    el.importResult.hidden = true;
    el.importErrors.textContent = "";
    renderFolderOptions(
      el.importFolder,
      state.selectedFolderId,
      true,
      "No folder",
    );
    showFormError(el.importError, "");
    markView("nav-import");
    showPanel("import");
  }

  async function submitImport(event) {
    event.preventDefault();

    var file =
      el.importFile.files && el.importFile.files.length > 0
        ? el.importFile.files[0]
        : null;
    if (!file) {
      showFormError(el.importError, "Select a bookmarks file to import.");
      return;
    }

    var form = new FormData();
    form.append("file", file, file.name);

    var path = API + "/import";
    if (el.importFolder.value) {
      path += "?folderId=" + encodeURIComponent(el.importFolder.value);
    }

    el.importSave.disabled = true;
    el.importSave.textContent = "Importing…";
    showFormError(el.importError, "");

    try {
      var response = await fetch(path, {
        method: "POST",
        credentials: "include",
        headers: { Accept: "application/json" },
        body: form,
      });

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
        showFormError(
          el.importError,
          "Sign in to DotNetCloud to use this module.",
        );
        return;
      }

      if (!response.ok) {
        var message = "Import failed (" + response.status + ").";
        if (
          payload &&
          payload.error &&
          typeof payload.error.message === "string" &&
          payload.error.message
        ) {
          message = payload.error.message;
        }

        showFormError(el.importError, message);
        return;
      }

      var result = payload && payload.data ? payload.data : payload || {};
      renderImportResult(result);
      await loadLibrary();
    } catch (error) {
      showFormError(el.importError, "Could not reach the module. Try again.");
    } finally {
      el.importSave.disabled = false;
      el.importSave.textContent = "Import";
    }
  }

  function renderImportResult(result) {
    el.importCount.textContent = String(result.importedCount || 0);
    el.importFolderCount.textContent = String(result.folderCount || 0);
    el.importSkipped.textContent = String(result.skippedCount || 0);

    el.importErrors.textContent = "";
    var errors = Array.isArray(result.errors) ? result.errors : [];
    errors.forEach(function (message) {
      var line = document.createElement("li");
      line.textContent = String(message);
      el.importErrors.appendChild(line);
    });

    el.importResult.hidden = false;
  }

  /* ----------------------------------------------------------------- export */

  function exportBookmarks() {
    // A download inside a sandboxed frame is blocked, so the export endpoint is opened in a
    // core-controlled tab: that navigation carries the session cookie, the proxy mints the assertion,
    // and the module answers with a text/html attachment.
    var path = API + "/export";
    if (state.selectedFolderId) {
      path += "?folderId=" + encodeURIComponent(state.selectedFolderId);
    }

    openExternal(absoluteUrl(path));
  }

  /* ------------------------------------------------------------------- boot */

  function cacheElements() {
    el.status = document.getElementById("status");

    el.main = document.querySelector(".main");
    el.sidebar = document.querySelector(".sidebar");
    el.sidebarToggle = document.getElementById("sidebar-toggle");

    el.newBookmark = document.getElementById("new-bookmark");
    el.newFolder = document.getElementById("new-folder");
    el.navAll = document.getElementById("nav-all");
    el.navFavorites = document.getElementById("nav-favorites");
    el.navImport = document.getElementById("nav-import");
    el.navExport = document.getElementById("nav-export");
    el.navHelp = document.getElementById("nav-help");
    el.folderList = document.getElementById("folder-list");
    el.folderEmpty = document.getElementById("folder-empty");

    el.libraryTitle = document.getElementById("library-title");
    el.grid = document.getElementById("grid");
    el.list = document.getElementById("list");
    el.empty = document.getElementById("empty");
    el.emptyTitle = document.getElementById("empty-title");
    el.emptyText = document.getElementById("empty-text");
    el.emptyAdd = document.getElementById("empty-add");

    el.searchForm = document.getElementById("search-form");
    el.searchInput = document.getElementById("search-input");
    el.searchClear = document.getElementById("search-clear");
    el.viewGrid = document.getElementById("view-grid");
    el.viewList = document.getElementById("view-list");

    el.detailBack = document.getElementById("detail-back");
    el.detailEdit = document.getElementById("detail-edit");
    el.detailFavorite = document.getElementById("detail-favorite");
    el.detailPreview = document.getElementById("detail-preview");
    el.detailDelete = document.getElementById("detail-delete");
    el.detailTitle = document.getElementById("detail-title");
    el.detailUrl = document.getElementById("detail-url");
    el.detailDescription = document.getElementById("detail-description");
    el.detailMeta = document.getElementById("detail-meta");
    el.detailNotes = document.getElementById("detail-notes");
    el.detailNotesText = document.getElementById("detail-notes-text");
    el.previewCard = document.getElementById("detail-preview-card");
    el.previewStatus = document.getElementById("detail-preview-status");
    el.previewBody = document.getElementById("detail-preview-body");

    el.editForm = document.getElementById("edit-form");
    el.editTitle = document.getElementById("edit-title");
    el.editUrl = document.getElementById("edit-url");
    el.editBookmarkTitle = document.getElementById("edit-bookmark-title");
    el.editFolder = document.getElementById("edit-folder");
    el.editDescription = document.getElementById("edit-description");
    el.editNotes = document.getElementById("edit-notes");
    el.editTags = document.getElementById("edit-tags");
    el.editError = document.getElementById("edit-error");
    el.editSave = document.getElementById("edit-save");
    el.editCancel = document.getElementById("edit-cancel");

    el.folderForm = document.getElementById("folder-form");
    el.folderTitle = document.getElementById("folder-title");
    el.folderName = document.getElementById("folder-name");
    el.folderColor = document.getElementById("folder-color");
    el.folderError = document.getElementById("folder-error");
    el.folderSave = document.getElementById("folder-save");
    el.folderCancel = document.getElementById("folder-cancel");
    el.folderDelete = document.getElementById("folder-delete");

    el.importForm = document.getElementById("import-form");
    el.importFile = document.getElementById("import-file");
    el.importFolder = document.getElementById("import-folder");
    el.importError = document.getElementById("import-error");
    el.importSave = document.getElementById("import-save");
    el.importDone = document.getElementById("import-done");
    el.importResult = document.getElementById("import-result");
    el.importCount = document.getElementById("import-count");
    el.importFolderCount = document.getElementById("import-folder-count");
    el.importSkipped = document.getElementById("import-skipped");
    el.importErrors = document.getElementById("import-errors");
  }

  function toggleSidebar() {
    state.collapsed = !state.collapsed;
    el.sidebar.classList.toggle("is-collapsed", state.collapsed);

    var label = state.collapsed ? "Expand sidebar" : "Collapse sidebar";
    el.sidebarToggle.setAttribute("aria-label", label);
    el.sidebarToggle.title = label;
    el.sidebarToggle.replaceChildren(
      buildIcon(state.collapsed ? "chevron_right" : "chevron_left"),
    );
  }

  function wireEvents() {
    el.sidebarToggle.addEventListener("click", toggleSidebar);

    el.newBookmark.addEventListener("click", beginCreate);
    el.newFolder.addEventListener("click", beginFolderCreate);
    el.emptyAdd.addEventListener("click", beginCreate);

    el.navAll.addEventListener("click", selectAll);
    el.navFavorites.addEventListener("click", selectFavorites);
    el.navImport.addEventListener("click", openImport);
    el.navExport.addEventListener("click", exportBookmarks);
    el.navHelp.addEventListener("click", function () {
      markView("nav-help");
      showPanel("help");
    });

    el.searchForm.addEventListener("submit", function (event) {
      event.preventDefault();
      var query = el.searchInput.value.trim();
      if (query.length === 0) {
        clearSearch();
        return;
      }

      runSearch(query);
    });

    el.searchClear.addEventListener("click", function () {
      clearSearch();
      el.searchInput.focus();
    });

    el.viewGrid.addEventListener("click", function () {
      state.viewMode = "grid";
      renderLibrary();
    });

    el.viewList.addEventListener("click", function () {
      state.viewMode = "list";
      renderLibrary();
    });

    el.detailBack.addEventListener("click", function () {
      state.selected = null;
      state.preview = null;
      showPanel("library");
      renderLibrary();
    });

    el.detailEdit.addEventListener("click", function () {
      if (state.selected) {
        beginEdit(state.selected);
      }
    });

    el.detailFavorite.addEventListener("click", function () {
      if (state.selected) {
        toggleFavorite(state.selected);
      }
    });

    el.detailPreview.addEventListener("click", function () {
      if (state.selected) {
        fetchPreview(state.selected);
      }
    });

    el.detailDelete.addEventListener("click", function () {
      if (state.selected) {
        deleteBookmark(state.selected);
      }
    });

    // The link is a real URL for copy/screen-reader use, but opening it must leave the frame.
    el.detailUrl.addEventListener("click", function (event) {
      event.preventDefault();
      openExternal(state.selected && state.selected.url);
    });

    el.editForm.addEventListener("submit", submitEdit);
    el.editCancel.addEventListener("click", cancelEdit);

    el.folderForm.addEventListener("submit", submitFolder);
    el.folderCancel.addEventListener("click", cancelFolder);
    el.folderDelete.addEventListener("click", deleteFolder);

    el.importForm.addEventListener("submit", submitImport);
    el.importDone.addEventListener("click", function () {
      markView(
        state.showFavorites
          ? "nav-favorites"
          : state.selectedFolderId
            ? "folder:" + state.selectedFolderId
            : "nav-all",
      );
      showPanel("library");
    });
  }

  function start() {
    cacheElements();
    paintIcons();
    wireEvents();

    if (bridge) {
      bridge
        .on("theme", function (info) {
          document.documentElement.setAttribute(
            "data-theme",
            info && info.theme === "dark" ? "dark" : "light",
          );
        })
        .on("init", function (info) {
          document.documentElement.setAttribute(
            "lang",
            (info && info.locale) || "en",
          );
        })
        .start({ title: "Bookmarks" });
    }

    markView("nav-all");
    if (el.folderEmpty) {
      el.folderEmpty.hidden = true;
    }

    loadLibrary();
  }

  start();
})();
