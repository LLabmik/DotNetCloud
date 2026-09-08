using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IAuditLogger = DotNetCloud.Core.Capabilities.IAuditLogger;
using ITeamDirectory = DotNetCloud.Core.Capabilities.ITeamDirectory;
using TeamInfo = DotNetCloud.Core.Capabilities.TeamInfo;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Tests for team-targeted photo/album shares: create/list/revoke via the share service,
/// event publication, and team-membership read access through <see cref="PhotoService"/> and
/// <see cref="AlbumService"/>.
/// </summary>
[TestClass]
public class PhotosTeamShareTests
{
    private PhotosDbContext _db = default!;
    private PhotoShareService _shareService = default!;
    private Mock<IEventBus> _eventBusMock = default!;
    private CallerContext _owner = default!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _shareService = new PhotoShareService(_db, _eventBusMock.Object, NullLogger<PhotoShareService>.Instance);
        _owner = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="ITeamDirectory"/> that maps a single member to a team and returns
    /// no memberships for everyone else.
    /// </summary>
    private static Mock<ITeamDirectory> CreateTeamDirectory(CallerContext member, Guid teamId)
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        // Default for everyone else: no memberships. Must be registered before the
        // member-specific setup so the more specific setup (registered last) wins.
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamInfo>());
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(member.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TeamInfo
                {
                    Id = teamId,
                    OrganizationId = Guid.CreateVersion7(),
                    Name = "Eng",
                    MemberCount = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            });
        return teamDirectory;
    }

    private PhotoService CreatePhotoService(ITeamDirectory? teamDirectory = null)
        => new(_db, new Mock<IEventBus>().Object, Mock.Of<IAuditLogger>(), NullLogger<PhotoService>.Instance, teamDirectory);

    private AlbumService CreateAlbumService(ITeamDirectory? teamDirectory = null)
        => new(_db, new Mock<IEventBus>().Object, NullLogger<AlbumService>.Instance, teamDirectory);

    private PhotoShareService CreateShareService(ITeamDirectory? teamDirectory = null)
        => new(_db, new Mock<IEventBus>().Object, NullLogger<PhotoShareService>.Instance, teamDirectory);

    // ─── Share photo to a team ───────────────────────────────────────

    [TestMethod]
    public async Task SharePhoto_TeamTarget_CreatesTeamShare()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var teamId = Guid.CreateVersion7();

        var share = await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.AreEqual(photo.Id, share.PhotoId);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.IsNull(share.SharedWithUserId);
        Assert.AreEqual(PhotoSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task SharePhoto_TeamDedupe_UpdatesPermission()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var teamId = Guid.CreateVersion7();

        await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);
        var updated = await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.Download, _owner);

        Assert.AreEqual(teamId, updated.SharedWithTeamId);
        Assert.AreEqual(PhotoSharePermission.Download, updated.Permission);
        Assert.AreEqual(1, _db.PhotoShares.Count(s => s.PhotoId == photo.Id));
    }

    [TestMethod]
    public async Task SharePhoto_NoTarget_Throws()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.SharePhotoAsync(photo.Id, null, null, PhotoSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task SharePhoto_BothTargets_Throws()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.SharePhotoAsync(photo.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), PhotoSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task SharePhoto_UserTarget_StillWorks()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var targetUserId = Guid.CreateVersion7();

        var share = await _shareService.SharePhotoAsync(photo.Id, targetUserId, PhotoSharePermission.ReadOnly, _owner);

        Assert.AreEqual(targetUserId, share.SharedWithUserId);
        Assert.IsNull(share.SharedWithTeamId);
    }

    [TestMethod]
    public async Task RemoveShare_TeamShare_OwnerCanRevoke()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var teamId = Guid.CreateVersion7();
        var share = await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        Assert.AreEqual(0, _db.PhotoShares.Count(s => s.PhotoId == photo.Id));
    }

    // ─── Share album to a team ───────────────────────────────────────

    [TestMethod]
    public async Task ShareAlbum_TeamTarget_CreatesTeamShareAndPublishesEvent()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);
        var teamId = Guid.CreateVersion7();

        var share = await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.Download, _owner);

        Assert.IsNotNull(share);
        Assert.AreEqual(album.Id, share.AlbumId);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.IsNull(share.SharedWithUserId);
        Assert.AreEqual(PhotoSharePermission.Download, share.Permission);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<AlbumSharedEvent>(e => e.AlbumId == album.Id && e.SharedWithTeamId == teamId && e.SharedWithUserId == null),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ShareAlbum_TeamDedupe_UpdatesPermission()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);
        var teamId = Guid.CreateVersion7();

        await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);
        var updated = await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.Contribute, _owner);

        Assert.AreEqual(teamId, updated.SharedWithTeamId);
        Assert.AreEqual(PhotoSharePermission.Contribute, updated.Permission);
        Assert.AreEqual(1, _db.PhotoShares.Count(s => s.AlbumId == album.Id));
    }

    [TestMethod]
    public async Task ShareAlbum_NoTarget_Throws()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareAlbumAsync(album.Id, null, null, PhotoSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task ShareAlbum_BothTargets_Throws()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareAlbumAsync(album.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), PhotoSharePermission.ReadOnly, _owner));
    }

    // ─── Team-member read access: photos ─────────────────────────────

    [TestMethod]
    public async Task GetPhotoAsync_TeamMember_CanReadTeamSharedPhoto()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreatePhotoService(CreateTeamDirectory(member, teamId).Object);
        var result = await service.GetPhotoAsync(photo.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(photo.Id, result.Id);
    }

    [TestMethod]
    public async Task GetPhotoAsync_NonMember_CannotReadTeamSharedPhoto()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var stranger = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        // Stranger is not a member of the team the photo is shared with.
        var service = CreatePhotoService(CreateTeamDirectory(member, teamId).Object);
        var result = await service.GetPhotoAsync(photo.Id, stranger);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPhotoAsync_Owner_StillReadsPhoto()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);

        var service = CreatePhotoService();
        var result = await service.GetPhotoAsync(photo.Id, _owner);

        Assert.IsNotNull(result);
        Assert.AreEqual(photo.Id, result.Id);
    }

    // ─── Team-member read access: albums ─────────────────────────────

    [TestMethod]
    public async Task GetAlbumAsync_TeamMember_CanReadTeamSharedAlbum()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreateAlbumService(CreateTeamDirectory(member, teamId).Object);
        var result = await service.GetAlbumAsync(album.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(album.Id, result.Id);
    }

    [TestMethod]
    public async Task GetAlbumAsync_NonMember_CannotReadTeamSharedAlbum()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var stranger = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreateAlbumService(CreateTeamDirectory(member, teamId).Object);
        var result = await service.GetAlbumAsync(album.Id, stranger);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlbumPhotosAsync_TeamMember_CanReadPhotosInTeamSharedAlbum()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        _db.AlbumPhotos.Add(new AlbumPhoto { AlbumId = album.Id, PhotoId = photo.Id, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var member = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreateAlbumService(CreateTeamDirectory(member, teamId).Object);
        var photos = await service.GetAlbumPhotosAsync(album.Id, member);

        Assert.AreEqual(1, photos.Count);
        Assert.AreEqual(photo.Id, photos[0].Id);
    }

    // ─── GetSharedWithMe: team membership ────────────────────────────

    [TestMethod]
    public async Task GetSharedWithMe_TeamMember_IncludesTeamPhotoShare()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreateShareService(CreateTeamDirectory(member, teamId).Object);
        var shares = await service.GetSharedWithMeAsync(member);

        Assert.AreEqual(1, shares.Count);
        Assert.AreEqual(teamId, shares[0].SharedWithTeamId);
        Assert.AreEqual(photo.Id, shares[0].PhotoId);
    }

    [TestMethod]
    public async Task GetSharedWithMe_NonMember_ExcludesTeamShare()
    {
        var photo = await TestHelpers.SeedPhotoAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var stranger = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.SharePhotoAsync(photo.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreateShareService(CreateTeamDirectory(member, teamId).Object);
        var shares = await service.GetSharedWithMeAsync(stranger);

        Assert.AreEqual(0, shares.Count);
    }

    [TestMethod]
    public async Task GetSharedWithMe_TeamMember_IncludesTeamAlbumShare()
    {
        var album = await TestHelpers.SeedAlbumAsync(_db, _owner.UserId);
        var member = TestHelpers.CreateCaller();
        var teamId = Guid.CreateVersion7();
        await _shareService.ShareAlbumAsync(album.Id, null, teamId, PhotoSharePermission.ReadOnly, _owner);

        var service = CreateShareService(CreateTeamDirectory(member, teamId).Object);
        var shares = await service.GetSharedWithMeAsync(member);

        Assert.AreEqual(1, shares.Count);
        Assert.AreEqual(teamId, shares[0].SharedWithTeamId);
        Assert.AreEqual(album.Id, shares[0].AlbumId);
    }
}
