# Chunk Integrity Hardening Plan

> **Goal:** Close 5 identified gaps in the file chunk handling pipeline that can cause "missing chunks" during sync/download.

## Architecture Summary

The chunk architecture is fundamentally solid — content-addressable dedup, CDC chunking, SHA-256 verification, ghost chunk detection, crash-resilient sessions, dual GC services with reference counting. These fixes are targeted hardening of specific edge cases, not architectural changes.

---

## Issues Found (Priority Order)

### Issue 1 — File Modified Between Pass 1 and Pass 2 (CLIENT, HIGH)

**File:** `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`

**Problem:** Upload reads the file twice — Pass 1 computes chunk metadata (hashes only, no data retained), Pass 2 re-reads and uploads actual chunk data. If the file is modified between passes, CDC boundaries shift and Pass 2 produces different chunks than what was registered with the server in Pass 1.

**Result:** Server expects chunk hashes from Pass 1, client sends Pass 2 hashes → hash mismatch, upload fails or produces incomplete file.

**Existing mitigation:** `WaitForFileStabilityAsync()` runs *before* both passes — checks file size stability over a 1-second window. But this doesn't protect against modifications *between* the two passes.

---

### Issue 2 — Race in `CompleteUploadAsync` Between Verify and Use (SERVER, MEDIUM)

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs`

**Problem:** `CompleteUploadAsync` first queries available chunk hashes (verification), then later loads each chunk via `FirstAsync()` (usage). If the hourly background GC (`UploadSessionCleanupService` or `TrashCleanupService`) deletes a `ReferenceCount=0` chunk between these two operations, `FirstAsync()` throws an unhandled `InvalidOperationException`.

**Result:** FileNode + FileVersion already created in DB but FileVersionChunk references incomplete → orphaned metadata, download fails with "missing chunk."

**Window:** Small (hourly GC), but real under concurrent load or slow transactions.

---

### Issue 3 — ZIP Download Silently Skips Missing Chunks (SERVER, MEDIUM)

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/DownloadService.cs` — `AddFileToZipAsync()`

**Problem:** When building a ZIP archive, if a chunk blob is missing from disk, the code does `if (chunkStream is null) continue;` — silently skipping the chunk. Regular single-file download (`BuildStreamFromVersionAsync`) correctly throws `NotFoundException`.

**Result:** User downloads a ZIP containing truncated/corrupted files with no error indication. Inconsistent behavior between download paths.

---

### Issue 4 — No Post-Write Verification on Chunk Storage (SERVER, LOW-MEDIUM)

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/Services/LocalFileStorageEngine.cs`

**Problem:** `WriteChunkAsync()` calls `File.WriteAllBytesAsync()` but never verifies the written file's size matches the source data length. On disk-full, filesystem error, or interrupted write, a truncated blob could be persisted.

**Result:** "Ghost chunk" that exists on disk but is incomplete — downloads return corrupted data without any hash check at read time.

---

### Issue 5 — No Explicit DB Transaction in `CompleteUploadAsync` (SERVER, LOW)

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs`

**Problem:** `CompleteUploadAsync` creates FileNode, FileVersion, FileVersionChunks, increments reference counts, and updates quotas across multiple operations without an explicit transaction. A failure partway through leaves the DB in an inconsistent state.

**Result:** FileNode exists in DB without complete chunk references → download fails.

---

## Implementation Steps

### Phase A: Client-Side File Integrity (Issue 1)

#### Step 1 — Add mtime+size snapshot validation between Pass 1 and Pass 2

In `ChunkedTransferClient.UploadAsync()`:

1. Capture `File.GetLastWriteTimeUtc(localPath)` and `fileStream.Length` **before** Pass 1 (metadata computation)
2. After `InitiateUploadAsync()` returns, just before `fileStream.Seek(0, SeekOrigin.Begin)` starts Pass 2:
   - Re-check `File.GetLastWriteTimeUtc(localPath)` and `new FileInfo(localPath).Length`
   - If either changed, throw `FileModifiedDuringUploadException`
3. New exception class: `FileModifiedDuringUploadException` in `DotNetCloud.Client.Core.Platform` namespace, following `FileStillGrowingException` pattern

**Pattern reference:** The existing crash-resume code already checks `fileSize != existingSession.FileSize` and `File.GetLastWriteTimeUtc(localPath) != existingSession.FileModifiedAt` — same approach.

#### Step 2 — Handle the new exception in SyncEngine

In `SyncEngine.cs` error handling (around line 1205):

- Add catch for `FileModifiedDuringUploadException` alongside existing `FileStillGrowingException` handler
- Defer upload for 5 seconds, don't count against retry budget
- Log at Warning level with file path and size delta

---

### Phase B: Server-Side CompleteUpload Hardening (Issues 2, 5)

#### Step 3 — Wrap `CompleteUploadAsync` in an explicit transaction

At the start of `CompleteUploadAsync()`:

```csharp
await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
```

After the final `SaveChangesAsync()`:

```csharp
await transaction.CommitAsync(cancellationToken);
```

On any exception, the transaction auto-rolls back — ensuring FileNode/FileVersion/FileVersionChunks are atomic.

#### Step 4 — Use `FirstOrDefaultAsync` with explicit missing-chunk error

In the chunk iteration loop (around line 343):

```csharp
// Before (throws InvalidOperationException if chunk deleted by GC):
var chunk = await _db.FileChunks
    .FirstAsync(c => c.ChunkHash == manifest[i], cancellationToken);

// After (clean error message):
var chunk = await _db.FileChunks
    .FirstOrDefaultAsync(c => c.ChunkHash == manifest[i], cancellationToken)
    ?? throw new ValidationException("Chunks",
        $"Chunk '{manifest[i][..12]}…' was removed during completion. Retry the upload.");
```

---

### Phase C: Download Resilience (Issue 3)

#### Step 5 — Fix ZIP download to throw on missing chunks

In `DownloadService.AddFileToZipAsync()`:

```csharp
// Before (silently corrupts ZIP):
if (chunkStream is null) continue;

// After (consistent with BuildStreamFromVersionAsync):
if (chunkStream is null)
{
    _logger.LogWarning("Chunk blob missing from storage for hash '{ChunkHash}' during ZIP assembly.",
        vc.FileChunk.ChunkHash);
    throw new NotFoundException(
        $"File content is unavailable: chunk '{vc.FileChunk.ChunkHash[..8]}…' blob is missing from storage.");
}
```

---

### Phase D: Storage Write Verification (Issue 4)

#### Step 6 — Add post-write size verification to `WriteChunkAsync`

After `File.WriteAllBytesAsync()` in `LocalFileStorageEngine.WriteChunkAsync()`:

```csharp
// Verify the write completed fully — catches disk-full, truncation, filesystem errors.
var writtenSize = new FileInfo(fullPath).Length;
if (writtenSize != data.Length)
{
    try { File.Delete(fullPath); } catch { /* best-effort cleanup */ }
    throw new IOException(
        $"Chunk write verification failed for '{storagePath}': expected {data.Length} bytes, got {writtenSize}.");
}
```

---

## Relevant Files

| File | Steps | Changes |
|------|-------|---------|
| `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` | 1 | Add mtime/size validation between passes |
| `src/Clients/DotNetCloud.Client.Core/Platform/` (new file) | 1 | `FileModifiedDuringUploadException` class |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs` | 2 | Add exception handler |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs` | 3, 4 | Transaction + safer chunk lookup |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/DownloadService.cs` | 5 | Fix silent ZIP chunk skip |
| `src/Modules/Files/DotNetCloud.Modules.Files/Services/LocalFileStorageEngine.cs` | 6 | Post-write size verification |

## Verification

1. **Unit test:** `ChunkedTransferClient` — mock file mtime change between Pass 1 and Pass 2 → verify `FileModifiedDuringUploadException`
2. **Unit test:** `CompleteUploadAsync` — mock chunk disappearing between verify and iteration → verify clean `ValidationException`
3. **Unit test:** `AddFileToZipAsync` — mock null chunk stream → verify `NotFoundException` thrown (not silently skipped)
4. **Unit test:** `WriteChunkAsync` — mock truncated write → verify `IOException` and partial file cleanup
5. **Integration:** corrupt a chunk blob on disk, attempt download → verify clear error message
6. **Manual:** sync a file while editing rapidly → verify client gracefully retries instead of producing corrupt upload
7. **Build:** `dotnet build && dotnet test` passes

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| New `FileModifiedDuringUploadException` class | Follows existing `FileStillGrowingException` pattern; allows sync engine to handle distinctly from other errors |
| ZIP behavior: throw on missing chunks | Consistency with single-file download; silent corruption is worse than a clear error |
| Post-write check: size-only (not hash re-read) | Adequate for disk-full/truncation without I/O overhead of re-hashing the entire chunk |
| Default transaction isolation level | Sufficient — the race window is between verification and chunk load; wrapping in a transaction prevents GC from interfering |

## Out of Scope

These are separate features, not missing-chunk fixes:

- Chunk encryption at rest
- Bandwidth throttling / per-user rate limiting
- Chunk TTL / aging policy for old files
- Full hash verification on every chunk read (too expensive for normal operations)

## Future Considerations

1. **Chunk GC exclusion during active sessions** — Background cleanup could skip chunks that appear in any active (non-expired) upload session's manifest, eliminating Issue 2 at the source. Worth considering if the transaction approach proves insufficient under high concurrency.

2. **Client-side file locking during upload** — Opening with `FileShare.Read` for both passes prevents modification entirely, but could block other applications from writing. The mtime-check approach is less intrusive and recommended first.
