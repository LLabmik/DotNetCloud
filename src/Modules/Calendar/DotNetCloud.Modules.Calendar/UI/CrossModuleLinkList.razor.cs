using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Calendar.UI;

/// <summary>Code-behind for the <see cref="CrossModuleLinkList"/> component.</summary>
public partial class CrossModuleLinkList : ComponentBase
{
    [Parameter]
    public IReadOnlyList<CrossModuleLinkRequest> Requests { get; set; } = [];

    private readonly List<CrossModuleLinkDto> _resolvedLinks = [];

    protected override async Task OnParametersSetAsync()
    {
        _resolvedLinks.Clear();

        if (Requests.Count == 0)
        {
            return;
        }

        _resolvedLinks.AddRange(await LinkResolver.ResolveBatchAsync(Requests));
    }
}
