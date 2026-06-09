using System.Text.Json;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Files.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Files gRPC client used by the Core Server.
/// </summary>
public sealed class FilesGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "FilesGrpc";
    /// <summary>The gRPC address of the Files module.</summary>
    public string FilesModuleAddress { get; set; } = "http://localhost:5004";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IFilesApiClient"/>.
/// </summary>
public sealed class FilesGrpcApiClient : IFilesApiClient, IDisposable
{
    private readonly FilesGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<FilesGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="FilesGrpcApiClient"/> class.</summary>
    public FilesGrpcApiClient(
        IOptions<FilesGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<FilesGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.files");
        _logger.LogInformation("FilesGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <inheritdoc />
    public async Task<MediaScanCandidatesResult> ScanMediaFoldersAsync(
        IReadOnlyCollection<MediaLibrarySource> sources,
        Guid ownerId,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new FilesService.FilesServiceClient(_channel.Value);
            var sourcesJson = JsonSerializer.Serialize(sources, JsonOptions);

            var request = new ScanMediaFoldersRequest
            {
                SourcesJson = sourcesJson,
                UserId = ownerId.ToString(),
                MediaType = mediaType,
            };

            var response = await client.ScanMediaFoldersAsync(request,
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("Media folder scan failed: {Error}", response.ErrorMessage);
                return new MediaScanCandidatesResult
                {
                    Success = false,
                    ErrorMessage = response.ErrorMessage,
                };
            }

            var candidates = response.Candidates
                .Select(c => new MediaFileCandidateDto
                {
                    Id = Guid.TryParse(c.Id, out var id) ? id : Guid.Empty,
                    Name = c.Name,
                    Size = c.Size,
                    MimeType = c.MimeType,
                    IsVirtual = c.IsVirtual,
                    SourceName = string.IsNullOrEmpty(c.SourceName) ? null : c.SourceName,
                    SubFolderPath = string.IsNullOrEmpty(c.SubFolderPath) ? null : c.SubFolderPath,
                })
                .Where(c => c.Id != Guid.Empty)
                .ToList();

            return new MediaScanCandidatesResult
            {
                Success = true,
                TotalFound = response.TotalFound,
                Candidates = candidates,
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(ex, "Files module is not available for media scan");
            return new MediaScanCandidatesResult
            {
                Success = false,
                ErrorMessage = "Files module is not available.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan media folders via gRPC");
            return new MediaScanCandidatesResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}
