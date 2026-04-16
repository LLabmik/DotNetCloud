using System.Threading.Channels;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Events.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Processes search indexing requests using a background channel for backpressure.
/// Receives <see cref="SearchIndexRequestEvent"/> events and calls the search provider
/// to update the index. Integrates content extraction for file-type documents.
/// </summary>
public sealed class SearchIndexingService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchIndexingService> _logger;
    private readonly Channel<SearchIndexRequestEvent> _channel;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private long _totalProcessed;
    private long _totalFailed;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexingService"/> class.</summary>
    public SearchIndexingService(
        IServiceScopeFactory scopeFactory,
        ILogger<SearchIndexingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<SearchIndexRequestEvent>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>Starts the background processing loop.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _processingTask = ProcessQueueAsync(_cts.Token);
    }

    /// <summary>Stops the background processing loop.</summary>
    public async Task StopAsync()
    {
        _channel.Writer.Complete();
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    /// <summary>Enqueues an indexing request for background processing.</summary>
    public async ValueTask EnqueueAsync(SearchIndexRequestEvent request, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>Gets the number of pending items in the queue.</summary>
    public int PendingCount => _channel.Reader.Count;

    /// <summary>Gets the total number of successfully processed indexing requests.</summary>
    public long TotalProcessed => Interlocked.Read(ref _totalProcessed);

    /// <summary>Gets the total number of failed indexing requests.</summary>
    public long TotalFailed => Interlocked.Read(ref _totalFailed);

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Search indexing service started processing");

        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessRequestAsync(request, cancellationToken);
                    Interlocked.Increment(ref _totalProcessed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref _totalFailed);
                    _logger.LogError(ex, "Error processing index request for {ModuleId}/{EntityId}",
                        request.ModuleId, request.EntityId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Search indexing service stopping");
        }
    }

    private async Task ProcessRequestAsync(SearchIndexRequestEvent request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();
        var searchableModules = scope.ServiceProvider.GetServices<ISearchableModule>();
        var extractionService = scope.ServiceProvider.GetRequiredService<ContentExtractionService>();

        if (request.Action == SearchIndexAction.Remove)
        {
            await searchProvider.RemoveDocumentAsync(request.ModuleId, request.EntityId, cancellationToken);
            _logger.LogDebug("Removed {ModuleId}/{EntityId} from index", request.ModuleId, request.EntityId);
            return;
        }

        // Find the module that owns this entity
        var module = searchableModules.FirstOrDefault(m => m.ModuleId == request.ModuleId);
        if (module is null)
        {
            _logger.LogWarning("No searchable module found for {ModuleId}", request.ModuleId);
            return;
        }

        // Get the document from the module
        var document = await module.GetSearchableDocumentAsync(request.EntityId, cancellationToken);
        if (document is null)
        {
            // Entity no longer exists — remove from index
            await searchProvider.RemoveDocumentAsync(request.ModuleId, request.EntityId, cancellationToken);
            _logger.LogDebug("Entity {ModuleId}/{EntityId} no longer exists, removed from index",
                request.ModuleId, request.EntityId);
            return;
        }

        // For file-type documents, attempt content extraction if MIME type is available
        var enrichedDocument = await TryEnrichWithContentExtraction(document, extractionService, cancellationToken);

        await searchProvider.IndexDocumentAsync(enrichedDocument, cancellationToken);
        _logger.LogDebug("Indexed {ModuleId}/{EntityId}", request.ModuleId, request.EntityId);
    }

    /// <summary>
    /// Attempts to enrich a document with extracted content from a file if MIME type metadata
    /// indicates extractable content and the document content is empty or minimal.
    /// </summary>
    internal async Task<SearchDocument> TryEnrichWithContentExtraction(
        SearchDocument document, ContentExtractionService extractionService, CancellationToken cancellationToken)
    {
        // Only attempt content extraction if MimeType metadata is present and supported
        if (!document.Metadata.TryGetValue("MimeType", out var mimeType) &&
            !document.Metadata.TryGetValue("mimeType", out mimeType))
        {
            return document;
        }

        if (string.IsNullOrWhiteSpace(mimeType) || !extractionService.CanExtract(mimeType))
        {
            return document;
        }

        // Only extract if Content is empty or just whitespace (avoid re-extracting)
        if (!string.IsNullOrWhiteSpace(document.Content))
        {
            return document;
        }

        // Content extraction requires a file stream from the module
        // The module should provide the content via the Content field or
        // the SearchDocument's metadata should include a file path
        // For now, log that extraction would be needed
        _logger.LogDebug(
            "Document {ModuleId}/{EntityId} has extractable MIME type {MimeType} but no content stream available; " +
            "content extraction deferred to module-level integration",
            document.ModuleId, document.EntityId, mimeType);

        return document;
    }

    /// <summary>
    /// Enriches a document by extracting text content from a file stream.
    /// Used when the indexing pipeline has access to file content.
    /// </summary>
    public async Task<SearchDocument> EnrichDocumentFromStreamAsync(
        SearchDocument document, Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var extractionService = scope.ServiceProvider.GetRequiredService<ContentExtractionService>();
        var extracted = await extractionService.ExtractAsync(fileStream, mimeType, cancellationToken);
        if (extracted is null)
        {
            return document;
        }

        // Merge extracted metadata with existing document metadata
        var mergedMetadata = new Dictionary<string, string>(document.Metadata);
        foreach (var kvp in extracted.Metadata)
        {
            mergedMetadata.TryAdd(kvp.Key, kvp.Value);
        }

        return document with
        {
            Content = extracted.Text,
            Metadata = mergedMetadata
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Dispose();
    }
}
