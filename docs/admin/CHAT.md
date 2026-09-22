# Chat Module — Administrator Guide

> **Applies to:** DotNetCloud Chat module (`dotnetcloud.chat`)
> **Admin page:** `/admin/chat`
> **Design record:** `docs/CHAT_ADMIN_SETTINGS_PLAN.md`

---

## What this page controls

`/admin/chat` decides how much chat history and attachment data the server keeps. Limits are
enforced as messages and attachments are written; the retention policy is applied in the
background.

| Section                   | Setting                                     | Default | Notes                                                |
| ------------------------- | ------------------------------------------- | ------- | ---------------------------------------------------- |
| **Message Limits**        | Maximum message length                      | 10 000  | Characters. Longer messages are rejected.            |
|                           | Maximum messages per channel                | 0       | `0` = unlimited. Oldest messages expire once passed. |
| **Attachment Limits**     | Maximum attachments per message             | 10      | Applies to inline attachments and image uploads.     |
|                           | Maximum size of one attachment (MB)         | 10      | 1–64 MB.                                             |
|                           | Maximum attachments per channel             | 0       | `0` = unlimited. New attachments are rejected.       |
|                           | Maximum attachment storage per channel (MB) | 0       | `0` = unlimited. Uploads over the cap are rejected.  |
| **Retention & Archiving** | Enable automatic retention                  | off     | Master switch for expiry.                            |
|                           | Message lifetime (days)                     | 0       | `0` = keep forever.                                  |
|                           | When a message expires                      | Archive | `Archive` hides and keeps; `Purge` deletes for good. |
|                           | Keep attachments of archived messages       | on      | Off = only the message text is kept.                 |
|                           | Run the sweep every (minutes)               | 60      | 1–1440.                                              |

## Archive vs purge

- **Archive** (default) sets an `ArchivedAt` stamp. The message disappears from channels and from
  search but stays in the database, so an MSA/administrator can still recover it with database
  access or a future restore tool. Attachments are kept unless you turn off
  _Keep attachments of archived messages_.
- **Purge** permanently deletes the messages, their attachments, their pins, and their reactions,
  mentions and link previews. Replies to a purged message survive with their link detached.

Archiving is the safe choice for compliance retention; purge is for data you are required to
erase.

## Tips

- **A channel never becomes read-only.** If you set _Maximum messages per channel_, the channel
  keeps accepting messages and the oldest ones expire instead.
- **Retention needs a rule to act on.** Enabling retention without a message lifetime and without
  a per-channel message cap does nothing, and the page warns you about it.
- **Archiving frees attachment capacity.** Only attachments on live messages count toward the
  per-channel attachment limits, so raising the retention aggressiveness gives users room again.
- **Apply changes immediately.** After saving, use **Run retention sweep now** to apply the new
  policy without waiting for the next scheduled pass (the sweep returns a summary of what it did).
- **Rollout is gradual.** A single sweep expires at most 5 000 messages per channel; the rest is
  handled by the following passes.
- **The module must be running.** The _Currently Enforced_ readout and _Run retention sweep now_
  need the Chat module host to be reachable; saving settings works regardless.

## Configuration fallback

The database is the source of truth. `config.json` (and environment variables) are only read for
keys that have no database row, which is useful for a standalone module host:

```json
{
  "Chat": {
    "Limits": {
      "MaxMessageLength": 10000,
      "MaxMessagesPerChannel": 0,
      "MaxAttachmentsPerMessage": 10,
      "MaxAttachmentsPerChannel": 0,
      "MaxAttachmentSizeMb": 10,
      "MaxAttachmentStoragePerChannelMb": 0
    },
    "Retention": {
      "Enabled": false,
      "MessageLifetimeDays": 0,
      "Mode": "Archive",
      "ArchiveAttachments": true,
      "SweepIntervalMinutes": 60
    }
  }
}
```

## Related admin pages

- `/admin/push-notifications` — FCM push delivery for chat.
- `/admin/settings?module=dotnetcloud.chat` — raw key/value view of every chat setting.
