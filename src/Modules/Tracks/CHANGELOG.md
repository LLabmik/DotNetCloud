# Changelog

All notable changes to the Tracks module are documented here.

## 2.0.0

Track C: the module leaves the core and runs as a sandboxed host with its own schema, Blazor
WebAssembly UI and Home-page widget.

- **Data plane.** `TracksDbContext` and both provider migration sets are schema-neutral (no
  `HasDefaultSchema`, no `EnsureSchema`, no schema-qualified DDL) with their original migration ids
  preserved, and the host self-migrates into the schema the core provisions (`dataStore:
  "isolated"`, `handlesUserErasure: true`). `TracksDbInitializer` baselines an adopted core-managed
  schema instead of replaying `InitialCreate`, with retry/backoff at host startup.
- **Erasure.** `TracksUserErasureHandler` removes the account's owned products (through the same
  teardown path the app uses) and its assignments, watchers, comments, votes and memberships
  elsewhere, idempotently, and never logs the account id.
- **Sandboxed identity.** The host has no cookie scheme and no controller-level `[Authorize]`;
  identity comes only from the core-minted assertion, and a module-wide
  `TracksRequireUserAssertionFilter` keeps the actions that never resolve a caller (share links,
  guest access, templates, discussions, webhook deliveries, recurring-rule sweep) from being reached
  anonymously through the proxy.
- **Module-hosted UI.** The 97 Razor components moved into `DotNetCloud.Modules.Tracks.Web` (Blazor
  WebAssembly). The frame boots with absolute boot-resource URLs, carries the shell's design tokens
  in its own stylesheet, and follows `<html>` for theme. Deep links are `?kanban=<id>` and
  `?card=<id>`; `wwwroot/widget/` serves the Home-page card from `GET api/v1/widget/upcoming`.
- **Names, not ids.** New `UserDisplayNames` resolves user ids through the `IUserDirectory`
  capability in one batched call per payload, so work-item assignees, review-session participants and
  the analytics workload/roadmap/capacity rollups all carry display names.
- **Consent.** A missing capability grant surfaces as **403 `PROFILE_CONSENT_REQUIRED`** with the
  `consent-scopes` trailer so the frame can ask the shell for consent instead of showing an empty
  list.
- **Decoupled.** No `Core.Server`, `UI.Web` or CLI reference remains; the in-core page, the help
  page and the `Tracks.Widget` project are deleted and the library is a plain class library. Search
  results deep-link to `/modules/dotnetcloud.tracks?card=…`.
