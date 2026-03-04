using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Discovers Collabora Online capabilities via the WOPI discovery endpoint.
/// Caches the result and provides file-extension-to-editor-URL mapping.
/// </summary>
public interface ICollaboraDiscoveryService
{
    /// <summary>
    /// Fetches and caches the Collabora WOPI discovery document.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery result with available actions and proof keys.</returns>
    Task<CollaboraDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the editor URL for a given file extension and action.
    /// </summary>
    /// <param name="extension">File extension without dot (e.g., "docx").</param>
    /// <param name="action">WOPI action (e.g., "edit", "view"). Default: "edit".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The editor URL template, or null if the format is not supported.</returns>
    Task<string?> GetEditorUrlAsync(string extension, string action = "edit", CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether Collabora is available and responding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a file extension is supported for editing.
    /// </summary>
    /// <param name="extension">File extension without dot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsSupportedExtensionAsync(string extension, CancellationToken cancellationToken = default);
}
