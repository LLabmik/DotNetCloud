using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Import;

/// <summary>
/// Provider that can parse and import data for a specific <see cref="ImportDataType"/>.
/// Modules register one provider per supported data type.
/// </summary>
public interface IImportProvider
{
    /// <summary>
    /// The data type this provider handles.
    /// </summary>
    ImportDataType DataType { get; }

    /// <summary>
    /// Validates and parses the raw import data, returning a preview report without persisting any records.
    /// </summary>
    Task<ImportReport> PreviewAsync(ImportRequest request, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the import, persisting records according to the request parameters.
    /// When <see cref="ImportRequest.DryRun"/> is true, behaves identically to <see cref="PreviewAsync"/>.
    /// </summary>
    Task<ImportReport> ExecuteAsync(ImportRequest request, CallerContext caller, CancellationToken cancellationToken = default);
}
