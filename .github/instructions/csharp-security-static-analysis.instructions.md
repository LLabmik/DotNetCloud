---
name: "C# security static-analysis rules"
description: "Use when writing or reviewing C# code that logs request/user input, builds filesystem paths from parameters, or touches credentials/account identifiers. Prevents recurring CodeQL cs/log-forging, cs/exposure-of-sensitive-information, cs/cleartext-storage-of-sensitive-information, and cs/path-injection alerts."
applyTo: "**/*.cs"
---

# C# security static-analysis rules

These rules exist because the same CodeQL alert classes keep returning. They are not optional style
preferences — every rule below maps to a GitHub code-scanning query. When a rule and a convenience
implementation conflict, the rule wins.

## 1. Never log unsanitized request/user input (`cs/log-forging`)

Any value that comes from HTTP (`Request.Method`, `Request.Path`, query string, headers, route
values, body), a module RPC payload, a filename, or a username **must** be passed through
`LogSanitizer.Sanitize(...)` before it reaches a logger.

```csharp
// ✅ correct
_logger.LogWarning("Antiforgery validation failed for {Method} {Path}",
    LogSanitizer.Sanitize(context.Request.Method),
    LogSanitizer.Sanitize(context.Request.Path.Value));

// ❌ wrong — raw request values can inject CR/LF into the log
_logger.LogWarning("Request failed for {Path}", context.Request.Path);
```

Applies to every logger overload (`LogInformation`, `LogDebug`, `LogError(ex, ...)`, etc.).

## 2. Do not log credentials or account identifiers (`cs/exposure-of-sensitive-information`, `cs/cleartext-storage-of-sensitive-information`)

`LogSanitizer.Sanitize` does **not** clear these queries. The only fix is to **stop the value from
reaching the log**.

Never log:

- passwords, passphrases, tokens, API keys, account keys, connection strings;
- email addresses (and display names that may contain an email address);
- account identifiers supplied as method parameters (`Guid accountId`, mailbox ids, remote
  account ids) — CodeQL classifies these as sensitive even when they look innocuous;
- user/account records resolved from those parameters.

```csharp
// ❌ wrong — CWE-312 cleartext storage of sensitive information
_logger.LogWarning("Sync requested for account {AccountId} for user {UserId}", accountId, userId);

// ✅ correct — log the outcome/context, not the sensitive identifier
_logger.LogWarning("Sync requested for an account that does not belong to user {UserId}", userId);
```

When you need diagnostics, log counts, statuses, module names, and non-identifying entity types
instead of ids.

## 3. Validate every filesystem path built from input (`cs/path-injection`)

Any path derived from a parameter, request value, or module payload must be canonicalized and
proven to be inside a trusted root **before** it reaches `File.*`, `Directory.*`,
`new FileStream(...)`, `ZipFile.*`, or similar sinks.

Rules:

- Call `Path.GetFullPath(...)` first.
- Keep the **same variable** in the guard that you pass to the sink. Guards on a derived variable
  (for example a `Path.GetRelativePath` result) do not count.
- Do **not** rely on `StartsWith`, `EndsWith`, `IsNullOrEmpty`, `IsNullOrWhiteSpace`,
  `File.Exists`, `Directory.Exists`, or null checks alone — CodeQL classifies all of them as
  *weak* traversal guards.
- Use `IndexOf`/`string.Equals` against a canonical root prefix, or allowlist the file name with
  an explicit regex/`Guid`-based name.

```csharp
var root = Path.GetFullPath(configuredRoot);
var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
var rootPrefix = trimmedRoot + Path.DirectorySeparatorChar;
var target = Path.GetFullPath(Path.Combine(root, userSuppliedRelativePath));

// Non-weak guard on the value used by the sinks below.
if (target.IndexOf(rootPrefix, StringComparison.Ordinal) != 0)
{
    if (!string.Equals(
            target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            trimmedRoot,
            StringComparison.Ordinal))
    {
        throw new ValidationException("path", "Path escapes the allowed root.");
    }
}

// Only now: Directory.Exists(target), File.WriteAllText(target, ...), etc.
```

For user-supplied **file names** (not relative paths), prefer discarding all directory components
(`Path.GetFileName`) and/or generating the stored name yourself.

## 4. Never build executable code from data (`js/bad-code-sanitization` applies to embedded JS too)

Do not interpolate values into strings passed to `vm.runInContext`, `eval`, or `new Function` — not
even with `JSON.stringify`/HTML escaping. Keep the code text a constant and pass the data as an
argument.

## 5. Dependency hygiene (Dependabot alerts)

Before adding or upgrading a NuGet/npm package, confirm it has no open advisory:

```bash
dotnet list package --vulnerable --include-transitive
npm audit
```

- Prefer a version above the advisory's `first_patched_version`.
- If no patched release exists, do not silently ignore the alert: either vendor a patched copy
  (documented in the package file/README) or record an explicit dismissal with a reason.
- Never disable `npm audit`/Dependabot to make a build pass.

## 6. Tests for every security fix

Every change made to satisfy one of these rules needs a regression test that exercises the hostile
input (CR/LF in the logged value, `..` in a path, `__proto__` in a merge source, etc.). Follow the
existing `MethodName_Condition_ExpectedResult` naming convention.

## Pre-PR self-check

- [ ] Every request/user-derived value in a log call is wrapped in `LogSanitizer.Sanitize`.
- [ ] No credentials, emails, or parameter-supplied account ids appear in log messages.
- [ ] Every parameter-derived path is `Path.GetFullPath`-canonicalized and guarded with a
      non-weak check on the same variable before any `File`/`Directory`/`FileStream` use.
- [ ] New/changed code has a hostile-input regression test.
- [ ] `dotnet list package --vulnerable --include-transitive` is clean.
