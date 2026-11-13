using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for user search (used by @mention typeahead).
/// </summary>
[ApiController]
public class UsersController : TracksControllerBase
{
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    public UsersController(IUserDirectory userDirectory, ILogger<UsersController> logger)
    {
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <summary>Searches users by display name or email for @mention typeahead.</summary>
    [HttpGet("api/v1/users/search")]
    public async Task<IActionResult> SearchUsersAsync([FromQuery] string q, [FromQuery] int max = 8, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();

        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(Envelope(Array.Empty<UserSearchResult>()));

        try
        {
            var results = await _userDirectory.SearchUsersAsync(q.Trim(), Math.Min(max, 20), ct);
            return Ok(Envelope(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search users for query '{Query}'", q);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "User search failed."));
        }
    }

    /// <summary>
    /// Resolves display names for a set of user ids (assignee chips, comment authors, member lists).
    /// </summary>
    /// <remarks>
    /// The sandboxed module UI cannot call the core's user directory itself; it asks the host. Unknown
    /// ids are simply absent from the response so the UI can fall back to a placeholder.
    /// </remarks>
    [HttpPost("api/v1/users/display-names")]
    public async Task<IActionResult> GetDisplayNamesAsync(
        [FromBody] DisplayNamesRequest request, CancellationToken ct = default)
    {
        GetAuthenticatedCaller();

        var ids = request?.UserIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (ids.Length == 0)
        {
            return Ok(Envelope(new Dictionary<Guid, string>()));
        }

        try
        {
            var names = await _userDirectory.GetDisplayNamesAsync(ids, ct);
            return Ok(Envelope(names));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve display names for {Count} users", ids.Length);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "Display-name lookup failed."));
        }
    }

    /// <summary>The user ids a display-name lookup should resolve.</summary>
    public sealed class DisplayNamesRequest
    {
        /// <summary>Gets or sets the ids to resolve.</summary>
        public List<Guid> UserIds { get; set; } = [];
    }
}
