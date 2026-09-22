using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Resolves <see cref="ChatSettings"/> from the core system settings database
/// (module <c>dotnetcloud.chat</c>) with fallback to <c>Chat:*</c> configuration values.
/// </summary>
/// <remarks>
/// <para>
/// The admin setting is the source of truth; configuration is only consulted for keys that
/// have no database row. The lazy <see cref="IAdminSettingsService"/> resolution keeps the
/// provider usable in a standalone module host where the core database is not registered.
/// </para>
/// <para>
/// Resolved values are cached for a short window so a burst of message sends does not issue
/// one settings read per message, while an admin change still takes effect promptly. Blazor
/// circuits can hold a DI scope for their whole lifetime, so the cache is time-based rather
/// than bound to the scope.
/// </para>
/// </remarks>
public sealed class ChatSettingsProvider : IChatSettingsProvider
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatSettingsProvider> _logger;
    private readonly TimeSpan _cacheTtl;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ChatSettings? _cached;
    private DateTime _cachedAtUtc;

    private IAdminSettingsService? _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSettingsProvider"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source for fallback values.</param>
    /// <param name="serviceProvider">Service provider used to optionally resolve <see cref="IAdminSettingsService"/>.</param>
    /// <param name="logger">Logger instance.</param>
    public ChatSettingsProvider(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<ChatSettingsProvider> logger)
        : this(configuration, serviceProvider, logger, DefaultCacheTtl)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSettingsProvider"/> class with an explicit cache window.
    /// </summary>
    /// <param name="configuration">Configuration source for fallback values.</param>
    /// <param name="serviceProvider">Service provider used to optionally resolve <see cref="IAdminSettingsService"/>.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cacheTtl">How long resolved settings are cached; <see cref="TimeSpan.Zero"/> disables caching.</param>
    internal ChatSettingsProvider(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<ChatSettingsProvider> logger,
        TimeSpan cacheTtl)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _cacheTtl = cacheTtl;
    }

    /// <inheritdoc />
    public async Task<ChatSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cached;
        if (cached is not null && !IsExpired())
            return cached;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cached;
            if (cached is not null && !IsExpired())
                return cached;

            var resolved = await LoadAsync(cancellationToken).ConfigureAwait(false);
            _cached = resolved;
            _cachedAtUtc = DateTime.UtcNow;
            return resolved;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        _cached = null;
        _cachedAtUtc = default;
    }

    private bool IsExpired() => _cacheTtl <= TimeSpan.Zero || DateTime.UtcNow - _cachedAtUtc >= _cacheTtl;

    private async Task<ChatSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var values = await ReadDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);

        string? Get(string key) => values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        var settings = new ChatSettings
        {
            MaxMessageLength = GetInt(Get(ChatSettingKeys.MaxMessageLength), "Chat:Limits:MaxMessageLength", ChatSettings.DefaultMaxMessageLength),
            MaxMessagesPerChannel = GetInt(Get(ChatSettingKeys.MaxMessagesPerChannel), "Chat:Limits:MaxMessagesPerChannel", 0),
            MaxAttachmentsPerMessage = GetInt(Get(ChatSettingKeys.MaxAttachmentsPerMessage), "Chat:Limits:MaxAttachmentsPerMessage", ChatSettings.DefaultMaxAttachmentsPerMessage),
            MaxAttachmentsPerChannel = GetInt(Get(ChatSettingKeys.MaxAttachmentsPerChannel), "Chat:Limits:MaxAttachmentsPerChannel", 0),
            MaxAttachmentSizeMb = GetInt(Get(ChatSettingKeys.MaxAttachmentSizeMb), "Chat:Limits:MaxAttachmentSizeMb", ChatSettings.DefaultMaxAttachmentSizeMb),
            MaxAttachmentStoragePerChannelMb = GetInt(Get(ChatSettingKeys.MaxAttachmentStoragePerChannelMb), "Chat:Limits:MaxAttachmentStoragePerChannelMb", 0),
            RetentionEnabled = GetBool(Get(ChatSettingKeys.RetentionEnabled), "Chat:Retention:Enabled", false),
            MessageLifetimeDays = GetInt(Get(ChatSettingKeys.MessageLifetimeDays), "Chat:Retention:MessageLifetimeDays", 0),
            RetentionMode = ChatSettings.ParseMode(
                Get(ChatSettingKeys.RetentionMode) ?? _configuration.GetValue<string>("Chat:Retention:Mode")),
            ArchiveAttachments = GetBool(Get(ChatSettingKeys.ArchiveAttachments), "Chat:Retention:ArchiveAttachments", true),
            SweepIntervalMinutes = GetInt(Get(ChatSettingKeys.SweepIntervalMinutes), "Chat:Retention:SweepIntervalMinutes", ChatSettings.DefaultSweepIntervalMinutes)
        };

        return settings.Normalized();
    }

    private async Task<Dictionary<string, string>> ReadDatabaseValuesAsync(CancellationToken cancellationToken)
    {
        var service = _settingsService;
        if (service is null)
        {
            if (_serviceProvider.GetService(typeof(IAdminSettingsService)) is not IAdminSettingsService resolved)
                return [];

            service = _settingsService = resolved;
        }

        try
        {
            var rows = await service.ListSettingsAsync(ChatSettingKeys.ModuleId).ConfigureAwait(false);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                // ListSettingsAsync filters with a substring match, so re-check module equality.
                if (string.Equals(row.Module, ChatSettingKeys.ModuleId, StringComparison.OrdinalIgnoreCase))
                    map[row.Key] = row.Value;
            }

            return map;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read Chat settings from the database; falling back to configuration values.");
            return [];
        }
    }

    private int GetInt(string? dbValue, string configKey, int defaultValue)
    {
        if (dbValue is not null)
            return int.TryParse(dbValue, out var fromDb) ? fromDb : defaultValue;

        var configured = _configuration.GetValue<int?>(configKey);
        return configured ?? defaultValue;
    }

    private bool GetBool(string? dbValue, string configKey, bool defaultValue)
    {
        if (dbValue is not null)
            return bool.TryParse(dbValue, out var fromDb) ? fromDb : defaultValue;

        var configured = _configuration.GetValue<bool?>(configKey);
        return configured ?? defaultValue;
    }
}
