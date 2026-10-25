using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>Code-behind for the <see cref="Groups"/> component.</summary>
public partial class Groups : ComponentBase, IDisposable
{
    private bool _loading = true;
    private bool _showCreate;
    private bool _showEdit;
    private bool _showMembers;
    private bool _showAddMember;
    private bool _saving;
    private bool _membersLoading;
    private bool _candidateUsersLoading;
    private string _error = string.Empty;
    private string _modalError = string.Empty;
    private string _membersError = string.Empty;
    private Guid _selectedOrganizationId;
    private Guid _createOrganizationId;
    private Guid _editId;
    private Guid _membersGroupId;
    private string _createName = string.Empty;
    private string? _createDescription;
    private string _editName = string.Empty;
    private string? _editDescription;
    private string _membersGroupName = string.Empty;
    private bool _membersGroupIsBuiltIn;
    private string _addMemberSearch = string.Empty;
    private IReadOnlyList<OrganizationDto> _orgs = [];
    private IReadOnlyList<GroupDto> _groups = [];
    private IReadOnlyList<GroupMemberDto> _members = [];
    private IReadOnlyList<UserDto> _candidateUsers = [];
    private ConfirmDialog _confirmDialog = default!;
    private System.Timers.Timer? _searchDebounce;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    public void Dispose()
    {
        _searchDebounce?.Stop();
        _searchDebounce?.Dispose();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _error = string.Empty;

        try
        {
            _orgs = await Api.ListOrganizationsAsync();
            if (_orgs.Count > 0)
            {
                if (_selectedOrganizationId == Guid.Empty || _orgs.All(org => org.Id != _selectedOrganizationId))
                {
                    _selectedOrganizationId = _orgs[0].Id;
                }

                _createOrganizationId = _selectedOrganizationId;
                await LoadGroupsAsync();
            }
            else
            {
                _groups = [];
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadGroupsAsync()
    {
        if (_selectedOrganizationId == Guid.Empty)
        {
            _groups = [];
            return;
        }

        _groups = await Api.ListGroupsAsync(_selectedOrganizationId);
    }

    private async Task OnOrganizationChanged(ChangeEventArgs args)
    {
        if (!Guid.TryParse(args.Value?.ToString(), out var organizationId))
            return;

        _selectedOrganizationId = organizationId;
        _createOrganizationId = organizationId;
        _error = string.Empty;

        try
        {
            await LoadGroupsAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    private void OnCreateOrganizationChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var organizationId))
        {
            _createOrganizationId = organizationId;
        }
    }

    private void ShowCreate()
    {
        _createOrganizationId = _selectedOrganizationId;
        _createName = string.Empty;
        _createDescription = null;
        _modalError = string.Empty;
        _showCreate = true;
    }

    private void CloseCreate()
    {
        _showCreate = false;
        _modalError = string.Empty;
    }

    private async Task CreateAsync()
    {
        if (_createOrganizationId == Guid.Empty)
        {
            _modalError = "Select an organization first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_createName))
        {
            _modalError = "Name is required.";
            return;
        }

        _saving = true;
        _modalError = string.Empty;

        try
        {
            await Api.CreateGroupAsync(new CreateGroupDto
            {
                OrganizationId = _createOrganizationId,
                Name = _createName.Trim(),
                Description = string.IsNullOrWhiteSpace(_createDescription) ? null : _createDescription.Trim(),
            });

            _selectedOrganizationId = _createOrganizationId;
            CloseCreate();
            await LoadGroupsAsync();
        }
        catch (Exception ex)
        {
            _modalError = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void StartEdit(GroupDto group)
    {
        _editId = group.Id;
        _editName = group.Name;
        _editDescription = group.Description;
        _modalError = string.Empty;
        _showEdit = true;
    }

    private void CloseEdit()
    {
        _showEdit = false;
        _modalError = string.Empty;
    }

    private async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(_editName))
        {
            _modalError = "Name is required.";
            return;
        }

        _saving = true;
        _modalError = string.Empty;

        try
        {
            var success = await Api.UpdateGroupAsync(_editId, new UpdateGroupDto
            {
                Name = _editName.Trim(),
                Description = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription.Trim(),
            });

            if (!success)
            {
                _modalError = "Failed to update group.";
                return;
            }

            CloseEdit();
            await LoadGroupsAsync();
        }
        catch (Exception ex)
        {
            _modalError = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(GroupDto group)
    {
        _error = string.Empty;

        if (!await _confirmDialog.ShowAsync($"Delete group '{group.Name}'?", "Delete Group"))
            return;

        try
        {
            var success = await Api.DeleteGroupAsync(group.Id);
            if (!success)
            {
                _error = "Failed to delete group.";
                return;
            }

            await LoadGroupsAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    private async Task ShowMembersAsync(GroupDto group)
    {
        _membersGroupId = group.Id;
        _membersGroupName = group.Name;
        _membersGroupIsBuiltIn = group.IsAllUsersGroup;
        _membersError = string.Empty;
        _showMembers = true;
        StateHasChanged();
        await LoadMembersAsync();
    }

    private void CloseMembers()
    {
        _showMembers = false;
        _showAddMember = false;
        _membersError = string.Empty;
        _addMemberSearch = string.Empty;
        _candidateUsers = [];
    }

    private async Task LoadMembersAsync()
    {
        _membersLoading = true;

        try
        {
            _members = await Api.ListGroupMembersAsync(_membersGroupId);
        }
        catch (Exception ex)
        {
            _membersError = ex.Message;
        }
        finally
        {
            _membersLoading = false;
        }
    }

    private void ShowAddMember()
    {
        _showAddMember = true;
        _addMemberSearch = string.Empty;
        _candidateUsers = [];
        _membersError = string.Empty;
    }

    private void CloseAddMember()
    {
        _showAddMember = false;
        _addMemberSearch = string.Empty;
        _candidateUsers = [];
        _membersError = string.Empty;
    }

    private async Task SearchUsersAsync()
    {
        _searchDebounce?.Stop();
        _searchDebounce?.Dispose();

        if (string.IsNullOrWhiteSpace(_addMemberSearch))
        {
            _candidateUsers = [];
            return;
        }

        _searchDebounce = new System.Timers.Timer(300)
        {
            AutoReset = false,
        };
        _searchDebounce.Elapsed += async (_, _) =>
        {
            await InvokeAsync(async () =>
            {
                _candidateUsersLoading = true;
                StateHasChanged();

                try
                {
                    var result = await Api.ListUsersAsync(page: 1, pageSize: 50, search: _addMemberSearch, isActive: true);
                    var existingMemberIds = _members.Select(member => member.UserId).ToHashSet();
                    _candidateUsers = result.Items
                        .Where(user => !existingMemberIds.Contains(user.Id))
                        .OrderBy(user => user.DisplayName)
                        .ToList();
                }
                catch (Exception ex)
                {
                    _membersError = ex.Message;
                }
                finally
                {
                    _candidateUsersLoading = false;
                    StateHasChanged();
                }
            });
        };
        _searchDebounce.Start();
    }

    private async Task AddMemberAsync(UserDto user)
    {
        _membersError = string.Empty;
        _saving = true;

        try
        {
            var success = await Api.AddGroupMemberAsync(_membersGroupId, user.Id);
            if (!success)
            {
                _membersError = "Failed to add member.";
                return;
            }

            _candidateUsers = _candidateUsers.Where(candidate => candidate.Id != user.Id).ToList();
            await LoadMembersAsync();
            await LoadGroupsAsync();
        }
        catch (Exception ex)
        {
            _membersError = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task RemoveMemberAsync(GroupMemberDto member)
    {
        _membersError = string.Empty;

        try
        {
            var success = await Api.RemoveGroupMemberAsync(_membersGroupId, member.UserId);
            if (!success)
            {
                _membersError = "Failed to remove member.";
                return;
            }

            await LoadMembersAsync();
            await LoadGroupsAsync();
        }
        catch (Exception ex)
        {
            _membersError = ex.Message;
        }
    }
}
