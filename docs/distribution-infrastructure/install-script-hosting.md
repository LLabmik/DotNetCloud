# Install Script Hosting via GitHub

> **Last Updated:** 2026-03-03  
> **Audience:** Project maintainers

---

## Overview

The DotNetCloud one-line install command relies on `raw.githubusercontent.com` to serve the install script directly from the GitHub repository. **No separate hosting, CDN, or web server is required** — GitHub provides this automatically for every public repository.

The install command referenced across all documentation is:

```bash
curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash
```

---

## How raw.githubusercontent.com Works

`raw.githubusercontent.com` is GitHub's raw file serving domain. It serves the **exact contents** of any file in a public repository, with no HTML wrapping, no rendering — just the raw bytes.

### URL Format

```
https://raw.githubusercontent.com/{owner}/{repo}/{branch-or-tag}/{path-to-file}
```

For DotNetCloud:

| Component | Value |
|---|---|
| Owner | `LLabmik` |
| Repo | `DotNetCloud` |
| Branch | `main` |
| File path | `tools/install.sh` |
| Full URL | `https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh` |

### Key Behaviors

- **Automatic:** Any file pushed to the repo is immediately available at its raw URL
- **No configuration:** There is no setup, no DNS, no deployment step
- **Branch-aware:** The URL includes the branch name, so `main` always serves the latest version
- **Tag-aware:** You can also pin to a release tag (e.g., `.../v1.0.0/tools/install.sh`)
- **Public repos only:** Raw serving works for public repositories. Private repos require authentication tokens in the URL.
- **Caching:** GitHub applies a short cache (typically ~5 minutes). After pushing an update to `install.sh`, it may take a few minutes for `raw.githubusercontent.com` to reflect the change.
- **Content-Type:** Files are served as `text/plain` regardless of extension. This is fine for `curl | bash` usage.
- **Rate limiting:** GitHub applies rate limits to raw content. For extremely high-traffic projects, consider mirroring to a CDN. For DotNetCloud's expected volume, GitHub's limits are more than sufficient.

---

## How to Publish or Update the Install Script

### Initial Setup (One-Time)

1. **Create the install script** at `tools/install.sh` in the repository root:

   ```powershell
   # The file already exists at tools/install.sh in the repo
   Get-Content "tools\install.sh"
   ```

2. **Commit and push to `main`:**

   ```powershell
   git add tools/install.sh
   git commit -m "Add Linux install script"
   git push origin main
   ```

3. **Verify it's live** (from any machine with internet access):

   ```bash
   curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh
   ```

   You should see the script contents printed to the terminal. If you get a 404, double-check:
   - The repository is **public** on GitHub
   - The file path is correct (case-sensitive)
   - The branch name is `main` (not `master`)
   - The push to GitHub has completed (check https://github.com/LLabmik/DotNetCloud)

### Updating the Script

1. **Edit** `tools/install.sh` locally
2. **Test** the script on a clean VM or container (see [Testing](#testing) below)
3. **Commit and push:**

   ```powershell
   git add tools/install.sh
   git commit -m "Update install script: <describe change>"
   git push origin main
   ```

4. **Wait ~5 minutes** for GitHub's CDN cache to clear, then verify:

   ```bash
   curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | head -20
   ```

That's it. There is no deploy step, no CI job, no separate hosting to manage.

---

## Pinning to a Release Tag

For stable releases, documentation can reference a tagged version instead of `main`:

```bash
# Always latest (follows main branch)
curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash

# Pinned to a specific release
curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/v1.0.0/tools/install.sh | bash
```

**Recommendation:** Use `main` in quick-start documentation (users want the latest). Use tagged URLs in release notes and changelogs for reproducibility.

---

## Testing

Before publishing an updated install script, test it in an isolated environment:

### Docker (Recommended)

```bash
# Test on Ubuntu 24.04
docker run --rm -it ubuntu:24.04 bash -c \
  "apt-get update && apt-get install -y curl && \
   curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash"
```

### Local VM

```bash
# On a clean Debian/Ubuntu VM
curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash
```

### Test from local file (before pushing)

```bash
# Test the local copy directly
bash tools/install.sh
```

---

## Troubleshooting

| Issue | Cause | Fix |
|---|---|---|
| `curl: (22) 404 Not Found` | File doesn't exist at that path on the specified branch | Verify the file path, branch name, and that the push reached GitHub |
| Old version served | GitHub CDN cache | Wait 5 minutes and retry; add `?token=$(date +%s)` as a cache buster for testing |
| `Permission denied` | Script missing execute bit or wrong line endings | Ensure `install.sh` has LF line endings (not CRLF) and is committed with `chmod +x` |
| Works on GitHub but not via curl | Repository is private | Make the repository public, or use a GitHub personal access token |

### Line Endings

The install script **must** use LF (`\n`) line endings, not CRLF (`\r\n`). If the script has CRLF line endings, bash will fail with errors like `/bin/bash^M: bad interpreter`.

Ensure `.gitattributes` includes:

```gitattributes
*.sh text eol=lf
```

---

## Alternative: GitHub Releases (for Binaries)

The install script itself is served via `raw.githubusercontent.com`, but the **release archives** it downloads (`.tar.gz`, `.zip`) should come from **GitHub Releases**:

```
https://github.com/LLabmik/DotNetCloud/releases/download/v{VERSION}/dotnetcloud-{VERSION}-linux-x64.tar.gz
```

GitHub Releases provides:
- Unlimited download bandwidth for release assets
- Permanent URLs tied to git tags
- Checksum verification via the releases page
- No CDN caching issues (assets are immutable per release)

The install script should download binaries from GitHub Releases, not from `raw.githubusercontent.com`.

---

## Summary

| What | Where | How |
|---|---|---|
| Install script (`install.sh`) | `raw.githubusercontent.com/.../main/tools/install.sh` | Push to `main` branch; automatically available |
| Release binaries (`.tar.gz`, `.zip`) | `github.com/.../releases/download/v{VERSION}/...` | Create a GitHub Release and upload assets |
| No separate hosting needed | — | GitHub handles everything for a public repo |
