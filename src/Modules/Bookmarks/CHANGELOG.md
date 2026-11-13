# Changelog

All notable changes to the Bookmarks module are documented here.

## 1.2.0

Track C: the module leaves the core and runs as a sandboxed host with its own UI and widget.

- **Data plane.** `BookmarksDbContext` and both provider migration sets are schema-neutral, and the
  host self-migrates into the schema the core provisions (`dataStore: "isolated"`). Adoption moves
  the historical `bookmarks` schema objects into the generated schema before ownership transfer.
- **Sandboxed host.** `module.json` topology, a module-private DataProtection ring under
  `DOTNETCLOUD_MODULE_DATA_DIR`, the scoped connection string, no cookie scheme, and an exception
  filter that turns a missing assertion into 401 instead of 500.
- **Module-hosted UI.** The module serves `wwwroot/index.html` + `assets/bookmarks-ui.js` (sidebar
  with views/folders/tools, grid and list, search, detail with preview, forms, import, export,
  in-frame help) and `wwwroot/widget/` for the Home-page card.
- **Erasure.** `BookmarksUserErasureHandler` deletes the user's items, folders and previews,
  idempotently per request id.
- **Decoupled.** No `Core.Server`, `UI.Web` or CLI reference remains; the in-core pages, help
  component, Blazor components and widget project are deleted, and the library is a plain class
  library.

## 1.1.x

- Core-hosted module (Blazor page inside `Core.Server`, core-managed migrations).

## 1.0.0

- Initial release: bookmark and folder management, previews, import/export, search indexing.
