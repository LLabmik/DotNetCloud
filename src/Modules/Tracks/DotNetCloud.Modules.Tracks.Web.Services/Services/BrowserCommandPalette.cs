using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Browser-side <see cref="ICommandPaletteService"/>: the palette search runs in the module host,
/// which owns the query, and the frame only renders the grouped results.
/// </summary>
/// <remarks>
/// Search is one round trip: <c>GET api/v1/organizations/{orgId}/palette?q=...&amp;productId=...</c>.
/// A failure returns an empty result rather than throwing into the keyboard handler — a palette that
/// cannot search must not make the page look broken.
/// </remarks>
public sealed class BrowserCommandPalette : ICommandPaletteService
{
    private readonly TracksApi _api;
    private readonly ILogger<BrowserCommandPalette> _logger;

    /// <summary>Initializes a new instance of the <see cref="BrowserCommandPalette"/> class.</summary>
    /// <param name="api">The module API client.</param>
    /// <param name="logger">Logger.</param>
    public BrowserCommandPalette(TracksApi api, ILogger<BrowserCommandPalette> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CommandPaletteResult> SearchAsync(
        Guid organizationId,
        string query,
        Guid? currentProductId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new CommandPaletteResult();
        }

        var path = $"api/v1/organizations/{organizationId}/palette?q={Uri.EscapeDataString(query)}";
        if (currentProductId is { } productId)
        {
            path += $"&productId={productId}";
        }

        try
        {
            return await _api.SendAsync<CommandPaletteResult>(HttpMethod.Get, path, cancellationToken: ct)
                ?? new CommandPaletteResult();
        }
        catch (Exception ex) when (ex is TracksApiException or HttpRequestException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Command palette search failed");
            return new CommandPaletteResult();
        }
    }
}
