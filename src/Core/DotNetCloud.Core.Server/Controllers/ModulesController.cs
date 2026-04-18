using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Public (authenticated) endpoints for querying module availability.
/// Unlike <see cref="AdminController"/>, these do not require the admin role.
/// </summary>
[ApiController]
[Route("api/v1/core/modules")]
[Authorize]
public class ModulesController : ControllerBase
{
    private readonly IAdminModuleService _moduleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModulesController"/> class.
    /// </summary>
    public ModulesController(IAdminModuleService moduleService)
    {
        _moduleService = moduleService ?? throw new ArgumentNullException(nameof(moduleService));
    }

    /// <summary>
    /// Checks whether a specific module is installed and available.
    /// Returns 200 with <c>{ installed: true }</c> or <c>{ installed: false }</c>.
    /// </summary>
    /// <param name="moduleId">The module identifier (e.g., "music").</param>
    [HttpGet("{moduleId}/available")]
    public async Task<IActionResult> IsModuleAvailableAsync(string moduleId)
    {
        var module = await _moduleService.GetModuleAsync(moduleId);
        return Ok(new { success = true, data = new { installed = module is not null } });
    }
}
