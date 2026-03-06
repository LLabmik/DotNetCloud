using System.Security.Claims;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Host.Protos;
using DotNetCloud.Modules.Files.Host.Services;
using DotNetCloud.Modules.Files.Models;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Host;

[TestClass]
public class FilesGrpcServiceSecurityTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static FilesGrpcService CreateService(FilesDbContext db)
    {
        return new FilesGrpcService(db, NullLogger<FilesGrpcService>.Instance);
    }

    [TestMethod]
    public async Task GetNode_NonOwnerCaller_ReturnsNotFound()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var node = new FileNode
        {
            Name = "secret.txt",
            NodeType = FileNodeType.File,
            OwnerId = ownerId
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var response = await service.GetNode(new GetNodeRequest
        {
            NodeId = node.Id.ToString(),
            UserId = callerId.ToString()
        }, CreateContextWithAuthenticatedUser(callerId));

        Assert.IsFalse(response.Found);
    }

    [TestMethod]
    public async Task GetNode_UserIdSpoofingAttempt_ReturnsNotFound()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var node = new FileNode
        {
            Name = "private.docx",
            NodeType = FileNodeType.File,
            OwnerId = ownerId
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var response = await service.GetNode(new GetNodeRequest
        {
            NodeId = node.Id.ToString(),
            UserId = ownerId.ToString()
        }, CreateContextWithAuthenticatedUser(attackerId));

        Assert.IsFalse(response.Found);
    }

    [TestMethod]
    public async Task RenameNode_NonOwnerCaller_FailsAndKeepsOriginalName()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var node = new FileNode
        {
            Name = "original.txt",
            NodeType = FileNodeType.File,
            OwnerId = ownerId
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RenameNode(new RenameNodeRequest
        {
            NodeId = node.Id.ToString(),
            NewName = "hijacked.txt",
            UserId = callerId.ToString()
        }, CreateContextWithAuthenticatedUser(callerId));

        Assert.IsFalse(result.Success);

        var persisted = await db.FileNodes.AsNoTracking().FirstAsync(n => n.Id == node.Id);
        Assert.AreEqual("original.txt", persisted.Name);
    }

    [TestMethod]
    public async Task ListShares_NonOwnerCaller_ReturnsEmpty()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var node = new FileNode
        {
            Name = "shared.pdf",
            NodeType = FileNodeType.File,
            OwnerId = ownerId
        };

        db.FileNodes.Add(node);
        db.FileShares.Add(new DotNetCloud.Modules.Files.Models.FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            Permission = SharePermission.Read,
            LinkToken = "token-123",
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var response = await service.ListShares(new ListSharesRequest
        {
            NodeId = node.Id.ToString(),
            UserId = callerId.ToString()
        }, CreateContextWithAuthenticatedUser(callerId));

        Assert.AreEqual(0, response.Shares.Count);
    }

    [TestMethod]
    public async Task UploadChunk_SessionDoesNotExist_Fails()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var service = CreateService(db);
        var response = await service.UploadChunk(new UploadChunkRequest
        {
            SessionId = Guid.NewGuid().ToString(),
            ChunkHash = "deadbeef",
            ChunkData = Google.Protobuf.ByteString.CopyFrom(new byte[] { 1, 2, 3 })
        }, CreateContextWithAuthenticatedUser(userId));

        Assert.IsFalse(response.Success);
        StringAssert.Contains(response.ErrorMessage, "session", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task UploadChunk_SessionOwnerMismatch_Fails()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        db.UploadSessions.Add(new ChunkedUploadSession
        {
            FileName = "secret.bin",
            TotalSize = 3,
            TotalChunks = 1,
            ReceivedChunks = 0,
            ChunkManifest = "[\"abcd\"]",
            UserId = ownerId,
            Status = UploadSessionStatus.InProgress,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await db.SaveChangesAsync();

        var session = await db.UploadSessions.AsNoTracking().FirstAsync();

        var service = CreateService(db);
        var response = await service.UploadChunk(new UploadChunkRequest
        {
            SessionId = session.Id.ToString(),
            ChunkHash = "abcd",
            ChunkData = Google.Protobuf.ByteString.CopyFrom(new byte[] { 1, 2, 3 })
        }, CreateContextWithAuthenticatedUser(attackerId));

        Assert.IsFalse(response.Success);
        StringAssert.Contains(response.ErrorMessage, "identity", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task UploadChunk_HashMismatch_Fails()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        db.UploadSessions.Add(new ChunkedUploadSession
        {
            FileName = "file.bin",
            TotalSize = 3,
            TotalChunks = 1,
            ReceivedChunks = 0,
            ChunkManifest = "[\"abcd\"]",
            UserId = userId,
            Status = UploadSessionStatus.InProgress,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await db.SaveChangesAsync();

        var session = await db.UploadSessions.AsNoTracking().FirstAsync();

        var service = CreateService(db);
        var response = await service.UploadChunk(new UploadChunkRequest
        {
            SessionId = session.Id.ToString(),
            ChunkHash = "abcd",
            ChunkData = Google.Protobuf.ByteString.CopyFrom(new byte[] { 4, 5, 6 })
        }, CreateContextWithAuthenticatedUser(userId));

        Assert.IsFalse(response.Success);
        StringAssert.Contains(response.ErrorMessage, "hash", StringComparison.OrdinalIgnoreCase);
    }

    private static ServerCallContext CreateContextWithAuthenticatedUser(Guid userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ],
        authenticationType: "TestAuth");

        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        return new FakeServerCallContext(httpContext);
    }

    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders = new();
        private readonly Metadata _responseTrailers = new();
        private readonly Dictionary<object, object> _userState;
        private WriteOptions? _writeOptions;
        private Status _status;

        public FakeServerCallContext(HttpContext httpContext)
        {
            _userState = new Dictionary<object, object>
            {
                ["__HttpContext"] = httpContext,
                ["HttpContext"] = httpContext
            };
        }

        protected override string MethodCore => "/dotnetcloud.files.FilesService/Test";

        protected override string HostCore => "localhost";

        protected override string PeerCore => "ipv4:127.0.0.1:5000";

        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);

        protected override Metadata RequestHeadersCore => _requestHeaders;

        protected override CancellationToken CancellationTokenCore => CancellationToken.None;

        protected override Metadata ResponseTrailersCore => _responseTrailers;

        protected override Status StatusCore
        {
            get => _status;
            set => _status = value;
        }

        protected override WriteOptions? WriteOptionsCore
        {
            get => _writeOptions;
            set => _writeOptions = value;
        }

        protected override AuthContext AuthContextCore =>
            new("insecure", new Dictionary<string, List<AuthProperty>>());

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            foreach (var header in responseHeaders)
            {
                _responseTrailers.Add(header);
            }

            return Task.CompletedTask;
        }

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        {
            throw new NotSupportedException();
        }

        protected override IDictionary<object, object> UserStateCore => _userState;
    }
}
