# Static sites (`www` + `modules`) on IIS

> **Audience:** the operator of the IIS host that serves `dotnetcloud.net`.
> Related: [`docs/modules/MODULE_STORE.md`](../modules/MODULE_STORE.md) (what the `modules` site
> serves), [`WINDOWS_IIS_INSTALL_GUIDE.md`](server/WINDOWS_IIS_INSTALL_GUIDE.md) (the cloud
> instance, which is a **different** IIS setup and shares nothing with this one).

## 1. What these sites are

Two **static** IIS sites, no managed code and no app pool running application code:

| Site                        | Serves                                                                                   | Content repo                |
| --------------------------- | ---------------------------------------------------------------------------------------- | --------------------------- |
| `www.dotnetcloud.net`       | marketing site (home, server, client pages, module store page, support)                    | `DotNetCloud-WebSite`       |
| `modules.dotnetcloud.net`   | module store frontend + the signed catalogue (`index/`) + the `.dncpkg` packages (`packages/`) | `DotNetCloud.ModuleStore`   |

Neither site is related to `scripts/deploy.sh`, which deploys the **cloud instance** over SSH to a
Linux host. This document is the IIS/Windows path only.

Site *configuration* (`web.config`) lives in this repository under `deploy/sites/<site>/` so the
content repos stay free of server configuration.

## 2. Prerequisites on the IIS host

1. **IIS 10** with the static content feature (default).
2. **URL Rewrite** (`rewrite` module). **Without it every request returns HTTP 500** — IIS refuses to
   parse a `web.config` containing `<rewrite>`, it does not silently skip the rules (measured on this
   host: deploy succeeded, all four bindings answered `500` until the module was installed). It is
   installed by `tools/install-windows.ps1`; verify with
   `Get-WebGlobalModule | Where-Object Name -eq 'RewriteModule'`. Install it **before** deploying a
   `web.config` that contains rewrite rules.
3. **ARR is not required** for these sites (no reverse proxy).
4. **DNS** A/AAAA records for `www`, `modules` and the apex `dotnetcloud.net` pointing at the host.
5. **Certificates** covering `www.dotnetcloud.net`, `modules.dotnetcloud.net` and
   `dotnetcloud.net`. **Certify The Web** is already installed and renews them automatically
   (`Certify Management Agent` service) — see §7. Do not add another ACME client beside it.
6. The two content checkouts on disk, e.g. `C:\Src\DotNetCloud-WebSite` and
   `C:\Src\DotNetCloud.ModuleStore`, and a checkout of this repo (for `scripts/deploy-sites.ps1`
   and `deploy/sites/*/web.config`). On the live host this is `C:\Src\DotNetCloud-WebSite`,
   `C:\Src\DotNetCloud.ModuleStore` and `C:\Src\DotNetCloud-deploy` (the repo's `scripts\` and
   `deploy\sites\` trees side by side, which is what `$PSScriptRoot\..\deploy\sites` resolves to).

## 3. Directory layout: releases + `current`

Both sites use the same layout, so a rollback is a pointer swap rather than a rebuild:

```
C:\inetpub\Apps\DotNetCloud-Modules\
    releases\
        20261108-120000\        <- immutable content snapshot
        20261109-081500\
    current  ->  releases\20261109-081500      (directory junction)
```

**The IIS site's physical path is `<SiteRoot>\current`**, never the site root and never a release
directory directly. `scripts/deploy-sites.ps1` creates and re-points the junction.

For `modules` the site root layout is fixed by the store frontend and must not be changed — the
frontend fetches `../index/v1/index.json` and the catalogue's `downloadUrl` values are
site-root-relative (`packages/<id>/<version>/<file>.dncpkg`):

```
<release>\
    index.html  package.html  publisher.html  store.css  store.js
    web.config
    index\v1\index.json (+ .sig, packages\<id>.json, icons\)
    packages\<id>\<version>\<file>.dncpkg (+ .sig, store.json)
    publishers.json  yanked.json
```

## 4. Creating the sites

Run the first deploy **before** creating the site: `New-WebSite` needs the physical path to exist,
and the physical path is the `current` junction that the deploy creates.

```powershell
# 1. first deploy (creates releases\<timestamp> and the `current` junction)
.\scripts\deploy-sites.ps1 -Site modules -Verify
.\scripts\deploy-sites.ps1 -Site www     -Verify
# 2. static-only app pool: no managed runtime
Import-Module WebAdministration
foreach ($pool in 'DotNetCloud-Website', 'DotNetCloud-Modules') {
    New-WebAppPool -Name $pool
    Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$pool" -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
}

# 3. www site + bindings (SNI, so several certs can share :443)
New-WebSite -Name 'DotNetCloud-Website' `
    -PhysicalPath 'C:\inetpub\Apps\DotNetCloud-WebSite\current' `
    -ApplicationPool 'DotNetCloud-Website' -Port 80 -HostHeader 'www.dotnetcloud.net'
New-WebBinding -Name 'DotNetCloud-Website' -Protocol https -Port 443 -HostHeader 'www.dotnetcloud.net' -SslFlags 1
New-WebBinding -Name 'DotNetCloud-Website' -Protocol http   -Port 80  -HostHeader 'dotnetcloud.net'
New-WebBinding -Name 'DotNetCloud-Website' -Protocol https -Port 443 -HostHeader 'dotnetcloud.net' -SslFlags 1

# 4. modules site
New-WebSite -Name 'DotNetCloud-Modules' `
    -PhysicalPath 'C:\inetpub\Apps\DotNetCloud-Modules\current' `
    -ApplicationPool 'DotNetCloud-Modules' -Port 80 -HostHeader 'modules.dotnetcloud.net'
New-WebBinding -Name 'DotNetCloud-Modules' -Protocol https -Port 443 -HostHeader 'modules.dotnetcloud.net' -SslFlags 1
```

### Converting a hand-copied site to the release layout

If the site was deployed by copying files straight into the site root, do this once:

```powershell
# the root already holds the content; deploy-sites.ps1 adds releases\ + current beside it
.\scripts\deploy-sites.ps1 -Site modules -Verify

Set-ItemProperty 'IIS:\Sites\DotNetCloud-Modules' -Name physicalPath `
    -Value 'C:\inetpub\Apps\DotNetCloud-Modules\current'
```

The old files at the site root are then inert — `current` is the served directory. Delete them once
the smoke checks pass.

## 5. `web.config` — what it does and where it lives

| File                                | Copied to              | Purpose                                                        |
| ----------------------------------- | ---------------------- | -------------------------------------------------------------- |
| `deploy/sites/www/web.config`       | www release root       | redirects, cache policy, MIME maps, security headers            |
| `deploy/sites/modules/web.config`   | modules release root   | same + `.dncpkg`/`.sig` maps, catalogue-aware cache policy      |

`deploy-sites.ps1` copies the file **last**, after the content robocopy, so the core repo always
wins over anything a content checkout might contain.

> **ACME webroot.** Certify The Web renews these certificates through its own HTTP listener (§7), so
> nothing writes into the site root. The deploy still creates an empty `.well-known/acme-challenge`
> in every release: if a webroot-based challenge is ever configured, it must target
> `<SiteRoot>\current\.well-known\acme-challenge`, because after a swap `current` is what IIS serves.
> Pointing a renewal tool at the site root instead would break on the next deploy — silently, ~90
> days later.

### Redirects (`modules` and `www`)

```text
http://modules.dotnetcloud.net/x    -> 301 -> https://modules.dotnetcloud.net/x
http://www.dotnetcloud.net/x        -> 301 -> https://www.dotnetcloud.net/x
http://dotnetcloud.net/x            -> 301 -> https://www.dotnetcloud.net/x
https://dotnetcloud.net/x           -> 301 -> https://www.dotnetcloud.net/x
```

The apex rule lives in the **www** `web.config`, which is why `dotnetcloud.net` is bound to the
`DotNetCloud-Website` site: the request reaches that site and the rule rewrites it to `www`. Apex and
`www` must therefore share one physical path (both bindings on one site). If the apex is ever moved
to its own site, that site needs the same `ApexToWww` rule.

### Cache policy

| Path                  | Header                       | Why                                              |
| --------------------- | ---------------------------- | ------------------------------------------------ |
| `www`: `css/ js/ images/ fonts/` | `max-age=30d`      | deploy-versioned static assets                    |
| `www`: everything else           | `no-cache`         | a page edit must be visible on reload             |
| `modules`: everything else       | `no-cache`         | `publishers.json`, `yanked.json`, html, css, js    |
| `modules`: `index/`              | `max-age=60s`      | the catalogue changes on every publish            |
| `modules`: `index/v1/icons/`     | `max-age=30d`      | copied out of the package, stable per version     |
| `modules`: `packages/`           | `max-age=365d`     | immutable by version — never re-publish a version |

Implemented with `staticContent/clientCache` at site level plus `<location path="...">` overrides.
`packages/*` is not marked `immutable` (IIS `clientCache` cannot emit that token); the year-long
`max-age` plus version-addressed file names gives the same effect in practice.

### MIME maps

`.dncpkg` and `.sig` are **not** IIS-known extensions. IIS refuses to serve an extension it does
not map, and the failure looks like a **404 on the package file** — the single most likely
first-deploy failure. Both the `modules` file and the smoke checks cover it.

| Extension      | Type                            |
| -------------- | ------------------------------- |
| `.dncpkg`      | `application/octet-stream`      |
| `.sig`         | `application/octet-stream`      |
| `.json`        | `application/json; charset=utf-8` |
| `.svg`         | `image/svg+xml`                 |
| `.webp`        | `image/webp`                    |
| `.woff`, `.woff2` | `font/woff`, `font/woff2`    |

**Extensionless files are not served** on this host — see the ACME note below.

These used to be added by hand on the server; the committed `web.config` replaces that and is now
the only place they should be maintained.

### Handlers and extensionless files (ACME constraint)

Both files `<clear />` the inherited handler list and register only the static-file handler. The
machine-level list maps `path="*."` to `ExtensionlessUrlHandler-*` (ISAPI / managed pipeline), which
claims extensionless requests on a site that should not run any.

**Measured limitation (2026-11-08, live host):** an extensionless file is **not** served —
`/.well-known/acme-challenge/configcheck` returns `404.17` (win32 `50`), i.e. "the requested content
appears to be script". Clearing the handlers leaves `StaticFile(*)` as the only handler and the file
still 404s, and neither `<mimeMap fileExtension="." />` nor `fileExtension=""` changes it. A
`.txt` file in the same directory returns 200, so the directory and bindings are fine — it is the
no-extension case specifically.

**Consequence:** an ACME HTTP-01 token (`/.well-known/acme-challenge/<token>`, always extensionless)
cannot be served from this webroot. **This does not affect the current setup** — Certify The Web uses
its own challenge listener (§7), which is why renewal has always worked here. It only matters if
someone switches to a webroot/filesystem challenge, in which case the options are: a `.txt` response
served through a rewrite rule, DNS-01, or a self-hosting challenge with the site stopped briefly.

## 6. Deploy

Run on the IIS host, from a checkout of this repo:

```powershell
# both sites, with smoke checks
.\scripts\deploy-sites.ps1 -Site all -Verify

# one site
.\scripts\deploy-sites.ps1 -Site modules -Verify

# see what would happen
.\scripts\deploy-sites.ps1 -Site all -WhatIf

# non-default content locations
.\scripts\deploy-sites.ps1 -Site modules `
    -ModulesCatalogueSource 'C:\Src\DotNetCloud.ModuleStore' `
    -ModulesSiteRoot 'C:\inetpub\Apps\DotNetCloud-Modules'
```

What it does, in order: validate every source (nothing is touched if a source is missing) → build
`releases\<yyyyMMdd-HHmmss>\` with `robocopy /MIR` from the content checkouts → copy the repo's
`web.config` over the release root → re-point `current` → smoke checks → prune old releases.

| Parameter            | Default                                    | Notes                                                |
| -------------------- | ------------------------------------------ | ---------------------------------------------------- |
| `-Site`              | `all`                                      | `www`, `modules` or `all`                            |
| `-WwwSource`         | `C:\Src\DotNetCloud-WebSite`               | marketing-site checkout                              |
| `-ModulesCatalogueSource` | `C:\Src\DotNetCloud.ModuleStore`      | store repo (`site/`, `index/`, `packages/`)          |
| `-ModulesFrontendSource` | `<catalogue>\site`                    | frontend files                                       |
| `-WwwSiteRoot`       | `C:\inetpub\Apps\DotNetCloud-WebSite`      | deploy root                                          |
| `-ModulesSiteRoot`   | `C:\inetpub\Apps\DotNetCloud-Modules`      | deploy root                                          |
| `-ReleaseName`       | current UTC time (`yyyyMMdd-HHmmss`)       | refuses to reuse an existing release                 |
| `-KeepReleases`      | `5`                                        | `0` keeps everything; current + previous are never pruned |
| `-Verify`            | off                                        | smoke checks after the swap; failure exits non-zero  |
| `-VerifyOnly`        | off                                        | smoke checks only, no deploy                         |
| `-Rollback`          | off                                        | re-point `current` at the previous release           |
| `-RollbackTo`        | –                                          | re-point `current` at a named release                |
| `-ListReleases`      | off                                        | list releases, mark the current one                  |
| `-WhatIf`            | off                                        | print every action, change nothing                   |

The script exits non-zero on any failure, so it can be wrapped by a task or a pipeline later
(CI-driven deploys are a separate, deferred decision — they need a deployment credential here).

## 7. TLS and renewal

Certificates are issued and renewed automatically by **Certify The Web**, which is already installed
on the IIS host. Nothing in this repo issues or renews a certificate; there is no win-acme task and
none should be added.

| Fact | Value |
| ---- | ----- |
| Service | `Certify Management Agent`, Automatic, Running |
| Program files | `C:\Program Files\CertifyTheWeb` |
| Configuration / state | `C:\ProgramData\Certify` (`manageditems.db` = managed certificates, `assets\<domain>\*.pfx`, `credentials`, `keys`, `logs`) |
| Managed names | `dotnetcloud.net`, `modules.dotnetcloud.net`, `llabmik.net`, `messagebase.net` |
| Expiry of the current pair | **2027-02-05** (`assets\dotnetcloud.net\20270205_*.pfx`, `assets\modules.dotnetcloud.net\20270205_*.pfx`) |

**Challenge method.** Certify uses its **own HTTP challenge listener** (`C:\ProgramData\Certify\logs\httpChallengeServer.log`),
not a site webroot. That matters here: this IIS will not serve extensionless files, so any
webroot-based challenge would fail (see §5) — Certify's listener is unaffected, and the release/
`current` swap therefore does not affect renewal either.

Routine checks:

```powershell
Get-Service 'Certify Management Agent'
Get-Content 'C:\ProgramData\Certify\logs\httpChallengeServer.log' -Tail 20
Get-ChildItem 'C:\ProgramData\Certify\assets\dotnetcloud.net' | Select-Object Name, LastWriteTime
```

A renewal is due roughly 30 days before expiry. If a certificate is not renewed, the site starts
serving an expired certificate — check `assets\<domain>` for a PFX whose name is the new expiry date.
Manual replacement still works if needed: import the PFX into `LocalMachine\My` and rebind it
(`New-WebBinding ... -SslFlags 1` with SNI, or IIS Manager → **Bindings → Edit → SSL certificate**).

**Do not** add a second ACME client (win-acme, Certbot) on top of Certify: two clients renewing the
same names will hit Let's Encrypt rate limits and fight over the port-80 challenge.

## 8. Rollback

```powershell
.\scripts\deploy-sites.ps1 -Site modules -ListReleases
.\scripts\deploy-sites.ps1 -Site modules -Rollback              # previous release
.\scripts\deploy-sites.ps1 -Site www -RollbackTo 20261108-120000  # a specific one
```

A rollback re-points the junction; nothing is rebuilt and no content is re-copied. IIS serves the
new directory immediately for new requests (a request already in flight finishes from the old one).
Roll back **before** touching DNS or certificates — they are independent of a content release.

## 9. Post-deploy smoke list

`-Verify` / `-VerifyOnly` runs this automatically:

| # | Check                                                                       | Site      |
| - | --------------------------------------------------------------------------- | --------- |
| 1 | `/` and `/index.html` return 200 (plus `/modules.html` on www)                | both      |
| 2 | `/index/v1/index.json` and `/index/v1/index.json.sig` return 200              | modules   |
| 3 | `/publishers.json` and `/yanked.json` return 200                              | modules   |
| 4 | The first package in the catalogue HEADs as `application/octet-stream`        | modules   |
| 5 | `http://…/` 301s to `https://…/`                                              | both      |
| 6 | `http(s)://dotnetcloud.net/` 301s to `https://www.dotnetcloud.net/`           | www       |

Manual checks worth doing after the first deploy on a host (not automated):

- `package.html` and `publisher.html` render the catalogue (the frontend is same-origin and
  relative-URL driven — a subdirectory deploy breaks them).
- `curl.exe -I https://modules.dotnetcloud.net/packages/<id>/<version>/<file>.dncpkg` shows
  `Content-Type: application/octet-stream` and the published byte length.
- A real `dotnetcloud module install <id> --from-store` against the published
  `Modules:Store:IndexUrl` (this exercises index signature, hash, package signature and install).
- Browser dev-tools: `index/v1/index.json` is not served from cache after a publish.

## 10. Troubleshooting

| Symptom                                                        | Cause                                                                 |
| -------------------------------------------------------------- | --------------------------------------------------------------------- |
| **404** on a `.dncpkg` / `.sig` file                           | MIME map missing — check the release root has this repo's `web.config` |
| **HTTP 500.19** mentioning a locked or unknown section          | `web.config` from a different site, or `rewrite` not installed         |
| Redirects do nothing, site otherwise fine                      | URL Rewrite module missing                                            |
| `current` missing / site shows a directory listing             | deploy never completed; check `current` exists and is a junction       |
| Store frontend loads but lists nothing                         | `index/v1/index.json` not beside the frontend (wrong release layout)  |
| A publish is not visible after a deploy                        | `index/` is cached up to 60 s; hard-reload or wait                    |
| `robocopy` returns 8+                                           | files locked by a running request or ACL problem — see the script log |
| `deploy-sites.ps1` refuses to remove `current`                  | `current` is a real directory, not a junction — inspect before deleting |
