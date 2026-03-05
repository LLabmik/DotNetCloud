using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Tests.DTOs;

/// <summary>
/// Tests for Files module DTOs verifying record semantics, defaults, and immutability.
/// </summary>
[TestClass]
public class FileDtoTests
{
    // ---- FileNodeDto ----

    [TestMethod]
    public void WhenFileNodeDtoCreatedThenRequiredPropertiesAreSet()
    {
        var id = Guid.NewGuid();
        var dto = new FileNodeDto
        {
            Id = id,
            Name = "report.pdf",
            NodeType = "File"
        };

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("report.pdf", dto.Name);
        Assert.AreEqual("File", dto.NodeType);
    }

    [TestMethod]
    public void WhenFileNodeDtoCreatedThenDefaultsAreCorrect()
    {
        var dto = new FileNodeDto { Id = Guid.NewGuid(), Name = "test", NodeType = "File" };

        Assert.AreEqual(0, dto.Size);
        Assert.AreEqual(0, dto.CurrentVersion);
        Assert.IsFalse(dto.IsFavorite);
        Assert.AreEqual(0, dto.ChildCount);
        Assert.IsNull(dto.MimeType);
        Assert.IsNull(dto.ParentId);
        Assert.IsNull(dto.ContentHash);
        Assert.AreEqual(0, dto.Tags.Count);
    }

    [TestMethod]
    public void WhenFileNodeDtoCreatedWithTagsThenStoresValues()
    {
        var dto = new FileNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "test",
            NodeType = "File",
            Tags = [
                new FileTagDto { Id = Guid.NewGuid(), Name = "Important" },
                new FileTagDto { Id = Guid.NewGuid(), Name = "Work" }
            ]
        };

        Assert.AreEqual(2, dto.Tags.Count);
        Assert.AreEqual("Important", dto.Tags[0].Name);
    }

    [TestMethod]
    public void WhenTwoFileNodeDtosWithSameValuesComparedThenAreEqual()
    {
        var id = Guid.NewGuid();
        var dto1 = new FileNodeDto { Id = id, Name = "test", NodeType = "File" };
        var dto2 = new FileNodeDto { Id = id, Name = "test", NodeType = "File" };

        Assert.AreEqual(dto1, dto2);
    }

    // ---- CreateFolderDto ----

    [TestMethod]
    public void WhenCreateFolderDtoCreatedThenRequiredPropertyIsSet()
    {
        var dto = new CreateFolderDto { Name = "Documents" };

        Assert.AreEqual("Documents", dto.Name);
        Assert.IsNull(dto.ParentId);
    }

    [TestMethod]
    public void WhenCreateFolderDtoCreatedWithParentThenStoresValue()
    {
        var parentId = Guid.NewGuid();
        var dto = new CreateFolderDto { Name = "Sub", ParentId = parentId };

        Assert.AreEqual(parentId, dto.ParentId);
    }

    // ---- RenameNodeDto ----

    [TestMethod]
    public void WhenRenameNodeDtoCreatedThenStoresName()
    {
        var dto = new RenameNodeDto { Name = "new-name.txt" };

        Assert.AreEqual("new-name.txt", dto.Name);
    }

    // ---- MoveNodeDto ----

    [TestMethod]
    public void WhenMoveNodeDtoCreatedThenStoresTargetParent()
    {
        var targetId = Guid.NewGuid();
        var dto = new MoveNodeDto { TargetParentId = targetId };

        Assert.AreEqual(targetId, dto.TargetParentId);
    }

    // ---- InitiateUploadDto ----

    [TestMethod]
    public void WhenInitiateUploadDtoCreatedThenStoresValues()
    {
        var hashes = new List<string> { "hash1", "hash2", "hash3" };
        var dto = new InitiateUploadDto
        {
            FileName = "large.zip",
            TotalSize = 10485760,
            MimeType = "application/zip",
            ChunkHashes = hashes
        };

        Assert.AreEqual("large.zip", dto.FileName);
        Assert.AreEqual(10485760, dto.TotalSize);
        Assert.AreEqual("application/zip", dto.MimeType);
        Assert.AreEqual(3, dto.ChunkHashes.Count);
    }

    [TestMethod]
    public void WhenInitiateUploadDtoCreatedThenParentIdIsNull()
    {
        var dto = new InitiateUploadDto
        {
            FileName = "test",
            ChunkHashes = ["hash1"]
        };

        Assert.IsNull(dto.ParentId);
    }

    // ---- UploadSessionDto ----

    [TestMethod]
    public void WhenUploadSessionDtoCreatedThenDefaultListsAreEmpty()
    {
        var dto = new UploadSessionDto { SessionId = Guid.NewGuid() };

        Assert.AreEqual(0, dto.ExistingChunks.Count);
        Assert.AreEqual(0, dto.MissingChunks.Count);
    }

    [TestMethod]
    public void WhenUploadSessionDtoCreatedWithChunksThenStoresValues()
    {
        var dto = new UploadSessionDto
        {
            SessionId = Guid.NewGuid(),
            ExistingChunks = ["a", "b"],
            MissingChunks = ["c"]
        };

        Assert.AreEqual(2, dto.ExistingChunks.Count);
        Assert.AreEqual(1, dto.MissingChunks.Count);
    }

    // ---- FileVersionDto ----

    [TestMethod]
    public void WhenFileVersionDtoCreatedThenStoresValues()
    {
        var id = Guid.NewGuid();
        var dto = new FileVersionDto
        {
            Id = id,
            VersionNumber = 3,
            Size = 1024,
            ContentHash = "abc123",
            MimeType = "text/plain",
            Label = "Draft"
        };

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual(3, dto.VersionNumber);
        Assert.AreEqual(1024, dto.Size);
        Assert.AreEqual("abc123", dto.ContentHash);
        Assert.AreEqual("text/plain", dto.MimeType);
        Assert.AreEqual("Draft", dto.Label);
    }

    // ---- FileShareDto ----

    [TestMethod]
    public void WhenFileShareDtoCreatedThenStoresValues()
    {
        var id = Guid.NewGuid();
        var dto = new FileShareDto
        {
            Id = id,
            ShareType = "PublicLink",
            Permission = "Read",
            LinkToken = "token123",
            MaxDownloads = 50,
            DownloadCount = 10
        };

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("PublicLink", dto.ShareType);
        Assert.AreEqual("Read", dto.Permission);
        Assert.AreEqual("token123", dto.LinkToken);
        Assert.AreEqual(50, dto.MaxDownloads);
        Assert.AreEqual(10, dto.DownloadCount);
    }

    // ---- CreateShareDto ----

    [TestMethod]
    public void WhenCreateShareDtoCreatedThenDefaultPermissionIsRead()
    {
        var dto = new CreateShareDto { ShareType = "User" };

        Assert.AreEqual("Read", dto.Permission);
    }

    [TestMethod]
    public void WhenCreateShareDtoCreatedThenOptionalFieldsAreNull()
    {
        var dto = new CreateShareDto { ShareType = "User" };

        Assert.IsNull(dto.SharedWithUserId);
        Assert.IsNull(dto.SharedWithTeamId);
        Assert.IsNull(dto.SharedWithGroupId);
        Assert.IsNull(dto.LinkPassword);
        Assert.IsNull(dto.MaxDownloads);
        Assert.IsNull(dto.ExpiresAt);
        Assert.IsNull(dto.Note);
    }

    // ---- QuotaDto ----

    [TestMethod]
    public void WhenQuotaDtoCreatedThenStoresValues()
    {
        var dto = new QuotaDto
        {
            UserId = Guid.NewGuid(),
            MaxBytes = 10737418240,
            UsedBytes = 5368709120,
            RemainingBytes = 5368709120,
            UsagePercent = 50.0
        };

        Assert.AreEqual(10737418240, dto.MaxBytes);
        Assert.AreEqual(50.0, dto.UsagePercent, 0.01);
    }

    // ---- TrashItemDto ----

    [TestMethod]
    public void WhenTrashItemDtoCreatedThenStoresValues()
    {
        var id = Guid.NewGuid();
        var deletedAt = DateTime.UtcNow;
        var dto = new TrashItemDto
        {
            Id = id,
            Name = "old-file.txt",
            NodeType = "File",
            Size = 512,
            MimeType = "text/plain",
            DeletedAt = deletedAt,
            OriginalPath = "/Documents"
        };

        Assert.AreEqual(id, dto.Id);
        Assert.AreEqual("old-file.txt", dto.Name);
        Assert.AreEqual("File", dto.NodeType);
        Assert.AreEqual(512, dto.Size);
        Assert.AreEqual(deletedAt, dto.DeletedAt);
        Assert.AreEqual("/Documents", dto.OriginalPath);
    }

    [TestMethod]
    public void WhenTrashItemDtoCreatedThenOptionalFieldsAreNull()
    {
        var dto = new TrashItemDto { Id = Guid.NewGuid(), Name = "test", NodeType = "File" };

        Assert.IsNull(dto.MimeType);
        Assert.IsNull(dto.DeletedAt);
        Assert.IsNull(dto.DeletedByUserId);
        Assert.IsNull(dto.OriginalPath);
    }
}
