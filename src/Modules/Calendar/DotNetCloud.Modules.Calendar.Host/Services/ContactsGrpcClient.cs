using DotNetCloud.Modules.Calendar.Host.Configuration;
using DotNetCloud.Modules.Calendar.Services;
using DotNetCloud.Modules.Contacts.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// gRPC client for calling the Contacts module's SearchContacts RPC.
/// Used by the Calendar module to resolve contacts for event attendee autocomplete.
/// </summary>
public sealed class ContactsGrpcClient : IDisposable
{
    private readonly ContactsGrpcClientOptions _options;
    private readonly ILogger<ContactsGrpcClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<ContactsService.ContactsServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ContactsGrpcClient"/> class.</summary>
    public ContactsGrpcClient(IOptions<ContactsGrpcClientOptions> options, ILogger<ContactsGrpcClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<ContactsService.ContactsServiceClient>(
            () => new ContactsService.ContactsServiceClient(_channel.Value));
    }

    /// <summary>
    /// Searches contacts owned by a user by display name or email address.
    /// Returns empty list on transient errors (Contacts process restart, network blip).
    /// </summary>
    public async Task<IReadOnlyList<ContactSearchResultDto>> SearchContactsAsync(
        Guid userId,
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var request = new SearchContactsRequest
            {
                UserId = userId.ToString(),
                Query = query.Trim(),
                MaxResults = maxResults
            };

            var callOptions = new CallOptions(
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            var response = await _client.Value.SearchContactsAsync(request, callOptions);

            if (!response.Success)
            {
                _logger.LogWarning("Contacts SearchContacts gRPC call failed: {Error}", response.ErrorMessage);
                return [];
            }

            return response.Results.Select(r => new ContactSearchResultDto
            {
                ContactId = Guid.Parse(r.ContactId),
                DisplayName = r.DisplayName,
                Emails = r.Emails.Select(e => (e.Address, e.Label)).ToList()
            }).ToList();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning("Contacts module gRPC service unavailable at {Address}", _options.ContactsModuleAddress);
            return [];
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("Contacts gRPC call timed out after {Timeout}", _options.Timeout);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Contacts module gRPC");
            return [];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_channel.IsValueCreated)
        {
            try
            { _channel.Value.ShutdownAsync().Wait(TimeSpan.FromSeconds(3)); }
            catch { /* best effort */ }
            _channel.Value.Dispose();
        }
    }

    private GrpcChannel CreateChannel()
    {
        var channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 4 * 1024 * 1024,  // 4 MB
            MaxSendMessageSize = 1 * 1024 * 1024,     // 1 MB
        };

        var address = _options.ContactsModuleAddress;

        if (address.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var socketPath = address["unix://".Length..];
            channelOptions.HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, ct) =>
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix,
                        System.Net.Sockets.SocketType.Stream,
                        System.Net.Sockets.ProtocolType.Unspecified);
                    var ep = new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath);
                    await socket.ConnectAsync(ep, ct);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
            };
            address = "http://localhost";
        }

        return GrpcChannel.ForAddress(address, channelOptions);
    }
}
