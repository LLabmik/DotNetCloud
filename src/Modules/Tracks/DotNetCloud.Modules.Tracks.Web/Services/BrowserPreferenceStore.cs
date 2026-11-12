using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Small key/value preference store for the module UI that survives the sandbox.
/// </summary>
/// <remarks>
/// <para>
/// The core frames a module UI with <c>sandbox="allow-scripts …"</c> and no
/// <c>allow-same-origin</c>, so the document has an opaque origin and <c>localStorage</c> access throws
/// a <see cref="JSException"/>. The ported components still want "remember the selected organization"
/// behaviour, so this store tries the browser first and silently keeps the value in memory when the
/// browser refuses.
/// </para>
/// <para>
/// Reads are memory-first after a write, so a component never has to care which path was taken. The
/// effect in a sandbox is per-page-session persistence, which is the most a frame with no storage can
/// honestly offer.
/// </para>
/// </remarks>
public sealed class BrowserPreferenceStore
{
    private readonly IJSRuntime _js;
    private readonly ILogger<BrowserPreferenceStore> _logger;
    private readonly Dictionary<string, string> _memory = new(StringComparer.Ordinal);
    private bool _browserStorageUnavailable;

    /// <summary>Initializes a new instance of the <see cref="BrowserPreferenceStore"/> class.</summary>
    /// <param name="js">JS runtime.</param>
    /// <param name="logger">Logger.</param>
    public BrowserPreferenceStore(IJSRuntime js, ILogger<BrowserPreferenceStore> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>Reads a preference, returning <see langword="null"/> when it is not set.</summary>
    /// <param name="key">The preference key.</param>
    /// <returns>The stored value.</returns>
    public async Task<string?> GetAsync(string key)
    {
        if (_memory.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_browserStorageUnavailable)
        {
            return null;
        }

        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            _browserStorageUnavailable = true;
            _logger.LogDebug("Browser storage is unavailable in this frame; preferences are session-only.");
            return null;
        }
    }

    /// <summary>Writes a preference.</summary>
    /// <param name="key">The preference key.</param>
    /// <param name="value">The value.</param>
    public async Task SetAsync(string key, string value)
    {
        _memory[key] = value;

        if (_browserStorageUnavailable)
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            _browserStorageUnavailable = true;
            _logger.LogDebug("Browser storage is unavailable in this frame; preferences are session-only.");
        }
    }

    /// <summary>Removes a preference.</summary>
    /// <param name="key">The preference key.</param>
    public async Task RemoveAsync(string key)
    {
        _memory.Remove(key);

        if (_browserStorageUnavailable)
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
            _browserStorageUnavailable = true;
            _logger.LogDebug("Browser storage is unavailable in this frame; preferences are session-only.");
        }
    }
}
