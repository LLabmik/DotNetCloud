using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc.Clients;

/// <summary>
/// gRPC client for the <c>CoreCapabilities.GetSetting</c> / <c>SetSetting</c> RPCs.
/// </summary>
/// <remarks>
/// <para>
/// Process-isolated modules use this client to persist key/value settings in the core. Every call
/// is tagged with the supervisor-issued module credential via <see cref="ModuleIdentityMetadata"/>
/// — the core ignores any module id in the request payload and namespaces the key by the verified
/// identity.
/// </para>
/// <para>
/// Unlike the audit client this is not a no-op when the module was not launched by the supervisor:
/// silently returning "no value" would make a module believe its settings vanished. Callers get an
/// <see cref="InvalidOperationException"/> instead, which surfaces during startup health checks.
/// </para>
/// </remarks>
internal sealed class ModuleSettingsGrpcClient : IModuleSettings, IDisposable
{
    private readonly ILogger<ModuleSettingsGrpcClient> _logger;
    private readonly string? _coreEndpoint;
    private readonly string? _moduleId;
    private GrpcChannel? _channel;
    private CoreCapabilities.CoreCapabilitiesClient? _client;
    private readonly object _sync = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleSettingsGrpcClient"/> class.
    /// </summary>
    /// <param name="logger">The logger for this client.</param>
    public ModuleSettingsGrpcClient(ILogger<ModuleSettingsGrpcClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID");
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!ModuleSettingLimits.IsValidKey(key))
        {
            throw new ArgumentException($"Invalid module setting key '{key}'.", nameof(key));
        }

        var request = new GetSettingRequest { ModuleId = _moduleId, Key = key };
        var response = await GetClient().GetSettingAsync(
            request,
            headers: ModuleIdentityMetadata.Create(ModuleId),
            cancellationToken: cancellationToken);

        return response.Found ? response.Value : null;
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (!ModuleSettingLimits.IsValidKey(key))
        {
            throw new ArgumentException($"Invalid module setting key '{key}'.", nameof(key));
        }

        if (!ModuleSettingLimits.IsValidValue(value))
        {
            throw new ArgumentException(
                $"Module setting value exceeds {ModuleSettingLimits.MaxValueLength} characters.",
                nameof(value));
        }

        var request = new SetSettingRequest { ModuleId = _moduleId, Key = key, Value = value };
        var response = await GetClient().SetSettingAsync(
            request,
            headers: ModuleIdentityMetadata.Create(ModuleId),
            cancellationToken: cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException(
                $"Core.Server rejected the setting write for key '{key}' in module '{ModuleId}'.");
        }
    }

    private string ModuleId =>
        string.IsNullOrWhiteSpace(_moduleId)
            ? throw new InvalidOperationException(
                "DOTNETCLOUD_MODULE_ID is not set. Module settings require the module to be launched by ProcessSupervisor.")
            : _moduleId;

    private CoreCapabilities.CoreCapabilitiesClient GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is not null)
        {
            return _client;
        }

        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            throw new InvalidOperationException(
                "DOTNETCLOUD_CORE_ENDPOINT is not set. Module settings require the module to be launched by ProcessSupervisor.");
        }

        lock (_sync)
        {
            if (_client is not null)
            {
                return _client;
            }

            var address = _coreEndpoint
                .Replace("unix://", "http://")
                .Replace("net.pipe://", "http://");

            _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false,
                },
                ThrowOperationCanceledOnCancellation = true,
            });

            _client = new CoreCapabilities.CoreCapabilitiesClient(_channel);

            _logger.LogInformation("ModuleSettingsGrpcClient: connected to Core.Server at {Endpoint}", address);
            return _client;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel?.Dispose();
    }
}
