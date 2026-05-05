# Module Help Sidebar Links & Help Pages

> **Date:** 2026-05-05
> **Status:** Planned
> **Author:** Deepseek V4 Flash

---

## Problem

Users need in-app guidance for each module. Currently, the only user documentation lives in `/docs/user/*.md` files (separate from the running application). There is no way to access module usage help from within the UI.

## Solution

Add a **"Help" link pinned to the bottom of every module's sidebar** (except Search, which has no sidebar) that navigates to a dedicated help page at `/apps/{module}/help` containing user-usage information authored in Razor.

---

## Architecture Overview

```
Module Sidebar
  └── ModuleHelpLink (shared component)
       └── "(❓) Help" — pinned to bottom, icon-only when collapsed
            └── navigates to /apps/{module}/help
                 └── ModuleHelp.razor (parameterized route)
                      ├── Back button → /apps/{module}
                      └── {Module}HelpContent.razor
                           (authored in Razor, informed by /docs/user/*.md)
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Help route | `/apps/{Module}/help` parameterized | Single file handles all modules; reduces boilerplate vs. 12+ wrappers |
| Help content | Razor components | User requested Razor over markdown rendering; content authored per module |
| Help link | Shared `ModuleHelpLink` component | Consistent appearance across all modules; single point of maintenance |
| Content location | Each module's own `UI/` folder | Self-contained; module can be dropped without core changes |

---

## Modules Covered

All modules **except** Search (no sidebar):

| # | Module | Sidebar Component | Existing User Doc |
|---|--------|-------------------|-------------------|
| 1 | Files | `FileSidebar.razor` | `GETTING_STARTED.md` |
| 2 | Notes | `NotesPage.razor` (inline) | `NOTES.md` |
| 3 | AI Assistant | `AiChatPage.razor` (inline) | `AI_ASSISTANT.md` |
| 4 | Calendar | `CalendarPage.razor` (inline) | `CALENDAR.md` |
| 5 | Contacts | `ContactsPage.razor` (inline) | `CONTACTS.md` |
| 6 | Chat | `ChatPageLayout.razor` (inline) | — |
| 7 | Bookmarks | `BookmarksPage.razor` (inline) | — |
| 8 | Email | `EmailPage.razor` (inline) | — |
| 9 | Music | `MusicPage.razor` (inline) | — |
| 10 | Photos | `PhotosPage.razor` (inline) | — |
| 11 | Tracks | `TracksPage.razor` (inline + `CustomViewsSidebar.razor`) | — |
| 12 | Video | `VideoPage.razor` (inline) | — |
| 13 | Example | *(new sidebar)* | — |

---

## 🚨 Optional Module Safety (Critical)

**The system must not break when a module is not installed.** The design ensures this:

- **Sidebar help links**: Authored INSIDE each module's own sidebar component. If the module isn't installed/loaded, its sidebar never renders → the help link never appears. Zero impact on core.
- **Help route**: The parameterized `/apps/{Module}/help` route validates the `Module` parameter against known modules. If someone navigates to `/apps/unknown-module/help`, it shows a "Module not found" state (not a crash).
- **No shared references**: No central enum, no shared list of help routes, no changes to `ModuleUiRegistrationHostedService`. Each module is fully self-contained.
- **Unregistered modules**: The `ModuleHelp.razor` page validates the module name and shows a graceful error for uninstalled/unknown modules instead of crashing.

---

## Implementation Phases

### Phase 1 — Shared Infrastructure

#### Step 1.1: Create `ModuleHelpLink.razor` (shared component)

**Path:** `src/UI/DotNetCloud.UI.Shared/Components/Navigation/ModuleHelpLink.razor`

A reusable component that renders a "Help" link pinned to the bottom of any module sidebar.

**Props:**
- `Href` (string, required) — The help page URL (e.g., `/apps/files/help`)
- `ModuleId` (string, optional) — For analytics/aria labels

**Behavior:**
- When sidebar is expanded: shows `❓ Help` text
- When sidebar is collapsed: shows only `❓` icon
- Visual separator above the link
- Styled to match sidebar nav items
- Pinned to bottom of sidebar (sticky positioning)

**Code-behind (`ModuleHelpLink.razor.cs`):**
- Accepts a `Collapsed` parameter (bool) to control icon vs. text display
- Accepts a callback or simply uses `NavigationManager` for navigation

#### Step 1.2: Create `ModuleHelp.razor` (parameterized route handler)

**Path:** `src/UI/DotNetCloud.UI.Web/Components/Pages/ModuleHelp.razor`

**Route:** `@page "/apps/{Module}/help"`
**Render mode:** `InteractiveServer`
**Auth:** `[Authorize]`

**Layout:**
- Page title: `{ModuleName} Help - DotNetCloud`
- Back button linking to `/apps/{Module}`
- Breadcrumb: `Apps > {ModuleName} > Help`
- Renders module-specific help content component

**Module Name Map:**
```
files → "Files"
notes → "Notes"
ai → "AI Assistant"
calendar → "Calendar"
contacts → "Contacts"
chat → "Chat"
bookmarks → "Bookmarks"
email → "Email"
music → "Music"
photos → "Photos"
tracks → "Tracks"
video → "Video"
example → "Example"
```

**Validation:**
- If `Module` parameter doesn't match a known module, show a "Module not found" error state
- Prevents crashes from manual URL manipulation

---

### Phase 2 — Example Module (Reference Pattern)

The Example module serves as the template for other module developers. It currently has NO sidebar and NO help page.

#### Step 2.1: Create `ExampleSidebar.razor`

**Path:** `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleSidebar.razor`

A collapsible sidebar following the `FileSidebar.razor` pattern:
- Sidebar header with title + collapse toggle
- Nav items specific to the Example module
- `<ModuleHelpLink>` pinned at the bottom

**Code-behind (`ExampleSidebar.razor.cs`):**
- Manages `_sidebarCollapsed` state
- Persists collapse state via localStorage (following existing pattern)

**CSS (`ExampleSidebar.razor.css`):**
- Follows `.example-sidebar` naming convention
- Matches existing sidebar styling patterns

#### Step 2.2: Refactor `ExampleNotesPage.razor`

Replace the current simple layout with a sidebar + content two-column layout:
```razor
<div class="example-page-layout">
    <ExampleSidebar ... />
    <div class="example-page-main">
        <!-- Existing content -->
    </div>
</div>
```

#### Step 2.3: Create `ExampleHelpContent.razor`

**Path:** `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleHelpContent.razor`

Full reference help content demonstrating all content types:

| Section | Content |
|---------|---------|
| Overview | What the Example module is (reference/demo module for developers) |
| Creating Notes | How to create a new note using the form |
| Editing Notes | How to edit existing notes |
| Deleting Notes | How to delete notes |
| Markdown Support | Supported markdown syntax (headings, bold, italic, lists, links) |
| Tips & Tricks | Best practices, keyboard shortcuts |
| FAQ | Common questions |

This serves as the **template** for all other module help content.

---

### Phase 3 — Help Content Components

For each module, create a `{Module}HelpContent.razor` component in the module's own `UI/` folder.

#### Standard Content Structure (all modules)

Each help content component follows this structure:

```
Overview → Features → How-To Guides → Tips & Tricks / FAQ
```

#### Modules with Existing Docs (convert to Razor)

| Module | Source | Key Topics |
|--------|--------|------------|
| Files | `GETTING_STARTED.md` | Uploading files, browsing, sharing (links + users), favorites, recent, trash, tags |
| Notes | `NOTES.md` | Creating notes, markdown editing, folders, tags, version history |
| AI | `AI_ASSISTANT.md` | Starting a chat, model selection, conversation history, multi-turn conversations |
| Calendar | `CALENDAR.md` | Creating events, CalDAV sync, reminders, sharing calendars |
| Contacts | `CONTACTS.md` | Managing contacts, groups, CardDAV sync |

#### Modules without Docs (new write-ups)

| Module | Key Topics |
|--------|------------|
| Chat | Channels, direct messages, audio/video calls, file sharing, channel management |
| Bookmarks | Adding bookmarks, organizing into folders, searching, importing/exporting |
| Email | Composing messages, folders, search, attachments, signature settings |
| Music | Library browsing, playback controls, playlists, favorites, search |
| Photos | Uploading photos, albums, gallery view, slideshow, sharing |
| Tracks | Projects/products, kanban board, backlog, sprints, goals, custom views |
| Video | Library, playback, playlists, favorites, search |

---

### Phase 4 — Module Help Route Wrappers

**Path:** `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/*.razor`

Create one wrapper file per module. Each follows this pattern:

```razor
@page "/apps/files/help"
@rendermode InteractiveServer
@attribute [Authorize]

<PageTitle>Files Help - DotNetCloud</PageTitle>

<ModuleHelp Module="files" />
```

Files to create (13 total):
- `FilesHelp.razor`
- `NotesHelp.razor`
- `AiHelp.razor`
- `CalendarHelp.razor`
- `ContactsHelp.razor`
- `ChatHelp.razor`
- `BookmarksHelp.razor`
- `EmailHelp.razor`
- `MusicHelp.razor`
- `PhotosHelp.razor`
- `TracksHelp.razor`
- `VideoHelp.razor`
- `ExampleHelp.razor`

---

### Phase 5 — Add Help Link to Each Sidebar

For each module's sidebar, add `<ModuleHelpLink>` at the bottom. The exact placement varies:

#### Dedicated Sidebar Components
| Sidebar | File | Placement |
|---------|------|-----------|
| Files | `FileSidebar.razor` | Before closing `</nav>` tag, after quota section |
| Example | `ExampleSidebar.razor` | Before closing `</nav>` tag |

#### Inline Sidebars (within page component)
| Page | File | Placement |
|------|------|-----------|
| Chat | `ChatPageLayout.razor` | Bottom of `.chat-sidebar` div |
| Contacts | `ContactsPage.razor` | Bottom of `.contacts-sidebar` div |
| Calendar | `CalendarPage.razor` | Bottom of `.calendar-sidebar` div |
| Notes | `NotesPage.razor` | Bottom of `.notes-sidebar` div |
| Tracks | `TracksPage.razor` | Bottom of tracks sidebar div |
| Photos | `PhotosPage.razor` | Bottom of `.photos-sidebar` div |
| Music | `MusicPage.razor` | Bottom of `.music-sidebar` nav |
| Video | `VideoPage.razor` | Bottom of `.video-sidebar` div |
| AI | `AiChatPage.razor` | Bottom of AI sidebar div |
| Bookmarks | `BookmarksPage.razor` | Bottom of `.bookmarks-sidebar` div |
| Email | `EmailPage.razor` | Bottom of `.email-sidebar` div |

---

## File Manifest

### Files to Create (31 total)

#### Infrastructure (3)
| # | File | Purpose |
|---|------|---------|
| 1 | `src/UI/DotNetCloud.UI.Shared/Components/Navigation/ModuleHelpLink.razor` | Shared help link component |
| 2 | `src/UI/DotNetCloud.UI.Shared/Components/Navigation/ModuleHelpLink.razor.css` | Help link styling |
| 3 | `src/UI/DotNetCloud.UI.Web/Components/Pages/ModuleHelp.razor` | Parameterized route handler |

#### Example Module (4)
| # | File | Purpose |
|---|------|---------|
| 4 | `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleSidebar.razor` | New collapsible sidebar |
| 5 | `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleSidebar.razor.css` | Sidebar styling |
| 6 | `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleSidebar.razor.cs` | Sidebar code-behind |
| 7 | `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleHelpContent.razor` | Reference help content |

#### Module Help Content (12)
| # | File |
|---|------|
| 8 | `src/Modules/Files/.../UI/FilesHelpContent.razor` |
| 9 | `src/Modules/Notes/.../UI/NotesHelpContent.razor` |
| 10 | `src/Modules/AI/.../UI/AiHelpContent.razor` |
| 11 | `src/Modules/Calendar/.../UI/CalendarHelpContent.razor` |
| 12 | `src/Modules/Contacts/.../UI/ContactsHelpContent.razor` |
| 13 | `src/Modules/Chat/.../UI/ChatHelpContent.razor` |
| 14 | `src/Modules/Bookmarks/.../UI/BookmarksHelpContent.razor` |
| 15 | `src/Modules/Email/.../UI/EmailHelpContent.razor` |
| 16 | `src/Modules/Music/.../UI/MusicHelpContent.razor` |
| 17 | `src/Modules/Photos/.../UI/PhotosHelpContent.razor` |
| 18 | `src/Modules/Tracks/.../UI/TracksHelpContent.razor` |
| 19 | `src/Modules/Video/.../UI/VideoHelpContent.razor` |

#### Module Help Route Wrappers (13)
| # | File |
|---|------|
| 20 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/FilesHelp.razor` |
| 21 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/NotesHelp.razor` |
| 22 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/AiHelp.razor` |
| 23 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/CalendarHelp.razor` |
| 24 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/ContactsHelp.razor` |
| 25 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/ChatHelp.razor` |
| 26 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/BookmarksHelp.razor` |
| 27 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/EmailHelp.razor` |
| 28 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/MusicHelp.razor` |
| 29 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/PhotosHelp.razor` |
| 30 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/TracksHelp.razor` |
| 31 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/VideoHelp.razor` |
| 32 | `src/UI/DotNetCloud.UI.Web/Components/Pages/Modules/Help/ExampleHelp.razor` |

### Files to Modify (13)

| # | File | Change |
|---|------|--------|
| 1 | `FileSidebar.razor` | Add `<ModuleHelpLink>` before `</nav>` |
| 2 | `ChatPageLayout.razor` | Add help link at bottom of chat sidebar |
| 3 | `ContactsPage.razor` | Add help link at bottom of contacts sidebar |
| 4 | `CalendarPage.razor` | Add help link at bottom of calendar sidebar |
| 5 | `NotesPage.razor` | Add help link at bottom of notes sidebar |
| 6 | `TracksPage.razor` | Add help link at bottom of tracks sidebar |
| 7 | `PhotosPage.razor` | Add help link at bottom of photos sidebar |
| 8 | `MusicPage.razor` | Add help link at bottom of music sidebar |
| 9 | `VideoPage.razor` | Add help link at bottom of video sidebar |
| 10 | `AiChatPage.razor` | Add help link at bottom of AI sidebar |
| 11 | `BookmarksPage.razor` | Add help link at bottom of bookmarks sidebar |
| 12 | `EmailPage.razor` | Add help link at bottom of email sidebar |
| 13 | `ExampleNotesPage.razor` | Replace layout with sidebar + content |

---

## Exclusions

- **Search module** — Has no sidebar; excluded by the brief.
- **Admin pages** — Not part of this scope (no module-specific help needed for admin).
- **Global help** — No dedicated global help route (e.g., `/help`).
- **Onboarding tours** — Separate feature, not part of this plan.
- **Contextual tooltips** — Not replacing the existing Chat `?` button pattern.

---

## Verification Checklist

- [ ] `dotnet build DotNetCloud.CI.slnf` — zero errors
- [ ] Each installed module shows Help link at bottom of sidebar
- [ ] Clicking Help → `/apps/{module}/help` loads correctly
- [ ] Back button returns to the module page
- [ ] Collapsed sidebar: Help icon remains visible (icon-only)
- [ ] Expanded sidebar: `❓ Help` text displayed
- [ ] `/apps/nonexistent-module/help` → graceful "not found" (no crash)
- [ ] Uninstalled modules never show help link anywhere (no leakage)
- [ ] Example module: new sidebar present with Help link
- [ ] Example module help page: complete reference content
