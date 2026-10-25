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

/// <summary>Code-behind for the <see cref="CollaboraSettings"/> component.</summary>
public partial class CollaboraSettings : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private const string Module = "dotnetcloud.files";

    private bool _loading = true;
    private bool _saving;
    private bool _healthLoading;
    private bool _collaboraHealthy;
    private ConfirmDialog _confirmDialog = default!;

    // Form fields (mirror CollaboraOptions properties)
    private bool _enabled;
    private string _serverUrl = string.Empty;
    private string _wopiBaseUrl = string.Empty;
    private string _tokenSigningKey = string.Empty;
    private int _tokenLifetimeMinutes = 480;
    private bool _enableProofKeyValidation = true;
    private bool _hasTokenSigningKey;
    private int _autoSaveIntervalSeconds = 300;
    private int _maxConcurrentSessions = 20;
    private string _supportedMimeTypes = string.Empty;
    private bool _useBuiltInCollabora;
    private string _collaboraInstallDirectory = string.Empty;
    private string _collaboraExecutablePath = string.Empty;
    private int _collaboraMaxRestartAttempts = 5;
    private string _proxySnippetType = "nginx";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadSettingsAsync(), RefreshHealthAsync());
    }

    private async Task LoadSettingsAsync()
    {
        _loading = true;
        try
        {
            // Show what is actually in force: the server-configured Files:Collabora:* values with
            // any administrator edits already layered on top. The page used to read its own
            // SystemSettings rows, which no Collabora code path ever consumed.
            var effective = await Api.GetCollaboraEffectiveSettingsAsync();
            if (effective is null)
            {
                Toast.ShowError("The Files module is not reachable, so the Collabora settings could not be read.");
                return;
            }

            PopulateFields(effective);
        }
        catch
        {
            Toast.ShowError("Failed to load Collabora settings.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateFields(CollaboraEffectiveSettingsDto effective)
    {
        _enabled = effective.Enabled;
        _serverUrl = effective.ServerUrl;
        _wopiBaseUrl = effective.WopiBaseUrl;

        // The stored secret is never sent to the browser: leave the box empty to keep the current key.
        _tokenSigningKey = string.Empty;
        _hasTokenSigningKey = effective.HasTokenSigningKey;

        _tokenLifetimeMinutes = effective.TokenLifetimeMinutes;
        _enableProofKeyValidation = effective.EnableProofKeyValidation;
        _autoSaveIntervalSeconds = effective.AutoSaveIntervalSeconds;
        _maxConcurrentSessions = effective.MaxConcurrentSessions;
        _supportedMimeTypes = effective.SupportedMimeTypes;
        _useBuiltInCollabora = effective.UseBuiltInCollabora;
        _collaboraInstallDirectory = effective.CollaboraInstallDirectory;
        _collaboraExecutablePath = effective.CollaboraExecutablePath;
        _collaboraMaxRestartAttempts = effective.CollaboraMaxRestartAttempts;
    }

    private async Task RefreshHealthAsync()
    {
        _healthLoading = true;
        try
        {
            var health = await Api.GetHealthAsync();
            _collaboraHealthy = health?.Entries
                .TryGetValue("collabora_online", out var entry) == true &&
                string.Equals(entry.Status, "Healthy", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _collaboraHealthy = false;
        }
        finally
        {
            _healthLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        _saving = true;
        try
        {
            var tasks = new List<Task<bool>>
            {
                Api.UpsertSettingAsync(Module, "Collabora:Enabled", Str(_enabled.ToString())),
                Api.UpsertSettingAsync(Module, "Collabora:ServerUrl", Str(_serverUrl)),
                Api.UpsertSettingAsync(Module, "Collabora:WopiBaseUrl", Str(_wopiBaseUrl)),
                Api.UpsertSettingAsync(Module, "Collabora:TokenLifetimeMinutes", Str(_tokenLifetimeMinutes.ToString())),
                Api.UpsertSettingAsync(Module, "Collabora:EnableProofKeyValidation", Str(_enableProofKeyValidation.ToString())),
                Api.UpsertSettingAsync(Module, "Collabora:AutoSaveIntervalSeconds", Str(_autoSaveIntervalSeconds.ToString())),
                Api.UpsertSettingAsync(Module, "Collabora:MaxConcurrentSessions", Str(_maxConcurrentSessions.ToString())),
                Api.UpsertSettingAsync(Module, "Collabora:SupportedMimeTypes", Str(_supportedMimeTypes)),
                Api.UpsertSettingAsync(Module, "Collabora:UseBuiltInCollabora", Str(_useBuiltInCollabora.ToString())),
                Api.UpsertSettingAsync(Module, "Collabora:CollaboraInstallDirectory", Str(_collaboraInstallDirectory)),
                Api.UpsertSettingAsync(Module, "Collabora:CollaboraExecutablePath", Str(_collaboraExecutablePath)),
                Api.UpsertSettingAsync(Module, "Collabora:CollaboraMaxRestartAttempts", Str(_collaboraMaxRestartAttempts.ToString()))
            };

            // Only write the signing key when the administrator typed a new one: an empty box means
            // "keep what is in force", not "clear the key".
            if (!string.IsNullOrWhiteSpace(_tokenSigningKey))
            {
                tasks.Add(Api.UpsertSettingAsync(Module, "Collabora:TokenSigningKey",
                    new UpsertSystemSettingDto { Value = _tokenSigningKey, IsSensitive = true }));
            }

            var results = await Task.WhenAll(tasks);
            if (results.All(r => r))
                Toast.ShowSuccess("Collabora settings saved and applied.");
            else
                Toast.ShowError("Some settings could not be saved.");
        }
        catch
        {
            Toast.ShowError("Failed to save Collabora settings.");
        }
        finally
        {
            _saving = false;
        }
    }

    private string GetProxySnippet() => _proxySnippetType switch
    {
        "apache" => GenerateApacheSnippet(),
        _ => GenerateNginxSnippet()
    };

    private string GenerateNginxSnippet()
    {
        var upstream = string.IsNullOrWhiteSpace(_serverUrl)
            ? "127.0.0.1:9980"
            : ExtractHostPort(_serverUrl);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Add these location blocks to your nginx server { } block:");
        sb.AppendLine();
        sb.AppendLine("location /hosting/discovery {");
        sb.AppendLine("    proxy_pass https://" + upstream + ";");
        sb.AppendLine("    proxy_set_header Host $host;");
        sb.AppendLine("    proxy_set_header X-Forwarded-Proto $scheme;");
        sb.AppendLine("}");
        sb.AppendLine("location /browser {");
        sb.AppendLine("    proxy_pass https://" + upstream + ";");
        sb.AppendLine("    proxy_set_header Host $host;");
        sb.AppendLine("    proxy_set_header X-Forwarded-Proto $scheme;");
        sb.AppendLine("}");
        sb.AppendLine("location ~ ^/cool/(.*)/ws$ {");
        sb.AppendLine("    proxy_pass https://" + upstream + ";");
        sb.AppendLine("    proxy_http_version 1.1;");
        sb.AppendLine("    proxy_set_header Upgrade $http_upgrade;");
        sb.AppendLine("    proxy_set_header Connection \"upgrade\";");
        sb.AppendLine("    proxy_set_header Host $host;");
        sb.AppendLine("    proxy_read_timeout 3600s;");
        sb.AppendLine("}");
        sb.AppendLine("location ^~ /cool {");
        sb.AppendLine("    proxy_pass https://" + upstream + ";");
        sb.AppendLine("    proxy_set_header Host $host;");
        sb.AppendLine("    proxy_set_header X-Forwarded-Proto $scheme;");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateApacheSnippet()
    {
        var upstream = string.IsNullOrWhiteSpace(_serverUrl)
            ? "https://127.0.0.1:9980"
            : _serverUrl.TrimEnd('/');

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Add inside your <VirtualHost> block (requires mod_proxy_wstunnel):");
        sb.AppendLine();
        sb.AppendLine("ProxyPass /hosting/ " + upstream + "/hosting/");
        sb.AppendLine("ProxyPassReverse /hosting/ " + upstream + "/hosting/");
        sb.AppendLine("ProxyPass /browser " + upstream + "/browser");
        sb.AppendLine("ProxyPassReverse /browser " + upstream + "/browser");
        sb.AppendLine();
        sb.AppendLine("RewriteEngine On");
        sb.AppendLine("RewriteCond %{HTTP:Upgrade} websocket [NC]");
        sb.AppendLine("RewriteRule ^/cool/(.*)/ws$ wss://" + ExtractHostPort(upstream) + "/cool/$1/ws [P,L]");
        sb.AppendLine();
        sb.AppendLine("ProxyPass /cool " + upstream + "/cool");
        sb.AppendLine("ProxyPassReverse /cool " + upstream + "/cool");
        return sb.ToString();
    }

    private static string ExtractHostPort(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        return url;
    }

    // ---- Setting helpers ----

    private static string GetString(IReadOnlyList<SystemSettingDto> settings, string key, string defaultValue = "")
    {
        var s = settings.FirstOrDefault(x => x.Key == key);
        return s?.Value ?? defaultValue;
    }

    private static bool GetBool(IReadOnlyList<SystemSettingDto> settings, string key, bool defaultValue = false)
    {
        var val = GetString(settings, key);
        return string.IsNullOrEmpty(val) ? defaultValue : bool.TryParse(val, out var b) ? b : defaultValue;
    }

    private static int GetInt(IReadOnlyList<SystemSettingDto> settings, string key, int defaultValue = 0)
    {
        var val = GetString(settings, key);
        return string.IsNullOrEmpty(val) ? defaultValue : int.TryParse(val, out var i) ? i : defaultValue;
    }

    private static UpsertSystemSettingDto Str(string value, string? description = null) =>
        new() { Value = value, Description = description };
}
