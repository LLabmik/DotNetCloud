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
}
