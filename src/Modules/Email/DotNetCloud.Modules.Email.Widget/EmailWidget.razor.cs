using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Email.Widget;

/// <summary>
/// Home-page widget for the Email module: shows a "No account configured" hint
/// when the signed-in user has no email account, otherwise lists the five most
/// recent inbox threads.
/// </summary>
public partial class EmailWidget : ComponentBase
{
    [Inject] private IEmailApiClient ApiClient { get; set; } = default!;

    private readonly List<EmailThreadDto> _items = [];
    private bool _loading = true;
    private bool _hasAccount;
    private string? _error;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var accounts = await ApiClient.ListAccountsAsync();
            _hasAccount = accounts.Count > 0;
            if (_hasAccount)
            {
                var threads = await ApiClient.GetRecentThreadsAsync(5);
                _items.AddRange(threads);
            }
        }
        catch (Exception)
        {
            _error = "Unable to load email.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds the URL that opens the given thread in the Email module page.
    /// </summary>
    private static string HrefFor(EmailThreadDto item)
        => $"/apps/email?threadId={item.Id}";
}
