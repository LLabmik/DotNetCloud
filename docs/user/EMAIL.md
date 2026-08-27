# Email — User Guide

> **Last Updated:** 2026-08-27

---

## Welcome

DotNetCloud Email is a web-based email client. Add one or more email accounts, read and organize your messages, and compose and send email — all from your browser, without a separate desktop mail program.

---

## Accounts

### Adding an Account

1. Open **Email** from the left sidebar
2. Click **+ Account**
3. Choose an account type:
   - **IMAP / SMTP** — enter your server, port, username, and password
   - **Gmail (OAuth)** — if configured by your administrator, sign in via Google
4. Your messages begin syncing

### Switching Accounts

Use the account selector in the sidebar to switch between accounts you've added.

### Syncing

Click **Sync Now** to fetch new messages immediately. Otherwise, sync happens automatically on an interval.

### Removing an Account

Click **Delete Account** to remove an account and its synced messages.

---

## Composing Messages

1. Click **+ Compose** to open the message editor
2. Pick the account to send from
3. Enter the recipient(s), with optional **Cc** and **Bcc**
4. Add a subject and the message body

Recipient suggestions appear as you type.

### Attachments

- **Attach from computer** — upload files from your device
- **Browse Files** — attach files directly from your DotNetCloud Files library

### Replying & Forwarding

Use **Reply** and **Forward** from an open message. Closing the compose window discards the draft.

---

## Folders

The sidebar lists the mailboxes synced from your account — for example **Inbox**, **Sent**, **Drafts**, **Spam**, and **Trash** (depending on your mail server). Click a folder to view its message threads.

---

## Reading Messages

Click a thread to open it. Attachments can be downloaded, and messages with stored attachments can be saved to your Files library with the folder button.

---

## Rules

Automate how incoming messages are handled:

1. Open **Rules** in the sidebar
2. Create a rule with:
   - **Conditions** — subject, from, to, body, cc, or "has attachment"
   - **Actions** — mark read/unread, star/unstar, apply a label, move to a folder, or archive
3. Rules run in priority order and can be enabled or disabled individually

Use **Run Rules Now** to apply rules to messages already in your mailbox.

---

## Tips & Tricks

- Add multiple email accounts and switch between them with the account selector.
- Create rules to automatically organize incoming email into folders or labels.
- Attach files directly from your Files library with **Browse Files**.

---

## Troubleshooting

| Issue                  | What to Do                                                                                                      |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- |
| Can't add an account   | Double-check your IMAP/SMTP server, port, and credentials; Gmail OAuth must be configured by your administrator |
| Messages not syncing   | Click **Sync Now**; if it still fails, check your connection and account settings                               |
| Attachments won't send | Verify the attachment size and your account's send limits                                                       |
| Rules not applying     | Check that the rule is enabled and that its conditions match the messages you expect                            |

---

## Related Guides

- [Getting Started](GETTING_STARTED.md) — attach and manage files from your Files library
- [Search](SEARCH.md) — cross-module search
- [Bookmarks](BOOKMARKS.md) — save links from email messages
