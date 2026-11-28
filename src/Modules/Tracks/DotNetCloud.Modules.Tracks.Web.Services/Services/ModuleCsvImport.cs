using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Browser-side <see cref="ICsvImportUiService"/>: parses and imports a CSV through the module host's
/// import endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The server keeps the authoritative CSV parser, so the wizard does not re-implement it in
/// WebAssembly. Two calls are used: a dry run (parse + validate) and the real import, both posting the
/// file as multipart form data to <c>products/{id}/work-items/import</c>.
/// </para>
/// <para>
/// The returned <see cref="CsvParseResult"/> is reconstructed from the dry-run response, which reports
/// the headers, delimiter, preview rows and total row count so the wizard's preview step still works.
/// </para>
/// </remarks>
public sealed class ModuleCsvImport : ICsvImportUiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ModuleCsvImport> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleCsvImport"/> class.</summary>
    /// <param name="http">The module client.</param>
    /// <param name="logger">Logger.</param>
    public ModuleCsvImport(HttpClient http, ILogger<ModuleCsvImport> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CsvParseResult> ParseCsvAsync(Stream stream, CancellationToken ct)
    {
        // Parsing needs a product for the dry-run endpoint to be meaningful; the wizard calls this
        // before it knows the product on a fresh file pick, so parse locally and let the validate step
        // confirm the rows against the module's rules.
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(ct);
        return CsvTextParser.Parse(text);
    }

    /// <inheritdoc />
    public async Task<CsvValidationResult> ValidateCsvAsync(
        Guid productId,
        Stream stream,
        CsvColumnMapping mapping,
        CancellationToken ct)
    {
        var response = await PostImportAsync(productId, swimlaneId: null, stream, mapping, dryRun: true, ct);

        return new CsvValidationResult
        {
            ValidRowCount = response.ValidRowCount,
            Errors = response.Errors,
        };
    }

    /// <inheritdoc />
    public async Task<CsvImportResult> ImportCsvAsync(
        Guid productId,
        Guid swimlaneId,
        Stream stream,
        CsvColumnMapping mapping,
        bool skipDuplicates,
        CancellationToken ct)
    {
        var response = await PostImportAsync(productId, swimlaneId, stream, mapping, dryRun: false, ct);

        return new CsvImportResult
        {
            Created = response.Created,
            Skipped = response.Skipped,
            Failed = response.Failed,
            BatchCount = response.BatchCount,
            // The wizard previews row-level problems; CsvImportResult keeps a flat message list.
            Errors = response.Errors.SelectMany(e => e.Errors.Select(message => $"Row {e.RowNumber}: {message}")).ToList(),
        };
    }

    private async Task<ImportResponse> PostImportAsync(
        Guid productId,
        Guid? swimlaneId,
        Stream stream,
        CsvColumnMapping mapping,
        bool dryRun,
        CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");

        if (mapping is not null)
        {
            content.Add(new StringContent(JsonSerializer.Serialize(mapping, TracksApi.JsonOptions)), "mappingJson");
        }

        var query = dryRun ? "?dryRun=true" : string.Empty;
        if (swimlaneId is { } id && id != Guid.Empty)
        {
            query += query.Length == 0 ? "?swimlaneId=" : "&swimlaneId=";
            query += id;
        }

        using var response = await _http.PostAsync($"api/v1/products/{productId}/work-items/import{query}", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("CSV import request failed with {StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException($"CSV import failed ({(int)response.StatusCode}).", null, response.StatusCode);
        }

        var envelope = await response.Content.ReadFromJsonAsync<ImportEnvelope>(TracksApi.JsonOptions, ct);
        if (envelope?.Data is null)
        {
            throw new InvalidOperationException("The module returned an empty import response.");
        }

        return envelope.Data;
    }

    private sealed class ImportEnvelope
    {
        public bool Success { get; set; }
        public ImportResponse? Data { get; set; }
    }

    /// <summary>Union of the dry-run and import payloads the host returns.</summary>
    public sealed class ImportResponse
    {
        /// <summary>Gets or sets the parsed headers (dry run only).</summary>
        public List<string> Headers { get; set; } = [];

        /// <summary>Gets or sets the detected delimiter (dry run only).</summary>
        public string? Delimiter { get; set; }

        /// <summary>Gets or sets the preview rows (dry run only).</summary>
        public List<List<string>> PreviewRows { get; set; } = [];

        /// <summary>Gets or sets the total row count (dry run only).</summary>
        public int TotalRows { get; set; }

        /// <summary>Gets or sets how many rows validated (dry run only).</summary>
        public int ValidRowCount { get; set; }

        /// <summary>Gets or sets how many items were created.</summary>
        public int Created { get; set; }

        /// <summary>Gets or sets how many rows were skipped as duplicates.</summary>
        public int Skipped { get; set; }

        /// <summary>Gets or sets how many rows failed.</summary>
        public int Failed { get; set; }

        /// <summary>Gets or sets the number of batches the import used.</summary>
        public int BatchCount { get; set; }

        /// <summary>Gets or sets the row-level errors.</summary>
        public List<CsvRowError> Errors { get; set; } = [];
    }
}

/// <summary>
/// Minimal RFC 4180 CSV parser for the wizard's preview step.
/// </summary>
internal static class CsvTextParser
{
    /// <summary>Parses CSV text into headers, a preview and a total row count.</summary>
    /// <param name="text">The file contents.</param>
    /// <param name="previewCount">How many rows to keep as a preview.</param>
    /// <returns>The parse result.</returns>
    public static CsvParseResult Parse(string text, int previewCount = 5)
    {
        var rows = SplitRows(text);
        var headers = rows.Count > 0 ? rows[0] : [];
        var dataRows = rows.Skip(1).ToList();

        return new CsvParseResult
        {
            Headers = headers,
            // The preview parser is comma-only; the host's dry run re-parses with its own sniffer and
            // is what actually imports the file.
            Delimiter = ',',
            PreviewRows = dataRows.Take(previewCount).ToList(),
            TotalRows = dataRows.Count,
        };
    }

    private static List<List<string>> SplitRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows.Where(r => r.Count > 0).ToList();
    }
}
