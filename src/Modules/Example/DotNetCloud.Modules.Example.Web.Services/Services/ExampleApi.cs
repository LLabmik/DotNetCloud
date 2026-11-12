using System.Text.Json;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Example.Web.Services;

/// <summary>
/// The Example module UI's client for the module host's own notes API.
/// </summary>
/// <remarks>
/// URLs are relative on purpose: the frame is served under <c>/apps/dotnetcloud.example/</c> and the
/// core proxy strips that prefix, so <c>api/notes</c> resolves inside the module either way. The
/// host answers <c>{ "notes": [...] }</c> for the list and the bare note object for a create, which
/// is why the list is read through an element rather than a typed envelope.
/// </remarks>
public sealed class ExampleApi
{
    private readonly ModuleApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="ExampleApi"/> class.</summary>
    /// <param name="api">The shared frame API client.</param>
    public ExampleApi(ModuleApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <summary>Lists the acting user's notes, newest first.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notes, or an empty list when the host returned none.</returns>
    public async Task<IReadOnlyList<ExampleNoteDto>> ListNotesAsync(CancellationToken cancellationToken = default)
    {
        var element = await _api
            .SendForElementAsync(HttpMethod.Get, "api/notes", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (element is null)
        {
            return [];
        }

        if (ModuleApiClient.TryGetProperty(element.Value, "notes", out var notes)
            && notes.ValueKind == JsonValueKind.Array)
        {
            return _api.Deserialize<List<ExampleNoteDto>>(notes) ?? [];
        }

        return _api.Deserialize<List<ExampleNoteDto>>(element.Value) ?? [];
    }

    /// <summary>Creates a note owned by the acting user.</summary>
    /// <param name="title">The title (required).</param>
    /// <param name="content">The body (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the note.</returns>
    public Task CreateNoteAsync(string title, string? content, CancellationToken cancellationToken = default)
        => _api.SendAsync(HttpMethod.Post, "api/notes", new { title, content }, cancellationToken);

    /// <summary>Deletes one of the acting user's notes.</summary>
    /// <param name="noteId">The note id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the deletion.</returns>
    public Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
        => _api.SendAsync(HttpMethod.Delete, $"api/notes/{noteId}", cancellationToken: cancellationToken);
}
