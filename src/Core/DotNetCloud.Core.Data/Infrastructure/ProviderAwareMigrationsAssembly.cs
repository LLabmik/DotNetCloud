using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;

#pragma warning disable EF1001 // MigrationsAssembly is an internal API — intentional override for provider-aware migration filtering

namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// Filters discovered EF Core migrations by database provider so that a single
/// assembly can safely contain both PostgreSQL (<c>...Data.Migrations</c>) and
/// SQL Server (<c>...Data.SqlServer.Migrations</c>) migration sets without
/// the runtime trying to apply the wrong provider's migrations.
/// </summary>
/// <remarks>
/// Replaces <see cref="IMigrationsAssembly"/> via
/// <c>options.ReplaceService&lt;IMigrationsAssembly, ProviderAwareMigrationsAssembly&gt;()</c>.
/// </remarks>
public class ProviderAwareMigrationsAssembly : MigrationsAssembly
{
    private readonly bool _isSqlServer;

    /// <summary>
    /// Creates a new provider-aware migrations assembly that filters
    /// discovered migrations to only those matching the active provider.
    /// </summary>
    public ProviderAwareMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger)
    {
        _isSqlServer = options.Extensions.Any(e =>
            e.GetType().FullName == "Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal.SqlServerOptionsExtension");
    }

    /// <summary>
    /// Returns only migrations whose namespace matches the active provider.
    /// SQL Server → namespaces containing ".SqlServer."
    /// PostgreSQL → namespaces NOT containing ".SqlServer."
    /// </summary>
    public override IReadOnlyDictionary<string, TypeInfo> Migrations
    {
        get
        {
            var all = base.Migrations;
            if (_isSqlServer)
            {
                return all.Where(kv =>
                        kv.Value.Namespace?.Contains(".SqlServer.", StringComparison.Ordinal) == true)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            return all.Where(kv =>
                    kv.Value.Namespace?.Contains(".SqlServer.", StringComparison.Ordinal) != true)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }


}
