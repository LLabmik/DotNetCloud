using DotNetCloud.Core.Search.Extraction;
using DotNetCloud.Core.Search.Extraction.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Core.Search.Extraction.Host.Services;

/// <summary>
/// gRPC service exposing the out-of-process content extraction worker to the core process.
/// </summary>
public sealed class ExtractionGrpcService : ExtractionService.ExtractionServiceBase
{
    private readonly ContentExtractionService _extractionService;
    private readonly ILogger<ExtractionGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionGrpcService"/> class.
    /// </summary>
    public ExtractionGrpcService(ContentExtractionService extractionService, ILogger<ExtractionGrpcService> logger)
    {
        _extractionService = extractionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ExtractResponse> Extract(ExtractRequest request, ServerCallContext context)
    {
        try
        {
            var extracted = await _extractionService.ExtractAsync(
                request.Content.ToByteArray(), request.MimeType, context.CancellationToken);

            if (extracted is null)
            {
                return new ExtractResponse
                {
                    Success = false,
                    ErrorMessage = "No extractor supports the given MIME type or extraction failed."
                };
            }

            var response = new ExtractResponse
            {
                Success = true,
                Text = extracted.Text
            };
            foreach (var kvp in extracted.Metadata)
            {
                response.Metadata[kvp.Key] = kvp.Value;
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed for MIME type {MimeType}", request.MimeType);
            return new ExtractResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
