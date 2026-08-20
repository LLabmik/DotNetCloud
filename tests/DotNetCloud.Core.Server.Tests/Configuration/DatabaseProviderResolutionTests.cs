using DotNetCloud.Core.Data.Naming;
using Microsoft.Extensions.Configuration;

namespace DotNetCloud.Core.Server.Tests.Configuration;

/// <summary>
/// Tests for the database provider resolution used by the server at startup.
/// Regression guard: the flat config.json key (<c>databaseProvider</c>) must
/// take precedence over the appsettings.json default (<c>Database:Provider</c>),
/// otherwise a configured PostgreSQL install is treated as SQL Server.
/// </summary>
[TestClass]
public class DatabaseProviderResolutionTests
{
    [TestMethod]
    public void ResolveConfiguredDatabaseProvider_WhenFlatKeySet_PrefersFlatOverNestedDefault()
    {
        // Arrange — legacy config.json has only the flat key; appsettings.json
        // contributes a development default under Database:Provider.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["databaseProvider"] = "PostgreSQL",
                ["Database:Provider"] = "SqlServer",
            })
            .Build();

        // Act
        var provider = Program.ResolveConfiguredDatabaseProvider(configuration);

        // Assert
        Assert.AreEqual(DatabaseProvider.PostgreSQL, provider);
    }

    [TestMethod]
    public void ResolveConfiguredDatabaseProvider_WhenOnlyNestedKeySet_UsesIt()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SqlServer",
            })
            .Build();

        // Act
        var provider = Program.ResolveConfiguredDatabaseProvider(configuration);

        // Assert
        Assert.AreEqual(DatabaseProvider.SqlServer, provider);
    }

    [TestMethod]
    public void ResolveConfiguredDatabaseProvider_WhenNeitherKeySet_Throws()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>(
            () => Program.ResolveConfiguredDatabaseProvider(configuration));
    }
}
