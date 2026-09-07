using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Contacts.Widget;

/// <summary>
/// Home-page widget for the Contacts module: the five most recently created contacts
/// for the signed-in user.
/// </summary>
public partial class ContactsWidget : ComponentBase
{
    [Inject] private IContactsApiClient ApiClient { get; set; } = default!;

    private readonly List<ContactDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No contacts yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var contacts = await ApiClient.GetRecentContactsAsync(5);
            _items.AddRange(contacts);
        }
        catch (Exception)
        {
            _error = "Unable to load contacts.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Contacts module at the given contact.
    /// </summary>
    private static string HrefFor(ContactDto item)
        => $"/apps/contacts?contactId={item.Id}";
}
