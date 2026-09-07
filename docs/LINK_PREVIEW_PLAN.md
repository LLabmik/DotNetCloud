# Chat Link Previews (Blazor Web + Android MAUI)

**Status:** implemented on `fix/chat-improvements` (2026-09-07) — code complete, server + Blazor + Android.
**Verification:** builds clean; unit tests pass; live end-to-end (real server + two clients) still to be run (see below).

## Goal

When a chat message contains an http(s) URL, show a rich **link preview card** under that
message (title, description, site/favicon, thumbnail) in the Blazor web chat
(`src/Modules/Chat/DotNetCloud.Modules.Chat/UI`) and the Android MAUI chat
(`src/Clients/DotNetCloud.Client.Android`). Tapping/clicking the card opens the URL.

## Design decisions (confirmed)

- **Server-side fetch at send time** — the Chat module fetches the page metadata when a
  message is created and **persists it with the message**, so history always shows the
  preview and every client renders from the same DTO. No client-side unfurling.
- **After send only** — no live preview while composing (future work).
- **Full SSRF guard** on the server fetch: only http/https, private/loopback/link-local/
  CGNAT IP literals blocked, manual redirect handling with per-hop re-validation, DNS
  re-resolution at connect time (mitigates rebinding), response size cap (768 KB), strict
  timeouts (connect 3 s / overall 6 s) so a slow page never delays a message send.
- **Synchronous (bounded) capture inside `MessageService.SendMessageAsync`** — the message
  row is saved first, then the preview is fetched + persisted best-effort, then the DTO is
  returned and broadcast. Because unfurling happens inside `MessageService`, **every** send
  path (Blazor in-process, module-host REST used by Android, DM accept/reply) and the
  realtime broadcast automatically carry the preview — no separate “preview ready” event and
  no `chat_service.proto` change are needed (the realtime `NewMessage` payload serializes the
  full `MessageDto` as JSON through both the in-process broadcaster and the module host's
  `GrpcRealtimeBroadcaster`).
- Edits re-run detection: changing the first URL refreshes the preview; removing it deletes it.
- Reuses the codebase's existing pattern: mirrors the **Bookmarks module** `SafeUrlFetcher` +
  AngleSharp metadata extraction (module-local copy, since modules can't reference each other).
  `AngleSharp` was already centrally pinned in `Directory.Packages.props`.

## Changes

### Server / shared

| File                                                                                              | Change                                                                                                                                                                                                                                        |
| ------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/MessageLinkPreview.cs`                          | **New** 1:0..1 entity (Url, Title, Description, ImageUrl, SiteName, FaviconUrl, FetchedAt).                                                                                                                                                   |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/Message.cs`                                     | `MessageLinkPreview? LinkPreview` navigation.                                                                                                                                                                                                 |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs`                                      | New `MessageLinkPreviewDto`; `MessageDto.LinkPreview`.                                                                                                                                                                                        |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Configuration/MessageLinkPreviewConfiguration.cs` | **New** EF config (unique `MessageId`, cascade delete).                                                                                                                                                                                       |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatDbContext.cs`                                 | `DbSet<MessageLinkPreview>` + `ApplyConfiguration`.                                                                                                                                                                                           |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ILinkPreviewService.cs`                       | **New** interface + `LinkPreviewResult`; `FindFirstUrl(content)` + `FetchPreviewAsync(url, ct)`.                                                                                                                                              |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/SafeUrlFetcher.cs`                       | **New** SSRF-safe fetcher (see above). `IsPrivateOrSpecialIp`/`IsBlockedIp` are `internal` for tests.                                                                                                                                         |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/LinkPreviewService.cs`                   | **New** unfurl impl: URL detection (skips code fences/indented code/media URLs), AngleSharp extraction (og → twitter → `<title>`/meta description/canonical/favicon), 1 h in-memory cache. `ParsePreviewHtml` is `internal static` for tests. |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/MessageService.cs`                       | Optional `ILinkPreviewService`; send captures preview after first save; edit refreshes/removes; `GetMessages/Search/GetMessage` `.Include(LinkPreview)`; DTO mapping.                                                                         |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs`                       | Registers `SafeUrlFetcher` + `ILinkPreviewService` (single registration point used by Core.Server **and** Chat.Host).                                                                                                                         |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/DotNetCloud.Modules.Chat.Data.csproj`             | `<PackageReference Include="AngleSharp" />`.                                                                                                                                                                                                  |
| Migrations                                                                                        | `AddMessageLinkPreview` (Postgres, Chat.Data) + `AddMessageLinkPreview_SqlServer` (Chat.Data.SqlServer).                                                                                                                                      |

### Blazor web chat

- `UI/ViewModels.cs`: `LinkPreviewViewModel` + `MessageViewModel.LinkPreview`.
- `UI/ChatPageLayout.razor.cs`: map `dto.LinkPreview` in `ToMessageViewModel` (covers history,
  send, remote realtime + edits — the remote handler already maps through this method).
- `UI/MessageList.razor`: preview card under the content (site row with favicon, title,
  description, optional thumbnail; links open in a new tab). Rendered by the shared
  `MessageList`, so **DMs** (`DirectMessageView`) get it automatically.
- `UI/MessageList.razor.css`: card styling using existing theme CSS variables.

### Android MAUI chat

- `Chat/IChatRestClient.cs`: `ChatLinkPreview` record; trailing optional `LinkPreview` on
  `ChatMessage`; `SignalRLinkPreviewDto` (SignalR mirror, JSON-name attributed).
- `Chat/HttpChatRestClient.cs`: parse `linkPreview` into `ChatMessage`.
- `Chat/SignalRChatClient.cs`: `SignalRMessageDto.linkPreview`; serialize + pass preview JSON.
- `src/Clients/DotNetCloud.Client.Core/IChatSignalRClient.cs`: trailing optional
  `LinkPreviewJson` on `ChatMessageReceivedEventArgs` (shared with SyncTray — additive).
- `ViewModels/MessageListViewModel.cs`: `MessageItemViewModel` carries the preview and exposes
  display props (`HasLinkPreview`, title/desc/image/site label); history/load-more/send/search/
  realtime sites pass it; `OpenLinkPreviewCommand` opens via `Launcher`.
- `Views/MessageListPage.xaml`: preview card in the message bubble (thumbnail + text), tap opens
  the link; hidden when there is no preview.

### Tests

- `tests/DotNetCloud.Modules.Chat.Tests/LinkPreviewServiceTests.cs` — URL detection
  (multi-URL, punctuation, code fences/indented code, media URLs, non-http) + metadata
  extraction (og/twitter/html fallbacks, relative-image resolution, empty page → null).
- `tests/DotNetCloud.Modules.Chat.Tests/SafeUrlFetcherTests.cs` — SSRF IP guard
  (private ranges, loopback, link-local, CGNAT, IPv4-mapped IPv6; public IPs allowed).
- `tests/DotNetCloud.Modules.Chat.Tests/MessageServiceLinkPreviewTests.cs` — send captures +
  persists preview; no URL → none; fetch failure → message still sends without preview;
  edit refresh on URL change; edit removal deletes preview.

## Verification

### Automated (done)

- `dotnet build` clean (0 warnings) for: Chat module + Data + Data.SqlServer + Chat.Host,
  Core.Server (web), Client.Core, SyncTray, Android app (`net10.0-android` arm64 Debug).
- `dotnet test` Chat module **1384/1384**; Android.Tests **268 pass / 1 skip**;
  Client.Core **302 pass** (+3 pre-existing temp-db file-lock failures); Core.Server
  **638 pass** (+1 pre-existing root-CA path test failure).

### Live E2E (operator — cannot run in this repo-only environment)

1. Deploy (web + module host) and apply the new migrations (Postgres + SQL Server).
2. In a web browser, post a message containing a link (e.g. `https://github.com/LLabmik/DotNetCloud`)
   → a preview card appears under the sent message.
3. From a second browser / the Android app in the same channel, confirm the card appears
   real-time (no reload) and persists after page refresh (history).
4. From Android, send a message with a link → card appears on the web + Android.
5. Edit the message to a different URL → card refreshes; edit to no URL → card disappears.
6. Negative: post `http://192.168.1.1` (or a loopback/`10.x` link) → message sends but **no**
   preview (SSRF block); an image-only URL (e.g. `.png`) → no card.
7. Tap the card on web (opens new tab) and on Android (opens browser).

## Notes / follow-ups

- Preview is captured once per message at send/edit time. A per-URL in-memory cache (1 h TTL,
  ≤512 entries) avoids re-fetching the same link across messages; cache lives in the Chat
  module process.
- Offline/queued messages and the Android local cache do not carry previews (acceptable —
  cache is only for offline display).
- Future options: async unfurl with a second “preview ready” event (would remove the bounded
  send-time fetch), composer live-preview confirmation, configurable per-user toggle.
- ⚠️ New files under `src/Modules/**` are matched by the `.gitignore` `modules/` rule and must
  be added with `git add -f` when committing (pre-existing repo quirk).
