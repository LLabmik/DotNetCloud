# Plan — Blazor Form Defaults: Enter submits in a text box (not a text area) + autofocus + TOTP auto-submit

**Status:** ⏳ handoff to server agent (mint22) for implementation
**Branch:** `fix/form-submit-handling` (HEAD = `c9aa08a3`, same as `origin/main`)
**Canonical plan:** this file
**From:** client agent (monolith), 2026-09-06
**Target:** server agent (mint22) — implementation + deploy to dev; interactive acceptance by user
**Owner doc:** `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` → Active Handoff

---

## 1. Objective (user requirement, verbatim intent)

> "Default for forms (ex: login, TOTP, file create name, etc) should be to submit if Enter is pressed while in a text box (Not text area)."

Additional requirements confirmed with the user:

1. **Shared global mechanism** for the behavior (not per-form bespoke C# keydown handlers).
2. **This pass only wires these forms**: Login, TOTP (verify **and** MFA-setup verify step), Files create/rename dialogs. The mechanism must be easy to extend to more forms later (user will add more after considering scope).
3. **TOTP auto-submits when the 6th digit is filled in.**
4. **All listed forms auto-focus their first text box.**
5. Files "New File" dialog = **per-row action** (Option A): Enter in the Document row creates the document; Enter in the freeform File row creates the freeform file. Each row's own Create button is its primary action.
6. **Text areas are exempt**: Enter in a multi-line text area inserts a newline, never submits.

## 2. Scope

### In scope (this pass)
- Login page (`/auth/login`) — autofocus Username; Enter submits.
- TOTP verify (`/auth/mfa-verify`) — autofocus; Enter submits; auto-submit at 6 digits.
- MFA setup verify step (`/auth/mfa-setup`, `SetupState.Ready`) — autofocus the code field; Enter submits; auto-submit at 6 digits.
- Files module create/rename dialogs (`FileBrowser.razor`): New Folder, New File (Document + freeform rows), inline Rename — autofocus first text box; Enter triggers the primary (per-row) action; Escape still cancels.

### Out of scope (do NOT touch this pass — user will request later)
- Register, Forgot/Reset password, Change password.
- Admin forms/dialogs (UserCreate/UserEdit/Settings/Org/Groups/BackupSettings/MediaLibraries/Profile…).
- Other modules' dialogs (Chat channel create, Calendar event title, Notes folder create/rename, AI conversation rename, Music/Email/Bookmarks/…).
- The shared mechanism is built now so adding one of these later = adding attributes to its container (see §6).

## 3. Current behavior (verified against HEAD `c9aa08a3`)

| Surface | Current behavior |
| --- | --- |
| Login.razor (`src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Login.razor`, lines 14–35) | Classic `<form method="post" action="/auth/session/login" data-enhance-nav="false">` with `type="submit"` → **Enter submits natively already**. No autofocus on Username (lines 17–18). |
| MfaVerify.razor (lines 14–30) | Classic `<form method="post" …>` with `type="submit"`; code `<input id="code" … maxlength="6" … required autofocus/>` (lines 17–18) → Enter submits natively; **already autofocuses**; does NOT auto-submit at 6 digits. |
| MfaSetup.razor (lines 66–84) | `<EditForm Model="_verifyModel" OnValidSubmit="VerifyAsync" FormName="mfa-setup-verify">` containing `<InputText id="verify-code" … @bind-Value="_verifyModel!.Code" placeholder="000000" maxlength="6"/>` + `type="submit"` button → Enter submits natively (implicit submit). **No autofocus**; no auto-submit at 6 digits. Note: `InputText` binds on `onchange` by default → model can lag the typed value on Enter/submit (classic Blazor gotcha). `VerifyAsync()` sets `_submitting`; model `TotpVerifyModel.Code` has `[Required]`, `[StringLength(6)]`, `[RegularExpression("^[0-9]{6}$")]`. |
| Files `FileBrowser.razor` (dialogs) | All four dialogs are **div-based, NOT `<form>`s**; buttons default to no explicit `type`. Each input currently hand-rolls Enter/Escape in C# `@onkeydown`; none `preventDefault`s. |
| Files create-folder dialog | Lines 204–208: `<div class="create-folder-dialog"> <input … @bind="NewFolderName" … @onkeydown="HandleFolderKeyDown" autofocus/> <button class="btn btn-primary btn-sm" @onclick="CreateFolder">Create</button> <button class="btn btn-sm" @onclick="HideCreateFolder">Cancel</button> </div>` |
| Files rename dialog | Lines 490–492: same `.create-folder-dialog` class: `<input … @bind="_renameNewName" … @onkeydown="HandleRenameKeyDown" autofocus/>` + Rename (`ConfirmRename`)/Cancel (`CancelRename`). |
| Files create-file dialog | Lines 211–238: `<div class="create-file-dialog">` containing **two `.create-file-row` divs**, each with its own name input + Create button: row 1 (only when `CollaboraNewFileExtensions.Count > 0`) = Document name `<input … @bind="NewDocumentName" @onkeydown="HandleCreateDocumentKeyDown" autofocus/>` + `<select>` extension + `CreateDocumentAsync`; row 2 = freeform `<input … @bind="FreeformFileName" @onkeydown="HandleFreeformFileKeyDown" @ref="_freeformInput"/>` + `CreateFreeformFileAsync`. Cancel is in `.create-file-actions` (lines 236–237). |
| Files code-behind (`FileBrowser.razor.cs`) | `HandleFolderKeyDown` (line 780), `HandleCreateDocumentKeyDown` (line 833), `HandleFreeformFileKeyDown` (line 842), `HandleRenameKeyDown` (line 1321) — each `if Enter → create/rename`, `if Escape → hide`. |
| App.razor (`src/UI/DotNetCloud.UI.Web/Components/App.razor`) | Per-page JS includes list ends at line 57 (`logout-confirm.js`), then `_framework/blazor.web.js` (line 58). Versioned `?v=YYYYMMDD-NN` includes are the convention. |

## 4. Design — shared global mechanism

Add ONE shared JS module loaded once by the Blazor Web app (`form-defaults.js`). It is attribute-driven so any current or future form/dialog participates just by adding an attribute — no C# Enter code needed. It operates on the real DOM, so it covers SSR pages, Blazor Server interactive pages, and dynamically inserted dialogs (all module UIs render inside the same DOM under `Routes @rendermode="InteractiveServer"`).

### 4.1 Behaviors

**A. Enter-submits (`data-enter-submit`)** — on a container (real `<form>` OR a div/dialog that opted in):

- On `Enter` (no Shift/Ctrl/Alt/Meta) while focus is in a **single-line text `<input>`** (type text/password/email/url/tel/number/search/date/etc.), run the input's submit action:
  1. If the input is inside a real `<form>` → `form.requestSubmit()` (native forms POST; Blazor `EditForm` receives its submit event and runs validation + `OnValidSubmit`).
  2. Else find the nearest `[data-enter-submit]` ancestor → click its **primary button** = first of `[data-primary-action]`, `button[type="submit"]`, `button.btn-primary`, else the first `button`.
- **Excluded (never auto-submit):**
  - `TEXTAREA` and any `contenteditable` (multi-line editors/composers) → Enter inserts a newline.
  - Non-text controls: `input[type=button|submit|reset|checkbox|radio|file|range|color|hidden|image]`.
  - Disabled/read-only inputs.
  - Any input (or ancestor) carrying `data-no-enter-submit` (opt-out for search/autocomplete/tag/mention inputs — use where Enter means something else).
- `preventDefault()` only when an action actually ran.

**B. Auto-submit at N chars (`data-autosubmit="N"`)** — on an input:

- On each `input` event, when the trimmed value length reaches N, run the same submit action as (A). Used for the 6-digit TOTP code.
- **Important:** For a **native (non-interactive) form** this is safe (the browser submits the DOM values). For an **interactive Blazor `EditForm`**, JS `requestSubmit()` can race the Blazor model round-trip (submit arrives before the typed value is pushed to the server-side model) → the server sees a stale 5-digit code. Therefore, on interactive `EditForm`s prefer the **C# `@bind:after`** approach in §5.3 instead of `data-autosubmit`. (`data-autosubmit` remains the right tool for native forms and future non-interactive forms.)

**C. Autofocus first text box** — `data-autofocus` on an input, or `data-autofocus-first` on a container:

- Focus the first **visible, enabled, single-line text input** inside a `[data-autofocus-first]` container (or the `[data-autofocus]` element). Runs on initial load (`DOMContentLoaded`) and whenever new matching elements are inserted (`MutationObserver`), which handles async setup steps (MFA-setup Ready state) and dialogs opened on demand.
- Guard: never steal focus while the user is already typing in an input/textarea/contenteditable.

### 4.2 `form-defaults.js` (full implementation to add)

Place at: `src/UI/DotNetCloud.UI.Web/wwwroot/js/form-defaults.js`

```js
// form-defaults.js — DotNetCloud shared form defaults.
// Rule: Enter in a single-line TEXT BOX submits the enclosing form/dialog; TEXT AREAS and
// rich editors are exempt (Enter = newline). Attribute-driven so any form/dialog can opt in:
//   [data-enter-submit]   container: Enter in a text box submits (form) / clicks primary button.
//   [data-autosubmit="N"] input:     auto-submit once value length >= N (native forms; see MFA plan).
//   [data-autofocus]      input:     focus it when available.
//   [data-autofocus-first] container: focus first visible text input inside.
// Opt out a special input: data-no-enter-submit (self or ancestor).
(function () {
  'use strict';

  var TEXT_LIKE = ['text', 'password', 'email', 'url', 'tel', 'number', 'search',
    'date', 'datetime-local', 'month', 'week', 'time'];
  var PRIMARY_SELECTOR = 'button[data-primary-action], button[type="submit"], button.btn-primary';

  function isTextInput(el) {
    if (!el || el.tagName !== 'INPUT') return false;
    var type = (el.getAttribute('type') || 'text').toLowerCase();
    return TEXT_LIKE.indexOf(type) !== -1 && !el.disabled && !el.readOnly;
  }

  function isExcluded(el) {
    if (el.tagName === 'TEXTAREA' || el.isContentEditable) return true;
    return !!el.closest('[data-no-enter-submit]');
  }

  // Returns the submit scope: a real <form> wins, else the nearest opted-in container.
  function submitScopeFor(el) {
    var form = el.closest('form');
    if (form) return { kind: 'form', node: form };
    var container = el.closest('[data-enter-submit]');
    if (container) return { kind: 'container', node: container };
    return null;
  }

  function runSubmit(el) {
    var scope = submitScopeFor(el);
    if (!scope) return false;
    if (scope.kind === 'form') {
      var form = scope.node;
      if (typeof form.requestSubmit === 'function') form.requestSubmit();
      else form.submit();
      return true;
    }
    var primary = scope.node.querySelector(PRIMARY_SELECTOR + ', button');
    if (primary && !primary.disabled && typeof primary.click === 'function') {
      primary.click();
      return true;
    }
    return false;
  }

  function onKeyDown(e) {
    if (e.key !== 'Enter') return;
    if (e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
    var el = e.target;
    if (!isTextInput(el) || isExcluded(el)) return;
    if (runSubmit(el)) e.preventDefault();
  }

  function onInput(e) {
    var el = e.target;
    if (!isTextInput(el) || isExcluded(el)) return;
    var raw = el.getAttribute('data-autosubmit');
    if (!raw) return;
    var n = parseInt(raw, 10);
    if (!isNaN(n) && el.value && el.value.trim().length >= n) runSubmit(el);
  }

  function firstVisibleTextInput(root) {
    var inputs = root.querySelectorAll('input');
    for (var i = 0; i < inputs.length; i++) {
      var el = inputs[i];
      if (!isTextInput(el)) continue;
      var r = el.getBoundingClientRect();
      if (r.width > 0 && r.height > 0) return el;
    }
    return null;
  }

  function tryFocus(el) {
    try {
      if (el && document.activeElement !== el) el.focus();
    } catch (err) { /* ignore */ }
  }

  // Don't steal focus while the user is typing elsewhere.
  function userIsTyping() {
    var a = document.activeElement;
    return !!(a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.isContentEditable));
  }

  function focusTargets() {
    if (userIsTyping()) return;
    var direct = document.querySelector('[data-autofocus]');
    if (direct && isTextInput(direct)) { tryFocus(direct); return; }
    var containers = document.querySelectorAll('[data-autofocus-first]');
    for (var i = 0; i < containers.length; i++) {
      var input = firstVisibleTextInput(containers[i]);
      if (input) { tryFocus(input); return; }
    }
  }

  document.addEventListener('keydown', onKeyDown, true);
  document.addEventListener('input', onInput, true);

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', focusTargets);
  } else {
    focusTargets();
  }
  try {
    new MutationObserver(focusTargets).observe(document.documentElement, { childList: true, subtree: true });
  } catch (err) { /* observer not available — autofocus still runs on load */ }

  window.dotnetcloudFormDefaults = { focusFirst: focusTargets, submit: runSubmit };
})();
```

Notes for the implementer:

- Listener is on `keydown`/`input` at **capture** phase so it runs before Blazor/other delegated handlers; `preventDefault` suppresses the browser default, and removing the old C# Enter branches (see §5.4) prevents double actions.
- `runSubmit` clicking the container's primary `button` dispatches a real `click`, which Blazor's delegated `@onclick` receives — no JS→.NET interop needed.
- Keep the file focused and dependency-free (no modules/`import`); it is loaded as a classic script.

## 5. Implementation steps (per file)

### 5.1 `src/UI/DotNetCloud.UI.Web/wwwroot/js/form-defaults.js` (NEW)
Add the file from §4.2 verbatim.

### 5.2 `src/UI/DotNetCloud.UI.Web/Components/App.razor`
After the existing per-page script includes (after line 57 `logout-confirm.js`, immediately **before** `_framework/blazor.web.js` on line 58), add:

```html
    <script src="_content/DotNetCloud.UI.Web/js/form-defaults.js?v=20260906-01"></script>
```

### 5.3 Auth pages

**`src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Login.razor`**
- Lines 17–18 (Username `<input>`): add `autofocus data-autofocus`:
```html
                <input id="username" name="username" class="input" type="text" autocomplete="username" required autofocus
                       value="@Username" placeholder="Bill.Jones" data-autofocus />
```
- Nothing else — Enter already submits via the classic `<form method="post">` (verify in §7).

**`src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/MfaVerify.razor`**
- Lines 17–18 (code `<input>`): it already has `autofocus`; add `data-autosubmit="6"` (and `data-autofocus` for the JS helper):
```html
                <input id="code" name="code" class="input input-code" type="text" inputmode="numeric" pattern="[0-9]*"
                       maxlength="6" placeholder="000000" autocomplete="one-time-code" required autofocus
                       data-autosubmit="6" data-autofocus />
```
- This is a native postback form → JS auto-submit at 6 digits works (DOM values posted). Enter already submits natively.

**`src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/MfaSetup.razor`** (interactive `EditForm` → auto-submit must be C#-driven; see §4.1-B)
- Line 66 (`<EditForm …>`): add `@ref="_verifyForm"`:
```html
                <EditForm Model="_verifyModel" OnValidSubmit="VerifyAsync" FormName="mfa-setup-verify" @ref="_verifyForm">
```
- Lines 69–70 (`<InputText id="verify-code" …>`): bind `oninput`, add `@bind:after` + `autofocus`; do NOT add `data-autosubmit` here (JS race on interactive forms):
```html
                        <InputText id="verify-code" class="input input-code" @bind-Value="_verifyModel!.Code"
                                   @bind:event="oninput" @bind:after="OnVerifyCodeChanged"
                                   placeholder="000000" maxlength="6" autofocus />
```
- Code-behind (`@code` block): add the field + handler near `VerifyAsync`:
```csharp
    private EditForm? _verifyForm;

    /// <summary>
    /// Runs after the verify-code input value is bound (oninput). Auto-submits once the
    /// 6th digit is present, reusing the same validation+verify path as the Verify button.
    /// </summary>
    private async Task OnVerifyCodeChanged()
    {
        if (_submitting || _state != SetupState.Ready)
            return;

        var code = _verifyModel.Code?.Trim() ?? string.Empty;
        if (code.Length < 6)
            return;

        // Mirror OnValidSubmit: only proceed when DataAnnotations validation passes.
        if (_verifyForm?.EditContext is { } editContext && !editContext.Validate())
            return;

        await VerifyAsync();
    }
```
- Add `using Microsoft.AspNetCore.Components.Forms;` if not already present (it is — `EditForm` is used in markup; ensure the namespace is in `@code` scope via `_Imports`; if unsure add the using).
- Enter still submits natively through the `EditForm` submit button; `@bind:event="oninput"` fixes the model-lag gotcha so Enter/auto-submit always see the full 6 digits.
- Verify `VerifyAsync()` is unchanged (it already guards with `_submitting` and clears on failure).

### 5.4 Files dialogs — `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor`

**New Folder dialog (lines 204–208)** — add `data-enter-submit data-autofocus-first` to the container; remove `autofocus` from the input (the shared helper focuses the first visible input):
```html
                                    <div class="create-folder-dialog" data-enter-submit data-autofocus-first>
                                        <input type="text" @bind="NewFolderName" placeholder="Folder name" @onkeydown="HandleFolderKeyDown" />
                                        <button class="btn btn-primary btn-sm" @onclick="CreateFolder">Create</button>
                                        <button class="btn btn-sm" @onclick="HideCreateFolder">Cancel</button>
                                    </div>
```

**Create-file dialog (lines ~211–238)** — **per-row action (Option A)**: add `data-enter-submit` to **each** `.create-file-row`; add `data-autofocus-first` to the `.create-file-dialog` container; remove the per-input `autofocus` from the Document row (helper picks the first visible row):
```html
                                @if (IsShowCreateDocument)
                                {
                                    <div class="create-file-dialog" data-autofocus-first>
                                        @if (CollaboraNewFileExtensions.Count > 0)
                                        {
                                            <div class="create-file-row" data-enter-submit>
                                                <span class="create-file-label">Document</span>
                                                <input type="text" @bind="NewDocumentName" placeholder="Document name"
                                                       @onkeydown="HandleCreateDocumentKeyDown" />
                                                <select @bind="SelectedDocumentExtension">
                                                    @foreach (var extension in CollaboraNewFileExtensions)
                                                    {
                                                        <option value="@extension">.@extension</option>
                                                    }
                                                </select>
                                                <button class="btn btn-primary btn-sm" @onclick="CreateDocumentAsync">Create</button>
                                            </div>
                                        }
                                        <div class="create-file-row" data-enter-submit>
                                            <span class="create-file-label">File</span>
                                            <input type="text" @bind="FreeformFileName" placeholder="filename.txt"
                                                   @onkeydown="HandleFreeformFileKeyDown" @ref="_freeformInput" />
                                            <button class="btn btn-primary btn-sm" @onclick="CreateFreeformFileAsync">Create</button>
                                        </div>
                                        <div class="create-file-actions">
                                            <button class="btn btn-sm" @onclick="HideCreateDocumentDialog">Cancel</button>
                                        </div>
                                    </div>
                                }
```
- `data-enter-submit` is scoped to the row, so Enter in the Document name clicks the Document row's Create; Enter in the freeform name clicks that row's Create.

**Rename dialog (lines 490–492)** — same treatment as New Folder:
```html
                                            <div class="create-folder-dialog" data-enter-submit data-autofocus-first>
                                                <input type="text" @bind="_renameNewName" placeholder="New name" @onkeydown="HandleRenameKeyDown" />
                                                <button class="btn btn-primary btn-sm" @onclick="ConfirmRename">Rename</button>
                                                <button class="btn btn-sm" @onclick="CancelRename">Cancel</button>
                                            </div>
```

### 5.5 Files code-behind — `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

Trim the Enter branches from the four handlers (Enter is now handled globally by `form-defaults.js`; **keep Escape-to-close**). Removing Enter avoids a double create (JS primary-click + C# handler).

- `HandleFolderKeyDown` (line 780):
```csharp
    protected void HandleFolderKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            _showCreateFolder = false;
    }
```
- `HandleCreateDocumentKeyDown` (line 833):
```csharp
    protected void HandleCreateDocumentKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            _showCreateDocument = false;
    }
```
- `HandleFreeformFileKeyDown` (line 842):
```csharp
    protected void HandleFreeformFileKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            _showCreateDocument = false;
    }
```
- `HandleRenameKeyDown` (line 1321) — read the current body first (grep shows the method at 1321; it likely mirrors the others and calls `ConfirmRename`/`CancelRename`):
```csharp
    protected void HandleRenameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CancelRename();
    }
```
> ⚠️ If any handler also handled other keys (e.g. nothing beyond Enter/Escape today), preserve Escape semantics exactly. If a handler was `async Task` purely for Enter, it can become sync `void`; update the `@onkeydown` wiring stays the same (Blazor accepts sync handlers).

## 6. Extension path (for later forms — NOT now)

Any future form/dialog gets the default by adding:
- Real `<form>`/`EditForm` → already covered (native + global). If it's a div-dialog → add `data-enter-submit` to its action container (and `data-primary-action` on the button if the primary isn't `.btn-primary`).
- First-text-box autofocus → `data-autofocus` on the input or `data-autofocus-first` on the container.
- Auto-submit on reaching max length → `data-autosubmit="N"` for native forms; `@bind:event="oninput"` + `@bind:after` handler for interactive `EditForm`s.
- Special inputs (search/autocomplete/tag/mention) that must NOT submit → `data-no-enter-submit`.

## 7. Verification (mandatory before any commit — hard gate)

### 7.1 Build
- `dotnet build` for the changed projects (and solution where practical): `src/UI/DotNetCloud.UI.Web`, `src/Modules/Files/DotNetCloud.Modules.Files`, plus dependents. 0 warnings/errors (TreatWarningsAsErrors is on).

### 7.2 Deploy (server agent, mint22 dev)
- Deploy via the usual server deploy script to the mint22 dev environment.
- Static asset reachable: `GET https://mint22:5443/_content/DotNetCloud.UI.Web/js/form-defaults.js` → 200.
- `/health/ready` Healthy; no pending migrations (no schema change).

### 7.3 Interactive browser acceptance (user — server agent typically can't obtain a session)
Run against the deployed dev server. Matrix:

1. **Login** (`/auth/login`): Username field auto-focused on load; typing + **Enter** submits (native POST). Regression: no double POST from the JS.
2. **MFA verify** (`/auth/mfa-verify`): code field auto-focused; type 6 digits → **auto-submits on the 6th**; Enter also submits; <6 digits does not submit.
3. **MFA setup** (`/auth/mfa-setup`, Ready step): code field auto-focused when the step appears; typing 6 digits auto-verifies; Enter submits; invalid code shows the existing error and does not proceed.
4. **Files — New Folder**: dialog appears → name field auto-focused; type + Enter creates the folder; Escape closes.
5. **Files — New File**: 
   - With Collabora configured: Document row appears first and auto-focuses its name; Enter in it creates the **document**.
   - Freeform row: Enter in `filename.txt` creates the **freeform file**.
6. **Files — Rename** (context menu → Rename): input auto-focused; Enter renames; Escape cancels.
7. **Regression — text areas NOT submitted**: chat composer / note & event description / MarkdownEditor / any `textarea`/contenteditable → Enter inserts a newline and does NOT submit.
8. **Regression — global search** and any `data-no-enter-submit` inputs still behave normally (no accidental submit).
9. **Regression — no double actions**: after Enter in a Files dialog, exactly ONE folder/file is created.

### 7.4 Hand-off of acceptance
Record the acceptance result (PASS/FAIL + evidence) in the Active Handoff / relay back. If any listed form still fails Enter-submit after wiring, root-cause (e.g., missing submit button or a wrapper `<form>` swallowing implicit submit) and fix that container before commit.

## 8. Notes / pitfalls for the implementer
- `.razor`/markup in module RCLs: if `read_file`/grep tooling seems stale on `src/Modules/…`, read from disk (e.g. `git show HEAD:<path>` or `Get-Content`) — files may be open in the editor and buffered.
- Do NOT add `data-autosubmit` to the interactive MFA-setup `EditForm` (JS/model race). Use the `@bind:after` approach.
- Keep the existing Escape-to-close behavior on the Files dialogs.
- Do not alter any other forms/pages (out of scope list in §2).
- No CSS/schema/test-project changes are expected. If adding tests is trivial (none of the changed surfaces have an existing browser test harness in repo), they are optional; build + §7.3 acceptance is the gate.
