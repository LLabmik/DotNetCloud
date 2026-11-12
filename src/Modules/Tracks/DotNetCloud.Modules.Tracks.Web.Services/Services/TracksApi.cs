using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Typed HTTP access to the module host's REST API through the core proxy.
/// </summary>
/// <remarks>
/// Every call is relative: the core proxies <c>/apps/dotnetcloud.tracks/</c> to this module and strips
/// the prefix, so <c>api/v1/...</c> reaches the module's own controllers with the frame ticket attached
/// by the bridge's fetch patch. The module binds enums by name (<c>AddModuleJsonOptions</c>), so the
/// client serialises them the same way.
/// </remarks>
public sealed class TracksApi
{
    /// <summary>The JSON options matching the module's MVC configuration.</summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _http;

    /// <summary>Initializes a new instance of the <see cref="TracksApi"/> class.</summary>
    /// <param name="http">The configured client.</param>
    public TracksApi(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Reads the module host's version probe.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The module info, or <see langword="null"/> when the host cannot be reached.</returns>
    public async Task<ModuleInfo?> GetModuleInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<ModuleInfo>("api/info", JsonOptions, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sends a request and throws a <see cref="TracksApiException"/> that carries the module's error code
    /// (for example <c>UNAUTHORIZED</c> or <c>FILES_CONSENT_REQUIRED</c>) instead of a bare status code.
    /// </summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The module-relative path.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialised payload.</returns>
    public async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions, cancellationToken);
        if (envelope is null)
        {
            throw new TracksApiException(response.StatusCode, "EMPTY_RESPONSE", "The module returned an empty response.");
        }

        return envelope.Data;
    }

    /// <summary>
    /// Sends a request whose response is not needed.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The module-relative path.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the module accepted the request.</returns>
    public async Task SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string code = "UNKNOWN";
        string message = $"The module answered {(int)response.StatusCode}.";
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>(JsonOptions, cancellationToken);
            if (error?.Error is { } payload)
            {
                code = payload.Code ?? code;
                message = payload.Message ?? message;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Non-JSON error body (a proxy failure page): keep the status-based message.
        }

        throw new TracksApiException(response.StatusCode, code, message);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>The module host's version envelope.</summary>
    /// <param name="Module">Module id.</param>
    /// <param name="Version">Module version.</param>
    /// <param name="Status">Process status.</param>
    public sealed record ModuleInfo(string Module, string Version, string Status);

    private sealed record Envelope<T>(bool Success, T Data);

    private sealed record ErrorEnvelope(ErrorBody? Error);

    private sealed record ErrorBody(string? Code, string? Message);
}

/// <summary>
/// The module answered with an error envelope (`{ success: false, error: { code, message } }`).
/// </summary>
public sealed class TracksApiException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TracksApiException"/> class.</summary>
    /// <param name="statusCode">The HTTP status the module answered.</param>
    /// <param name="code">The module's error code.</param>
    /// <param name="message">The module's message.</param>
    public TracksApiException(System.Net.HttpStatusCode statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>Gets the HTTP status the module answered.</summary>
    public System.Net.HttpStatusCode StatusCode { get; }

    /// <summary>Gets the module's error code.</summary>
    public string Code { get; }
}
