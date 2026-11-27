# Changelog

All notable changes to the Photos module are documented here.

## 2.0.0

Track C: the module leaves the core and runs as a sandboxed host with its own UI and widget.

- **Data plane.** `PhotosDbContext` and both provider migration sets are schema-neutral (no
  `HasDefaultSchema`, no `EnsureSchema`, no schema-qualified DDL) with their original migration ids
  preserved, and the host self-migrates into the schema the core provisions (`dataStore:
  "isolated"`). `PhotosDbInitializer` baselines an adopted core-managed schema instead of replaying
  `InitialCreate`.
- **Media path rebuilt.** The host no longer references the Files data layer or reads the shared
  storage root. Originals are read through the core's module API (`IFileDirectory`, consent
  `files:read`) on the acting user's behalf; thumbnails are generated lazily on first view because a
  background thread has no user to assert. Only the owner can generate; a shared photo keeps the
  owner's cached previews, and the original answers 404 for anyone else.
- **Erasure.** `PhotosUserErasureHandler` deletes the account's photos, albums, album links,
  metadata, tags, edit records and shares in both directions, idempotently, and never logs the
  account id.
- **Indexing.** The `FileUploadedEvent` subscription is gone (a sandboxed module cannot see another
  module's events). The core's media import calls `IndexPhoto` / `GetIndexedFileNodeIds` /
  `RemoveDeletedPhotos` with a core-signed assertion instead.
- **Module-hosted UI.** `wwwroot/index.html` + `assets/photos-ui.js` (paged library, favourites,
  timeline, search, albums, viewer with non-destructive rotate/flip + save, download,
  remove-from-library, shared-with-me, in-frame help) and `wwwroot/widget/` for the Home-page card.
  The shell's query string is forwarded into the frame, so `?photoId=` / `?album=` open one item.
- **Shared with me.** New `GetSharedWithMe` RPC keeps the Files "Shared with me → Photos" entries
  working now that the core cannot read the module's tables.
- **Decoupled.** No `Core.Server`, `UI.Web` or CLI reference remains; the in-core page, help page,
  layout script and the widget project are gone, and the library is a plain class library.
- **Notifications.** Album-share notifications now fan out to team members from the module itself and
  deep-link into the framed app; the core's duplicate producer for this event is removed.
