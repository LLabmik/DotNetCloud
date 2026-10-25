using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using DotNetCloud.Core.Authorization;
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

/// <summary>Code-behind for the <see cref="Organizations"/> component.</summary>
public partial class Organizations : ComponentBase
{
    private bool _loading = true;
    private bool _showCreate;
    private bool _showEdit;
    private bool _showMembers;
    private bool _showAddMember;
    private bool _saving;
    private bool _membersLoading;
    private bool _nonMembersLoading;
    private string _error = "";
    private string _membersError = "";
    private string _createName = "";
    private string? _createDescription;
    private string _editName = "";
    private string? _editDescription;
    private Guid _editId;
    private Guid _membersOrgId;
    private string _membersOrgName = "";
    private string _addMemberSearch = "";
    private IReadOnlyList<OrganizationDto> _orgs = [];
    private IReadOnlyList<OrganizationMemberDto> _members = [];
    private IReadOnlyList<OrganizationMemberDto> _nonMembers = [];
    private Guid? _savingRolesFor;
    private List<Guid> _addMemberRoleIds = new() { OrgRoleIds.OrgMember };
    private System.Timers.Timer? _searchDebounce;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _orgs = await Api.ListOrganizationsAsync();
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

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(_createName))
        {
            _error = "Name is required.";
            return;
        }

        _saving = true;
        _error = "";
        try
        {
            await Api.CreateOrganizationAsync(new CreateOrganizationDto
            {
                Name = _createName.Trim(),
                Description = string.IsNullOrWhiteSpace(_createDescription) ? null : _createDescription.Trim()
            });
            _showCreate = false;
            _createName = "";
            _createDescription = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void StartEdit(OrganizationDto org)
    {
        _editId = org.Id;
        _editName = org.Name;
        _editDescription = org.Description;
        _error = "";
        _showEdit = true;
    }

    private async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(_editName))
        {
            _error = "Name is required.";
            return;
        }

        _saving = true;
        _error = "";
        try
        {
            await Api.UpdateOrganizationAsync(_editId, new UpdateOrganizationDto
            {
                Name = _editName.Trim(),
                Description = _editDescription?.Trim()
            });
            _showEdit = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(OrganizationDto org)
    {
        _error = "";
        try
        {
            var success = await Api.DeleteOrganizationAsync(org.Id);
            if (success)
            {
                await LoadAsync();
            }
            else
            {
                _error = "Failed to delete organization.";
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    private async Task ShowMembersAsync(OrganizationDto org)
    {
        _membersOrgId = org.Id;
        _membersOrgName = org.Name;
        _membersError = "";
        _showMembers = true;
        await LoadMembersAsync();
    }

    private async Task LoadMembersAsync()
    {
        _membersLoading = true;
        try
        {
            _members = await Api.ListOrganizationMembersAsync(_membersOrgId);
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

    private async Task RemoveMemberAsync(OrganizationMemberDto member)
    {
        _membersError = "";
        try
        {
            var success = await Api.RemoveOrganizationMemberAsync(_membersOrgId, member.UserId);
            if (success)
            {
                await LoadMembersAsync();
                await LoadAsync();
            }
            else
            {
                _membersError = "Failed to remove member.";
            }
        }
        catch (Exception ex)
        {
            _membersError = ex.Message;
        }
    }

    private async Task ToggleRoleAsync(OrganizationMemberDto member, Guid roleId, bool add)
    {
        _membersError = "";
        _savingRolesFor = member.UserId;
        try
        {
            var roleIds = member.RoleIds.ToList();
            if (add)
            {
                if (!roleIds.Contains(roleId))
                    roleIds.Add(roleId);
            }
            else
            {
                roleIds.Remove(roleId);
                if (roleIds.Count == 0)
                    roleIds.Add(OrgRoleIds.OrgMember);
            }

            var result = await Api.SetOrgMemberRolesAsync(_membersOrgId, member.UserId, roleIds);
            if (result is not null)
                await LoadMembersAsync();
            else
                _membersError = "Failed to update roles.";
        }
        catch (Exception ex)
        {
            _membersError = ex.Message;
        }
        finally
        {
            _savingRolesFor = null;
        }
    }

    private void ToggleAddRole(Guid roleId, bool add)
    {
        if (add)
        {
            if (!_addMemberRoleIds.Contains(roleId))
                _addMemberRoleIds.Add(roleId);
        }
        else
        {
            _addMemberRoleIds.Remove(roleId);
            if (_addMemberRoleIds.Count == 0)
                _addMemberRoleIds.Add(OrgRoleIds.OrgMember);
        }
    }

    private void ShowAddMember()
    {
        _addMemberSearch = "";
        _nonMembers = [];
        _membersError = "";
        _addMemberRoleIds = new() { OrgRoleIds.OrgMember };
        _showAddMember = true;
    }

    private async Task SearchNonMembersAsync()
    {
        _searchDebounce?.Stop();
        _searchDebounce?.Dispose();

        if (string.IsNullOrWhiteSpace(_addMemberSearch))
        {
            _nonMembers = [];
            return;
        }

        _searchDebounce = new System.Timers.Timer(300);
        _searchDebounce.AutoReset = false;
        _searchDebounce.Elapsed += async (_, _) =>
        {
            await InvokeAsync(async () =>
            {
                _nonMembersLoading = true;
                StateHasChanged();
                try
                {
                    _nonMembers = await Api.ListNonMembersAsync(_membersOrgId, _addMemberSearch);
                }
                catch (Exception ex)
                {
                    _membersError = ex.Message;
                }
                finally
                {
                    _nonMembersLoading = false;
                    StateHasChanged();
                }
            });
        };
        _searchDebounce.Start();
    }

    private async Task AddMemberAsync(OrganizationMemberDto user)
    {
        _membersError = "";
        _saving = true;
        try
        {
            var success = await Api.AddOrganizationMemberAsync(_membersOrgId, user.UserId, _addMemberRoleIds);
            if (success)
            {
                _nonMembers = _nonMembers.Where(u => u.UserId != user.UserId).ToList();
                await LoadMembersAsync();
                await LoadAsync();
            }
            else
            {
                _membersError = "Failed to add member.";
            }
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
}
