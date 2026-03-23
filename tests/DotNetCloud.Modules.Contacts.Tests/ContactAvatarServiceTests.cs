using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="ContactAvatarService"/> — avatar upload/download/delete and attachment management.
/// </summary>
[TestClass]
public class ContactAvatarServiceTests
{
    private ContactsDbContext _db = null!;
    private ContactService _contactService = null!;
    private ContactAvatarService _avatarService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;
    private string _tempStoragePath = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _contactService = new ContactService(_db, _eventBusMock.Object, NullLogger<ContactService>.Instance);
        _tempStoragePath = Path.Combine(Path.GetTempPath(), "dnc-avatar-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempStoragePath);
        _avatarService = new ContactAvatarService(_db, NullLogger<ContactAvatarService>.Instance, _tempStoragePath);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        if (Directory.Exists(_tempStoragePath))
        {
            Directory.Delete(_tempStoragePath, recursive: true);
        }
    }

    private async Task<ContactDto> CreateTestContactAsync(string name = "Test Contact")
    {
        return await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = name },
            _caller);
    }

    private static Stream CreateFakeImageStream(int size = 1024)
    {
        var data = new byte[size];
        new Random(42).NextBytes(data);
        return new MemoryStream(data);
    }

    // ─── Avatar Upload ─────────────────────────────────────────────────

    [TestMethod]
    public async Task UploadAvatar_ValidImage_ReturnsAttachmentDto()
    {
        var contact = await CreateTestContactAsync();

        using var stream = CreateFakeImageStream();
        var result = await _avatarService.UploadAvatarAsync(
            contact.Id, stream, "photo.jpg", "image/jpeg", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(contact.Id, result.ContactId);
        Assert.AreEqual("photo.jpg", result.FileName);
        Assert.AreEqual("image/jpeg", result.ContentType);
        Assert.IsTrue(result.IsAvatar);
        Assert.AreEqual(1024, result.FileSizeBytes);
    }

    [TestMethod]
    public async Task UploadAvatar_UpdatesContactAvatarUrl()
    {
        var contact = await CreateTestContactAsync();

        using var stream = CreateFakeImageStream();
        await _avatarService.UploadAvatarAsync(contact.Id, stream, "photo.png", "image/png", _caller);

        var updated = await _contactService.GetContactAsync(contact.Id, _caller);
        Assert.IsNotNull(updated);
        Assert.AreEqual($"/api/v1/contacts/{contact.Id}/avatar", updated.AvatarUrl);
    }

    [TestMethod]
    public async Task UploadAvatar_ReplacesExistingAvatar()
    {
        var contact = await CreateTestContactAsync();

        using var stream1 = CreateFakeImageStream(512);
        await _avatarService.UploadAvatarAsync(contact.Id, stream1, "old.jpg", "image/jpeg", _caller);

        using var stream2 = CreateFakeImageStream(2048);
        var result = await _avatarService.UploadAvatarAsync(contact.Id, stream2, "new.png", "image/png", _caller);

        Assert.AreEqual("new.png", result.FileName);
        Assert.AreEqual(2048, result.FileSizeBytes);

        var attachments = await _avatarService.ListAttachmentsAsync(contact.Id, _caller);
        Assert.AreEqual(1, attachments.Count(a => a.IsAvatar));
    }

    [TestMethod]
    public async Task UploadAvatar_InvalidContentType_ThrowsValidation()
    {
        var contact = await CreateTestContactAsync();

        using var stream = CreateFakeImageStream();
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _avatarService.UploadAvatarAsync(contact.Id, stream, "doc.pdf", "application/pdf", _caller));
    }

    [TestMethod]
    public async Task UploadAvatar_NonexistentContact_ThrowsValidation()
    {
        using var stream = CreateFakeImageStream();
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _avatarService.UploadAvatarAsync(Guid.NewGuid(), stream, "photo.jpg", "image/jpeg", _caller));
    }

    [TestMethod]
    public async Task UploadAvatar_OtherUsersContact_ThrowsValidation()
    {
        var contact = await CreateTestContactAsync();

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        using var stream = CreateFakeImageStream();
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _avatarService.UploadAvatarAsync(contact.Id, stream, "photo.jpg", "image/jpeg", otherCaller));
    }

    [TestMethod]
    public async Task UploadAvatar_SvgAccepted()
    {
        var contact = await CreateTestContactAsync();
        var svgData = "<svg xmlns='http://www.w3.org/2000/svg'><circle r='10'/></svg>"u8.ToArray();
        using var stream = new MemoryStream(svgData);

        var result = await _avatarService.UploadAvatarAsync(contact.Id, stream, "avatar.svg", "image/svg+xml", _caller);

        Assert.AreEqual("image/svg+xml", result.ContentType);
    }

    // ─── Avatar Get ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetAvatar_ExistingAvatar_ReturnsStreamAndContentType()
    {
        var contact = await CreateTestContactAsync();
        using var uploadStream = CreateFakeImageStream();
        await _avatarService.UploadAvatarAsync(contact.Id, uploadStream, "photo.jpg", "image/jpeg", _caller);

        var result = await _avatarService.GetAvatarAsync(contact.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("image/jpeg", result.Value.ContentType);
        Assert.AreEqual("photo.jpg", result.Value.FileName);

        using var ms = new MemoryStream();
        await result.Value.Stream.CopyToAsync(ms);
        Assert.AreEqual(1024, ms.Length);
        await result.Value.Stream.DisposeAsync();
    }

    [TestMethod]
    public async Task GetAvatar_NoAvatar_ReturnsNull()
    {
        var contact = await CreateTestContactAsync();

        var result = await _avatarService.GetAvatarAsync(contact.Id, _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAvatarBytes_ExistingAvatar_ReturnsBytesAndContentType()
    {
        var contact = await CreateTestContactAsync();
        using var uploadStream = CreateFakeImageStream();
        await _avatarService.UploadAvatarAsync(contact.Id, uploadStream, "photo.png", "image/png", _caller);

        var result = await _avatarService.GetAvatarBytesAsync(contact.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("image/png", result.Value.ContentType);
        Assert.AreEqual(1024, result.Value.Data.Length);
    }

    // ─── Avatar Delete ─────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAvatar_ExistingAvatar_RemovesAvatarAndClearsUrl()
    {
        var contact = await CreateTestContactAsync();
        using var stream = CreateFakeImageStream();
        await _avatarService.UploadAvatarAsync(contact.Id, stream, "photo.jpg", "image/jpeg", _caller);

        await _avatarService.DeleteAvatarAsync(contact.Id, _caller);

        var updated = await _contactService.GetContactAsync(contact.Id, _caller);
        Assert.IsNull(updated?.AvatarUrl);

        var avatar = await _avatarService.GetAvatarAsync(contact.Id, _caller);
        Assert.IsNull(avatar);
    }

    [TestMethod]
    public async Task DeleteAvatar_NoAvatar_Succeeds()
    {
        var contact = await CreateTestContactAsync();

        // Should not throw even if no avatar exists
        await _avatarService.DeleteAvatarAsync(contact.Id, _caller);
    }

    // ─── Avatar From Bytes (vCard import) ──────────────────────────────

    [TestMethod]
    public async Task SaveAvatarFromBytes_StoresAndSetsUrl()
    {
        var contact = await CreateTestContactAsync();
        var data = new byte[512];
        new Random(1).NextBytes(data);

        await _avatarService.SaveAvatarFromBytesAsync(contact.Id, data, "image/png", _caller);

        var result = await _avatarService.GetAvatarBytesAsync(contact.Id, _caller);
        Assert.IsNotNull(result);
        Assert.AreEqual("image/png", result.Value.ContentType);
        Assert.AreEqual(512, result.Value.Data.Length);

        var updated = await _contactService.GetContactAsync(contact.Id, _caller);
        Assert.IsNotNull(updated?.AvatarUrl);
    }

    // ─── General Attachments ───────────────────────────────────────────

    [TestMethod]
    public async Task AddAttachment_ValidFile_ReturnsMetadata()
    {
        var contact = await CreateTestContactAsync();

        using var stream = CreateFakeImageStream(2048);
        var result = await _avatarService.AddAttachmentAsync(
            contact.Id, stream, "document.pdf", "application/pdf", "Important doc", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(contact.Id, result.ContactId);
        Assert.AreEqual("document.pdf", result.FileName);
        Assert.AreEqual("application/pdf", result.ContentType);
        Assert.AreEqual(2048, result.FileSizeBytes);
        Assert.IsFalse(result.IsAvatar);
        Assert.AreEqual("Important doc", result.Description);
    }

    [TestMethod]
    public async Task ListAttachments_ReturnsAllAttachments()
    {
        var contact = await CreateTestContactAsync();

        using var stream1 = CreateFakeImageStream(100);
        await _avatarService.AddAttachmentAsync(contact.Id, stream1, "file1.txt", "text/plain", null, _caller);

        using var stream2 = CreateFakeImageStream(200);
        await _avatarService.AddAttachmentAsync(contact.Id, stream2, "file2.pdf", "application/pdf", "Desc", _caller);

        using var avatarStream = CreateFakeImageStream(300);
        await _avatarService.UploadAvatarAsync(contact.Id, avatarStream, "avatar.jpg", "image/jpeg", _caller);

        var attachments = await _avatarService.ListAttachmentsAsync(contact.Id, _caller);

        Assert.AreEqual(3, attachments.Count);
        Assert.AreEqual(1, attachments.Count(a => a.IsAvatar));
        Assert.AreEqual(2, attachments.Count(a => !a.IsAvatar));
    }

    [TestMethod]
    public async Task GetAttachment_ValidId_ReturnsStream()
    {
        var contact = await CreateTestContactAsync();

        using var stream = CreateFakeImageStream(512);
        var attachment = await _avatarService.AddAttachmentAsync(
            contact.Id, stream, "data.bin", "application/octet-stream", null, _caller);

        var result = await _avatarService.GetAttachmentAsync(attachment.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("application/octet-stream", result.Value.ContentType);

        using var ms = new MemoryStream();
        await result.Value.Stream.CopyToAsync(ms);
        Assert.AreEqual(512, ms.Length);
        await result.Value.Stream.DisposeAsync();
    }

    [TestMethod]
    public async Task GetAttachment_NonexistentId_ReturnsNull()
    {
        var result = await _avatarService.GetAttachmentAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAttachment_RemovesFromListAndDisk()
    {
        var contact = await CreateTestContactAsync();

        using var stream = CreateFakeImageStream(256);
        var attachment = await _avatarService.AddAttachmentAsync(
            contact.Id, stream, "temp.txt", "text/plain", null, _caller);

        await _avatarService.DeleteAttachmentAsync(attachment.Id, _caller);

        var attachments = await _avatarService.ListAttachmentsAsync(contact.Id, _caller);
        Assert.AreEqual(0, attachments.Count);

        var result = await _avatarService.GetAttachmentAsync(attachment.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAttachment_NonexistentId_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _avatarService.DeleteAttachmentAsync(Guid.NewGuid(), _caller));
    }

    [TestMethod]
    public async Task DeleteAttachment_OtherUsersAttachment_Throws()
    {
        var contact = await CreateTestContactAsync();
        using var stream = CreateFakeImageStream();
        var attachment = await _avatarService.AddAttachmentAsync(
            contact.Id, stream, "file.txt", "text/plain", null, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _avatarService.DeleteAttachmentAsync(attachment.Id, otherCaller));
    }

    // ─── Contact DTO includes attachments ──────────────────────────────

    [TestMethod]
    public async Task ContactDto_IncludesAttachments()
    {
        var contact = await CreateTestContactAsync();

        using var avatarStream = CreateFakeImageStream();
        await _avatarService.UploadAvatarAsync(contact.Id, avatarStream, "avatar.jpg", "image/jpeg", _caller);

        using var attachStream = CreateFakeImageStream(100);
        await _avatarService.AddAttachmentAsync(contact.Id, attachStream, "doc.pdf", "application/pdf", null, _caller);

        var dto = await _contactService.GetContactAsync(contact.Id, _caller);

        Assert.IsNotNull(dto);
        Assert.AreEqual(2, dto.Attachments.Count);
        Assert.IsTrue(dto.Attachments.Any(a => a.IsAvatar && a.FileName == "avatar.jpg"));
        Assert.IsTrue(dto.Attachments.Any(a => !a.IsAvatar && a.FileName == "doc.pdf"));
    }

    // ─── File name sanitization ────────────────────────────────────────

    [TestMethod]
    public async Task UploadAvatar_PathTraversalFileName_Sanitized()
    {
        var contact = await CreateTestContactAsync();
        using var stream = CreateFakeImageStream();

        var result = await _avatarService.UploadAvatarAsync(
            contact.Id, stream, "../../../etc/passwd.jpg", "image/jpeg", _caller);

        Assert.IsFalse(result.FileName.Contains(".."));
        Assert.AreEqual("passwd.jpg", result.FileName);
    }
}
