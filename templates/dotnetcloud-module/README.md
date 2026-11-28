# MyModule

A DotNetCloud module, scaffolded from the `dotnetcloud-module` template.

| | |
| --- | --- |
| Module id | `org.example.mymodule` |
| Version | `0.1.0` |
| Platform SDK | `__SDK_VERSION__` |
| Data store | none — the module keeps no per-user data |

This skeleton is deliberately small: a lifecycle service, a health check and a manifest. The
**Example** module in the platform repository is the exhaustive reference for capabilities, events,
an isolated data store, a framed UI and the account-erasure contract.

## Build

```bash
dotnet build
```

The module contracts (`DotNetCloud.Core`, `DotNetCloud.Core.Grpc`, `DotNetCloud.Modules.Sdk`) come
from the `sdk-feed` folder of the DotNetCloud release you installed. `NuGet.config` already maps
`DotNetCloud.*` to that folder; if you moved the feed, change the `dotnetcloud-sdk` source or
regenerate with `--sdkFeed`.

## Run it

A module is a separate process supervised by a running DotNetCloud server, so there is nothing to
`dotnet run` on its own. With the server running in Development:

```bash
dotnetcloud module dev .            # publish, deploy and health-check in one step
dotnetcloud module dev . --watch    # do it again on every save
```

`module dev` refuses outside Development, records an audit entry per deploy, and shows the deployed
build in `dotnetcloud module info`.

## Package and submit

```bash
dotnetcloud module keygen --key-id org.example-2026 --output org.example-2026.key.json
dotnetcloud module pack --project DotNetCloud.Modules.MyModule.Host \
    --rid linux-x64 --rid win-x64 --sign-key org.example-2026.key.json
```

Then follow the submission guide on the DotNetCloud module store, including the source archive the
official tier requires.

## Before you publish

- Replace `org.example.mymodule` with your own reverse-DNS id (assembly name, directory name and
  `manifest.json` must agree), and the publisher block in `manifest.json`.
- Replace `LICENSE` with the licence you distribute the module under, and fill in `CHANGELOG.md`.
- Declare exactly the capabilities and consent scopes the module uses; the store review checks them
  against the package.
