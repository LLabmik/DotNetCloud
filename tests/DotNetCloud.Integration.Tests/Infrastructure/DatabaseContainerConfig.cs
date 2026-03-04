namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Configuration for Docker-based database containers used in integration tests.
/// </summary>
internal sealed class DatabaseContainerConfig
{
    /// <summary>
    /// Gets the Docker image name (e.g., "postgres:16", "mcr.microsoft.com/mssql/server:2022-latest").
    /// </summary>
    public required string ImageName { get; init; }

    /// <summary>
    /// Gets the container port that the database listens on.
    /// </summary>
    public required int ContainerPort { get; init; }

    /// <summary>
    /// Gets the environment variables to pass to the container.
    /// </summary>
    public required IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }

    /// <summary>
    /// Gets a function that builds a connection string given the host port.
    /// </summary>
    public required Func<int, string> ConnectionStringFactory { get; init; }

    /// <summary>
    /// Gets the health check command to run inside the container.
    /// </summary>
    public required string HealthCheckCommand { get; init; }

    /// <summary>
    /// Gets the default database name.
    /// </summary>
    public required string DatabaseName { get; init; }

    /// <summary>
    /// Preset configuration for PostgreSQL 16.
    /// </summary>
    public static DatabaseContainerConfig PostgreSql() => new()
    {
        ImageName = "postgres:16",
        ContainerPort = 5432,
        DatabaseName = "dotnetcloud_test",
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["POSTGRES_USER"] = "testuser",
            ["POSTGRES_PASSWORD"] = "testpass",
            ["POSTGRES_DB"] = "dotnetcloud_test",
        },
        ConnectionStringFactory = hostPort =>
            $"Host=localhost;Port={hostPort};Database=dotnetcloud_test;Username=testuser;Password=testpass",
        HealthCheckCommand = "pg_isready -U testuser -d dotnetcloud_test",
    };

    /// <summary>
    /// Preset configuration for SQL Server 2022.
    /// </summary>
    public static DatabaseContainerConfig SqlServer() => new()
    {
        ImageName = "mcr.microsoft.com/mssql/server:2022-latest",
        ContainerPort = 1433,
        DatabaseName = "dotnetcloud_test",
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["ACCEPT_EULA"] = "Y",
            ["SA_PASSWORD"] = "TestP@ssw0rd!",
            ["MSSQL_PID"] = "Developer",
        },
        ConnectionStringFactory = hostPort =>
            $"Data Source=localhost,{hostPort};Initial Catalog=dotnetcloud_test;User Id=sa;Password=TestP@ssw0rd!;TrustServerCertificate=true",
        HealthCheckCommand = "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P TestP@ssw0rd! -C -Q \"SELECT 1\"",
    };

    /// <summary>
    /// Preset configuration for MariaDB 11.
    /// </summary>
    public static DatabaseContainerConfig MariaDb() => new()
    {
        ImageName = "mariadb:11",
        ContainerPort = 3306,
        DatabaseName = "dotnetcloud_test",
        EnvironmentVariables = new Dictionary<string, string>
        {
            ["MYSQL_ROOT_PASSWORD"] = "testpass",
            ["MYSQL_DATABASE"] = "dotnetcloud_test",
            ["MYSQL_USER"] = "testuser",
            ["MYSQL_PASSWORD"] = "testpass",
        },
        ConnectionStringFactory = hostPort =>
            $"Server=localhost;Port={hostPort};Database=dotnetcloud_test;User=testuser;Password=testpass",
        HealthCheckCommand = "mariadb-admin ping -h localhost -u testuser --password=testpass",
    };
}
