namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Process-wide paths and environment shared by the integration test host.
/// </summary>
/// <remarks>
/// The data directory must be in place before any host starts: <c>Program</c> resolves the file
/// storage, temp and OIDC key directories from <c>DOTNETCLOUD_DATA_DIR</c>, and the backup service
/// falls back to <c>/var/lib/dotnetcloud</c> when it is unset. On a machine with a real install that
/// path is either unwritable (the tests fail) or readable (the tests would copy live production data
/// into the backup archive).
/// </remarks>
internal static class IntegrationTestEnvironment
{
    private static readonly Lazy<string> s_dataDirectory = new(CreateDataDirectory);

    /// <summary>
    /// Gets the data directory the test host uses for storage, temp files and backups.
    /// </summary>
    public static string DataDirectory => s_dataDirectory.Value;

    /// <summary>
    /// Creates the data directory and exports it as <c>DOTNETCLOUD_DATA_DIR</c>.
    /// </summary>
    public static void Initialize()
    {
        _ = DataDirectory;
    }

    private static string CreateDataDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotnetcloud-integration-data");
        Directory.CreateDirectory(path);
        Environment.SetEnvironmentVariable("DOTNETCLOUD_DATA_DIR", path);
        return path;
    }
}

/// <summary>
/// Assembly-level setup for the integration test suite.
/// </summary>
[TestClass]
public sealed class IntegrationTestAssemblySetup
{
    /// <summary>
    /// Points <c>DOTNETCLOUD_DATA_DIR</c> at a temp directory before any host is built.
    /// </summary>
    /// <param name="context">Test context supplied by MSTest.</param>
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        _ = context;
        IntegrationTestEnvironment.Initialize();
    }
}
