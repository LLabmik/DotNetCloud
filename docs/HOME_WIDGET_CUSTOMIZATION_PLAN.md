# Home Widget Customization — Implementation Plan

**Status:** Ready for implementation
**Supersedes:** the deferred item in `docs/MODULE_WIDGETS_PLAN.md` §2.7
**Audience:** written to be detailed enough for an LLM agent with no prior context to implement end-to-end.

---

## 1. Summary

Three user-facing capabilities on the Home page (`/`):

1. **Choose a widget style** — pick one of three visual treatments for the whole widget grid.
2. **Show / hide widgets** — per-user visibility toggle for each registered widget.
3. **Reorder widgets** — per-user drag-and-drop (plus keyboard) ordering, replacing the fixed module sort order.

All three persist **per user, server-side**, so the layout follows the user across browsers and devices.

The deferred item this implements is `docs/MODULE_WIDGETS_PLAN.md` decision #7:

> 7. Per-user show/hide + drag-and-drop reorder is **DEFERRED to a follow-up**. Not in this change.

---

## 2. The three styles (naming)

| #   | Name                  | Source                                                                | Look                                                                                                                                                                                                                                   |
| --- | --------------------- | --------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Strictly Business** | Original pre-customization styling (`app.css` §"Module Home Widgets") | Plain surface cards, 8px radius, soft shadow, flat accent header border. Opt-in alternative.                                                                                                                                           |
| 2   | **Art Department**    | Option A (currently in `app.css`)                                     | Gradient skeuomorphism. Each module imitates a real object: sticky note, polaroid, filmstrip, wall calendar, cassette.                                                                                                                 |
| 3   | **Hard Copy**         | Option B (currently `artifacts/preview/option-b-section.css`)         | Neo-brutalist "hard art". Flat saturated fills, 3px ink borders, hard offset shadows, zero radius, ALL-CAPS 900 type, one geometric device per module (folder tab, bubble tail, rolodex spine, IC chip, clapperboard, airmail border). |

**Default: Art Department.** A user who has never chosen a style sees Art Department. Changing the default is a single constant (`HomeWidgetStyles.DefaultToken`).

**Names are deliberate and cheap to change** — they live in one `const` + display-label map. Alternates if preferred: _Curated_, _Show & Tell_ (for Art Department); _Print Shop_, _Brutalist Press_ (for Hard Copy). The three names form a workplace-idiom set with "Strictly Business", escalating in playfulness.

---

## 3. Locked decisions

1. **Persistence is server-side `UserSettings`**, not `localStorage`. Reuses the existing `IUserSettingsService` (`GetSettingAsync` / `UpsertSettingAsync`) — same store Music/Photos/Video already use for media-library sources. Follows the user across devices.
2. **A single JSON blob** holds style + order + visibility, so one read and one write per change. No schema/migration needed — `UserSettings` is a key-value table.
3. **`WidgetUiRegistry` is NOT modified.** It stays the catalog of _what exists_, ordered by module `SortOrder`. Per-user layout is a separate overlay applied on top. Existing `WidgetUiRegistryTests` keep passing untouched.
4. **`WidgetCard.razor` is NOT modified.** Reordering/visibility UI lives in a separate customizer component so the shared card stays a pure presentation primitive.
5. **The merge/resolve algorithm is a pure static function in `DotNetCloud.Core`**, so it is unit-testable without Blazor and reusable by the future desktop/mobile clients. `DotNetCloud.Core` must not reference `DotNetCloud.UI.*` — the resolver takes a minimal catalog tuple, not `WidgetDescriptor`.
6. **The reorder surface is the customizer's list** (drag handle + Move up/Move down buttons + visibility checkbox), not the cards in the grid. Rationale: one surface handles visible _and_ hidden widgets uniformly, and HTML5 drag-and-drop alone is not keyboard accessible — the up/down buttons provide the accessible path.
7. **Styles are applied via a `data-widget-style` attribute**, and all variant CSS is prefixed with that attribute selector. This gives variants strictly higher specificity than the base shell, so they override cleanly and never collide with each other.
8. **The deleted `WidgetCard.razor.css` stays deleted.** The base card shell must live in _global_ CSS: Blazor scoped CSS compiles to `.widget-card[b-xxxxx]` (specificity 0,2,0) and loads after `app.css`, so a scoped base would permanently outrank the variant blocks. This is the reason the scoped file was removed.
9. **No new JavaScript.** The `data-widget-style` attribute is emitted by the server render (Home is prerendered, so it is present in the first paint). A `localStorage` primer script is only worth adding if a flash is actually observed — see §8 (Risk R1).
10. **No module-boundary impact.** This feature reorders/hides _cards_; each widget still loads its own data exactly as today. No gRPC, proto, or module project changes.
11. **Default style is Art Department.** Users who have never saved a preference get Art Department, not Strictly Business. Only the _default_ changes: Strictly Business and Hard Copy stay fully available, and Strictly Business remains the minimal CSS base that the variant blocks override on top of (§5.1).

---

## 4. Data model & persistence

### 4.1 Persisted shape

```
module: "home-widgets"
key:    "preferences"
value:  JSON
```

```json
{
  "version": 1,
  "style": "art-department",
  "items": [
    { "moduleId": "dotnetcloud.notes", "visible": true },
    { "moduleId": "dotnetcloud.files", "visible": true },
    { "moduleId": "dotnetcloud.chat", "visible": false }
  ]
}
```

- `items` order **is** the display order.
- `style` is stored as the **CSS token**, not the C# enum name, so CSS identifiers can be renamed without a data migration. Unknown values fall back to the default token.
- `version` allows future shape changes; an unrecognised/newer version falls back to defaults rather than throwing.

### 4.2 Resolve algorithm (pure, deterministic)

```
Resolve(catalog, prefs) -> full ordered layout, each entry flagged Visible
  1. index persisted items by moduleId, keeping their array position
  2. flag entries whose persisted entry has visible == false
  3. order by: persisted position, then catalog SortOrder (asc)
  4. entries absent from prefs (newly installed modules) sort last,
     in catalog SortOrder

BuildPreferences(styleToken, layout, existing) -> payload to persist
  - writes layout order + visibility, normalizes an unknown style token
  - appends slots from `existing` whose module is not in the layout,
    so an uninstalled module's position survives a reinstall
```

`Resolve` deliberately returns hidden widgets too, flagged — the Home page renders only `Visible` ones, while the customizer needs the hidden ones to offer them for re-enabling, in their resolved positions.

Consequences, all intentional:

- A newly installed module's widget **appends** to the end of the user's layout rather than interleaving. Predictable, and matches how dashboards usually behave.
- A widget hidden, then its module disabled and re-enabled, **returns to its slot** (prefs are keyed by `moduleId`, which is stable).
- Empty/missing prefs ⇒ registry default order, everything visible.
- Unknown `moduleId`s in prefs are ignored (module uninstalled). They are **retained on write** so uninstall/reinstall does not lose the slot.

### 4.3 Reset

`IUserSettingsService` exposes only `GetSettingAsync` / `UpsertSettingAsync` — there is **no delete**. "Reset to default" therefore writes a canonical default payload (`version: 1`, the default style `art-department`, empty `items`) rather than removing the row. Empty `items` resolves to registry default order, so behaviour matches a first-time user.

---

## 5. Style delivery (CSS)

### 5.1 File split

| File                               | Contents                                                                                                                                                 | Linked                                             |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| `wwwroot/css/app.css`              | **Strictly Business** base: `.widgets-section`, `.widget-grid`, base `.widget-card` shell (restored), `.widget-list*`, `.widget-quota*`, `.widget-stat*` | already linked, bump `?v=`                         |
| `wwwroot/css/widgets.variants.css` | **Art Department** + **Hard Copy** blocks, every selector prefixed `[data-widget-style="..."]`                                                           | **new** `<link>` in `App.razor` with its own `?v=` |

Moving the Art Department block _out_ of `app.css` has three benefits: `app.css` returns to roughly its original size, the variant file is separately cacheable, and the attribute prefix removes the ordering/specificity guesswork entirely.

### 5.2 Specificity

| Selector                                                                 | Specificity |
| ------------------------------------------------------------------------ | ----------- |
| `.widget-card` (base)                                                    | 0,1,0       |
| `[data-widget-style="hard-copy"] .widget-card`                           | 0,2,0       |
| `[data-widget-style="hard-copy"] .widget-card--tracks .widget-list-item` | 0,3,0       |

Variants always win over base, and two variants can never both match (a single element has one value for the attribute). **No `!important` anywhere except the existing `prefers-reduced-motion` guards.**

### 5.3 Where the attribute goes

`<section class="widgets-section" data-widget-style="@_styleToken">` in `Home.razor`.

- Eager-loaded from `UserSettings` in `OnInitializedAsync`, so it is present in the prerendered HTML → no flash, no JS.
- Changing style in the UI just updates `_styleToken` and re-renders: the CSS is already loaded, so the switch is **instantaneous** with no round-trip.
- No-JS degradation: the attribute is still server-rendered, so styling is correct; only the _changing_ of style needs the circuit.

### 5.4 Strictly Business base must be restored

The original `.widget-card` shell lived in `WidgetCard.razor.css`, which was deleted (§3.8). For Strictly Business to render, restore those rules into `app.css` as global base CSS:

- `.widget-card`, `.widget-card:hover`, `.widget-card-header`, `.widget-card-icon`, `.widget-card-title`, `.widget-card-open`, `.widget-card-body`

Recover the exact original from git: `git show HEAD:src/UI/DotNetCloud.UI.Shared/Components/DataDisplay/WidgetCard.razor.css`.

**Deliberate difference from the original:** the header icon stays a `MaterialIcon` (inline SVG) rather than the original raw emoji / text-ligature span, because raw emoji in Blazor UI is forbidden by the Material Icons mandate. Visually this is a small, intended improvement, not a regression.

### 5.5 Three selector traps in the variants file

1. **At-rules cannot be attribute-scoped.** `@property --widget-spin` and every `@keyframes` must stay **global and unprefixed** — CSS offers no way to conditionalise them on an attribute. They are already namespaced per style, so they never collide: `widget-eq` / `widget-slide` / `widget-scan` / `widget-spin` / `widget-pulse` (Art Department) and `nb-blink` / `nb-rec` (Hard Copy). Put them at the top of the file; do **not** try to nest them inside a `[data-widget-style]` rule.
2. **The element that carries the attribute needs a compound selector.** The attribute sits on `<section class="widgets-section">`. Rules for the section itself therefore need the no-space form — `[data-widget-style="art-department"].widgets-section::before` — while everything inside it uses the descendant form — `[data-widget-style="art-department"] .widget-card--notes::after`. Using the descendant form for the section's own pseudo-element means the flourish silently never applies, which is easy to miss because nothing errors.
3. **`.dark-mode` must stay OUTERMOST — this one actually broke dark mode.** `theme.js` puts `.dark-mode` on `<html>` and `<body>`, which is an **ancestor** of `.widgets-section`. Naively prefixing turns `.dark-mode .widget-card--notes` into `[data-widget-style="art-department"] .dark-mode .widget-card--notes`, which demands a `.dark-mode` element _inside_ the widget subtree. There is none, so the rule never matches and **all 33 dark-mode rules across both variants silently die** — the widgets simply render their light theme at night. The correct form keeps the ancestor first:

   ```css
   /* WRONG - never matches, no error, dark mode silently broken */
   [data-widget-style="art-department"] .dark-mode .widget-card--notes {
   }

   /* RIGHT - .dark-mode is an ancestor of the section */
   .dark-mode [data-widget-style="art-department"] .widget-card--notes {
   }
   ```

   This is the single most dangerous rule in the file: it produces valid CSS, no console warning, and a failure that only shows up as _light cards in dark mode_. Any future prefixing change should re-check that `.dark-mode` never appears after a `[data-widget-style]` in the same selector.

**Graceful degradation:** because Art Department ships in the second stylesheet, a browser that fails to fetch it falls back to Strictly Business rather than to an unstyled card. That is the intended failure mode, and it is why the base shell must remain fully renderable on its own.

---

## 6. UI design

### 6.1 Toolbar (widgets section header)

Existing header is `Your Widgets` + refresh button. Add a **Customize** button (`tune` icon, `aria-label="Customize widgets"`, `aria-expanded`).

### 6.2 Customizer panel

An inline expandable `<section>` (not a modal — avoids focus-trap work and keeps drag-and-drop simple):

```
┌ Customize widgets ─────────────────────────────────────────┐
│ Style                                                      │
│  ( ) Strictly Business   — Clean, minimal, no surprises.   │
│  (•) Art Department      — Every widget gets its own art.  │
│  ( ) Hard Copy           — Flat ink, hard edges, loud.     │
│                                                            │
│ Widgets                              [Reset to default]    │
│  ⠿ ☑ Files            ▲ ▼                                  │
│  ⠿ ☑ Chat             ▲ ▼                                  │
│  ⠿ ☐ Contacts         ▲ ▼      (hidden)                    │
│  ⠿ ☑ Calendar         ▲ ▼                                  │
│  …                                                         │
└────────────────────────────────────────────────────────────┘
```

- **Style**: radio group, three labelled options. Selecting one applies live and persists immediately.
- **Widgets**: every entry in the registry, hidden ones included, each with drag handle, visibility checkbox, and Move up / Move down buttons.
- **Reset to default**: restores default style + registry order + all visible, after a confirm.

### 6.3 Persistence timing

Save on every discrete change (style pick, checkbox, drop, move) via a fire-and-forget call that surfaces failures with `DncToastService.ShowError`. Optimistic local state, so the UI never blocks on the round-trip. A failed save leaves local state applied and shows an error — the next load reverts to the last successfully persisted layout.

### 6.4 Accessibility

- Drag handles are decorative; **Move up / Move down buttons are the accessible reorder path**.
- Checkboxes are real `<input type="checkbox">` with labels.
- The style group is a real `<fieldset>` + radio group.
- Live changes announce via `aria-live="polite"` status text (`"Notes hidden"`, `"Notes moved to position 2"`).
- The drag handle carries `aria-hidden="true"` and the row keeps its text label.

---

## 7. Files

### New

- ✓ `src/Core/DotNetCloud.Core/DTOs/Home/HomeWidgetPreferences.cs` — persisted model (`Version`, `Style`, `Items`), `HomeWidgetPreferenceItem(ModuleId, Visible)`, `HomeWidgetStyles` (tokens, labels, descriptions, default, `IsValid`, `Normalize`) and `HomeWidgetPreferencesSettings` (static `SettingsModule`/`PreferencesKey` consts + `LoadAsync` / `SaveAsync` / `Serialize` / `Deserialize`), modelled on `src/Core/DotNetCloud.Core/DTOs/Media/MediaLibrarySource.cs`
- ✓ `src/Core/DotNetCloud.Core/Services/Home/HomeWidgetLayoutResolver.cs` — pure static `Resolve` + `BuildPreferences` (§4.2)
- ☐ `src/UI/DotNetCloud.UI.Web/Services/HomeWidgetLayoutService.cs` — scoped; loads once per circuit, exposes ordered `WidgetDescriptor`s with visibility, saves style/order/visibility, reset
- ☐ `src/UI/DotNetCloud.UI.Web/Components/Home/HomeWidgetCustomizer.razor` (+ `.razor.css`) — toolbar button + panel
- ✓ `src/UI/DotNetCloud.UI.Web/wwwroot/css/widgets.variants.css` — Art Department + Hard Copy, every selector attribute-prefixed, at-rules kept global (§5.5)
- ✓ `tests/DotNetCloud.Core.Tests/Home/HomeWidgetPreferencesTests.cs`
- ✓ `tests/DotNetCloud.Core.Tests/Home/HomeWidgetLayoutResolverTests.cs`

### Modified

- ☐ `src/UI/DotNetCloud.UI.Web/Components/Pages/Home.razor` — resolve layout, render customizer, stamp `data-widget-style`
- ✓ `src/UI/DotNetCloud.UI.Web/wwwroot/css/app.css` — removed the Art Department block (moved to `widgets.variants.css`); restored the Strictly Business base card shell from git, plus a `.widget-card-icon .material-icon` size rule
- ✓ `src/UI/DotNetCloud.UI.Web/Components/App.razor` — linked `widgets.variants.css?v=20260912-01`, bumped `app.css?v=20260912-02`
- ☐ `docs/MODULE_WIDGETS_PLAN.md` — mark §2.7 implemented, link this plan
- ☐ `docs/IMPLEMENTATION_CHECKLIST.md` — task checkboxes
- ☐ `docs/MASTER_PROJECT_PLAN.md` — Quick Status Summary + step Status/Deliverables/Notes

---

## 8. Risks & traps

- **R1 — Flash of wrong style.** Only possible if Home stops being prerendered or the layout load is deferred to `OnAfterRenderAsync`. Mitigation if observed: add `wwwroot/js/widget-style.js` in `<head>` (synchronous, external — _not_ inline, because the CSP middleware would block inline script) that reads the persisted token from `localStorage` and sets the attribute before first paint. Mirror the existing `theme.js` pattern. **Do not add this pre-emptively.**
- **R2 — Scoped-CSS specificity.** Re-restoring `WidgetCard.razor.css` would silently kill both variant blocks. Any future per-module override must go in global CSS. (This trap cost a debugging cycle previously.)
- **R3 — `app.css` cache-buster.** `App.razor` hard-codes `?v=YYYYMMDD-NN`; forgetting to bump it ships stale CSS to browsers that already cached the file.
- **R4 — `TreatWarningsAsErrors` + CS1591.** Every new public type/member needs an XML doc comment or the build fails.
- **R5 — Formatter.** `.prettierignore` exists in the repo root and a formatter has previously reformatted `app.css` (799 → 919 lines). Re-read files before editing, and run the repo's formatter over touched CSS so the diff stays clean.
- **R6 — Prerender double-invocation.** `OnInitializedAsync` runs for both the prerender and the interactive pass. The layout load must be idempotent and cheap; cache in the scoped service, and tolerate a second load.
- **R7 — Concurrency.** Two rapid changes (drag then hide) can race. Serialise saves inside the service behind a single in-flight task; last write wins.

---

## 9. Phases

### Phase 1 — Model, persistence, resolver

**Status:** completed — 31 new tests, full `DotNetCloud.Core.Tests` suite green (532 passed), 0 warnings.

- ✓ `HomeWidgetPreferences` model + JSON settings helper (camelCase, style stored as a token string, `DefaultIgnoreCondition.WhenWritingNull`)
- ✓ `HomeWidgetStyles`: tokens `strictly-business` / `art-department` / `hard-copy`, display names, one-line descriptions, default (`art-department`), `Normalize` (unknown → default)
- ✓ `HomeWidgetLayoutResolver` pure functions `Resolve` + `BuildPreferences` (§4.2)
- ✓ Unit tests: round-trip, unknown token, malformed JSON, newer version, empty prefs, hidden entries, new-module append, unknown moduleId retention, case-insensitive ids, order stability
- ✓ Save/load round-trip test with a mocked `IUserSettingsService`

### Phase 2 — CSS variants

**Status:** completed — all three styles verified in light and dark at desktop width, and all three collapse to a single column at ≤640px with their own gaps.

- ✓ Create `widgets.variants.css`; move the Art Department block in from `app.css`, prefix every selector with `[data-widget-style="art-department"]`
- ✓ Port the Hard Copy block from `artifacts/preview/option-b-section.css`, prefix with `[data-widget-style="hard-copy"]`, and make it **fully self-contained** (a section that _replaces_ others must re-declare its own `display: grid` / `display: flex` — this exact omission broke the Hard Copy preview earlier)
- ✓ Restore the Strictly Business base shell into `app.css` from git (§5.4), plus a `.widget-card-icon .material-icon` size rule the scoped file used to provide
- ✓ Link `widgets.variants.css` in `App.razor`; bump `app.css?v=` → `20260912-02`
- ✓ Verify all three styles render correctly in light **and** dark mode, at desktop and ≤640px widths
- ✓ **Bug found and fixed during verification:** the first prefixing pass broke every dark-mode rule in both variants (see §5.5 trap 3). 33 dark-mode selectors now assert `.dark-mode` outermost.
- ☐ Delete `artifacts/preview/` — **deferred to Phase 4**; kept as the verification harness for the Phase 3 UI work

### Phase 3 — Home page UI

**Status:** code complete — builds with 0 warnings, 773 server tests green (13 new for the service). Live behaviour is verified in Phase 4.

- ✓ `HomeWidgetLayoutService` (load, resolve, save style, save order, save visibility, reset; serialised writes per R7)
- ✓ `HomeWidgetCustomizer.razor` (+ `.razor.css`) — style radio group, widget list with drag + Move up/down + visibility, reset
- ✓ Wire `Home.razor`: resolve layout, render only visible widgets in resolved order, stamp `data-widget-style`
- ✓ Error + status reporting via an inline `aria-live` region
- ☐ Confirm instant style switching with no round-trip — deferred to the Phase 4 live check
- ✓ Added `drag_indicator` and `tune` to `MaterialSvgIcons` (both were missing, and would otherwise have rendered as text fallbacks)

**Two deliberate deviations from §6, both to reduce risk:**

1. **No toast integration.** `DncToastService` has no DI registration — it is passed as a `[Parameter]` to the `<DncToast>` host — so reaching it from an arbitrary component is awkward. The customizer reports save failures in an inline `aria-live` status line instead. For this feature that is arguably better UX: the error appears next to the control that failed, rather than as a floating toast that can be missed.
2. **The toggle button lives in `Home.razor`, not the customizer.** The button sits inside the `.widgets-section-header` flex row while the panel must render _below_ that row as a full-width sibling. Splitting them keeps the layout correct without resorting to absolute positioning or a layout hack.

**Extra coverage beyond the plan:** `HomeWidgetLayoutServiceTests` (13 tests) covers registry order, persistence round-trips, the boundary cases on reordering, optimistic state on a failed write, and registry churn (a widget appearing/disappearing). The `IUserSettingsService` double is a small hand-written fake rather than a mock, because the round-trip tests need real stored state.

### Phase 4 — Docs, verification, commit

- ✓ Update `MODULE_WIDGETS_PLAN.md` §2.7 → implemented, link this plan
- ✓ Update `IMPLEMENTATION_CHECKLIST.md` (`✓`/`☐`) — **targeted edits only**
- ✓ Update `MASTER_PROJECT_PLAN.md` — Quick Status Summary table + step Status/Deliverables/Notes — **targeted edits only**
- ✓ `dotnet build` clean — 0 warnings, 0 errors, full solution
- ✓ Affected test suites green: `Core.Tests` 532, `Core.Server.Tests` 773, `UI.Shared.Tests` 111
- ☐ **Full-repo `dotnet test` (every project) — OUTSTANDING.** Only the three suites touched by this change were run; the remaining projects were inferred unaffected rather than re-run, and a full run was deferred at the user's request. **Re-run before the PR is merged.**
- ✓ **End-to-end verification on a running instance** — deployed and confirmed working by the user 2026-09-12
- ✓ `git status --short` clean; temporary preview harness removed; committed `61836982` and pushed `origin/feature/crazy-widgets`

**Deploy:** `sudo ./scripts/deploy.sh --force --verify` — 15/15 targets, all assembly hashes verified, migrations applied, `/health/ready` HTTP 200, version `0.6.05`. Confirmed `widgets.variants.css` is present in the deployed `wwwroot/_content/DotNetCloud.UI.Web/css/`; a 404 there would have silently downgraded every user to the base style with no error surface.

---

## 10. Verification (commit gate)

Rule #1 in `.github/copilot-instructions.md` blocks committing until testing is complete, and explicitly warns that "unit tests pass ≠ testing complete". Required before commit:

1. ✓ `dotnet build` — 0 warnings, 0 errors (full solution).
2. ⚠ `dotnet test` — the three suites this change touches are green: `Core.Tests` 532, `Core.Server.Tests` 773, `UI.Shared.Tests` 111. A **full-repo run was deferred at the user's request**; the remaining projects were assumed unaffected rather than re-run. Re-run before merging the PR.
3. ⚠ Live instance, logged in as a real user — **user-verified 2026-09-12** on the deployed build (v0.6.05): "looks good". The granular checks below were *not* individually signed off, so treat them as the regression checklist for the PR review or a follow-up session:
   - ☐ Default state on first ever visit = **Art Department**, registry order, all visible
   - ☐ Switch to each of the three styles → grid restyles immediately, correctly, in light and dark mode
   - ☐ Reorder by drag **and** by Move up/down; order sticks
   - ☐ Hide a widget → disappears; re-show → returns to its former position
   - ☐ **Reload the page** → style, order, and visibility all persist
   - ☐ **Load in a second browser** → same layout (proves it is server-side, not `localStorage`)
   - ☐ Reset to default → back to registry order, all visible, default style
   - ☐ Keyboard-only pass: reach the customizer, change style, toggle a checkbox, reorder with Move up/down
   - ☐ Disable a module in admin → its widget disappears; re-enable → returns to its saved slot
4. ✓ Confirm the working tree contains **only** intended changes.

Determine the live environment from **runtime config**, not this repo (committed configs use generic `localhost` defaults by design).

---

## 11. Decisions

- **D1 — Default style for users who have never chosen. DECIDED: Art Department.** New users, and anyone who has never saved a preference, get Art Department. Implemented as `HomeWidgetStyles.DefaultToken = "art-department"`.
- **D2 — Admin-settable instance default.** Proposed: out of scope now (per-user only). Would be a small follow-up reading `SystemSetting` as the fallback before defaulting.
- **D3 — Confirm the three names** (§2).
- **D4 — Hidden widgets keep their slot** (proposed yes) vs. hidden widgets move to the end when re-shown.
- **D5 — Optional enhancement:** dragging the actual cards in the grid while the customizer is open. Proposed: deferred; the customizer list covers the requirement and is accessible.

---

## 12. Out of scope

- Per-widget sizing / column spans (widgets stay uniform grid cells)
- Multiple pages or tabs of widgets
- Sharing layouts between users or by role/team
- Admin/instance-level style overrides (D2)
- Changing widget _content_ or adding new widgets
