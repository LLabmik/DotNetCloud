# DotNetCloud Files — Online Document Editing

> **Last Updated:** 2026-03-03

---

## Overview

DotNetCloud integrates with **Collabora Online** (based on LibreOffice) to let you edit documents directly in your web browser — no desktop software required.

---

## Supported File Types

| Type | Extensions |
|---|---|
| **Word documents** | `.docx`, `.doc`, `.odt`, `.rtf` |
| **Spreadsheets** | `.xlsx`, `.xls`, `.ods`, `.csv` |
| **Presentations** | `.pptx`, `.ppt`, `.odp` |
| **Text files** | `.txt` |

Your administrator may configure additional or fewer supported types.

---

## Opening a Document for Editing

1. Navigate to the file in your **Files** browser
2. Click the file to open the preview
3. If the file type supports editing, click **"Edit in Collabora"**
4. The document opens in a full-featured editor within your browser

If the file type is not supported for online editing, the standard download/preview appears instead.

---

## Using the Editor

The Collabora editor provides a familiar office experience:

### Word Processing (Documents)

- Full text formatting (bold, italic, underline, headings, lists)
- Tables, images, and page layout
- Spelling and grammar checking
- Track changes and comments
- Export to PDF

### Spreadsheets

- Formulas and functions
- Cell formatting and conditional formatting
- Charts and graphs
- Multiple sheets
- Sort and filter

### Presentations

- Slide layouts and templates
- Text, images, and shapes
- Slide transitions and animations
- Presenter notes
- Slide show mode

---

## Auto-Save

Your changes are saved automatically every 5 minutes (configurable by your admin). You can also save manually:

- **Ctrl+S** (Windows/Linux) or **Cmd+S** (macOS)

Each save creates a new **version** of the file, so you can always go back to a previous state.

---

## Collaborative Editing

Multiple users can edit the same document simultaneously:

- You'll see **colored cursors** showing where others are editing
- Changes from other users appear in real time
- There's no need to "lock" the document — Collabora handles concurrent editing

### Who's Editing

The editor shows indicators for other users currently editing the document. Look for the user avatars or names in the editor toolbar.

---

## Closing the Editor

Simply navigate away from the page or close the browser tab. Your changes are auto-saved before the session closes.

---

## Version History After Editing

Each save during an editing session creates a new file version. To see all versions:

1. Close the editor
2. Select the file in the file browser
3. Click **Version History**
4. You'll see entries for each auto-save and manual save

You can restore any previous version from this panel.

---

## Offline Editing

If your admin hasn't enabled Collabora, or if you prefer to edit locally:

1. **Download** the file from the file browser
2. Edit it with your local office software (Microsoft Office, LibreOffice, etc.)
3. **Upload** the modified file back to DotNetCloud

The sync client can automate this — any file you save in your sync folder is uploaded automatically.

---

## Limitations

- **File size:** Very large documents (100+ MB) may take longer to load in the editor
- **Complex formatting:** Some advanced formatting features may render differently than in desktop Microsoft Office
- **Browser support:** Collabora works best in modern browsers (Chrome, Firefox, Edge, Safari)

---

## Troubleshooting

### "Edit" Button Not Appearing

- The file type may not be supported for online editing
- Collabora may not be enabled on your server — contact your administrator
- Check the file preview for a "Download to edit locally" option

### Document Loads Slowly

- Large documents take longer to load
- Check your internet connection speed
- The first load may be slower while Collabora prepares the document

### Changes Not Saving

- Check your internet connection
- Look for error messages in the editor toolbar
- Try saving manually with **Ctrl+S**
- If the issue persists, download the file and save your changes locally

### Editor Shows "Session Expired"

- Your editing session has timed out (default: 8 hours)
- Refresh the page to start a new session
- Your most recent auto-save is preserved

---

## Related Guides

- [Getting Started with Files](GETTING_STARTED.md) — file management basics
- [Desktop Sync Client](SYNC_CLIENT.md) — automatic file synchronization
