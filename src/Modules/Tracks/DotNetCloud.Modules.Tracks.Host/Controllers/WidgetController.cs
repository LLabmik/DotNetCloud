using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller backing the Tracks home-page widget.
/// </summary>
/// <remarks>
/// <para>
/// The widget is a manifest-declared iframe card (manifest v2 <c>ui.widget.path</c>), so it cannot
/// read the module's database or its Blazor DI the way the old in-core
/// <c>TracksWidget</c> component did. It needs one small, assertion-scoped read instead — the same
/// work items <c>WorkItemService.GetMyUpcomingDueItemsAsync</c> already returns for the acting user.
/// </para>
/// <para>
/// The acting user is always the verified assertion subject: there is no user id in the payload, so
/// a card can never be pointed at another account's work.
/// </para>
/// </remarks>
[Route("api/v1/widget")]
public class WidgetController : TracksControllerBase
{
    private const int MaxItems = 20;

    private readonly WorkItemService _workItems;

    /// <summary>Initializes a new instance of the <see cref="WidgetController"/> class.</summary>
    /// <param name="workItems">Work-item service used for the upcoming-due query.</param>
    public WidgetController(WorkItemService workItems)
    {
        _workItems = workItems;
    }

    /// <summary>
    /// Returns the acting user's upcoming due work items (assigned to or watched by them, due within
    /// the next fourteen days, not in a done swimlane).
    /// </summary>
    /// <param name="take">How many items to return (1–20, default 5).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcomingAsync([FromQuery] int take = 5, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var count = Math.Clamp(take, 1, MaxItems);

        var items = await _workItems.GetMyUpcomingDueItemsAsync(caller.UserId, count, ct: ct);

        return Ok(Envelope(items.Select(item => new
        {
            id = item.Id,
            productId = item.ProductId,
            itemNumber = item.ItemNumber,
            title = item.Title,
            dueDate = item.DueDate,
            priority = item.Priority.ToString(),
            type = item.Type.ToString(),
        })));
    }
}
