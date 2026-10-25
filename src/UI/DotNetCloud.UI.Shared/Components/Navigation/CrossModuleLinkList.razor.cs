using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Navigation;

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

        var resolved = await LinkResolver.ResolveBatchAsync(Requests);
        _resolvedLinks.AddRange(resolved);
    }
}
