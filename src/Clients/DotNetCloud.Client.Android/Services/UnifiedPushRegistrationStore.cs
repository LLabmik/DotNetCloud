using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Preferences-backed implementation of <see cref="IUnifiedPushRegistrationStore"/>.
/// </summary>
/// <remarks>
/// In the app the storage is Android's encrypted shared-preferences file (via
/// <see cref="IAppPreferences"/>), which matters because a push endpoint is a capability URL.
/// This type itself only depends on the abstraction, so the persistence rules — per-connection
/// keying, endpoint and token lookup — are unit-testable on plain <c>net10.0</c>.
/// </remarks>
public sealed class UnifiedPushRegistrationStore : IUnifiedPushRegistrationStore
{
    private const string RegistrationsKey = "up_registrations_v1";
    private const string DistributorKey = "up_distributor_package";

    private readonly IAppPreferences _preferences;
    private readonly ILogger<UnifiedPushRegistrationStore> _logger;
    private readonly object _sync = new();

    /// <summary>Initializes a new <see cref="UnifiedPushRegistrationStore"/>.</summary>
    /// <param name="preferences">Preferences storage.</param>
    /// <param name="logger">Logger.</param>
    public UnifiedPushRegistrationStore(IAppPreferences preferences, ILogger<UnifiedPushRegistrationStore> logger)
    {
        _preferences = preferences;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? SelectedDistributorPackage
    {
        get
        {
            try
            {
                var value = _preferences.Get(DistributorKey, string.Empty);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reading the selected distributor failed.");
                return null;
            }
        }

        set
        {
            try
            {
                _preferences.Set(DistributorKey, value ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storing the selected distributor failed.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<UnifiedPushRegistration> GetAll()
    {
        lock (_sync)
            return Load();
    }

    /// <inheritdoc />
    public UnifiedPushRegistration? Get(string serverBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return null;

        var key = Normalize(serverBaseUrl);
        lock (_sync)
        {
            return Load().FirstOrDefault(
                registration => Normalize(registration.ServerBaseUrl) == key);
        }
    }

    /// <inheritdoc />
    public UnifiedPushRegistration? FindByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        lock (_sync)
        {
            return Load().FirstOrDefault(
                registration => string.Equals(registration.Token, token, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc />
    public void Save(UnifiedPushRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.ServerBaseUrl) || string.IsNullOrWhiteSpace(registration.Token))
            return;

        lock (_sync)
        {
            var key = Normalize(registration.ServerBaseUrl);
            var all = Load();
            var index = all.FindIndex(item => Normalize(item.ServerBaseUrl) == key);
            if (index >= 0)
                all[index] = registration;
            else
                all.Add(registration);

            Save(all);
        }
    }

    /// <inheritdoc />
    public void Remove(string serverBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return;

        var key = Normalize(serverBaseUrl);
        lock (_sync)
        {
            var all = Load();
            all.RemoveAll(item => Normalize(item.ServerBaseUrl) == key);
            Save(all);
        }
    }

    private List<UnifiedPushRegistration> Load()
    {
        try
        {
            var json = _preferences.Get(RegistrationsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JsonSerializer.Deserialize<List<UnifiedPushRegistration>>(json) ?? [];
        }
        catch (Exception ex)
        {
            // Corrupt state must not brick push: start over rather than throwing into a receiver.
            _logger.LogWarning(ex, "Stored UnifiedPush registrations could not be read; starting empty.");
            return [];
        }
    }

    private void Save(List<UnifiedPushRegistration> registrations)
    {
        try
        {
            _preferences.Set(RegistrationsKey, JsonSerializer.Serialize(registrations));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Storing UnifiedPush registrations failed.");
        }
    }

    private static string Normalize(string serverBaseUrl) =>
        serverBaseUrl.Trim().TrimEnd('/').ToLowerInvariant();
}
