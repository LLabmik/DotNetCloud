using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Contacts.UI;

/// <summary>Code-behind for the <see cref="ContactsPage"/> component.</summary>
public partial class ContactsPage : ComponentBase
{
    /// <summary>
    /// Optional contact ID to open when the page loads (deep-link from the home widget).
    /// </summary>
    [Parameter]
    public string? ContactId { get; set; }

    private Guid? _lastHandledContactId;

    private readonly List<ContactDto> _contacts = [];
    private readonly List<ContactGroupDto> _groups = [];
    private ContactRelatedEntitiesDto _related = new();
    private ContactDto? _selectedContact;
    private ContactEditorModel? _editingContact;
    private bool _avatarFailed;
    private string _searchQuery = string.Empty;
    private string _activeSection = "all";
    private bool _isLoading;
    private bool _showShareDialog;
    private List<DncShareEntry> _shareDialogShares = [];
    private bool _isLoadingShareDialogShares;
    private bool _sidebarCollapsed;

    /// <summary>Permission choices for contact shares (ReadOnly / ReadWrite).</summary>
    private static readonly IReadOnlyList<DncSharePermissionOption> ContactPermissionOptions =
    [
        new() { Value = "Read", Label = "View only" },
        new() { Value = "ReadWrite", Label = "Can edit" },
    ];

    private IEnumerable<ContactDto> VisibleContacts => _activeSection switch
    {
        "recent" => _contacts.OrderByDescending(c => c.CreatedAt).Take(20),
        _ => _contacts
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var collapsed = await JSRuntime.InvokeAsync<string>("localStorage.getItem", new object?[] {
"dotnetcloud.sidebar:contacts" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
            {
                _sidebarCollapsed = parsed;
            }
        }
        catch
        {
            // localStorage unavailable
        }

        await ReloadAsync();
        await HandleContactDeepLinkAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle contactId changes when already on the page (same-page navigation).
        await HandleContactDeepLinkAsync();
    }

    /// <summary>
    /// Opens the contact referenced by the <c>ContactId</c> deep-link parameter when it is a new,
    /// well-formed id. Repeat handling of the same id is guarded, and a bad id never breaks
    /// the page.
    /// </summary>
    private async Task HandleContactDeepLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(ContactId) || !Guid.TryParse(ContactId, out var contactId) || contactId ==
_lastHandledContactId)
        {
            return;
        }

        _lastHandledContactId = contactId;
        try
        {
            await SelectContactAsync(contactId);
        }
        catch (Exception)
        {
            // A stale or inaccessible contact id must not break the page.
        }
    }

    private async Task ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        await SaveSidebarCollapsedStateAsync();
    }

    private async Task SaveSidebarCollapsedStateAsync()
    {
        try
        {
            await JSRuntime.InvokeAsync<object?>("localStorage.setItem", new object?[] { "dotnetcloud.sidebar:contacts",
_sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch
        {
            // localStorage unavailable
        }
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;
        try
        {
            _contacts.Clear();
            _contacts.AddRange(await ApiClient.ListContactsAsync(_searchQuery, 0, 100));

            _groups.Clear();
            _groups.AddRange(await ApiClient.ListGroupsAsync());

            if (_selectedContact is not null)
            {
                _selectedContact = _contacts.FirstOrDefault(c => c.Id == _selectedContact.Id);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectContactAsync(Guid contactId)
    {
        _avatarFailed = false;
        _selectedContact = await ApiClient.GetContactAsync(contactId);
        _related = _selectedContact is null
            ? new ContactRelatedEntitiesDto()
            : await ApiClient.GetRelatedAsync(_selectedContact.Id);
        _editingContact = null;
    }

    /// <summary>
    /// Hides the avatar when it fails to load.
    /// </summary>
    /// <remarks>
    /// Replaces the previous inline <c>onerror=""</c> attribute, which the hardened CSP blocks
    /// (no <c>'unsafe-inline'</c>/<c>'unsafe-hashes'</c> for scripts).
    /// </remarks>
    private void OnAvatarError()
    {
        _avatarFailed = true;
    }

    private void BeginCreateContact()
    {
        _selectedContact = null;
        _editingContact = new ContactEditorModel();
    }

    private void BeginEditSelected()
    {
        if (_selectedContact is null)
        {
            return;
        }

        _editingContact = ContactEditorModel.From(_selectedContact);
    }

    private async Task SaveContactAsync()
    {
        if (_editingContact is null)
        {
            return;
        }

        if (_editingContact.Id is null)
        {
            _selectedContact = await ApiClient.CreateContactAsync(_editingContact.ToCreateDto());
        }
        else
        {
            _selectedContact = await ApiClient.UpdateContactAsync(_editingContact.Id.Value, _editingContact.ToUpdateDto());
        }

        _editingContact = null;
        await ReloadAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_editingContact?.Id is not Guid id)
        {
            return;
        }

        await ApiClient.DeleteContactAsync(id);
        _editingContact = null;
        _selectedContact = null;
        _related = new ContactRelatedEntitiesDto();
        await ReloadAsync();
    }

    private void CancelEdit()
    {
        _editingContact = null;
    }

    private async Task ChangeSection(string section)
    {
        _activeSection = section;
        await ReloadAsync();
    }

    private async Task OpenShareDialogAsync()
    {
        if (_selectedContact is null)
            return;

        _showShareDialog = true;
        _shareDialogShares = [];
        _isLoadingShareDialogShares = true;
        StateHasChanged();

        try
        {
            await LoadShareDialogEntriesAsync();
        }
        finally
        {
            _isLoadingShareDialogShares = false;
        }
    }

    private void CloseShareDialogAsync()
    {
        _showShareDialog = false;
    }

    private async Task LoadShareDialogEntriesAsync()
    {
        if (_selectedContact is null)
            return;

        var shares = await ApiClient.ListSharesAsync(_selectedContact.Id);
        _shareDialogShares = await MapShareDtosToEntriesAsync(shares);
    }

    private async Task HandleShareCreatedAsync(DncShareCreatedEventArgs args)
    {
        if (_selectedContact is null)
            return;

        var perm = args.Permission == "ReadWrite" ? ContactSharePermission.ReadWrite : ContactSharePermission.ReadOnly;
        Guid? targetUserId = args.ShareType == "Team" ? null : args.TargetId;
        Guid? targetTeamId = args.ShareType == "Team" ? args.TargetId : null;

        var result = await ApiClient.ShareContactAsync(_selectedContact.Id, targetUserId, targetTeamId, perm.ToString());
        if (result is not null)
        {
            await LoadShareDialogEntriesAsync();
        }
    }

    private async Task HandleShareRemovedAsync(Guid shareId)
    {
        await ApiClient.RevokeShareAsync(shareId);
        _shareDialogShares = [.. _shareDialogShares.Where(e => e.ShareId != shareId)];
    }

    private async Task<List<DncShareEntry>> MapShareDtosToEntriesAsync(IReadOnlyList<ContactShareResponse> shares)
    {
        var userNames = await ResolveUserNamesAsync(shares);
        var entries = new List<DncShareEntry>(shares.Count);

        foreach (var share in shares)
        {
            var isTeam = share.SharedWithTeamId is not null;
            string recipientName;

            if (isTeam)
            {
                recipientName = await ResolveTeamNameAsync(share.SharedWithTeamId!.Value)
                    ?? $"{share.SharedWithTeamId.Value:N}"[..8];
            }
            else if (share.SharedWithUserId is { } sharedWithUserId)
            {
                recipientName = userNames.TryGetValue(sharedWithUserId, out var name)
                    ? name
                    : $"{sharedWithUserId:N}"[..8];
            }
            else
            {
                recipientName = "Unknown";
            }

            entries.Add(new DncShareEntry
            {
                ShareId = share.Id,
                RecipientName = recipientName,
                RecipientType = isTeam ? "Team" : "User",
                Permission = share.Permission == ContactSharePermission.ReadWrite ? "ReadWrite" : "Read",
                CreatedAt = share.CreatedAt
            });
        }

        return entries;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(IReadOnlyList<ContactShareResponse> shares)
    {
        var ids = shares
            .Where(s => s.SharedWithTeamId is null && s.SharedWithUserId is not null)
            .Select(s => s.SharedWithUserId!.Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await UserDirectory.GetDisplayNamesAsync(ids);
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private async Task<string?> ResolveTeamNameAsync(Guid teamId)
    {
        try
        {
            var team = await TeamDirectory.GetTeamAsync(teamId);
            return team?.Name;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ContactEditorModel
    {
        public Guid? Id { get; set; }
        public ContactType ContactType { get; set; } = ContactType.Person;
        public string DisplayName { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Organization { get; set; }
        public string? JobTitle { get; set; }
        public string? PrimaryEmail { get; set; }
        public string? PrimaryPhone { get; set; }

        public static ContactEditorModel From(ContactDto dto)
        {
            return new ContactEditorModel
            {
                Id = dto.Id,
                ContactType = dto.ContactType,
                DisplayName = dto.DisplayName,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Organization = dto.Organization,
                JobTitle = dto.JobTitle,
                PrimaryEmail = dto.Emails.FirstOrDefault()?.Address,
                PrimaryPhone = dto.PhoneNumbers.FirstOrDefault()?.Number
            };
        }

        public CreateContactDto ToCreateDto()
        {
            return new CreateContactDto
            {
                ContactType = ContactType,
                DisplayName = DisplayName,
                FirstName = FirstName,
                LastName = LastName,
                Organization = Organization,
                JobTitle = JobTitle,
                Emails = string.IsNullOrWhiteSpace(PrimaryEmail)
                    ? []
                    : [new ContactEmailDto { Address = PrimaryEmail!, Label = "work", IsPrimary = true }],
                PhoneNumbers = string.IsNullOrWhiteSpace(PrimaryPhone)
                    ? []
                    : [new ContactPhoneDto { Number = PrimaryPhone!, Label = "mobile", IsPrimary = true }]
            };
        }

        public UpdateContactDto ToUpdateDto()
        {
            return new UpdateContactDto
            {
                DisplayName = DisplayName,
                FirstName = FirstName,
                LastName = LastName,
                Organization = Organization,
                JobTitle = JobTitle,
                Emails = string.IsNullOrWhiteSpace(PrimaryEmail)
                    ? []
                    : [new ContactEmailDto { Address = PrimaryEmail!, Label = "work", IsPrimary = true }],
                PhoneNumbers = string.IsNullOrWhiteSpace(PrimaryPhone)
                    ? []
                    : [new ContactPhoneDto { Number = PrimaryPhone!, Label = "mobile", IsPrimary = true }]
            };
        }
    }

    private static IReadOnlyList<CrossModuleLinkRequest> BuildEventRequests(IReadOnlyList<CalendarEventSummaryDto> events)
    {
        return events
            .Select(e => new CrossModuleLinkRequest
            {
                LinkType = CrossModuleLinkType.CalendarEvent,
                TargetId = e.Id
            })
            .ToList();
    }

    private static IReadOnlyList<CrossModuleLinkRequest> BuildNoteRequests(IReadOnlyList<NoteSummaryDto> notes)
    {
        return notes
            .Select(n => new CrossModuleLinkRequest
            {
                LinkType = CrossModuleLinkType.Note,
                TargetId = n.Id
            })
            .ToList();
    }
}
