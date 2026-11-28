# Security policy

DotNetCloud is a self-hosted platform, and its modules are separate processes that a third party can
write. This page says where a report goes, who owns what, and what to expect back.

## Report a vulnerability

**Email:** [security@dotnetcloud.net](mailto:security@dotnetcloud.net)

The full policy — what to include, the disclosure timeline and the severity-based fix windows — is
[`docs/security/VULNERABILITY_DISCLOSURE.md`](docs/security/VULNERABILITY_DISCLOSURE.md). That
document is the authority; this page only routes your report.

Do not open a public issue for a vulnerability, and do not include a working exploit in a public
place. A private report and a reproduction are enough to start.

## Who owns what

| You found a flaw in…                                              | It belongs to | What happens                                  |
| ----------------------------------------------------------------- | ------------- | --------------------------------------------- |
| The core server, the supervisor, the CLI, the packaging format, a contract package (`DotNetCloud.Core`, `.Core.Data`, `.Core.Grpc`, `.Core.Packaging`, `.Modules.Sdk`, `.Modules.Sdk.Web`, `DotNetCloud.UI.Shared`), the desktop or Android client | The platform | Fix and release from this repository |
| A module's own behaviour, code or data handling                   | The module's author | The platform routes the report to the publisher |
| A **published** package's bytes, its signature, or the signed catalogue | The DotNetCloud module store | Revocation and takedown, see below |

A sandbox escape, a capability check that can be bypassed, a forged user assertion, or a contract
flaw that lets one module reach another module's data is a **platform** finding even when the
triggering code lives in a module: report it here.

## What an author should expect

- An acknowledgement within **48 hours**, and a triage decision within **5 business days**.
- The fix windows listed in the disclosure policy: critical 7 days, high 14, medium 30, low 90.
- Credit in the release notes unless you ask not to be named.

A module author receiving a report about their own module gets the same routing and the same
timeline; the platform is not the arbiter of a publisher's fix, but it can stop distributing the
package.

## A vulnerable published module

A **published** module is handled by the store, not by this repository. The process — who may
request a takedown, what a yank does, and how the catalogue is re-signed — is
`policy/REVOCATION_RUNBOOK.md` in the `DotNetCloud-ModuleStore` repository, which serves
<https://modules.dotnetcloud.net>. Published `(id, version)` bytes never change, so the remedy is a
yanked version plus a fixed one, never an edited package.

## Scope

In scope and out of scope, including third-party services and infrastructure-level denial of
service, are listed in [`docs/security/VULNERABILITY_DISCLOSURE.md`](docs/security/VULNERABILITY_DISCLOSURE.md#scope).
