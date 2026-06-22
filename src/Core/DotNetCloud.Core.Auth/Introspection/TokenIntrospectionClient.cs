using System.Security.Claims;
using DotNetCloud.Core.Grpc.TokenIntrospection;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Introspection;

/// <summary>
/// gRPC client for the TokenIntrospection service hosted by Core.Server.
/// Connects over the internal gRPC channel (Unix socket / Named Pipe depending on platform).
/// </summary>
internal sealed class TokenIntrospectionClient : ITokenIntrospectionClient, IDisposable
{
    private readonly ILogger<TokenIntrospectionClient> _logger;
    private readonly GrpcChannel _channel;
    private readonly TokenIntrospection.TokenIntrospectionClient _client;
    private bool _disposed;

    public TokenIntrospectionClient(ILogger<TokenIntrospectionClient> logger)
    {
        _logger = logger;

        var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(coreEndpoint))
        {
            throw new InvalidOperationException(
                "DOTNETCLOUD_CORE_ENDPOINT environment variable is not set. " +
                "The module host must be launched by ProcessSupervisor, which sets this variable.");
        }

        // The endpoint is in internal gRPC format (e.g., "http://localhost:5001")
        // Convert from the internal URL format used by ProcessSupervisor.
        var address = coreEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://");

        _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                UseCookies = false,
            },
            ThrowOperationCanceledOnCancellation = true,
        });

        _client = new TokenIntrospection.TokenIntrospectionClient(_channel);

        _logger.LogInformation(
            "TokenIntrospectionClient: connected to Core.Server at {Endpoint}",
            address);
    }

    /// <inheritdoc />
    public async Task<IntrospectionResult> IntrospectAsync(
        string token,
        string moduleId,
        string? requiredAudience,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var request = new IntrospectTokenRequest
        {
            Token = token,
            CallerModuleId = moduleId,
        };

        if (!string.IsNullOrWhiteSpace(requiredAudience))
            request.RequiredAudience = requiredAudience;

        try
        {
            // Attach module-id header required by Core.Server's AuthenticationInterceptor.
            var headers = new Metadata
            {
                { "module-id", moduleId }
            };

            var response = await _client.IntrospectTokenAsync(
                request,
                headers,
                cancellationToken: cancellationToken);

            var result = new IntrospectionResult
            {
                Active = response.Active,
                Sub = string.IsNullOrWhiteSpace(response.Sub) ? null : response.Sub,
                Username = string.IsNullOrWhiteSpace(response.Username) ? null : response.Username,
                Email = string.IsNullOrWhiteSpace(response.Email) ? null : response.Email,
                Scopes = response.Scopes.ToArray(),
                Claims = response.Claims
                    .Select(c => new Claim(c.Type, c.Value))
                    .ToArray(),
                ErrorDescription = string.IsNullOrWhiteSpace(response.ErrorDescription) ? null : response.ErrorDescription,
            };

            if (result.Active)
            {
                _logger.LogDebug(
                    "IntrospectAsync: token valid for sub={Sub}, module={Module}",
                    result.Sub, moduleId);
            }
            else
            {
                _logger.LogWarning(
                    "IntrospectAsync: token INACTIVE. Module={Module}, Reason={Reason}",
                    moduleId, result.ErrorDescription);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "IntrospectAsync: gRPC call failed for module {Module}", moduleId);

            // Return inactive rather than throwing — the auth handler
            // will treat this as an authentication failure.
            return new IntrospectionResult
            {
                Active = false,
                ErrorDescription = $"Introspection service unavailable: {ex.Message}",
            };
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _channel.Dispose();
    }
}
