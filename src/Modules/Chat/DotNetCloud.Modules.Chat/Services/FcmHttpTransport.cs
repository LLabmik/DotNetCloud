using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// FCM transport that sends messages via the Firebase Cloud Messaging HTTP v1 API.
/// Authenticates using a Google service account key file.
/// </summary>
internal sealed class FcmHttpTransport : IFcmTransport, IDisposable
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string FcmScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const int MaxConcurrency = 10;

    private readonly HttpClient _httpClient;
    private readonly FcmPushOptions _options;
    private readonly ILogger<FcmHttpTransport> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;
    private ServiceAccountCredentials? _credentials;

    public FcmHttpTransport(
        HttpClient httpClient,
        IOptions<FcmPushOptions> options,
        ILogger<FcmHttpTransport> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FcmSendResult> SendAsync(DeviceRegistration device, PushNotification notification, CancellationToken cancellationToken = default)
    {
        var credentials = GetCredentials();
        if (credentials is null)
            return new FcmSendResult { Error = "FCM credentials not configured" };

        var accessToken = await GetAccessTokenAsync(credentials, cancellationToken);
        if (accessToken is null)
            return new FcmSendResult { Error = "Failed to obtain FCM access token" };

        return await SendSingleAsync(device, notification, credentials.ProjectId, accessToken, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FcmSendResult>> SendBatchAsync(
        IReadOnlyList<(DeviceRegistration Device, PushNotification Notification)> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
            return [];

        var credentials = GetCredentials();
        if (credentials is null)
            return messages.Select(_ => new FcmSendResult { Error = "FCM credentials not configured" }).ToList();

        var accessToken = await GetAccessTokenAsync(credentials, cancellationToken);
        if (accessToken is null)
            return messages.Select(_ => new FcmSendResult { Error = "Failed to obtain FCM access token" }).ToList();

        // Use SemaphoreSlim to limit concurrent FCM API calls
        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = messages.Select(async m =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await SendSingleAsync(m.Device, m.Notification, credentials.ProjectId, accessToken, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results;
    }

    private async Task<FcmSendResult> SendSingleAsync(
        DeviceRegistration device,
        PushNotification notification,
        string projectId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";

        var payload = new FcmPayload
        {
            Message = new FcmMessage
            {
                Token = device.Token,
                Notification = new FcmNotificationBody
                {
                    Title = notification.Title,
                    Body = notification.Body,
                    Image = notification.ImageUrl
                },
                Data = notification.Data,
                Android = new FcmAndroidConfig
                {
                    Priority = "high"
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
                return FcmSendResult.Success;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // FCM returns UNREGISTERED for invalid tokens
            if (body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("FCM invalid token for device {Token}: {Body}",
                    device.Token[..Math.Min(8, device.Token.Length)], body);
                return FcmSendResult.InvalidToken;
            }

            _logger.LogWarning("FCM send failed ({StatusCode}): {Body}", response.StatusCode, body);
            return new FcmSendResult { Error = $"HTTP {(int)response.StatusCode}: {body}" };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "FCM send exception for device {Token}",
                device.Token[..Math.Min(8, device.Token.Length)]);
            return new FcmSendResult { Error = ex.Message };
        }
    }

    private ServiceAccountCredentials? GetCredentials()
    {
        if (_credentials is not null)
            return _credentials;

        if (string.IsNullOrWhiteSpace(_options.CredentialsPath))
        {
            _logger.LogDebug("FCM credentials path not configured");
            return null;
        }

        if (!File.Exists(_options.CredentialsPath))
        {
            _logger.LogWarning("FCM credentials file not found: {Path}", _options.CredentialsPath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(_options.CredentialsPath);
            _credentials = JsonSerializer.Deserialize<ServiceAccountCredentials>(json, JsonOptions);
            return _credentials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load FCM credentials from {Path}", _options.CredentialsPath);
            return null;
        }
    }

    private async Task<string?> GetAccessTokenAsync(ServiceAccountCredentials credentials, CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresUtc.AddMinutes(-5))
            return _accessToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresUtc.AddMinutes(-5))
                return _accessToken;

            var jwt = CreateSignedJwt(credentials);
            var token = await ExchangeJwtForTokenAsync(jwt, cancellationToken);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string CreateSignedJwt(ServiceAccountCredentials credentials)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddHours(1);

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT"
        }));

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = credentials.ClientEmail,
            scope = FcmScope,
            aud = TokenEndpoint,
            iat = now.ToUnixTimeSeconds(),
            exp = expiry.ToUnixTimeSeconds()
        }));

        var signatureInput = $"{header}.{payload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(credentials.PrivateKey);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signatureInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signatureInput}.{Base64UrlEncode(signature)}";
    }

    private async Task<string?> ExchangeJwtForTokenAsync(string jwt, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        });

        try
        {
            using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
            if (tokenResponse?.AccessToken is null)
                return null;

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange JWT for FCM access token");
            return null;
        }
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ── Internal models ──────────────────────────────────────────────────

    private sealed record ServiceAccountCredentials
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; init; } = string.Empty;

        [JsonPropertyName("client_email")]
        public string ClientEmail { get; init; } = string.Empty;

        [JsonPropertyName("private_key")]
        public string PrivateKey { get; init; } = string.Empty;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed record FcmPayload
    {
        public FcmMessage Message { get; init; } = null!;
    }

    private sealed record FcmMessage
    {
        public string Token { get; init; } = string.Empty;
        public FcmNotificationBody? Notification { get; init; }
        public Dictionary<string, string>? Data { get; init; }
        public FcmAndroidConfig? Android { get; init; }
    }

    private sealed record FcmNotificationBody
    {
        public string? Title { get; init; }
        public string? Body { get; init; }
        public string? Image { get; init; }
    }

    private sealed record FcmAndroidConfig
    {
        public string? Priority { get; init; }
    }
}
