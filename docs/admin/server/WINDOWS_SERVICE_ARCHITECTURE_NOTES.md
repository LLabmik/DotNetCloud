# Windows Hosting Architecture Notes (Option 2)

## Decision

Use **IIS reverse proxy + Windows Service-hosted DotNetCloud core host**.

This means:

- IIS is the public edge for HTTP/HTTPS.
- DotNetCloud runs privately on Kestrel (default `http://localhost:5080`).
- A Windows Service keeps DotNetCloud alive independently of IIS app pool lifecycle.

## Why Option 2 Fits DotNetCloud

DotNetCloud is not only a web front-end. It is a modular host/supervisor platform.

Option 2 aligns better with that model because:

1. Host lifecycle is stable and always-on (boot/start/recovery) via Windows Service Control Manager.
2. IIS remains focused on edge concerns (bindings, TLS, reverse proxy, request filtering).
3. Application lifecycle is decoupled from IIS app pool idle/recycle behavior.
4. Background and supervisory responsibilities have a proper long-running host process.
5. The architecture remains consistent with Linux reverse-proxy deployment guidance.

## What Requires a Windows Service

In Option 2, the Windows Service is for the **core DotNetCloud host process**.

It is **not** required for:

- IIS itself (already managed by Windows).
- static site assets.
- one-time setup wizard execution.

## What Is "Just a Windows Web App" (Option 1)

Option 1 would host the ASP.NET Core app directly in IIS (ANCM/in-process or IIS-managed out-of-process), with no separate service for the backend host.

That can work for simple web apps, but it is less ideal when the application acts as a broader platform host/supervisor.

## Operational Tradeoffs

### Benefits of Option 2

- Stronger backend reliability model.
- Clear split of responsibilities (IIS edge vs app host runtime).
- Better future support for module supervision and non-request-driven workloads.

### Costs of Option 2

- More installer complexity.
- Service configuration/recovery setup is required.
- More explicit environment/config path management needed.

These costs are mostly one-time infrastructure work and are acceptable for long-term platform stability.

## Configuration Boundaries To Keep Clear

For a machine-wide Windows deployment:

- Use machine-level config/data/log roots (for example under `C:\ProgramData\DotNetCloud`).
- Do not rely on per-user profile defaults for production service execution.
- Ensure service runtime environment includes required variables (for example config/data roots and production environment).

## Recommended Windows Deployment Shape

1. Install DotNetCloud binaries to machine scope.
2. Run setup wizard with machine-level configuration paths.
3. Register/start a proper Windows Service for the core host.
4. Configure IIS site + app pool + reverse proxy rule to `localhost:5080`.
5. Handle certificates and public bindings at IIS.
6. Validate health endpoint and service recovery behavior.

## Implementation Guardrails

- Do not mix hosting models in the same path (avoid half IIS-hosted + half service-hosted behavior).
- Keep Kestrel private; expose public traffic through IIS.
- Treat Windows installer and docs as a first-class path, independent from Linux installer logic.

## Long-Term Product Direction

If a graphical installer is added later, it should be a UI layer over this same backend installation flow, not a separate implementation path.

That preserves one authoritative installation engine and avoids drift between CLI/script and GUI behavior.
