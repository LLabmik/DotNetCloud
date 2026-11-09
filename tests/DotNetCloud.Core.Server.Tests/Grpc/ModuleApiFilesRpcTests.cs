using Api = DotNetCloud.Core.Grpc.ModuleApi.V1;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Tests.Consent;
using DotNetCloud.Core.Server.Tests.Services;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for the file RPCs on the canonical module API surface: the acting user must come from the
/// verified assertion, the consent scope must be held, and a streamed upload must be shaped
/// correctly before anything reaches the Files module.
/// </summary>
[TestClass]
public class ModuleApiFilesRpcTests
{
    private const string ModuleId = "dotnetcloud.email";

    private static readonly Guid UserId = Guid.CreateVersion7();

    [TestMethod]
    public async Task ListFiles_ModuleScopedCall_ThrowsUnauthenticated()
    {
        var harness = new Harness();

        var context = new TestServerCallContext("/dotnetcloud.moduleapi.v1.ModuleApi/ListFiles", []);
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = CallerContext.CreateSystemContext();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => harness.Service.ListFiles(new Api.ListFilesRequest(), context));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
        harness.Files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ListFiles_WithConsent_MapsNodesOntoTheWireShape()
    {
        var nodeId = Guid.CreateVersion7();

        var harness = new Harness();
        harness.Files
            .Setup(client => client.ListChildrenAsync(UserId, null, 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileListResult
            {
                Status = FilesAccessStatus.Success,
                TotalCount = 1,
                Nodes =
                [
                    new FileNodeInfo
                    {
                        Id = nodeId,
                        Name = "Quarterly report.pdf",
                        NodeType = "File",
                        MimeType = "application/pdf",
                        Size = 1024,
                        ParentId = null,
                        UpdatedAt = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero),
                    },
                ],
            });

        var response = await harness.Service.ListFiles(
            new Api.ListFilesRequest(),
            UserContext("/dotnetcloud.moduleapi.v1.ModuleApi/ListFiles"));

        Assert.AreEqual(1, response.TotalCount);
        Assert.AreEqual(nodeId.ToString(), response.Nodes[0].Id);
        Assert.AreEqual("application/pdf", response.Nodes[0].MimeType);
        Assert.AreEqual(string.Empty, response.Nodes[0].ParentId, "root-level nodes have no parent id on the wire");
    }

    [TestMethod]
    public async Task DownloadFile_StreamsTheFilesModulesContent()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        var harness = new Harness();
        harness.Files
            .Setup(client => client.OpenReadAsync(UserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileReadResult
            {
                Status = FilesAccessStatus.Success,
                Content = new MemoryStream(payload),
            });

        var writer = new CollectingWriter<Api.FileContentChunk>();

        await harness.Service.DownloadFile(
            new Api.DownloadFileRequest { NodeId = Guid.CreateVersion7().ToString() },
            writer,
            UserContext("/dotnetcloud.moduleapi.v1.ModuleApi/DownloadFile"));

        var received = writer.Items.SelectMany(chunk => chunk.Data.ToByteArray()).ToArray();

        CollectionAssert.AreEqual(payload, received);
    }

    [TestMethod]
    public async Task UploadFile_FirstMessageWithoutMetadata_ThrowsInvalidArgument()
    {
        var harness = new Harness();

        var stream = new FakeStreamReader(
        [
            new Api.UploadFileRequest
            {
                Chunk = new Api.FileContentChunk { Data = Google.Protobuf.ByteString.CopyFrom([1]) },
            },
        ]);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => harness.Service.UploadFile(stream, UserContext("/dotnetcloud.moduleapi.v1.ModuleApi/UploadFile")));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
        harness.Files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task UploadFile_DeclaredSizeOverTheLimit_ThrowsResourceExhausted()
    {
        var harness = new Harness();

        var stream = new FakeStreamReader(
        [
            new Api.UploadFileRequest
            {
                Metadata = new Api.ModuleFileUploadMetadata
                {
                    FileName = "huge.bin",
                    MimeType = "application/octet-stream",
                    TotalSize = ModuleFileAccessService.MaxUploadBytes + 1,
                },
            },
        ]);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => harness.Service.UploadFile(stream, UserContext("/dotnetcloud.moduleapi.v1.ModuleApi/UploadFile")));

        Assert.AreEqual(StatusCode.ResourceExhausted, ex.StatusCode);
        harness.Files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task UploadFile_MetadataThenChunks_StoresTheContentAndReturnsTheNode()
    {
        byte[]? stored = null;
        string? storedName = null;

        var harness = new Harness();
        harness.Files
            .Setup(client => client.UploadFileAsync(
                UserId,
                null,
                "hello.txt",
                "text/plain",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid?, string, string, Stream, CancellationToken>(async (_, _, name, _, content, token) =>
            {
                storedName = name;
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer, token);
                stored = buffer.ToArray();

                return new FileWriteResult
                {
                    Status = FilesAccessStatus.Success,
                    Node = new FileNodeInfo { Id = Guid.CreateVersion7(), Name = name, NodeType = "File", Size = stored.Length },
                };
            });

        var stream = new FakeStreamReader(
        [
            new Api.UploadFileRequest
            {
                Metadata = new Api.ModuleFileUploadMetadata
                {
                    FileName = "hello.txt",
                    MimeType = "text/plain",
                    TotalSize = 6,
                },
            },
            new Api.UploadFileRequest { Chunk = new Api.FileContentChunk { Data = Google.Protobuf.ByteString.CopyFrom("hello "u8) } },
            new Api.UploadFileRequest { Chunk = new Api.FileContentChunk { Data = Google.Protobuf.ByteString.CopyFrom("world"u8) } },
        ]);

        var response = await harness.Service.UploadFile(
            stream,
            UserContext("/dotnetcloud.moduleapi.v1.ModuleApi/UploadFile"));

        Assert.AreEqual("hello.txt", storedName);
        CollectionAssert.AreEqual("hello world"u8.ToArray(), stored);
        Assert.AreEqual("hello.txt", response.Node.Name);
    }

    private static TestServerCallContext UserContext(string method)
    {
        var context = new TestServerCallContext(method, []);
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = new CallerContext(UserId, [], CallerType.User);
        return context;
    }

    private sealed class Harness
    {
        public Harness()
        {
            var services = new ServiceCollection();
            services.AddScoped(_ => Files.Object);
            var provider = services.BuildServiceProvider();

            var manifests = new ModuleManifestRegistry();
            manifests.Record(ModuleId, new ModuleManifestData
            {
                Id = ModuleId,
                Name = "Email",
                Version = "1.2.0",
                ManifestVersion = 2,
                UserConsentScopes = [ModuleScopes.FilesRead, ModuleScopes.FilesWrite],
            });

            var policy = ConsentTestDoubles.Policy(
                manifests,
                new ConsentTestDoubles.GrantingConsentService([ModuleScopes.FilesRead, ModuleScopes.FilesWrite]));

            var fileAccess = new ModuleFileAccessService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                policy,
                NullLogger<ModuleFileAccessService>.Instance);

            var legacy = new Mock<CoreCapabilitiesServiceImpl>(
                NullLogger<CoreCapabilitiesServiceImpl>.Instance,
                provider,
                null!,
                null!,
                ConsentTestDoubles.NoConsent());

            Service = new ModuleApiV1Service(
                legacy.Object,
                new ModuleHealthRegistry(TimeProvider.System),
                policy,
                fileAccess,
                NullLogger<ModuleApiV1Service>.Instance);
        }

        public Mock<IFilesApiClient> Files { get; } = new();

        public ModuleApiV1Service Service { get; }
    }

    private sealed class CollectingWriter<T> : IServerStreamWriter<T>
    {
        public List<T> Items { get; } = [];

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            Items.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStreamReader : IAsyncStreamReader<Api.UploadFileRequest>
    {
        private readonly Queue<Api.UploadFileRequest> _messages;

        public FakeStreamReader(IEnumerable<Api.UploadFileRequest> messages)
        {
            _messages = new Queue<Api.UploadFileRequest>(messages);
        }

        public Api.UploadFileRequest Current { get; private set; } = new();

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_messages.Count == 0)
            {
                return Task.FromResult(false);
            }

            Current = _messages.Dequeue();
            return Task.FromResult(true);
        }
    }
}
