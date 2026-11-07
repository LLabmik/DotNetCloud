using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc;

namespace DotNetCloud.Modules.Sdk.Api;

/// <summary>
/// JSON implementation of <see cref="IDncModuleApi"/> over the core's internal module REST facade.
/// </summary>
/// <remarks>
/// The facade accepts exactly the same credential and user assertion as the gRPC surface, and
/// answers with the same stable error codes, so a module backend written in another language is not
/// a second-class citizen. Errors raise <see cref="DncModuleApiException"/> carrying the HTTP status
/// and the core's error code.
/// </remarks>
public sealed class DncModuleRestClient : IDncModuleApi, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string? _userAssertion;

    /// <summary>Initializes a new instance of the <see cref="DncModuleRestClient"/> class.</summary>
    /// <param name="baseAddress">The facade base address (for example <c>https://core:5443/internal/moduleapi/v1/</c>).</param>
    /// <param name="moduleId">The module id.</param>
    /// <param name="moduleToken">The supervisor-issued credential.</param>
    /// <param name="httpClient">Optional pre-configured client (tests, custom handlers).</param>
    /// <param name="userAssertion">Optional end-user assertion to send with every call.</param>
    public DncModuleRestClient(
        Uri baseAddress,
        string moduleId,
        string? moduleToken,
        HttpClient? httpClient = null,
        string? userAssertion = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        _http = httpClient ?? new HttpClient();
        _http.BaseAddress = EnsureTrailingSlash(baseAddress);
        _http.DefaultRequestHeaders.Add(ModuleIdentityMetadata.ModuleIdHeader, moduleId);

        if (!string.IsNullOrEmpty(moduleToken))
        {
            _http.DefaultRequestHeaders.Add(ModuleIdentityMetadata.ModuleTokenHeader, moduleToken);
        }

        _userAssertion = userAssertion;
    }

    /// <summary>
    /// Creates a client from the environment the supervisor provides.
    /// </summary>
    /// <returns>The client.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the environment does not carry a REST endpoint or a credential.
    /// </exception>
    public static DncModuleRestClient CreateFromEnvironment()
    {
        var environment = ModuleEnvironment.FromEnvironment();

        if (environment.CoreRestEndpoint is null)
        {
            throw new InvalidOperationException(
                $"Environment variable {ModuleEnvironment.CoreRestEndpointVariable} is not set.");
        }

        return new DncModuleRestClient(
            new Uri(environment.CoreRestEndpoint, UriKind.Absolute),
            environment.ModuleId,
            Environment.GetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable));
    }

    /// <inheritdoc />
    public async Task<DncCurrentUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "me", content: null, cancellationToken);

        var me = await ReadAsync<CurrentUserDto>(response, cancellationToken);
        return new DncCurrentUser(
            me.UserId ?? string.Empty,
            me.DisplayName ?? string.Empty,
            me.Email ?? string.Empty,
            me.Locale ?? "en-US",
            me.Timezone ?? "UTC",
            me.Roles ?? []);
    }

    /// <inheritdoc />
    public async Task<DncModuleUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"users/{Uri.EscapeDataString(userId)}", content: null, cancellationToken);

        var result = await ReadAsync<UserResponseDto>(response, cancellationToken);
        return result.Found && result.User is not null ? ToUser(result.User) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DncModuleUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var path = $"users?query={Uri.EscapeDataString(query)}&limit={maxResults}";
        using var response = await SendAsync(HttpMethod.Get, path, content: null, cancellationToken);

        var result = await ReadAsync<UserListDto>(response, cancellationToken);
        return (result.Users ?? []).Select(ToUser).ToArray();
    }

    /// <inheritdoc />
    public async Task<int> SendNotificationAsync(
        DncNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        using var response = await SendAsync(
            HttpMethod.Post,
            "notifications",
            JsonContent.Create(
                new
                {
                    title = notification.Title,
                    body = notification.Body,
                    category = notification.Category,
                    link = notification.Link,
                    recipientUserIds = notification.RecipientUserIds,
                },
                options: Json),
            cancellationToken);

        var result = await ReadAsync<NotificationResponseDto>(response, cancellationToken);
        return result.DeliveredCount;
    }

    /// <inheritdoc />
    public async Task<bool> PublishEventAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "events",
            JsonContent.Create(new { eventType, payloadJson }, options: Json),
            cancellationToken);

        var result = await ReadAsync<SuccessDto>(response, cancellationToken);
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<bool> SubmitSearchIndexAsync(
        string entityId,
        bool remove = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "search/index",
            JsonContent.Create(new { entityId, action = remove ? 1 : 0 }, options: Json),
            cancellationToken);

        var result = await ReadAsync<SuccessDto>(response, cancellationToken);
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"settings/{Uri.EscapeDataString(key)}", content: null, cancellationToken);

        var result = await ReadAsync<SettingDto>(response, cancellationToken);
        return result.Found ? result.Value : null;
    }

    /// <inheritdoc />
    public async Task<bool> SetSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Put,
            $"settings/{Uri.EscapeDataString(key)}",
            JsonContent.Create(new { value }, options: Json),
            cancellationToken);

        var result = await ReadAsync<SuccessDto>(response, cancellationToken);
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<bool> LogAuditAsync(
        AuditAction action,
        string entityType,
        Guid entityId,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "audit",
            JsonContent.Create(
                new
                {
                    action = (int)action,
                    entityType,
                    entityId = entityId.ToString(),
                    description,
                },
                options: Json),
            cancellationToken);

        var result = await ReadAsync<SuccessDto>(response, cancellationToken);
        return result.Success;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };

        if (_userAssertion is not null)
        {
            request.Headers.TryAddWithoutValidation(ModuleIdentityMetadata.UserAssertionHeader, _userAssertion);
        }

        var response = await _http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            response.Dispose();
            throw new DncModuleApiException(error.Code, error.Message, error.Status);
        }

        return response;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);

        return value ?? throw new DncModuleApiException(
            "MODULE_API_EMPTY_RESPONSE",
            "The core returned an empty response body.",
            (int)response.StatusCode);
    }

    private static async Task<(string Code, string Message, int Status)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorDto>(Json, cancellationToken);
            if (error?.Code is { Length: > 0 } code)
            {
                return (code, error.Message ?? "Request failed.", (int)response.StatusCode);
            }
        }
        catch (JsonException)
        {
            // Fall through to the status-based fallback below.
        }

        return ($"HTTP_{(int)response.StatusCode}", "Request failed.", (int)response.StatusCode);
    }

    private static Uri EnsureTrailingSlash(Uri baseAddress) =>
        baseAddress.AbsolutePath.EndsWith('/')
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + "/", UriKind.Absolute);

    private static DncModuleUser ToUser(UserDto user) => new(
        user.Id ?? string.Empty,
        user.DisplayName ?? string.Empty,
        user.AvatarUrl ?? string.Empty,
        user.IsActive,
        user.Email ?? string.Empty);

    private sealed record UserDto(string? Id, string? DisplayName, string? AvatarUrl, bool IsActive, string? Email);

    private sealed record UserResponseDto(bool Found, UserDto? User);

    private sealed record UserListDto(IReadOnlyList<UserDto>? Users);

    private sealed record CurrentUserDto(
        string? UserId,
        string? DisplayName,
        string? Email,
        string? Locale,
        string? Timezone,
        IReadOnlyList<string>? Roles);

    private sealed record NotificationResponseDto(bool Success, int DeliveredCount);

    private sealed record SuccessDto(bool Success);

    private sealed record SettingDto(bool Found, string? Value);

    private sealed record ErrorDto(string? Code, string? Message);
}

/// <summary>An error returned by the core module API.</summary>
public sealed class DncModuleApiException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DncModuleApiException"/> class.</summary>
    /// <param name="code">The stable core error code (for example <c>CAP_NOT_GRANTED</c>).</param>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The transport status code.</param>
    public DncModuleApiException(string code, string message, int statusCode)
        : base($"[{code}] {message}")
    {
        Code = code;
        StatusCode = statusCode;
    }

    /// <summary>Gets the stable core error code.</summary>
    public string Code { get; }

    /// <summary>Gets the transport status code (HTTP status for the REST facade).</summary>
    public int StatusCode { get; }
}
