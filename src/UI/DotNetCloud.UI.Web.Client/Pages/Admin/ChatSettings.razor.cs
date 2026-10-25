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

/// <summary>Code-behind for the <see cref="ChatSettings"/> component.</summary>
public partial class ChatSettings : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private const string Module = "dotnetcloud.chat";

    private bool _loading = true;
    private bool _saving;
    private bool _sweeping;
    private string? _errorMessage;
    private string? _successMessage;
    private string? _validationMessage;

    private ChatEffectiveSettingsDto? _effective;
    private ChatRetentionSweepResultDto? _lastSweep;

    // Message limits
    private int _maxMessageLength = 10000;
    private int _maxMessagesPerChannel;

    // Attachment limits
    private int _maxAttachmentsPerMessage = 10;
    private int _maxAttachmentSizeMb = 10;
    private int _maxAttachmentsPerChannel;
    private int _maxAttachmentStoragePerChannelMb;

    // Retention & archiving
    private bool _retentionEnabled;
    private int _messageLifetimeDays;
    private string _retentionMode = "Archive";
    private string _archivePath = string.Empty;
    private bool _archiveAttachments = true;
    private int _sweepIntervalMinutes = 60;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _errorMessage = null;

        try
        {
            var settings = await Api.ListSettingsAsync(Module);
            PopulateFields(settings);
            _effective = await Api.GetChatEffectiveSettingsAsync();
        }
        catch
        {
            _errorMessage = "Failed to load chat settings.";
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateFields(IReadOnlyList<SystemSettingDto> settings)
    {
        _maxMessageLength = GetInt(settings, "Limits:MaxMessageLength", 10000);
        _maxMessagesPerChannel = GetInt(settings, "Limits:MaxMessagesPerChannel", 0);
        _maxAttachmentsPerMessage = GetInt(settings, "Limits:MaxAttachmentsPerMessage", 10);
        _maxAttachmentSizeMb = GetInt(settings, "Limits:MaxAttachmentSizeMb", 10);
        _maxAttachmentsPerChannel = GetInt(settings, "Limits:MaxAttachmentsPerChannel", 0);
        _maxAttachmentStoragePerChannelMb = GetInt(settings, "Limits:MaxAttachmentStoragePerChannelMb", 0);
        _retentionEnabled = GetBool(settings, "Retention:Enabled");
        _messageLifetimeDays = GetInt(settings, "Retention:MessageLifetimeDays", 0);
        _retentionMode = NormalizeMode(GetString(settings, "Retention:Mode"));
        _archivePath = GetString(settings, "Retention:ArchivePath");
        _archiveAttachments = GetBool(settings, "Retention:ArchiveAttachments", defaultValue: true);
        _sweepIntervalMinutes = GetInt(settings, "Retention:SweepIntervalMinutes", 60);
    }

    private async Task SaveAsync()
    {
        _saving = true;
        _successMessage = null;
        _validationMessage = Validate();

        if (_validationMessage is not null)
        {
            _saving = false;
            return;
        }

        try
        {
            var tasks = new[]
            {
                Api.UpsertSettingAsync(Module, "Limits:MaxMessageLength",
                    Str(_maxMessageLength.ToString(), "Maximum number of characters in a chat message")),
                Api.UpsertSettingAsync(Module, "Limits:MaxMessagesPerChannel",
                    Str(_maxMessagesPerChannel.ToString(), "Maximum live messages retained per channel (0 = unlimited)")),
                Api.UpsertSettingAsync(Module, "Limits:MaxAttachmentsPerMessage",
                    Str(_maxAttachmentsPerMessage.ToString(), "Maximum number of attachments on a single chat message")),
                Api.UpsertSettingAsync(Module, "Limits:MaxAttachmentSizeMb",
                    Str(_maxAttachmentSizeMb.ToString(), "Maximum size of a single chat attachment in megabytes")),
                Api.UpsertSettingAsync(Module, "Limits:MaxAttachmentsPerChannel",
                    Str(_maxAttachmentsPerChannel.ToString(), "Maximum number of attachments per channel (0 = unlimited)")),
                Api.UpsertSettingAsync(Module, "Limits:MaxAttachmentStoragePerChannelMb",
                    Str(_maxAttachmentStoragePerChannelMb.ToString(), "Maximum total attachment storage per channel in megabytes (0 = unlimited)")),
                Api.UpsertSettingAsync(Module, "Retention:Enabled",
                    Str(_retentionEnabled.ToString(), "When true, the background sweep archives or purges expired messages")),
                Api.UpsertSettingAsync(Module, "Retention:MessageLifetimeDays",
                    Str(_messageLifetimeDays.ToString(), "Age in days after which chat messages expire (0 = keep forever)")),
                Api.UpsertSettingAsync(Module, "Retention:Mode",
                    Str(_retentionMode, "What happens to expired messages: 'Archive' (hide but keep) or 'Purge' (permanently delete)")),
                Api.UpsertSettingAsync(Module, "Retention:ArchiveAttachments",
                    Str(_archiveAttachments.ToString(), "When true, attachments of archived messages are retained")),
                Api.UpsertSettingAsync(Module, "Retention:ArchivePath",
                    Str(_archivePath.Trim(), "Filesystem folder archived messages are exported to before their rows are deleted (blank = default under the server data directory)")),
                Api.UpsertSettingAsync(Module, "Retention:SweepIntervalMinutes",
                    Str(_sweepIntervalMinutes.ToString(), "How often the chat retention sweep runs, in minutes"))
            };

            var results = await Task.WhenAll(tasks);
            if (results.All(r => r))
            {
                _successMessage = "Chat settings saved.";
                Toast.ShowSuccess("Chat settings saved.");
                _effective = await Api.GetChatEffectiveSettingsAsync();
            }
            else
            {
                _errorMessage = "Some settings could not be saved.";
                Toast.ShowError(_errorMessage);
            }
        }
        catch
        {
            _errorMessage = "Failed to save chat settings.";
            Toast.ShowError(_errorMessage);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task RunSweepAsync()
    {
        _sweeping = true;
        _errorMessage = null;

        try
        {
            var result = await Api.RunChatRetentionSweepAsync();
            if (result is null)
            {
                _errorMessage = "The Chat module is not reachable, so the sweep could not be run.";
            }
            else
            {
                _lastSweep = result;
            }
        }
        catch
        {
            _errorMessage = "Failed to run the retention sweep.";
        }
        finally
        {
            _sweeping = false;
        }
    }

    /// <summary>Validates the form, returning a message on the first problem found.</summary>
    private string? Validate()
    {
        if (_maxMessageLength < 1 || _maxMessageLength > 10000)
            return "Maximum message length must be between 1 and 10000 characters.";

        if (_maxMessagesPerChannel < 0)
            return "Maximum messages per channel cannot be negative (use 0 for unlimited).";

        if (_maxAttachmentsPerMessage < 1 || _maxAttachmentsPerMessage > 100)
            return "Maximum attachments per message must be between 1 and 100.";

        if (_maxAttachmentSizeMb < 1 || _maxAttachmentSizeMb > 64)
            return "Maximum attachment size must be between 1 and 64 MB.";

        if (_maxAttachmentsPerChannel < 0)
            return "Maximum attachments per channel cannot be negative (use 0 for unlimited).";

        if (_maxAttachmentStoragePerChannelMb < 0)
            return "Maximum attachment storage per channel cannot be negative (use 0 for unlimited).";

        if (_maxAttachmentStoragePerChannelMb > 0
            && _maxAttachmentsPerChannel == 1
            && _maxAttachmentStoragePerChannelMb < _maxAttachmentSizeMb)
        {
            return "Attachment storage per channel is smaller than the maximum size of a single attachment.";
        }

        if (_messageLifetimeDays < 0)
            return "Message lifetime cannot be negative (use 0 to keep messages forever).";

        if (_sweepIntervalMinutes < 1 || _sweepIntervalMinutes > 1440)
            return "The sweep interval must be between 1 and 1440 minutes.";

        if (_retentionEnabled && _messageLifetimeDays == 0 && _maxMessagesPerChannel == 0
            && _maxAttachmentsPerChannel == 0 && _maxAttachmentStoragePerChannelMb == 0)
        {
            return "Automatic retention is enabled but nothing will expire: set a message lifetime, " +
                   "a maximum number of messages per channel, or a per-channel attachment ceiling.";
        }

        var archivePath = _archivePath.Trim();
        if (_retentionMode == "Archive" && archivePath.Length > 0 && !Path.IsPathRooted(archivePath))
        {
            return "The archive location must be an absolute path (for example " +
                   "/var/lib/dotnetcloud/storage/chat-archive).";
        }

        return null;
    }

    // ---- Setting helpers ----

    private static string GetString(IReadOnlyList<SystemSettingDto> settings, string key, string defaultValue = "")
    {
        var s = settings.FirstOrDefault(x => x.Key == key);
        return string.IsNullOrWhiteSpace(s?.Value) ? defaultValue : s!.Value;
    }

    private static int GetInt(IReadOnlyList<SystemSettingDto> settings, string key, int defaultValue)
        => int.TryParse(GetString(settings, key), out var value) ? value : defaultValue;

    private static bool GetBool(IReadOnlyList<SystemSettingDto> settings, string key, bool defaultValue = false)
    {
        var value = GetString(settings, key);
        return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string NormalizeMode(string value)
        => string.Equals(value, "Purge", StringComparison.OrdinalIgnoreCase) ? "Purge" : "Archive";

    private static UpsertSystemSettingDto Str(string value, string? description = null) =>
        new() { Value = value, Description = description };
}
