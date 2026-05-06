# Plan: Implement "Browse Files" in Email Compose

**TL;DR:** Follow the existing cross-module capability pattern (like `IContactDirectory`) to let the email compose form browse and attach files from the user's Files module storage. No HTTP calls — use DI-injected service interface.

## Pattern (as used by `IContactDirectory`)

1. Define interface + DTO in `DotNetCloud.Core/Capabilities/`
2. Implement in the providing module's `.Data` project
3. Register in DI via the module's `ServiceRegistration`
4. Declare in consumer's `ModuleManifest` as `RequiredCapability`
5. Inject into the Blazor component and use

---

## Steps

### Phase 1 — Create `IFileDirectory` capability interface

**New file:** `src/Core/DotNetCloud.Core/Capabilities/IFileDirectory.cs`
- `Task<IReadOnlyList<FileNodeInfo>> ListChildrenAsync(Guid userId, Guid? parentId, CancellationToken ct = default)` — list folder contents
- `Task<Stream?> OpenReadAsync(Guid userId, Guid fileNodeId, CancellationToken ct = default)` — get file content stream by node ID

**New file:** `src/Core/DotNetCloud.Core/Capabilities/FileNodeInfo.cs`
- `Id`, `Name`, `NodeType` ("File"/"Folder"), `MimeType`, `Size`, `ParentId`
- Simple record, no dependency on Files module types

*Tier:* Public (same as `IContactDirectory`)

### Phase 2 — Implement `FileDirectoryService` in Files.Data

**New file:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/FileDirectoryService.cs`
- Implements `IFileDirectory`
- Injects `FilesDbContext` + `IFileStorageEngine`
- `ListChildrenAsync` queries `FileNode` table filtered by `OwnerId == userId`. If `parentId` is null, return root-level files. If `parentId` is set, return children of that folder.
- `OpenReadAsync` resolves the file node, gets its `ContentHash`, and opens a read stream from `IFileStorageEngine`

### Phase 3 — Register in DI

**Modify:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`
- Add `services.AddScoped<IFileDirectory, FileDirectoryService>();`

### Phase 4 — Declare in Email module manifest

**Modify:** `src/Modules/Email/DotNetCloud.Modules.Email/EmailModuleManifest.cs`
- Add `nameof(Core.Capabilities.IFileDirectory)` to `RequiredCapabilities`

### Phase 5 — Add backend endpoint to attach a Files file as email attachment

**Modify:** `src/Modules/Email/DotNetCloud.Modules.Email.Host/Controllers/EmailController.cs`
- Add `POST /api/v1/email/attach-from-files/{fileNodeId:guid}`
- Injects `IFileDirectory?` (nullable — capability may not be granted)
- Opens stream via `IFileDirectory.OpenReadAsync`
- Stores via `IAttachmentStorage.StoreAsync`
- Returns `UploadAttachmentResult`

### Phase 6 — Add API client method

**Modify:** `src/Modules/Email/DotNetCloud.Modules.Email/Services/IEmailApiClient.cs`
- Add `Task<UploadAttachmentResult> AttachFromFilesModuleAsync(Guid fileNodeId, CancellationToken ct = default)`

**Modify:** `src/Modules/Email/DotNetCloud.Modules.Email/Services/EmailApiClient.cs`
- Implement: POST to `api/v1/email/attach-from-files/{fileNodeId}`, parse response

### Phase 7 — Create file picker modal

**New file:** `src/Modules/Email/DotNetCloud.Modules.Email/UI/FilePickerModal.razor`
- Full-screen modal dialog
- Injects `IFileDirectory?` (nullable) directly
- Shows files/folders in a list with name, type icon, size
- Click folder → navigate into it
- Click file → highlight/select
- "Attach Selected" button → calls `_apiClient.AttachFromFilesModuleAsync(fileNodeId)` for each selected file, adds result to compose form's `_attachments`
- "Back" button for folder navigation, breadcrumbs
- Close/cancel button

### Phase 8 — Wire up "Browse Files" button

**Modify:** `src/Modules/Email/DotNetCloud.Modules.Email/UI/EmailComposeForm.razor`
- Remove `disabled` + `title="Coming soon"` from the Browse Files button
- When clicked, set a `_showFilePicker` flag to true
- Render `<FilePickerModal>` conditionally when `_showFilePicker` is true
- Pass a callback `OnFilesSelected` that adds chosen files to `_attachments`

---

## Files Summary

| File | Action |
|------|--------|
| `src/Core/DotNetCloud.Core/Capabilities/IFileDirectory.cs` | **Create** — capability interface |
| `src/Core/DotNetCloud.Core/Capabilities/FileNodeInfo.cs` | **Create** — DTO record |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/FileDirectoryService.cs` | **Create** — implementation |
| `.../FilesServiceRegistration.cs` | **Edit** — add `AddScoped<IFileDirectory, FileDirectoryService>()` |
| `.../EmailModuleManifest.cs` | **Edit** — add `IFileDirectory` to RequiredCapabilities |
| `.../EmailController.cs` | **Edit** — add `POST attach-from-files/{fileNodeId}` endpoint |
| `.../Services/IEmailApiClient.cs` | **Edit** — add `AttachFromFilesModuleAsync` |
| `.../Services/EmailApiClient.cs` | **Edit** — implement `AttachFromFilesModuleAsync` |
| `.../UI/FilePickerModal.razor` | **Create** — file browser modal |
| `.../UI/EmailComposeForm.razor` | **Edit** — wire up Browse Files button |

## Verification

1. `dotnet build DotNetCloud.CI.slnf`
2. `dotnet test DotNetCloud.CI.slnf`
3. Open Email → Compose → click "Browse Files" → verify modal opens with user's Files module files
4. Select a file → click "Attach Selected" → verify it appears in attachment list
5. Send email → verify file is attached in received email
6. Test folder navigation in file picker (subfolders, back navigation)

## Out of Scope

- Search/filter in file picker (future enhancement)
- Multi-file selection in a single pick operation (can attach one at a time for now)
- Drag-and-drop from Files module
