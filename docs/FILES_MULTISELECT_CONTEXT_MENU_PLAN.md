# Files Module — Multi-Select Context Menu Actions

**Branch:** `fix/files-module-multiselect`
**Date:** 2026-08-18
**Scope:** Files module web UI (Blazor Server), Files REST API, and Files data services.

## 1. Goal

Today the Files browser supports selecting multiple cards (via **Select** mode), but the right-click context menu actions (`Move to…`, `Copy to…`, `Share`, `Download`, `Tag`, `Delete`) only act on the single right-clicked card.

Make those actions operate on the **full current selection**:

- `Move to…` and `Copy to…` — open the existing folder picker and apply to all selected nodes.
- `Share` — open a **new bulk-share dialog** that applies one recipient + permission to all selected nodes.
- `Download` — build a **single ZIP** containing all selected nodes (including folder contents). If the resulting ZIP would exceed the maximum ZIP size, show an **informational modal** explaining the problem and the workaround (select fewer/smaller items).
- `Tag` — apply the tag to all selected nodes.
- `Delete` — move all selected nodes to trash, **with a confirmation dialog** (both for the context menu and the existing bulk toolbar "Trash" button).

Single-item behavior must be preserved when only one card is selected (or when right-clicking a card that is not part of the current selection).

## 2. Confirmed decisions

| Decision              | Choice                                                                                                                  |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Multi-select Share    | New dedicated `BulkShareDialog` component (user/team/group shares; public-link bulk sharing out of scope).              |
| Maximum ZIP size      | `4 GiB` default, configurable via `FileUpload:MaxZipSizeBytes`.                                                         |
| Delete confirmation   | Add confirmation to **both** the right-click `Delete` and the bulk toolbar `Trash` button.                              |
| Download behavior     | Single file → existing direct download; folder or multiple items → single ZIP.                                          |
| Right-click selection | Right-clicking a non-selected card selects just that card; right-clicking inside a multi-selection keeps the selection. |

## 3. Files to change (summary)

**Server / backend**

- `src/Modules/Files/DotNetCloud.Modules.Files/Options/FileUploadOptions.cs`
- `src/Core/DotNetCloud.Core.Server/appsettings.json`
- `src/Core/DotNetCloud.Core/Errors/ErrorCodes.cs`
- `src/Core/DotNetCloud.Core/Errors/DotNetCloudExceptions.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/DownloadService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesControllerBase.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`

**Client**

- `src/UI/DotNetCloud.UI.Web/wwwroot/js/files-bulk.js`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileContextMenu.razor`
- **New:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/BulkShareDialog.razor`
- **New:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/BulkShareDialog.razor.cs`

**Tests**

- `tests/DotNetCloud.Modules.Files.Tests/Services/DownloadServiceTests.cs`
- `tests/DotNetCloud.Modules.Files.Tests/Host/` (add a controller mapping test)

**Docs (after implementation)**

- `docs/admin/CONFIGURATION.md`
- `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` (targeted edits, per project rules).

---

## 4. Step-by-step implementation

Implement in the order below. Each step lists the exact file, the exact symbol/method to touch, the current shape, and the required new shape.

### Step 1 — Add `MaxZipSizeBytes` option

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/Options/FileUploadOptions.cs`

Current class has:

```csharp
public sealed class FileUploadOptions
{
    public const string SectionName = "FileUpload";

    public long MaxFileSizeBytes { get; set; } = 16_106_127_360L;

    public string? TmpPath { get; set; }
}
```

Replace the whole class with:

```csharp
public sealed class FileUploadOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "FileUpload";

    /// <summary>
    /// Maximum permitted total file size for a single upload, in bytes.
    /// Default: 15 GB (16,106,127,360 bytes).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 16_106_127_360L;

    /// <summary>
    /// Maximum size of a generated multi-item ZIP download, in bytes.
    /// Default: 4 GiB (4,294,967,296 bytes). Downloads exceeding this fail with a 413.
    /// </summary>
    public long MaxZipSizeBytes { get; set; } = 4_294_967_296L;

    /// <summary>
    /// Directory used for temporary file assembly during downloads.
    /// Set programmatically at startup from <c>DOTNETCLOUD_DATA_DIR</c>.
    /// Falls back to <see cref="Path.GetTempPath"/> when not set.
    /// </summary>
    public string? TmpPath { get; set; }
}
```

---

### Step 2 — Configure the value

**File:** `src/Core/DotNetCloud.Core.Server/appsettings.json`

Find the `FileUpload` section (currently):

```json
  "FileUpload": {
    "MaxFileSizeBytes": 16106127360
  },
```

Change to:

```json
  "FileUpload": {
    "MaxFileSizeBytes": 16106127360,
    "MaxZipSizeBytes": 4294967296
  },
```

---

### Step 3 — Add error code

**File:** `src/Core/DotNetCloud.Core/Errors/ErrorCodes.cs`

In the `// Files & Storage` region, next to `FileTooLarge` (value `"FILE_TOO_LARGE"`), add:

```csharp
    /// <summary>Error code for a ZIP download exceeding the maximum allowed archive size.</summary>
    public const string ZipSizeLimitExceeded = "FILE_ZIP_SIZE_LIMIT_EXCEEDED";
```

---

### Step 4 — Add exception type

**File:** `src/Core/DotNetCloud.Core/Errors/DotNetCloudExceptions.cs`

Add a new public sealed class after `InvalidOperationException` (before `NameConflictException`):

```csharp
/// <summary>
/// Exception thrown when a generated multi-item ZIP download would exceed the
/// configured maximum archive size.
/// </summary>
public sealed class ZipSizeLimitExceededException : DotNetCloudException
{
    /// <summary>Gets the configured maximum ZIP size, in bytes.</summary>
    public long MaxZipSizeBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipSizeLimitExceededException"/> class.
    /// </summary>
    /// <param name="maxZipSizeBytes">The configured maximum ZIP size, in bytes.</param>
    public ZipSizeLimitExceededException(long maxZipSizeBytes)
        : base(
            ErrorCodes.ZipSizeLimitExceeded,
            BuildMessage(maxZipSizeBytes))
    {
        MaxZipSizeBytes = maxZipSizeBytes;
    }

    private static string BuildMessage(long maxZipSizeBytes)
    {
        var limitText = maxZipSizeBytes >= (1L << 30)
            ? $"{maxZipSizeBytes / (1L << 30)} GB"
            : $"{maxZipSizeBytes:N0} bytes";

        return $"The selected items exceed the maximum ZIP download size of {limitText}. "
            + "Select fewer or smaller files and folders, then try again.";
    }
}
```

---

### Step 5 — Enforce the limit in `DownloadService`

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/DownloadService.cs`

5a. Add a private field and initialize it in the constructor.

Current constructor tail:

```csharp
        _shareAccessMembershipResolver = shareAccessMembershipResolver;
        _tmpPath = uploadOptions.Value.TmpPath ?? Path.GetTempPath();
    }
```

Add the field next to `_tmpPath` (near the other private readonly fields at the top of the class) and assign it:

```csharp
    private readonly string _tmpPath;
    private readonly long _maxZipSizeBytes;
```

In the constructor, after `_tmpPath` assignment, add:

```csharp
        _maxZipSizeBytes = uploadOptions.Value.MaxZipSizeBytes > 0
            ? uploadOptions.Value.MaxZipSizeBytes
            : 4_294_967_296L;
```

5b. Replace `DownloadZipAsync` with a version that passes the temp `FileStream` down and enforces the limit. The key changes:

- Keep the existing validations (`nodeIds.Count == 0` and `> 500`).
- Keep the existing temp file creation and try/catch cleanup.
- Pass `zipStream` to `AddFolderToZipAsync` and `AddFileToZipAsync`.
- After each top-level node is added, no explicit check is required because the helpers check (Step 5c/5d) — but adding a check after each top-level node is acceptable for early abort.

New `DownloadZipAsync` body (replace from the `try` block through the final `catch`):

```csharp
        try
        {
            await using (var zipStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, System.IO.FileShare.None, 81920, FileOptions.Asynchronous))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var nodeId in nodeIds.Distinct())
                {
                    var node = await _db.FileNodes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

                    if (node is null)
                        continue;

                    await _permissions.RequirePermissionAsync(nodeId, caller, SharePermission.Read, cancellationToken);

                    if (node.NodeType == FileNodeType.Folder)
                    {
                        await AddFolderToZipAsync(archive, node, node.Name, caller, zipStream, cancellationToken);
                    }
                    else
                    {
                        await AddFileToZipAsync(archive, node, node.Name, zipStream, cancellationToken);
                    }

                    if (zipStream.Length > _maxZipSizeBytes)
                        throw new ZipSizeLimitExceededException(_maxZipSizeBytes);
                }
            }

            _logger.LogInformation("zip.created {NodeCount} {UserId}", nodeIds.Count, caller.UserId);

            return new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                System.IO.FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }
        catch
        {
            try
            { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best-effort */ }
            throw;
        }
```

5c. Change the `AddFolderToZipAsync` signature to accept and pass down the stream, and add a size check after processing children:

Current:

```csharp
    private async Task AddFolderToZipAsync(ZipArchive archive, FileNode folder, string pathPrefix, CallerContext caller, CancellationToken cancellationToken)
    {
        archive.CreateEntry(pathPrefix + "/");

        var children = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.ParentId == folder.Id && !n.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var child in children)
        {
            var childPath = $"{pathPrefix}/{child.Name}";

            if (child.NodeType == FileNodeType.Folder)
            {
                await AddFolderToZipAsync(archive, child, childPath, caller, cancellationToken);
            }
            else
            {
                await AddFileToZipAsync(archive, child, childPath, cancellationToken);
            }
        }
    }
```

New:

```csharp
    private async Task AddFolderToZipAsync(
        ZipArchive archive,
        FileNode folder,
        string pathPrefix,
        CallerContext caller,
        Stream zipStream,
        CancellationToken cancellationToken)
    {
        archive.CreateEntry(pathPrefix + "/");

        var children = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.ParentId == folder.Id && !n.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var child in children)
        {
            var childPath = $"{pathPrefix}/{child.Name}";

            if (child.NodeType == FileNodeType.Folder)
            {
                await AddFolderToZipAsync(archive, child, childPath, caller, zipStream, cancellationToken);
            }
            else
            {
                await AddFileToZipAsync(archive, child, childPath, zipStream, cancellationToken);
            }

            if (zipStream.Length > _maxZipSizeBytes)
                throw new ZipSizeLimitExceededException(_maxZipSizeBytes);
        }
    }
```

5d. Change `AddFileToZipAsync` signature to accept the stream, and add a size check after the entry stream is closed (i.e., after the `await using (entryStream)` block).

Current:

```csharp
    private async Task AddFileToZipAsync(ZipArchive archive, FileNode fileNode, string entryPath, CancellationToken cancellationToken)
    {
        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == fileNode.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);

        if (latestVersion is null)
            return;

        await using var entryStream = entry.Open();

        var versionChunks = await _db.FileVersionChunks
            .AsNoTracking()
            .Include(vc => vc.FileChunk)
            .Where(vc => vc.FileVersionId == latestVersion.Id)
            .OrderBy(vc => vc.SequenceIndex)
            .ToListAsync(cancellationToken);

        foreach (var vc in versionChunks)
        {
            if (vc.FileChunk!.Size == 0)
                continue;

            var chunkStream = await _storageEngine.OpenReadStreamAsync(vc.FileChunk.StoragePath, cancellationToken);
            if (chunkStream is null)
            {
                _logger.LogWarning("Chunk blob missing from storage for hash '{ChunkHash}' during ZIP assembly for file '{EntryPath}'.",
                    vc.FileChunk.ChunkHash, entryPath);
                throw new NotFoundException(
                    $"File content is unavailable: chunk '{vc.FileChunk.ChunkHash[..8]}…' blob is missing from storage.");
            }

            await using (chunkStream)
            {
                await chunkStream.CopyToAsync(entryStream, cancellationToken);
            }
        }
    }
```

New:

```csharp
    private async Task AddFileToZipAsync(
        ZipArchive archive,
        FileNode fileNode,
        string entryPath,
        Stream zipStream,
        CancellationToken cancellationToken)
    {
        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == fileNode.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);

        if (latestVersion is null)
            return;

        await using (var entryStream = entry.Open())
        {
            var versionChunks = await _db.FileVersionChunks
                .AsNoTracking()
                .Include(vc => vc.FileChunk)
                .Where(vc => vc.FileVersionId == latestVersion.Id)
                .OrderBy(vc => vc.SequenceIndex)
                .ToListAsync(cancellationToken);

            foreach (var vc in versionChunks)
            {
                if (vc.FileChunk!.Size == 0)
                    continue;

                var chunkStream = await _storageEngine.OpenReadStreamAsync(vc.FileChunk.StoragePath, cancellationToken);
                if (chunkStream is null)
                {
                    _logger.LogWarning("Chunk blob missing from storage for hash '{ChunkHash}' during ZIP assembly for file '{EntryPath}'.",
                        vc.FileChunk.ChunkHash, entryPath);
                    throw new NotFoundException(
                        $"File content is unavailable: chunk '{vc.FileChunk.ChunkHash[..8]}…' blob is missing from storage.");
                }

                await using (chunkStream)
                {
                    await chunkStream.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }

        if (zipStream.Length > _maxZipSizeBytes)
            throw new ZipSizeLimitExceededException(_maxZipSizeBytes);
    }
```

Note: `ZipSizeLimitExceededException` is in `DotNetCloud.Core.Errors`, already imported by this file (`using DotNetCloud.Core.Errors;`).

---

### Step 6 — Map the exception to HTTP 413

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesControllerBase.cs`

In `ExecuteAsync`, before the final `catch (Exception ex)` block, add:

```csharp
        catch (ZipSizeLimitExceededException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
```

`ZipSizeLimitExceededException` comes from `DotNetCloud.Core.Errors`, already imported (`using DotNetCloud.Core.Errors;`).

---

### Step 7 — Expose `maxZipSizeBytes` in config endpoint

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`

Find:

```csharp
    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        return Ok(new { maxUploadSizeBytes = _uploadOptions.MaxFileSizeBytes });
    }
```

Change to:

```csharp
    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            maxUploadSizeBytes = _uploadOptions.MaxFileSizeBytes,
            maxZipSizeBytes = _uploadOptions.MaxZipSizeBytes
        });
    }
```

---

### Step 8 — Update `files-bulk.js` to return a structured result

**File:** `src/UI/DotNetCloud.UI.Web/wwwroot/js/files-bulk.js`

Replace the entire `downloadZip` function with:

```js
window.dotnetcloudFiles.downloadZip = async function (url, nodeIds) {
  try {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ nodeIds: nodeIds }),
    });

    if (!response.ok) {
      let code = "DOWNLOAD_FAILED";
      let message = "The ZIP download failed.";
      try {
        const envelope = await response.json();
        code =
          envelope && envelope.error && envelope.error.code
            ? envelope.error.code
            : code;
        message =
          envelope && envelope.error && envelope.error.message
            ? envelope.error.message
            : message;
      } catch (e) {
        // Non-JSON error body — keep the default message.
      }
      return { ok: false, code: code, message: message };
    }

    const blob = await response.blob();
    const blobUrl = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = blobUrl;
    a.download = "download.zip";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(blobUrl);
    return { ok: true };
  } catch (err) {
    return { ok: false, code: "DOWNLOAD_FAILED", message: String(err) };
  }
};
```

---

### Step 9 — `FileBrowser.razor.cs`: selection normalization + target helper

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

9a. Update `OnContextMenu` (currently at ~line 937) to normalize the selection before showing the menu.

Current:

```csharp
    [JSInvokable]
    public void OnContextMenu(string nodeId, string nodeType, double x, double y)
    {
        if (Guid.TryParse(nodeId, out var id))
        {
            var node = _nodes.FirstOrDefault(candidate => candidate.Id == id);
            _contextMenuNodeId = id;
            _contextMenuNodeType = node?.NodeType ?? nodeType;
            _contextMenuNodeIsReadOnly = node?.IsReadOnly == true;
            _contextMenuX = x;
            _contextMenuY = y;
            _showContextMenu = true;
            InvokeAsync(StateHasChanged);
        }
    }
```

New:

```csharp
    [JSInvokable]
    public void OnContextMenu(string nodeId, string nodeType, double x, double y)
    {
        if (Guid.TryParse(nodeId, out var id))
        {
            // Right-clicking a card inside the current multi-selection keeps the
            // selection; right-clicking any other card selects just that card.
            if (_selectedNodes.Count <= 1 || !_selectedNodes.Contains(id))
            {
                _selectedNodes.Clear();
                _selectedNodes.Add(id);
            }

            var node = _nodes.FirstOrDefault(candidate => candidate.Id == id);
            _contextMenuNodeId = id;
            _contextMenuNodeType = node?.NodeType ?? nodeType;
            _contextMenuNodeIsReadOnly = node?.IsReadOnly == true;
            _contextMenuX = x;
            _contextMenuY = y;
            _showContextMenu = true;
            InvokeAsync(StateHasChanged);
        }
    }
```

9b. Add a private helper near the context-menu handlers (for example right after `DismissContextMenu`):

```csharp
    /// <summary>Returns the node IDs a context-menu action should target.</summary>
    private IReadOnlyList<Guid> GetContextMenuTargetIds(Guid nodeId)
        => _selectedNodes.Count > 0 && _selectedNodes.Contains(nodeId)
            ? [.. _selectedNodes]
            : [nodeId];
```

---

### Step 10 — Rewrite context-menu handlers

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

Replace these five handlers (keep `HandleContextOpen`, `HandleContextRename`, `HandleContextVersionHistory`, `HandleContextComments` unchanged).

10a. `HandleContextMove` (currently ~line 995):

```csharp
    protected async Task HandleContextMove(Guid nodeId)
    {
        _showContextMenu = false;

        var targetIds = GetContextMenuTargetIds(nodeId);
        _selectedNodes.Clear();
        foreach (var id in targetIds)
            _selectedNodes.Add(id);

        _folderPickerMode = FolderPickerMode.Move;
        await OpenFolderPicker();
    }
```

10b. `HandleContextCopy` (currently ~line 1005):

```csharp
    protected async Task HandleContextCopy(Guid nodeId)
    {
        _showContextMenu = false;

        var targetIds = GetContextMenuTargetIds(nodeId);
        _selectedNodes.Clear();
        foreach (var id in targetIds)
            _selectedNodes.Add(id);

        _folderPickerMode = FolderPickerMode.Copy;
        await OpenFolderPicker();
    }
```

10c. `HandleContextShare` (currently ~line 1015):

```csharp
    protected async Task HandleContextShare(Guid nodeId)
    {
        _showContextMenu = false;

        var targetIds = GetContextMenuTargetIds(nodeId);
        if (targetIds.Count > 1)
        {
            _bulkShareTargetIds = [.. targetIds];
            _showBulkShareDialog = true;
            StateHasChanged();
            return;
        }

        var node = _nodes.FirstOrDefault(n => n.Id == targetIds[0]);
        if (node is not null)
            await ShowShareDialogAsync(node);
    }
```

10d. `HandleContextDownload` (currently ~line 1024):

```csharp
    protected async Task HandleContextDownload(Guid nodeId)
    {
        _showContextMenu = false;

        var targetIds = GetContextMenuTargetIds(nodeId);

        if (targetIds.Count == 1)
        {
            var node = _nodes.FirstOrDefault(n => n.Id == targetIds[0]);
            if (node is not null
                && string.Equals(node.NodeType, "File", StringComparison.OrdinalIgnoreCase))
            {
                await DownloadNodeAsync(node);
                return;
            }
        }

        await DownloadSelectedAsZipAsync(targetIds);
    }
```

10e. `HandleContextDelete` (currently ~line 1033):

```csharp
    protected void HandleContextDelete(Guid nodeId)
    {
        _showContextMenu = false;
        OpenDeleteConfirmation(GetContextMenuTargetIds(nodeId));
    }
```

10f. `HandleContextTag` (currently ~line 1517) — simplify to use the helper:

```csharp
    protected void HandleContextTag(Guid nodeId)
    {
        _showContextMenu = false;

        _tagTargetNodeIds = [.. GetContextMenuTargetIds(nodeId)];
        _singleTagNodeId = nodeId;
        _showSingleTagDialog = true;
    }
```

---

### Step 11 — Add ZIP download result handling + error modal state

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

11a. Add fields near the other dialog state fields (e.g., next to the version-history or comments fields):

```csharp
    // ZIP download error modal
    private bool _showZipErrorModal;
    private string _zipErrorMessage = string.Empty;
```

11b. Add a JS-result DTO (private nested class anywhere in the class, e.g., near the bottom or top):

```csharp
    /// <summary>Result returned by the JS ZIP download helper.</summary>
    private sealed class ZipDownloadResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("code")]
        public string? Code { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }
    }
```

11c. Replace `BulkDownloadZip` with a thin wrapper and add `DownloadSelectedAsZipAsync`.

Current (`BulkDownloadZip`, ~line 1343):

```csharp
    protected async Task BulkDownloadZip()
    {
        if (_selectedNodes.Count == 0)
            return;

        var nodeIds = _selectedNodes.ToList();
        var idsParam = string.Join(",", nodeIds);
        var effectiveUserId = UserId;
        if (effectiveUserId == Guid.Empty)
            effectiveUserId = (await GetCallerContextAsync()).UserId;

        var baseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl) ? string.Empty : ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/files/download-zip?userId={Uri.EscapeDataString(effectiveUserId.ToString())}";

        await Js.InvokeVoidAsync("dotnetcloudFiles.downloadZip", url, nodeIds);
    }
```

New:

```csharp
    /// <summary>Downloads all selected items as a ZIP archive.</summary>
    protected Task BulkDownloadZip() => DownloadSelectedAsZipAsync([.. _selectedNodes]);

    /// <summary>Downloads the given node IDs as a single ZIP archive.</summary>
    private async Task DownloadSelectedAsZipAsync(IReadOnlyList<Guid> nodeIds)
    {
        if (nodeIds.Count == 0)
            return;

        var effectiveUserId = UserId;
        if (effectiveUserId == Guid.Empty)
            effectiveUserId = (await GetCallerContextAsync()).UserId;

        var baseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl) ? string.Empty : ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/files/download-zip?userId={Uri.EscapeDataString(effectiveUserId.ToString())}";

        var result = await Js.InvokeAsync<ZipDownloadResult>(
            "dotnetcloudFiles.downloadZip", url, nodeIds.ToList());

        if (result is not null && !result.Ok)
        {
            _zipErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "The selected items could not be downloaded as a ZIP file."
                : result.Message;
            _showZipErrorModal = true;
            StateHasChanged();
        }
    }

    /// <summary>Closes the ZIP download error modal.</summary>
    protected void HideZipErrorModal() => _showZipErrorModal = false;
```

---

### Step 12 — Delete confirmation

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

12a. Add fields near the ZIP error fields:

```csharp
    // Delete confirmation dialog
    private bool _showDeleteConfirm;
    private List<Guid> _deleteTargetNodeIds = [];
```

12b. Replace `DeleteSelected` + `BulkTrashSelected` (currently ~line 1314–1339) with:

```csharp
    /// <summary>Opens the delete confirmation dialog for the given nodes.</summary>
    private void OpenDeleteConfirmation(IReadOnlyList<Guid> nodeIds)
    {
        _deleteTargetNodeIds = [.. nodeIds];
        _showDeleteConfirm = true;
        StateHasChanged();
    }

    /// <summary>Cancels the delete confirmation dialog.</summary>
    protected void CancelDelete() => _showDeleteConfirm = false;

    /// <summary>Moves the confirmed target nodes to trash.</summary>
    protected async Task ConfirmDeleteAsync()
    {
        _showDeleteConfirm = false;

        if (_deleteTargetNodeIds.Count == 0)
            return;

        var caller = await GetCallerContextAsync();

        foreach (var nodeId in _deleteTargetNodeIds)
        {
            await FileService.DeleteAsync(nodeId, caller);
        }

        _deleteTargetNodeIds = [];
        _selectedNodes.Clear();
        _selectionMode = false;

        await LoadCurrentFolderAsync();
        await LoadTrashCountAsync();
        StateHasChanged();
    }

    // ── Bulk actions ─────────────────────────────────────────────────────────

    /// <summary>Opens the delete confirmation for all selected items.</summary>
    protected void BulkTrashSelected()
    {
        if (_selectedNodes.Count == 0)
            return;

        OpenDeleteConfirmation([.. _selectedNodes]);
    }
```

> Note: the old `DeleteSelected()` method is only referenced by `BulkTrashSelected`; it can be removed. Do **not** touch `TrashBin.razor.cs`, which has its own unrelated `DeleteSelected()`.

---

### Step 13 — Bulk share state + handler

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`

13a. Add fields near the share state (`_shareTargetNode` etc.):

```csharp
    // Bulk share dialog
    private bool _showBulkShareDialog;
    private List<Guid> _bulkShareTargetIds = [];
```

13b. Add computed accessor and handlers near the share handlers (after `HideShareDialog` or near `HandleShareCreatedAsync`):

```csharp
    /// <summary>Nodes targeted by the bulk-share dialog.</summary>
    protected IReadOnlyList<FileNodeViewModel> BulkShareTargetNodes
        => _nodes.Where(n => _bulkShareTargetIds.Contains(n.Id)).ToList();

    /// <summary>Closes the bulk-share dialog.</summary>
    protected void HideBulkShareDialog()
    {
        _showBulkShareDialog = false;
        _bulkShareTargetIds = [];
    }

    /// <summary>Creates the chosen share on every targeted node.</summary>
    protected async Task HandleBulkShareCreatedAsync(BulkShareCreatedEventArgs args)
    {
        if (_bulkShareTargetIds.Count == 0)
            return;

        var caller = await GetCallerContextAsync();
        var dto = new CreateShareDto
        {
            ShareType = args.ShareType,
            SharedWithUserId = args.ShareType == "User" ? args.TargetId : null,
            SharedWithTeamId = args.ShareType == "Team" ? args.TargetId : null,
            SharedWithGroupId = args.ShareType == "Group" ? args.TargetId : null,
            Permission = args.Permission,
            ExpiresAt = args.ExpirationDays > 0 ? DateTime.UtcNow.AddDays(args.ExpirationDays) : null,
            Note = args.Note
        };

        foreach (var nodeId in _bulkShareTargetIds)
        {
            await ShareService.CreateShareAsync(nodeId, dto, caller);
        }

        HideBulkShareDialog();
    }
```

`BulkShareCreatedEventArgs` is defined in Step 15 (new `BulkShareDialog.razor.cs`).

---

### Step 14 — `FileBrowser.razor`: render new dialogs

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor`

14a. Bulk share dialog — add near the existing single `ShareDialog` block (currently around line 402):

```razor
                                    @if (_showBulkShareDialog && _bulkShareTargetIds.Count > 1)
                                    {
                                        <BulkShareDialog Nodes="BulkShareTargetNodes"
                                                         OnClose="HideBulkShareDialog"
                                                         OnSearch="HandleShareSearchAsync"
                                                         OnShareCreated="HandleBulkShareCreatedAsync" />
                                    }
```

14b. Delete confirmation — add near the single-tag dialog (around line 418) or after the rename dialog:

```razor
                                        @if (_showDeleteConfirm)
                                        {
                                            <div class="dialog-overlay" @onclick="CancelDelete">
                                                <div class="dialog" @onclick:stopPropagation>
                                                    <div class="dialog-header">
                                                        <h3>Move to trash?</h3>
                                                        <button class="btn-icon" @onclick="CancelDelete" aria-label="Close" title="Close">X</button>
                                                    </div>
                                                    <div class="dialog-body">
                                                        <p>Move @_deleteTargetNodeIds.Count @(_deleteTargetNodeIds.Count == 1 ? "item" : "items") to the trash? You can restore them from the Trash view.</p>
                                                    </div>
                                                    <div class="dialog-footer">
                                                        <button class="btn btn-danger btn-sm" @onclick="ConfirmDeleteAsync">Delete</button>
                                                        <button class="btn btn-sm" @onclick="CancelDelete">Cancel</button>
                                                    </div>
                                                </div>
                                            </div>
                                        }
```

14c. ZIP error modal — add near the folder-picker block (around line 510):

```razor
                                @if (_showZipErrorModal)
                                {
                                    <div class="dialog-overlay" @onclick="HideZipErrorModal">
                                        <div class="dialog" @onclick:stopPropagation>
                                            <div class="dialog-header">
                                                <h3>Download too large</h3>
                                                <button class="btn-icon" @onclick="HideZipErrorModal" aria-label="Close" title="Close">X</button>
                                            </div>
                                            <div class="dialog-body">
                                                <p>@_zipErrorMessage</p>
                                                <p class="text-muted">Tip: select fewer or smaller files and folders, then try downloading again.</p>
                                            </div>
                                            <div class="dialog-footer">
                                                <button class="btn btn-sm" @onclick="HideZipErrorModal">OK</button>
                                            </div>
                                        </div>
                                    </div>
                                }
```

---

### Step 15 — Create the `BulkShareDialog` component

**New file:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/BulkShareDialog.razor`

Reference the existing `ShareDialog.razor` markup for classes/patterns. Use the existing CSS classes (`share-dialog-overlay`, `share-dialog`, `share-header`, `share-body`, `share-search-row`, `share-search-wrapper`, `share-search-input`, `share-search-results`, `share-search-result-item`, `share-permission-select`, `form-control`, `btn`, etc.) so existing styles apply.

```razor
@namespace DotNetCloud.Modules.Files.UI

<div class="share-dialog-overlay" @onclick="HandleOverlayClick">
    <div class="share-dialog" @onclick:stopPropagation>
        <div class="share-header">
            <h3>Share @(Nodes.Count) items</h3>
            <button class="btn-icon" @onclick="Close" aria-label="Close" title="Close">✕</button>
        </div>

        <div class="share-body">
            <section class="share-new-section">
                <h4>Add people or teams</h4>

                <div class="share-search-row">
                    <div class="share-search-wrapper">
                        <input type="text"
                               class="form-control share-search-input"
                               placeholder="Search users, teams, or groups…"
                               @bind="SearchQuery"
                               @bind:event="oninput"
                               @bind:after="OnSearchInputAsync"
                               aria-label="Search for share recipients" />

                        @if (IsSearching)
                        {
                            <span class="share-search-spinner" aria-hidden="true">…</span>
                        }

                        @if (SearchResults.Count > 0)
                        {
                            <ul class="share-search-results" role="listbox">
                                @foreach (var result in SearchResults)
                                {
                                    <li class="share-search-result-item @(result.Id == SelectedSearchResult?.Id ? "selected" : "")"
                                        role="option"
                                        @onclick="() => SelectSearchResult(result)">
                                        <span class="result-type-badge badge-@result.ResultType.ToLowerInvariant()">
                                            @result.ResultType
                                        </span>
                                        <span class="result-name">@result.DisplayName</span>
                                    </li>
                                }
                            </ul>
                        }
                    </div>

                    <select class="form-control share-permission-select" @bind="NewSharePermission">
                        <option value="Read">Read</option>
                        <option value="ReadWrite">Read/Write</option>
                        <option value="Full">Full</option>
                    </select>
                </div>

                <div class="share-note-row">
                    <input type="text" class="form-control" placeholder="Note (optional)" @bind="Note" />
                </div>

                <div class="share-actions">
                    <button class="btn btn-primary btn-sm"
                            @onclick="CreateShareAsync"
                            disabled="@(SelectedSearchResult is null || IsCreatingShare)">
                        Share with @Nodes.Count items
                    </button>
                </div>

                @if (!string.IsNullOrWhiteSpace(ShareErrorMessage))
                {
                    <p class="text-danger">@ShareErrorMessage</p>
                }
            </section>
        </div>
    </div>
</div>
```

**New file:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/BulkShareDialog.razor.cs`

```csharp
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Dialog for sharing multiple files/folders with a single recipient at once.
/// The parent is responsible for creating the share on every target node.
/// </summary>
public partial class BulkShareDialog : ComponentBase
{
    /// <summary>The nodes being shared.</summary>
    [Parameter] public IReadOnlyList<FileNodeViewModel> Nodes { get; set; } = [];

    /// <summary>Raised when the dialog should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Search callback for users/teams/groups. Returns matching results.</summary>
    [Parameter] public Func<string, Task<IReadOnlyList<ShareSearchResult>>>? OnSearch { get; set; }

    /// <summary>Raised when the user confirms the share. Parent creates the share on all nodes.</summary>
    [Parameter] public EventCallback<BulkShareCreatedEventArgs> OnShareCreated { get; set; }

    private List<ShareSearchResult> _searchResults = [];
    private ShareSearchResult? _selectedSearchResult;
    private string _searchQuery = string.Empty;
    private string _newSharePermission = "Read";
    private string _note = string.Empty;
    private bool _isSearching;
    private bool _isCreatingShare;
    private string _shareErrorMessage = string.Empty;
    private bool _overlayMouseDown;

    protected IReadOnlyList<ShareSearchResult> SearchResults => _searchResults;
    protected ShareSearchResult? SelectedSearchResult => _selectedSearchResult;

    protected string SearchQuery
    {
        get => _searchQuery;
        set => _searchQuery = value;
    }

    protected string NewSharePermission
    {
        get => _newSharePermission;
        set => _newSharePermission = value;
    }

    protected string Note
    {
        get => _note;
        set => _note = value;
    }

    protected bool IsSearching => _isSearching;
    protected bool IsCreatingShare => _isCreatingShare;
    protected string ShareErrorMessage => _shareErrorMessage;

    protected async Task OnSearchInputAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery) || _searchQuery.Length < 2)
        {
            _searchResults = [];
            return;
        }

        if (OnSearch is null)
            return;

        _isSearching = true;
        StateHasChanged();

        var results = await OnSearch(_searchQuery);
        _searchResults = [.. results];
        _isSearching = false;
    }

    protected void SelectSearchResult(ShareSearchResult result)
    {
        _selectedSearchResult = result;
        _searchResults = [];
        _searchQuery = result.DisplayName;
    }

    protected async Task CreateShareAsync()
    {
        if (_selectedSearchResult is null)
            return;

        _shareErrorMessage = string.Empty;
        _isCreatingShare = true;
        StateHasChanged();

        await OnShareCreated.InvokeAsync(new BulkShareCreatedEventArgs
        {
            ShareType = _selectedSearchResult.ResultType,
            TargetId = _selectedSearchResult.Id,
            TargetName = _selectedSearchResult.DisplayName,
            Permission = _newSharePermission,
            Note = string.IsNullOrWhiteSpace(_note) ? null : _note
        });

        _isCreatingShare = false;
    }

    protected void HandleOverlayMouseDown() => _overlayMouseDown = true;

    protected void HandleOverlayClick()
    {
        if (_overlayMouseDown)
            Close();

        _overlayMouseDown = false;
    }

    protected async void Close() => await OnClose.InvokeAsync();
}

/// <summary>Event args raised when a bulk share is confirmed.</summary>
public sealed class BulkShareCreatedEventArgs
{
    /// <summary>Share type: "User", "Team", or "Group".</summary>
    public string ShareType { get; init; } = string.Empty;

    /// <summary>Target entity ID (user, team, or group).</summary>
    public Guid TargetId { get; init; }

    /// <summary>Display name of the target.</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Permission level: "Read", "ReadWrite", or "Full".</summary>
    public string Permission { get; init; } = "Read";

    /// <summary>Expiration in days (0 = never).</summary>
    public int ExpirationDays { get; init; }

    /// <summary>Optional note.</summary>
    public string? Note { get; init; }
}
```

> `ShareSearchResult` and `FileNodeViewModel` are already defined in `DotNetCloud.Modules.Files.UI` (`ViewModels.cs`), so no extra using is needed.

---

### Step 16 — Show `Download` for folders in the context menu

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileContextMenu.razor`

16a. In the **non-read-only** branch, the Download and Version History buttons are currently nested inside `@if (NodeType != "Folder")`. Move `Download` outside that block so it appears for both files and folders; keep `Version History` files-only.

Replace this section:

```razor
            @if (NodeType != "Folder")
            {
                <button class="context-menu-item" role="menuitem" @onclick="HandleDownload">
                    <span class="context-menu-icon" aria-hidden="true">⬇</span>
                    <span>Download</span>
                </button>
                <button class="context-menu-item" role="menuitem" @onclick="HandleVersionHistory">
                    <span class="context-menu-icon" aria-hidden="true">📋</span>
                    <span>Version History</span>
                </button>
            }
```

with:

```razor
            <button class="context-menu-item" role="menuitem" @onclick="HandleDownload">
                <span class="context-menu-icon" aria-hidden="true">⬇</span>
                <span>Download</span>
            </button>

            @if (NodeType != "Folder")
            {
                <button class="context-menu-item" role="menuitem" @onclick="HandleVersionHistory">
                    <span class="context-menu-icon" aria-hidden="true">📋</span>
                    <span>Version History</span>
                </button>
            }
```

16b. In the **read-only** branch, change `else if (NodeType != "Folder")` to `else` so folders also get Download:

Current:

```razor
        else if (NodeType != "Folder")
        {
            <div class="context-menu-separator" role="separator"></div>
            <button class="context-menu-item" role="menuitem" @onclick="HandleDownload">
                <span class="context-menu-icon" aria-hidden="true">⬇</span>
                <span>Download</span>
            </button>
        }
```

New:

```razor
        else
        {
            <div class="context-menu-separator" role="separator"></div>
            <button class="context-menu-item" role="menuitem" @onclick="HandleDownload">
                <span class="context-menu-icon" aria-hidden="true">⬇</span>
                <span>Download</span>
            </button>
        }
```

---

### Step 17 — Tests

**File:** `tests/DotNetCloud.Modules.Files.Tests/Services/DownloadServiceTests.cs`

Add a test that a multi-item ZIP exceeding `MaxZipSizeBytes` throws `ZipSizeLimitExceededException`. The existing test helper `CreateService` passes `new FileUploadOptions()` (which now has the 4 GiB default). To force a small limit, add an overload or construct the service directly with a custom option.

Suggested test:

```csharp
    [TestMethod]
    public async Task DownloadZipAsync_ExceedsMaxZipSize_ThrowsZipSizeLimitExceededException()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();

        var chunkData = Encoding.UTF8.GetBytes(new string('a', 1024));
        var node = new FileNode { Name = "big.bin", NodeType = FileNodeType.File, OwnerId = userId, Size = chunkData.Length };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "hash1", StoragePath = "chunks/ha/sh/hash1", Size = chunkData.Length };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = chunkData.Length,
            ContentHash = "hash1",
            StoragePath = "files/test",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);
        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var options = Options.Create(new FileUploadOptions { MaxZipSizeBytes = 128 });
        var service = new DownloadService(
            db,
            storageMock.Object,
            NullLogger<DownloadService>.Instance,
            new PermissionService(db),
            options);

        await Assert.ThrowsExactlyAsync<ZipSizeLimitExceededException>(
            () => service.DownloadZipAsync([node.Id], UserCaller(userId)));
    }
```

Also add a within-limit test if practical: reuse the same setup but with `MaxZipSizeBytes = 1_000_000` and assert a non-null stream is returned and readable.

**New file:** `tests/DotNetCloud.Modules.Files.Tests/Host/ZipSizeLimitExceededMappingTests.cs` (or add to an existing Host test file)

Verify `FilesControllerBase.ExecuteAsync` maps `ZipSizeLimitExceededException` to 413. A lightweight approach: subclass `FilesControllerBase` in the test exposing `ExecuteAsync`, then assert the `IActionResult` is a `StatusCodeResult` with status 413 and the envelope error code. Follow the existing controller-test patterns in `tests/DotNetCloud.Modules.Files.Tests/Host/`.

---

### Step 18 — Documentation

**File:** `docs/admin/CONFIGURATION.md`

In the "Upload Limits" section, add a row and example for `MaxZipSizeBytes` (4 GiB default, applied to multi-item ZIP downloads).

Per the project's `.github/copilot-instructions.md`, after the implementation is complete and builds/tests pass, also update:

- `docs/IMPLEMENTATION_CHECKLIST.md` (mark completed items `✓`)
- `docs/MASTER_PROJECT_PLAN.md` (Quick Status Summary table + step status/deliverables/notes)

using targeted edits.

---

## 5. Order of implementation and dependencies

1. **Steps 1–7** (backend) — independent; do first so the API contract is stable.
2. **Step 8** (JS result contract) — depends on Step 6 (server returns envelope).
3. **Steps 9–14** (Blazor logic + markup) — depends on Steps 8, 12, 13.
4. **Step 15** (new `BulkShareDialog`) — independent; needed by Step 13/14.
5. **Step 16** (context menu visibility) — independent.
6. **Step 17** (tests) — after backend + client code compile.
7. **Step 18** (docs) — last.

Suggested commit chunks (optional):

- Commit A: backend ZIP limit + error mapping + config endpoint (Steps 1–7).
- Commit B: JS + Blazor download/error handling (Steps 8, 11, 14c).
- Commit C: multi-select targeting + delete confirmation + context menu download visibility (Steps 9, 10, 12, 14b, 16).
- Commit D: bulk share dialog (Steps 13, 14a, 15).
- Commit E: tests + docs.

## 6. Verification checklist

```bash
# Build using the CI solution filter (avoids the Android SDK requirement)
dotnet build DotNetCloud.CI.slnf -c Release

# Run Files tests
dotnet test tests/DotNetCloud.Modules.Files.Tests
```

Manual UI checks:

1. Enter **Select** mode, check multiple cards (files + folders), right-click a selected card.
2. **Move to…** → folder picker opens → confirm → all selected items move.
3. **Copy to…** → folder picker opens → confirm → all selected items copy.
4. **Share** → new bulk dialog opens showing item count → pick a user/team → confirm → shares created on all selected items.
5. **Download** with a folder + files selected → single `download.zip` downloads with folder hierarchy preserved.
6. **Download** selection exceeding 4 GiB → modal appears explaining the limit and "select fewer/smaller items".
7. **Tag** → tag applied to all selected items.
8. **Delete** via context menu → confirmation appears → confirm → items trashed.
9. **Trash** via bulk toolbar → same confirmation appears.
10. Right-click a card **not** in the current selection → only that card is affected (single-item behavior preserved).
11. Right-click a single file → Download performs the existing direct download (no ZIP).

## 7. Risks / notes

- `zipStream.Length` reflects the actual bytes written to the temp ZIP file. Checking it after each file entry and after each top-level node is sufficient for the 4 GiB limit. A single huge file will be streamed into the temp file before the limit triggers at entry close; the existing `catch` deletes the temp file, so disk usage is bounded per request.
- The bulk share dialog deliberately omits public-link sharing (one public link maps to one node). Bulk public links are out of scope for this change.
- `FileBrowser` runs Blazor Server; `Js.InvokeAsync<ZipDownloadResult>` deserializes the JS object. Use `[JsonPropertyName]` to avoid casing ambiguity.
- Do not modify `TrashBin.razor.cs` `DeleteSelected` (separate component).
- Keep all public members XML-documented (repo convention: `TreatWarningsAsErrors`).
