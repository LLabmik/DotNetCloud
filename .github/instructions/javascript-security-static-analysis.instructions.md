---
name: "JavaScript/TypeScript security static-analysis rules"
description: "Use when writing browser-extension or Node.js/TypeScript code (including build tools and visualizer presets) that executes generated code, handles untrusted config/data, or adds npm dependencies. Prevents recurring CodeQL js/bad-code-sanitization and Dependabot alerts."
applyTo:
  - "**/*.js"
  - "**/*.cjs"
  - "**/*.mjs"
  - "**/*.ts"
---

# JavaScript/TypeScript security static-analysis rules

## 0. npm scope (MANDATORY)

npm is allowed **only** for the browser extension at
`src/Clients/DotNetCloud.Client.BrowserExtension/` (its `package.json`,
`package-lock.json`, and the `npm ci` job in `.github/workflows/ci.yml`).

- ❌ Never add `package.json`, `package-lock.json`, `.npmrc`, `node_modules/`, or an npm build
  step to the server, modules, CLI, `tools/`, docs, or the repo root.
- ❌ Never add npm as part of a .NET build or test flow.
- ✅ Any npm-related change stays inside the browser-extension folder and updates its lockfile.

## 1. Never interpolate data into executable code (`js/bad-code-sanitization`)

Passing escaped data into a code string still counts as building code from data — escaping
(`JSON.stringify`, HTML encoding) does not satisfy the query.

```js
// ❌ wrong — the equation source becomes part of the code text
vm.runInContext(`(function (src) { ... })(${JSON.stringify(str)})`, sandbox);

// ✅ correct — constant code text, data passed as an argument
const compileReference = vm.runInContext('(function (src) { ... return ... })', sandbox);
compileReference(str);
```

Applies to `vm.runInContext`/`vm.runInNewContext`/`vm.Script`, `eval`, `new Function`, and any
template-built import/require/command string.

## 2. Treat config/preset/remote data as untrusted

- Validate shape and types before use; never spread parsed JSON into behaviour-affecting objects
  without an allowlist of keys.
- When merging objects from external input, reject `__proto__`, `constructor`, and `prototype` keys
  (see the vendored `vendor/deepmerge` package for the pattern).
- Do not inject HTML from remote data without an explicit sanitizer.

## 3. Dependency hygiene (Dependabot alerts)

Before adding or upgrading an npm dependency **in the browser extension**:

```bash
npm audit
npm ls <package>            # confirm which dependency pulled it in
npm view <package> version  # confirm the current release
```

- Prefer a version above the advisory's `first_patched_version`.
- If no patched release exists: prefer a vendored, patched copy declared as a `file:` dependency
  (as done for `deepmerge`) over suppressing the advisory.
- Do not add `overrides` that point at a `file:` path for a **transitive** dependency — npm can
  emit a broken symlink and an unparseable lockfile. Declare the patched package as a direct
  dependency with a version that satisfies the dependent's semver range instead.
- Commit the updated `package-lock.json`; never hand-delete advisory entries.

## 4. Tests

Any hardening gets a regression test under `tests/` (Jest), including the hostile input that
triggered the original finding.
