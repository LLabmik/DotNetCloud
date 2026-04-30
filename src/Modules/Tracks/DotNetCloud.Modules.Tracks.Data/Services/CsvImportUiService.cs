using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Implements <see cref="ICsvImportUiService"/> — bridges CsvImportService to the UI layer.
/// </summary>
public sealed class CsvImportUiService : ICsvImportUiService
{
    private readonly CsvImportService _importService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvImportUiService"/> class.
    /// </summary>
    public CsvImportUiService(CsvImportService importService)
    {
        _importService = importService;
    }

    /// <inheritdoc />
    public Task<CsvParseResult> ParseCsvAsync(Stream stream, CancellationToken ct)
        => _importService.ParseCsvAsync(stream, ct);

    /// <inheritdoc />
    public Task<CsvValidationResult> ValidateCsvAsync(Guid productId, Stream stream, CsvColumnMapping mapping, CancellationToken ct)
        => _importService.ValidateCsvAsync(productId, stream, mapping, ct);

    /// <inheritdoc />
    public Task<CsvImportResult> ImportCsvAsync(Guid productId, Guid swimlaneId, Stream stream, CsvColumnMapping mapping, bool skipDuplicates, CancellationToken ct)
        => _importService.ImportCsvAsync(productId, swimlaneId, Guid.Empty, stream, mapping, skipDuplicates, ct);
}
