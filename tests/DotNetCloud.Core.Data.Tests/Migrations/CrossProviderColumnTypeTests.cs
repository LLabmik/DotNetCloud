using System.Text.RegularExpressions;

namespace DotNetCloud.Core.Data.Tests.Migrations;

/// <summary>
/// Cross-provider schema hygiene: no entity configuration may pin a column to a SQL Server
/// non-comparable LOB type.
/// </summary>
/// <remarks>
/// <para>
/// <c>HasColumnType("text")</c> is the PostgreSQL way to say "unbounded string" and reads naturally in an
/// entity configuration — but the same annotation is applied verbatim to the SQL Server model, where
/// <c>text</c>/<c>ntext</c>/<c>image</c> are the deprecated, non-comparable LOB types. Any
/// <c>DISTINCT</c>, <c>GROUP BY</c>, <c>ORDER BY</c> or <c>=</c> that touches such a column fails at
/// runtime with <c>SqlException 80131904: The text data type cannot be selected as DISTINCT because
/// it is not comparable</c> — which is exactly how opening a contact started answering 500.
/// </para>
/// <para>
/// Leaving the type unconfigured is correct on both engines: EF maps an unbounded <see langword="string"/>
/// to <c>nvarchar(max)</c> on SQL Server and <c>text</c> on PostgreSQL. PostgreSQL-native types that
/// have no SQL Server equivalent (e.g. <c>jsonb</c>) stay explicit and are translated by
/// <see cref="DotNetCloud.Core.Data.Extensions.ModelBuilderExtensions.FixColumnTypesForSqlServer"/>.
/// </para>
/// </remarks>
[TestClass]
public class CrossProviderColumnTypeTests
{
    private static readonly Regex NonComparableLobType = new(
        "HasColumnType\\(\\s*\"(?:text|ntext|image)\"\\s*\\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Walks every entity configuration in <c>src/</c> (migrations excluded — they record history) and
    /// fails if one pins a column to a non-comparable LOB type.
    /// </summary>
    [TestMethod]
    public void NoEntityConfigurationPinsANonComparableLobColumnType()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repoRoot, "src");

        var files = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var normalized = path.Replace('\\', '/');
                return !normalized.Contains("/obj/", StringComparison.Ordinal)
                    && !normalized.Contains("/bin/", StringComparison.Ordinal)
                    && !normalized.Contains("/Migrations/", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.IsTrue(files.Length > 100, $"Expected to scan the source tree, found {files.Length} file(s).");

        var offenders = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (NonComparableLobType.IsMatch(lines[index]))
                {
                    offenders.Add(
                        $"{Path.GetRelativePath(repoRoot, file).Replace('\\', '/')}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "These configurations pin a column to a SQL Server non-comparable LOB type (text/ntext/image). "
            + "Drop the explicit type so EF uses nvarchar(max) on SQL Server and text on PostgreSQL:"
            + string.Concat(offenders.Select(offender => $"{Environment.NewLine}  {offender}")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DotNetCloud.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Could not locate DotNetCloud.sln above {AppContext.BaseDirectory}.");
        return string.Empty;
    }
}
