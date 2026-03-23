using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Import;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Routes import requests to the appropriate <see cref="IImportProvider"/> and produces unified reports.
/// </summary>
public sealed class ImportPipelineService : IImportPipeline
{
    private readonly IReadOnlyDictionary<ImportDataType, IImportProvider> _providers;
    private readonly ILogger<ImportPipelineService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportPipelineService"/> class.
    /// </summary>
    public ImportPipelineService(
        IEnumerable<IImportProvider> providers,
        ILogger<ImportPipelineService> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var dict = new Dictionary<ImportDataType, IImportProvider>();
        foreach (var provider in providers)
        {
            if (!dict.TryAdd(provider.DataType, provider))
            {
                _logger.LogWarning(
                    "Duplicate import provider for {DataType}; keeping first registration",
                    provider.DataType);
            }
        }

        _providers = dict;
        _logger.LogInformation("Import pipeline initialized with providers: {Types}",
            string.Join(", ", dict.Keys));
    }

    /// <inheritdoc />
    public IReadOnlyList<ImportDataType> SupportedDataTypes =>
        _providers.Keys.ToList().AsReadOnly();

    /// <inheritdoc />
    public Task<ImportReport> PreviewAsync(
        ImportRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var provider = ResolveProvider(request.DataType);
        _logger.LogInformation(
            "Preview import: {DataType} from {Source} for user {UserId}",
            request.DataType, request.Source, caller.UserId);

        return provider.PreviewAsync(request, caller, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ImportReport> ExecuteAsync(
        ImportRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (request.DryRun)
        {
            return PreviewAsync(request, caller, cancellationToken);
        }

        var provider = ResolveProvider(request.DataType);
        _logger.LogInformation(
            "Execute import: {DataType} from {Source} for user {UserId}",
            request.DataType, request.Source, caller.UserId);

        return provider.ExecuteAsync(request, caller, cancellationToken);
    }

    private IImportProvider ResolveProvider(ImportDataType dataType)
    {
        if (_providers.TryGetValue(dataType, out var provider))
        {
            return provider;
        }

        throw new Core.Errors.ValidationException(
            "DataType",
            $"No import provider registered for data type '{dataType}'.");
    }
}
