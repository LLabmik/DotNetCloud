# Module Error Recovery Plan

**Branch:** `fix/module-error-recovery`
**Date:** 2026-09-06
**Status:** Ready for implementation

---

## 1. Problem

If a module page throws an error (unhandled exception during render or lifecycle), the
entire content area shows an error. After that, clicking to another module appears to do
nothing — the site is effectively non-responsive until the user goes to the home page and
does a full browser refresh.

Expected behavior: after a module error, navigating to another module (or page) should
render that page normally, without a manual refresh.

---

## 2. Root Cause

`src/UI/DotNetCloud.UI.Web/Components/Layout/MainLayout.razor` wraps the entire page body
(`@Body`) in a single Blazor `<ErrorBoundary>`.

- When a module page component (rendered through
  `Components/Shared/ModulePageHost.razor` → `DynamicComponent`) throws, the
  `ErrorBoundary` enters its "errored" state and renders its `<ErrorContent>` (the
  `ErrorDisplay` component).
- Blazor's `ErrorBoundary` does **not** reset itself when the route changes. The layout is
  a persistent component, so the boundary stays in the errored state across navigations.
- The only existing way to reset it is the "Try Again" button (`RecoverError()` →
  `_errorBoundary.Recover()`), which is not triggered by navigation.

Result: after an error, every navigation still renders the stale error content instead of
the new page. A full browser refresh rebuilds the circuit and creates a fresh boundary,
which is why refreshing "fixes" it.

---

## 3. Solution Overview

Two changes (scope: "Both"):

1. **Auto-recover on navigation** — subscribe to `NavigationManager.LocationChanged` and
   call `Recover()` on the error boundary whenever the URL changes.
2. **Per-module error isolation** — give each module its own `<ErrorBoundary>` inside
   `ModulePageHost.razor` so a module crash only blanks that module's content area (the
   sidebar, top bar, and the rest of the layout stay fully interactive). The module-level
   error's retry button reads **"Reload module"** (the layout-level one keeps **"Try Again"**).

Three files are modified:

| File                                                               | Change                                                                                                    |
| ------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------- |
| `src/UI/DotNetCloud.UI.Web/Components/Shared/ErrorDisplay.razor`   | Add a configurable `DismissText` parameter (default `"Try Again"`).                                       |
| `src/UI/DotNetCloud.UI.Web/Components/Layout/MainLayout.razor`     | Inject `NavigationManager`, recover the layout boundary on navigation, unsubscribe in `Dispose`.          |
| `src/UI/DotNetCloud.UI.Web/Components/Shared/ModulePageHost.razor` | Wrap `DynamicComponent` in its own `ErrorBoundary`; recover on navigation; button text `"Reload module"`. |

---

## 4. File Changes (exact)

### 4.1 `ErrorDisplay.razor`

Path: `src/UI/DotNetCloud.UI.Web/Components/Shared/ErrorDisplay.razor`

**Change 1 of 2 — button text uses the new parameter.**

Find this exact block:

```razor
    @if (OnDismiss.HasDelegate)
    {
        <button class="btn btn-primary" @onclick="OnDismiss">Try Again</button>
    }
```

Replace it with:

```razor
    @if (OnDismiss.HasDelegate)
    {
        <button class="btn btn-primary" @onclick="OnDismiss">@DismissText</button>
    }
```

**Change 2 of 2 — add the `DismissText` parameter.**

Find this exact block (the end of the `@code` section):

```razor
    [Parameter]
    public EventCallback OnDismiss { get; set; }
}
```

Replace it with:

```razor
    [Parameter]
    public EventCallback OnDismiss { get; set; }

    /// <summary>
    /// Text shown on the dismiss/retry button. Defaults to "Try Again".
    /// </summary>
    [Parameter]
    public string DismissText { get; set; } = "Try Again";
}
```

---

### 4.2 `MainLayout.razor`

Path: `src/UI/DotNetCloud.UI.Web/Components/Layout/MainLayout.razor`

**Change 1 of 3 — inject `NavigationManager`.**

Find this exact block (the top `@inject` lines):

```razor
@inject PersistentComponentState PersistentState
@inject MusicPlaybackState MusicPlayback
@implements IDisposable
```

Replace it with:

```razor
@inject PersistentComponentState PersistentState
@inject MusicPlaybackState MusicPlayback
@inject NavigationManager Navigation
@implements IDisposable
```

**Change 2 of 3 — subscribe in `OnInitializedAsync`.**

Find this exact block:

```razor
    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = PersistentState.RegisterOnPersisting(PersistState);

        if (PersistentState.TryTakeFromJson<bool>(NavbarCollapsedKey, out var restored))
```

Replace it with:

```razor
    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = PersistentState.RegisterOnPersisting(PersistState);
        Navigation.LocationChanged += OnLocationChanged;

        if (PersistentState.TryTakeFromJson<bool>(NavbarCollapsedKey, out var restored))
```

**Change 3 of 3 — add the handler and unsubscribe in `Dispose`.**

Find this exact block (the end of the file):

```razor
    private void RecoverError() => _errorBoundary?.Recover();

    public void Dispose() => _persistingSubscription.Dispose();
}
```

Replace it with:

```razor
    private void RecoverError() => _errorBoundary?.Recover();

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _errorBoundary?.Recover();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _persistingSubscription.Dispose();
    }
}
```

> `LocationChangedEventArgs` is in `Microsoft.AspNetCore.Components.Routing`, which is
> already imported by `src/UI/DotNetCloud.UI.Web/_Imports.razor` — no new `@using` needed.

---

### 4.3 `ModulePageHost.razor` (replace the whole file)

Path: `src/UI/DotNetCloud.UI.Web/Components/Shared/ModulePageHost.razor`

Replace the entire file contents with:

```razor
@using DotNetCloud.UI.Web.Services
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Routing

@inject ModuleUiRegistry ModuleRegistry
@inject NavigationManager Navigation
@implements IDisposable

@if (_componentType is not null)
{
    <ErrorBoundary @ref="_moduleErrorBoundary">
        <ChildContent>
            <DynamicComponent Type="_componentType" Parameters="Parameters" />
        </ChildContent>
        <ErrorContent Context="ex">
            <ErrorDisplay Exception="ex" OnDismiss="RecoverModule" DismissText="Reload module" />
        </ErrorContent>
    </ErrorBoundary>
}
else
{
    <div class="empty-state">
        <p>Module page "@RouteKey" is not registered.</p>
    </div>
}

@code {
    /// <summary>
    /// The route key identifying the module page to render (e.g., "files.browser").
    /// </summary>
    [Parameter, EditorRequired]
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional parameters to pass to the dynamic component.
    /// </summary>
    [Parameter]
    public IDictionary<string, object>? Parameters { get; set; }

    private Type? _componentType;
    private ErrorBoundary? _moduleErrorBoundary;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override void OnParametersSet()
    {
        _componentType = ModuleRegistry.GetPage(RouteKey);
    }

    private void RecoverModule() => _moduleErrorBoundary?.Recover();

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _moduleErrorBoundary?.Recover();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
```

> Why `ModulePageHost` also subscribes to `LocationChanged`: cross-module navigation
> (e.g., Files → Chat) swaps the whole page component, so the module boundary is recreated
> anyway. But same-module navigation via query-string (e.g., Files deep links using
> `?fileId=...&_nav=...`) reuses the same page component, so the module boundary would
> otherwise stay stuck. Recovering on every location change keeps both cases consistent.

---

## 5. Build & Verification

### 5.1 Build

From the repo root (PowerShell):

```powershell
dotnet build
```

The solution must build cleanly (the repo treats warnings as errors).

### 5.2 Manual test A — module error isolation + cross-module recovery (the actual bug)

1. **Temporarily** add a throw at the top of the Files module's init method.

   File: `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

   `OnInitializedAsync()` starts at line 220. Add this as the **first line inside the
   method body** (before `var caller = await GetCallerContextAsync();`):

   ```csharp
   throw new InvalidOperationException("test module error");
   ```

2. Build and run the app. Log in.

3. Open **Files** (`/apps/files`).

   Expected:
   - The content area shows an inline error: "Something went wrong" and the message
     "test module error", with a **"Reload module"** button.
   - The sidebar, top bar, and notification/music components remain visible and clickable
     (the app is **not** blanked out).

4. Click **Chat** in the sidebar.

   Expected:
   - Chat renders normally **without any refresh**. ✅ This is the core fix.

5. Optional: navigate back to **Files** — the error shows again (expected, because the
   throw is still present). Click **"Reload module"** — it re-renders and shows the error
   again (still broken). This confirms the retry button works.

6. **Remove the temporary `throw`**, rebuild, and confirm Files works normally again.

### 5.3 Manual test B — layout boundary auto-recovery (non-module pages)

1. Temporarily add `throw new InvalidOperationException("test page error");` at the top of a
   non-module page's `OnInitializedAsync` (e.g., `Home.razor` at `@page "/"`, or any
   Admin page). If the page has no `OnInitializedAsync`, add an override.

2. Navigate to that page.

   Expected: the layout-level error shows "Something went wrong" with a **"Try Again"**
   button (default text unchanged).

3. Navigate to any other page.

   Expected: the new page renders normally **without refresh**.

4. **Remove the temporary throw** and rebuild.

### 5.4 Regression checks

- Normal navigation between all modules (Files, Chat, Calendar, Notes, Music, AI, …) with
  no errors — unchanged behavior.
- Admin pages, Home, Profile, Auth pages still render.
- Sidebar collapse preference still persists (the `OnInitializedAsync`/`Dispose` changes
  must not break the existing `PersistingComponentStateSubscription`).

---

## 6. Notes / Gotchas

- `Recover()` on an `ErrorBoundary` is a safe no-op when the boundary is not in an errored
  state, so calling it unconditionally on every navigation is fine.
- The nested boundary is intentional: the module-level boundary catches module exceptions
  first; the layout-level boundary remains as the safety net for non-module pages
  (admin/home/profile/auth/search).
- `ErrorBoundary` catches exceptions from child rendering and lifecycle methods
  (`OnInitialized`, `OnInitializedAsync`, `OnParametersSet`, render). It does **not** catch
  exceptions thrown inside event handlers (button clicks) — that is out of scope here.
- After implementation, per repo convention, update the tracking docs with **targeted
  edits**:
  - `docs/IMPLEMENTATION_CHECKLIST.md` (mark completed items `✓`)
  - `docs/MASTER_PROJECT_PLAN.md` (Quick Status Summary + step status/deliverables/notes)
- Do **not** commit until the build is clean and the manual verification above has passed.
  Remove all temporary `throw` statements before committing.
