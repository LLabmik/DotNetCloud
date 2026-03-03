using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Integration;

/// <summary>
/// Integration tests for multi-database provider support.
/// </summary>
[TestClass]
public class MultiDatabaseTests
{
    [TestMethod]
    public void DatabaseProvider_PostgreSQL_IsDetected()
    {
        // Arrange
        var connectionString = "Host=localhost;Port=5432;Database=testdb;Username=postgres;Password=password";

        // Act
        var provider = DatabaseProviderDetector.DetectProvider(connectionString);

        // Assert
        Assert.AreEqual(DatabaseProvider.PostgreSQL, provider, "PostgreSQL connection string should be detected");
    }

    [TestMethod]
    public void DatabaseProvider_SqlServer_IsDetected()
    {
        // Arrange - Use unambiguous SQL Server connection strings
        var connectionStrings = new[]
        {
            "Data Source=localhost;Database=testdb;User Id=sa;Password=password",
            "Data Source=localhost;Initial Catalog=testdb;Integrated Security=true"
        };

        // Act & Assert
        foreach (var connectionString in connectionStrings)
        {
            var provider = DatabaseProviderDetector.DetectProvider(connectionString);
            Assert.AreEqual(DatabaseProvider.SqlServer, provider, $"SQL Server connection string should be detected: {connectionString}");
        }
    }

    [TestMethod]
    public void DatabaseProvider_MariaDB_IsDetected()
    {
        // Arrange
        var connectionStrings = new[]
        {
            "Server=localhost;Port=3306;Database=testdb;User=root;Password=password",
            "Server=localhost;Database=testdb;User Id=root;Password=password"
        };

        // Act & Assert
        foreach (var connectionString in connectionStrings)
        {
            var provider = DatabaseProviderDetector.DetectProvider(connectionString);
            Assert.AreEqual(DatabaseProvider.MariaDB, provider, $"MariaDB connection string should be detected: {connectionString}");
        }
    }

    [TestMethod]
    public void NamingStrategy_PostgreSQL_UsesSchemasAndSnakeCase()
    {
        // Arrange
        var strategy = new PostgreSqlNamingStrategy();

        // Act
        var tableName = strategy.GetTableName("Organization", "core");
        var columnName = strategy.GetColumnName("DisplayName");

        // Assert
        Assert.AreEqual("core.organization", tableName, "PostgreSQL should use schema-qualified snake_case");
        Assert.AreEqual("display_name", columnName, "PostgreSQL should use snake_case");
    }

    [TestMethod]
    public void NamingStrategy_SqlServer_UsesPascalCaseAndSchemas()
    {
        // Arrange
        var strategy = new SqlServerNamingStrategy();

        // Act
        var tableName = strategy.GetTableName("Organization", "core");
        var columnName = strategy.GetColumnName("DisplayName");

        // Assert
        Assert.AreEqual("[core].[Organization]", tableName, "SQL Server should use schema-qualified PascalCase");
        Assert.AreEqual("DisplayName", columnName, "SQL Server should use PascalCase");
    }

    [TestMethod]
    public void NamingStrategy_MariaDB_UsesPrefixesAndSnakeCase()
    {
        // Arrange
        var strategy = new MariaDbNamingStrategy();

        // Act
        var tableName = strategy.GetTableName("Organization", "core");
        var columnName = strategy.GetColumnName("DisplayName");

        // Assert
        Assert.AreEqual("core_organization", tableName, "MariaDB should use prefixed snake_case");
        Assert.AreEqual("display_name", columnName, "MariaDB should use snake_case");
    }

    [TestMethod]
    public void MultiDatabase_PostgreSQL_CreatesContextSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("psql_test")
            .Options;

        // Act
        using var context = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        // Assert
        Assert.IsNotNull(context, "PostgreSQL context should be created");
    }

    [TestMethod]
    public void CoreDbContext_WithMultipleDatabases_MaintainsConsistentSchema()
    {
        // Arrange - Create contexts with different naming strategies
        var psqlOptions = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("psql_test")
            .Options;

        var sqlServerOptions = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("sqlserver_test")
            .Options;

        // Act
        var psqlContext = new CoreDbContext(psqlOptions, new PostgreSqlNamingStrategy());
        var sqlServerContext = new CoreDbContext(sqlServerOptions, new SqlServerNamingStrategy());

        // Assert - Both contexts should have same entities configured
        var psqlEntityTypes = psqlContext.Model.GetEntityTypes().Select(e => e.Name).ToList();
        var sqlServerEntityTypes = sqlServerContext.Model.GetEntityTypes().Select(e => e.Name).ToList();

        Assert.AreEqual(psqlEntityTypes.Count, sqlServerEntityTypes.Count, 
            "Both providers should configure same number of entities");
        
        foreach (var entityType in psqlEntityTypes)
        {
            Assert.IsTrue(sqlServerEntityTypes.Contains(entityType),
                $"SQL Server context should include entity type: {entityType}");
        }
    }

    [TestMethod]
    public async Task MultiDatabase_InMemory_HandlesDataIdentically()
    {
        // Arrange - Create two in-memory contexts
        var options1 = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"TestDb1_{Guid.NewGuid()}")
            .Options;

        var options2 = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"TestDb2_{Guid.NewGuid()}")
            .Options;

        var context1 = new CoreDbContext(options1, new PostgreSqlNamingStrategy());
        var context2 = new CoreDbContext(options2, new SqlServerNamingStrategy());

        // Act - Create identical data in both contexts
        var org1 = new Organization { Name = "Test Org", Description = "A test organization" };
        var org2 = new Organization { Name = "Test Org", Description = "A test organization" };

        context1.Organizations.Add(org1);
        context2.Organizations.Add(org2);

        await context1.SaveChangesAsync();
        await context2.SaveChangesAsync();

        // Assert - Verify identical results from both
        var orgs1 = await context1.Organizations.ToListAsync();
        var orgs2 = await context2.Organizations.ToListAsync();

        Assert.AreEqual(1, orgs1.Count, "Context 1 should have 1 organization");
        Assert.AreEqual(1, orgs2.Count, "Context 2 should have 1 organization");
        Assert.AreEqual(orgs1[0].Name, orgs2[0].Name, "Organization names should match");
        Assert.AreEqual(orgs1[0].Description, orgs2[0].Description, "Organization descriptions should match");

        context1.Dispose();
        context2.Dispose();
    }

    [TestMethod]
    public void NamingStrategy_ConsistencyAcrossProviders_IndexNaming()
    {
        // Arrange
        var strategies = new ITableNamingStrategy[]
        {
            new PostgreSqlNamingStrategy(),
            new SqlServerNamingStrategy(),
            new MariaDbNamingStrategy()
        };

        // Act & Assert
        foreach (var strategy in strategies)
        {
            var indexName = strategy.GetIndexName("Users", "UserName");
            Assert.IsFalse(string.IsNullOrWhiteSpace(indexName), 
                $"{strategy.GetType().Name} should generate index names");
        }
    }

    [TestMethod]
    public void DatabaseProviderDetector_UnknownProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var unknownConnectionString = "some-unknown-connection-string";

        // Act & Assert
        try
        {
            DatabaseProviderDetector.DetectProvider(unknownConnectionString);
            Assert.Fail("Should have thrown InvalidOperationException for unknown provider");
        }
        catch (InvalidOperationException)
        {
            // Expected — detector cannot determine provider from unknown connection string
        }
    }

    [TestMethod]
    public void NamingStrategy_ForeignKeyNaming_IsConsistent()
    {
        // Arrange
        var strategies = new ITableNamingStrategy[]
        {
            new PostgreSqlNamingStrategy(),
            new SqlServerNamingStrategy(),
            new MariaDbNamingStrategy()
        };

        // Act & Assert
        foreach (var strategy in strategies)
        {
            var fkName = strategy.GetForeignKeyName("Organizations", "Teams", "OrganizationId");
            Assert.IsFalse(string.IsNullOrWhiteSpace(fkName),
                $"{strategy.GetType().Name} should generate foreign key names");
            
            // Foreign key names should be reasonably short (database limits)
            Assert.IsTrue(fkName.Length <= 128,
                $"{strategy.GetType().Name} foreign key name should respect database limits");
        }
    }
}
