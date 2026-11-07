# Module UI Hosting

How a (non-required) DotNetCloud module ships its own web UI, and how the core frames it.

> Required modules are the exception: they are part of the core and their Blazor pages live inside
> the core process. A manifest for a required module must not declare a `ui` block — the validator
> rejects it.

---

## 1. Declaring a UI

Add a `ui` section to your **version 2** manifest:

```json
{
  "manifestVersion": 2,
  "id": "vendor.receipts",
  "name": "Receipts",
  "version": "1.0.0",
  "ui": {
    "type": "iframe",
    "entryPath": "/",
    "navItems": [
      { "id": "receipts", "label": "Receipts", "icon": "receipt_long", "path": "/" },
      { "id": "settings", "label": "Receipt settings", "icon": "settings", "path": "/settings" }
    ]
  }
}
```

| Field | Rules |
| ----- | ----- |
| `ui.type` | Must be `iframe`. Nothing else is accepted. |
| `ui.entryPath` | Site-relative (`/`), no query or fragment, no `..`, no `//host`. |
| `ui.navItems[].id` | Stable id, unique within the module. |
| `ui.navItems[].label` | Sidebar label. |
| `ui.navItems[].icon` | **Google Material Icons ligature name** (`[a-z][a-z0-9_]*`) — never an emoji. If omitted or invalid, the core falls back to the module's canonical icon from `ModuleIconProvider`. |
| `ui.navItems[].path` | Module-relative path (`/`, `/settings`). |

The core registers each nav item as `<label>` → `/modules/<moduleId><path>`, and that shell page
frames your app.

---

## 2. Where your UI is served

| URL | Who serves it |
| --- | ------------- |
| `/apps/<moduleId>/…` | **You.** The core proxies this to your host process, stripping `/apps/<moduleId>` before forwarding, so your app still sees `/`, `/settings`, `/assets/app.js`, … |
| `/modules/<moduleId>…` | The core. This is the chrome page (sidebar + header) that iframes the proxied path. |

Consequences:

- **Your absolute paths keep working.** Because the prefix is stripped, `/assets/app.js` in your
  markup resolves correctly — the browser requests `/apps/<moduleId>/assets/app.js`, and your process
  sees `/assets/app.js`.
- **Never hard-code `/apps/<moduleId>`** in your own links. Use relative paths (`./settings`) or read
  `window.location.pathname` (which already includes the prefix).
- Your listener is whatever Kestrel endpoint the supervisor assigned (a Unix socket by default, phase
  A4) — you only need to serve HTTP/1.1 or HTTP/2 on it, as every module host already does.

---

## 3. Bring the bridge

Copy `dncModuleBridge.js` from the SDK package (`contentFiles/any/any/wwwroot/`) into your host's
`wwwroot` and load it from the framed page:

```html
<script src="/dncModuleBridge.js"></script>
<script>
  window.dncModuleBridge
    .on('init', function (info) { /* role, moduleId, theme, locale */ })
    .on('theme', function (t) { document.body.dataset.theme = t.theme; })
    .start({ title: 'Receipts' });
</script>
```

No npm, no bundler: it is a plain static asset.

### Things to design for

- **Opaque origin.** Your document is sandboxed with `allow-scripts allow-forms allow-popups` and
  **no** `allow-same-origin`. `document.cookie`, `localStorage` and `sessionStorage` are unavailable.
  Keep state on your server and reach it through the same proxied path the document came from.
- **Never trust `event.origin`.** It is `"null"` for every sandboxed frame. The bridge already
  validates `event.source === window.parent`; use its `on(...)` API instead of adding your own
  listener.
- **Do not touch `window.parent`/`window.top`.** They are cross-origin. Use the bridge.

---

## 4. Protocol

Host → frame:

| Type | Payload | When |
| ---- | ------- | ---- |
| `dnc:init` | `{ role, moduleId, theme, locale, title }` | Immediately after each frame load |
| `dnc:theme` | `{ theme, locale }` | The user changed theme or language |
| `dnc:consentResult` | `{ scope, granted }` | A consent decision was recorded |

Frame → host (rate-limited where noted):

| Type | Payload | Notes |
| ---- | ------- | ----- |
| `dnc:ready` | – | Required. The host shows a "not available" state if it does not arrive within 10 s. |
| `dnc:navigate` | `{ path }` | Module-relative only. |
| `dnc:resize` | `{ height }` | Widget role only; clamped to 200–20000 px. |
| `dnc:title` | `{ title }` | ≤ 200 chars. |
| `dnc:openExternal` | `{ url }` | `https:` only, opened with `noopener,noreferrer`. |
| `dnc:requestConsent` | `{ scopes: [...] }` | Renders a core consent banner; this is not an OIDC flow yet (§6). |
| `dnc:notify` | `{ message }` | ≤ 500 chars, **5 per minute**. |

Messages larger than 8 KiB are dropped, unknown types are dropped, and messages whose source is not
the expected window are dropped.

---

## 5. Identity

Your backend never receives the user's core session cookie — the proxy strips `Cookie` and
`Authorization`. What it does receive is:

```
dnc-user-assertion: <core-signed token>
x-forwarded-proto: https
x-forwarded-host: <public host>
```

The assertion is the same token type the core uses for module capability calls: audience-bound to
**your** module id, valid for **60 seconds**, operation `ui`. Verify it with the public key the
supervisor puts in `DOTNETCLOUD_ASSERTION_PUBLIC_KEY`, or simply forward it on your `IDncModuleApi`
calls — `ModuleUserAssertionContext` (in `DotNetCloud.Core.Grpc`) does that for you.

You cannot mint an assertion, and you cannot use one issued for another module.

---

## 6. Widgets (first pass)

A Home-page widget can be a framed document instead of a compiled Blazor component:

- The core registers it with `WidgetUiRegistry.RegisterIframeWidget(moduleId, title, icon, href,
  appPath)` — there is no manifest key for widgets yet.
- The frame is rendered by `ModuleWidgetFrame` and uses the same protocol with `role: "widget"`, so
  `dnc:resize` is honoured (the card grows to your reported height) and notifications work.

---

## 7. Limits and known gaps

- **Consent from a frame is a banner, not an OIDC popup.** `dnc:requestConsent` lists the scopes and
  links to `/connected-apps`.
- **No service worker, no `window.top` access, no cookies** from inside the frame.
- **`apps.<host>` origin separation** is a public-store launch gate, not part of this pass. Until
  then the frame shares the core origin: it cannot *read* core resources, but its requests are
  cross-site for cookie purposes (core cookies are `SameSite=Lax`).
- **Response bodies are capped at 32 MiB** by the proxy.
