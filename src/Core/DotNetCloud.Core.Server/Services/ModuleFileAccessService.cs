using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// The core half of the <see cref="IFileDirectory"/> / <see cref="IFileWriter"/> capabilities: it
/// brokers a module's request to the Files module on the acting user's behalf.
/// </summary>
/// <remarks>
/// <para>
/// Two checks stand in front of every call, and neither can be performed by the caller:
/// the capability grant (enforced by <c>ModuleAccessInterceptor</c> from the RPC mapping in
/// <see cref="CapabilityCatalog"/>) and the per-user consent to <c>files:read</c>/<c>files:write</c>
/// (enforced here, because only the handler knows which user the call is for).
/// </para>
/// <para>
/// The Files module is reached over its own gRPC surface, never by touching its database or process.
/// A failure there is translated into a gRPC status so a module sees the same stable codes it gets
/// from every other core call — "the file is gone" (NotFound) and "file access is unavailable"
/// (Unavailable) are deliberately different answers.
/// </para>
/// </remarks>
internal sealed class ModuleFileAccessService
{
    /// <summary>Largest single file a module may store through <see cref="IFileWriter"/>.</summary>
    internal const long MaxUploadBytes = 100L * 1024 * 1024;

    /// <summary>Largest page of directory entries one call may request.</summary>
    internal const int MaxPageSize = 500;

    /// <summary>Page size used when the caller does not ask for one.</summary>
    internal const int DefaultPageSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleConsentPolicy _consentPolicy;
    private readonly ILogger<ModuleFileAccessService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleFileAccessService"/> class.</summary>
    /// <param name="scopeFactory">Scope factory used to resolve the scoped Files client.</param>
    /// <param name="consentPolicy">Consent policy for the <c>files:read</c>/<c>files:write</c> scopes.</param>
    /// <param name="logger">Logger.</param>
    public ModuleFileAccessService(
        IServiceScopeFactory scopeFactory,
        ModuleConsentPolicy consentPolicy,
        ILogger<ModuleFileAccessService> logger)
    {
        _scopeFactory = scopeFactory;
        _consentPolicy = consentPolicy;
        _logger = logger;
    }

    /// <summary>Lists one folder of the acting user's files.</summary>
    /// <param name="moduleId">The authenticated module.</param>
    /// <param name="userId">The acting user, from the verified assertion.</param>
    /// <param name="parentId">Folder to list; <see langword="null"/> lists the user's root.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder listing.</returns>
    public async Task<FileListResult> ListAsync(
        string moduleId,
        Guid userId,
        Guid? parentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await RequireConsentAsync(moduleId, userId, ModuleScopes.FilesRead, cancellationToken);

        // Normalize here rather than trusting the caller: an unbounded page size is a cheap way for a
        // module to make the core pull a whole file tree out of the Files module.
        var safePage = page > 0 ? page : 1;
        var safePageSize = pageSize > 0 ? Math.Min(pageSize, MaxPageSize) : DefaultPageSize;

        using var scope = _scopeFactory.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFilesApiClient>();

        var result = await files.ListChildrenAsync(userId, parentId, safePage, safePageSize, cancellationToken);
        ThrowIfFailed(result.Status, result.ErrorMessage, "list files");

        return result;
    }

    /// <summary>Opens the current content of one of the acting user's files.</summary>
    /// <param name="moduleId">The authenticated module.</param>
    /// <param name="userId">The acting user, from the verified assertion.</param>
    /// <param name="nodeId">The file node.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content stream; the caller owns it.</returns>
    public async Task<FileReadResult> OpenReadAsync(
        string moduleId,
        Guid userId,
        Guid nodeId,
        CancellationToken cancellationToken)
    {
        await RequireConsentAsync(moduleId, userId, ModuleScopes.FilesRead, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFilesApiClient>();

        var result = await files.OpenReadAsync(userId, nodeId, cancellationToken);
        ThrowIfFailed(result.Status, result.ErrorMessage, "read file");

        return result;
    }

    /// <summary>Stores content in the acting user's files.</summary>
    /// <param name="moduleId">The authenticated module.</param>
    /// <param name="userId">The acting user, from the verified assertion.</param>
    /// <param name="parentId">Target folder; <see langword="null"/> stores in the user's root.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="contentType">MIME type.</param>
    /// <param name="content">The bytes to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored node.</returns>
    public async Task<FileWriteResult> SaveAsync(
        string moduleId,
        Guid userId,
        Guid? parentId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeName = SanitizeFileName(fileName);

        await RequireConsentAsync(moduleId, userId, ModuleScopes.FilesWrite, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFilesApiClient>();

        var result = await files.UploadFileAsync(
            userId,
            parentId,
            safeName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            content,
            cancellationToken);

        ThrowIfFailed(result.Status, result.ErrorMessage, "save file");

        return result;
    }

    private async Task RequireConsentAsync(
        string moduleId,
        Guid userId,
        string scope,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "File access needs a verified end-user identity; this call carries none."));
        }

        var missing = await _consentPolicy.GetMissingConsentScopesAsync(
            moduleId,
            userId,
            [scope],
            cancellationToken);

        if (missing.Count > 0)
        {
            throw ModuleConsentPolicy.CreateConsentRequiredException(missing);
        }
    }

    private void ThrowIfFailed(FilesAccessStatus status, string? errorMessage, string operation)
    {
        switch (status)
        {
            case FilesAccessStatus.Success:
                return;

            case FilesAccessStatus.NotFound:
                throw new RpcException(new Status(
                    StatusCode.NotFound,
                    "The file does not exist, was deleted, or does not belong to the acting user."));

            case FilesAccessStatus.Unavailable:
                throw new RpcException(new Status(
                    StatusCode.Unavailable,
                    "File access is unavailable: the Files module is not running."));

            default:
                _logger.LogWarning(
                    "Files module failed to {Operation}: {Message}",
                    operation,
                    LogSanitizer.Sanitize(errorMessage ?? "no message"));

                throw new RpcException(new Status(
                    StatusCode.FailedPrecondition,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "The Files module rejected the request."
                        : errorMessage));
        }
    }

    /// <summary>
    /// Reduces a module-supplied name to one path segment: no directory separators, no traversal,
    /// no control characters, and a bounded length.
    /// </summary>
    /// <param name="fileName">The proposed name.</param>
    /// <returns>A safe file name.</returns>
    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));

        var cleaned = new string(name
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();

        if (cleaned is "" or "." or "..")
        {
            cleaned = "attachment";
        }

        return cleaned.Length <= 255 ? cleaned : cleaned[..255];
    }
}
