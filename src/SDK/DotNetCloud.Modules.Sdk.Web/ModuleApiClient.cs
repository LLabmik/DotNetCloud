using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// The HTTP client a module UI uses to talk to its own host.
/// </summary>
/// <remarks>
/// <para>
/// Every URL is <b>relative</b>: the core proxies <c>/apps/&lt;moduleId&gt;/</c> and strips the
/// prefix, so the module host serves both the frame document and its API under the frame's own base
/// address. The frame ticket that authenticates each call is attached by
/// <c>js/dncModuleBridge.js</c>, which patches <c>window.fetch</c>; nothing here has to know about it.
/// </para>
/// <para>
/// The core answers REST calls with an envelope (<c>{"success":true,"data":…}</c>) while a few module
/// hosts answer a bare object. Both are accepted: the payload is unwrapped when a <c>data</c>
/// property is present and returned as-is otherwise.
/// </para>
/// </remarks>
public sealed class ModuleApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes a new instance of the <see cref="ModuleApiClient"/> class.</summary>
    /// <param name="http">The scoped HTTP client whose base address is the frame document.</param>
    /// <param name="jsonOptions">
    /// Optional serializer settings. When omitted, <see cref="CreateDefaultJsonOptions"/> is used.
    /// </param>
    public ModuleApiClient(HttpClient http, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _json = jsonOptions ?? CreateDefaultJsonOptions();
    }

    /// <summary>Gets the serializer settings every request helper uses.</summary>
    public JsonSerializerOptions JsonOptions => _json;

    /// <summary>
    /// Creates the default serializer settings: web casing, enum names accepted on read *and* on
    /// write (a module host that calls <c>AddModuleJsonOptions</c> emits names; one that keeps MVC
    /// defaults reads both names and integers).
    /// </summary>
    /// <returns>A new options instance.</returns>
    public static JsonSerializerOptions CreateDefaultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>Sends a request and returns the unwrapped payload.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">A module-relative URL, e.g. <c>api/v1/bookmarks</c>.</param>
    /// <param name="body">An optional JSON body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payload, or <see langword="null"/> when the response had no content.</returns>
    public async Task<JsonElement?> SendForElementAsync(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, url, body);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(payload) ? null : Unwrap(payload);
    }

    /// <summary>Sends a request and deserialises the unwrapped payload.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">A module-relative URL.</param>
    /// <param name="body">An optional JSON body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payload, or <see langword="default"/> when the response had no content.</returns>
    public async Task<T?> SendAsync<T>(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        var element = await SendForElementAsync(method, url, body, cancellationToken).ConfigureAwait(false);
        return element is null ? default : Deserialize<T>(element.Value);
    }

    /// <summary>Sends a request whose payload is not needed.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">A module-relative URL.</param>
    /// <param name="body">An optional JSON body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the request.</returns>
    public async Task SendAsync(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, url, body);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request with a caller-supplied body (multipart form data, for example) and deserialises
    /// the unwrapped payload.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">A module-relative URL.</param>
    /// <param name="content">The request body. Disposed with the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payload, or <see langword="default"/> when the response had no content.</returns>
    public async Task<T?> SendContentAsync<T>(
        HttpMethod method,
        string url,
        HttpContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(payload) ? default : Deserialize<T>(Unwrap(payload));
    }

    /// <summary>Deserialises an element with this client's settings.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="element">The element.</param>
    /// <returns>The deserialised value.</returns>
    public T? Deserialize<T>(JsonElement element) => element.Deserialize<T>(_json);

    /// <summary>Reads a case-insensitive property from an element.</summary>
    /// <param name="element">The element to read from.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value when found.</param>
    /// <returns><see langword="true"/> when the property exists.</returns>
    public static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.ParseAdd("application/json");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: _json);
        }

        return request;
    }

    private JsonElement Unwrap(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object
            && TryGetProperty(root, "data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object
                && TryGetProperty(data, "data", out var nested))
            {
                return nested.Clone();
            }

            return data.Clone();
        }

        return root.Clone();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        string? code = null;
        string message = $"Request failed ({status}).";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (TryGetProperty(document.RootElement, "error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    message = error.GetString() ?? message;
                }
                else if (error.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetProperty(error, "message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    {
                        message = msg.GetString() ?? message;
                    }

                    if (TryGetProperty(error, "code", out var errCode) && errCode.ValueKind == JsonValueKind.String)
                    {
                        code = errCode.GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not an error envelope; keep the generic message.
        }

        code ??= ReadHeader(response, "error-code");

        if (code is ModuleConsentRequiredException.ProfileConsentCode
            or ModuleConsentRequiredException.FilesConsentCode
            or ModuleConsentRequiredException.ConsentRequiredCode)
        {
            throw new ModuleConsentRequiredException(message, ReadConsentScopes(response));
        }

        throw new ModuleApiException(message, status, code);
    }

    private static string? ReadHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string[] ReadConsentScopes(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("consent-scopes", out var values))
        {
            return ["profile"];
        }

        var scopes = values
            .SelectMany(value => value.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return scopes.Length == 0 ? ["profile"] : scopes;
    }
}
