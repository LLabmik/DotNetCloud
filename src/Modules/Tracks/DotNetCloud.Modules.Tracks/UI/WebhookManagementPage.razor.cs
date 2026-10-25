using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>Code-behind for the <see cref="WebhookManagementPage"/> component.</summary>
public partial class WebhookManagementPage : ComponentBase
{
    private List<DotNetCloud.Core.Services.ModuleApis.WebhookSubscriptionDto> _subscriptions = [];
    private bool _isLoading = true;
    private bool _showModal;
    private bool _saving;
    private string? _error;
    private DotNetCloud.Core.Services.ModuleApis.WebhookSubscriptionDto? _editingSubscription;

    private string _formUrl = string.Empty;
    private List<string> _formEvents = [];
    private bool _formIsActive = true;

    private string? _testResult;
    private bool _testSuccess;

    private static readonly string[] _availableEvents =
    [
        "work_item.created", "work_item.updated", "work_item.deleted",
        "work_item.moved", "comment.added", "sprint.started", "sprint.completed",
        "milestone.reached"
    ];

    [Parameter] public Guid ProductId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadSubscriptionsAsync();
    }

    private async Task LoadSubscriptionsAsync()
    {
        _isLoading = true;
        try
        {
            _subscriptions = (await ApiClient.ListProductWebhooksAsync(ProductId)).ToList();
        }
        catch { }
        finally { _isLoading = false; }
    }

    private void OpenCreateModal()
    {
        _editingSubscription = null;
        _formUrl = string.Empty;
        _formEvents = [];
        _formIsActive = true;
        _error = null;
        _showModal = true;
    }

    private void OpenEditModal(DotNetCloud.Core.Services.ModuleApis.WebhookSubscriptionDto sub)
    {
        _editingSubscription = sub;
        _formUrl = sub.Url;
        _formEvents = GetEventTypes(sub.EventsJson);
        _formIsActive = sub.IsActive;
        _error = null;
        _showModal = true;
    }

    private void CloseModal()
    {
        _showModal = false;
        _error = null;
    }

    private void ToggleEvent(string eventType, bool isChecked)
    {
        if (isChecked && !_formEvents.Contains(eventType))
            _formEvents.Add(eventType);
        else if (!isChecked)
            _formEvents.Remove(eventType);
    }

    private async Task SaveWebhook()
    {
        if (string.IsNullOrWhiteSpace(_formUrl) || _formEvents.Count == 0)
        {
            _error = "URL and at least one event type are required.";
            return;
        }

        _saving = true;
        _error = null;

        try
        {
            if (_editingSubscription is not null)
            {
                await ApiClient.UpdateProductWebhookAsync(ProductId, _editingSubscription.Id, _formUrl, _formEvents, _formIsActive);
            }
            else
            {
                await ApiClient.CreateProductWebhookAsync(ProductId, _formUrl, _formEvents);
            }
            _showModal = false;
            await LoadSubscriptionsAsync();
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

    private async Task DeleteWebhook(DotNetCloud.Core.Services.ModuleApis.WebhookSubscriptionDto sub)
    {
        try
        {
            await ApiClient.DeleteProductWebhookAsync(ProductId, sub.Id);
            await LoadSubscriptionsAsync();
        }
        catch { }
    }

    private async Task TestWebhook(DotNetCloud.Core.Services.ModuleApis.WebhookSubscriptionDto sub)
    {
        try
        {
            var result = await ApiClient.TestProductWebhookAsync(ProductId, sub.Id);
            _testSuccess = result.Success;
            _testResult = result.Success
                ? $"✅ Test ping succeeded — HTTP {result.StatusCode} in {result.DurationMs}ms"
                : $"❌ Test ping failed — {result.Error ?? $"HTTP {result.StatusCode}"}";
        }
        catch (Exception ex)
        {
            _testSuccess = false;
            _testResult = $"❌ Test ping error: {ex.Message}";
        }

        StateHasChanged();
        await Task.Delay(5000);
        _testResult = null;
        StateHasChanged();
    }

    private static List<string> GetEventTypes(string eventsJson)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(eventsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string TruncateUrl(string url)
    {
        if (url.Length <= 60) return url;
        return url[..30] + "..." + url[^27..];
    }

    private static string GetStatusClass(int? statusCode) => statusCode switch
    {
        >= 200 and < 300 => "wh-status-active",
        null => "wh-status-inactive",
        _ => "wh-failures"
    };
}
