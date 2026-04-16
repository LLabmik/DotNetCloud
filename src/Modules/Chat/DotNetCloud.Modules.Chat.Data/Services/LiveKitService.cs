using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// LiveKit SFU integration service. Manages rooms and generates participant access tokens
/// via the LiveKit Server API. Used when 4+ participants join a call, requiring SFU media relay.
/// </summary>
internal sealed class LiveKitService : ILiveKitService
{
    private readonly LiveKitOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiveKitService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LiveKitService"/>.
    /// </summary>
    public LiveKitService(
        IOptions<LiveKitOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<LiveKitService> logger)
    {
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("livekit");
        _logger = logger;

        if (!_options.IsValid())
        {
            throw new InvalidOperationException(
                "LiveKit configuration is invalid. Ensure ServerUrl, ApiKey, and ApiSecret are configured in Chat:LiveKit.");
        }

        // Set base address for LiveKit API
        _httpClient.BaseAddress = new Uri(_options.ServerUrl.TrimEnd('/') + "/");
    }

    /// <inheritdoc />
    public bool IsAvailable => _options.Enabled && _options.IsValid();

    /// <inheritdoc />
    public int MaxP2PParticipants => _options.MaxP2PParticipants;

    /// <inheritdoc />
    public async Task<string> CreateRoomAsync(Guid callId, int maxParticipants, CancellationToken cancellationToken = default)
    {
        var roomName = $"call-{callId}";

        var request = new LiveKitCreateRoomRequest
        {
            Name = roomName,
            MaxParticipants = maxParticipants > 0 ? maxParticipants : _options.DefaultMaxParticipants,
            EmptyTimeout = _options.EmptyRoomTimeoutSeconds
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "twirp/livekit.RoomService/CreateRoom");
        httpRequest.Content = JsonContent.Create(request, options: JsonSerializerOptions);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", GenerateServiceToken());

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var roomResponse = await response.Content.ReadFromJsonAsync<LiveKitRoomResponse>(JsonSerializerOptions, cancellationToken);

        _logger.LogInformation(
            "Created LiveKit room {RoomName} for call {CallId} (MaxParticipants={MaxParticipants})",
            roomName, callId, maxParticipants);

        return roomResponse?.Name ?? roomName;
    }

    /// <inheritdoc />
    public string GenerateToken(string roomName, string participantIdentity, string participantName, bool canPublish = true, bool canSubscribe = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantIdentity);

        var now = DateTimeOffset.UtcNow;
        var exp = now.AddSeconds(_options.TokenTtlSeconds);

        var grants = new LiveKitVideoGrants
        {
            Room = roomName,
            RoomJoin = true,
            CanPublish = canPublish,
            CanSubscribe = canSubscribe,
            CanPublishData = true
        };

        var claims = new LiveKitTokenClaims
        {
            Iss = _options.ApiKey,
            Sub = participantIdentity,
            Name = participantName ?? participantIdentity,
            Nbf = now.ToUnixTimeSeconds(),
            Exp = exp.ToUnixTimeSeconds(),
            Iat = now.ToUnixTimeSeconds(),
            Jti = Guid.NewGuid().ToString("N"),
            Video = grants
        };

        var token = CreateJwt(claims);

        _logger.LogDebug(
            "Generated LiveKit token for participant {ParticipantIdentity} in room {RoomName} (Publish={CanPublish}, Subscribe={CanSubscribe})",
            participantIdentity, roomName, canPublish, canSubscribe);

        return token;
    }

    /// <inheritdoc />
    public async Task DeleteRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var request = new LiveKitDeleteRoomRequest { Room = roomName };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "twirp/livekit.RoomService/DeleteRoom");
        httpRequest.Content = JsonContent.Create(request, options: JsonSerializerOptions);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", GenerateServiceToken());

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Deleted LiveKit room {RoomName}", roomName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to delete LiveKit room {RoomName}. It may have already been cleaned up.", roomName);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRoomParticipantsAsync(string roomName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var request = new LiveKitListParticipantsRequest { Room = roomName };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "twirp/livekit.RoomService/ListParticipants");
        httpRequest.Content = JsonContent.Create(request, options: JsonSerializerOptions);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", GenerateServiceToken());

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LiveKitParticipantsResponse>(JsonSerializerOptions, cancellationToken);

        return result?.Participants?.Select(p => p.Identity).ToList() ?? [];
    }

    /// <summary>
    /// Generates a service-level JWT for LiveKit API calls (no room grants, just API key auth).
    /// </summary>
    private string GenerateServiceToken()
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new LiveKitTokenClaims
        {
            Iss = _options.ApiKey,
            Nbf = now.ToUnixTimeSeconds(),
            Exp = now.AddMinutes(5).ToUnixTimeSeconds(),
            Iat = now.ToUnixTimeSeconds(),
            Jti = Guid.NewGuid().ToString("N"),
            Video = new LiveKitVideoGrants { RoomList = true, RoomCreate = true, RoomAdmin = true }
        };

        return CreateJwt(claims);
    }

    /// <summary>
    /// Creates an HMAC-SHA256 signed JWT from the given claims using the API secret.
    /// </summary>
    internal string CreateJwt(LiveKitTokenClaims claims)
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "HS256", typ = "JWT" }));

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims, JsonSerializerOptions));

        var signingInput = $"{header}.{payload}";
        var keyBytes = Encoding.UTF8.GetBytes(_options.ApiSecret);

        using var hmac = new HMACSHA256(keyBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        var signature = Base64UrlEncode(signatureBytes);

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ── LiveKit API request/response models ─────────────────────

    internal sealed class LiveKitTokenClaims
    {
        [JsonPropertyName("iss")]
        public string? Iss { get; set; }

        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nbf")]
        public long Nbf { get; set; }

        [JsonPropertyName("exp")]
        public long Exp { get; set; }

        [JsonPropertyName("iat")]
        public long Iat { get; set; }

        [JsonPropertyName("jti")]
        public string? Jti { get; set; }

        [JsonPropertyName("video")]
        public LiveKitVideoGrants? Video { get; set; }
    }

    internal sealed class LiveKitVideoGrants
    {
        [JsonPropertyName("room")]
        public string? Room { get; set; }

        [JsonPropertyName("roomJoin")]
        public bool RoomJoin { get; set; }

        [JsonPropertyName("roomList")]
        public bool RoomList { get; set; }

        [JsonPropertyName("roomCreate")]
        public bool RoomCreate { get; set; }

        [JsonPropertyName("roomAdmin")]
        public bool RoomAdmin { get; set; }

        [JsonPropertyName("canPublish")]
        public bool CanPublish { get; set; }

        [JsonPropertyName("canSubscribe")]
        public bool CanSubscribe { get; set; }

        [JsonPropertyName("canPublishData")]
        public bool CanPublishData { get; set; }
    }

    internal sealed class LiveKitCreateRoomRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("max_participants")]
        public int MaxParticipants { get; set; }

        [JsonPropertyName("empty_timeout")]
        public int EmptyTimeout { get; set; }
    }

    internal sealed class LiveKitDeleteRoomRequest
    {
        [JsonPropertyName("room")]
        public string Room { get; set; } = string.Empty;
    }

    internal sealed class LiveKitListParticipantsRequest
    {
        [JsonPropertyName("room")]
        public string Room { get; set; } = string.Empty;
    }

    internal sealed class LiveKitRoomResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sid")]
        public string? Sid { get; set; }
    }

    internal sealed class LiveKitParticipantInfo
    {
        [JsonPropertyName("identity")]
        public string Identity { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sid")]
        public string? Sid { get; set; }
    }

    internal sealed class LiveKitParticipantsResponse
    {
        [JsonPropertyName("participants")]
        public List<LiveKitParticipantInfo>? Participants { get; set; }
    }
}
