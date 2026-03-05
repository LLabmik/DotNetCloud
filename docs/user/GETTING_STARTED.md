# DotNetCloud Files — Getting Started

> **Last Updated:** 2026-03-03

---

## Welcome

DotNetCloud Files lets you store, organize, and share files from your web browser. This guide covers the basics of uploading, browsing, sharing, and organizing your files.

---

## Uploading Files

### Drag and Drop

1. Open the **Files** section from the left sidebar
2. Navigate to the folder where you want to upload
3. Drag files from your computer and drop them onto the file browser area
4. A progress panel appears showing upload status for each file

### Upload Button

1. Click the **Upload** button in the toolbar
2. Select one or more files from the file picker
3. Files begin uploading immediately

### Upload Progress

The upload progress panel shows:

- Per-file progress bar
- Upload speed
- Estimated time remaining
- Pause/resume and cancel buttons per file

You can minimize the progress panel and continue browsing while files upload.

---

## Browsing Files

### Navigation

- Click a **folder** to open it
- Use the **breadcrumb trail** at the top to navigate back to parent folders
- Click **All Files** in the sidebar to return to the root

### Views

Toggle between two views using the view buttons in the toolbar:

- **Grid view** — large icons with file names, useful for images
- **List view** — detailed table with name, size, date, and type columns

### Sorting

Click column headers in list view to sort by:

- Name (A-Z or Z-A)
- Size (smallest or largest first)
- Date modified (newest or oldest first)
- Type (file extension)

### Searching

Use the **search bar** in the toolbar to find files by name. Results are paginated and searchable across all your files and folders.

---

## Creating Folders

1. Click the **New Folder** button in the toolbar (or right-click → New Folder)
2. Enter a name for the folder
3. Click **Create**

Folders can be nested to any depth.

---

## File Operations

### Rename

1. Select a file or folder
2. Click **Rename** (or right-click → Rename)
3. Enter the new name
4. Press Enter or click **Save**

### Move

1. Select one or more files/folders
2. Click **Move** (or right-click → Move to...)
3. Choose the destination folder
4. Click **Move**

### Copy

1. Select one or more files/folders
2. Click **Copy** (or right-click → Copy to...)
3. Choose the destination folder
4. Click **Copy**

Copied files share the same storage as the original (deduplication), so copies don't use extra space.

### Delete

1. Select one or more files/folders
2. Click **Delete** (or press the Delete key)
3. Items are moved to the **Trash** (not permanently deleted)

---

## Previewing Files

Click a file to open the preview panel:

| File Type | Preview |
|---|---|
| **Images** (JPEG, PNG, GIF, WebP, SVG) | Inline image viewer |
| **Videos** (MP4, WebM) | HTML5 video player |
| **Audio** (MP3, WAV, OGG) | HTML5 audio player |
| **PDFs** | Embedded PDF viewer |
| **Text/Code** | Syntax-highlighted text viewer |
| **Markdown** | Rendered Markdown |
| **Other** | Download button |

Use **← →** arrow keys to navigate between files in the same folder. Press **Escape** to close the preview.

---

## Sharing Files

### Share with a User

1. Select a file or folder
2. Click **Share** (or the share icon)
3. Search for a user by name or email
4. Choose a permission level:
   - **Read** — view and download only
   - **Read & Write** — view, download, upload, rename, move
   - **Full** — all operations including re-share and delete
5. Click **Share**

### Share with a Team or Group

Same process as above, but search for a team or group name instead of a user.

### Create a Public Link

1. Select a file or folder
2. Click **Share** → **Public Link** section
3. Toggle **Enable public link**
4. Optionally set:
   - **Password** — require a password to access the link
   - **Expiration date** — link stops working after this date
   - **Max downloads** — limit the number of times the file can be downloaded
5. Click **Copy Link** to copy the URL to your clipboard

Anyone with the link can access the file without a DotNetCloud account.

### View Shared Files

- **Shared with me** — files and folders others have shared with you
- **Shared by me** — files and folders you have shared with others

Both views are accessible from the sidebar.

---

## Favorites

Mark frequently accessed files as favorites:

1. Select a file or folder
2. Click the **star icon** (or right-click → Add to Favorites)
3. Access all favorites from **Favorites** in the sidebar

Click the star again to remove from favorites.

---

## Tags

Organize files with colored tags:

### Adding Tags

1. Select a file or folder
2. Click the **tag icon** in the toolbar
3. Type a tag name (or select from suggestions)
4. Choose a color
5. Press Enter

### Filtering by Tag

Click a tag name in the sidebar under **Tags** to see all files with that tag.

---

## Trash

Deleted files go to the **Trash** for 30 days (configurable by your admin).

### Restore from Trash

1. Click **Trash** in the sidebar
2. Select the item(s) to restore
3. Click **Restore**

Items are restored to their original location.

### Permanently Delete

1. Click **Trash** in the sidebar
2. Select the item(s)
3. Click **Delete Permanently**

**Warning:** Permanent deletion cannot be undone.

### Empty Trash

Click **Empty Trash** to permanently delete all trashed items at once.

---

## Version History

Every time you update a file, a new version is saved automatically.

### Viewing Versions

1. Select a file
2. Click **Version History** (or the clock icon)
3. A side panel shows all versions with date, author, and size

### Restoring a Version

1. Open version history
2. Find the version you want
3. Click **Restore**

Restoring creates a new version with the old content — no data is lost.

### Downloading a Version

Click **Download** next to any version to get that specific version's content.

### Labeling a Version

Click the label icon next to a version to add a descriptive name (e.g., "Final draft"). Labeled versions are protected from automatic cleanup.

---

## Storage Quota

Your admin may set a storage limit for your account. Check your usage:

- The **quota bar** in the sidebar shows used vs. available space
- You'll receive a warning at 80% usage and a critical alert at 95%
- When your quota is full, you cannot upload new files until you free space

### Freeing Space

1. Delete files you no longer need
2. Empty the trash (trashed files may count against your quota)
3. Delete old file versions from version history

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `←` / `→` | Navigate between files in preview |
| `Escape` | Close preview |
| `Delete` | Move selected items to trash |

---

## Next Steps

- [Install the Sync Client](SYNC_CLIENT.md) to keep files synced to your desktop
- [Edit Documents Online](DOCUMENT_EDITING.md) with Collabora
