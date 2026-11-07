# `.dncpkg` module packaging

> **Status:** implemented 2026-11-07 (phase B1) · **Tool:** `src/Tools/DotNetCloud.ModulePackaging`
> (`dotnetcloud-pack`) · **Plan:** `docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md` §4.6, §4.10, B1
> **Audience:** module authors, store operators, and anyone implementing the installer (B3)

A `.dncpkg` file is a **deterministic, signed ZIP** that contains a module manifest, its licence and
documentation, and one self-contained publish output per runtime identifier. The format is designed
so that a package can be verified *before* a single entry is extracted, and so that two independent
builders of the same inputs produce the same bytes — and therefore the same signature.

---

## 1. File name and layout

```
<id>-<version>.dncpkg
├── manifest.json        (v2 for non-required modules; v1 accepted for bundled modules until Track C)
├── LICENSE              required
├── README.md            required
├── CHANGELOG.md         required
├── icon.png             optional
└── files/
    ├── linux-x64/       self-contained:false publish output
    └── win-x64/         (one directory per RID)
```

* The file name **is** part of the identity: `<id>` and `<version>` must match the manifest. Module
  ids may contain hyphens, so the parser splits on the **last** hyphen whose suffix is a version —
  `org.example.receipt-scanner-1.2.3.dncpkg` yields `org.example.receipt-scanner` + `1.2.3`.
* `signature.json` **is not** inside the archive. A file cannot sign its own bytes (finding F8); the
  signature is detached as `<package>.sig` — see §3.
* Directories are implicit; the packer never writes directory entries.

---

## 2. Determinism

The packer guarantees that the same input tree produces byte-identical output:

| Rule                        | Implementation                                                             |
| --------------------------- | -------------------------------------------------------------------------- |
| Entry order                 | sorted with `StringComparer.Ordinal` on the entry name                     |
| Timestamps                  | every entry gets `1980-01-01T00:00:00` (ZIP has no time zone, 2 s resolution) |
| Compression                 | `CompressionLevel.Optimal` for every entry                                 |
| Directory entries           | never written                                                              |
| Owner / permissions / xattr | never recorded                                                             |
| Ignored files               | `.DS_Store`, `Thumbs.db`, `desktop.ini` are skipped                        |

Repository tests pack the same staging directory twice and assert both the byte arrays and the
SHA-256 match, so a regression is caught immediately.

---

## 3. Signing

### 3.1 Scheme (decision, F8)

**The signature is detached and covers `SHA-256(package bytes)`.** Two independent statements must
hold for a package to be accepted:

1. `sha256` in the `.sig` file equals the SHA-256 of the package file on disk.
2. The Ed25519 signature verifies over those 32 digest bytes with a **trusted** public key.

Signing the digest instead of the whole archive keeps signing and verification streaming: a 500 MB
package is never loaded into memory. The security is unchanged — an attacker who rewrites the digest
must also forge an Ed25519 signature over the new digest.

Rejected by design:

* `algorithm` other than `ed25519` — the **key id** selects the algorithm, the field is informational
  and any other value fails.
* a `.sig` naming a different package file (prevents moving a signature between packages).
* a key id the verifier does not trust — unknown keys are never "accepted on faith".

### 3.2 Signature file

```json
{
  "schemaVersion": 1,
  "keyId": "store-2026",
  "algorithm": "ed25519",
  "package": "org.example.receipt-scanner-1.2.3.dncpkg",
  "sha256": "9f2c…",
  "signature": "base64 of 64 bytes"
}
```

### 3.3 Key formats and generation

| Item        | Format                                                                 |
| ----------- | ---------------------------------------------------------------------- |
| Private key | base64 of the raw **32-byte Ed25519 seed** (written `0600` on Unix)    |
| Public key  | base64 of the raw **32-byte Ed25519 public key**                       |
| Key file    | JSON `{ "keyId", "publicKey", "privateKey" }` produced by `keygen`     |

```bash
dotnetcloud-pack keygen --key-id store-2026 --output keys/store-2026.key.json
```

The public key is what a verifier pins; the private key never leaves the signing environment. Store
key management (offline backup, rotation, current+previous key pinning) is phase B5; the verifier
already accepts an arbitrary set of trusted key ids.

### 3.4 Why BouncyCastle

.NET 10 has **no** `System.Security.Cryptography.Ed25519` surface, and a libsodium binding would add
a native asset to a cross-platform build tool. `BouncyCastle.Cryptography` is pinned in
`Directory.Packages.props` and is pure managed. Replacing it later is a one-file change
(`Ed25519Signature.cs`); the key and signature formats above are specified independently of it.

---

## 4. Verification pipeline

`verify` (and the installer in B3) runs the checks in this order. A valid signature over an unsafe
archive is still rejected.

| #  | Check                                                         | Failure code                 |
| -- | ------------------------------------------------------------- | ---------------------------- |
| 1  | package exists, size ≤ 512 MiB                                | `PACKAGE_NOT_FOUND`, `PACKAGE_TOO_LARGE` |
| 2  | file name is `<id>-<version>.dncpkg`                          | `PACKAGE_FILENAME_INVALID`   |
| 3  | archive opens, structure passes §5                            | `PACKAGE_INVALID_ZIP`, `ARCHIVE_*` |
| 4  | `manifest.json`, `LICENSE`, `README.md`, `CHANGELOG.md` exist | `PACKAGE_ENTRY_MISSING`      |
| 5  | at least one `files/<rid>/` binary exists                     | `PACKAGE_ENTRY_MISSING`      |
| 6  | manifest validates with the **core's own loader**             | `MANIFEST_INVALID`           |
| 7  | `manifest.id` / `manifest.version` match the file name        | `MANIFEST_MISMATCH`          |
| 8  | `minCoreVersion` parses and this core satisfies it            | `MANIFEST_INCOMPATIBLE_CORE` |
| 9  | signature present (unless `--allow-unverified`)               | `SIGNATURE_MISSING`          |
| 10 | signature is well-formed, `ed25519`, names this file          | `SIGNATURE_INVALID`          |
| 11 | hash matches the signature's `sha256`                         | `SIGNATURE_HASH_MISMATCH`    |
| 12 | key id is trusted                                             | `SIGNATURE_UNTRUSTED_KEY`    |
| 13 | Ed25519 signature verifies over the digest                    | `SIGNATURE_INVALID`          |

Step 6 deliberately reuses `ModuleManifestLoader` (the loader the supervisor uses) rather than a
second implementation, so packaging cannot drift from runtime validation. A v1 manifest is accepted
with the non-fatal warning `MANIFEST_LEGACY_V1` — bundled first-party modules keep v1 until Track C
migrates them, while every non-required module must ship v2 at store launch.

---

## 5. Archive hardening (F13)

Signature verification proves *who* built an archive, not that it is safe to expand. These checks run
before extraction, and extraction re-checks every resolved path against the destination root.

| Limit                     | Value         | Rationale                                      |
| ------------------------- | ------------- | ---------------------------------------------- |
| entries                   | 20 000        | bounded metadata cost                          |
| package size              | 512 MiB       | bounded download                               |
| uncompressed total        | 2 GiB         | bounded disk use                               |
| single entry              | 512 MiB       | bounded single-file expansion                  |
| compression ratio         | 200:1         | zip bomb (entries ≥ 1 MiB only)                |
| path length               | 200 chars     | Windows `MAX_PATH` headroom                    |
| path segments             | 16            | bounded depth                                  |
| manifest size             | 256 KiB       | bounded parse                                  |

Rejected entry names — zip-slip and its cousins:

| Shape                                        | Code                          |
| -------------------------------------------- | ----------------------------- |
| absolute (`/etc/passwd`), drive letter (`C:`) | `ARCHIVE_ABSOLUTE_PATH`       |
| `..` segment                                  | `ARCHIVE_PATH_TRAVERSAL`      |
| backslash separator                           | `ARCHIVE_INVALID_NAME`        |
| empty or `.` segment                          | `ARCHIVE_INVALID_NAME`        |
| control characters                            | `ARCHIVE_INVALID_NAME`        |
| trailing dot or space (Windows strips them)   | `ARCHIVE_ALTERNATE_DATA_STREAM` |
| `:` alternate data stream                     | `ARCHIVE_ALTERNATE_DATA_STREAM` |
| reserved device name (`CON`, `NUL`, `COM1`…)  | `ARCHIVE_RESERVED_NAME`       |
| same name twice                               | `ARCHIVE_DUPLICATE_ENTRY`     |
| case-only collision                           | `ARCHIVE_CASE_COLLISION`      |
| symbolic link (unix `S_IFLNK` / reparse point) | `ARCHIVE_SYMLINK`             |

`ModulePackageVerifier.ExtractAsync` resolves `Path.GetFullPath(Path.Combine(root, name))` and
requires the relative path to stay inside the root (no rooted path, no `..` first segment, not the
root itself) before writing.

**Not yet covered here (B3):** the download path (SSRF allow-list for index-provided URLs,
`https:`-only, redirect re-checks) and index anti-replay. Those belong to the installer that fetches
packages, not to the archive format.

---

## 6. Local feed

A local feed is a directory with the package files next to an `index.json`:

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-11-07T22:13:40Z",
  "packages": [
    {
      "id": "dotnetcloud.example",
      "name": "Example",
      "version": "1.0.0",
      "file": "dotnetcloud.example-1.0.0.dncpkg",
      "size": 21138267,
      "sha256": "12759d5c…",
      "capabilities": ["INotificationService", "IStorageProvider", "IAuditLogger"],
      "userConsentScopes": [],
      "keyId": "dev-local",
      "signature": "base64…"
    }
  ]
}
```

Rules enforced when reading a feed:

* entries are sorted by id then version when written;
* a feed entry's `file` may not resolve outside the feed directory (a feed is data, not authority);
* verification prefers the sibling `<package>.sig` and falls back to the `keyId` + `signature`
  recorded in the index, so a single-file distribution still verifies;
* the package is copied into the feed directory if it came from elsewhere — a feed never records an
  external path.

This format is a trimmed version of the store index (§4.9 of the plan), so the installer's
verification path does not change when the remote index arrives.

---

## 7. Tool reference

```
dotnetcloud-pack pack    --input <dir> | --project <csproj> --rid <rid> [--rid …]
                         [--configuration Release] [--manifest <path>] [--output <file>]
                         [--sign-key <key.json>] [--feed <dir>] [--no-build]
dotnetcloud-pack verify  <package> [--signature <file>] [--key id=base64]… [--key-file <path>]…
                         [--allow-unverified]
dotnetcloud-pack inspect <package> [--signature <file>]
dotnetcloud-pack keygen  --key-id <id> [--output <file>]
dotnetcloud-pack feed    list   --feed <dir>
dotnetcloud-pack feed    verify --feed <dir> --id <id> --version <ver> [--key …] [--allow-unverified]
```

Exit codes: `0` success, `1` verification or usage failure. `pack` always verifies its own output
before reporting success — a package that would be refused at install time is never published.

`pack --project` runs `dotnet publish -c <configuration> -r <rid> --self-contained false -o
<staging>/files/<rid>` for each `--rid`, copies `manifest.json` from `<projectDir>` or
`<projectDir>/..`, and copies `LICENSE`/`README.md`/`CHANGELOG.md`/`icon.png` from the manifest's
directory when present.

---

## 8. Authoring checklist

- ☐ `manifest.json` with `manifestVersion: 2` for a non-required module (see §4.7 of the plan).
- ☐ `LICENSE`, `README.md`, `CHANGELOG.md` next to the manifest.
- ☐ A host project that publishes per RID (`dotnet publish -r <rid>`).
- ☐ `minCoreVersion` set to the oldest core you support (`0.7.0` today).
- ☐ `dotnetcloud-pack pack --project … --rid linux-x64 --rid win-x64 --sign-key …`
- ☐ `dotnetcloud-pack verify <package> --key <your-key-id>=<public-key>` passes.
- ☐ `dotnetcloud-pack inspect <package>` shows exactly the entries you expect.

---

## 9. CI fixture

`.github/workflows/ci.yml` (`module-package` job) packs the Example module, signs it with an
ephemeral key, verifies it, inspects it, and uploads the `.dncpkg`, its `.sig` and the local
`index.json` as the `example-module-package` artifact. That artifact is the fixture the installer
tests (B3) consume, and the job fails if determinism, archive safety or manifest validation regresses.

---

## 10. Deferred to B2/B3

| Item                                              | Where      |
| ------------------------------------------------- | ---------- |
| store index build + curated key signing           | B2         |
| publisher key registry, revocation/yank           | B2/B5      |
| remote index client, mirrors, update checks       | B3         |
| download URL allow-list (SSRF), redirect re-check | B3         |
| index anti-replay (`generatedAt` monotonic)       | B3         |
| installer state machine + rollback                | B3         |
| provenance attestations (SLSA)                    | B5         |
