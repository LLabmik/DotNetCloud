using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Service interface for CSV import operations accessible from the UI layer.
/// Implemented in the Data layer.
/// </summary>
public interface ICsvImportUiService
{
    /// <summary>Parses a CSV stream and returns detected columns + preview rows.</summary>
    Task<CsvParseResult> ParseCsvAsync(Stream stream, CancellationToken ct);

    /// <summary>Validates mapped rows and returns row-level errors without importing.</summary>
    Task<CsvValidationResult> ValidateCsvAsync(Guid productId, Stream stream, CsvColumnMapping mapping, CancellationToken ct);

    /// <summary>Imports work items from a CSV stream with the specified column mapping.</summary>
    Task<CsvImportResult> ImportCsvAsync(Guid productId, Guid swimlaneId, Stream stream, CsvColumnMapping mapping, bool skipDuplicates, CancellationToken ct);
}
