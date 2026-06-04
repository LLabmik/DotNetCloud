# Blazor Component DI Registration Plan

## Problem

Blazor components render in Core.Server's process but inject module business services (`IFileService`, `IArtistService`, etc.) that are only registered in the module host processes' DI containers. Many implementations are `internal` to their module's `.Data` assembly.

Scope: **40+ missing service registrations** across 6 modules.

## Architecture Constraint

Calling the existing `AddXxxServices()` from Core.Server would register hosted services (`MusicEnrichmentBackgroundService`, `PhotoIndexingBackgroundService`, `VideoEnrichmentBackgroundService`) that would run in BOTH Core.Server AND the module host → duplicate processing.

## Solution

Add a new `AddXxxUiServices()` extension method in each module's `.Data` project that registers ONLY the services that Blazor UI components inject. These methods can reference `internal` types because they're in the same assembly.

### What each `AddXxxUiServices` registers

| Method                | Assembly      | Interfaces Registered                                                                                                                                                                                                                                                                                                                                            |
| --------------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AddFilesUiServices`  | `Files.Data`  | `IFileService`, `IChunkedUploadService`, `ICollaboraDiscoveryService`, `ITrashService`, `IQuotaService`, `IVersionService`, `IShareService`, `ITagService`, `ICommentService`, `IPermissionService`, `IFileDirectory`, `IDownloadService`, `ISyncService`, `IDeviceContext`, `IStorageMetricsService`, `IThumbnailService`, `IVideoFrameExtractor`               |
| `AddMusicUiServices`  | `Music.Data`  | `IArtistService`, `IMusicAlbumService`, `ITrackService`, `IPlaylistService`, `IPlaybackService`, `IEqPresetService`, `IRecommendationService`, `IMusicStreamingService`, `IMetadataEnrichmentService`, `IMusicEnrichmentBackgroundQueue`, `MusicPlaybackState`, `ActivePlaylistContext`, `ScanProgressState`, `IMusicIndexingCallback`                           |
| `AddPhotosUiServices` | `Photos.Data` | `IPhotoService`, `IAlbumService`, `IPhotoShareService`, `IPhotoEditService`, `ISlideshowService`, `IPhotoThumbnailService`, `IPhotoGeoService`, `IPhotoIndexingCallback`, `PhotoMetadataService`                                                                                                                                                                 |
| `AddVideoUiServices`  | `Video.Data`  | `IVideoService`, `IVideoCollectionService`, `ISubtitleService`, `IWatchProgressService`, `IVideoMetadataService`, `IVideoStreamingService`, `IVideoSeriesService`, `IVideoSettingsProvider`, `IVideoThumbnailService`, `IVideoEnrichmentService`, `IVideoEnrichmentBackgroundQueue`, `VideoScanProgressState`, `IVideoIndexingCallback`                          |
| `AddTracksUiServices` | `Tracks.Data` | `ITracksSignalRService`, `ProductService`, `WorkItemService`, `SprintService`, `SprintPlanningService`, `SwimlaneService`, `CommentService`, `ChecklistService`, `DependencyService`, `TimeTrackingService`, `AttachmentService`, `AnalyticsService`, `PokerService`, `ReviewSessionService`, `ActivityService`, `ItemTemplateService`, `ProductTemplateService` |
| `AddNotesUiServices`  | `Notes.Data`  | `IMarkdownRenderer`, `INoteService`, `INotebookService`, `INoteExportService`                                                                                                                                                                                                                                                                                    |

### What is NOT registered (module host only)

- **Hosted services**: `MusicEnrichmentBackgroundService`, `PhotoIndexingBackgroundService`, `VideoEnrichmentBackgroundService`
- **DbContexts**: Already registered via `AddModuleDbContexts`
- **Event handlers**: `FileUploadedPhotoHandler`, `FileUploadedMusicHandler`, etc. (only needed in module hosts)
- **Internal-only services**: Anything not injected by a Blazor component

### What stays `internal`

Nothing needs to change visibility. All `AddXxxUiServices` methods are in the same assembly as the `internal` implementations they reference.

### What goes in Program.cs

```csharp
// Module UI services (Blazor components render in Core.Server process)
builder.Services.AddFilesUiServices(builder.Configuration);
builder.Services.AddMusicUiServices(builder.Configuration);
builder.Services.AddPhotosUiServices(builder.Configuration);
builder.Services.AddVideoUiServices(builder.Configuration);
builder.Services.AddTracksUiServices(builder.Configuration);
builder.Services.AddNotesUiServices(builder.Configuration);
```

### Existing Music registrations to remove

These are redundant once `AddMusicUiServices` is called:

- `MusicPlaybackState` (individual)
- `ActivePlaylistContext` (individual)
- `IPlaybackService` (individual)
- `IEqPresetService` (individual)
- `IPlaylistService` (individual)

## Implementation Order

1. Add `AddNotesUiServices` in `NotesServiceRegistration.cs` — simplest, only `IMarkdownRenderer`
2. Add `AddTracksUiServices` in `TracksServiceRegistration.cs`
3. Add `AddFilesUiServices` in `FilesServiceRegistration.cs`
4. Add `AddMusicUiServices` in `MusicServiceRegistration.cs`
5. Add `AddPhotosUiServices` in `PhotosServiceRegistration.cs`
6. Add `AddVideoUiServices` in `VideoServiceRegistration.cs`
7. Update `Program.cs` — add calls, remove redundant Music registrations
8. Build, deploy, verify

## Verification

After deploy, check journal for zero `Unhandled exception in circuit` errors:

```bash
sudo journalctl -u dotnetcloud --since "1 min ago" --no-pager | grep "Unhandled exception rendering component"
# Should produce NO output
```
