using System.Data.Common;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.ServiceDefaults.HealthChecks;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Creates raw ADO.NET connections for health probes using the configured
/// provider and connection string.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;

    /// <summary>Creates a connection factory for the given provider and connection string.</summary>
    public DbConnectionFactory(string connectionString, DatabaseProvider provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    /// <inheritdoc />
    public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        DbConnection connection = _provider == DatabaseProvider.SqlServer
            ? new Microsoft.Data.SqlClient.SqlConnection(_connectionString)
            : new Npgsql.NpgsqlConnection(_connectionString);

        return Task.FromResult(connection);
    }
}
