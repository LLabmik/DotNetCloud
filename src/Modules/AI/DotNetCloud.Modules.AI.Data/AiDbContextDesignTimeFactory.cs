using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.AI.Data;

/// <summary>
/// Design-time factory for <see cref="AiDbContext"/> to support EF Core migrations.
/// </summary>
public sealed class AiDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    /// <inheritdoc />
    public AiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=dotnetcloud_ai;Username=dotnetcloud;Password=dev");

        return new AiDbContext(optionsBuilder.Options);
    }
}
