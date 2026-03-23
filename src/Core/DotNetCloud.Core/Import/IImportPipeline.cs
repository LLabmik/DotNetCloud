using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Import;

/// <summary>
/// Orchestrates import operations by routing <see cref="ImportRequest"/>s to the
/// appropriate <see cref="IImportProvider"/> and producing unified <see cref="ImportReport"/>s.
/// </summary>
public interface IImportPipeline
{
    /// <summary>
    /// Runs a dry-run preview of the import without persisting any data.
    /// </summary>
    Task<ImportReport> PreviewAsync(ImportRequest request, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the import and returns the final report.
    /// Respects <see cref="ImportRequest.DryRun"/>: when true, delegates to <see cref="PreviewAsync"/>.
    /// </summary>
    Task<ImportReport> ExecuteAsync(ImportRequest request, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the supported data types that have registered providers.
    /// </summary>
    IReadOnlyList<ImportDataType> SupportedDataTypes { get; }
}
