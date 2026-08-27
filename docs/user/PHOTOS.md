# Photos — User Guide

> **Last Updated:** 2026-08-27

---

## Welcome

DotNetCloud Photos lets you browse, organize, view, and share your photos in a gallery interface with albums, favorites, a timeline, and a map of geotagged photos. Photos are indexed from your Files library rather than uploaded directly.

---

## Adding Photos

Photos are imported by scanning folders in your Files library.

1. Open **Library Settings** in the sidebar
2. Click **Add Source…**
3. Choose one or more folders from your Files library (admin-shared folders mounted under `_DotNetCloud` are also supported)
4. Click **Scan Now**

### Supported Formats

JPEG, PNG, WebP, BMP, TIFF, SVG, and HEIC/HEIF.

Overlapping folders are deduplicated during scans. You can reset the collection from Library Settings without affecting your actual image files.

---

## Browsing Photos

### Gallery View

Browse all your photos in a responsive grid with thumbnails. Switch between **grid** and **list** views with the toolbar toggle.

### Albums

- Create albums with **New Album** in the sidebar to group photos by event, trip, or category
- Open an album to view its photos
- Edit an album's title or description from the album dialog

### Timeline

View photos organized chronologically, grouped by date.

### Favorites

Star photos to view them together in the **Favorites** section.

### Map

Photos with GPS location data appear in the **Map** section, clustered by coordinates.

### Searching

Use the toolbar search box to find photos by filename.

---

## Viewing Photos

Click a photo to open the lightbox viewer.

- Navigate with the **← / →** arrow keys or on-screen controls
- View details including file name, date, dimensions, file size, and camera/lens settings when available

### Editing

Open the edit panel to:

- **Rotate** and **flip**
- Adjust **brightness**, **contrast**, and **saturation**
- Apply **sharpen** and **blur**

Changes can be undone, reverted, or saved.

### Slideshow

Start a fullscreen slideshow that automatically advances through your photos.

---

## Sharing

Share individual photos with other users by entering their user ID in the share dialog, and manage your shares from the **Shared** section.

---

## Tips & Tricks

- Use **albums** to organize photos from specific events or trips.
- The **timeline** view is great for finding photos from a particular date range.
- Use the search bar to find photos by filename.

---

## Troubleshooting

| Issue                 | What to Do                                                                            |
| --------------------- | ------------------------------------------------------------------------------------- |
| Photos not appearing  | Run **Scan Now** from Library Settings and confirm the source folder is correct       |
| Unsupported file type | Convert the image to a supported format (JPEG, PNG, WebP, etc.)                       |
| Map shows no photos   | Only photos with GPS location data appear — geotag photos to see them on the map      |
| Edit changes lost     | Save your edits from the edit panel; unsaved changes revert when you close the viewer |

---

## Related Guides

- [Getting Started](GETTING_STARTED.md) — manage the files Photos indexes from
- [Search](SEARCH.md) — find photos and content across modules
- [Video](VIDEO.md) — your video library
