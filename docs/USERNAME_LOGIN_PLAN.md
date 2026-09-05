# Username-Based Login — Implementation Plan

> **Status:** Ready for implementation
> **Branch:** `feature/convert-to-username-login`
> **Target:** Switch DotNetCloud sign-in from email to a distinct **username** (`Bill.Jones`, `pat123`, `danh`), while still collecting email as an **optional** field. Password stays required.
> **Key insight:** ASP.NET Core Identity already has a `UserName` column on `AspNetUsers`. Today the app sets `UserName = Email` everywhere. We simply stop deriving `UserName` from `Email`, authenticate with `UserName`, make `Email` nullable, and backfill existing accounts. **No schema migration is required** (both columns already exist).

---

## 1. Locked Decisions (do not re-litigate)

1. **Email uniqueness:** keep `RequireUniqueEmail = true`. Empty/whitespace email is stored as `null`, so any number of users may have no email. Non-empty emails must remain unique.
2. **Existing accounts:** one-time idempotent backfill — for any user whose `UserName` contains `@`, set `UserName` to the sanitized email local-part (e.g. `admin@example.com` → `admin`) and keep `Email` unchanged. Collisions (two `admin@…`, or an existing `admin`) are resolved by appending a numeric suffix (`admin2`, `admin3`, …).
3. **Forgot password:** accept **username or email**. If the matched account has no email, do not send a reset link — surface "contact your administrator".
4. **Username characters:** restrict `AllowedUserNameCharacters` to `a-z A-Z 0-9 - . _` (drop `@` and `+` so usernames can never look like email addresses).
5. **Usernames are immutable** after creation (no UI/DTO path edits them; only the legacy backfill writes `UserName`).

---

## 2. How auth flows work today (context for the implementer)

- **Web login (cookies):** `Login.razor` (`/auth/login`) posts a form to `AuthSessionController.LoginAsync` → `SignInManager.PasswordSignInAsync(email, …)`.
- **API login (legacy/JSON):** `AuthController.LoginAsync` (`POST /api/v1/core/auth/login`) → `IAuthService.LoginAsync` → `UserManager.FindByEmailAsync`.
- **OAuth2 clients (desktop/Android):** open a browser to `/connect/authorize` (OpenIddict) → challenged to the cookie login page `/auth/login` → after login, `/connect/token` issues tokens. `OpenIddictEndpointsExtensions` sets `preferred_username = user.UserName` and `email = user.Email`.
- **Registration:** `AuthService.RegisterAsync` and `Register.razor` both create `ApplicationUser` with `UserName = Email`.
- **Admin seed:** `AdminSeeder` creates the initial admin with `UserName = email`, `Email = email`.
- **Password reset:** `AuthService.InitiatePasswordResetAsync`/`ResetPasswordAsync`, `ForgotPassword.razor`, `ResetPassword.razor` all key off email.

---

## 3. Implementation order (dependencies)

1. DTOs (Phase 1)
2. Identity config (Phase 2)
3. `AuthService` (Phase 3)
4. Controllers + OpenIddict + `AdminSeeder` + `UserManagementService` (Phase 4)
5. Legacy username backfill (Phase 5) — must run **before** `AdminSeeder` at startup
6. Web UI (Phase 6)
7. CLI + installers (Phase 7)
8. Clients (Phase 8)
9. Tests (Phase 9)
10. Docs + tracking (Phase 10)

Phases 1 and 2 are independent and can be done in either order. Phases 3–5 depend on 1–2. Phases 6–8 depend on the DTO/controller contracts. Phases 9–10 last.

---

## Phase 1 — Domain DTOs

All in `src/Core/DotNetCloud.Core/DTOs/`.

### 1.1 `AuthDtos.cs`

**`LoginRequest`** — replace the `Email` property with `Username`. The class keeps `Password` and `TotpCode`. Final shape:

```csharp
public sealed class LoginRequest
{
    /// <summary>
    /// Gets or sets the user's username (used for login).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TOTP code from the user's authenticator app, if MFA is enabled.
    /// </summary>
    public string? TotpCode { get; set; }
}
```

**`RegisterRequest`** — add `Username` (required) and change `Email` to `string?`:

```csharp
public sealed class RegisterRequest
{
    /// <summary>
    /// Gets or sets the user's username (used for login).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address (optional).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the plaintext password (hashed before storage).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's preferred display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's preferred locale (e.g., "en-US"). Defaults to "en-US".
    /// </summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the user's preferred timezone (e.g., "UTC"). Defaults to "UTC".
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets a value indicating whether the user must change their password on first login.
    /// </summary>
    public bool PasswordChangeRequired { get; set; }
}
```

**`RegisterResponse`** — change `Email` to `string?`:

```csharp
public sealed class RegisterResponse
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the registered email address (null when none was provided).
    /// </summary>
    public string? Email { get; set; }

    public bool RequiresEmailConfirmation { get; set; }
}
```

**`ResetPasswordRequest`** — replace `Email` with `Username`:

```csharp
public sealed class ResetPasswordRequest
{
    /// <summary>
    /// Gets or sets the username of the account to reset.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password reset token received via email.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new password to set.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}
```

**`UserProfileResponse`** — add a `Username` property (immediately after `UserId`) and change `Email` to `string?`:

```csharp
public sealed class UserProfileResponse
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user's username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string? Email { get; set; }
    // ...remaining properties unchanged...
}
```

### 1.2 `UserDtos.cs`

**`UserDto`** — add `Username` after `Id`; change `Email` to `string?`:

```csharp
public class UserDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user's username.
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's email address (null when none is set).
    /// </summary>
    public string? Email { get; set; }
    // ...remaining properties unchanged...
}
```

**`CreateUserDto`** — add `Username` (required) after `Id`-adjacent fields; change `Email` to `string?`. (Note: this DTO may be unused scaffolding; update it anyway for consistency.)

```csharp
public class CreateUserDto
{
    /// <summary>
    /// Gets or sets the user's username (required).
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's email address (optional).
    /// </summary>
    public string? Email { get; set; }
    // ...Password, DisplayName, Locale, Timezone, Roles unchanged...
}
```

**`UpdateUserDto`** — add an optional `Email` with these semantics: `null` = leave unchanged, empty/whitespace string = clear the email, non-empty = set (validated unique). Keep all other properties.

```csharp
public class UpdateUserDto
{
    /// <summary>
    /// Gets or sets the user's email address.
    /// <c>null</c> = no change; empty string = clear email; otherwise sets the email (must be unique when non-empty).
    /// </summary>
    public string? Email { get; set; }
    // ...DisplayName, AvatarUrl, Locale, Timezone, IsActive unchanged...
}
```

**`UserSearchResultDto`** — add `Username` and change `Email` to `string?` (nullable).

---

## Phase 2 — Identity configuration

File: `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs`

Inside `services.AddIdentity<ApplicationUser, ApplicationRole>(options => { ... })`, after the existing `options.User.RequireUniqueEmail = true;` and `options.SignIn.RequireConfirmedEmail = false;` lines, add:

```csharp
// Usernames may contain letters, digits, '-', '.', '_' only.
// '@' and '+' are excluded so a username can never look like an email address.
options.User.AllowedUserNameCharacters =
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
```

Keep `RequireUniqueEmail = true` exactly as-is (empty emails will be stored as `null`, which Identity treats as "no email" and skips uniqueness checks).

---

## Phase 3 — `AuthService`

File: `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs`

### 3.1 `RegisterAsync`

Replace the `ApplicationUser` creation block. Current code is:

```csharp
var user = new ApplicationUser
{
    UserName = request.Email,
    Email = request.Email,
    DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
        ? request.Email.Split('@')[0]
        : request.DisplayName,
    ...
};
```

New code:

```csharp
var user = new ApplicationUser
{
    UserName = request.Username,
    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
    DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
        ? request.Username
        : request.DisplayName,
    Locale = request.Locale,
    Timezone = request.Timezone,
    IsDemoUser = isDemoUser,
};
```

Later in the method, the email-confirmation block already checks `user.Email!` — change the send calls to guard on a non-null email. Wrap the `requiresEmailConfirmation` block so it only executes when `user.Email` is non-null:

```csharp
if (requiresEmailConfirmation && user.Email is not null)
{
    // ... existing confirmation-token + send-email logic unchanged ...
}
```

Also change the log line and `RegisterResponse`:

```csharp
_logger.LogInformation(
    "User {UserId} registered with username {Username} (DemoUser={IsDemoUser})",
    user.Id, user.UserName, isDemoUser);

return new RegisterResponse
{
    UserId = user.Id,
    Email = user.Email,
    RequiresEmailConfirmation = requiresEmailConfirmation && user.Email is not null,
};
```

### 3.2 `LoginAsync`

Replace the first lookup:

```csharp
var user = await _userManager.FindByEmailAsync(request.Email);
if (user is null)
{
    _logger.LogWarning("Login failed: user not found for username {Username}", LogSanitizer.Sanitize(request.Username));
    throw new UnauthorizedAccessException("Invalid credentials.");
}
```

All subsequent log messages in this method that reference `request.Email` should reference `request.Username`. No other logic changes (password check, lockout, MFA, `PasswordChangeRequired`, `LastLoginAt` all stay).

### 3.3 `InitiatePasswordResetAsync`

Change signature to accept **username or email** and look up by username first, then email:

```csharp
public async Task InitiatePasswordResetAsync(string usernameOrEmail)
{
    var user = await _userManager.FindByNameAsync(usernameOrEmail)
        ?? await _userManager.FindByEmailAsync(usernameOrEmail);

    if (user is null)
    {
        _logger.LogInformation("Password reset requested for unknown account {Identifier}", usernameOrEmail);
        return;
    }

    if (string.IsNullOrEmpty(user.Email))
    {
        _logger.LogInformation("Password reset requested for account {UserId} with no email on file", user.Id);
        return;
    }

    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
    var encodedToken = HttpUtility.UrlEncode(token);
    var encodedUsername = HttpUtility.UrlEncode(user.UserName!);
    var resetUrl = $"{_smtpOptions.BaseUrl.TrimEnd('/')}/auth/reset-password?username={encodedUsername}&token={encodedToken}";
    // ... email body uses user.Email, user.DisplayName ?? user.UserName ...
}
```

Update the `IAuthService` interface signature accordingly in `src/Core/DotNetCloud.Core/Services/IAuthService.cs` (parameter renamed from `email` to `usernameOrEmail`, XML doc updated). Check the caller in `AuthController` (see Phase 4) to pass a single string.

### 3.4 `ResetPasswordAsync`

Change lookup to username:

```csharp
var user = await _userManager.FindByNameAsync(request.Username);
if (user is null) return false;

var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
// ...unchanged...
```

### 3.5 `GetUserProfileAsync`

Add `Username = user.UserName ?? string.Empty` to the returned `UserProfileResponse`.

### 3.6 `MfaService.GetTotpSetupAsync` (optional but recommended)

File: `src/Core/DotNetCloud.Core.Auth/Services/MfaService.cs`, line ~51. Change the QR label fallback to prefer username:

```csharp
var accountLabel = user.UserName ?? user.Email ?? userId.ToString();
var qrCodeUri = GenerateQrCodeUri(accountLabel, key!);
```

---

## Phase 4 — Server controllers, OpenIddict, AdminSeeder, UserManagement

### 4.1 `AuthController.cs`

File: `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`

- `RegisterAsync`: log `request.Username` instead of `request.Email`.
- `LoginAsync`: log `request.Username`; in the `UnauthorizedAccessException` catch, use `LogSanitizer.Sanitize(request.Username)` and message `"Invalid username or password"`; `MFA_REQUIRED` / `PASSWORD_CHANGE_REQUIRED` catch blocks log `request.Username`.
- Any `InitiatePasswordResetAsync`/`ResetPasswordAsync` callers in this controller: pass the new username/identifier field (check whether these exist here; if they live elsewhere, update those call sites too). The `ResetPasswordAsync` body must now map the request's username field.

### 4.2 `AuthSessionController.cs`

File: `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs`

**`LoginAsync`** — rename the form parameter and all logic:

```csharp
[HttpPost("login")]
[AllowAnonymous]
public async Task<IActionResult> LoginAsync(
    [FromForm] string username,
    [FromForm] string password,
    [FromForm] string? returnUrl = null)
{
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        _logger.LogWarning("Form login rejected: missing username or password");
        return RedirectToLogin("Username and password are required.", returnUrl, username);
    }

    try
    {
        var result = await _signInManager.PasswordSignInAsync(
            username,
            password,
            isPersistent: true,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await UpdateLastLoginAsync(username);

            var user = await _userManager.FindByNameAsync(username);
            // ... audit log, PasswordChangeRequired check, MFA redirect all unchanged
            //     except FindByEmailAsync → FindByNameAsync ...
            var target = await ResolvePostLoginTargetAsync(username, returnUrl);
            return LocalRedirect(target);
        }
        // ... RequiresTwoFactor / IsLockedOut / IsNotAllowed branches unchanged
        //     but all RedirectToLogin(..., username) and LogLoginFailureAsync(username, ...) ...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Form login failed for {Username}", LogSanitizer.Sanitize(username));
        await LogLoginFailureAsync(username, "exception");
        return RedirectToLogin($"Login error: {ex.GetType().Name}", returnUrl, username);
    }
}
```

Change the error string `"Invalid email or password."` to `"Invalid username or password."`.

**Helpers** — rename parameter `email` → `username` and switch lookups:

- `RedirectToLogin(string error, string? returnUrl, string username)` — encode `username` into the `username=` query param (currently `email=`).
- `ResolvePostLoginTargetAsync(string username, string? returnUrl)` — `FindByNameAsync(username)`.
- `LogLoginFailureAsync(string username, string reason)` — `FindByNameAsync(username)`, audit description `$"form-login-failed:{reason}:{LogSanitizer.Sanitize(username)}"`.
- `UpdateLastLoginAsync(string username)` — replace the `NormalizedEmail` filter with `NormalizedUserName`:

```csharp
private async Task UpdateLastLoginAsync(string username)
{
    var now = DateTime.UtcNow;
    var normalizedUsername = _userManager.NormalizeName(username);
    var updated = await _userManager.Users
        .Where(u => u.NormalizedUserName == normalizedUsername)
        .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, now));
    if (updated == 0)
        _logger.LogWarning("Could not persist LastLoginAt: user not found");
}
```

Note: `PasswordSignInAsync` already looks users up by username, so passing `username` directly is correct.

### 4.3 `OpenIddictEndpointsExtensions.cs`

File: `src/Core/DotNetCloud.Core.Server/Extensions/OpenIddictEndpointsExtensions.cs`

In `HandleAuthorizeEndpoint`, guard the email claim:

```csharp
identity.SetClaim(Claims.Name, user.DisplayName);
identity.SetClaim(Claims.PreferredUsername, user.UserName);
if (!string.IsNullOrEmpty(user.Email))
{
    identity.SetClaim(Claims.Email, user.Email);
}
```

`HandleUserInfoEndpoint` already returns `preferred_username = user.UserName` and `email = user.Email` (null-safe as JSON null) — leave as-is, but change `email_verified = user.EmailConfirmed` to `email_verified = user.EmailConfirmed && !string.IsNullOrEmpty(user.Email)` for correctness.

### 4.4 `AdminSeeder.cs`

File: `src/Core/DotNetCloud.Core.Server/Initialization/AdminSeeder.cs`

Replace the beginning of `SeedAsync`:

```csharp
var username = GetConfigValue("DotNetCloud:AdminUsername", "adminUsername");
var email = GetConfigValue("DotNetCloud:AdminEmail", "adminEmail");

// Legacy installs only set AdminEmail; derive a username from the email local-part
// to match the LegacyUsernameMigration backfill.
if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(email))
{
    username = email.Split('@')[0];
}
```

**Existing-install branch** — currently `FindByEmailAsync(email)`. Change to look up by username first, then email fallback:

```csharp
if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(email))
{
    var existingAdmin = string.IsNullOrWhiteSpace(username)
        ? null
        : await _userManager.FindByNameAsync(username)
            ?? (string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email));
    if (existingAdmin is not null)
    {
        // ... existing role-assignment + AdminMfaSetting logic unchanged ...
    }
}
```

(If `username` is null but `email` is set, look up by email directly.)

**Create branch** — build the user with the username and nullable email:

```csharp
var user = new ApplicationUser
{
    UserName = username!,
    Email = string.IsNullOrWhiteSpace(email) ? null : email,
    DisplayName = "Administrator",
    EmailConfirmed = true,
    IsActive = true
};
```

The `userCount == 0` + password check guard should be `string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)` → skip.

Update the XML doc comment at the top of the class to mention `DotNetCloud:AdminUsername` and `DotNetCloud:AdminEmail`.

### 4.5 `UserManagementService.cs`

File: `src/Core/DotNetCloud.Core.Auth/Services/UserManagementService.cs`

- `ListUsersAsync` search: also search username. Change:

```csharp
usersQuery = usersQuery.Where(u =>
    u.UserName!.Contains(search) ||
    u.Email!.Contains(search) ||
    u.DisplayName.Contains(search));
```

- Default sort (`_ =>` branch): change `u.Email` → `u.UserName`.
- `MapToDto`: add `Username = user.UserName!`, and change `Email = user.Email!` → `Email = user.Email` (nullable).
- `UpdateUserAsync`: handle `dto.Email`:

```csharp
if (dto.Email is not null)
{
    user.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
    user.NormalizedEmail = _userManager.NormalizeEmail(user.Email);
}
```

> ⚠️ `UserManager.UpdateAsync` does **not** re-normalize email; always update `NormalizedEmail` together with `Email`, or uniqueness/lookup will break.

### 4.6 `UserManagementController.cs`

File: `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs`

In `UpdateUserAsync`, non-admin users must not be able to set/clear email. Add `dto.Email = null;` in the non-admin guard block (alongside the existing `dto.IsActive = null;`).

---

## Phase 5 — Legacy username backfill (existing accounts)

### 5.1 New file: `src/Core/DotNetCloud.Core.Server/Initialization/LegacyUsernameMigration.cs`

Create this class (scoped service). It directly edits `UserName`/`NormalizedUserName` via `CoreDbContext` (no `UserManager` needed; this is a one-time data transformation, and `UserManager.SetUserNameAsync` would re-run validators unnecessarily).

```csharp
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// One-time, idempotent backfill that rewrites legacy accounts whose
/// <c>UserName</c> was set to their email address into a distinct username
/// derived from the email local-part. Collisions are resolved by appending
/// a numeric suffix.
/// </summary>
internal sealed class LegacyUsernameMigration
{
    private const string AllowedChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";

    private readonly CoreDbContext _dbContext;
    private readonly ILogger<LegacyUsernameMigration> _logger;

    public LegacyUsernameMigration(CoreDbContext dbContext, ILogger<LegacyUsernameMigration> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Runs the migration. Safe to call on every startup: it is a no-op once
    /// no users have an <c>@</c> in <c>UserName</c>.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var legacyUsers = await _dbContext.Users
            .Where(u => u.UserName != null && u.UserName.Contains("@"))
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);

        if (legacyUsers.Count == 0)
        {
            return;
        }

        // Seed the taken set with all existing usernames that are NOT legacy
        // (case-insensitive, matching Identity's normalization).
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allUsers = await _dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var u in allUsers)
        {
            if (!string.IsNullOrWhiteSpace(u.UserName) && !u.UserName.Contains("@"))
            {
                taken.Add(u.UserName);
            }
        }

        foreach (var user in legacyUsers)
        {
            var oldUserName = user.UserName;
            var candidate = DeriveBaseUsername(user.Email ?? oldUserName!);

            // Resolve collisions against taken + already-assigned candidates.
            var final = candidate;
            var suffix = 2;
            while (taken.Contains(final))
            {
                final = $"{candidate}{suffix++}";
            }

            taken.Add(final);
            user.UserName = final;
            user.NormalizedUserName = final.ToUpperInvariant();

            _logger.LogInformation(
                "Migrated legacy username {OldUserName} -> {NewUserName} (user {UserId})",
                oldUserName ?? string.Empty,
                final,
                user.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string DeriveBaseUsername(string emailOrUsername)
    {
        var localPart = emailOrUsername.Split('@')[0];
        var sanitized = new string(localPart.Where(AllowedChars.Contains).ToArray());
        if (sanitized.Length == 0)
        {
            sanitized = "user";
        }
        return sanitized;
    }
}
```

> Note: the migration logs the old username directly. This is an internal startup log; if PII masking is desired, wrap `oldUserName` with `LogSanitizer` (from `DotNetCloud.Core.Services`) where available — otherwise plain interpolation is acceptable.

### 5.2 Register the service

File: `src/Core/DotNetCloud.Core.Server/Program.cs` (near lines 734–735 where `AdminSeeder`/`OidcClientSeeder` are registered):

```csharp
builder.Services.AddScoped<LegacyUsernameMigration>();
```

### 5.3 Invoke it at startup (BEFORE `AdminSeeder`)

File: `src/Core/DotNetCloud.Core.Server/Program.cs`, method `InitializeDatabaseAsync`, in the `try` block, after `dbInitializer.InitializeAsync()` and **before** `adminSeeder.SeedAsync()`:

```csharp
var usernameMigration = scope.ServiceProvider.GetRequiredService<LegacyUsernameMigration>();
await usernameMigration.MigrateAsync();
```

Ordering matters: the migration must run before `AdminSeeder` so the admin's migrated username can be found by `FindByNameAsync`.

---

## Phase 6 — Web UI

### 6.1 `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Login.razor`

- Rename the form field from `email` to `username` (input `id="username"`, `name="username"`, `type="text"`, `autocomplete="username"`, `placeholder="Bill.Jones"`).
- Change the label text from `Email` to `Username`.
- In `@code`, rename the `Email` query param to `Username` (it is the value pre-populated on redirect after a failed login).
- Update the heading copy if it says "Sign in with email".

### 6.2 `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Register.razor`

- Add a required **Username** field above the Email field.
- Change the Email field label to "Email (optional)" and remove the `required`-style validation (email is optional now).
- In `RegisterAsync`, build the user:

```csharp
var user = new ApplicationUser
{
    UserName = _model.Username,
    Email = string.IsNullOrWhiteSpace(_model.Email) ? null : _model.Email,
    DisplayName = _model.DisplayName,
    Locale = !string.IsNullOrWhiteSpace(_model.Locale) ? _model.Locale : "en-US",
    Timezone = !string.IsNullOrWhiteSpace(_model.Timezone) ? _model.Timezone : "UTC",
    CreatedAt = DateTime.UtcNow,
    IsActive = true,
    IsDemoUser = isDemoUser
};
```

- In the nested `RegisterFormModel`, add `public string Username { get; set; } = string.Empty;` and change `Email` to `string?` (or keep string but treat blank as null — implementer's choice; blank → null is the key behavior).

### 6.3 `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/ForgotPassword.razor`

- Change the single input to "Username or email" (`id="identifier"`, `name` bound via `EditForm` model property `Identifier`).
- In `SubmitAsync`:

```csharp
var user = await UserManager.FindByNameAsync(_model.Identifier)
    ?? await UserManager.FindByEmailAsync(_model.Identifier);

if (user is not null && !string.IsNullOrEmpty(user.Email))
{
    var token = await UserManager.GeneratePasswordResetTokenAsync(user);
    Logger.LogInformation("Password reset token generated for {Identifier}", _model.Identifier);
}
else if (user is not null && string.IsNullOrEmpty(user.Email))
{
    _noEmailOnFile = true; // display "Contact your administrator" message
}
_submitted = true;
```

- Add a `_noEmailOnFile` flag and render a distinct message: "This account has no email address on file. Please contact your administrator to reset your password." Keep the generic "if an account exists…" message for the unknown-account case.

### 6.4 `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/ResetPassword.razor`

- Change the query param and model field from `Email` to `Username`.
- The reset link generated in `AuthService.InitiatePasswordResetAsync` now sends `?username=…&token=…` (see Phase 3.3), so `OnInitialized` reads `Username` instead of `Email`.
- In `SubmitAsync`, look up with `UserManager.FindByNameAsync(_model.Username)`.
- Change the form label from "Email" to "Username".

### 6.5 Admin / profile pages (null-safe email + show username)

- `src/UI/DotNetCloud.UI.Web.Client/Pages/Profile.razor`: show `_profile.Username` (readonly) in addition to email; change "Email cannot be changed" hint if needed. Profile name display around line 268 (`name = _profile.Email`) → use `_profile.Username` or `DisplayName`.
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/UserCreate.razor`: add a required **Username** field above Email; email becomes optional. The `_model` is a `RegisterRequest`, which now has `Username` + nullable `Email`.
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/UserList.razor`: add/display the `Username` column (replace or augment the Email column).
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/UserDetail.razor`: show `Username`; email may be blank.
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Groups.razor` and `Organizations.razor`: these render `user.Email` / `member.Email`. Switch to show username primarily and guard blank email (render `—` when null/empty).

### 6.6 `src/UI/DotNetCloud.UI.Web.Client/Services/DotNetCloudApiClient.cs`

`CreateUserAsync(RegisterRequest)` posts to `api/v1/core/auth/register` — no URL change. The `RegisterRequest` now carries `Username`; verify `GetUserAsync`/`UpdateUserAsync` still compile with `UserDto.Email` now nullable (adjust any non-null usage to `?? string.Empty`).

---

## Phase 7 — CLI & installers

### 7.1 `src/CLI/DotNetCloud.CLI/Infrastructure/CliConfiguration.cs`

Add after `AdminEmail`:

```csharp
/// <summary>
/// The admin username created during setup (used for login).
/// </summary>
public string? AdminUsername { get; set; }
```

### 7.2 `src/CLI/DotNetCloud.CLI/Commands/SetupCommand.cs`

- Around lines 293–301 (the "Admin User Configuration" step), replace the email prompt with two prompts:

```csharp
ConsoleOutput.WriteInfo("Choose a username for your admin account. This is what you will use to sign in.");
ConsoleOutput.WriteInfo("Allowed characters: letters, digits, '-', '.', '_' (e.g. Bill.Jones, pat123).");
config.AdminUsername = ConsoleOutput.Prompt("Admin username", config.AdminUsername);

ConsoleOutput.WriteInfo("Enter the admin email address (optional). Leave blank if the account has none.");
config.AdminEmail = ConsoleOutput.Prompt("Admin email (optional)", config.AdminEmail ?? "");
if (string.IsNullOrWhiteSpace(config.AdminEmail))
{
    config.AdminEmail = null;
}
```

- Summary output (~line 655): change `WriteDetail("Admin Login", config.AdminEmail ?? "(not set)")` to `WriteDetail("Admin Username", config.AdminUsername ?? "(not set)")`, and optionally add a separate `WriteDetail("Admin Email", config.AdminEmail ?? "(none)")`.
- Final "Setup Complete" (~line 853): `ConsoleOutput.WriteInfo($"Login username: {config.AdminEmail}")` → `$"Login username: {config.AdminUsername}"`.
- Beginner-mode completion summary (~lines 1322, 1332, 1339): replace `sign in with {config.AdminEmail}` with `sign in with {config.AdminUsername}`.

### 7.3 `src/CLI/DotNetCloud.CLI/Commands/ServiceCommands.cs`

Around line 115, add the username env var next to `DotNetCloud__AdminEmail`:

```csharp
["DotNetCloud__AdminEmail"] = config.AdminEmail ?? "",
["DotNetCloud__AdminUsername"] = config.AdminUsername ?? ""
```

### 7.4 `src/Core/DotNetCloud.Core.Server/appsettings.Development.json`

In the `"DotNetCloud"` section, add `"AdminUsername": ""` next to `"AdminEmail": ""`.

### 7.5 Installer scripts (best-effort; keep AdminEmail fallback working)

- `tools/install-windows.ps1`: where it prompts for and writes `AdminEmail`, add an `AdminUsername` prompt and write `$config["DotNetCloud"]["AdminUsername"] = $Script:AdminUsername`. Update the final "Admin account" summary line to show the username.
- `tools/install.sh`: where it reads/writes `admin_email` (around lines 1320–1336), also read/write an `admin_username` and add `DotNetCloud__AdminUsername=${admin_username}` to the env file. If no username is available, the `AdminSeeder` derives one from `AdminEmail` automatically, so this is a soft requirement.

---

## Phase 8 — Client compatibility (SyncTray + Android) — MUST NOT break

### 8.0 Why the clients keep working (read first)

Both desktop and Android clients authenticate with OAuth2 **authorization code + PKCE**, not a username/password grant:

- They open a system browser to `/connect/authorize`, which redirects to the web login form (`/auth/login`). The **only** place credentials are entered is that form — which Phase 6 changes from email to username.
- The token exchange sends `grant_type=authorization_code` (or `grant_type=refresh_token`) plus `code`/`refresh_token`/`client_id`. It never sends a username or email.
- So the OAuth2 endpoints (`/connect/authorize`, `/connect/token`, `/connect/userinfo`) and the client wire flow are **untouched**. Authentication keeps working with zero client protocol changes.

The only real risk is the **`email` OIDC claim becoming absent** for users who have no email. That is handled by the Phase 4.3 guard (skip `Claims.Email` when `user.Email` is null). The authoritative identity claim remains `preferred_username = user.UserName`.

### 8.1 Required Android change — claim priority (email may now be absent)

`src/Clients/DotNetCloud.Client.Android/ViewModels/LoginViewModel.cs` (~line 102) currently prefers `email` then `preferred_username`. Swap so the username wins:

```csharp
var email = ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "preferred_username")
            ?? ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "email")
            ?? ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "name")
            ?? new Uri(normalizedUrl).Host;
```

- `ExtractClaimFromToken` already returns `null` for a missing claim, so the fallback chain is safe.
- `ServerConnection.AccountEmail` (in `src/Clients/DotNetCloud.Client.Android/Services/IServerConnectionStore.cs`) is a display-only label; after this change it will hold the username. No functional impact. Do **not** change the record's shape or its persisted JSON — that would break stored connections.
- The Android auth URL (`MauiOAuth2Service.BuildAuthUrl`) still requests the `email` scope — **keep it**. Users who do have an email still receive it; the server simply omits the claim when email is null.

### 8.2 SyncTray — no code change required (verify only)

`src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs` is already safe:

- `BuildDisplayName` falls back `name` → `preferred_username` → `email` → `"user"`. The display name always comes from `name` (= `user.DisplayName`, always non-null), so account labels are unaffected.
- `UserProfileInfo.Email` is `string?` and deserializes a null/missing `email` cleanly.
- SyncTray's scope list is `openid profile offline_access files:read files:write` — it does **not** request `email`, so its userinfo already tolerates an absent email today.

Do **not** modify SyncTray auth code.

### 8.3 Server-side claim guard (already Phase 4.3 — do not skip)

`OpenIddictEndpointsExtensions.HandleAuthorizeEndpoint` must not write a null `email` claim:

```csharp
identity.SetClaim(Claims.Name, user.DisplayName);
identity.SetClaim(Claims.PreferredUsername, user.UserName);
if (!string.IsNullOrEmpty(user.Email))
{
    identity.SetClaim(Claims.Email, user.Email);
}
```

Also update `HandleUserInfoEndpoint` so `email_verified` is only true when an email actually exists:

```csharp
email_verified = user.EmailConfirmed && !string.IsNullOrEmpty(user.Email)
```

### 8.4 Client compatibility verification (add these to the final checklist)

- **Android, no-email account:** log in against a server where the account has no email → login succeeds, the app stores/shows the username, token refresh and API calls succeed.
- **Android, with-email account:** unchanged behavior.
- **SyncTray, no-email account:** add an account against a server where the account has no email → account label shows `<DisplayName> @ <host>`, sync starts and files sync, reconnect flow works.
- **SyncTray, pre-existing account:** after a server upgrade, an account added before the change continues to sync (token refresh via `grant_type=refresh_token` is unaffected).
- **Both:** `/connect/userinfo` returns `preferred_username` and omits/null `email` for a no-email user; the `sub` claim is always present so `UserId` resolution still works.

---

## Phase 9 — Tests

> Repo rules: test method naming `MethodName_Condition_ExpectedResult`; always run `dotnet test` after changes; never commit without updated tests.

### 9.1 Unit test fixtures to update

- `tests/DotNetCloud.Core.Auth.Tests/Services/AuthServiceTests.cs`
  - Every `new LoginRequest { Email = "…" }` → `new LoginRequest { Username = "…" }`.
  - `RegisterRequest` constructions add `Username` (e.g. `"testuser"`) and keep `Email` optional.
  - Mock setups for `FindByEmailAsync(...)` in login tests → `FindByNameAsync(...)`.
  - `RegisterAsync_ValidRequest_ReturnsResponseWithUserId`: assert `response.Username`? — `RegisterResponse` has no Username; instead assert `response.Email == request.Email` (still valid) and add an assertion that the created `ApplicationUser.UserName == request.Username` via the `CreateAsync` callback capture.
- `tests/DotNetCloud.Core.Auth.Tests/Services/UserManagementServiceTests.cs`
  - `TestUser` static: change `UserName = "user@example.com"` → `UserName = "testuser"`, keep `Email = "user@example.com"`.
  - Any `MapToDto` assertions: expect `Username` populated.
- `tests/DotNetCloud.Core.Server.Tests/Controllers/AuthSessionControllerTests.cs`
  - Login tests: `PasswordSignInAsync("kaminskidale@gmail.com", …)` → a username like `"kaminskidale"`; `LoginAsync("kaminskidale", …)`.
  - `VerifyLoginFailureAudited("invalid-credentials", "kaminskidale")` (username).
  - `AssertRedirectToLogin` query-param check: `username` not `email`.
- `tests/DotNetCloud.Core.Server.Tests/Initialization/AdminSeederTests.cs`
  - Add `["DotNetCloud:AdminUsername"] = "admin"` to config dicts; update the create assertion to expect `u.UserName == "admin"` and `u.Email == "admin@test.com"`.
  - Add a legacy-only test: `AdminEmail` set, no `AdminUsername` → seeder derives `admin`.
  - Add a test: `AdminUsername` set, `AdminEmail` null → creates admin with null email.
- `tests/DotNetCloud.Core.Server.Tests/Controllers/UserManagementControllerTests.cs`
  - The local `CreateUserDto(...)` helper builds a `UserDto`; add `Username` and keep `Email` (nullable). Update assertions if they check `Email`.
- `tests/DotNetCloud.CLI.Tests/Infrastructure/CliConfigTests.cs`
  - Add `AdminUsername` round-trip test alongside the existing `AdminEmail` tests (serialization/deserialization + null default).

### 9.2 Integration test fixtures

- `tests/DotNetCloud.Integration.Tests/Builders/ApplicationUserBuilder.cs`
  - Add `WithUsername(string)` and a `_username` field defaulting to a unique value (e.g. `$"user-{Guid.CreateVersion7():N}"`). In `Build()`, set `UserName = _username` (not `_email`), `NormalizedUserName = _username.ToUpperInvariant()`, keep `Email = _email`.
  - `CreateAdmin`: `WithUsername("admin")` and keep email default.
- `tests/DotNetCloud.Integration.Tests/Builders/RegisterRequestBuilder.cs`
  - Add `_username` + `WithUsername(...)`, default to a unique value; include `Username` in `Build()`.
- `tests/DotNetCloud.Integration.Tests/Api/AuthEndpointTests.cs`, `ClosedSystemIntegrationTests.cs`, `MfaEndpointTests.cs`, `UserManagementEndpointTests.cs`, `GroupsEndpointIntegrationTests.cs`
  - Registration: add `Username` to register payloads; login payloads use `Username` instead of `Email`; update assertions to login by username.
  - Seed users via `ApplicationUserBuilder` now have a real username.

### 9.3 New tests to add

1. **AuthService login:** `LoginAsync_ValidUsername_ReturnsUserId` and `LoginAsync_UnknownUsername_ThrowsUnauthorized`.
2. **AuthService registration:** `RegisterAsync_EmailOmitted_StoresNullEmail` and `RegisterAsync_EmailProvided_StoresEmail`.
3. **Username validation:** a test (or integration test) verifying `UserManager.CreateAsync` rejects a username containing `@` or `+` (driven by `AllowedUserNameCharacters`). This is best as a focused unit test on Identity options or an integration test against a real in-memory Identity setup — use the existing integration harness.
4. **LegacyUsernameMigration unit tests** (new file `tests/DotNetCloud.Core.Server.Tests/Initialization/LegacyUsernameMigrationTests.cs`):
   - `MigrateAsync_UsersWithEmailUsername_RewritesToLocalPart`
   - `MigrateAsync_DuplicateLocalParts_AppendsSuffix`
   - `MigrateAsync_NoLegacyUsers_IsNoOp` (idempotency: run twice, second run changes nothing)
   - `MigrateAsync_PreservesEmail`
     Use the InMemory EF provider (`UseInMemoryDatabase`) with a fresh `CoreDbContext`.
5. **Forgot password:** unit test for `InitiatePasswordResetAsync` with a no-email user → no email send and no token generated (verify `GeneratePasswordResetTokenAsync` never called).

---

## Phase 10 — Docs & tracking (repo requirement)

Per `CLAUDE.md`/`copilot-instructions.md`, after implementation:

1. `docs/api/AUTHENTICATION.md` — update the registration/login sections: login uses `username`, email optional; update the ASCII flow diagrams and validation rules; note the forgot-password username-or-email behavior.
2. `docs/IMPLEMENTATION_CHECKLIST.md` — mark completed auth items `✓`, pending `☐` using **targeted edits** only.
3. `docs/MASTER_PROJECT_PLAN.md` — update the Quick Status Summary table + the relevant step's `Status`/`Deliverables`/`Notes` using **targeted edits**.

Use visual checkmark characters (`✓` / `☐`), never `[x]`/`[ ]`.

---

## Verification checklist (do all before committing)

```bash
dotnet build
dotnet test
```

1. Builds clean (warnings are errors in this repo).
2. All unit + integration tests pass.
3. Manual web checks:
   - Register a user with username only (no email) → sign in with username.
   - Register a user with username + email → sign in with username.
   - Duplicate email → rejected; duplicate username → rejected.
   - `Bill.Jones`, `pat123`, `danh` accepted; `@` and `+` rejected.
4. Forgot password:
   - User with no email → "contact your administrator" message.
   - User with email → reset link sent; `/auth/reset-password?username=…&token=…` completes.
5. Migration smoke:
   - In a test DB, seed users with `UserName = email`; run startup; verify `UserName` becomes the local-part, collisions get suffixes, `Email` preserved, and the admin can still log in with the migrated username.
6. Client smoke: desktop/Android OAuth flow still lands on `/auth/login` and completes; username shown when email absent.

---

## Common pitfalls (read carefully)

- **Empty email must be stored as `null`, not `""`.** With `RequireUniqueEmail = true`, Identity treats `""` as a non-null normalized email and will reject the _second_ user with an empty email. Always use `string.IsNullOrWhiteSpace(email) ? null : email.Trim()`.
- **`UpdateUserAsync` does not re-normalize.** When changing `Email`, also set `NormalizedEmail = _userManager.NormalizeEmail(user.Email)`.
- **`AuthSessionController.UpdateLastLoginAsync`** currently filters on `NormalizedEmail`; after the switch it must filter on `NormalizedUserName`.
- **Backfill ordering:** the migration must run before `AdminSeeder.SeedAsync()`, and `AdminSeeder` must look up the existing admin by username first.
- **Do not change `ApplicationUser` entity or add columns** — `UserName`/`Email` already exist; this is a data/flow change, not a schema change.
- **The OIDC `email` claim can now be null.** Guard `identity.SetClaim(Claims.Email, …)` in `HandleAuthorizeEndpoint` and make `email_verified` conditional in `HandleUserInfoEndpoint`. `preferred_username` (= `user.UserName`) is the authoritative identity claim and must remain set.
- **Clients need no protocol changes.** SyncTray and Android use authorization-code + PKCE (browser login), not a username/password grant — do not add or alter client credential logic. Only the Android `LoginViewModel` claim-priority swap (Phase 8.1) is needed. Do **not** change the Android `ServerConnection` record shape or its persisted JSON, and do not change the Android `email` scope.
- **Nullable reference types are enforced** (`TreatWarningsAsErrors`). Any `user.Email!` that could now be null will produce build errors — fix with null guards or `?? string.Empty`.
- **XML doc comments** are required on all new public members (per repo conventions).
