# Calendar — User Guide

> **Last Updated:** 2026-07-16

---

## Welcome

DotNetCloud Calendar lets you manage calendars and events, invite attendees, set reminders, and sync with external calendar applications via CalDAV.

---

## Managing Calendars

### Creating a Calendar

1. Open **Calendar** from the left sidebar
2. Click **New Calendar**
3. Enter a name for the calendar
4. Optionally set:
   - **Description** — what this calendar is for
   - **Color** — a hex color code for visual distinction
   - **Timezone** — IANA timezone identifier (e.g., `America/New_York`). Defaults to UTC.
5. Click **Create**

You can have multiple calendars (e.g., "Work", "Personal", "Holidays").

### Editing a Calendar

1. Click the settings icon next to the calendar name
2. Update the name, description, color, or timezone
3. Click **Save**

### Hiding a Calendar

Toggle the visibility checkbox next to a calendar name to show or hide its events in the calendar view without deleting it.

### Deleting a Calendar

1. Click the settings icon next to the calendar
2. Click **Delete**
3. The calendar and all its events are soft-deleted

---

## Managing Events

### Creating an Event

1. Click on a date/time in the calendar view, or click **New Event**
2. Fill in the event details:
   - **Title** (required)
   - **Calendar** — which calendar to add it to
   - **Start** and **End** date/time (required)
   - **All Day** — toggle for full-day events
   - **Location** — free text
   - **Description** — supports Markdown
   - **Color** — override the calendar's default color
   - **URL** — link to related content
3. Add **Attendees** (see Invitations below)
4. Add **Reminders** (see Reminders below)
5. Click **Save**

### Editing an Event

1. Click on an event in the calendar view
2. Click **Edit**
3. Update any fields
4. Click **Save**

### Deleting an Event

1. Click on an event
2. Click **Delete**
3. The event is soft-deleted

### Searching Events

1. Click the **Search** icon in the toolbar
2. Enter keywords to search event titles, descriptions, and locations
3. Optionally filter by date range
4. Results show matching events across all your calendars

---

## Invitations & RSVP

### Inviting Attendees

When creating or editing an event:

1. Click **Add Attendee**
2. Search for a user or enter an email address
3. Set the attendee's role:
   - **Required** — essential participant
   - **Optional** — invited but not critical
   - **Informational** — FYI only (no response expected)
4. Save the event

### Responding to an Invitation

When you're invited to an event:

1. Open the event from your calendar
2. Click your RSVP status:
   - **Accept** — you'll attend
   - **Decline** — you won't attend
   - **Tentative** — you may attend
3. Optionally add a comment
4. Click **Save**

### Attendee Statuses

| Status | Meaning |
|---|---|
| Needs Action | No response yet (default) |
| Accepted | Attendee confirmed |
| Declined | Attendee won't attend |
| Tentative | Attendee may attend |

---

## Reminders

### Adding Reminders

When creating or editing an event:

1. Click **Add Reminder**
2. Choose the reminder method:
   - **Notification** — in-app notification
   - **Email** — email reminder
3. Set the time: minutes before the event starts
4. You can add multiple reminders per event

### Reminder Examples

| Setting | When It Fires |
|---|---|
| 15 minutes before | 15 min before start |
| 60 minutes before | 1 hour before start |
| 1440 minutes before | 1 day before start |

### Reminders on Recurring Events

Reminders automatically fire for each occurrence of a recurring event. For example, a "15 minutes before" reminder on a weekly meeting will fire every week, 15 minutes before that occurrence starts. If you edit a single occurrence and change its reminders, only that instance is affected — the rest of the series keeps the original reminders.

---

## Recurring Events

Events can repeat on a regular schedule using RFC 5545 recurrence rules.

### Supported Patterns

- **Daily** — every N days
- **Weekly** — on specific days of the week (e.g., Mon, Wed, Fri)
- **Monthly** — on a specific day of the month
- **Yearly** — on a specific date each year

### Recurrence Options

- **Repeat every** — interval (e.g., every 2 weeks)
- **On days** — specific weekdays (for weekly recurrence)
- **Until** — end date for the recurrence
- **Count** — maximum number of occurrences

### Editing Recurring Events

When editing a recurring event, you can:

- **Edit this occurrence** — modify only this instance (creates a recurrence exception)
- **Edit all events** — modify the entire series

### Recurrence Rule Format

Under the hood, recurrence uses RFC 5545 RRULE format:

```
FREQ=WEEKLY;BYDAY=MO,WE,FR;UNTIL=20260630T000000Z
```

This format is compatible with CalDAV clients and iCalendar imports/exports.

---

## Sharing Calendars

You can share entire calendars with other users or teams. Sharing applies to all events in the calendar.

### Share with a User

1. Click the settings icon next to the calendar name
2. Click **Share**
3. Search for the user
4. Choose a permission level:
   - **Read Only** — can view events but not modify them
   - **Read/Write** — can view, create, edit, and delete events
5. Click **Share**

### Share with a Team

1. Click the calendar settings icon
2. Click **Share**
3. Select a team
4. Choose a permission level
5. Click **Share**

### Removing a Share

1. Open the calendar's share settings
2. Click **Remove** next to the share

Only the calendar owner can manage shares and delete the calendar.

---

## Importing Events

### Import from iCalendar (.ics)

DotNetCloud accepts iCalendar (RFC 5545) format files. Most calendar applications can export to this format.

1. Go to **Calendar**
2. Click **Import**
3. Select the target calendar
4. Paste or upload your `.ics` file content
5. Review the import preview
6. Click **Import**

### Supported iCalendar Fields

| iCalendar Field | Mapped To |
|---|---|
| `SUMMARY` | Event Title |
| `DTSTART` / `DTEND` | Start / End Time |
| `DESCRIPTION` | Description |
| `LOCATION` | Location |
| `URL` | URL |
| `RRULE` | Recurrence Rule |
| `VALARM` | Reminders |
| `ATTENDEE` | Attendees |

### Import Tips

- You can import single events or entire calendars
- Duplicate detection uses start time + title
- All-day events are supported
- Timezone information in the `.ics` file is respected
- Use dry-run mode to preview before importing large files

---

## Exporting Events

### Export Entire Calendar

1. Go to **Calendar**
2. Click the settings icon next to the calendar
3. Click **Export**
4. All events download as an `.ics` file

### Export a Single Event

1. Open an event
2. Click **Export as iCalendar**
3. The event downloads as an `.ics` file

Exported files use iCalendar RFC 5545 format, compatible with all major calendar applications.

---

## CalDAV Sync

CalDAV lets you sync your DotNetCloud calendars with external applications in real time.

### Setting Up DAVx5 (Android)

1. Install **DAVx5** from Google Play or F-Droid
2. Add a new account → **Login with URL**
3. Enter your server URL: `https://your-server/.well-known/caldav`
4. Authenticate with your DotNetCloud credentials
5. Select the calendars to sync
6. Events appear in your phone's Calendar app

### Setting Up Thunderbird

1. In Thunderbird, go to **Calendar** → **New Calendar**
2. Select **On the Network** → **CalDAV**
3. URL: `https://your-server/.well-known/caldav`
4. Enter your credentials
5. Select calendars to subscribe to

### Setting Up iOS / macOS

1. Go to **Settings** → **Accounts** → **Add Account** → **Other**
2. Select **Add CalDAV Account**
3. Server: `your-server`
4. User Name and Password: your DotNetCloud credentials
5. Calendars appear in the Calendar app

### Sync Behavior

- Changes sync automatically in both directions
- Conflict detection uses ETags — the most recent change wins
- New calendars created on external devices appear in DotNetCloud
- Deleted events are removed from all synced devices
- Sync tokens enable efficient incremental sync (only changes are transferred)

---

## Time Handling

- All events are stored in UTC on the server
- Each calendar has a timezone setting for display purposes
- CalDAV clients handle timezone conversion locally
- All-day events span the full day in the calendar's timezone
- Reminders fire based on the event's UTC start time

---

## Troubleshooting

### Events Not Appearing After Import

- Check the import report for errors or skipped records
- Verify the `.ics` file follows RFC 5545 format
- Ensure start and end dates are valid

### CalDAV Sync Not Working

- Verify the server URL includes `/.well-known/caldav`
- Check authentication credentials
- Ensure the server's TLS certificate is trusted
- Verify your reverse proxy forwards PROPFIND and REPORT methods

### Recurring Event Shows Wrong Times

- Check the calendar's timezone setting
- Verify the recurrence rule uses UTC timestamps
- CalDAV clients should handle timezone conversion — check client settings

### "Event Conflict" Error

- Another user modified the event while you were editing
- Reload the event to get the latest version and retry your changes
