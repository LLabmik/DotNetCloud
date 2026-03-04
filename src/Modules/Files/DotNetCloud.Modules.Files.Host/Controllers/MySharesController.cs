using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Exposes shares that have been granted to the calling user.
/// </summary>
[Route("api/v1/me/shares")]
public class MySharesController : FilesControllerBase
{
    private readonly IShareService _shareService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySharesController"/> class.
    /// </summary>
    public MySharesController(IShareService shareService)
    {
        _shareService = shareService;
    }

    /// <summary>
    /// Lists all files and folders that have been shared with the calling user.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> GetAsync([FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var shares = await _shareService.GetSharedWithMeAsync(ToCaller(userId));
        return Ok(Envelope(shares));
    });
}
