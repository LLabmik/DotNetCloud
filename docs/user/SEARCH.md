# Search — User Guide

> **Last Updated:** 2026-08-27

---

## Welcome

DotNetCloud Search lets you find anything across all your modules — files, notes, chat messages, contacts, calendar events, photos, music, video, and tracks — from a single search box. Results are permission-scoped, so you only ever see content you already have access to.

---

## Quick Search

### Opening Search

- Click the **Search…** button in the top navigation bar, or
- Press **Ctrl+K** from anywhere in the app

A search overlay opens with a search input and, if you've searched before, your recent searches.

### Searching

1. Type at least two characters — results start appearing automatically as you type
2. Use the **↑ / ↓** arrow keys to move through suggestions
3. Press **Enter** to open the selected result
4. Press **Esc** to close the overlay

Each suggestion shows the result title, a context snippet, and which module it belongs to (with its module icon).

### Viewing All Results

Click **View all results** (or press **Enter** with no suggestion selected) to open the full search results page.

---

## Search Results Page

The full results page (`/search`) shows:

- A search input with the same instant search behavior
- A **sort** control: **Relevance** (default), **Newest first**, or **Oldest first**
- A **Modules** sidebar showing result counts per module — click a module to filter
- Result cards with the module icon, title, snippet, and a link to open the item
- **Pagination** (20 results per page)

---

## Advanced Query Syntax

You can refine searches with special operators:

| Syntax        | Example              | Meaning                           |
| ------------- | -------------------- | --------------------------------- |
| Quoted phrase | `"quarterly report"` | Match the exact phrase            |
| Module filter | `in:notes`           | Only search one module            |
| Exclusion     | `-draft`             | Exclude results containing a term |

You can combine them, for example: `"quarterly report" in:notes -draft`.

---

## What's Searchable

Search covers content from these modules (where installed):

- **Files** — file and folder names, plus extracted text from PDF, DOCX, XLSX, Markdown, and plain text files
- **Notes** — note titles and content
- **Chat** — messages
- **Contacts** — names, emails, and phone numbers
- **Calendar** — event titles and descriptions
- **Photos**, **Music**, **Video** — library item titles
- **Tracks** — cards and boards

---

## Privacy

Search results are **permission-scoped** to you — you only see your own content and content that has been shared with you.

---

## Troubleshooting

| Issue                                    | What to Do                                                                                          |
| ---------------------------------------- | --------------------------------------------------------------------------------------------------- |
| No results for something you know exists | The search index updates in near-real-time; wait a moment and try again, or use fewer/simpler terms |
| Results missing from one module          | The module may be unavailable or not installed — check with your administrator                      |
| Snippets look incomplete                 | Snippets are context-aware; open the full result to see all matching content                        |
| Search overlay won't open                | Make sure the page has focus before pressing Ctrl+K                                                 |

---

## Related Guides

- [Getting Started](GETTING_STARTED.md) — file management basics
- [Notes](NOTES.md) — note-taking and full-text search within notes
- [Chat](CHAT.md) — searching messages within a channel
