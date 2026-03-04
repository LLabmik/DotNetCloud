# Internationalization (i18n) Guide

> **Applies to:** DotNetCloud v0.16+  
> **Last Updated:** 2026-03-03

## Overview

DotNetCloud uses the standard ASP.NET Core / Blazor localization stack:

| Component | Purpose |
|-----------|---------|
| `IStringLocalizer<T>` | Looks up translated strings from `.resx` files |
| `RequestLocalizationMiddleware` | Server-side culture from cookie / Accept-Language |
| Browser `localStorage` | Client-side (WASM) culture persistence |
| `CultureController` | Sets the localization cookie via redirect |
| `CultureSelector` | Blazor dropdown for switching locale |

The architecture supports **InteractiveAuto** render mode — culture is persisted in both a cookie (SSR) and `localStorage` (CSR/WASM).

---

## Supported Cultures

Defined in `src/Core/DotNetCloud.Core/Localization/SupportedCultures.cs`:

| Tag | Language |
|-----|----------|
| `en-US` | English (United States) — **default** |
| `es-ES` | Spanish (Spain) |
| `de-DE` | German (Germany) |
| `fr-FR` | French (France) |
| `pt-BR` | Portuguese (Brazil) |
| `ja-JP` | Japanese (Japan) |
| `zh-CN` | Chinese (Simplified) |

To add a new culture:

1. Add the BCP-47 tag to `SupportedCultures.All`
2. Add a display name to `SupportedCultures.DisplayNames`
3. Create a new `.resx` file: `SharedResources.{tag}.resx`

---

## Resource File Structure

All shared UI strings live in `src/UI/DotNetCloud.UI.Shared/Resources/`:

```
Resources/
  SharedResources.cs          ← marker class for IStringLocalizer<SharedResources>
  SharedResources.resx         ← default (English) strings
  SharedResources.es.resx      ← Spanish translations
  SharedResources.de.resx      ← German (add as needed)
  SharedResources.fr.resx      ← French (add as needed)
  ...
```

The `.resx` files contain all string categories:

- **Common UI** — buttons, labels, navigation (`Save`, `Cancel`, `Search`, …)
- **Authentication** — login, register, MFA (`Auth_Login`, `Auth_Password`, …)
- **Admin** — admin section labels (`Admin_Users`, `Admin_Modules`, …)
- **Errors** — user-facing error messages (`Error_Unexpected`, `Error_NotFound`, …)
- **Validation** — form validation messages (`Validation_Required`, `Validation_InvalidEmail`, …)

### Translation Key Constants

Use `TranslationKeys` (in `DotNetCloud.Core.Localization`) to avoid magic strings:

```razor
@inject IStringLocalizer<SharedResources> Loc

<h1>@Loc[TranslationKeys.Common.Dashboard]</h1>
<button>@Loc[TranslationKeys.Common.Save]</button>
```

---

## Using Localization in Components

### Blazor Components

```razor
@inject IStringLocalizer<SharedResources> Loc

<p>@Loc["Save"]</p>
<p>@Loc[TranslationKeys.Errors.NotFound]</p>

@* Format strings with parameters *@
<p>@string.Format(Loc[TranslationKeys.Validation.Required], "Email")</p>
```

### C# Services / Controllers

```csharp
public class MyService
{
    private readonly IStringLocalizer<SharedResources> _loc;

    public MyService(IStringLocalizer<SharedResources> loc)
    {
        _loc = loc;
    }

    public string GetErrorMessage() => _loc[TranslationKeys.Errors.UnexpectedError];
}
```

---

## Module-Specific Resources

Each module can define its own resource files. Place them in the module project:

```
src/Modules/MyModule/
  Resources/
    MyModuleResources.cs         ← marker class
    MyModuleResources.resx       ← English
    MyModuleResources.es.resx    ← Spanish
```

Then inject `IStringLocalizer<MyModuleResources>` in module components.

---

## How Culture Selection Works

### Flow (InteractiveAuto)

1. User picks a culture in `CultureSelector`
2. Culture is saved to `localStorage` via JS interop (`blazorCulture.set`)
3. Browser navigates to `Culture/Set?culture=es-ES&redirectUri=/dashboard` (`forceLoad: true`)
4. `CultureController.Set` writes the ASP.NET Core localization cookie
5. Controller redirects back to the original page
6. On reload:
   - **Server side:** `RequestLocalizationMiddleware` reads the cookie → sets `CultureInfo.CurrentCulture`
   - **WASM side:** `Program.cs` reads `localStorage` → sets `CultureInfo.DefaultThreadCurrentCulture`

### Number / Date / Time Formatting

Blazor automatically uses `CultureInfo.CurrentCulture` for `@bind` formatting. No extra configuration needed — dates, numbers, and currencies will render according to the active culture.

---

## Contributing Translations

### Adding a New Language

1. **Create the `.resx` file:**
   - Copy `SharedResources.resx` → `SharedResources.{tag}.resx` (e.g. `SharedResources.fr.resx`)
   - Translate all `<data>` values
   - Keep `name` attributes identical to the English file

2. **Register the culture:**
   - Add the tag to `SupportedCultures.All` in `SupportedCultures.cs`
   - Add a display name to `SupportedCultures.DisplayNames`

3. **Test:**
   - Build the solution
   - Switch culture in the UI and verify strings render correctly

### Translation Guidelines

- Keep translations concise (UI space is limited)
- Preserve `{0}`, `{1}` placeholders in format strings
- Use the formal register for user-facing text
- Do not translate brand names (`DotNetCloud`)

### Future: Weblate Integration

A self-hosted [Weblate](https://weblate.org/) instance may be set up for community translations. The `.resx` files will be synced via Git integration. See project roadmap for timeline.

---

## Configuration Reference

### Server (`Program.cs`)

```csharp
builder.Services.AddLocalization();

// In pipeline:
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(SupportedCultures.DefaultCulture)
    .AddSupportedCultures(SupportedCultures.All)
    .AddSupportedUICultures(SupportedCultures.All);
app.UseRequestLocalization(localizationOptions);
```

### WASM Client (`Program.cs`)

```csharp
builder.Services.AddLocalization();

// Read from localStorage, set thread culture
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
```

### WASM Project File

```xml
<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>
```

This ensures the WebAssembly runtime loads full ICU globalization data for all supported cultures.
