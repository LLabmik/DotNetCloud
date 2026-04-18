# Database Setup Guide

> **Purpose:** Configure local database instances for DotNetCloud development and testing  
> **Audience:** Developers, DevOps, QA engineers  
> **Last Updated:** 2026-03-02

---

## Table of Contents

1. [Overview](#overview)
2. [PostgreSQL Setup](#postgresql-setup)
3. [SQL Server Setup](#sql-server-setup)
4. [MariaDB Setup](#mariadb-setup)
5. [Multi-Database Testing](#multi-database-testing)
6. [Connection Strings](#connection-strings)
7. [Database Initialization](#database-initialization)
8. [Troubleshooting](#troubleshooting)

---

## Overview

DotNetCloud supports three database engines for flexibility and portability:

| Database | Platform | Use Case | Provider |
|----------|----------|----------|----------|
| **PostgreSQL** | Linux, macOS, Windows | Primary (recommended) | Npgsql |
| **SQL Server** | Windows, Linux, Docker | Enterprise | System.Data.SqlClient |
| **MariaDB** | Linux, macOS, Windows | Alternative MySQL-compatible | MySql.Data |

### Development Workflow

1. **Primary Development:** PostgreSQL (fastest setup, most common in Linux environments)
2. **Cross-Testing:** Set up one additional database locally
3. **Full Matrix:** Docker Compose handles all three for CI/CD

### Naming Convention

DotNetCloud uses schema/prefix naming per database provider:

- **PostgreSQL:** Schemas (`core.*`, `files.*`, etc.)
- **SQL Server:** Schemas (`dbo.`, `core.`, etc.)
- **MariaDB:** Table prefixes (`core_users`, `files_metadata`, etc.)

---

## PostgreSQL Setup

### Windows

#### Option 1: PostgreSQL Installer (Recommended for Beginners)

1. **Download PostgreSQL**
   - Visit: [https://www.postgresql.org/download/windows/](https://www.postgresql.org/download/windows/)
   - Download PostgreSQL 16.x or 17.x

2. **Run Installer**
   ```bash
   postgresql-16-x64.exe
   ```
   - Installation directory: `C:\Program Files\PostgreSQL\16`
   - Superuser: `postgres`
   - Password: Choose a strong password (e.g., `postgres_dev_local_123`)
   - Port: `5432` (default)
   - Locale: Your system locale

3. **Verify Installation**
   ```bash
   psql --version
   ```

#### Option 2: Chocolatey (Quick)

```bash
choco install postgresql
```

#### Option 3: Windows Subsystem for Linux (WSL2)

```bash
# In WSL2 Ubuntu terminal
sudo apt update
sudo apt install -y postgresql postgresql-contrib

# Start PostgreSQL service
sudo service postgresql start

# Verify
psql --version
```

### Linux (Ubuntu/Debian)

```bash
# Update package lists
sudo apt update

# Install PostgreSQL and client tools
sudo apt install -y postgresql postgresql-contrib postgis

# Start and enable service
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Verify
psql --version
```

### macOS

#### Using Homebrew (Recommended)

```bash
# Install
brew install postgresql@16

# Start service (one-time)
brew services start postgresql@16

# Verify
psql --version
```

#### Using PostgreSQL Installer

- Download from: [https://www.postgresql.org/download/macosx/](https://www.postgresql.org/download/macosx/)
- Run installer and follow prompts

### Post-Installation Setup

1. **Connect to PostgreSQL**
   ```bash
   # Windows (PowerShell)
   psql -U postgres -h localhost

   # Linux/macOS
   sudo -u postgres psql
   ```

2. **Create Development User and Database**
   ```sql
   -- Create user
   CREATE USER dotnetcloud WITH PASSWORD 'dotnetcloud_dev_password' CREATEDB;

   -- Create database
   CREATE DATABASE dotnetcloud_dev OWNER dotnetcloud;

   -- Enable extensions (optional but recommended)
   \c dotnetcloud_dev
   CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
   CREATE EXTENSION IF NOT EXISTS "ltree";
   ```

3. **Verify Connection**
   ```bash
   psql -U dotnetcloud -d dotnetcloud_dev -h localhost
   ```

4. **Connection String**
   ```
   Server=localhost;Port=5432;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=dotnetcloud_dev_password;
   ```

### Additional Configuration

1. **Create Schema Structure** (Optional, EF Core handles this)
   ```sql
   -- These are created automatically by Entity Framework migrations
   CREATE SCHEMA IF NOT EXISTS core;
   CREATE SCHEMA IF NOT EXISTS files;
   ```

2. **Configure Authentication** (Edit `pg_hba.conf`)
   - **Linux:** `/etc/postgresql/16/main/pg_hba.conf`
   - **Windows:** `C:\Program Files\PostgreSQL\16\data\pg_hba.conf`
   - **macOS:** `/usr/local/var/postgres/pg_hba.conf`

   Change local connection to md5 (password):
   ```
   # TYPE  DATABASE        USER            ADDRESS                 METHOD
   local   all             all                                     md5
   ```

---

## SQL Server Setup

### Windows (Local Installation)

#### Option 1: SQL Server Express Installer (Free, Limited)

1. **Download SQL Server Express**
   - Visit: [https://www.microsoft.com/en-us/sql-server/sql-server-editions-express](https://www.microsoft.com/en-us/sql-server/sql-server-editions-express)
   - Download SQL Server 2022 Express (or 2019 if preferred)

2. **Run Installer**
   ```bash
   SQLEXPR_x64_ENU.exe
   ```
   - Instance name: `SQLEXPRESS` (default) or custom
   - Enable SQL Server authentication
   - SA (System Admin) password: Choose strong password (e.g., `SQLServer2022!Local`)

3. **Install SQL Server Management Studio (SSMS)** - Separate download
   - Visit: [https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms](https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)
   - Download and install latest version

4. **Verify Installation**
   ```bash
   # PowerShell
   sqlcmd -S .\SQLEXPRESS -U sa -P SQLServer2022!Local -Q "SELECT @@version;"
   ```

#### Option 2: Chocolatey

```bash
choco install sql-server-2022-express
choco install sql-server-management-studio
```

### Windows/Linux/Docker (SQL Server in Docker)

See [DOCKER_SETUP.md](./DOCKER_SETUP.md) for containerized SQL Server.

### Post-Installation Setup

1. **Connect Using SSMS**
   - Server name: `localhost\SQLEXPRESS` (or just `localhost` for default instance)
   - Authentication: SQL Server Authentication
   - Login: `sa`
   - Password: `SQLServer2022!Local` (your chosen password)

2. **Create Development Database & User**
   ```sql
   -- Create database
   CREATE DATABASE [dotnetcloud_dev];

   -- Create user
   USE [dotnetcloud_dev];
   CREATE LOGIN [dotnetcloud] WITH PASSWORD = 'DotNetCloud2024!Dev';
   CREATE USER [dotnetcloud] FOR LOGIN [dotnetcloud];

   -- Grant permissions
   ALTER ROLE db_owner ADD MEMBER [dotnetcloud];
   ```

3. **Connection String**
   ```
   Server=localhost\SQLEXPRESS;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=DotNetCloud2024!Dev;MultipleActiveResultSets=True;
   ```

4. **Enable TCP/IP** (if using remote connections or Docker)
   - SQL Server Configuration Manager → SQL Server Network Configuration → Protocols for SQLEXPRESS
   - Enable TCP/IP and restart SQL Server service

### Additional Configuration

1. **Check SQL Server Status**
   ```bash
   # PowerShell (Windows)
   Get-Service -Name "MSSQL*"

   # Should see MSSQL$SQLEXPRESS with Status: Running
   ```

2. **Create Schema** (Optional, EF Core handles this)
   ```sql
   USE [dotnetcloud_dev];
   CREATE SCHEMA [core];
   CREATE SCHEMA [files];
   ```

---

## MariaDB Setup

### Windows

#### Option 1: MariaDB MSI Installer

1. **Download MariaDB**
   - Visit: [https://mariadb.org/download/](https://mariadb.org/download/)
   - Download MariaDB 10.11.x or 11.x

2. **Run Installer**
   ```bash
   mariadb-11.0.x-msi-x64.msi
   ```
   - Port: `3306` (default)
   - Root password: Choose strong password
   - Install as Windows Service: Yes
   - Service name: `MariaDB`

3. **Verify Installation**
   ```bash
   mysql --version
   ```

#### Option 2: Chocolatey

```bash
choco install mariadb
```

### Linux (Ubuntu/Debian)

```bash
# Update package lists
sudo apt update

# Install MariaDB
sudo apt install -y mariadb-server mariadb-client

# Secure installation
sudo mysql_secure_installation

# Start and enable service
sudo systemctl start mariadb
sudo systemctl enable mariadb

# Verify
mysql --version
```

### macOS

#### Using Homebrew

```bash
# Install
brew install mariadb

# Start service (one-time)
brew services start mariadb

# Secure installation
mysql_secure_installation

# Verify
mysql --version
```

### Post-Installation Setup

1. **Connect to MariaDB**
   ```bash
   mysql -u root -p
   # Enter root password
   ```

2. **Create Development Database & User**
   ```sql
   -- Create database
   CREATE DATABASE dotnetcloud_dev;

   -- Create user
   CREATE USER 'dotnetcloud'@'localhost' IDENTIFIED BY 'dotnetcloud_dev_password';

   -- Grant permissions
   GRANT ALL PRIVILEGES ON dotnetcloud_dev.* TO 'dotnetcloud'@'localhost';
   FLUSH PRIVILEGES;

   -- Verify
   SHOW GRANTS FOR 'dotnetcloud'@'localhost';
   ```

3. **Connection String**
   ```
   Server=localhost;Port=3306;Database=dotnetcloud_dev;Uid=dotnetcloud;Pwd=dotnetcloud_dev_password;
   ```

4. **Verify Connection**
   ```bash
   mysql -u dotnetcloud -p -h localhost dotnetcloud_dev
   ```

### Additional Configuration

1. **Check MariaDB Status**
   ```bash
   # Linux
   sudo systemctl status mariadb

   # Windows
   Get-Service -Name "MariaDB"
   ```

2. **Configure Character Set (UTF-8)**
   - Edit `/etc/mysql/mariadb.conf.d/50-server.cnf` (Linux) or `C:\ProgramData\MariaDB\my.ini` (Windows)
   - Add to `[mysqld]` section:
     ```ini
     default-character-set = utf8mb4
     collation-server = utf8mb4_unicode_ci
     ```
   - Restart MariaDB: `sudo systemctl restart mariadb`

---

## Connection Strings

### Configuration Files

Store connection strings in `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Server=localhost;Port=5432;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=dotnetcloud_dev_password;",
    "SqlServer": "Server=localhost\\SQLEXPRESS;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=DotNetCloud2024!Dev;MultipleActiveResultSets=True;",
    "MariaDb": "Server=localhost;Port=3306;Database=dotnetcloud_dev;Uid=dotnetcloud;Pwd=dotnetcloud_dev_password;"
  },
  "Database": {
    "Provider": "PostgreSQL"  // or "SqlServer", "MariaDb"
  }
}
```

### Environment Variables (Alternative)

```bash
# Linux/macOS
export ConnectionStrings__PostgreSQL="Server=localhost;Port=5432;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=dotnetcloud_dev_password;"
export Database__Provider="PostgreSQL"

# Windows PowerShell
$env:ConnectionStrings__PostgreSQL="Server=localhost;Port=5432;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=dotnetcloud_dev_password;"
$env:Database__Provider="PostgreSQL"
```

### User Secrets (Secure Development)

Use `dotnet user-secrets` to store sensitive connection strings:

```bash
# Initialize user secrets
dotnet user-secrets init

# Set connection strings
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Server=localhost;Port=5432;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=dotnetcloud_dev_password;"
dotnet user-secrets set "Database:Provider" "PostgreSQL"

# View secrets
dotnet user-secrets list
```

---

## Database Initialization

### Auto-Migrations (EF Core)

EF Core applies migrations automatically on startup if configured:

```csharp
// In Program.cs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.Migrate(); // Applies pending migrations
}
```

### Manual Migration

```bash
# Create migration
dotnet ef migrations add InitialCreate

# Apply pending migrations
dotnet ef database update

# View applied migrations
dotnet ef migrations list

# Remove last unapplied migration
dotnet ef migrations remove

# Reset database (for development only!)
dotnet ef database drop --force
dotnet ef database update
```

### Seeding Sample Data

Create a seeding service:

```csharp
public class DbInitializer
{
    public static void Initialize(CoreDbContext context)
    {
        // Check if database has been seeded
        if (context.Users.Any()) return;

        var users = new[]
        {
            new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin@example.com", Email = "admin@example.com" },
            new ApplicationUser { Id = Guid.NewGuid(), UserName = "user@example.com", Email = "user@example.com" }
        };

        context.Users.AddRange(users);
        context.SaveChanges();
    }
}
```

Call during initialization:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.Migrate();
    DbInitializer.Initialize(db); // Seed data
}
```

---

## Multi-Database Testing

### Local Testing Strategy

1. **Development:** Use single database (PostgreSQL recommended)
2. **Before Committing:** Test against all three databases
3. **CI/CD:** Docker Compose runs full matrix

### Running Tests Against Different Databases

```bash
# Test with PostgreSQL
dotnet test --configuration Release -- --Database:Provider=PostgreSQL

# Test with SQL Server
dotnet test --configuration Release -- --Database:Provider=SqlServer

# Test with MariaDB
dotnet test --configuration Release -- --Database:Provider=MariaDb
```

### Test Fixture Configuration

Create a test fixture that supports multiple databases:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    private readonly IHost _host;
    public CoreDbContext DbContext { get; private set; }

    public async Task InitializeAsync()
    {
        var provider = GetDatabaseProvider();
        
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<CoreDbContext>(options =>
                {
                    switch (provider)
                    {
                        case "PostgreSQL":
                            options.UseNpgsql("Server=localhost;Port=5432;Database=dotnetcloud_test;User Id=dotnetcloud;Password=test_password;");
                            break;
                        case "SqlServer":
                            options.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=dotnetcloud_test;User Id=sa;Password=Test@123;MultipleActiveResultSets=True;");
                            break;
                        case "MariaDb":
                            options.UseMySql("Server=localhost;Port=3306;Database=dotnetcloud_test;Uid=root;Pwd=test_password;", ServerVersion.AutoDetect("Server=localhost;Port=3306;Uid=root;Pwd=test_password;"));
                            break;
                    }
                });
            })
            .Build();

        _host = host;
        DbContext = _host.Services.GetRequiredService<CoreDbContext>();
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private string GetDatabaseProvider()
    {
        return Environment.GetEnvironmentVariable("Database:Provider") ?? "PostgreSQL";
    }
}
```

---

## Troubleshooting

### PostgreSQL

#### "Connection Refused"
- **Check Service:** `sudo systemctl status postgresql` (Linux) or Services app (Windows)
- **Start Service:** `sudo systemctl start postgresql` or `net start postgresql-x64-XX`
- **Check Port:** `netstat -an | grep 5432` (PostgreSQL on port 5432?)

#### "Role 'dotnetcloud' Does Not Exist"
```sql
-- Verify user exists
SELECT * FROM pg_user WHERE usename = 'dotnetcloud';

-- Recreate if missing
CREATE USER dotnetcloud WITH PASSWORD 'dotnetcloud_dev_password';
```

#### "Database Already Exists"
```sql
DROP DATABASE dotnetcloud_dev;
CREATE DATABASE dotnetcloud_dev OWNER dotnetcloud;
```

### SQL Server

#### "Connection Timeout"
- **Check Service:** `Get-Service -Name "MSSQL$SQLEXPRESS"`
- **Check TCP/IP:** SQL Server Configuration Manager → Protocols → TCP/IP (should be Enabled)
- **Restart Service:** `Restart-Service -Name "MSSQL$SQLEXPRESS"`

#### "Login Failed for User 'sa'"
- **Verify Credentials:** Check password matches what you set during installation
- **Enable SQL Authentication:** SQL Server Configuration Manager → MSSQL Server Network Configuration → Properties → Mixed Authentication Mode

#### "Port 1433 Already in Use"
```bash
# Find process using port
netstat -ano | findstr :1433

# Restart SQL Server
Restart-Service -Name "MSSQL$SQLEXPRESS"
```

### MariaDB

#### "Can't Connect to MySQL Server"
- **Check Service:** `sudo systemctl status mariadb` (Linux) or Services app (Windows)
- **Start Service:** `sudo systemctl start mariadb` or `net start MariaDB`
- **Check Port:** Default is `3306`

#### "Access Denied for User"
- **Verify Credentials:** Check username and password
- **Reset Root Password:**
  ```bash
  # Linux
  sudo mysqld_safe --skip-grant-tables &
  mysql -u root
  FLUSH PRIVILEGES;
  ALTER USER 'root'@'localhost' IDENTIFIED BY 'new_password';
  ```

#### "Database/User Already Exists"
```sql
DROP DATABASE dotnetcloud_dev;
DROP USER 'dotnetcloud'@'localhost';

-- Then recreate as shown in setup steps
```

### General Issues

#### "Migrations Not Applying"
```bash
# Check pending migrations
dotnet ef migrations list

# Remove database and reapply
dotnet ef database drop --force
dotnet ef database update
```

#### "Connection String Format Error"
- Double-check syntax, especially passwords with special characters
- Escape special characters: `Password=my%40password` for `@`
- Use raw strings in C#: `@"Server=...;Password=my@password;"`

#### "Multiple Databases Conflict"
- Ensure each database uses different ports or instance names
- Test connection before running migrations: `dotnet ef dbcontext info`

---

## Next Steps

- See [DOCKER_SETUP.md](./DOCKER_SETUP.md) for containerized database setup
- Refer to [IDE_SETUP.md](./IDE_SETUP.md) for database tools in your IDE
- Review [DEVELOPMENT_WORKFLOW.md](./DEVELOPMENT_WORKFLOW.md) for team collaboration guidelines
