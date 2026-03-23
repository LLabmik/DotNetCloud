# Notes — User Guide

> **Last Updated:** 2026-03-23

---

## Welcome

DotNetCloud Notes is a Markdown-first note-taking system with folders, tags, version history, cross-module links, and sharing.

---

## Creating & Editing Notes

### Creating a Note

1. Open **Notes** from the left sidebar
2. Click **New Note**
3. Enter a **Title** (required)
4. Write your content in Markdown or plain text
5. Optionally:
   - Select a **Folder** to organize the note
   - Add **Tags** for categorization
   - Toggle **Pin** or **Favorite** for quick access
6. Click **Save**

### Editing a Note

1. Click on a note to open it
2. Edit the title or content
3. Click **Save**

Each save creates a new version in the note's history (see Version History below).

### Markdown Support

Notes support Markdown formatting:

- **Bold**: `**text**`
- *Italic*: `*text*`
- Headings: `# H1`, `## H2`, `### H3`
- Lists: `- item` or `1. item`
- Code blocks: triple backticks
- Links: `[text](url)`
- Tables, blockquotes, and more

### Deleting a Note

1. Select a note
2. Click **Delete**
3. The note is soft-deleted and can be recovered by an administrator

---

## Folders

Folders provide hierarchical organization for your notes.

### Creating a Folder

1. In the Notes sidebar, click **New Folder**
2. Enter a folder name
3. Optionally select a parent folder (for nesting)
4. Optionally set a color
5. Click **Create**

Folders can be nested to any depth.

### Moving Notes Between Folders

1. Open a note
2. Change the **Folder** selection
3. Save the note

Or use the **Move** action from the context menu.

### Deleting a Folder

1. Right-click a folder
2. Click **Delete**
3. Notes in the folder become unfiled (they are not deleted)

### Folder Sorting

Folders have a sort order that controls their display position. Drag and drop to reorder folders.

---

## Tags

Tags let you categorize notes across folders with free-form labels.

### Adding Tags

1. Open a note
2. In the **Tags** section, type a tag name and press Enter
3. Save the note

You can add multiple tags to a single note (e.g., "work", "idea", "todo", "project-alpha").

### Searching by Tag

Use the search bar with tag names to find all notes with specific tags.

---

## Searching Notes

1. Click the **Search** icon in the Notes toolbar
2. Enter keywords to search note titles, content, and tags
3. Results are ranked by relevance and paginated

Search covers:
- Note titles
- Note content (full-text)
- Tag names

---

## Version History

Every time you save a note, a new version is automatically created. This gives you a complete edit history.

### Viewing History

1. Open a note
2. Click **Version History**
3. Browse previous versions with timestamps and the user who made each edit

### Restoring a Version

1. Open the version history
2. Click **Restore** on the version you want
3. The note's content is replaced with the selected version's content
4. A new version is created (the restore itself is versioned — nothing is lost)

### Optimistic Concurrency

If two users edit the same note simultaneously:

1. The first save succeeds
2. The second save receives a **version conflict** error
3. The second user must reload the note, merge their changes, and save again

This prevents accidental overwrites.

---

## Cross-Module Links

Notes can link to entities in other DotNetCloud modules:

| Link Type | Links To |
|---|---|
| `File` | A file in the Files module |
| `CalendarEvent` | An event in the Calendar module |
| `Contact` | A contact in the Contacts module |
| `Note` | Another note |

### Adding Links

When creating or editing a note:

1. Click **Add Link**
2. Select the link type
3. Search for or select the target entity
4. Optionally add a display label
5. Save the note

Links are stored as metadata and enable cross-module navigation.

---

## Sharing Notes

You can share individual notes with other users. Note sharing is user-scoped only (team sharing is not available for notes).

### Sharing a Note

1. Open the note
2. Click **Share**
3. Search for the user
4. Choose a permission level:
   - **Read Only** — can view the note
   - **Read/Write** — can view and edit the note
5. Click **Share**

### Removing a Share

1. Open the note's share panel
2. Click **Remove** next to the share

### Important Notes About Sharing

- Only the note owner can manage shares and delete the note
- Folder-level sharing is not supported — each note must be shared individually
- Shared notes appear in the recipient's note list alongside their own notes

---

## Importing Notes

### Import from JSON

DotNetCloud accepts a JSON manifest format for bulk note imports:

```json
[
  {
    "title": "Meeting Notes",
    "content": "# Project Kickoff\n\nDiscussed timeline and milestones.",
    "folder": "Work",
    "tags": ["meeting", "project"]
  },
  {
    "title": "Shopping List",
    "content": "- Milk\n- Bread\n- Eggs",
    "folder": "Personal",
    "tags": ["list"]
  }
]
```

### Import from Markdown

You can also import raw Markdown files:

- H1 headings (`# Title`) become note titles
- Content under each H1 becomes the note body
- Multiple H1 headings in a single file create multiple notes

### Import Steps

1. Go to **Notes**
2. Click **Import**
3. Paste or upload the JSON manifest or Markdown content
4. Select a target folder (optional)
5. Review the import preview (dry-run)
6. Click **Import**

### Duplicate Detection

Notes are detected as duplicates when the title and owner match. Choose a conflict strategy:

- **Skip** — ignore duplicates (default)
- **Overwrite** — replace existing notes
- **Create Copy** — import alongside existing notes

---

## Tips & Best Practices

### Quick Access

- **Pin** important notes to keep them at the top of your list
- **Favorite** notes you reference frequently

### Organization Strategies

- Use **folders** for broad categories (Work, Personal, Projects)
- Use **tags** for cross-cutting themes (urgent, idea, reference)
- Use **links** to connect related notes, files, contacts, and events

### Keyboard Shortcuts

- **Ctrl+S** — Save note
- **Ctrl+N** — New note
- **Ctrl+F** — Search notes

---

## Troubleshooting

### Note Not Saving

- Check for version conflicts — another user may have edited the note
- Reload the note and retry

### Imported Notes Missing Content

- Verify JSON format matches the expected schema
- For Markdown imports, ensure each note starts with an H1 heading
- Check the import report for per-item error details

### Search Not Finding Results

- Search indexes update in near-real-time
- Try searching for partial words or different terms
- Verify the note exists in your owned or shared notes

### Folder Deleted But Notes Still Visible

- This is expected — deleting a folder orphans its notes (moves them to "unfiled")
- Notes are never deleted when their folder is deleted
