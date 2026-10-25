# Blazor Code & Style Standards Plan

**Status:** ✅ complete (2026-10-24) — all `@code`, `<style>` and multi-declaration inline styles migrated; the CI
allowlist is deleted and the gate is unconditional.
**Created:** 2026-10-24
**Branch:** `fix/blazor-code-standards`
**Rule location:** `.github/copilot-instructions.md` (§ *Blazor Code & Style Separation*), `CLAUDE.md`, `CONTRIBUTING.md` (§ *Coding Standards → Blazor Code & Style Separation*)

---

## 1. The standard

A `.razor` file contains **markup and Razor directives only**.

| Concern | Allowed in `.razor` | Must live in |
| --- | --- | --- |
| `@code { … }` / `@functions { … }` | ❌ never | `Component.razor.cs` |
| `<style> … </style>` | ❌ never | `Component.razor.css` |
| Static `style="…"` with **2+ declarations** (`style="font-weight: bold; background-color: blue;"`) | ❌ never | `Component.razor.css` |
| Static `style="…"` with **1 declaration** (`style="font-weight: bold"`) | ✅ allowed — do not crowd `.razor.css` with one-liners | — |
| `@page`, `@using`, `@inject`, `@inherits`, `@implements`, `@attribute`, `@layout`, `@typeparam`, `@rendermode` | ✅ always | — |
| Markup, `@if`/`@foreach`, `@bind`, event attributes | ✅ always | — |
| Runtime-dependent inline style (`style="--pct: @pct"`, `style="width:@pct%"`) | ✅ allowed (prefer CSS custom properties) | — |
| `<style>` rendered as *content* (email/HTML preview `MarkupString`) | ✅ exempt | — |

**Code-behind shape**

```csharp
namespace <same namespace as the .razor file>;

/// <summary>Code-behind for the <see cref="Foo"/> component.</summary>
public partial class Foo : ComponentBase   // omit `: ComponentBase` when the .razor has @inherits
{
    // body of the old @code block, dedented
}
```

**Rationale:** markup stays readable; logic becomes compiler/analyzer-visible C# (testable, greppable, refactorable); CSS goes through Blazor CSS isolation instead of leaking globally; every component looks the same.

---

## 2. Audit method (reproducible)

```bash
# files with an inline @code (or legacy @functions) block
rg -l '^\s*@(code|functions)\s*\{' --glob '*.razor'

# files with an inline <style> block
rg -l '<style' --glob '*.razor'

# static inline style attributes (no Razor expression in the value)
rg -l 'style="[^"@$]*"' --glob '*.razor'

# sidecar presence check
for f in $(rg -l '^\s*@code\s*\{' --glob '*.razor'); do [ -f "$f.cs" ] || echo "NO CODE-BEHIND: $f"; done

# @code body size (used for phase sizing)
for f in $(rg -l '^\s*@code\s*\{' --glob '*.razor'); do
  n=$(awk '/^[[:space:]]*@code[[:space:]]*\{/{flag=1} flag{c++} END{print c+0}' "$f")
  echo "$n $f"
done | sort -rn
```

---

## 3. Baseline (2026-10-24)

| Metric | Count |
| --- | ---: |
| `.razor` files | 286 |
| `.razor.cs` files | 95 |
| `.razor.css` files | 68 |
| `.razor` files with an inline `@code` block | **126** |
| Lines of C# inside those `@code` blocks | **14 510** |
| `.razor` files with an inline `<style>` block | **8** (≈ 1 099 CSS lines) |
| `.razor` files with both `@code` and `<style>` | 4 |
| Files with static inline `style="…"` attributes | 34 (202 occurrences) |
| Legacy `@functions` blocks | 0 |
| Already compliant (code-behind or no code at all) | 160 |
| Partially migrated (has `@code` **and** a `.razor.cs`) | 1 — `Tracks/UI/WorkItemListView.razor` |

**Phase sizing**

| Bucket | `@code` lines | Files |
| --- | --- | ---: |
| Small | < 50 | 67 |
| Medium | 50 – 199 | 40 |
| Large | ≥ 200 | 19 |

**Already fully compliant module UI:** Chat, Files (except `DocumentEditor.razor`), Photos, Video, Music — their components already use code-behind/scoped CSS. The debt is concentrated in the older `UI.Web`, `UI.Web.Client`, `UI.Shared`, and the Calendar / Email / Notes / Tracks / Bookmarks / About / AI modules.

---

## 4. Findings that change the implementation order

### 4.1 `DotNetCloud.UI.Web.Client` scoped CSS was inert — FIXED in Phase 0

`App.razor` only linked `DotNetCloud.Core.Server.styles.css`. The client project (`Microsoft.NET.Sdk.BlazorWebAssembly`) emits `DotNetCloud.UI.Web.Client.styles.css` into the server's `wwwroot` root (confirmed in `DotNetCloud.Core.Server.staticwebassets.endpoints.json`), but **nothing linked it**, so every `.razor.css` under `src/UI/DotNetCloud.UI.Web.Client/` was dead in production.

**Resolved:** `App.razor` now links `DotNetCloud.UI.Web.Client.styles.css` immediately after the server bundle, with a comment explaining why the link must not be removed. The five previously inert stylesheets (`FilesSharedFolders`, `Health`, `SearchAdmin`, `SystemPage`, `Updates`) are now active — this is an intentional visual change on those admin pages (they were authored against these rules).

### 4.2 Inline `<style>` is global; scoped CSS is not

Today an inline `<style>` block leaks to the whole document. Moving it to `.razor.css` scopes it to the component subtree. For each of the 8 files, before moving:

1. `rg '<class-name>' --glob '*.razor' --glob '*.css'` for each class defined in the block — another component may depend on the global leak.
2. Check whether the styled element is rendered inside the component's own DOM (these 8 all are: overlays, toasts, a palette, a document editor, a toggle). If a rule targets a child component's internals, `::deep` is required.
3. Check for `body`/`html`/`:root` selectors or `@keyframes` — keep those global (they cannot be scoped) and move them to `wwwroot/css/app.css` + bump the `?v=` cache-buster.

### 4.3 Code-behind migration gotchas

| Gotcha | Handling |
| --- | --- |
| `@using` in the `.razor` (or `_Imports.razor`) does **not** apply to `.razor.cs` | Add explicit `using` statements to the new `.razor.cs`; the build will flag what is missing. |
| `@inherits Base` present | Do **not** re-declare the base type in the code-behind (7 files affected — all `Dnc*` form components + `MainLayout`). |
| `@typeparam T` | Code-behind must be `public partial class Foo<T>` (`DncDataTable.razor`). |
| `@implements IDisposable` / `IAsyncDisposable` | Implement the members in the code-behind; leave the directive in the `.razor`. |
| `@inject Foo Bar` | May stay in the `.razor` (rule allows directives). Only move to `[Inject]` if the injected member is needed as a C# property anyway. |
| Nested classes/records declared inside `@code` | Move them with the block; keep them `private` unless the markup needs them public (markup is generated in the same partial class, so `private` is fine). |
| `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` | Build with `/p:EnforceCodeStyleInBuild=true`; unused `using`s / unused private members will surface immediately. |
| Generated class name must match | File name, class name and `.razor` component name must be identical. |
| `CascadingParameter`, `[Parameter]`, `[JSInvokable]` attributes | Move with their members — unchanged. |

### 4.4 Verification gap

There is **no bUnit / Blazor render-test infrastructure** in this repo. Moving code out of `@code` is a pure refactor (same partial class), so the compiler is the primary guard; visual regressions must be caught by loading the affected page. Treat every migration batch as requiring a manual smoke check of the pages it touched.

---

## 5. Phases

> **Global rule from now on (effective immediately):** no new `@code` block, `<style>` block, or inline `style` with
> more than one declaration may be added to a `.razor` file. A single declaration (`style="display:none"`) is fine,
> and any file edited for another reason must be migrated in the same change (boy-scout rule). CI enforces it.

### Phase 0 — Unblock + tooling (½ day) — ✅ complete 2026-10-24

- ✓ **Fixed Client CSS delivery** — `App.razor` now links `DotNetCloud.UI.Web.Client.styles.css` right after `DotNetCloud.Core.Server.styles.css`. Verified against the server's static-web-asset manifest (`DotNetCloud.UI.Web.Client.styles.css` + a fingerprinted variant are both served at the root).
  - Visual review still required on the five admin pages whose scoped CSS became live for the first time (`/admin/files-shared-folders`, `/admin/health`, `/admin/search`, `/admin/system`, `/admin/updates`).
- ✓ **Added `scripts/check-blazor-standards.sh`** — checks `@code`/`@functions` and `<style>` as errors (allowlist-aware), reports static inline styles as warnings, supports `--list`/`--help`, emits GitHub Actions annotations.
- ✓ **Added `scripts/blazor-standards-allowlist.txt`** — 130 baseline paths, each with a removal-on-migration contract.
- ✓ **Wired into `.github/workflows/build-test.yml`** — step *Check Blazor code & style standards* in the `build` job, before `Setup .NET`.
- ✓ Documented in `CONTRIBUTING.md` § *Coding Standards*.

**Verification:** script exits 0 on the baseline; dropping a throw-away `.razor` with an `@code` block makes it exit 1 with a per-file message (fixture removed afterwards).

### Phase 1 — Inline `<style>` → `.razor.css` (1 day) — ✅ complete 2026-10-24

All 8 files migrated (≈1 099 CSS lines). The 4 files that had no `@code` were removed from the allowlist; the other 4 keep their entry until their `@code` moves in Phase 2/3/4.

| File | CSS lines | Notes |
| --- | ---: | --- |
| `Modules/Tracks/…/UI/WebhookManagementPage.razor` | 364 | large block — check `.wh-*` classes are not used elsewhere |
| `Modules/Tracks/…/UI/CsvImportWizard.razor` | 286 | overlay + steps |
| `Modules/Tracks/…/UI/TracksCommandPalette.razor` | 174 | overlay; `position: fixed` |
| `Modules/Files/…/UI/DocumentEditor.razor` | 100 | overlay; confirm no `body` rules |
| `UI.Web/…/Shared/GlobalChatNotifications.razor` | 51 | toasts; `position: fixed` |
| `Modules/Tracks/…/UI/UndoToast.razor` | 48 | toast |
| `Modules/Tracks/…/UI/TracksShortcutsModal.razor` | 43 | modal |
| `UI.Web/…/Shared/UserDndToggle.razor` | 33 | toggle |

Per file:
1. Grep every selector defined in the block across `*.razor` and `*.css` to find external dependencies.
2. Create `Foo.razor.css` with the block contents.
3. Delete the `<style>` block from `Foo.razor`.
4. `dotnet build` (the relevant project), then load the page/dialog and compare against the pre-change screenshot.
5. Remove the file from the allowlist.

**Outcome / decisions taken:**

- `DocumentEditor`'s `.document-editor-overlay` rule was **not** component-local: the overlay `<div>` is rendered by `FileBrowser.razor`. The rule (and its mobile media query) moved to a new `FileBrowser.razor.css`; the container/header/iframe rules stayed in `DocumentEditor.razor.css`.
- `UserDndToggle`'s `.dnd-toggle .material-icon` needed `::deep` — `MaterialIcon` is a child component, so the descendant `<svg>` carries the child's scope id. Verified in the emitted bundle: `.dnd-toggle[b-n81mh3be35]  .material-icon { … }`.
- All `@keyframes` moves are safe: the repo already relies on scoped keyframes (`MessageList`, `CallControls`, `IncomingCallNotification`, …).
- No `body`/`html`/`:root` selectors were present in any of the 8 blocks.

**Exit criteria:** `rg -l '<style' --glob '*.razor'` returns nothing (excluding documented `MarkupString` content); allowlist down to 126 entries. ✅ both met.

### Phase 2 — Small components, `< 50` `@code` lines (67 files) — ✅ complete 2026-10-24

All 67 files migrated to `.razor.cs`; allowlist down to 59 entries. `dotnet build DotNetCloud.CI.slnf -c Release` →
0 warnings / 0 errors; tests green for every touched project (UI.Shared 200, Bookmarks 24, Calendar 210,
Contacts 154, Notes 157, Email 8, Example 51, Tracks 205, Files 991, Core.Server 851/2 skipped).

Useful precedents established by this phase:

- `@inherits` components (`DncInput`, `DncCheckbox`, `DncDatePicker`, `DncRadioGroup`, `DncSelect`, `DncTextArea`) —
the code-behind declares the class **without** a base type.
- `@inject` directives stayed in the `.razor`; the generated private property is visible to the other partial, so
  `LinkResolver`, `Navigation`, `JSRuntime`, `AuthStateProvider`, `ApiClient`, `ModuleRegistry` are used directly.
- Extension methods need their namespace in the code-behind even though the type is fine: `Microsoft.JSInterop` for
  `InvokeVoidAsync` (`CultureSelector`, `ExampleSidebar`, `LogoutButton`) and `Microsoft.AspNetCore.Components.Web`
  for `ErrorBoundary` (`ModulePageHost`).
- Nested types move with the block: `ExampleNotesPage.NoteViewModel` is now declared in
  `ExampleNotesPage.razor.cs` and is still addressed as `ExampleNotesPage.NoteViewModel` from `ExampleNoteDisplay`.

Original ordering guidance (kept for reference):

1. **`DotNetCloud.UI.Shared` — all 36 files** (DataDisplay 7, Dialogs 4 — including the 443-line `DncShareDialog`, do that one in Phase 3, Editors 1, Forms 7, Layout 4, Navigation 8, Notifications 5). These are shared by every page: migrate, build the UI projects, smoke check one page per component family.
2. **Module UI** — About (1), Bookmarks (2 of 4; the 7/6-line grid/list ones are the smallest in the repo), Calendar `ColorSwatchPicker` + `CrossModuleLinkList`, Contacts/Notes `CrossModuleLinkList`, Example (4), Tracks `TracksShortcutsModal`.
3. **`UI.Web` small** — `RedirectTo*`, `LogoutButton`, `ErrorDisplay`, `ModulePageHost`/`DemoBanner`, `NavMenu`, auth pages (`Logout`, `MfaVerify`, `ChangePassword`, `Login`, `ForgotPassword`), module host pages (`Chat`/`Notes`/`Email`/`Contacts`/`Tracks`/`Photos`/`Video`/…), `TracksCard`/`TracksItem`.
4. **`UI.Web.Client` small** — `LoadingIndicator`, `ToastContainer`, `ConfirmDialog`, `Health`, `ModuleDetail`, `UserList`, `Updates`, `SyncDevices`.

**Exit criteria:** 67 files migrated; `dotnet build` clean; allowlist down to 59 entries.

### Phase 3 — Medium components, `50 – 199` lines (40 files) — ✅ complete 2026-10-24

Grouped by project so each build/verify cycle covers a coherent slice:

- `UI.Shared`: `DncDataTable` (⚠ `@typeparam`), `WidgetCard`, `DncButton`, `MarkdownEditor` (250 lines — actually Phase 4), `DncModal`, `DncConfirmDialog`, `DncAlertDialog`, `DncTabs`, `DncAccordion`, `DncPaginator`, `DncInput`, `DncRadioGroup`, `DncCheckbox`, `DncDatePicker`, `DncTextArea`, `DncSelect`, `DncCard`, `DncSection`, `CultureSelector`, `DncSidebar`, `DncAlert`, `DncErrorDisplay`, `DncToast`, `DncGrid`, `DncPanel`, `DncTopBar`, `DncBadge`, `DncLoadingIndicator`, `DncMenu`.
- `UI.Web`: `HomeWidgetCustomizer`, `PublicShare`, `Home`, `ModuleHelp`, `MfaSetup`, `Register`, `ResetPassword`, `NotificationBell`, `GlobalSearchBar` (250 → Phase 4), `SearchResultCard`, `MainLayout` (⚠ `@inherits`), `App` (⚠ root component — `App.razor.cs` is fine, no `.razor.css` needed).
- `UI.Web.Client`: `MediaLibraries`, `Dashboard`, `PushNotificationSettings`, `ModuleList`, `FilesSettings`, `UserCreate`, `UserDetail`, `SystemPage`, `UserEdit`, `BackupSettings`, `SearchAdmin`, `Settings`, `CollaboraSettings`, `ChatSettings`, `Organizations`, `Broadcast`, `Groups`, `Profile`.
- Modules: `ExampleNoteForm`, `BookmarkEditForm`, `Email/EditRuleForm`, `Email/FilePickerModal`, `Contacts/ContactsPage` (429 → Phase 4), `Tracks/UndoToast`, `Tracks/WebhookManagementPage`, `About/AboutPage`, `AI` widget-adjacent components.

Migrate `App.razor` early in this phase — it proves the pattern on the root component and its `@code` holds the culture-cookie logic (needs `System.Globalization`, `Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Localization` usings in the code-behind).

**Exit criteria:** 40 files migrated; allowlist down to 19 entries.

### Phase 4 — Large components, `≥ 200` lines (19 files) — ✅ complete 2026-10-24

Phases 3 and 4 were executed together: 58 files migrated in one pass, plus `WorkItemListView` (which already
had a code-behind) merged by hand. Mechanical rules used by the pass:

- `@code` body copied verbatim (it is already indented for a class body) — no logic was rewritten.
- Namespace from the explicit `@namespace`, otherwise `RootNamespace` + relative folder.
- `@inherits` (only `MainLayout`) → no base type declared in the code-behind; `@implements IDisposable` /
  `IAsyncDisposable` (11 files) → added to the base list; `@typeparam TItem` (`DncDataTable`) →
  `partial class DncDataTable<TItem>`.
- Usings = the `.razor`'s `@using` lines + every `_Imports.razor` up to the project root + `Microsoft.AspNetCore.Components`.
  The compiler then flagged the two genuinely missing ones — `Microsoft.AspNetCore.Components.Web` for
  `KeyboardEventArgs` (`WorkItemListView`) — and a malformed generic `cref`.
- 
**Verification:** `dotnet build DotNetCloud.CI.slnf` → 0 warnings / 0 errors in both Debug and Release; the full unit
suite is green (22 projects, 7 400+ tests, incl. Chat 1 488, Files 991, Core.Server 851/2 skipped);
`check-blazor-standards.sh` reports **0** remaining `@code` blocks.

Split, because each one is a real refactor:

- **4a — 200–299 lines (9):** `Email/BookmarksPage.razor` (287), `Admin/Broadcast` (275), `Shared/Search/GlobalSearchBar` (250), `Editors/MarkdownEditor` (250), `Admin/ChatSettings` (243), `Admin/CollaboraSettings` (241), `Admin/Settings` (219), `Admin/SystemPage`/`UserEdit` boundary items, `Calendar` help components.
- **4b — 300–499 (6):** `Admin/Organizations` (312), `Profile` (349), `Admin/Groups` (393), `Dialogs/DncShareDialog` (443), `Email/EmailComposeForm` (455), `Contacts/ContactsPage` (429).
- **4c — ≥ 500 (5):** `AI/AiChatPage` (612), `Notes/NotesPage` (646), `Email/EmailPage` (805), `Shared/GlobalMusicPlaybar` (816), `Admin/FilesSharedFolders` (823), `Calendar/CalendarPage` (1 133).

For 4b/4c:
1. Move the `@code` block verbatim first (mechanical, reviewable diff), **do not** restructure logic in the same commit.
2. Build + smoke check the page (login, main interaction, one edit round-trip).
3. Optional follow-up commit: extract pure helpers into testable `internal static` classes (the repo already does this for `FilesDeleteProgress`, `ChannelPresenceMapping`) and add unit tests — this is how the migrated logic earns test coverage.

**Exit criteria:** `rg -l '^\s*@code\s*\{' --glob '*.razor'` returns nothing; `.razor.cs` count ≥ 221; `scripts/blazor-standards-allowlist.txt` deleted.

### Phase 5 — Inline `style=` burn-down — ✅ complete 2026-10-24

Rule refined during implementation: a **single declaration** in `style` stays inline (no point crowding `.razor.css`
with one-liners); only **2+ declarations** must become a class.

- 113 multi-declaration attributes across 20 files converted: `VideoPage` 25, `MusicPage` 19, `PhotosPage` 18,
  `MediaLibraries` 15, `Admin/Organizations` 8, `Admin/Groups` 7, `CalendarPage` 5, plus singles across Profile,
  FilePreview, PublicShare, Register, Admin/Dashboard, Admin/Settings, DncButton, WorkItemTrashView, TeamManagement,
  ProductSettingsPage, FetchArtModal and the AI widget.
- 14 new `.razor.css` sidecars were created where the component had none.
- The repeated "library setup hint" / "source editor" patterns are now `.library-setup-hint`, `.library-hint-*`,
  `.import-notice`, `.library-source-*` in the Photos, Video and Music stylesheets (same names, per-module scopes).
- The gate now **fails** on any multi-declaration attribute; 178 single-declaration / dynamic attributes remain and are
  explicitly allowed.

- ✓ Flip the `style="…"` check in `check-blazor-standards.sh` from **warning** to **error** (multi-declaration only).
- ✓ Delete the allowlist mechanism from the script (file deleted so it cannot be re-populated silently).
- ✓ Update `CONTRIBUTING.md`: single declaration allowed, 2+ declarations forbidden.
- ✓ Final sweep: `rg -l '<style|@code' --glob '*.razor'` → nothing.

---

## 6. CI gate design (`scripts/check-blazor-standards.sh`)

```bash
#!/usr/bin/env bash
# Fails when a .razor file contains code or styles that belong in .razor.cs / .razor.css.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ALLOWLIST="$ROOT/scripts/blazor-standards-allowlist.txt"
cd "$ROOT"
fail=0

allowlisted() { [ -f "$ALLOWLIST" ] && grep -qxF "$1" "$ALLOWLIST"; }

# 1. error: inline @code / @functions
while IFS= read -r f; do
  allowlisted "$f" || { echo "✗ @code block in $f"; fail=1; }
done < <(rg -l '^\s*@(code|functions)\s*\{' --glob '*.razor' | sort)

# 2. error: inline <style>
while IFS= read -r f; do
  allowlisted "$f" || { echo "✗ <style> block in $f"; fail=1; }
done < <(rg -l '<style' --glob '*.razor' | sort)

# 3. error: static inline style with 2+ declarations (single declarations are allowed)
awk '…' | while read -r hit; do echo "✗ multi-declaration inline style: $hit"; fail=1; done

exit $fail
```

- **Workflow step:** `.github/workflows/build-test.yml`, job `build`, first step after checkout/setup — `./scripts/check-blazor-standards.sh`.
- **Why an allowlist instead of "changed files only":** it is exact, reviewable in the diff, and the diff to the allowlist *is* the progress report. Every migration PR removes its file(s).
- `.razor.css` and `.razor.cs` need no checks — the compiler already rejects a duplicate `partial` declaration.

---

## 7. Per-file verification protocol

For every migrated file:

1. `dotnet build src/<project> --configuration Debug /p:EnforceCodeStyleInBuild=true` — 0 warnings.
2. `dotnet test` for the owning test project (module UI helpers are unit-testable; Core tests for shared components).
3. Delete the path from `scripts/blazor-standards-allowlist.txt` and run `scripts/check-blazor-standards.sh`.
4. Manual smoke check of the rendered component (see §4.4). Record the page + what was checked in the commit message.
5. `git diff` review — the `.razor` should lose exactly one block; the `.razor.cs`/`.razor.css` is additive. Watch for formatter churn on untouched lines.

---

## 8. Definition of done

- ✓ `rg -l '^\s*@code\s*\{' --glob '*.razor'` → empty
- ✓ `rg -l '<style' --glob '*.razor'` → empty (no `MarkupString` content cases exist)
- ✓ No multi-declaration static `style="…"` attributes remain (single declarations are allowed by design)
- ✓ `scripts/blazor-standards-allowlist.txt` deleted; the CI step is unconditional
- ✓ `.razor.cs` for every component that has logic; `.razor.css` for every component that has component-scoped styles, with Client-page CSS delivery fixed
- ✓ `dotnet build -c Release` / `dotnet test` green (22 test projects, 0 warnings / 0 errors)
- ☐ Every touched page smoke-checked in a browser (needs a running instance; not possible in this environment)
- ✓ Standard documented in `.github/copilot-instructions.md`, `CLAUDE.md`, `CONTRIBUTING.md`, progress tracked in this file, `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md`

---

## 9. Progress tracking

| Phase | Scope | Status | Allowlist entries left |
| --- | --- | --- | ---: |
| 0 | Client CSS delivery fix + lint script + CI gate | ✓ complete 2026-10-24 | 130 |
| 1 | 8 inline `<style>` blocks | ✓ complete 2026-10-24 | 126 |
| 2 | 67 small `@code` components | ✓ complete 2026-10-24 | 59 |
| 3 | 40 medium `@code` components | ✓ complete 2026-10-24 | 19 |
| 4 | 19 large `@code` components | ✓ complete 2026-10-24 | **0 — file deleted** |
| 5 | 113 multi-declaration inline styles | ✓ complete 2026-10-24 | — |

> Allowlist starts at **130 unique paths** = 126 files with `@code` + 8 with `<style>` − 4 files that have both.
> Phase 1 removes the 4 files that become fully compliant (`DocumentEditor`, `CsvImportWizard`, `TracksCommandPalette`,
> `GlobalChatNotifications`); the other 4 keep their entry until their `@code` is migrated.

---

## 10. Known issues found during the migration — both FIXED 2026-10-24

### 10.1 `:global(...)` in `.razor.css` was dead CSS — 100 occurrences / 21 files — ✅ fixed

`:global(...)` is not understood by Blazor CSS isolation; it was emitted verbatim and browsers dropped the rule.

**Root cause of the fix:** Blazor's rewriter scopes **only the last compound** of a selector (verified with a
throw-away component: `.a .b { }` compiles to `.a .b[b-scope]`, not `.a[b-scope] .b[b-scope]`). Therefore an
ancestor-driven theme selector only needs the wrapper removed:

```css
:global(.dark-mode) .csv-wizard-dialog { }   /* before — invalid, dropped by the browser */
.dark-mode .csv-wizard-dialog { }            /* after  — compiles to .dark-mode .csv-wizard-dialog[b-scope] */
```

The repository's only theme signal is `.dark-mode` (set on `<html>` **and** `<body>` by `theme.js`); neither
`.dark` nor `[data-theme="dark"]` is ever set, so both collapsed onto `.dark-mode`. All 21 files were converted
and the result verified in the emitted bundles (e.g.
`.dark-mode .command-palette-dialog[b-lcy9jshtxe]`).

### 10.2 Descendant selectors into child components needed `::deep` — ✅ fixed

A rule like `.ds-platform .material-icon` compiled to `.ds-platform .material-icon[b-child-scope]` and never
matched, because the `<svg>` carries `MaterialIcon`'s own scope. Four `.material-icon` rules were converted to
`::deep` (three in `DesktopSync.razor.css`, one in `PhotosPage.razor.css`) and verified in the bundle as
`.ds-platform[b-sc9x0f9k7w]  .material-icon`. Any future rule that reaches into another component must use
`::deep`.

---

## Appendix A — Inline `<style>` inventory (all migrated)

| File | CSS lines | Destination |
| --- | ---: | --- |
| `Modules/Tracks/…/UI/WebhookManagementPage.razor` | 364 | `WebhookManagementPage.razor.css` |
| `Modules/Tracks/…/UI/CsvImportWizard.razor` | 286 | `CsvImportWizard.razor.css` |
| `Modules/Tracks/…/UI/TracksCommandPalette.razor` | 174 | `TracksCommandPalette.razor.css` |
| `Modules/Files/…/UI/DocumentEditor.razor` | 100 | `DocumentEditor.razor.css` + overlay rules → `FileBrowser.razor.css` |
| `UI.Web/…/Shared/GlobalChatNotifications.razor` | 51 | `GlobalChatNotifications.razor.css` |
| `Modules/Tracks/…/UI/UndoToast.razor` | 48 | `UndoToast.razor.css` |
| `Modules/Tracks/…/UI/TracksShortcutsModal.razor` | 43 | `TracksShortcutsModal.razor.css` |
| `UI.Web/…/Shared/UserDndToggle.razor` | 33 | `UserDndToggle.razor.css` (`::deep` for the icon) |

## Appendix B — Inline `@code` inventory (126 files)

`✓` = sidecar already exists, `☐` = missing.

**`src/Modules/About/DotNetCloud.Modules.About/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `AboutPage.razor` | 100 | ☐ | ✓ |

**`src/Modules/AI/DotNetCloud.Modules.AI/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `AiChatPage.razor` | 612 | ☐ | ✓ |

**`src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `BookmarksPage.razor` | 287 | ☐ | ✓ |
| `BookmarkEditForm.razor` | 69 | ☐ | ✓ |
| `BookmarkGrid.razor` | 7 | ☐ | ☐ |
| `BookmarkList.razor` | 6 | ☐ | ☐ |

**`src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `CalendarPage.razor` | 1133 | ☐ | ✓ |
| `ColorSwatchPicker.razor` | 21 | ☐ | ✓ |
| `CrossModuleLinkList.razor` | 18 | ☐ | ☐ |

**`src/Modules/Contacts/DotNetCloud.Modules.Contacts/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `ContactsPage.razor` | 429 | ☐ | ✓ |
| `CrossModuleLinkList.razor` | 18 | ☐ | ☐ |

**`src/Modules/Email/DotNetCloud.Modules.Email/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `EmailPage.razor` | 805 | ☐ | ✓ |
| `EmailComposeForm.razor` | 455 | ☐ | ☐ |
| `FilePickerModal.razor` | 145 | ☐ | ☐ |
| `EditRuleForm.razor` | 42 | ☐ | ☐ |

**`src/Modules/Example/DotNetCloud.Modules.Example/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `ExampleNoteForm.razor` | 50 | ☐ | ☐ |
| `ExampleSidebar.razor` | 37 | ☐ | ✓ |
| `ExampleNotesPage.razor` | 29 | ☐ | ✓ |
| `ExampleNoteDisplay.razor` | 12 | ☐ | ☐ |

**`src/Modules/Notes/DotNetCloud.Modules.Notes/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `NotesPage.razor` | 646 | ☐ | ✓ |
| `CrossModuleLinkList.razor` | 18 | ☐ | ☐ |

**`src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `WebhookManagementPage.razor` | 165 | ☐ | ☐ |
| `UndoToast.razor` | 86 | ☐ | ☐ |
| `WorkItemListView.razor` | 63 | ✓ | ✓ |
| `TracksShortcutsModal.razor` | 3 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Shared/Components/DataDisplay`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `DncDataTable.razor` | 70 | ☐ | ☐ |
| `WidgetCard.razor` | 65 | ☐ | ☐ |
| `DncTabs.razor` | 31 | ☐ | ☐ |
| `MaterialIcon.razor` | 28 | ☐ | ✓ |
| `DncAccordion.razor` | 27 | ☐ | ☐ |
| `DncPaginator.razor` | 23 | ☐ | ☐ |
| `DncBreadcrumb.razor` | 5 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Shared/Components/Dialogs`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `DncShareDialog.razor` | 443 | ☐ | ☐ |
| `DncModal.razor` | 74 | ☐ | ☐ |
| `DncConfirmDialog.razor` | 51 | ☐ | ☐ |
| `DncAlertDialog.razor` | 40 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Shared/Components/Editors`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `MarkdownEditor.razor` | 250 | ☐ | ✓ |

**`src/UI/DotNetCloud.UI.Shared/Components/Forms`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `DncInput.razor` | 30 | ☐ | ☐ |
| `DncRadioGroup.razor` | 30 | ☐ | ☐ |
| `DncCheckbox.razor` | 25 | ☐ | ☐ |
| `DncDatePicker.razor` | 24 | ☐ | ☐ |
| `DncTextArea.razor` | 23 | ☐ | ☐ |
| `DncSelect.razor` | 19 | ☐ | ☐ |
| `DncFormValidation.razor` | 5 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Shared/Components/Layout`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `DncCard.razor` | 21 | ☐ | ☐ |
| `DncSection.razor` | 17 | ☐ | ☐ |
| `DncGrid.razor` | 13 | ☐ | ☐ |
| `DncPanel.razor` | 13 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Shared/Components/Navigation`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `DncButton.razor` | 57 | ☐ | ☐ |
| `CultureSelector.razor` | 31 | ☐ | ☐ |
| `DncSidebar.razor` | 31 | ☐ | ☐ |
| `CrossModuleLinkList.razor` | 19 | ☐ | ✓ |
| `CrossModuleLink.razor` | 16 | ☐ | ✓ |
| `ModuleHelpLink.razor` | 14 | ☐ | ✓ |
| `DncTopBar.razor` | 13 | ☐ | ☐ |
| `DncMenu.razor` | 9 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Shared/Components/Notifications`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `DncAlert.razor` | 27 | ☐ | ☐ |
| `DncErrorDisplay.razor` | 21 | ☐ | ☐ |
| `DncToast.razor` | 17 | ☐ | ☐ |
| `DncBadge.razor` | 13 | ☐ | ☐ |
| `DncLoadingIndicator.razor` | 9 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web.Client/Pages`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `Profile.razor` | 349 | ☐ | ☐ |
| `MediaLibraries.razor` | 86 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web.Client/Pages/Admin`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `FilesSharedFolders.razor` | 823 | ☐ | ✓ (inert) |
| `Groups.razor` | 393 | ☐ | ☐ |
| `Organizations.razor` | 312 | ☐ | ☐ |
| `Broadcast.razor` | 275 | ☐ | ☐ |
| `ChatSettings.razor` | 243 | ☐ | ☐ |
| `CollaboraSettings.razor` | 241 | ☐ | ☐ |
| `Settings.razor` | 219 | ☐ | ☐ |
| `BackupSettings.razor` | 186 | ☐ | ☐ |
| `SearchAdmin.razor` | 181 | ☐ | ✓ (inert) |
| `FilesSettings.razor` | 169 | ☐ | ☐ |
| `UserCreate.razor` | 150 | ☐ | ☐ |
| `UserDetail.razor` | 148 | ☐ | ☐ |
| `SystemPage.razor` | 146 | ☐ | ✓ (inert) |
| `UserEdit.razor` | 146 | ☐ | ☐ |
| `Dashboard.razor` | 114 | ☐ | ☐ |
| `PushNotificationSettings.razor` | 111 | ☐ | ☐ |
| `ModuleList.razor` | 95 | ☐ | ☐ |
| `SyncDevices.razor` | 84 | ☐ | ☐ |
| `Updates.razor` | 80 | ☐ | ✓ (inert) |
| `UserList.razor` | 78 | ☐ | ☐ |
| `ModuleDetail.razor` | 74 | ☐ | ☐ |
| `Health.razor` | 58 | ☐ | ✓ (inert) |

**`src/UI/DotNetCloud.UI.Web.Client/Shared`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `ConfirmDialog.razor` | 46 | ☐ | ☐ |
| `ToastContainer.razor` | 13 | ☐ | ☐ |
| `LoadingIndicator.razor` | 7 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web/Components`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `App.razor` | 21 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web/Components/Home`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `HomeWidgetCustomizer.razor` | 110 | ☐ | ✓ |

**`src/UI/DotNetCloud.UI.Web/Components/Layout`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `MainLayout.razor` | 105 | ☐ | ☐ |
| `NavMenu.razor` | 14 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web/Components/Pages`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `SearchResults.razor` | 188 | ☐ | ✓ |
| `PublicShare.razor` | 107 | ☐ | ☐ |
| `Home.razor` | 56 | ☐ | ☐ |
| `ModuleHelp.razor` | 37 | ☐ | ✓ |

**`src/UI/DotNetCloud.UI.Web/Components/Pages/Auth`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `MfaSetup.razor` | 186 | ☐ | ☐ |
| `Register.razor` | 127 | ☐ | ☐ |
| `ResetPassword.razor` | 69 | ☐ | ☐ |
| `ForgotPassword.razor` | 50 | ☐ | ☐ |
| `Login.razor` | 25 | ☐ | ☐ |
| `ChangePassword.razor` | 19 | ☐ | ☐ |
| `MfaVerify.razor` | 16 | ☐ | ☐ |
| `Logout.razor` | 10 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web/Components/Pages/Modules`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `Music.razor` | 63 | ☐ | ☐ |
| `Files.razor` | 37 | ☐ | ☐ |
| `Video.razor` | 29 | ☐ | ☐ |
| `Photos.razor` | 25 | ☐ | ☐ |
| `TracksItem.razor` | 17 | ☐ | ☐ |
| `TracksCard.razor` | 15 | ☐ | ✓ |
| `Chat.razor` | 13 | ☐ | ☐ |
| `Contacts.razor` | 13 | ☐ | ☐ |
| `Email.razor` | 13 | ☐ | ☐ |
| `Notes.razor` | 13 | ☐ | ☐ |
| `Tracks.razor` | 13 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web/Components/Shared`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `GlobalMusicPlaybar.razor` | 816 | ☐ | ☐ |
| `NotificationBell.razor` | 135 | ☐ | ✓ |
| `DemoBanner.razor` | 38 | ☐ | ☐ |
| `ModulePageHost.razor` | 38 | ☐ | ☐ |
| `UserDndToggle.razor` | 31 | ☐ | ☐ |
| `RedirectToLogin.razor` | 18 | ☐ | ☐ |
| `LogoutButton.razor` | 14 | ☐ | ☐ |
| `ErrorDisplay.razor` | 13 | ☐ | ☐ |
| `RedirectToDashboard.razor` | 6 | ☐ | ☐ |

**`src/UI/DotNetCloud.UI.Web/Components/Shared/Search`**

| File | @code lines | .razor.cs | .razor.css |
| --- | ---: | :---: | :---: |
| `GlobalSearchBar.razor` | 250 | ☐ | ✓ |
| `SearchResultCard.razor` | 73 | ☐ | ✓ |
