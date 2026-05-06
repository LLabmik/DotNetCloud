# Plan: Email Attachment Support (Inbound + Outbound)

> **Version:** 1.0
> **Created:** 2026-05-06
> **Status:** Design phase — ready for implementation

---

## TL;DR

Add full attachment support to the email module: (1) download and store attachment content during IMAP/Gmail sync, (2) allow composing and sending emails with attachments (via browser upload or Files module picker), (3) provide attachment download and "Save to Files" detach UI. Attachments stored using content-addressable storage (SHA-256) in a dedicated directory, with a storage abstraction shared between providers.

---

## Discovery Summary

### Current State

The module has **attachment metadata infrastructure** but **no content handling**:

| Area | What Exists | What's Missing |
|------|------------|----------------|
| `EmailAttachment` model | Full model with StorageKey, ContentHash, ContentType, Size, FileName, ContentId | ⚠️ StorageKey/ContentHash never populated |
| EF Core config | `EmailAttachmentConfiguration` with FK, cascade delete, indexes | ✅ Complete |
| IMAP sync (`ImapSmtpEmailProvider.SyncMailboxAsync`) | Creates `EmailAttachment` records with FileName, ContentType, Size | ❌ No content downloaded, no StorageKey set |
| Gmail sync (`GmailEmailProvider.SyncMailboxAsync`) | Creates `EmailAttachment` records with FileName, ContentType, Size | ❌ No content downloaded, no StorageKey set |
| Email send (IMAP/SMTP `SendAsync`) | MIME message built via MimeKit's `BodyBuilder` | ❌ No attachment handling, `EmailSendRequest` has no attachment fields |
| Email send (Gmail `BuildMimeMessage`) | Manual string-based MIME construction | ❌ No MIME multipart, no attachment handling |
| `EmailSendRequest` | Fields: To, Cc, Bcc, Subject, BodyHtml, BodyPlainText, InReplyToMessageId, References | ❌ No `Attachments` field |
| `EmailComposeForm` | Full compose UI with To/Cc/Bcc/Subject/Body | ❌ No attachment upload UI |
| `EmailPage.razor` message view | Attachment chips displayed (📎 filename) | ❌ Not clickable/downloadable |
| Storage infrastructure | `IStorageProvider` marker interface (empty) | ❌ No actual storage methods |
| REST API (`EmailController`) | Send endpoint (`POST .../send`) expects JSON body | ❌ Can't accept file uploads; no download endpoint |

### Architecture Context

- Email module has 3 projects: `DotNetCloud.Modules.Email` (models/UI/events), `DotNetCloud.Modules.Email.Data` (EF Core/services/implementations), `DotNetCloud.Modules.Email.Host` (REST API/gRPC)
- Both `ImapSmtpEmailProvider` and `GmailEmailProvider` implement `IEmailProvider`
- Both providers already iterate message attachments during sync but only save metadata (name, type, size)
- MimeKit is already a dependency (used in IMAP provider for body extraction and SMTP sending)
- The Files module uses content-addressable storage with SHA-256 hash prefix directories — the same pattern should be reused for email attachments
- `IStorageProvider` in Core is an empty marker interface (no methods); not usable as-is

---

## Steps

### Phase A — Storage Abstraction (Foundation)

**A1. Define `IAttachmentStorage` interface** in `DotNetCloud.Modules.Email/Services/`

```csharp
public interface IAttachmentStorage
{
    /// <summary>Stores attachment content and returns storage metadata.</summary>
    Task<AttachmentStorageResult> StoreAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Opens a read stream for an attachment by storage key.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Deletes an attachment by storage key.</summary>
    Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Gets the file size of a stored attachment.</summary>
    Task<long> GetSizeAsync(string storageKey, CancellationToken ct = default);
}

public sealed record AttachmentStorageResult
{
    public string StorageKey { get; init; } = string.Empty;   // SHA-256 hex
    public string ContentHash { get; init; } = string.Empty;   // Same SHA-256 hex
    public long Size { get; init; }
    public DateTime StoredAt { get; init; } = DateTime.UtcNow;
}
```

**A2. Implement `FileSystemAttachmentStorage`** in `DotNetCloud.Modules.Email.Data/Services/`

- Store files at `{basePath}/attachments/{hash[0..2]}/{hash[2..4]}/{hash}` (same prefix directory pattern as Files module's `LocalFileStorageEngine`)
- Compute SHA-256 hash during write
- Reads/writes with 80 KB buffer (matching Files module pattern)
- Track creation timestamp for TTL-based cleanup (orphaned compose uploads)
- Configurable base path from module configuration

**A3. Register services** in `EmailServiceRegistration.cs`

- Add `IAttachmentStorage → FileSystemAttachmentStorage` as a scoped service
- Add storage configuration options (base path, max attachment size)

---

### Phase B — Inbound Attachments (Receiving)

**B1. Enhance IMAP sync to download attachment content** in `ImapSmtpEmailProvider.SyncMailboxAsync`

- Replace current stub attachment creation (which only saves FileName/ContentType/Size) with full content download
- Use MimeKit's `message.Attachments` enumeration and `message.BodyPart` traversal
- For each attachment `BodyPartBasic`:
  - Fetch the content via `folder.GetMessageAsync()` or `folder.GetBodyPartAsync()`
  - Stream the decoded content through `IAttachmentStorage.StoreAsync()`
  - Populate `StorageKey`, `ContentHash`, `Size` on the `EmailAttachment` record
- For inline images (Content-ID), set `ContentId` and mark them for inline display (not shown as attachment chips)
- Handle `multipart/alternative`, `multipart/mixed`, `multipart/related` nesting

**B2. Enhance Gmail sync to download attachment content** in `GmailEmailProvider.SyncMailboxAsync`

- Replace current stub attachment creation with full content download
- Use Gmail API's `Users.Messages.Attachments.Get` to fetch each attachment's base64url data by `AttachmentId`
- Decode base64url → wrap in `MemoryStream` → call `IAttachmentStorage.StoreAsync()`
- Populate `StorageKey`, `ContentHash`, `Size` on the `EmailAttachment` record
- Handle inline images (Content-ID) same as IMAP

**B3. Add attachment download API endpoint** in `EmailController.cs`

```
GET /api/v1/email/attachments/{attachmentId}/download[?inline=true]
```

- Verify caller owns the parent account (message ownership chain)
- `IAttachmentStorage.OpenReadAsync(storageKey)` to get content stream
- Return `FileStreamResult` with:
  - `Content-Type` from `EmailAttachment.ContentType`
  - `Content-Disposition: attachment; filename="..."` (or `inline` if `?inline=true`)
- 404 if storage key missing, 403 if not owner

**B4. Add attachment download methods** in `IEmailApiClient` / `EmailApiClient`

- `DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct)` → returns `(Stream, string fileName, string contentType)`
- Or generate download URL for direct browser navigation: `GetAttachmentDownloadUrl(Guid attachmentId)`

**B5. Make attachment chips clickable** in `EmailPage.razor`

- Replace `<span class="email-attachment-chip">` with `<a class="email-attachment-chip" href="..." download>` elements
- Link to download endpoint with proper filename
- Display file size (formatted as KB/MB) alongside filename
- Add download icon (⬇️ or SVG)
- Show file type icon based on MIME category (image, document, archive, etc.)

---

### Phase C — Outbound Attachments (Sending)

**C1. Add attachment fields to `EmailSendRequest`**

```csharp
public sealed record EmailSendRequest
{
    // ... existing fields ...

    /// <summary>Attachments to include when sending.</summary>
    public IReadOnlyList<EmailAttachmentRef>? Attachments { get; init; }
}

public sealed record EmailAttachmentRef
{
    /// <summary>Storage key from pre-upload (browser upload or Files module ref).</summary>
    public required string StorageKey { get; init; }

    /// <summary>Original filename to use in the MIME message.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Optional Content-ID for inline embedding.</summary>
    public string? ContentId { get; init; }

    /// <summary>Whether this is an inline image (vs. regular attachment).</summary>
    public bool IsInline { get; init; }
}
```

- Keep the model backward-compatible (`null` = no attachments)
- The `StorageKey` is obtained from the upload endpoint (C2) or from Files module reference

**C2. Add upload endpoint for compose attachments** in `EmailController.cs`

```
POST /api/v1/email/upload-attachment
```

- Accept `multipart/form-data` with a single file field
- **Enforce 25 MB max file size** — return `413 Payload Too Large` with error body: `"Files over 25 MB can be shared via the Files module."` (include link/path to Files module)
- Store via `IAttachmentStorage.StoreAsync()`
- Return JSON: `{ storageKey, fileName, contentType, size, contentHash }`
- The uploaded file is stored as a "temp" attachment — no `EmailAttachment` DB record yet. The cleanup job (Phase E) removes orphans after 24h.

**C3. Enhance `EmailComposeForm` with attachment UI**

Add new section between the body area and the footer:

```
[Attach from computer] [Browse Files]
─────────────────────────────────────
📎 report.pdf (2.4 MB)  ✕
🖼️ screenshot.png (1.1 MB)  ✕
```

- **"Attach from computer" button** → triggers `InputFile` component → on file selected:
  - Client-side check: if file > 25 MB, show error toast and block upload
  - Upload file to `POST /api/v1/email/upload-attachment`
  - On success, add the returned `{ storageKey, fileName, contentType, size }` to compose state
  - Show as chip with filename, size, and remove button (×)
- **"Browse Files" button** → opens modal/inline file picker that calls Files module REST API (`GET /api/v1/files/nodes?parentId=...`) to browse the user's file tree:
  - User navigates folders and selects files
  - On selection, the email module gets the file's `StoragePath`/`ContentHash` from the Files API
  - Uses that to reference the file via storage key (or copies it to email storage)
  - Shows as chip same as browser upload
- Attachment data is stored in the `EmailSendRequest` that gets submitted on Send

**C4. Enhance `ImapSmtpEmailProvider.SendAsync` with MIME attachments**

- Before building the message, check `request.Attachments` for items
- If attachments present:
  - Build `multipart/mixed` MIME body
  - For each attachment, read content from `IAttachmentStorage.OpenReadAsync(storageKey)`
  - Use MimeKit's `BodyBuilder.Attachments.Add(fileName, stream, contentType)` to add each part
  - For inline images (`IsInline == true`), use `BodyBuilder.LinkedResources.Add()` with ContentId
  - Set `message.Body = builder.ToMessageBody()`
- If no attachments, keep existing single-part behavior

**C5. Enhance Gmail `SendAsync` / `BuildMimeMessage` with MIME attachments**

- **Switch `BuildMimeMessage` from manual string-based MIME to MimeKit** (critical for proper multipart support, charset handling, and consistency with IMAP provider)
- Build MIME message using same pattern as C4
- Encode final MIME as base64url for Gmail API: `Convert.ToBase64String(Encoding.UTF8.GetBytes(mimeMessage)).Replace(...).Replace(...).TrimEnd('=')`

**C6. Update `EmailSendService.SendAsync` to persist attachment records**

- After successful provider send, iterate `request.Attachments` and create `EmailAttachment` DB records:
  1. Create `EmailMessage` entity (already done)
  2. For each attachment reference, look up the stored content metadata
  3. Create `EmailAttachment` with: `MessageId`, `FileName`, `ContentType`, `Size`, `StorageKey`, `ContentHash`, `CreatedAt`
  4. Add to `db.EmailAttachments` and save
- If send fails, do NOT delete the uploaded attachments — they remain for retry and get cleaned up by the TTL job
- For Files module references: mark `IsFromFilesModule = true` (or keep a reference to the source) for audit purposes

---

### Phase D — Detach to Files Module

**D1. Add "Save to Files" action on attachment chips** in `EmailPage.razor`

- Each attachment chip in message view gets a secondary action: "Save to Files" button (📁 icon)
- Clicking triggers a folder picker flow:
  1. Call Files module REST API to list the user's folders (`GET /api/v1/files/nodes?parentId=root&type=folder`)
  2. Show a simple folder tree modal for the user to pick a destination
  3. On confirm, publish `EmailAttachmentDetachedEvent` with the selected folder

**D2. Publish `EmailAttachmentDetachedEvent`** in `EmailEvents.cs`

```csharp
public sealed record EmailAttachmentDetachedEvent : IEvent
{
    public Guid EventId { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid AttachmentId { get; init; }
    public string StorageKey { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Size { get; init; }
    public Guid OwnerId { get; init; }
    public Guid? TargetFolderId { get; init; }
}
```

- Register in `EmailModuleManifest.cs` published events
- Fire from the `EmailController` (detach endpoint) or from the email page's code-behind

**D3. Handle event in Files module** (Files module development)

- Subscribe to `EmailAttachmentDetachedEvent` in Files module
- On receipt:
  1. Read attachment content via `IAttachmentStorage.OpenReadAsync(storageKey)` (or direct file access)
  2. Create a `FileNode` / `FileVersion` in the user's files tree under the specified folder
  3. Copy the content to Files module storage (for consistency with Files module's own storage)
  4. The copied content now counts against the user's Files quota
- This is deferred — the event bus message is published regardless of whether a handler exists yet

---

### Phase E — UI Polish, Cleanup & Integration

**E1. Style all attachment UI elements** in `EmailPage.razor.css`

- Attachment download chip styling (hover, active states, cursor pointer)
- Compose attachment chip styling (with × remove button, file type color coding)
- Upload progress indicator (simple bar during upload)
- Drag-and-drop zone on compose form (future, optional)

**E2. Add attachment info to message view**

- Show file type icon based on MIME category:
  - `image/*` → 🖼️
  - `application/pdf` → 📄
  - `application/zip`, `application/x-rar-compressed` → 📦
  - `text/*` → 📝
  - Others → 📎 (default)
- Show file size formatted (B, KB, MB, GB)
- Preview thumbnail for images (fetch first few KB and render inline)

**E3. Temp cleanup background job**

- Add a `CleanupTempAttachmentsBackgroundService` (IHostedService) that runs periodically
- Scans `FileSystemAttachmentStorage` for files older than 24h with no associated `EmailAttachment` record
- Deletes orphaned files to reclaim space
- Logs cleanup statistics

**E4. Update search index** in `EmailSearchableModule`

- Include attachment filenames in the searchable content for each email message

---

## Relevant Files

| File | What to Change |
|------|---------------|
| `src/.../Email/Services/IEmailProvider.cs` | `EmailSendRequest` — add `Attachments` property + `EmailAttachmentRef` record |
| `src/.../Email/Services/IEmailSendService.cs` | No change needed (signature stays same) |
| `src/.../Email/Services/IEmailApiClient.cs` | Add download/upload/browse methods |
| `src/.../Email/Services/EmailApiClient.cs` | Implement download/upload HTTP calls, Files module browsing |
| `src/.../Email/Services/IAttachmentStorage.cs` | **NEW** — Storage interface + `AttachmentStorageResult` record |
| `src/.../Email.Data/Services/FileSystemAttachmentStorage.cs` | **NEW** — Content-addressable filesystem storage |
| `src/.../Email.Data/Services/CleanupTempAttachmentsService.cs` | **NEW** — Background job for TTL cleanup |
| `src/.../Email.Data/EmailServiceRegistration.cs` | Register `IAttachmentStorage`, cleanup service |
| `src/.../Email.Data/Services/ImapSmtpEmailProvider.cs` | Enhance sync (download content), enhance send (MIME attachments) |
| `src/.../Email.Data/Services/GmailEmailProvider.cs` | Enhance sync (download via API), enhance send (switch to MimeKit) |
| `src/.../Email.Data/Services/EmailSendService.cs` | Persist attachment records after send, 25 MB enforcement |
| `src/.../Email.Host/Controllers/EmailController.cs` | Add `GET .../attachments/{id}/download`, `POST .../upload-attachment` |
| `src/.../Email/Models/EmailAttachment.cs` | No change needed (model is complete) |
| `src/.../Email/UI/EmailComposeForm.razor` | Add attachment upload UI (browser upload + Files module picker) |
| `src/.../Email/UI/EmailPage.razor` | Make attachment chips clickable, add "Save to Files" |
| `src/.../Email/UI/EmailPage.razor.css` | Style all attachment elements |
| `src/.../Email/Events/EmailEvents.cs` | Add `EmailAttachmentDetachedEvent` |
| `src/.../Email/EmailModuleManifest.cs` | Add new published event |
| `src/.../Email.Data/Services/EmailSearchableModule.cs` | Include attachment filenames in search index |

---

## Verification Checklist

1. **Build**: `dotnet build DotNetCloud.CI.slnf` passes
2. **IMAP sync test**: Sync an inbox with attachments → `EmailAttachment` records created with StorageKey, ContentHash, Size populated → files exist on disk at correct SHA-256 paths
3. **Gmail sync test**: Same for Gmail account
4. **Attachment download**: Click attachment chip in UI → file downloads with correct name, type, and content
5. **Inline images**: Email with embedded images displays inline (not shown as attachment chips)
6. **Compose + browser upload**: Open compose, attach file from computer via `InputFile` → chip appears → send → recipient receives multipart MIME with attachment
7. **Compose + Files module**: Open compose, browse Files module, select file → chip appears → send → works
8. **25 MB limit**: Try uploading a file >25 MB → error message with Files module suggestion
9. **Detach to Files**: Click "Save to Files" on an inbound attachment → event published → file appears in Files module
10. **Temp cleanup**: Abandon a compose with uploaded attachments → after 24h, orphaned files are deleted
11. **Edge cases**: Duplicate filenames, special characters in filenames, empty files, very large sync batches, concurrent access

---

## Decisions

- **Max attachment size**: 25 MB per email (configurable). If user exceeds this, show a message: *"Files over 25 MB can be shared via the Files module"* with a link to the Files module.
- **Files module integration**: The compose form includes both (a) a "Browse Files" button that opens a modal file picker to select files already stored in the Files module, and (b) a standard "Attach from computer" button using `InputFile` for browser upload. Both paths produce the same storage reference.
- **Storage**: Use dedicated `FileSystemAttachmentStorage` (content-addressable, SHA-256 prefix directories) rather than `IStorageProvider` (which is a blank marker interface). This keeps email storage independent of the capability system.
- **No Files module dependency**: Email module will not reference Files module projects directly. Cross-module communication uses the event bus (`EmailAttachmentDetachedEvent`). For "Browse Files" in compose, the email module makes an API call to the Files module's REST API (not a direct project reference).
- **Gmail MIME**: Switch Gmail provider from manual string-based MIME to MimeKit for consistency, proper multipart support, and charset handling.
- **Temp attachment cleanup**: Uploaded compose attachments that are never sent are auto-deleted via a background cleanup job (24h TTL). The `FileSystemAttachmentStorage` tracks creation timestamps; the cleanup job deletes orphaned files without associated `EmailAttachment` records.
- **Send API remains JSON**: The send endpoint stays JSON (`POST .../send` with `EmailSendRequest`). Attachments are uploaded first via a separate endpoint, then referenced by `storageKey` in the send request.
- **Quota**: Email attachment storage is separate from Files module quotas. Only "Save to Files" (detach) copies content to Files module and counts against the user's Files quota.

---

## Further Considerations

1. **Inline image in compose**: Rich text editor (future) would enable embedding images inline within the message body. For now, inline images are received-only (displayed in HTML body from incoming emails).
2. **Cross-module file picker**: The "Browse Files" modal needs to call the Files module REST API (list files in a folder, search files). This is an API integration, not a project reference — the email module's host already has HTTP client capabilities.
3. **25 MB limit enforcement**: Enforced server-side in both the upload endpoint and the send endpoint. Client-side (Blazor) also warns before upload attempt. The error message should suggest Files module sharing as an alternative.
4. **Concurrent sync safety**: Both IMAP and Gmail providers could be downloading attachments simultaneously for the same account. The storage layer is append-only (writes are atomic by hash), so no locking needed.
5. **Migration of existing attachments**: Existing `EmailAttachment` records with empty `StorageKey` represent previously synced messages where attachment content was never saved. A one-time migration could re-download them, but this is optional — old attachments simply won't be downloadable until a re-sync occurs.
