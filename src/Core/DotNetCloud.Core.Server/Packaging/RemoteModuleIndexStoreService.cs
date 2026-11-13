using System.Text.Json;
using DotNetCloud.Core;
using DotNetCloud.Core.Modules.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// The remote module store client: fetches, verifies and caches the published signed index, then
/// downloads and verifies packages on demand.
/// </summary>
/// <remarks>
/// <para>
/// Trust comes from <see cref="ModuleStoreOptions.TrustedKeys"/> and nowhere else. The index is only
/// accepted when the detached <c>&lt;IndexUrl&gt;.sig</c> verifies against a pinned key, and a
/// package is only accepted when its bytes hash to the signed index entry and its own Ed25519
/// signature verifies against the same pinned key set — the second check is the installer's, so a
/// compromised index cannot inject bytes.
/// </para>
/// <para>
/// The index is cached on disk (<c>Modules:Store:CachePath</c>) for
/// <see cref="ModuleStoreOptions.IndexCacheTtl"/> so a restart does not refetch, and a failed
/// refresh falls back to the cached copy rather than breaking the admin page. An index whose
/// <c>generatedAt</c> is older than the cached one is refused (replay protection) unless
/// <see cref="ModuleStoreOptions.AllowIndexRollback"/> is set.
/// </para>
/// <para>
/// Downloaded packages and their signatures are cached beside the index, so installing the same
/// version twice costs no network traffic. Every path is derived through
/// <see cref="ModuleStorePaths"/>, which rejects path traversal from index-supplied ids and
/// versions.
/// </para>
/// </remarks>
public sealed class RemoteModuleIndexStoreService : IModuleStoreService
{
    /// <summary>The named <see cref="HttpClient"/> used by this client (for handlers and diagnostics).</summary>
    public const string HttpClientName = "ModuleStore";

    private const string IndexFileName = "index.json";
    private const string CacheStateFileName = "state.json";
    private const int MaxIndexBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions StateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly HttpClient _http;
    private readonly ModuleStoreOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<RemoteModuleIndexStoreService> _logger;

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _downloadGate = new(1, 1);

    private ModuleStoreIndex? _index;
    private Uri? _downloadBaseUri;
    private DateTimeOffset _indexCachedAtUtc;

    /// <summary>Initializes a new instance of the <see cref="RemoteModuleIndexStoreService"/> class.</summary>
    /// <param name="http">The HTTP client used for index and package downloads.</param>
    /// <param name="options">The <c>Modules:Store</c> options.</param>
    /// <param name="timeProvider">Clock used for cache expiry and replay comparison.</param>
    /// <param name="logger">Logger.</param>
    public RemoteModuleIndexStoreService(
        HttpClient http,
        IOptions<ModuleStoreOptions> options,
        TimeProvider timeProvider,
        ILogger<RemoteModuleIndexStoreService> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _options = options.Value;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ModuleStoreSourceKind SourceKind => ModuleStoreSourceKind.RemoteIndex;

    /// <inheritdoc/>
    public string SourceId => !string.IsNullOrWhiteSpace(_options.IndexUrl)
        ? _options.IndexUrl.Trim()
        : (_options.Mirrors ?? []).FirstOrDefault(mirror => !string.IsNullOrWhiteSpace(mirror))
          ?? "remote-index";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleStorePackage>> ListPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(cancellationToken);

        return index.Packages
            .SelectMany(package => (package.Versions ?? []).Select(version => Map(package, version)))
            .OrderBy(package => package.ModuleId, StringComparer.Ordinal)
            .ThenBy(package => package.Version, ModuleStoreVersionComparer.Instance)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<ModuleStoreResolution?> ResolveAsync(
        string moduleId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!ModuleStorePaths.IsSafeSegment(moduleId))
        {
            throw new ModulePackageException($"'{moduleId}' is not a valid module id.");
        }

        var index = await GetIndexAsync(cancellationToken);

        var package = index.Packages.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, moduleId, StringComparison.Ordinal));

        if (package is null)
        {
            return null;
        }

        var candidates = package.Versions ?? [];

        var selected = string.IsNullOrWhiteSpace(version)
            ? candidates.OrderByDescending(candidate => candidate.Version, ModuleStoreVersionComparer.Instance)
                .FirstOrDefault()
            : candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Version, version, StringComparison.Ordinal));

        if (selected is null)
        {
            return null;
        }

        var downloadUri = ResolveDownloadUri(_downloadBaseUri!, selected.DownloadUrl);
        var fileName = Uri.UnescapeDataString(downloadUri.Segments[^1]);

        // The published file name must identify this exact module and version; anything else is a
        // catalogue that cannot be installed and is refused before a byte is downloaded.
        if (!ModulePackageFormat.TryParseFileName(fileName, out var fileNameId, out var fileNameVersion)
            || !string.Equals(fileNameId, package.Id, StringComparison.Ordinal)
            || !string.Equals(fileNameVersion, selected.Version, StringComparison.Ordinal))
        {
            throw new ModulePackageException(
                $"The store lists '{selected.DownloadUrl}' for {package.Id} {selected.Version}, "
                + "which is not a '<id>-<version>.dncpkg' file name.");
        }

        var packagePath = ModuleStorePaths.CombineUnder(
            GetPackageCacheRoot(), package.Id, selected.Version, fileName);

        // The id, version and file name all come from a remote document: re-check containment on the
        // final, fully-resolved path before anything touches the filesystem.
        var cacheRoot = GetPackageCacheRoot();
        if (!ModuleStorePaths.IsInside(cacheRoot, packagePath))
        {
            throw new ModulePackageException(
                $"The store entry for {package.Id} {selected.Version} resolves outside the package cache.");
        }

        var signaturePath = ModulePackageFormat.GetSignaturePath(packagePath);

        await _downloadGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

            if (!await IsCachedPackageValidAsync(packagePath, selected, cancellationToken))
            {
                _logger.LogInformation(
                    "Downloading {ModuleId} {Version} from the module store ({SizeBytes} bytes)",
                    LogSanitizer.Sanitize(package.Id), LogSanitizer.Sanitize(selected.Version), selected.Size);

                await DownloadPackageAsync(downloadUri, packagePath, selected, cancellationToken);
            }

            await WriteSignatureAsync(signaturePath, package, selected, fileName, cancellationToken);
        }
        finally
        {
            _downloadGate.Release();
        }

        return new ModuleStoreResolution(
            Map(package, selected),
            packagePath,
            File.Exists(signaturePath) ? signaturePath : null);
    }

    private static ModuleStorePackage Map(
        ModuleStoreIndexPackage package, ModuleStoreIndexVersion version) =>
        new(
            package.Id,
            string.IsNullOrWhiteSpace(package.Name) ? package.Id : package.Name,
            version.Version,
            GetDisplayFileName(version.DownloadUrl),
            version.Size,
            version.Sha256,
            package.Publisher?.Id,
            version.Capabilities ?? [],
            version.UserConsentScopes ?? [],
            version.Signature?.KeyId,
            ModuleStoreTrustTiers.Parse(version.TrustTier),
            version.Yanked,
            version.Yanked ? version.YankReason ?? "Withdrawn by the store." : null,
            version.Yanked ? version.YankedSeverity ?? "deprecation" : null);

    private static string GetDisplayFileName(string downloadUrl)
    {
        var segment = downloadUrl.Split(['/', '\\', '?', '#'])[^1];
        return string.IsNullOrWhiteSpace(segment) ? downloadUrl : segment;
    }

    // ---------------------------------------------------------------------
    // Index acquisition
    // ---------------------------------------------------------------------

    private async Task<ModuleStoreIndex> GetIndexAsync(CancellationToken cancellationToken)
    {
        if (_index is not null && !IsIndexExpired())
        {
            return _index;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (_index is not null && !IsIndexExpired())
            {
                return _index;
            }

            if (_index is null && await TryReadCacheAsync(cancellationToken) is { } cached)
            {
                _index = cached.Index;
                _downloadBaseUri = cached.DownloadBaseUri;
                _indexCachedAtUtc = cached.FetchedAtUtc;

                if (!IsIndexExpired())
                {
                    return _index;
                }
            }

            var fetched = await FetchAsync(cancellationToken);
            _index = fetched.Index;
            _downloadBaseUri = fetched.DownloadBaseUri;
            _indexCachedAtUtc = _time.GetUtcNow();

            await WriteCacheAsync(fetched, cancellationToken);
            return _index;
        }
        catch (Exception ex) when (IsTransportFailure(ex) && _index is not null)
        {
            // A refresh failure never breaks the store page: the last verified index keeps being
            // served, and the clock is pushed forward so a dead host is not hit on every call.
            _logger.LogWarning(
                "Refreshing the module store index failed; serving the cached copy generated {GeneratedAt:u}: {Error}",
                _index.GeneratedAt, LogSanitizer.Sanitize(ex.Message));

            _indexCachedAtUtc = _time.GetUtcNow();
            return _index;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<(ModuleStoreIndex Index, Uri DownloadBaseUri, string SourceUrl)> FetchAsync(
        CancellationToken cancellationToken)
    {
        var candidates = GetIndexUrls();
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                var indexBytes = await GetBytesAsync(candidate, MaxIndexBytes, cancellationToken);
                var signatureBytes = await GetBytesAsync(BuildSignatureUri(candidate), MaxIndexBytes, cancellationToken);

                var signature = ParseSignature(signatureBytes);
                var keyId = ModuleStoreIndexReader.VerifySignature(
                    indexBytes, signature, _options.GetEffectiveTrustedKeys());

                var index = ModuleStoreIndexReader.Parse(indexBytes);
                EnsureNoReplay(index, candidate);

                _logger.LogInformation(
                    "Module store index verified with key {KeyId}: {PackageCount} package(s), generated {GeneratedAt:u}",
                    LogSanitizer.Sanitize(keyId), index.Packages.Count, index.GeneratedAt);

                return (index, GetDownloadBaseUri(candidate), candidate.AbsoluteUri);
            }
            catch (Exception ex) when (IsTransportFailure(ex))
            {
                errors.Add($"{candidate}: {ex.Message}");
                _logger.LogWarning(
                    "Module store index source {IndexUrl} failed: {Error}",
                    candidate, LogSanitizer.Sanitize(ex.Message));
            }
        }

        throw new ModulePackageException(
            "The module store index could not be fetched or verified. " + string.Join(" | ", errors));
    }

    private IReadOnlyList<Uri> GetIndexUrls()
    {
        var configured = new List<string>();

        if (!string.IsNullOrWhiteSpace(_options.IndexUrl))
        {
            configured.Add(_options.IndexUrl.Trim());
        }

        configured.AddRange((_options.Mirrors ?? [])
            .Where(mirror => !string.IsNullOrWhiteSpace(mirror))
            .Select(mirror => mirror.Trim()));

        var urls = new List<Uri>();

        foreach (var candidate in configured.Distinct(StringComparer.Ordinal))
        {
            if (!TryCreateHttpsUri(candidate, out var uri))
            {
                throw new ModulePackageException(
                    $"'{candidate}' is not a usable store index URL; an https URL is required.");
            }

            urls.Add(uri);
        }

        if (urls.Count == 0)
        {
            throw new ModulePackageException("No module store index URL is configured.");
        }

        return urls;
    }

    private void EnsureNoReplay(ModuleStoreIndex index, Uri source)
    {
        if (_options.AllowIndexRollback || _index is null)
        {
            return;
        }

        if (index.GeneratedAt < _index.GeneratedAt)
        {
            throw new ModulePackageException(
                $"The index at {source} reports generatedAt {index.GeneratedAt:u}, older than the verified "
                + $"cached index ({_index.GeneratedAt:u}). Refusing a possible replay; set "
                + "Modules:Store:AllowIndexRollback to accept a deliberate rollback.");
        }
    }

    private Uri GetDownloadBaseUri(Uri indexUri)
    {
        if (!string.IsNullOrWhiteSpace(_options.DownloadBaseUrl))
        {
            if (!TryCreateHttpsUri(EnsureTrailingSlash(_options.DownloadBaseUrl.Trim()), out var configured))
            {
                throw new ModulePackageException(
                    $"'{_options.DownloadBaseUrl}' is not a usable Modules:Store:DownloadBaseUrl; an https URL is required.");
            }

            return configured;
        }

        return new Uri(indexUri.GetLeftPart(UriPartial.Authority) + "/");
    }

    private Uri ResolveDownloadUri(Uri baseUri, string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.RelativeOrAbsolute, out var parsed))
        {
            throw new ModulePackageException($"The store lists an unusable download URL '{downloadUrl}'.");
        }

        Uri resolved;

        if (parsed.IsAbsoluteUri)
        {
            if (!IsAllowedScheme(parsed))
            {
                throw new ModulePackageException(
                    $"The store lists a download URL with an unsupported scheme: '{downloadUrl}'.");
            }

            resolved = parsed;
        }
        else
        {
            // A relative URL may not climb out of the store root before it is resolved.
            var segments = downloadUrl.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)
                                        || string.Equals(segment, ".", StringComparison.Ordinal)))
            {
                throw new ModulePackageException(
                    $"The store lists a download URL that escapes the store root: '{downloadUrl}'.");
            }

            resolved = new Uri(baseUri, downloadUrl);
        }

        if (!IsAllowedDownloadOrigin(resolved, baseUri))
        {
            throw new ModulePackageException(
                $"The store lists a download URL on a host this server does not accept "
                + $"({resolved.GetLeftPart(UriPartial.Authority)}); add it to Modules:Store:DownloadOrigins.");
        }

        return resolved;
    }

    private bool IsAllowedDownloadOrigin(Uri candidate, Uri baseUri)
    {
        if (SameOrigin(candidate, baseUri))
        {
            return true;
        }

        foreach (var origin in _options.DownloadOrigins ?? [])
        {
            if (Uri.TryCreate(origin?.Trim(), UriKind.Absolute, out var allowed)
                && SameOrigin(candidate, allowed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        Uri.Compare(
            left,
            right,
            UriComponents.SchemeAndServer,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

    private static bool IsAllowedScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        || (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && uri.IsLoopback);

    private static bool TryCreateHttpsUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) && IsAllowedScheme(parsed))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : value + "/";

    private static Uri BuildSignatureUri(Uri indexUri)
    {
        var builder = new UriBuilder(indexUri)
        {
            Path = indexUri.AbsolutePath + ModulePackageFormat.SignatureExtension,
        };

        return builder.Uri;
    }

    private static ModulePackageSignatureFile ParseSignature(byte[] signatureBytes)
    {
        try
        {
            return JsonSerializer.Deserialize<ModulePackageSignatureFile>(
                       signatureBytes, ModulePackageSigner.SignatureSerializerOptions)
                   ?? throw new ModulePackageException("The store index signature document is empty.");
        }
        catch (JsonException ex)
        {
            throw new ModulePackageException(
                $"The store index signature is not valid JSON: {ex.Message}");
        }
    }

    private bool IsIndexExpired() =>
        _time.GetUtcNow() - _indexCachedAtUtc >= _options.IndexCacheTtl;

    private static bool IsTransportFailure(Exception ex) =>
        ex is ModulePackageException
            or HttpRequestException
            or TaskCanceledException
            or IOException
            or UnauthorizedAccessException
            or JsonException;

    // ---------------------------------------------------------------------
    // Packages
    // ---------------------------------------------------------------------

    private static async Task<bool> IsCachedPackageValidAsync(
        string packagePath, ModuleStoreIndexVersion version, CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            return false;
        }

        var length = new FileInfo(packagePath).Length;
        if (length != version.Size)
        {
            return false;
        }

        var digest = await DeterministicPackageWriter.ComputeSha256Async(packagePath, cancellationToken);
        return string.Equals(digest, version.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadPackageAsync(
        Uri downloadUri,
        string packagePath,
        ModuleStoreIndexVersion version,
        CancellationToken cancellationToken)
    {
        var temporaryPath = packagePath + ".part";

        try
        {
            await DownloadToFileAsync(downloadUri, temporaryPath, version.Size, cancellationToken);

            var length = new FileInfo(temporaryPath).Length;
            if (length != version.Size)
            {
                throw new ModulePackageException(
                    $"The store published {version.Size} bytes for {version.Version} but "
                    + $"{length} bytes were downloaded.");
            }

            var digest = await DeterministicPackageWriter.ComputeSha256Async(temporaryPath, cancellationToken);
            if (!string.Equals(digest, version.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new ModulePackageException(
                    $"The download does not match the signed index: expected SHA-256 "
                    + $"{version.Sha256}, got {digest}.");
            }

            File.Move(temporaryPath, packagePath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task WriteSignatureAsync(
        string signaturePath,
        ModuleStoreIndexPackage package,
        ModuleStoreIndexVersion version,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (version.Signature is not { } signature
            || string.IsNullOrWhiteSpace(signature.KeyId)
            || string.IsNullOrWhiteSpace(signature.Value))
        {
            // No signature in the signed index: an unverified install is the only option, so a
            // signature left over from an earlier download must not be reused.
            TryDelete(signaturePath);
            return;
        }

        var document = new ModulePackageSignatureFile
        {
            KeyId = signature.KeyId,
            File = fileName,
            Sha256 = version.Sha256,
            Signature = signature.Value,
        };

        var temporaryPath = signaturePath + ".part";
        await File.WriteAllTextAsync(
            temporaryPath, ModulePackageSigner.Serialize(document) + Environment.NewLine, cancellationToken);
        File.Move(temporaryPath, signaturePath, overwrite: true);

        _logger.LogDebug(
            "Wrote the store signature for {ModuleId} {Version} (key {KeyId})",
            LogSanitizer.Sanitize(package.Id), LogSanitizer.Sanitize(version.Version),
            LogSanitizer.Sanitize(signature.KeyId));
    }

    // ---------------------------------------------------------------------
    // Disk cache
    // ---------------------------------------------------------------------

    private async Task<(ModuleStoreIndex Index, Uri DownloadBaseUri, DateTimeOffset FetchedAtUtc)?> TryReadCacheAsync(
        CancellationToken cancellationToken)
    {
        var indexPath = GetCachedIndexPath();
        var statePath = Path.Combine(Path.GetDirectoryName(indexPath)!, CacheStateFileName);

        if (!File.Exists(indexPath) || !File.Exists(statePath))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<CacheState>(
                await File.ReadAllTextAsync(statePath, cancellationToken), StateJsonOptions);

            if (state is null)
            {
                return null;
            }

            var index = ModuleStoreIndexReader.Parse(
                await File.ReadAllBytesAsync(indexPath, cancellationToken));

            var sourceUri = Uri.TryCreate(state.SourceUrl, UriKind.Absolute, out var parsed)
                ? parsed
                : GetIndexUrls()[0];

            return (index, GetDownloadBaseUri(sourceUri), state.FetchedAtUtc);
        }
        catch (Exception ex) when (ex is JsonException or IOException or ModulePackageException
                                       or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "The cached module store index at {Path} is unreadable and will be refetched: {Error}",
                indexPath, LogSanitizer.Sanitize(ex.Message));

            return null;
        }
    }

    private async Task WriteCacheAsync(
        (ModuleStoreIndex Index, Uri DownloadBaseUri, string SourceUrl) fetched, CancellationToken cancellationToken)
    {
        try
        {
            var indexPath = GetCachedIndexPath();
            var directory = Path.GetDirectoryName(indexPath)!;
            Directory.CreateDirectory(directory);

            var state = new CacheState
            {
                FetchedAtUtc = _time.GetUtcNow(),
                GeneratedAtUtc = fetched.Index.GeneratedAt,
                SourceUrl = fetched.SourceUrl,
            };

            var temporaryIndexPath = indexPath + ".part";
            var temporaryStatePath = Path.Combine(directory, CacheStateFileName) + ".part";

            // The cache holds the exact bytes that were verified; the signature is not re-checked on
            // read because only this process writes here (and a writer able to reach this directory
            // can already replace the installed modules).
            var bytes = JsonSerializer.SerializeToUtf8Bytes(fetched.Index, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await File.WriteAllBytesAsync(temporaryIndexPath, bytes, cancellationToken);

            await File.WriteAllTextAsync(
                temporaryStatePath,
                JsonSerializer.Serialize(state, StateJsonOptions) + Environment.NewLine,
                cancellationToken);

            File.Move(temporaryIndexPath, indexPath, overwrite: true);
            File.Move(temporaryStatePath, Path.Combine(directory, CacheStateFileName), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A cache that cannot be written costs a refetch next start; it must not fail the request.
            _logger.LogWarning(
                "Could not cache the module store index: {Error}", LogSanitizer.Sanitize(ex.Message));
        }
    }

    private string GetCachedIndexPath() =>
        Path.Combine(GetCacheRoot(), "index", IndexFileName);

    private string GetPackageCacheRoot() =>
        Path.Combine(GetCacheRoot(), "packages");

    private string GetCacheRoot()
    {
        var configured = _options.CachePath;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "store-cache"));
    }

    // ---------------------------------------------------------------------
    // HTTP
    // ---------------------------------------------------------------------

    private async Task<byte[]> GetBytesAsync(Uri uri, int maxBytes, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);
        using var response = await _http.GetAsync(
            uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxBytes)
        {
            throw new ModulePackageException(
                $"The response from {uri.GetLeftPart(UriPartial.Path)} is {declaredLength} bytes; the limit is {maxBytes}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream();

        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new ModulePackageException(
                    $"The response from {uri.GetLeftPart(UriPartial.Path)} exceeds the {maxBytes}-byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private async Task DownloadToFileAsync(
        Uri uri, string destinationPath, long expectedBytes, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);
        using var response = await _http.GetAsync(
            uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > expectedBytes)
        {
            throw new ModulePackageException(
                $"The download reports {declaredLength} bytes but the index published {expectedBytes}.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var destination = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(chunk, timeout.Token)) > 0)
        {
            total += read;

            if (total > expectedBytes)
            {
                throw new ModulePackageException(
                    $"The download exceeded the {expectedBytes} bytes the index published.");
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), timeout.Token);
        }
    }

    private CancellationTokenSource CreateTimeoutSource(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(_options.DownloadTimeout);
        return source;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; a leftover .part file is overwritten by the next attempt.
        }
    }

    /// <summary>The persisted cache metadata for the last verified index.</summary>
    private sealed class CacheState
    {
        /// <summary>Gets or sets when the index was fetched.</summary>
        public DateTimeOffset FetchedAtUtc { get; init; }

        /// <summary>Gets or sets the index's own <c>generatedAt</c>.</summary>
        public DateTimeOffset GeneratedAtUtc { get; init; }

        /// <summary>Gets or sets the index URL the copy came from.</summary>
        public string? SourceUrl { get; init; }
    }
}
