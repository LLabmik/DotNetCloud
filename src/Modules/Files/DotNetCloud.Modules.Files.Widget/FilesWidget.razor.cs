using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Files.Widget;

/// <summary>
/// Home-page widget for the Files module: storage quota bar plus the five most recently
/// modified files for the signed-in user.
/// </summary>
public partial class FilesWidget : ComponentBase
{
    [Inject] private IFileService FileService { get; set; } = default!;
    [Inject] private IQuotaService QuotaService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<FileNodeDto> _items = [];
    private QuotaDto? _quota;
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No files yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var recent = await FileService.ListRecentAsync(5, caller);
            _items.AddRange(recent);

            try
            {
                _quota = await QuotaService.GetOrCreateQuotaAsync(caller.UserId, caller);
            }
            catch
            {
                // Quota is secondary — its failure must not hide the file list.
            }
        }
        catch (Exception)
        {
            _error = "Unable to load file data.";
        }
        finally
        {
            _loading = false;
        }
    }

    private QuotaDto? Quota => _quota;

    private string UsedText => FormatBytes(_quota?.UsedBytes ?? 0);

    private string MaxText => _quota is { MaxBytes: > 0 } ? FormatBytes(_quota.MaxBytes) : string.Empty;

    private string PercentText => _quota is { MaxBytes: > 0 } q
        ? $"{Math.Min(100, q.UsedBytes * 100.0 / q.MaxBytes):0}%"
        : string.Empty;

    private string PercentWidth => _quota is { MaxBytes: > 0 } q
        ? $"{Math.Min(100, q.UsedBytes * 100.0 / q.MaxBytes):0}%"
        : "0%";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.#} {units[unit]}";
    }

    /// <summary>
    /// Builds a deep link that opens the Files module at the folder containing the given file.
    /// A fresh <c>_nav</c> nonce is included so repeated clicks on the same file re-handle.
    /// </summary>
    private static string HrefFor(FileNodeDto item)
        => $"/apps/files?fileId={item.Id}&_nav={Guid.NewGuid():N}";

    private async Task<CallerContext> BuildCallerAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }
}
