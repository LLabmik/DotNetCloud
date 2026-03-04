# Running Tests

> **Purpose:** Guide for running unit, integration, and coverage tests in the DotNetCloud project  
> **Audience:** Developers, CI/CD pipelines  
> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Running All Tests](#running-all-tests)
3. [Running Specific Test Projects](#running-specific-test-projects)
4. [Integration Tests](#integration-tests)
5. [Code Coverage](#code-coverage)
6. [CI/CD Pipeline](#cicd-pipeline)
7. [Test Framework Reference](#test-framework-reference)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

- **.NET 10 SDK** — install the version pinned in `global.json`
- **Docker** (optional) — required for Docker-based integration tests (PostgreSQL, SQL Server)
- **WSL 2** (Windows only, optional) — required if Docker Desktop is not installed; the test fixture auto-detects Docker via native path first, then falls back to WSL

Verify your environment:

```powershell
dotnet --version        # Should match global.json
docker --version        # Optional, for integration tests
```

---

## Running All Tests

From the solution root (`D:\Repos\dotnetcloud`):

```powershell
dotnet test
```

This discovers and runs all test projects in the solution:

| Test Project | Description | Test Count |
|---|---|---|
| `DotNetCloud.Core.Tests` | Core abstractions, capabilities, events, modules, authorization | 108 |
| `DotNetCloud.Core.Data.Tests` | Database models, naming strategies, DbContext | varies |
| `DotNetCloud.Core.Server.Tests` | Server middleware, gRPC, process supervisor, observability | varies |
| `DotNetCloud.Core.Auth.Tests` | Authentication integration tests | 18 |
| `DotNetCloud.CLI.Tests` | CLI commands, configuration, console output | 66 |
| `DotNetCloud.Modules.Example.Tests` | Example module lifecycle, events, manifest | 51 |
| `DotNetCloud.Integration.Tests` | Multi-database integration tests (requires Docker) | varies |

---

## Running Specific Test Projects

Run a single project:

```powershell
dotnet test tests\DotNetCloud.Core.Tests\DotNetCloud.Core.Tests.csproj
```

Run tests matching a filter:

```powershell
# Run tests whose fully qualified name contains "CallerContext"
dotnet test --filter "FullyQualifiedName~CallerContext"

# Run a specific test class
dotnet test --filter "ClassName=DotNetCloud.Core.Tests.Authorization.CallerContextTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~CallerContextTests.Constructor_WithValidParameters_SetsProperties"
```

Run with detailed output:

```powershell
dotnet test --verbosity detailed
```

Generate TRX result files (used in CI):

```powershell
dotnet test --logger "trx;LogFileName=test-results.trx"
```

---

## Integration Tests

Integration tests live in `tests\DotNetCloud.Integration.Tests` and can run against real database containers.

### Database Container Tests

The `DatabaseContainerFixture` automatically manages Docker containers for PostgreSQL and SQL Server.

**Docker detection order:**

1. Native Docker (`docker` on PATH)
2. WSL Docker fallback (`wsl docker`)

```powershell
# Run only integration tests
dotnet test tests\DotNetCloud.Integration.Tests\DotNetCloud.Integration.Tests.csproj
```

### Supported Databases

| Database | Container Image | Status |
|---|---|---|
| PostgreSQL 16 | `postgres:16-alpine` | ✅ Fully supported |
| SQL Server 2022 | `mcr.microsoft.com/mssql/server:2022-latest` | ✅ CI only (WSL2 kernel compatibility) |
| MariaDB | `mariadb:11` | ⏳ Pending Pomelo .NET 10 support |

### Skipping Integration Tests

Integration tests skip gracefully when Docker is unavailable. They use `Assert.Inconclusive()` to signal the skip rather than failing.

---

## Code Coverage

### Install the Coverage Tool

```powershell
dotnet tool install -g dotnet-coverage
```

### Generate Coverage Report

```powershell
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

### CI Coverage Configuration

The CI pipeline uses `coverlet.collector` with XPlat Code Coverage:

```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

Coverage exclusions (configured in CI):

- Test projects (`*.Tests`)
- Migration files (`Migrations`)
- Auto-generated code (`*.g.cs`)

### Viewing Coverage Locally

Install ReportGenerator for HTML reports:

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

Open `CoverageReport\index.html` in a browser.

---

## CI/CD Pipeline

Tests run automatically on every push and pull request via GitHub Actions and Gitea Actions.

### Workflow File

- GitHub: `.github\workflows\build-test.yml`
- Gitea: `.gitea\workflows\build-test.yml`

### CI Test Matrix

| Job | What It Does |
|---|---|
| **Build** | Restore, compile (Release), publish Core Server + CLI, upload artifacts |
| **Unit Tests** | Run all unit tests with MSTest, generate TRX logs and Cobertura coverage |
| **Integration Tests** | Multi-database matrix (PostgreSQL 16, SQL Server 2022) via service containers |

### Running CI Locally

To simulate the CI build and test locally:

```powershell
# Build in Release configuration (matches CI)
dotnet build --configuration Release

# Run tests with coverage (matches CI)
dotnet test --configuration Release --collect:"XPlat Code Coverage" --logger "trx"
```

---

## Test Framework Reference

### Framework

All test projects use **MSTest** (`Microsoft.VisualStudio.TestTools.UnitTesting`).

### Key Attributes

| Attribute | Purpose |
|---|---|
| `[TestClass]` | Marks a class as containing test methods |
| `[TestMethod]` | Marks a method as a test |
| `[DataTestMethod]` | Parameterized test method |
| `[DataRow(...)]` | Provides data for parameterized tests |
| `[TestInitialize]` | Runs before each test method |
| `[TestCleanup]` | Runs after each test method |
| `[ExpectedException]` | Expects a specific exception (prefer `Assert.ThrowsException`) |

### Mocking

The project uses **Moq** for mocking external dependencies.

```csharp
var mockService = new Mock<IMyService>();
mockService.Setup(s => s.DoWorkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(expectedResult);
```

### Common Assertions

```csharp
Assert.AreEqual(expected, actual);
Assert.IsNotNull(result);
Assert.IsTrue(condition);
Assert.IsInstanceOfType(obj, typeof(ExpectedType));
Assert.ThrowsException<ArgumentNullException>(() => new MyClass(null!));
await Assert.ThrowsExceptionAsync<InvalidOperationException>(
    () => service.DoWorkAsync(invalidInput));
```

---

## Troubleshooting

### Tests Not Discovered

1. Ensure the test project references `Microsoft.NET.Test.Sdk`
2. Verify `[TestClass]` and `[TestMethod]` attributes are present
3. Run `dotnet build` before `dotnet test`

### Docker Integration Tests Failing

1. Verify Docker is running: `docker info`
2. On Windows without Docker Desktop, verify WSL: `wsl docker info`
3. Check container logs: `docker logs <container-id>`
4. SQL Server tests may skip on WSL2 due to kernel incompatibility — this is expected

### Coverage Report Empty

1. Ensure `coverlet.collector` is referenced in test projects
2. Check that the `--collect:"XPlat Code Coverage"` flag is passed
3. Look for `coverage.cobertura.xml` in the `TestResults` subdirectory

### Slow Tests

- Integration tests with Docker containers have startup overhead (~5-15 seconds per container)
- Use `--filter` to run only specific tests during development
- Run the full suite before committing

---

**See also:**

- [Development Setup Guide](README.md)
- [Database Setup](DATABASE_SETUP.md)
- [Docker Setup](DOCKER_SETUP.md)
- [CI/CD Workflows](../../.github/workflows/build-test.yml)
