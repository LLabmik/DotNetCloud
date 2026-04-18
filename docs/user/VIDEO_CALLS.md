# Video & Audio Calls

> **Last Updated:** 2026-04-15

---

## Overview

DotNetCloud lets you make video and audio calls directly from any Chat channel — public channels, private channels, direct messages, and group DMs. You can also share your screen during a call.

---

## Starting a Call

### From a Channel

1. Open any Chat channel or direct message
2. Click the **phone** (audio) or **video camera** icon in the channel header
3. All channel members receive an incoming call notification
4. The call enters a **Ringing** state for up to 30 seconds

### Call Types

| Type | Description |
|---|---|
| **Audio call** | Voice only — lower bandwidth, ideal for quick conversations |
| **Video call** | Camera and microphone — full face-to-face experience |

---

## Answering a Call

When someone calls you, an incoming call notification appears in the chat interface:

- Click **Accept with Video** to join with your camera on
- Click **Accept with Audio** to join with audio only (camera off)
- Click **Reject** to decline the call

If you don't answer within 30 seconds, the call is marked as **Missed**.

---

## During a Call

### Call Controls

The call dialog provides these controls:

| Button | Action |
|---|---|
| **Mute / Unmute** | Toggle your microphone on or off |
| **Camera On / Off** | Toggle your camera on or off |
| **Share Screen** | Start or stop sharing your screen |
| **Minimize** | Shrink the call to a small picture-in-picture window |
| **Hang Up** | Leave the call |

### Screen Sharing

1. Click the **Share Screen** button during a call
2. Your browser will ask you to choose what to share:
   - **Entire screen** — shares everything on your display
   - **Application window** — shares a specific app window
   - **Browser tab** — shares a single browser tab
3. Click **Stop Sharing** to end the screen share

### Picture-in-Picture

Click **Minimize** to shrink the call dialog into a small floating window. This lets you continue browsing channels and reading messages while staying on the call. Click the floating window to return to the full call view.

---

## Group Calls

- Any channel with 2 or more members supports calls
- Only **one call** can be active in a channel at a time
- All channel members can join an active call at any time
- For small groups (1–3 participants), calls use direct peer-to-peer connections
- For larger groups (4+ participants), your admin may have configured a media server (LiveKit) for better quality

---

## Call History

To view past calls in a channel:

1. Open the channel
2. Click the **Call History** icon in the channel header (clock icon)
3. Browse past calls showing:
   - Who started the call
   - When it happened
   - Duration
   - Whether it was answered, missed, or rejected
4. Click **Call Back** on any history entry to start a new call

---

## Notifications

### Incoming Calls

When someone calls you:
- An in-app notification appears immediately
- If you have push notifications enabled, your mobile device receives a push notification
- Calls ring for 30 seconds before being marked as missed

### Missed Calls

If you miss a call:
- A push notification is sent to your registered devices
- The call appears in the channel's call history as **Missed**

### Do Not Disturb

If your presence status is set to **Do Not Disturb**, call notifications are still delivered (calls are treated as high-priority). To silence all notifications including calls, mute the specific channel.

---

## Troubleshooting

### Call won't connect

- Check that your browser has permission to access your camera and microphone
- Ensure you're not behind a restrictive firewall that blocks UDP traffic
- Try refreshing the page and rejoining

### No audio or video

- Check your browser's site permissions (camera/microphone)
- Verify the correct input device is selected in your browser settings
- Some browsers require HTTPS for WebRTC — ensure you're using `https://`

### Poor call quality

- Close other bandwidth-heavy applications
- Switch to audio-only if video quality is poor
- Move closer to your Wi-Fi router or use a wired connection
- For group calls with many participants, ask your admin about LiveKit SFU setup

### Screen sharing not available

- Screen sharing requires a modern browser (Chrome, Edge, Firefox, Safari 13+)
- Some browsers require HTTPS for the `getDisplayMedia` API
- Screen sharing is a browser-only feature — it is not yet available in the desktop or mobile apps

---

## Related

- [Getting Started](GETTING_STARTED.md)
- [Chat Module Overview](../modules/chat/README.md)
