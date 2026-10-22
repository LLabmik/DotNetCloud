namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for the administrator-managed default user content template.
/// </summary>
public sealed class DefaultUserContentOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:DefaultUserContent";

    /// <summary>Directory name under the data directory.</summary>
    public const string DirectoryName = "default-user-content";

    /// <summary>Master switch. When disabled every provisioning path is a no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Explicit template directory. Required on Windows (where <c>DOTNETCLOUD_DATA_DIR</c>
    /// is not set); empty means resolve from the data directory or content root.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Maximum number of files copied from the template.</summary>
    public int MaxFiles { get; set; } = 500;

    /// <summary>Maximum total size of the template in bytes (default 5 GiB).</summary>
    public long MaxTotalBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>Maximum size of a single template file in bytes (default 500 MiB).</summary>
    public long MaxFileBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>Maximum directory nesting depth.</summary>
    public int MaxDepth { get; set; } = 8;

    /// <summary>Per-user reconciliation budget.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Skip dotfiles and OS metadata files.</summary>
    public bool SkipHiddenFiles { get; set; } = true;

    /// <summary>Creates a copy (overlays never mutate the shared options instance).</summary>
    /// <param name="source">Source options.</param>
    /// <returns>A shallow copy.</returns>
    public static DefaultUserContentOptions Copy(DefaultUserContentOptions source) => new()
    {
        Enabled = source.Enabled,
        RootPath = source.RootPath,
        MaxFiles = source.MaxFiles,
        MaxTotalBytes = source.MaxTotalBytes,
        MaxFileBytes = source.MaxFileBytes,
        MaxDepth = source.MaxDepth,
        Timeout = source.Timeout,
        SkipHiddenFiles = source.SkipHiddenFiles
    };

    /// <summary>
    /// Applies administrator-editable overlay values onto <paramref name="options"/>.
    /// Absent keys keep the configuration baseline.
    /// </summary>
    /// <param name="options">Options to update in place.</param>
    /// <param name="values">Stored values keyed by setting name.</param>
    public static void Apply(DefaultUserContentOptions options, IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue(AdminSettings.EnabledKey, out var enabledText)
            && bool.TryParse(enabledText, out var enabled))
        {
            options.Enabled = enabled;
        }

        if (values.TryGetValue(AdminSettings.RootPathKey, out var rootPath)
            && !string.IsNullOrWhiteSpace(rootPath))
        {
            options.RootPath = rootPath.Trim();
        }

        static int? Int(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var text) && int.TryParse(text, out var parsed) && parsed > 0
                ? parsed
                : null;

        static long? Long(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var text) && long.TryParse(text, out var parsed) && parsed > 0
                ? parsed
                : null;

        options.MaxFiles = Int(values, AdminSettings.MaxFilesKey) ?? options.MaxFiles;
        options.MaxTotalBytes = Long(values, AdminSettings.MaxTotalBytesKey) ?? options.MaxTotalBytes;
        options.MaxFileBytes = Long(values, AdminSettings.MaxFileBytesKey) ?? options.MaxFileBytes;
        options.MaxDepth = Int(values, AdminSettings.MaxDepthKey) ?? options.MaxDepth;
    }

    /// <summary>Setting keys understood by the admin overlay.</summary>
    public static class AdminSettings
    {
        /// <summary>Module that owns the default-content admin settings.</summary>
        public const string ModuleId = "dotnetcloud.files";

        /// <summary>Master switch key.</summary>
        public const string EnabledKey = "DefaultUserContent:Enabled";

        /// <summary>Explicit template root key.</summary>
        public const string RootPathKey = "DefaultUserContent:RootPath";

        /// <summary>Template file cap key.</summary>
        public const string MaxFilesKey = "DefaultUserContent:MaxFiles";

        /// <summary>Template total-size cap key.</summary>
        public const string MaxTotalBytesKey = "DefaultUserContent:MaxTotalBytes";

        /// <summary>Per-file cap key.</summary>
        public const string MaxFileBytesKey = "DefaultUserContent:MaxFileBytes";

        /// <summary>Nesting-depth cap key.</summary>
        public const string MaxDepthKey = "DefaultUserContent:MaxDepth";

        /// <summary>All keys the overlay understands.</summary>
        public static readonly IReadOnlyList<string> Keys =
        [
            EnabledKey, RootPathKey, MaxFilesKey, MaxTotalBytesKey, MaxFileBytesKey, MaxDepthKey
        ];
    }
}
