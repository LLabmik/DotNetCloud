# Android Chat Alert Sound — fix + background-delivery findings

**Date:** 2026-09-17
**Branch:** `fix/more-android-improvements`
**Symptom reported:** "user does not hear an alert sound when a new message arrives, whether app is
foreground or background."

## Root causes

### 1. Foreground: no alert existed at all (fixed)

All three notification paths (`SignalRChatClient.PostSignalRNotification`, `FcmMessagingService`,
`UnifiedPushReceiver`) deliberately suppress notifications while
`IAppForegroundService.IsInForeground` is true — correct, since the user is already looking at the
app — but the Android client never had an in-app sound. The web client has had one since
`4029ae5a` (Blazor `chat-sound.js` + `ShouldPlayMessageSound`); the port was simply never done, so a
message that arrived on screen was completely silent.

Device evidence (logcat, app visible, message from another user):

```
SignalRChatClient: NewMessage received! channelId=019fd4f0-…, content='test aga8in', senderName='Test Dude'
SignalR notification: foreground=True, channelId=019fd4f0-…        ← old code: no branch taken, no sound
```

### 2. Background: no reachable push transport (partly fixed, needs configuration)

Three independent problems, in order of impact:

| #   | Problem                                                                                                                                                                                                                                                                                 | Evidence                                                                                                                                                                |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2a  | **The googleplay build has no Firebase configuration.** There is no `google-services.json` anywhere in the repo, so the APK carries no `google_app_id`/`gcm_defaultSenderId` resources and `FirebaseApp` never initialises → `GetToken()` can never return a token.                     | `Push registration failed: Default FirebaseApp is not initialized in this process net.dotnetcloud.client…`; resource names absent from the built APK's `resources.arsc` |
| 2b  | **Push device registrations live in memory on the server** (`NotificationRouter._deviceMap`, `FcmPushProvider._registrations`) and are lost on every module-host restart, while the client only re-registered when Firebase rotated a token (i.e. essentially never — no self-healing). | code inspection of `src/Modules/Chat/.../NotificationRouter.cs`                                                                                                         |
| 2c  | **The server suppresses push whenever the user appears online** (`NotificationRouter.CanSendPushAsync` → `IsPresenceTracker.IsOnlineAsync`), and the phone's own hub connection counts. While that connection is alive-but-frozen the message is delivered to nobody.                   | `NotificationRouter.cs` lines ~180-185                                                                                                                                  |

Background delivery _does_ work while the app process is alive and unfrozen — SignalR posts a
notification on the sounding `chat_messages` channel (verified live, see below). It stops working
once Android freezes/reclaims the cached process: the sticky `ChatConnectionService` is no longer a
foreground service (removed 2026-09-12, `faae32c9`, because Android 15/16 caps `dataSync` to a
24-hour budget and the kill loop was structural), and without FCM nothing can wake the process.

On-device channel configuration is healthy — `chat_messages` has `mImportance=3` (DEFAULT) and
`mSound=content://settings/system/notification_sound`, and `POST_NOTIFICATIONS` is allowed.

## Implemented (Android client)

- `Services/IChatSoundPlayer.cs` + `Platforms/Android/AndroidChatSoundPlayer.cs` — plays the web
  client's own `chat-ding.mp3` (packaged as `Platforms/Android/Resources/raw/chat_ding.mp3`) through
  `SoundPool` tagged `USAGE_NOTIFICATION`/`CONTENT_TYPE_SONIFICATION`, so the ding follows the same
  silent/DND/volume rules as a notification rather than the media stream. Loaded at connect time so
  the first message of a process is not swallowed while it decodes; a play request that races the
  load is deferred instead of lost.
- `Services/ChatAlertPolicy.cs` — one decision function, exactly one alert per message:
  muted channel → nothing; app visible → ding (unless the user's own echo); app not visible →
  system notification (unchanged behavior). Own-message echoes never ding; an unresolvable current
  user only disables that check, it never silences the alert.
- `Services/ChatSoundSettings.cs` — preference `chat_message_sound_enabled` (default on).
- `Chat/SignalRChatClient.cs` — the `NewMessage` handler now routes through `ChatAlertPolicy`
  (logging `decision=`), resolves the signed-in user from the id_token at connect, and prepares the
  sound. `PostSignalRNotification` is unchanged.
- `ViewModels/SettingsViewModel.cs` + `Views/SettingsPage.xaml` — "Message Sound" switch in
  Settings → CHAT NOTIFICATIONS; switching it on previews the ding.
- `App.xaml.cs` + `Views/LoginPage.xaml.cs` — push device registration is re-sent on **every app
  start and after every login** (fixes 2b's self-healing half).
- `Services/FcmPushService.cs` — an explicitly actionable warning when no token is available.
- Tests: `tests/DotNetCloud.Client.Android.Tests/Chat/ChatAlertPolicyTests.cs` (10 tests).

## Verification (device R5CWC356B2K, 2026-09-17)

| Case                                | Result                                                                                                                                                                |
| ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Sound resource loads                | `[AndroidChatSoundPlayer] Chat alert sound loaded (soundId=1)`                                                                                                        |
| Ding actually plays                 | `dumpsys audio`: `piid:8471 … package:net.dotnetcloud.client type:android.media.SoundPool attr:usage=USAGE_NOTIFICATION` + `event:started` at the moment of the alert |
| **Foreground, live remote message** | `SignalR chat alert: decision=InAppSound, foreground=True` → `playing ding…` → audio `event:started` (operator's own test message)                                    |
| **Background, live remote message** | `SignalR chat alert: decision=SystemNotification, foreground=False` → notification posted on `channel=chat_messages` (sounding channel)                               |
| Own message echo                    | policy returns `None` (no self-ding)                                                                                                                                  |
| Build / tests                       | Android arm64 Debug: 0 warnings, 0 errors; Android tests 357 passed / 1 skipped                                                                                       |

## Remaining work (background alerts while the app is closed)

1. **Firebase project + `google-services.json` (operator, client side).** The googleplay flavour
   needs the config file whose `google_app_id` matches the Firebase project the server signs FCM
   sends with. Add it to `src/Clients/DotNetCloud.Client.Android/` (or supply
   `-p:GoogleServicesJson=<path>`); the client half is already self-healing once a token exists.
   _(The F-Droid flavour + a UnifiedPush distributor is the alternative.)_
2. **Server: FCM credentials** (`Chat:Push:Fcm` → `ProjectId`, `CredentialsPath`) for the same
   Firebase project, otherwise sends fail at the transport.
3. **Server: persist push device registrations** instead of an in-memory dictionary, so a module-host
   restart no longer forgets every device (self-healing registration mitigates, but a device that is
   offline during the restart window stays forgotten).
4. **Optional:** stop counting a bridged/mobile connection as presence for push suppression, or let
   the client mark a connection as background/delivery-only, to close the frozen-process window.

Verification for 1–3: with the app force-stopped (`adb shell am force-stop net.dotnetcloud.client`),
have a second user send a message and confirm the notification appears and sounds.
