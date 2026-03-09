namespace DotNetCloud.Client.Core.Conflict;

/// <summary>Merge capability of a file type.</summary>
public enum FileMergeMode
{
    /// <summary>Text file — line-based diff and three-way merge with DiffPlex.</summary>
    Text,

    /// <summary>XML family — structure-aware merge using System.Xml.Linq.</summary>
    Xml,

    /// <summary>Binary or structured binary — no merge; keep-local / keep-server / keep-both only.</summary>
    Binary,
}

/// <summary>
/// Classifies files by merge capability based on file extension.
/// </summary>
public static class FileTypeClassifier
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".yaml", ".yml", ".csv", ".tsv",
        ".html", ".css", ".js", ".ts", ".cs", ".py", ".java",
        ".c", ".cpp", ".h", ".sh", ".ps1", ".sql",
        ".ini", ".cfg", ".conf", ".toml", ".env", ".log",
        ".gitignore", ".dockerignore", ".editorconfig", ".gitattributes",
        ".bat", ".cmd", ".vbs", ".rb", ".php", ".go", ".rs", ".kt", ".swift",
        ".dart", ".lua", ".pl", ".r", ".m", ".asm", ".s",
    };

    private static readonly HashSet<string> XmlExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xml", ".csproj", ".fsproj", ".vbproj", ".props", ".targets",
        ".xaml", ".axaml", ".svg", ".xslt", ".xsl", ".xsd", ".wsdl",
        ".config", ".resx", ".nuspec", ".pubxml",
    };

    /// <summary>Returns the merge mode suitable for the given file path.</summary>
    public static FileMergeMode GetMergeMode(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return FileMergeMode.Binary;
        if (XmlExtensions.Contains(ext)) return FileMergeMode.Xml;
        if (TextExtensions.Contains(ext)) return FileMergeMode.Text;
        return FileMergeMode.Binary;
    }

    /// <summary>Returns true if the file is a text-based type (Text or Xml).</summary>
    public static bool IsTextBased(string filePath) =>
        GetMergeMode(filePath) != FileMergeMode.Binary;
}
