# Android Music Tab — Title Bar Search Implementation Plan

**Branch:** `feat/android-music-search`  
**Date:** 2026-07-15  
**Status:** planning

## Overview

Add a toggleable search icon (🔍) to the `Shell.TitleView` of `MusicPage.xaml`. When tapped, a search panel drops down below the title bar. Typing triggers a debounced server-side search that replaces the currently active tab's collection (Artists / Albums / Tracks) with search results. Clearing the query or closing the panel restores the original collection data.

**UX behavior:**

- 🔍 icon always visible in the title bar (not gated by playback state)
- Panel appears below the title bar / above the tab bar
- Search filters the **current tab only** (no cross-type unified search)
- Uses **server-side** search endpoints (not client-side filtering)
- 300ms debounce on text input before sending search request
- State resets when switching tabs or pressing back

**Reference pattern:** `MessageListPage.xaml` toggle search panel (lines 14–53)

---

## Files to Modify

| File                                                                  | Changes                                                                                                                        |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml`         | Add 🔍 icon in TitleView (Col 4), search panel Border in main Grid, results label, update ColumnDefinitions and RowDefinitions |
| `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml.cs`      | Add `OnSearchEntryCompleted` handler to focus search Entry when panel opens                                                    |
| `src/Clients/DotNetCloud.Client.Android/ViewModels/MusicViewModel.cs` | Add search properties, commands, `SearchAsync()`, collection save/restore logic, tab-switch search-closure                     |

**No changes needed:** `IMusicRestClient.cs`, `HttpMusicRestClient.cs` (search endpoints already exist).

---

## Step 1: ViewModel — Add Search State Properties

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/MusicViewModel.cs`

### 1.1 Add private backing state fields

Insert **after the `_previousNonEqView` field** (around line 108, before the `// ── Observable properties ──` comment):

```csharp
    // ── Search state ───────────────────────────────────────────

    private CancellationTokenSource? _searchCts;
    private bool _isSearchOpen;
    private string _searchQuery = string.Empty;
    private string? _searchResultText;
    private bool _isSearching;

    /// <summary>Saved pre-search collections to restore on search close.</summary>
    private ObservableCollection<ArtistDto>? _preSearchArtists;
    private ObservableCollection<MusicAlbumDto>? _preSearchAlbums;
    private ObservableCollection<TrackDto>? _preSearchTracks;
```

### 1.2 Add observable properties

Insert **after the `_title` property** (around line 149, after `[ObservableProperty] private string _title = "Music";`):

```csharp
    /// <summary>Whether the search panel is open.</summary>
    [ObservableProperty]
    private bool _isSearchOpen;

    /// <summary>Current search query text.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>Search result status text (e.g. "12 results" or "No results").</summary>
    [ObservableProperty]
    private string? _searchResultText;

    /// <summary>Whether a search request is in flight.</summary>
    [ObservableProperty]
    private bool _isSearching;
```

### 1.3 Add `OnSearchQueryChanged` partial method

Insert **after the observable properties** added above. This method fires automatically whenever `SearchQuery` is set via binding (the source generator creates it from `OnSearchQueryChanged`).

```csharp
    partial void OnSearchQueryChanged(string value)
    {
        _ = SearchAsync(value);
    }
```

### 1.4 Add placeholder text helper

Insert **after the `GetCredentialsAsync` method** (around line 75):

```csharp
    /// <summary>Gets the search Entry placeholder text for the current tab.</summary>
    private string SearchPlaceholder => CurrentView switch
    {
        MusicView.Artists => "Search artists…",
        MusicView.Albums => "Search albums…",
        MusicView.Tracks => "Search tracks…",
        _ => "Search…"
    };

    /// <summary>Backing property for SearchPlaceholder (for XAML binding — no direct CollectionView support in XAML for computed properties).
    /// Updated whenever CurrentView changes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholder))]
    private string _searchPlaceholderText = "Search…";

    partial void OnCurrentViewChanged(MusicView value)
    {
        SearchPlaceholderText = SearchPlaceholder;
        // If search is open when tab changes, close it
        if (IsSearchOpen)
        {
            CloseSearch();
        }
    }
```

**IMPORTANT:** The above uses `OnCurrentViewChanged`. This partial method already exists (generated by `[ObservableProperty]` on `_currentView`). You need to **merge** the existing partial method's logic with this. Since `OnCurrentViewChanged` does not exist in the handwritten code (it's generated by the source generator), we handle the tab-switch search-close differently. See **Step 3.1** below.

---

## Step 2: ViewModel — Add Search Commands

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/MusicViewModel.cs`

### 2.1 Add `ToggleSearchCommand`

Insert in the commands region (after `ScrollToRequested`, around line 312):

```csharp
    // ── Search commands ────────────────────────────────────────

    /// <summary>Toggles the search panel open/close.</summary>
    [RelayCommand]
    private void ToggleSearch()
    {
        if (IsSearchOpen)
        {
            CloseSearch();
        }
        else
        {
            OpenSearch();
        }
    }

    /// <summary>Opens the search panel.</summary>
    private void OpenSearch()
    {
        // Save current collections before replacing with search results
        _preSearchArtists = Artists;
        _preSearchAlbums = Albums;
        _preSearchTracks = Tracks;

        IsSearchOpen = true;
        SearchQuery = string.Empty;
        SearchResultText = null;
        ErrorMessage = null;
    }

    /// <summary>Closes the search panel and restores original data.</summary>
    [RelayCommand]
    private void CloseSearch()
    {
        _searchCts?.Cancel();

        if (IsSearchOpen)
        {
            // Restore pre-search collections if they were saved
            if (_preSearchArtists is not null) Artists = _preSearchArtists;
            if (_preSearchAlbums is not null) Albums = _preSearchAlbums;
            if (_preSearchTracks is not null) Tracks = _preSearchTracks;
        }

        _preSearchArtists = null;
        _preSearchAlbums = null;
        _preSearchTracks = null;
        IsSearchOpen = false;
        SearchQuery = string.Empty;
        SearchResultText = null;
        IsSearching = false;
    }
```

### 2.2 Add `SearchAsync` method

Insert after the `CloseSearch` command:

```csharp
    /// <summary>
    /// Debounced server-side search. Called automatically when <see cref="SearchQuery"/> changes
    /// via the source-generated <c>OnSearchQueryChanged</c> partial method.
    /// Fans out to the correct search endpoint based on <see cref="CurrentView"/>.
    /// </summary>
    private async Task SearchAsync(string query)
    {
        // Cancel any in-flight search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        // If query is empty/whitespace, restore original collections
        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearching = false;
            SearchResultText = null;
            RestorePreSearchCollections();
            return;
        }

        // Debounce: wait 300ms before firing
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
        {
            ErrorMessage = "Not connected to server";
            return;
        }

        IsSearching = true;
        ErrorMessage = null;

        try
        {
            int count;

            switch (CurrentView)
            {
                case MusicView.Artists:
                    var artists = await _music.SearchArtistsAsync(serverUrl, token, query, take: 50, ct: ct);
                    if (ct.IsCancellationRequested) return;
                    count = artists.Count;
                    Dispatch(() =>
                    {
                        Artists = new ObservableCollection<ArtistDto>(artists);
                        ArtistAlphabet = ComputeAlphabetLocal(artists, a => a.Name);
                    });
                    break;

                case MusicView.Albums:
                    var albums = await _music.SearchAlbumsAsync(serverUrl, token, query, take: 50, ct: ct);
                    if (ct.IsCancellationRequested) return;
                    count = albums.Count;
                    Dispatch(() =>
                    {
                        Albums = new ObservableCollection<MusicAlbumDto>(albums);
                        AlbumAlphabet = ComputeAlphabetLocal(albums, a => a.Title);
                    });
                    break;

                case MusicView.Tracks:
                    var tracks = await _music.SearchTracksAsync(serverUrl, token, query, take: 50, ct: ct);
                    if (ct.IsCancellationRequested) return;
                    count = tracks.Count;
                    Dispatch(() =>
                    {
                        Tracks = new ObservableCollection<TrackDto>(tracks);
                        TrackAlphabet = ComputeAlphabetLocal(tracks, t => t.Title);
                    });
                    break;

                default:
                    // Playlists and EQ views — search not applicable
                    count = 0;
                    break;
            }

            if (!ct.IsCancellationRequested)
            {
                Dispatch(() =>
                {
                    SearchResultText = count == 0
                        ? $"No results for \"{query}\""
                        : $"{count} result{(count != 1 ? "s" : "")} for \"{query}\"";
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled — do nothing
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                Dispatch(() => ErrorMessage = $"Search failed: {ex.Message}");
            }
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => IsSearching = false);
        }
    }

    /// <summary>
    /// Restores the pre-search collections when the user clears the search query.
    /// Only restores if pre-search collections were saved (i.e., search was opened from
    /// a populated tab).
    /// </summary>
    private void RestorePreSearchCollections()
    {
        Dispatch(() =>
        {
            if (_preSearchArtists is not null && CurrentView == MusicView.Artists)
                Artists = _preSearchArtists;
            if (_preSearchAlbums is not null && CurrentView == MusicView.Albums)
                Albums = _preSearchAlbums;
            if (_preSearchTracks is not null && CurrentView == MusicView.Tracks)
                Tracks = _preSearchTracks;
        });
    }
```

---

## Step 3: ViewModel — Tab-Switch Search Closure

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/MusicViewModel.cs`

### 3.1 Close search when switching tabs

In each of these commands, add `IsSearchOpen = false; SearchQuery = string.Empty;` at the very top, before doing anything else. Only set them and don't call `CloseSearch()` (to avoid double-restoring collections — the tab load command itself replaces the collection).

#### In `LoadArtistsAsync()` (around line 315):

**Find this section:**

```csharp
    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
```

**Change to:**

```csharp
    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        // Close search if open — tab switch clears search state
        IsSearchOpen = false;
        SearchQuery = string.Empty;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
```

#### In `LoadAlbumsAsync()` (around line 397):

**Find this section:**

```csharp
    [RelayCommand]
    private async Task LoadAlbumsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
```

**Change to:**

```csharp
    [RelayCommand]
    private async Task LoadAlbumsAsync()
    {
        // Close search if open — tab switch clears search state
        IsSearchOpen = false;
        SearchQuery = string.Empty;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
```

#### In `LoadTracksAsync()` (around line 472):

**Find this section:**

```csharp
    [RelayCommand]
    private async Task LoadTracksAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
```

**Change to:**

```csharp
    [RelayCommand]
    private async Task LoadTracksAsync()
    {
        // Close search if open — tab switch clears search state
        IsSearchOpen = false;
        SearchQuery = string.Empty;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
```

### 3.2 Close search before navigating back

In `BackAsync()` (around line 1200), add search-close logic at the beginning:

**Find:**

```csharp
    [RelayCommand]
    private async Task BackAsync()
    {
        if (CurrentView == MusicView.Tracks && CanGoBackToPlaylist)
```

**Change to:**

```csharp
    [RelayCommand]
    private async Task BackAsync()
    {
        // If search is open, close it first — don't navigate back
        if (IsSearchOpen)
        {
            CloseSearchCommand.Execute(null);
            return;
        }

        if (CurrentView == MusicView.Tracks && CanGoBackToPlaylist)
```

---

## Step 4: XAML — Title View Search Icon

**File:** `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml`

### 4.1 Update TitleView ColumnDefinitions

**Find line 18:**

```xml
        <Grid ColumnDefinitions="*,Auto,Auto,Auto"
```

**Change to:**

```xml
        <Grid ColumnDefinitions="*,Auto,Auto,Auto,Auto"
```

### 4.2 Add search icon button as Column 4

**Find the closing `</Grid>` at the end of the EQ icon button (after the `</AbsoluteLayout>` and `</Grid>` that ends the EQ button, around line 155) and BEFORE `</Grid>` (the one that closes the TitleView Grid):**

The last element before `</Grid>` is the EQ icon button ending around line 152. Insert the search icon button between the EQ button's closing `</Grid>` and the outer TitleView's `</Grid>`:

```xml
            <!-- Search icon button — always visible, toggles search panel -->
            <Grid Grid.Column="4"
                  WidthRequest="44"
                  HeightRequest="44"
                  VerticalOptions="Center"
                  Padding="8">
                <Grid.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding ToggleSearchCommand}"/>
                </Grid.GestureRecognizers>
                <Label Text="🔍"
                       FontSize="16"
                       TextColor="#94A3B8"
                       VerticalOptions="Center"
                       HorizontalOptions="Center"/>
            </Grid>
        </Grid>
    </Shell.TitleView>
```

**Visual reference — the complete TitleView should look like this after the change:**

```xml
    <Shell.TitleView>
        <Grid ColumnDefinitions="*,Auto,Auto,Auto,Auto"
              VerticalOptions="Center">
            <HorizontalStackLayout Grid.Column="0" ...>
                <Image .../>
                <Label Text="Music" .../>
            </HorizontalStackLayout>

            <!-- Col 1: Repeat icon -->
            <Grid Grid.Column="1" ...>...</Grid>

            <!-- Col 2: Repeat label -->
            <Label Grid.Column="2" .../>

            <!-- Col 3: EQ icon -->
            <Grid Grid.Column="3" ...>...</Grid>

            <!-- Col 4: Search icon (NEW) -->
            <Grid Grid.Column="4"
                  WidthRequest="44"
                  HeightRequest="44"
                  VerticalOptions="Center"
                  Padding="8">
                <Grid.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding ToggleSearchCommand}"/>
                </Grid.GestureRecognizers>
                <Label Text="🔍"
                       FontSize="16"
                       TextColor="#94A3B8"
                       VerticalOptions="Center"
                       HorizontalOptions="Center"/>
            </Grid>
        </Grid>
    </Shell.TitleView>
```

---

## Step 5: XAML — Search Panel

**File:** `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml`

### 5.1 Update outer Grid RowDefinitions

**Find line (around line 170):**

```xml
    <Grid RowDefinitions="Auto,*">
```

This is the main content Grid that contains the now-playing bar and content area. The search panel goes BETWEEN the now-playing bar and the content. But since the content already occupies the `*` row with its own inner `Grid` (RowDefinitions="Auto,\*") for the tab bar and collections, the cleanest approach is:

**Change to:**

```xml
    <Grid RowDefinitions="Auto,Auto,*">
```

Row 0: Now-playing bar  
Row 1: Search panel  
Row 2: Content area (tab bar + collections)

### 5.2 Add search panel between now-playing bar and content area

**Find the now-playing bar `</Border>` closing tag, then the `<!-- ── Content area ── →` comment.** Insert the search panel between them:

```xml
        </Border>

        <!-- ── Search panel (toggleable) ───────────────────────────── -->
        <Border
            Grid.Row="1"
            IsVisible="{Binding IsSearchOpen}"
            BackgroundColor="#1E293B"
            StrokeThickness="0"
            Padding="8,6">
            <Grid ColumnDefinitions="*,Auto"
                  ColumnSpacing="6">
                <Entry
                    x:Name="SearchEntry"
                    Placeholder="{Binding SearchPlaceholderText}"
                    PlaceholderColor="#475569"
                    TextColor="#F1F5F9"
                    BackgroundColor="#0F172A"
                    Text="{Binding SearchQuery, Mode=TwoWay}"
                    ReturnType="Search"/>
                <Label
                    Grid.Column="1"
                    Text="✕"
                    TextColor="#94A3B8"
                    FontSize="22"
                    HorizontalTextAlignment="Center"
                    VerticalTextAlignment="Center"
                    WidthRequest="44"
                    HeightRequest="36">
                    <Label.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding CloseSearchCommand}"/>
                    </Label.GestureRecognizers>
                </Label>
            </Grid>
        </Border>

        <!-- ── Content area ────────────────────────────────────────── -->
```

### 5.3 Update content area Grid.Row

**Find:**

```xml
        <!-- ── Content area ────────────────────────────────────────── -->
        <Grid Grid.Row="1"
```

**Change to:**

```xml
        <!-- ── Content area ────────────────────────────────────────── -->
        <Grid Grid.Row="2"
```

---

## Step 6: XAML — Search Results Label

**File:** `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml`

### 6.1 Add results label below the tab bar

**Find the tab bar's `</HorizontalStackLayout>` closing and the loading indicator `</ActivityIndicator>`.** Insert the results label between them.

**Find:**

```xml
                </HorizontalStackLayout>
            </Grid>

            <!-- Loading indicator -->
            <ActivityIndicator
                Grid.Row="1"
```

**Change to:**

```xml
                </HorizontalStackLayout>
            </Grid>

            <!-- Search results indicator (visible only when search is active and has results) -->
            <Label
                Grid.Row="1"
                Text="{Binding SearchResultText}"
                TextColor="#94A3B8"
                FontSize="13"
                HorizontalOptions="Center"
                Padding="8,4"
                IsVisible="{Binding IsSearchOpen}"
                LineBreakMode="TailTruncation"/>

            <!-- Loading indicator -->
            <!-- NOTE: The original loading indicator was Grid.Row="1". Since we inserted
                 the results label at Row 1, the loading indicator, error message, and all
                 collection views need to move to Row 2. See Step 6.2. -->

```

### 6.2 Update ALL Grid.Row references in the content Grid

The content area's inner `Grid` currently has `RowDefinitions="Auto,*"`. This becomes `RowDefinitions="Auto,Auto,*"`:

**Find:**

```xml
        <Grid Grid.Row="2"
              RowDefinitions="Auto,*">
```

**Change to:**

```xml
        <Grid Grid.Row="2"
              RowDefinitions="Auto,Auto,*">
```

Row 0: Back button + tab bar  
Row 1: Search results label  
Row 2: Everything else (loading, error, collections)

Now all elements that were `Grid.Row="1"` in this inner Grid must become `Grid.Row="2"`:

| Element                       | Old `Grid.Row` | New `Grid.Row` |
| ----------------------------- | -------------- | -------------- |
| `ActivityIndicator` (loading) | `1`            | `2`            |
| `VerticalStackLayout` (error) | `1`            | `2`            |
| Artists Grid                  | `1`            | `2`            |
| Albums Grid                   | `1`            | `2`            |
| Tracks Grid                   | `1`            | `2`            |
| Playlists CollectionView      | `1`            | `2`            |
| EQ ScrollView                 | `1`            | `2`            |

**Do a find-and-replace across the file:**

- Find: `Grid.Row="1"` that appear **after** the `<!-- ── Content area ── →` comment
- These should ALL be changed to `Grid.Row="2"` (there are ~7 occurrences)

---

## Step 7: Code-Behind — Focus Search Entry

**File:** `src/Clients/DotNetCloud.Client.Android/Views/MusicPage.xaml.cs`

### 7.1 Focus the search Entry when panel opens

Add this method to the `MusicPage` class. This is called from the ViewModel when the search panel is opened, since the ViewModel can't directly access the Entry control.

```csharp
    /// <summary>
    /// Called when the search Entry completes (user presses Search on keyboard).
    /// This dismisses the keyboard — search is already triggered by text changes.
    /// </summary>
    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry entry)
        {
#if ANDROID
            entry.IsEnabled = false;
            entry.IsEnabled = true;
#endif
            entry.Unfocus();
        }
    }
```

### 7.2 Wire the Completed event

In the XAML `SearchEntry`, add the `Completed` attribute on the Entry. The Entry in the search panel (Step 5.2) already has `x:Name="SearchEntry"`. Add the event:

```xml
                <Entry
                    x:Name="SearchEntry"
                    Placeholder="{Binding SearchPlaceholderText}"
                    PlaceholderColor="#475569"
                    TextColor="#F1F5F9"
                    BackgroundColor="#0F172A"
                    Text="{Binding SearchQuery, Mode=TwoWay}"
                    ReturnType="Search"
                    Completed="OnSearchCompleted"/>
```

### 7.3 Focus Entry when search opens

Add this to the `OnAppearing` override and also wire it via a property change. The simplest approach is to add a handler in the constructor:

In the constructor, after `_vm = vm;`, add:

```csharp
        // Focus the search Entry automatically when the search panel opens
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MusicViewModel.IsSearchOpen) && _vm.IsSearchOpen)
            {
                // Small delay to let the Entry render before focusing
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(100);
                    SearchEntry?.Focus();
                });
            }
        };
```

---

## Step 8: Verification

### 8.1 Build

```powershell
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj
```

Fix any compilation errors. Common issues:

- Missing `using` for `System.Threading` (for `CancellationTokenSource`)
- `OnCurrentViewChanged` partial method (don't add — it's source-generated)
- Verify `SearchPlaceholderText` binding works in XAML (it's `[ObservableProperty]`)
- Verify all `Grid.Row="2"` changes are correct

### 8.2 Manual test checklist

| #   | Test                          | Expected                                                                                            |
| --- | ----------------------------- | --------------------------------------------------------------------------------------------------- |
| 1   | Tap 🔍 icon                   | Search panel appears, keyboard opens, Entry focused                                                 |
| 2   | Type "test"                   | After 300ms, results replace current tab data                                                       |
| 3   | Clear query (empty)           | Original pre-search data restored                                                                   |
| 4   | Tap ✕ button                  | Panel closes, original data restored                                                                |
| 5   | Tap 🔍 again while open       | Panel closes, original data restored                                                                |
| 6   | Search with no-matching query | "No results for 'xyz'" shown                                                                        |
| 7   | Switch tab while searching    | Search closes, new tab loads normally                                                               |
| 8   | Press Back while searching    | Search closes first, then normal back                                                               |
| 9   | Search on Artists tab         | `SearchArtistsAsync` called                                                                         |
| 10  | Search on Albums tab          | `SearchAlbumsAsync` called                                                                          |
| 11  | Search on Tracks tab          | `SearchTracksAsync` called                                                                          |
| 12  | Search on Playlists tab       | No search endpoint exists; "No results" or empty (accepted behavior — playlist search out of scope) |
| 13  | Rapid typing (debounce test)  | Only last query's results show                                                                      |

### 8.3 Edge cases

| #   | Scenario                                      | Expected                                             |
| --- | --------------------------------------------- | ---------------------------------------------------- |
| 1   | Not connected to server                       | "Not connected to server" error shown                |
| 2   | Server returns error                          | Error message shown in error label                   |
| 3   | Page backgrounded and returned                | Search state preserved (not cleared)                 |
| 4   | Tapping 🔍 while data is still loading        | Panel opens, loading continues beneath               |
| 5   | Drill-down view active (scoped albums/tracks) | Search closes and tab reloads (per tab-switch logic) |

---

## Summary of All Changes

### `MusicPage.xaml`

1. Line 18: `ColumnDefinitions="*,Auto,Auto,Auto"` → `"*,Auto,Auto,Auto,Auto"`
2. Before `</Grid>` (closing TitleView): add Col 4 search icon Grid
3. Line ~170: `RowDefinitions="Auto,*"` → `"Auto,Auto,*"` (outer Grid)
4. Between now-playing `</Border>` and `<!-- Content area -->`: add search panel Border
5. Content area Grid: `Grid.Row="1"` → `Grid.Row="2"`
6. Inner content Grid: `RowDefinitions="Auto,*"` → `"Auto,Auto,*"`
7. After tab bar `</HorizontalStackLayout></Grid>`: add results Label (Row 1)
8. All other elements: `Grid.Row="1"` → `Grid.Row="2"` (~7 occurrences)
9. Search Entry: add `Completed="OnSearchCompleted"`

### `MusicPage.xaml.cs`

1. Add `OnSearchCompleted` method
2. Add property-changed handler in constructor for auto-focusing SearchEntry

### `MusicViewModel.cs`

1. Add 5 private fields: `_searchCts`, `_isSearchOpen`, `_searchQuery`, `_searchResultText`, `_isSearching`
2. Add 3 collection save fields: `_preSearchArtists`, `_preSearchAlbums`, `_preSearchTracks`
3. Add 4 `[ObservableProperty]` properties: `IsSearchOpen`, `SearchQuery`, `SearchResultText`, `IsSearching`
4. Add `SearchPlaceholderText` property + `SearchPlaceholder` computed property
5. Add `OnSearchQueryChanged` partial method
6. Add `ToggleSearchCommand`, `CloseSearchCommand`
7. Add `OpenSearch()`, `CloseSearch()`, `SearchAsync()`, `RestorePreSearchCollections()` methods
8. In `LoadArtistsAsync`, `LoadAlbumsAsync`, `LoadTracksAsync`: add `IsSearchOpen = false; SearchQuery = string.Empty;` at top
9. In `BackAsync`: add search-close guard at top

### No changes needed

- `IMusicRestClient.cs` — search endpoints already exist
- `HttpMusicRestClient.cs` — already implements search
- `Music/` directory — no new files
