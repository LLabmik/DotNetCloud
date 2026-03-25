using Microsoft.Data.SqlClient;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Detects a local SQL Server instance (e.g., SQL Server Express) on the host machine.
/// Uses Windows Authentication and creates an isolated test database per session.
/// </summary>
/// <remarks>
/// This enables SQL Server integration tests on Windows dev machines where Docker-based
/// SQL Server containers crash due to WSL2 kernel incompatibilities. The detector tries
/// the local default instance first; if unavailable, tests fall back to Docker containers
/// (which may also be unavailable on WSL2).
/// </remarks>
internal static class LocalSqlServerDetector
{
    private const string TestDatabasePrefix = "dotnetcloud_test_";
    private const string ExternalConnectionStringEnvVar = "DOTNETCLOUD_TEST_SQLSERVER_CONNECTION_STRING";

    private static bool s_detectionDone;
    private static string? s_connectionString;
    private static bool s_useExternalConnectionString;

    /// <summary>
    /// Gets the connection string for a local SQL Server instance, or <see langword="null"/>
    /// if no local instance is reachable.
    /// </summary>
    public static string? ConnectionString => s_connectionString;

    /// <summary>
    /// Probes for a local SQL Server default instance using Windows Authentication.
    /// Creates an isolated test database with a unique name per test session.
    /// The result is cached — detection runs only once per process.
    /// </summary>
    /// <returns><see langword="true"/> if a local SQL Server is available and the test database was created.</returns>
    public static async Task<bool> TryDetectAsync(CancellationToken cancellationToken = default)
    {
        if (s_detectionDone)
        {
            return s_connectionString is not null;
        }

        s_detectionDone = true;

        // Highest priority: explicit external SQL Server connection string
        // (supports Linux/macOS CI runners and network SQL Server instances).
        var externalConnectionString = Environment.GetEnvironmentVariable(ExternalConnectionStringEnvVar);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(externalConnectionString)
                {
                    ConnectTimeout = 5,
                    TrustServerCertificate = true,
                };

                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                s_connectionString = builder.ConnectionString;
                s_useExternalConnectionString = true;
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // Only attempt on Windows — Windows Authentication is not available on Linux/macOS
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // Use "." (shared memory) instead of "localhost" (TCP) because SQL Server Express
        // ships with TCP/IP disabled by default.
        var masterConnectionString =
            "Data Source=.;Initial Catalog=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5";

        try
        {
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Connection succeeded — create an isolated test database
            var dbName = TestDatabasePrefix + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                IF DB_ID('{dbName}') IS NULL
                    CREATE DATABASE [{dbName}]
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            s_connectionString =
                $"Data Source=.;Initial Catalog={dbName};Integrated Security=true;TrustServerCertificate=true";

            return true;
        }
        catch (SqlException)
        {
            // Local SQL Server not available
            return false;
        }
        catch (InvalidOperationException)
        {
            // Connection pool exhausted or other transient issue
            return false;
        }
    }

    /// <summary>
    /// Drops the test database created during detection. Call from test cleanup.
    /// </summary>
    public static async Task CleanupAsync()
    {
        if (s_connectionString is null)
        {
            return;
        }

        // External connection strings point to user-managed databases.
        // Never attempt destructive cleanup in that case.
        if (s_useExternalConnectionString)
        {
            return;
        }

        // Extract the database name from the connection string
        var builder = new SqlConnectionStringBuilder(s_connectionString);
        var dbName = builder.InitialCatalog;

        if (string.IsNullOrEmpty(dbName) || !dbName.StartsWith(TestDatabasePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var masterConnectionString =
            "Data Source=.;Initial Catalog=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5";

        try
        {
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                IF DB_ID('{dbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                END
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Best-effort cleanup — don't fail tests if drop fails
        }
    }
}
