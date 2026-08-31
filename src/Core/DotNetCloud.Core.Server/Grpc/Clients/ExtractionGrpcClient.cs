using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Search;
using DotNetCloud.Core.Search.Extraction.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// gRPC implementation of <see cref="IExtractionService"/> that calls the
/// out-of-process <c>dotnetcloud.extraction</c> worker over gRPC.
/// </summary>
public sealed class ExtractionGrpcClient : IExtractionService, IDisposable
{
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<ExtractionGrpcClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<ExtractionService.ExtractionServiceClient> _client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionGrpcClient"/> class.
    /// </summary>
    public ExtractionGrpcClient(
        ModuleEndpointProvider endpointProvider,
        ILogger<ExtractionGrpcClient> logger)
    {
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<ExtractionService.ExtractionServiceClient>(
            () => new ExtractionService.ExtractionServiceClient(_channel.Value));
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.extraction");
        _logger.LogDebug("ExtractionGrpcClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    /// <inheritdoc />
    public async Task<ExtractedContent?> ExtractAsync(byte[] content, string mimeType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var request = new ExtractRequest
        {
            Content = Google.Protobuf.ByteString.CopyFrom(content),
            MimeType = mimeType ?? string.Empty
        };

        try
        {
            var response = await _client.Value
                .ExtractAsync(request, cancellationToken: cancellationToken)
                .ResponseAsync
                .ConfigureAwait(false);

            if (!response.Success)
            {
                _logger.LogWarning("Extraction worker failed for MIME type {MimeType}: {Error}",
                    mimeType, response.ErrorMessage);
                return null;
            }

            return new ExtractedContent
            {
                Text = response.Text,
                Metadata = new Dictionary<string, string>(response.Metadata)
            };
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "ExtractionGrpcClient.ExtractAsync failed for MIME type {MimeType}", mimeType);
            return null;
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
