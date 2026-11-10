/*
 * Photos module UI.
 *
 * Runs inside a sandboxed frame with an OPAQUE ORIGIN (no allow-same-origin), so:
 *   * no document.cookie / localStorage / sessionStorage — every request goes to this module's own
 *     server through the same proxied path this document was loaded from;
 *   * event.origin is "null" for every message — the parent is identified by the SDK bridge, so never
 *     add your own window.message listener;
 *   * the module never sees a session cookie: the core proxy injects a short-lived, module-audience
 *     user assertion on every request;
 *   * every string that comes from the server is user data and is written with textContent (or an
 *     attribute setter), never innerHTML.
 *
 * All requests are RELATIVE on purpose (see index.html): the core strips /apps/dotnetcloud.photos
 * before forwarding, so "api/v1/photos" resolves to this process's own REST API.
 */
(function () {
  "use strict";

  var API_PHOTOS = "api/v1/photos";
  var API_ALBUMS = "api/v1/albums";
  var PAGE_SIZE = 60;

  /* ---------------------------------------------------------------- icons */

  var ICONS = {
    add: "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z",
    photo_library:
      "M22 16V4c0-1.1-.9-2-2-2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2zm-11-4 2.03 2.71L16 11l4 5H8l3-4zM2 6v14c0 1.1.9 2 2 2h14v-2H4V6H2z",
    star: "M12 17.27 18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z",
    star_border:
      "M22 9.24l-7.19-.62L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21 12 17.27 18.18 21l-1.63-7.03L22 9.24zM12 15.4l-3.76 2.27 1-4.28-3.32-2.88 4.38-.38L12 6.1l1.71 4.04 4.38.38-3.32 2.88 1 4.28L12 15.4z",
    timeline: "M23 8c0 1.1-.9 2-2 2a1.7 1.7 0 0 1-.51-.07l-3.56 3.55c.05.16.07.34.07.52 0 1.1-.9 2-2 2s-2-.9-2-2c0-.18.02-.36.07-.52l-2.55-2.55c-.16.05-.34.07-.52.07s-.36-.02-.52-.07l-4.55 4.56c.05.16.07.33.07.51 0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2c.18 0 .35.02.51.07l4.56-4.55C8.02 9.36 8 9.18 8 9c0-1.1.9-2 2-2s2 .9 2 2c0 .18-.02.36-.07.52l2.55 2.55c.16-.05.34-.07.52-.07s.36.02.52.07l3.55-3.56A1.7 1.7 0 0 1 19 8c0-1.1.9-2 2-2s2 .9 2 2z",
    people:
      "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z",
    refresh:
      "M17.65 6.35A7.958 7.958 0 0 0 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0 1 12 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z",
    search:
      "M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z",
    grid_view: "M3 3h8v8H3zm10 0h8v8h-8zM3 13h8v8H3zm10 0h8v8h-8z",
    view_list: "M3 14h4v-4H3v4zm0 5h4v-4H3v4zM3 9h4V5H3v4zm5 5h13v-4H8v4zm0 5h13v-4H8v4zM8 5v4h13V5H8z",
    help: "M11 18h2v-2h-2v2zm1-16C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-2.21 0-4 1.79-4 4h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4-4z",
    close:
      "M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z",
    chevron_left: "M15.41 7.41 14 6l-6 6 6 6 1.41-1.41L10.83 12z",
    chevron_right: "M10 6 8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z",
    delete:
      "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z",
    download: "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z",
  };

  function buildIcon(name, className) {
    var path = ICONS[name] || ICONS.photo_library;
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
    var targets = scope.querySelectorAll("[data-icon]:not([data-icon-painted])");
    Array.prototype.forEach.call(targets, function (target) {
      target.setAttribute("data-icon-painted", "yes");
      target.appendChild(buildIcon(target.getAttribute("data-icon")));
    });
  }

  /** Replaces an icon button's glyph (favorite on/off, view mode). */
  function swapIcon(target, name) {
    if (!target) {
      return;
    }

    var previous = target.querySelector("svg");
    if (previous) {
      previous.remove();
    }

    target.appendChild(buildIcon(name));
  }

  /* ----------------------------------------------------------------- state */

  var state = {
    view: "all",
    photos: [],
    albums: [],
    shares: [],
    page: 0,
    hasMore: false,
    viewMode: "grid",
    selectedIndex: 0,
    selectedAlbumId: null,
    searchQuery: null,
    collapsed: false,
    busy: false,
    // photoId → { rotate, flipX, flipY } for edits queued but not yet saved to the pixels.
    previewTransforms: new Map(),
  };

  var el = {};
  var bridge = window.dncModuleBridge || null;

  /** Error carrying the HTTP status so a 404 can be treated as "no data" rather than a failure. */
  function ApiError(status, message, code) {
    this.name = "ApiError";
    this.status = status;
    this.message = message;
    this.code = code || null;
  }

  ApiError.prototype = Object.create(Error.prototype);

  /* ------------------------------------------------------------------- api */

  async function apiFetch(path, init) {
    var options = {
      method: (init && init.method) || "GET",
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    };

    if (init && init.body !== undefined) {
      options.body = init.body;
      options.headers["Content-Type"] = "application/json";
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
      var code = payload && payload.error && typeof payload.error.code === "string" ? payload.error.code : null;
      if (payload && payload.error && typeof payload.error.message === "string") {
        message = payload.error.message;
      }

      // Reading an original needs the user's `files:read` approval. The core refuses the call and the
      // module answers FILES_CONSENT_REQUIRED; ask the shell to show its consent prompt.
      if (code === "FILES_CONSENT_REQUIRED") {
        requestFileConsent();
        message =
          "DotNetCloud needs your approval to read this photo's original. Approve the request shown " +
          "above, then try again.";
      }

      throw new ApiError(response.status, message, code);
    }

    return payload;
  }

  /** Unwraps the module's { success, data } envelope. */
  function dataOf(payload) {
    return payload && payload.success ? payload.data : null;
  }

  /* --------------------------------------------------------------- helpers */

  function showStatus(message, isError) {
    if (!el.status) {
      return;
    }

    if (!message) {
      el.status.hidden = true;
      el.status.textContent = "";
      el.status.classList.remove("is-error");
      return;
    }

    el.status.hidden = false;
    el.status.textContent = message;
    el.status.classList.toggle("is-error", Boolean(isError));
  }

  function thumbnailUrl(photoId, size) {
    return (
      API_PHOTOS + "/" + encodeURIComponent(photoId) + "/thumbnail?size=" + (size || "grid")
    );
  }

  /**
   * Authenticates a URL the browser loads by itself (<img src>, <video src>, …). Those requests carry
   * neither the core session cookie (the frame's origin is opaque) nor the bridge's ticket header, so
   * the bridge appends the ticket to the query string instead. Never put a bare module URL in `src`.
   */
  function mediaUrl(url) {
    return bridge && typeof bridge.mediaUrl === "function" ? bridge.mediaUrl(url) : url;
  }

  /**
   * Asks the shell to show its consent prompt for reading the user's files.
   *
   * Reading an original (thumbnails, downloads, saving edits) is brokered by the core under the
   * `files:read` consent, so without it every one of those calls is refused. Asked at most once per
   * page: the shell keeps the prompt open until the user answers it.
   */
  var consentRequested = false;

  function requestFileConsent() {
    if (consentRequested || !bridge || typeof bridge.requestConsent !== "function") {
      return;
    }

    consentRequested = true;
    bridge.requestConsent(["files:read"]);
  }

  /* ------------------------------------------------- pending edit preview */

  var IDENTITY_TRANSFORM = { rotate: 0, flipX: false, flipY: false };

  /**
   * Derives how the recorded edit stack looks right now.
   *
   * Edits are non-destructive: until "Save edits" bakes them into the cached thumbnails, the pixels on
   * disk (and therefore the image the module can serve) still show the untouched photo. The viewer
   * previews the pending result with a CSS transform, so the user sees the rotation they just asked
   * for instead of nothing happening until they save.
   */
  function transformOfEditStack(stack) {
    var result = { rotate: 0, flipX: false, flipY: false };

    for (var i = 0; i < stack.length; i += 1) {
      var operation = stack[i] || {};
      var parameters = operation.parameters || {};

      if (operation.operationType === "Rotate") {
        var degrees = parseInt(parameters.value, 10);
        if (!isNaN(degrees)) {
          result.rotate = (result.rotate + degrees + 360) % 360;
        }
      } else if (operation.operationType === "Flip") {
        if (String(parameters.value) === "1") {
          result.flipY = !result.flipY;
        } else {
          result.flipX = !result.flipX;
        }
      }
    }

    return result;
  }

  function applyPreviewTransform() {
    var image = el.viewerImage;
    if (!image) {
      return;
    }

    var photo = currentPhoto();
    var transform = (photo && state.previewTransforms.get(photo.id)) || IDENTITY_TRANSFORM;

    image.style.transform =
      "rotate(" + transform.rotate + "deg) scaleX(" + (transform.flipX ? -1 : 1) + ") scaleY(" +
      (transform.flipY ? -1 : 1) + ")";
    image.classList.toggle("is-quarter-turn", transform.rotate === 90 || transform.rotate === 270);
  }

  /** Recomputes the preview from the photo's stored edit stack. */
  var previewSequence = 0;

  async function refreshPreviewTransform(photoId) {
    // Two quick rotations issue two refreshes; without the guard the earlier (smaller) stack can land
    // last and roll the preview back to a single turn.
    var token = ++previewSequence;

    try {
      var payload = await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photoId) + "/edits");
      if (token === previewSequence) {
        state.previewTransforms.set(photoId, transformOfEditStack(dataOf(payload) || []));
      }
    } catch (error) {
      // A missing stack is not worth interrupting the user; the previous preview stays.
    }

    if (token === previewSequence) {
      applyPreviewTransform();
    }
  }

  /** Applies one operation to the preview immediately, before the stack is re-read. */
  function applyPreviewOperation(photoId, operationType, parameters) {
    var current = state.previewTransforms.get(photoId) || IDENTITY_TRANSFORM;
    var next = { rotate: current.rotate, flipX: current.flipX, flipY: current.flipY };

    if (operationType === "Rotate") {
      var degrees = parseInt(parameters && parameters.value, 10);
      if (!isNaN(degrees)) {
        next.rotate = (next.rotate + degrees + 360) % 360;
      }
    } else if (operationType === "Flip") {
      if (String(parameters && parameters.value) === "1") {
        next.flipY = !next.flipY;
      } else {
        next.flipX = !next.flipX;
      }
    }

    state.previewTransforms.set(photoId, next);
    applyPreviewTransform();
  }

  function formatDate(value) {
    if (!value) {
      return "";
    }

    var date = new Date(value);
    if (isNaN(date.getTime())) {
      return "";
    }

    return date.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  function formatMonth(value) {
    var date = new Date(value);
    if (isNaN(date.getTime())) {
      return "Unknown";
    }

    return date.toLocaleDateString(undefined, { year: "numeric", month: "long" });
  }

  function describePhoto(photo) {
    var parts = [];
    if (photo.width && photo.height) {
      parts.push(photo.width + " × " + photo.height);
    }

    if (photo.takenAt) {
      parts.push(formatDate(photo.takenAt));
    }

    return parts.join(" · ");
  }

  function setPanelVisible(name) {
    var panels = { gallery: el.panelGallery, albums: el.panelAlbums, shared: el.panelShared, help: el.panelHelp };
    Object.keys(panels).forEach(function (key) {
      if (panels[key]) {
        panels[key].hidden = key !== name;
      }
    });
  }

  function markNav(activeId) {
    var items = document.querySelectorAll(".nav-item");
    Array.prototype.forEach.call(items, function (item) {
      item.classList.toggle("is-active", item.id === activeId);
    });
  }

  /* --------------------------------------------------------------- rendering */

  function renderGallery() {
    if (!el.grid || !el.list) {
      return;
    }

    el.grid.textContent = "";
    el.list.textContent = "";

    var isEmpty = state.photos.length === 0;
    if (el.galleryEmpty) {
      el.galleryEmpty.hidden = !isEmpty;
    }

    var useGrid = state.viewMode === "grid";
    el.grid.hidden = !useGrid;
    el.list.hidden = useGrid;

    state.photos.forEach(function (photo, index) {
      if (useGrid) {
        el.grid.appendChild(buildPhotoCard(photo, index));
      } else {
        el.list.appendChild(buildPhotoRow(photo, index));
      }
    });

    renderPager();
  }

  function buildPhotoCard(photo, index) {
    var card = document.createElement("button");
    card.type = "button";
    card.className = "photo-card";
    card.setAttribute("role", "listitem");
    card.title = photo.fileName;
    card.addEventListener("click", function () {
      openViewer(index);
    });

    var image = document.createElement("img");
    image.loading = "lazy";
    image.alt = photo.fileName;
    image.src = mediaUrl(thumbnailUrl(photo.id, "grid"));
    card.appendChild(image);

    if (photo.isFavorite) {
      var badge = document.createElement("span");
      badge.className = "photo-badge";
      badge.appendChild(buildIcon("star"));
      card.appendChild(badge);
    }

    return card;
  }

  function buildPhotoRow(photo, index) {
    var row = document.createElement("button");
    row.type = "button";
    row.className = "photo-row";
    row.setAttribute("role", "listitem");
    row.addEventListener("click", function () {
      openViewer(index);
    });

    var image = document.createElement("img");
    image.loading = "lazy";
    image.alt = "";
    image.src = mediaUrl(thumbnailUrl(photo.id, "grid"));
    row.appendChild(image);

    var main = document.createElement("span");
    main.className = "photo-row-main";

    var name = document.createElement("span");
    name.className = "photo-row-name";
    name.textContent = photo.fileName;
    main.appendChild(name);

    var meta = document.createElement("span");
    meta.className = "photo-row-meta";
    meta.textContent = describePhoto(photo);
    main.appendChild(meta);

    row.appendChild(main);

    if (photo.isFavorite) {
      row.appendChild(buildIcon("star"));
    }

    return row;
  }

  function renderPager() {
    if (!el.pager) {
      return;
    }

    var visible = state.view !== "albums" && state.view !== "help" && (state.page > 0 || state.hasMore);
    el.pager.hidden = !visible;

    if (el.pagePrev) {
      el.pagePrev.disabled = state.page === 0;
    }

    if (el.pageNext) {
      el.pageNext.disabled = !state.hasMore;
    }

    if (el.pageText) {
      el.pageText.textContent = "Page " + (state.page + 1);
    }
  }

  function renderAlbums() {
    if (!el.albumGrid) {
      return;
    }

    el.albumGrid.textContent = "";

    if (el.albumsEmpty) {
      el.albumsEmpty.hidden = state.albums.length > 0;
    }

    state.albums.forEach(function (album) {
      el.albumGrid.appendChild(buildAlbumCard(album));
    });
  }

  function buildAlbumCard(album) {
    var card = document.createElement("button");
    card.type = "button";
    card.className = "album-card";
    card.setAttribute("role", "listitem");
    card.addEventListener("click", function () {
      openAlbum(album.id, album.title);
    });

    if (album.coverPhotoId) {
      var cover = document.createElement("img");
      cover.className = "album-cover";
      cover.loading = "lazy";
      cover.alt = "";
      cover.src = mediaUrl(thumbnailUrl(album.coverPhotoId, "grid"));
      card.appendChild(cover);
    }

    var title = document.createElement("span");
    title.className = "album-title";
    title.textContent = album.title;
    card.appendChild(title);

    var meta = document.createElement("span");
    meta.className = "album-meta";
    meta.textContent =
      album.photoCount + (album.photoCount === 1 ? " photo" : " photos") +
      (album.isShared ? " · shared" : "");
    card.appendChild(meta);

    return card;
  }

  function renderAlbumSidebar() {
    if (!el.albumList) {
      return;
    }

    el.albumList.textContent = "";

    if (el.albumEmpty) {
      el.albumEmpty.hidden = state.albums.length > 0;
    }

    state.albums.forEach(function (album) {
      var item = document.createElement("button");
      item.type = "button";
      item.className = "nav-item";
      item.classList.toggle("is-active", state.selectedAlbumId === album.id);
      item.addEventListener("click", function () {
        openAlbum(album.id, album.title);
      });

      item.appendChild(buildIcon("photo_library"));

      var text = document.createElement("span");
      text.className = "nav-text";
      text.textContent = album.title;
      item.appendChild(text);

      var count = document.createElement("span");
      count.className = "nav-count";
      count.textContent = String(album.photoCount);
      item.appendChild(count);

      el.albumList.appendChild(item);
    });
  }

  function renderShares() {
    if (!el.sharedList) {
      return;
    }

    el.sharedList.textContent = "";

    if (el.sharedEmpty) {
      el.sharedEmpty.hidden = state.shares.length > 0;
    }

    state.shares.forEach(function (share) {
      var item = document.createElement("div");
      item.className = "shared-item";

      var main = document.createElement("div");
      main.className = "shared-main";

      var title = document.createElement("div");
      title.className = "shared-title";
      title.textContent = share.albumId ? "Album" : "Photo";
      main.appendChild(title);

      var meta = document.createElement("div");
      meta.className = "shared-meta";
      meta.textContent = share.permission + " · shared " + formatDate(share.createdAt);
      main.appendChild(meta);

      item.appendChild(main);

      if (share.albumId) {
        var openAlbumButton = document.createElement("button");
        openAlbumButton.type = "button";
        openAlbumButton.className = "btn btn-secondary";
        openAlbumButton.textContent = "Open album";
        openAlbumButton.addEventListener("click", function () {
          openAlbum(share.albumId, "Shared album");
        });
        item.appendChild(openAlbumButton);
      } else if (share.photoId) {
        var openPhotoButton = document.createElement("button");
        openPhotoButton.type = "button";
        openPhotoButton.className = "btn btn-secondary";
        openPhotoButton.textContent = "View photo";
        openPhotoButton.addEventListener("click", function () {
          openSharedPhoto(share.photoId);
        });
        item.appendChild(openPhotoButton);
      }

      el.sharedList.appendChild(item);
    });
  }

  function renderAlbumOptions() {
    if (!el.albumSelect) {
      return;
    }

    el.albumSelect.textContent = "";

    var placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = state.albums.length ? "Choose an album…" : "No albums yet";
    el.albumSelect.appendChild(placeholder);

    state.albums.forEach(function (album) {
      var option = document.createElement("option");
      option.value = album.id;
      option.textContent = album.title;
      el.albumSelect.appendChild(option);
    });
  }

  /* ------------------------------------------------------------------ loading */

  async function loadPhotos(options) {
    var view = (options && options.view) || state.view;
    var page = options && typeof options.page === "number" ? options.page : 0;
    var append = Boolean(options && options.append);
    var query = options && options.query ? options.query : null;

    setBusy(true);

    try {
      var photos;

      if (view === "favorites") {
        photos = dataOf(await apiFetch(API_PHOTOS + "/favorites")) || [];
      } else if (view === "search" && query) {
        photos = dataOf(await apiFetch(API_PHOTOS + "/search?q=" + encodeURIComponent(query))) || [];
      } else if (view === "timeline") {
        var now = new Date();
        var from = new Date(1970, 0, 1);
        var to = new Date(now.getTime() + 24 * 60 * 60 * 1000);
        photos =
          dataOf(
            await apiFetch(
              API_PHOTOS +
                "/timeline?from=" + encodeURIComponent(from.toISOString()) +
                "&to=" + encodeURIComponent(to.toISOString()),
            ),
          ) || [];
      } else if (view === "album" && state.selectedAlbumId) {
        photos =
          dataOf(await apiFetch(API_ALBUMS + "/" + encodeURIComponent(state.selectedAlbumId) + "/photos")) || [];
      } else {
        photos =
          dataOf(
            await apiFetch(
              API_PHOTOS + "?skip=" + page * PAGE_SIZE + "&take=" + PAGE_SIZE,
            ),
          ) || [];

        state.hasMore = photos.length === PAGE_SIZE;
      }

      if (view !== "all") {
        state.hasMore = false;
      }

      if (view === "timeline") {
        photos.sort(function (a, b) {
          return new Date(b.takenAt).getTime() - new Date(a.takenAt).getTime();
        });
      }

      state.page = page;
      state.photos = append ? state.photos.concat(photos) : photos;
      renderGallery();
      showStatus("");
    } catch (error) {
      showStatus(error.message || "Could not load photos.", true);
    } finally {
      setBusy(false);
    }
  }

  async function loadAlbums() {
    try {
      state.albums = dataOf(await apiFetch(API_ALBUMS)) || [];
      renderAlbumSidebar();
      renderAlbums();
      renderAlbumOptions();
    } catch (error) {
      showStatus(error.message || "Could not load albums.", true);
    }
  }

  async function loadShares() {
    try {
      state.shares = dataOf(await apiFetch(API_PHOTOS + "/shared-with-me")) || [];
      renderShares();
    } catch (error) {
      showStatus(error.message || "Could not load shared items.", true);
    }
  }

  function setBusy(busy) {
    state.busy = busy;
    document.body.classList.toggle("is-busy", busy);
  }

  /* ------------------------------------------------------------------- views */

  function showLibrary(view, title) {
    state.view = view;
    state.selectedAlbumId = null;
    state.page = 0;
    state.photos = [];
    state.searchQuery = null;
    if (el.viewTitle) {
      el.viewTitle.textContent = title;
    }

    setPanelVisible("gallery");
    renderGallery();
    loadPhotos({ view: view, page: 0 });
  }

  function showAlbumsPanel() {
    state.view = "albums";
    state.selectedAlbumId = null;
    if (el.viewTitle) {
      el.viewTitle.textContent = "Albums";
    }

    setPanelVisible("albums");
    renderAlbums();
  }

  function openAlbum(albumId, title) {
    state.view = "album";
    state.selectedAlbumId = albumId;
    state.page = 0;

    var resolved = title;
    if (!resolved) {
      state.albums.forEach(function (album) {
        if (album.id === albumId) {
          resolved = album.title;
        }
      });
    }

    if (el.viewTitle) {
      el.viewTitle.textContent = resolved || "Album";
    }

    markNav("");
    setPanelVisible("gallery");
    renderAlbumSidebar();
    loadPhotos({ view: "album", page: 0 });
  }

  function showShared() {
    state.view = "shared";
    state.selectedAlbumId = null;
    if (el.viewTitle) {
      el.viewTitle.textContent = "Shared with me";
    }

    setPanelVisible("shared");
    loadShares();
  }

  function showHelp() {
    state.view = "help";
    state.selectedAlbumId = null;
    if (el.viewTitle) {
      el.viewTitle.textContent = "Help";
    }

    setPanelVisible("help");
  }

  async function openSharedPhoto(photoId) {
    try {
      var photo = dataOf(await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photoId)));
      if (!photo) {
        showStatus("That photo is no longer shared with you.", true);
        return;
      }

      state.photos = [photo];
      openViewer(0);
    } catch (error) {
      showStatus(error.message || "Could not open that photo.", true);
    }
  }

  /**
   * Opens whatever the shell deep-linked to (`?photoId=` from a search hit or the Home widget,
   * `?album=` from a share notification). The core forwards the shell's query into this frame, so
   * the browser URL and the framed document agree on what the user clicked.
   */
  async function handleDeepLink() {
    var params;

    try {
      params = new URLSearchParams(window.location.search || "");
    } catch (error) {
      return;
    }

    var albumId = params.get("album");
    if (albumId) {
      markNav("");
      await openAlbum(albumId, "");
      return;
    }

    var photoId = params.get("photoId");
    if (photoId) {
      await openSharedPhoto(photoId);
    }
  }

  /* ------------------------------------------------------------------ viewer */

  function currentPhoto() {
    return state.photos[state.selectedIndex] || null;
  }

  function openViewer(index) {
    state.selectedIndex = typeof index === "number" ? index : 0;
    renderViewer();

    if (el.viewer && !el.viewer.open) {
      el.viewer.showModal();
    }
  }

  function closeViewer() {
    if (el.viewer && el.viewer.open) {
      el.viewer.close();
    }
  }

  function stepViewer(delta) {
    var next = state.selectedIndex + delta;
    if (next < 0 || next >= state.photos.length) {
      return;
    }

    state.selectedIndex = next;
    renderViewer();
  }

  function renderViewer() {
    var photo = currentPhoto();
    if (!photo || !el.viewerImage) {
      return;
    }

    el.viewerImage.src = mediaUrl(thumbnailUrl(photo.id, "detail"));
    el.viewerImage.alt = photo.fileName;
    applyPreviewTransform();

    // A stack that is not saved yet must still be visible when the viewer opens (or steps to another
    // photo), so the preview is derived from the stored stack rather than from a click counter.
    if (!state.previewTransforms.has(photo.id)) {
      refreshPreviewTransform(photo.id);
    }

    if (el.viewerTitle) {
      el.viewerTitle.textContent = photo.fileName;
    }

    if (el.viewerSubtitle) {
      el.viewerSubtitle.textContent = describePhoto(photo);
    }

    if (el.viewerCaption) {
      el.viewerCaption.textContent =
        (photo.tags && photo.tags.length ? "#" + photo.tags.join(" #") + " · " : "") +
        (photo.hasEdits ? "edited · " : "") +
        formatDate(photo.createdAt);
    }

    swapIcon(el.viewerFavorite, photo.isFavorite ? "star" : "star_border");
    if (el.viewerFavorite) {
      el.viewerFavorite.classList.toggle("is-active", photo.isFavorite);
      el.viewerFavorite.title = photo.isFavorite ? "Remove from favorites" : "Add to favorites";
    }

    if (el.viewerPrev) {
      el.viewerPrev.disabled = state.selectedIndex === 0;
    }

    if (el.viewerNext) {
      el.viewerNext.disabled = state.selectedIndex >= state.photos.length - 1;
    }

    if (el.albumSelect) {
      el.albumSelect.value = "";
    }
  }

  async function toggleFavorite() {
    var photo = currentPhoto();
    if (!photo) {
      return;
    }

    try {
      var updated = dataOf(
        await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photo.id) + "/favorite", { method: "POST" }),
      );

      if (updated) {
        photo.isFavorite = updated.isFavorite;
      } else {
        photo.isFavorite = !photo.isFavorite;
      }

      renderViewer();
      renderGallery();
    } catch (error) {
      showStatus(error.message || "Could not update the favorite flag.", true);
    }
  }

  async function deleteCurrentPhoto() {
    var photo = currentPhoto();
    if (!photo) {
      return;
    }

    if (!window.confirm("Remove \u201C" + photo.fileName + "\u201D from your photo library?\n\nThe file itself stays in your cloud files.")) {
      return;
    }

    try {
      await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photo.id), { method: "DELETE" });
      state.photos.splice(state.selectedIndex, 1);

      if (state.photos.length === 0) {
        closeViewer();
      } else {
        state.selectedIndex = Math.min(state.selectedIndex, state.photos.length - 1);
        renderViewer();
      }

      renderGallery();
      showStatus("Removed from the library.");
    } catch (error) {
      showStatus(error.message || "Could not remove that photo.", true);
    }
  }

  function downloadCurrentPhoto() {
    var photo = currentPhoto();
    if (!photo) {
      return;
    }

    // A same-frame navigation cannot download from an opaque origin, so the bridge opens a
    // core-controlled tab instead. The URL is module-relative; the bridge prefixes the app path.
    if (bridge && typeof bridge.openExternal === "function") {
      bridge.openExternal(
        API_PHOTOS + "/" + encodeURIComponent(photo.id) + "/download",
      );
      return;
    }

    showStatus("Downloads are only available inside DotNetCloud.", true);
  }

  /* ------------------------------------------------------------------- edits */

  async function applyEdit(operationType, parameters) {
    var photo = currentPhoto();
    if (!photo) {
      return;
    }

    try {
      await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photo.id) + "/edits", {
        method: "POST",
        body: JSON.stringify({ operationType: operationType, parameters: parameters || {} }),
      });

      // The stored pixels are untouched until the stack is saved; the preview carries the change so
      // the user sees the rotation now rather than after "Save edits".
      applyPreviewOperation(photo.id, operationType, parameters);
      await refreshPreviewTransform(photo.id);
      showStatus("Edit queued. Choose Save edits to apply it to the photo.");
    } catch (error) {
      showStatus(error.message || "Could not apply that edit.", true);
    }
  }

  async function undoEdit() {
    var photo = currentPhoto();
    if (!photo) {
      return;
    }

    try {
      await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photo.id) + "/edits/last", { method: "DELETE" });
      await refreshPreviewTransform(photo.id);
      showStatus("Last edit undone.");
    } catch (error) {
      showStatus(error.message || "Could not undo that edit.", true);
    }
  }

  async function saveEdits() {
    var photo = currentPhoto();
    if (!photo) {
      return;
    }

    try {
      await apiFetch(API_PHOTOS + "/" + encodeURIComponent(photo.id) + "/edits/save", { method: "POST" });
      photo.hasEdits = true;

      // The edits are baked into the served thumbnails now, so drop the CSS preview and refetch the
      // image, or the viewer would show the transform on top of the rotated pixels.
      state.previewTransforms.delete(photo.id);
      applyPreviewTransform();
      el.viewerImage.src = mediaUrl(thumbnailUrl(photo.id, "detail") + "&t=" + Date.now());
      renderGallery();
      showStatus("Saved. The photo now shows those edits everywhere.");
    } catch (error) {
      showStatus(error.message || "Could not save edits.", true);
    }
  }

  /* ------------------------------------------------------------------ albums */

  function openAlbumDialog() {
    if (!el.albumDialog) {
      return;
    }

    if (el.albumTitle) {
      el.albumTitle.value = "";
    }

    if (el.albumDescription) {
      el.albumDescription.value = "";
    }

    el.albumDialog.showModal();
    if (el.albumTitle) {
      el.albumTitle.focus();
    }
  }

  async function createAlbum() {
    var title = el.albumTitle ? el.albumTitle.value.trim() : "";
    if (!title) {
      return;
    }

    try {
      await apiFetch(API_ALBUMS, {
        method: "POST",
        body: JSON.stringify({
          title: title,
          description: el.albumDescription ? el.albumDescription.value.trim() || null : null,
        }),
      });

      if (el.albumDialog) {
        el.albumDialog.close();
      }

      await loadAlbums();
      showStatus("Album created.");
    } catch (error) {
      showStatus(error.message || "Could not create that album.", true);
    }
  }

  async function addCurrentPhotoToAlbum() {
    var photo = currentPhoto();
    var albumId = el.albumSelect ? el.albumSelect.value : "";

    if (!photo || !albumId) {
      showStatus("Choose an album first.", true);
      return;
    }

    try {
      await apiFetch(
        API_ALBUMS + "/" + encodeURIComponent(albumId) + "/photos/" + encodeURIComponent(photo.id),
        { method: "POST" },
      );
      await loadAlbums();
      showStatus("Added to the album.");
    } catch (error) {
      showStatus(error.message || "Could not add that photo to the album.", true);
    }
  }

  async function removeCurrentPhotoFromAlbum() {
    var photo = currentPhoto();
    var albumId = el.albumSelect ? el.albumSelect.value : "";

    if (!photo || !albumId) {
      showStatus("Choose an album first.", true);
      return;
    }

    try {
      await apiFetch(
        API_ALBUMS + "/" + encodeURIComponent(albumId) + "/photos/" + encodeURIComponent(photo.id),
        { method: "DELETE" },
      );
      await loadAlbums();

      if (state.view === "album" && state.selectedAlbumId === albumId) {
        await loadPhotos({ view: "album", page: 0 });
      }

      showStatus("Removed from the album.");
    } catch (error) {
      showStatus(error.message || "Could not remove that photo from the album.", true);
    }
  }

  /* ------------------------------------------------------------------ wiring */

  function cacheElements() {
    el.status = document.getElementById("status");
    el.viewTitle = document.getElementById("view-title");
    el.grid = document.getElementById("photo-grid");
    el.list = document.getElementById("photo-list");
    el.galleryEmpty = document.getElementById("gallery-empty");
    el.panelGallery = document.getElementById("panel-gallery");
    el.panelAlbums = document.getElementById("panel-albums");
    el.panelShared = document.getElementById("panel-shared");
    el.panelHelp = document.getElementById("panel-help");
    el.pager = document.getElementById("pager");
    el.pagePrev = document.getElementById("page-prev");
    el.pageNext = document.getElementById("page-next");
    el.pageText = document.getElementById("page-text");
    el.viewMode = document.getElementById("view-mode");
    el.albumList = document.getElementById("album-list");
    el.albumEmpty = document.getElementById("album-empty");
    el.albumGrid = document.getElementById("album-grid");
    el.albumsEmpty = document.getElementById("albums-empty");
    el.sharedList = document.getElementById("shared-list");
    el.sharedEmpty = document.getElementById("shared-empty");
    el.searchForm = document.getElementById("search-form");
    el.searchInput = document.getElementById("search-input");
    el.sidebar = document.querySelector(".sidebar");
    el.sidebarToggle = document.getElementById("sidebar-toggle");
    el.viewer = document.getElementById("viewer");
    el.viewerImage = document.getElementById("viewer-image");
    el.viewerTitle = document.getElementById("viewer-title");
    el.viewerSubtitle = document.getElementById("viewer-subtitle");
    el.viewerCaption = document.getElementById("viewer-caption");
    el.viewerFavorite = document.getElementById("viewer-favorite");
    el.viewerDownload = document.getElementById("viewer-download");
    el.viewerDelete = document.getElementById("viewer-delete");
    el.viewerClose = document.getElementById("viewer-close");
    el.viewerPrev = document.getElementById("viewer-prev");
    el.viewerNext = document.getElementById("viewer-next");
    el.editRotateLeft = document.getElementById("edit-rotate-left");
    el.editRotateRight = document.getElementById("edit-rotate-right");
    el.editFlip = document.getElementById("edit-flip");
    el.editUndo = document.getElementById("edit-undo");
    el.editSave = document.getElementById("edit-save");
    el.albumSelect = document.getElementById("album-select");
    el.albumAdd = document.getElementById("album-add");
    el.albumRemove = document.getElementById("album-remove");
    el.albumDialog = document.getElementById("album-dialog");
    el.albumForm = document.getElementById("album-form");
    el.albumTitle = document.getElementById("album-title");
    el.albumDescription = document.getElementById("album-description");
    el.albumCancel = document.getElementById("album-cancel");
  }

  function on(id, event, handler, capture) {
    var target = document.getElementById(id);
    if (target) {
      target.addEventListener(event, handler, capture === true);
    }
  }

  function wireEvents() {
    // A thumbnail that was never generated is produced on first view, and that read needs the user's
    // `files:read` approval — a broken <img> is the first place the user meets that rule, so ask for
    // consent there rather than leaving a grey box. `error` does not bubble, hence capture.
    on("photo-grid", "error", requestFileConsent, true);

    on("nav-all", "click", function () {
      markNav("nav-all");
      showLibrary("all", "All photos");
    });

    on("nav-favorites", "click", function () {
      markNav("nav-favorites");
      showLibrary("favorites", "Favorites");
    });

    on("nav-timeline", "click", function () {
      markNav("nav-timeline");
      showLibrary("timeline", "Timeline");
    });

    on("nav-shared", "click", function () {
      markNav("nav-shared");
      showShared();
    });

    on("nav-refresh", "click", async function () {
      await loadAlbums();
      if (state.view === "all" || state.view === "favorites" || state.view === "timeline" || state.view === "album") {
        await loadPhotos({ view: state.view, page: 0 });
      } else if (state.view === "shared") {
        await loadShares();
      }

      showStatus("Refreshed.");
    });

    on("nav-help", "click", function () {
      markNav("nav-help");
      showHelp();
    });

    on("nav-new-album", "click", openAlbumDialog);

    if (el.sidebarToggle) {
      el.sidebarToggle.addEventListener("click", function () {
        state.collapsed = !state.collapsed;
        if (el.sidebar) {
          el.sidebar.classList.toggle("is-collapsed", state.collapsed);
        }

        swapIcon(el.sidebarToggle, state.collapsed ? "chevron_right" : "chevron_left");
      });
    }

    if (el.viewMode) {
      el.viewMode.addEventListener("click", function () {
        state.viewMode = state.viewMode === "grid" ? "list" : "grid";
        swapIcon(el.viewMode, state.viewMode === "grid" ? "grid_view" : "view_list");
        renderGallery();
      });
    }

    if (el.searchForm) {
      el.searchForm.addEventListener("submit", function (event) {
        event.preventDefault();
        var query = el.searchInput ? el.searchInput.value.trim() : "";
        if (!query) {
          markNav("nav-all");
          showLibrary("all", "All photos");
          return;
        }

        state.view = "search";
        state.searchQuery = query;
        state.page = 0;
        markNav("");
        setPanelVisible("gallery");
        if (el.viewTitle) {
          el.viewTitle.textContent = "Search: " + query;
        }

        loadPhotos({ view: "search", page: 0, query: query });
      });
    }

    on("page-prev", "click", function () {
      if (state.page === 0) {
        return;
      }

      loadPhotos({ view: state.view, page: state.page - 1 });
    });

    on("page-next", "click", function () {
      if (!state.hasMore) {
        return;
      }

      loadPhotos({ view: state.view, page: state.page + 1 });
    });

    on("viewer-close", "click", closeViewer);
    on("viewer-prev", "click", function () {
      stepViewer(-1);
    });
    on("viewer-next", "click", function () {
      stepViewer(1);
    });
    on("viewer-favorite", "click", toggleFavorite);
    on("viewer-download", "click", downloadCurrentPhoto);
    on("viewer-delete", "click", deleteCurrentPhoto);

    // `value` is the key the image pipeline reads. Rotation is clockwise degrees (0/90/180/270), so a
    // quarter turn anticlockwise is 270, not -90 — ImageSharp has no negative rotate mode.
    on("edit-rotate-left", "click", function () {
      applyEdit("Rotate", { value: "270" });
    });
    on("edit-rotate-right", "click", function () {
      applyEdit("Rotate", { value: "90" });
    });
    // 0 = horizontal flip, 1 = vertical.
    on("edit-flip", "click", function () {
      applyEdit("Flip", { value: "0" });
    });
    on("edit-undo", "click", undoEdit);
    on("edit-save", "click", saveEdits);

    on("album-add", "click", addCurrentPhotoToAlbum);
    on("album-remove", "click", removeCurrentPhotoFromAlbum);
    on("album-cancel", "click", function () {
      if (el.albumDialog) {
        el.albumDialog.close();
      }
    });

    if (el.albumForm) {
      el.albumForm.addEventListener("submit", function (event) {
        event.preventDefault();
        createAlbum();
      });
    }

    if (el.viewer) {
      el.viewer.addEventListener("keydown", function (event) {
        if (event.key === "ArrowLeft") {
          stepViewer(-1);
        } else if (event.key === "ArrowRight") {
          stepViewer(1);
        }
      });

      // Clicking the backdrop (the dialog element itself, outside its children) closes the viewer.
      el.viewer.addEventListener("click", function (event) {
        if (event.target === el.viewer) {
          closeViewer();
        }
      });
    }
  }

  /* ------------------------------------------------------------------- start */

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
          document.documentElement.setAttribute("lang", (info && info.locale) || "en");
        })
        .start({ title: "Photos" });
    }

    markNav("nav-all");
    setPanelVisible("gallery");

    Promise.all([loadAlbums(), loadPhotos({ view: "all", page: 0 })]).then(function () {
      // Deep links open after the first load so an album's title and a photo's neighbour list exist.
      handleDeepLink();
    });
  }

  start();
})();
