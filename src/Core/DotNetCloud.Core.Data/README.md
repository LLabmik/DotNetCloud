# DotNetCloud.Core.Data

Data access layer for the DotNetCloud core system, providing multi-database provider support with automatic naming strategy application.

## Overview

The `DotNetCloud.Core.Data` project provides:

- **Multi-Database Provider Support**: PostgreSQL, SQL Server, and MariaDB/MySQL
- **Automatic Naming Strategies**: Provider-specific naming conventions automatically applied
- **DbContext Factory Pattern**: Centralized context creation and configuration
- **Provider Detection**: Automatic database provider detection from connection strings
- **Entity Framework Core Integration**: Full EF Core support with migrations

## Database Providers

### PostgreSQL

- **Schema Naming**: Lowercase module names (e.g., `core`, `files`, `chat`)
- **Table Naming**: Snake_case (e.g., `application_user`, `team_member`)
- **Column Naming**: Snake_case
- **Indexes**: Lowercase with underscores (e.g., `idx_application_user_email`)
- **Constraints**: Lowercase with prefix (e.g., `fk_`, `uq_`)

**Connection String Examples:**
```
Host=localhost;Database=dotnetcloud;Username=postgres;Password=password
Server=postgres.example.com;Database=dotnetcloud_prod;User Id=app;Password=secret
```

### SQL Server

- **Schema Naming**: Lowercase module names (e.g., `[core]`, `[files]`, `[chat]`)
- **Table Naming**: PascalCase (e.g., `[core].[ApplicationUser]`)
- **Column Naming**: PascalCase
- **Indexes**: PascalCase with prefix (e.g., `IX_ApplicationUser_Email`)
- **Constraints**: PascalCase with prefix (e.g., `FK_`, `UQ_`)

**Connection String Examples:**
```
Server=(local);Database=DotNetCloud;Trusted_Connection=true;
Data Source=sqlserver.example.com;Initial Catalog=DotNetCloud_Prod;User Id=sa;Password=Secret123!
```

### MariaDB/MySQL

- **Table Naming**: Table prefix with lowercase module names (e.g., `core_application_user`, `files_file_entry`)
- **Column Naming**: Snake_case
- **Indexes**: Lowercase with truncation for 64-character identifier limit
- **Constraints**: Lowercase with truncation for MySQL identifier limits

**Connection String Examples:**
```
Server=localhost;Port=3306;Database=dotnetcloud;User Id=root;Password=password;
Server=mysql.example.com;Port=3306;Database=dotnetcloud_prod;User=app;Password=secret;
```

## Architecture

### Provider Detection

The `DatabaseProviderDetector` class automatically detects the database provider from connection strings:

```csharp
var provider = DatabaseProviderDetector.DetectProvider(connectionString);
// Returns: DatabaseProvider.PostgreSQL, DatabaseProvider.SqlServer, or DatabaseProvider.MariaDB
```

Detection logic:
- **PostgreSQL**: Looks for `Host=` or `Server=postgresql` keywords
- **SQL Server**: Looks for `Data Source=` or `Server=` without PostgreSQL indicators
- **MariaDB**: Looks for port 3306 or MySQL-specific keywords

### Naming Strategies

Each database provider has a corresponding naming strategy implementing `ITableNamingStrategy`:

- `PostgreSqlNamingStrategy`: Schema-based organization with snake_case naming
- `SqlServerNamingStrategy`: Schema-based organization with PascalCase naming
- `MariaDbNamingStrategy`: Table prefix-based organization with snake_case naming and truncation support

**Core Methods:**
- `GetSchemaForModule(moduleName)`: Returns the schema/prefix for a module
- `GetTableName(entityName, moduleName)`: Returns the full table name
- `GetColumnName(propertyName)`: Returns the column name
- `GetIndexName(...)`: Returns the index name
- `GetForeignKeyName(...)`: Returns the foreign key name
- `GetUniqueConstraintName(...)`: Returns the unique constraint name

### DbContext Factory

The factory pattern provides centralized context creation:

```csharp
IDbContextFactory factory = new DefaultDbContextFactory(connectionString);
CoreDbContext context = factory.CreateDbContext();
```

The factory automatically:
1. Detects the database provider from the connection string
2. Creates the appropriate naming strategy instance
3. Configures EF Core with provider-specific options (retry policies, timeouts, etc.)
4. Applies naming strategies to the context

## Usage

### Creating a DbContext

```csharp
// Automatic provider detection
var factory = new DefaultDbContextFactory(connectionString);
var context = factory.CreateDbContext();

// Or explicit provider specification (useful for testing)
var factory = new DefaultDbContextFactory(connectionString, DatabaseProvider.PostgreSQL);
var context = factory.CreateDbContext();
```

### Dependency Injection

Register the factory in the service container:

```csharp
services.AddScoped<IDbContextFactory>(sp =>
    new DefaultDbContextFactory(configuration.GetConnectionString("Default")));

services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory>().CreateDbContext());
```

### Entity Configuration Example

When creating entity configurations in `CoreDbContext.OnModelCreating()`:

```csharp
// The naming strategy is automatically applied to all entities
// Developers simply configure relationships and constraints

modelBuilder.Entity<ApplicationUser>(entity =>
{
    entity.HasKey(u => u.Id);
    entity.HasIndex(u => u.Email).IsUnique();
    
    // Naming strategy automatically handles:
    // - Table name (different for each provider)
    // - Column names (provider-specific casing)
    // - Index names (provider-specific format)
});
```

## Integration Points

### With DotNetCloud.Core

- Uses `CallerContext` from core for audit trail support
- Uses `IModule` manifest for module-scoped entities
- Uses capability interfaces for data access permission checking

### With EF Core Migrations

Migration files are provider-specific:

```
Migrations/
├── 20240101000001_InitialCreate_PostgreSQL.cs
├── 20240101000001_InitialCreate_SqlServer.cs
└── 20240101000001_InitialCreate_MariaDB.cs
```

## Performance Considerations

- **Connection Pooling**: Configured per provider with default pool size of 10
- **Retry Logic**: Automatic retry on transient failures (up to 3 retries)
- **Command Timeout**: 30-second timeout per command
- **Lazy Loading**: Consider explicit loading or projection for performance

## Security

- Connection strings stored securely (user secrets in development, Azure Key Vault in production)
- Parameterized queries prevent SQL injection (handled by EF Core)
- Identifier truncation in MariaDB preserves query uniqueness via hash suffix
- Audit logging supported via `CoreDbContext` integration

## Testing

The factory supports easy testing with different providers:

```csharp
[TestClass]
public class DataAccessTests
{
    private IDbContextFactory _postgresFactory;
    private IDbContextFactory _sqlServerFactory;
    private IDbContextFactory _mariadbFactory;

    [TestInitialize]
    public void Setup()
    {
        _postgresFactory = new DefaultDbContextFactory(PostgresConnectionString);
        _sqlServerFactory = new DefaultDbContextFactory(SqlServerConnectionString);
        _mariadbFactory = new DefaultDbContextFactory(MariadbConnectionString);
    }

    [TestMethod]
    public void TestWithPostgreSQL()
    {
        var context = _postgresFactory.CreateDbContext();
        // Test logic...
    }

    [TestMethod]
    public void TestWithSqlServer()
    {
        var context = _sqlServerFactory.CreateDbContext();
        // Test logic...
    }

    [TestMethod]
    public void TestWithMariaDB()
    {
        var context = _mariadbFactory.CreateDbContext();
        // Test logic...
    }
}
```

## Extension Points

### Adding a New Database Provider

To add support for a new database provider:

1. Create a new enum value in `DatabaseProvider`
2. Implement a new naming strategy class extending `ITableNamingStrategy`
3. Add detection logic to `DatabaseProviderDetector.DetectProvider()`
4. Add provider configuration to `DefaultDbContextFactory.ConfigureDbContextOptions()`
5. Add migration files for the new provider

### Custom Naming Strategies

Applications can implement custom naming strategies:

```csharp
public class CustomNamingStrategy : ITableNamingStrategy
{
    public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;
    
    // Implement interface methods with custom logic...
}

// Use in factory
var factory = new DefaultDbContextFactory(connectionString)
{
    NamingStrategy = new CustomNamingStrategy()
};
```

## Related Documentation

- [Core Abstractions](../architecture/core-abstractions.md) - Core interfaces and types
- [Architecture Overview](../architecture/ARCHITECTURE.md) - System-wide architecture
- [Phase 0.2 Specification](../MASTER_PROJECT_PLAN.md#phase-02-database--data-access-layer) - Requirements for this phase
