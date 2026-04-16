using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Provides ICE server configuration for WebRTC clients.
/// Supports built-in STUN, external STUN, static TURN credentials,
/// and coturn-compatible ephemeral HMAC-SHA1 TURN credentials.
/// </summary>
public sealed class IceServerService : IIceServerService
{
    private readonly IceServerOptions _options;
    private readonly ILogger<IceServerService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="IceServerService"/> class.
    /// </summary>
    public IceServerService(
        IOptions<IceServerOptions> options,
        ILogger<IceServerService> logger,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string IceTransportPolicy => _options.IceTransportPolicy;

    /// <inheritdoc />
    public IReadOnlyList<IceServerDto> GetIceServers(string? publicHost = null)
    {
        var servers = new List<IceServerDto>();

        // 1. Built-in STUN server (privacy-first default)
        if (_options.EnableBuiltInStun)
        {
            var host = !string.IsNullOrWhiteSpace(publicHost)
                ? publicHost
                : !string.IsNullOrWhiteSpace(_options.StunPublicHost)
                    ? _options.StunPublicHost
                    : "localhost";

            servers.Add(new IceServerDto
            {
                Urls = [$"stun:{host}:{_options.StunPort}"]
            });
        }

        // 2. Additional STUN servers (opt-in — e.g., Google, Cloudflare)
        foreach (var url in _options.AdditionalStunUrls)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                servers.Add(new IceServerDto { Urls = [url] });
            }
        }

        // 3. TURN server(s) with ephemeral or static credentials
        if (_options.EnableTurn && _options.TurnUrls.Length > 0)
        {
            var validUrls = _options.TurnUrls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToArray();

            if (validUrls.Length > 0)
            {
                if (_options.EnableEphemeralCredentials &&
                    !string.IsNullOrWhiteSpace(_options.TurnSharedSecret))
                {
                    var (username, credential) = GenerateEphemeralCredentials();
                    servers.Add(new IceServerDto
                    {
                        Urls = validUrls,
                        Username = username,
                        Credential = credential
                    });

                    _logger.LogDebug(
                        "Generated ephemeral TURN credentials for {UrlCount} server(s), TTL {TtlSeconds}s",
                        validUrls.Length, _options.CredentialTtlSeconds);
                }
                else if (!string.IsNullOrWhiteSpace(_options.TurnUsername) &&
                         !string.IsNullOrWhiteSpace(_options.TurnCredential))
                {
                    servers.Add(new IceServerDto
                    {
                        Urls = validUrls,
                        Username = _options.TurnUsername,
                        Credential = _options.TurnCredential
                    });
                }
                else
                {
                    _logger.LogWarning(
                        "TURN is enabled but no valid credentials configured. " +
                        "Set either static TurnUsername/TurnCredential or enable ephemeral credentials with TurnSharedSecret.");
                }
            }
        }

        _logger.LogDebug("Returning {Count} ICE server(s) to client", servers.Count);
        return servers;
    }

    /// <summary>
    /// Generates coturn-compatible ephemeral TURN credentials using HMAC-SHA1.
    /// Username format: "{expiry-unix-timestamp}:{random-id}"
    /// Credential: Base64(HMAC-SHA1(shared_secret, username))
    /// </summary>
    internal (string Username, string Credential) GenerateEphemeralCredentials()
    {
        var expiry = _timeProvider.GetUtcNow().AddSeconds(_options.CredentialTtlSeconds);
        var expiryTimestamp = expiry.ToUnixTimeSeconds();

        // Random component prevents credential reuse across concurrent callers
        var randomId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var username = $"{expiryTimestamp}:{randomId}";

        var credential = ComputeHmacSha1(_options.TurnSharedSecret, username);

        return (username, credential);
    }

    /// <summary>
    /// Computes HMAC-SHA1 and returns the result as a Base64 string.
    /// This is the coturn-standard credential format.
    /// </summary>
    internal static string ComputeHmacSha1(string secret, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hash = HMACSHA1.HashData(keyBytes, messageBytes);
        return Convert.ToBase64String(hash);
    }
}
