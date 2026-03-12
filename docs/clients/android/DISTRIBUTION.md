# Android Client Distribution

> Guide for building, signing, and distributing the DotNetCloud Android client across Google Play, F-Droid, and direct APK channels.

## Build Flavors

| Channel | App ID | Push | Proprietary Deps | Build Flag |
|---|---|---|---|---|
| **Google Play** | `net.dotnetcloud.client` | FCM | Yes (Firebase) | Default |
| **F-Droid** | `net.dotnetcloud.client.fdroid` | UnifiedPush | None | `-p:BuildFlavor=fdroid` |
| **Direct APK** | `net.dotnetcloud.client` | FCM | Yes (Firebase) | Default |

Both flavors can be installed side-by-side on the same device due to separate app IDs.

---

## Release Build

### Google Play / Direct APK

```powershell
dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj `
  -c Release `
  -f net10.0-android
```

### F-Droid

```powershell
dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj `
  -c Release `
  -f net10.0-android `
  -p:BuildFlavor=fdroid
```

Output location: `artifacts/publish/` or `bin/Release/net10.0-android/publish/`.

---

## Signing

### Keystore Setup

Generate a release keystore (one-time):

```powershell
keytool -genkeypair `
  -alias dotnetcloud `
  -keyalg RSA -keysize 4096 `
  -validity 10000 `
  -keystore dotnetcloud-release.keystore `
  -storepass "<secure-password>" `
  -dname "CN=DotNetCloud, O=DotNetCloud Project"
```

> **Security:** Never commit the keystore or passwords to source control. Store them securely (e.g., CI secrets, password manager).

### Configure Signing in the Project

Add to the `.csproj` or pass as MSBuild properties:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <AndroidKeyStore>true</AndroidKeyStore>
  <AndroidSigningKeyStore>dotnetcloud-release.keystore</AndroidSigningKeyStore>
  <AndroidSigningKeyAlias>dotnetcloud</AndroidSigningKeyAlias>
  <AndroidSigningKeyPass>env:SIGNING_KEY_PASS</AndroidSigningKeyPass>
  <AndroidSigningStorePass>env:SIGNING_STORE_PASS</AndroidSigningStorePass>
</PropertyGroup>
```

Or at build time:

```powershell
dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj `
  -c Release `
  -f net10.0-android `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore=dotnetcloud-release.keystore `
  -p:AndroidSigningKeyAlias=dotnetcloud `
  -p:AndroidSigningKeyPass="<password>" `
  -p:AndroidSigningStorePass="<password>"
```

---

## Google Play Distribution

### Prerequisites

- Google Play Developer account ($25 one-time fee)
- App signed with upload key (Google Play App Signing recommended)
- Privacy policy URL

### Steps

1. **Create the app** in Google Play Console.
2. **Upload the AAB** (Android App Bundle, preferred over APK):
   ```powershell
   dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj `
     -c Release -f net10.0-android `
     -p:AndroidPackageFormat=aab
   ```
3. **Complete the store listing:**
   - Title: `DotNetCloud`
   - Short description: `Self-hosted cloud — chat, files, and more`
   - Full description: Feature overview
   - Screenshots: At minimum phone (2) and tablet (1)
   - Category: Communication / Productivity
4. **Set content rating** via the questionnaire.
5. **Configure pricing** (Free).
6. **Submit for review.**

### Version Numbering

| Property | Format | Example |
|---|---|---|
| `ApplicationDisplayVersion` | `major.minor.patch[-suffix]` | `0.1.0-alpha` |
| `ApplicationVersion` | Auto-incrementing integer | `1`, `2`, `3` |

Increment `ApplicationVersion` for every upload to Google Play.

---

## F-Droid Distribution

### Why F-Droid?

F-Droid is an open-source app store. DotNetCloud's F-Droid build contains **zero proprietary dependencies** — using UnifiedPush instead of FCM for push notifications.

### F-Droid Metadata

Create `metadata/net.dotnetcloud.client.fdroid.yml`:

```yaml
Categories:
  - Internet
  - System
License: AGPL-3.0-only
AuthorName: DotNetCloud Project
AuthorWebSite: https://dotnetcloud.net
SourceCode: https://github.com/LLabmik/DotNetCloud
IssueTracker: https://github.com/LLabmik/DotNetCloud/issues

AutoName: DotNetCloud
Description: |
  Self-hosted cloud platform with chat, file sync, and collaboration.
  Privacy-focused alternative to commercial cloud services.

  Features:
  * Real-time chat with channels, threads, and reactions
  * File synchronization across devices
  * Self-hosted — full data ownership
  * UnifiedPush notifications (no Google dependencies)

RepoType: git
Repo: https://github.com/LLabmik/DotNetCloud.git

Builds:
  - versionName: 0.1.0-alpha
    versionCode: 1
    commit: v0.1.0-alpha
    subdir: src/Clients/DotNetCloud.Client.Android
    sudo:
      - apt-get update
      - apt-get install -y dotnet-sdk-10.0
    build:
      - dotnet publish -c Release -f net10.0-android -p:BuildFlavor=fdroid

AutoUpdateMode: Version
UpdateCheckMode: Tags
CurrentVersion: 0.1.0-alpha
CurrentVersionCode: 1
```

### Submission Process

1. Fork the [F-Droid Data](https://gitlab.com/fdroid/fdroiddata) repository.
2. Add the metadata YAML file.
3. Submit a merge request.
4. F-Droid team reviews build reproducibility and FOSS compliance.
5. App appears in the F-Droid repository after merge.

### Self-Hosted F-Droid Repository

For private/enterprise distribution, host your own F-Droid repo:

1. Install `fdroidserver`:
   ```bash
   pip install fdroidserver
   ```
2. Initialize the repo:
   ```bash
   fdroid init
   ```
3. Copy the signed APK to `repo/`.
4. Update the repo index:
   ```bash
   fdroid update
   ```
5. Serve the `repo/` directory via HTTPS.
6. Users add the repo URL in the F-Droid app.

---

## Direct APK Distribution

For organizations that sideload or use MDM (Mobile Device Management):

1. **Build the release APK:**
   ```powershell
   dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj `
     -c Release -f net10.0-android `
     -p:AndroidPackageFormat=apk
   ```

2. **Generate SHA-256 checksum:**
   ```powershell
   Get-FileHash -Algorithm SHA256 "artifacts/publish/*.apk" | Format-List
   ```

3. **Distribute:**
   - Host on an internal download page with the checksum.
   - Users enable "Install from unknown sources" on their device.
   - Provide install instructions.

> **Security:** Always distribute with a checksum and over HTTPS. Consider signing with a well-known key for enterprise MDM scenarios.

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Android Release
on:
  push:
    tags: ['v*']

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet workload install android

      # Google Play build
      - run: |
          dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj \
            -c Release -f net10.0-android \
            -p:AndroidKeyStore=true \
            -p:AndroidSigningKeyStore=${{ github.workspace }}/release.keystore \
            -p:AndroidSigningKeyAlias=dotnetcloud \
            -p:AndroidSigningKeyPass=${{ secrets.SIGNING_KEY_PASS }} \
            -p:AndroidSigningStorePass=${{ secrets.SIGNING_STORE_PASS }}

      # F-Droid build
      - run: |
          dotnet publish src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj \
            -c Release -f net10.0-android -p:BuildFlavor=fdroid

      - uses: actions/upload-artifact@v4
        with:
          name: android-release
          path: artifacts/publish/**/*.apk
```

---

## Versioning Checklist

Before each release:

- Update `ApplicationDisplayVersion` in the `.csproj`
- Increment `ApplicationVersion` integer
- Update `CurrentVersion` and `CurrentVersionCode` in F-Droid metadata
- Tag the release commit: `git tag v0.1.0-alpha`
- Build both flavors and verify signing
- Generate checksums for direct APK distribution
