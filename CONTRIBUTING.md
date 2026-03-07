# Contributing to DotNetCloud

Thank you for your interest in contributing to DotNetCloud! This document provides guidelines and instructions for contributing to the project.

## Code of Conduct

This project adheres to the Contributor Covenant Code of Conduct. By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## How to Contribute

### Reporting Bugs

Before creating bug reports, please check the issue tracker to avoid duplicates.

**When creating a bug report, include:**

- Clear, descriptive title
- Exact steps to reproduce the problem
- Expected behavior vs actual behavior
- Screenshots/error logs if applicable
- Your environment (OS, .NET version, database, etc.)

### Suggesting Features

Feature suggestions are welcome! Please:

1. Check the [MASTER_PROJECT_PLAN.md](./docs/MASTER_PROJECT_PLAN.md) to see if it's already planned
2. Use a clear, descriptive title
3. Explain the use case and expected benefit
4. Include examples or mockups if helpful

### Pull Requests

#### Before Starting

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Review the current phase in [MASTER_PROJECT_PLAN.md](./docs/MASTER_PROJECT_PLAN.md)

#### Development Process

1. **Code Style**: Follow the `.editorconfig` and code style conventions
   - Use 4-space indentation (configured in `.editorconfig`)
   - Follow C# naming conventions (PascalCase for classes/methods, camelCase for private fields)
   - Enable nullable reference types (`#nullable enable`)
   - Use modern C# features (records, pattern matching, etc.)

2. **Testing**:
   - Write unit tests for new functionality
   - Maintain 80%+ code coverage
   - Run tests locally: `dotnet test`
   - Use MSTest framework for consistency

3. **Documentation**:
   - Add XML documentation (`///`) to public members
   - Update relevant documentation files
   - Update IMPLEMENTATION_CHECKLIST.md if completing a phase task

4. **Commits**:
   - Write clear, descriptive commit messages
   - Reference issues: `Fixes #123`
   - Commit frequently with logical groupings

#### Submitting a Pull Request

1. Push to your fork: `git push origin feature/your-feature-name`
2. Create a Pull Request with:
   - Clear title referencing the phase/issue
   - Description of changes and rationale
   - Link to related issue/phase step
   - Checklist of items completed

3. PR Checklist:
   - [ ] Code follows style guidelines
   - [ ] All tests pass locally
   - [ ] No warnings in build output
   - [ ] Documentation updated
   - [ ] IMPLEMENTATION_CHECKLIST.md updated (if applicable)
   - [ ] Commit messages are clear

4. Wait for review and address feedback

## Development Environment Setup

### Prerequisites

- .NET 10 SDK or later
- PostgreSQL 14+ (or SQL Server 2019+, MariaDB 10.5+)
- Docker & Docker Compose
- Git

### Local Setup

```bash
# Clone your fork
git clone https://github.com/your-username/dotnetcloud.git
cd dotnetcloud

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# (Optional) Start Docker services
docker-compose up -d
```

### Running the API Locally

```bash
cd src/Core/DotNetCloud.API
dotnet run
```

The API will be available at `https://localhost:7001`

### Database Setup

1. Ensure PostgreSQL is running (via docker-compose or locally)
2. Run migrations:
   ```bash
   dotnet ef database update --project src/Core/DotNetCloud.Core.Data
   ```
3. Database will be initialized with default data

## Project Structure

- **src/Core/DotNetCloud.Core/**: Core abstractions and interfaces
- **src/Core/DotNetCloud.Core.Data/**: Entity Framework models and data access
- **src/Core/DotNetCloud.Core.ServiceDefaults/**: Logging, telemetry, health checks
- **src/Core/DotNetCloud.API/**: Main REST API
- **src/Modules/**: Feature modules (plugins)
- **src/UI/**: Web user interface
- **src/Clients/**: Client SDKs
- **tests/**: Test projects
- **docs/**: Documentation

## Coding Standards

### C# Style Guide

```csharp
// Namespaces follow folder structure
namespace DotNetCloud.Core.Capabilities;

// Use file-scoped namespaces
// Use top-level statements where appropriate

// Classes and records
public sealed class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Clear XML documentation for public members.
    /// </summary>
    /// <param name="input">Parameter description</param>
    /// <returns>Return value description</returns>
    public async Task<string> ProcessAsync(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        _logger.LogInformation("Processing input: {Input}", input);
        
        return await DoWorkAsync(input);
    }
    
    private async Task<string> DoWorkAsync(string input)
    {
        // Implementation
        return input;
    }
}
```

### Testing

```csharp
[TestClass]
public class MyServiceTests
{
    private MyService _service;
    private Mock<ILogger<MyService>> _loggerMock;
    
    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MyService>>();
        _service = new MyService(_loggerMock.Object);
    }
    
    [TestMethod]
    public async Task ProcessAsync_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = "test";
        
        // Act
        var result = await _service.ProcessAsync(input);
        
        // Assert
        Assert.AreEqual("test", result);
    }
}
```

## Working with Phases

The project is organized by phases as defined in [MASTER_PROJECT_PLAN.md](./docs/MASTER_PROJECT_PLAN.md).

### When Starting a Phase

1. Read the phase specification in MASTER_PROJECT_PLAN.md
2. Check [IMPLEMENTATION_CHECKLIST.md](./docs/IMPLEMENTATION_CHECKLIST.md)
3. Create a feature branch: `git checkout -b phase/0.1-core-abstractions`

### When Completing a Phase Task

1. Update IMPLEMENTATION_CHECKLIST.md:
   ```markdown
   - [x] Task name - completion date
   ```

2. Include in commit message:
   ```
   Complete phase-0.1.1: Capability System Interfaces
   ```

## Build and Test

### Building

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/Core/DotNetCloud.Core/DotNetCloud.Core.csproj

# Release build
dotnet build -c Release
```

### Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter ClassName=DotNetCloud.Core.Tests.CapabilitySystemTests

# Watch mode (requires watcher)
dotnet watch test
```

### Linting and Analysis

```bash
# Code analysis runs automatically during build
# To run manually:
dotnet build /p:EnforceCodeStyleInBuild=true

# Format code
dotnet format
```

## Commits and History

### Commit Message Format

```
<type>(<scope>): <subject>

Why:
- ...

What changed:
- ...

Tests:
- ...

Docs:
- ...

Deployment/Ops:
- ...

Breaking changes:
- none

Refs:
- ...
```

**Type:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Code style
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Build or dependency updates

**Scope examples:**
- `files`
- `server`
- `ui`
- `packaging`
- `docs`

### Use the Repository Commit Template

This repository includes `.gitmessage` for detailed commit bodies.

Set it once in your local clone:

```bash
git config commit.template .gitmessage
```

Optional (global):

```bash
git config --global commit.template /absolute/path/to/your/.gitmessage
```

### AI Commit Message Workflow (Recommended)

1. Stage all intended files before generating a message (`git add ...`).
2. Ask AI for both subject and body, not subject only.
3. Use this prompt shape:

```text
Generate a Conventional Commit subject and a detailed body from the staged diff.
Include Why, What changed, Tests, Docs, Deployment/Ops, and Refs.
```

The one-click SCM “generate message” action may return short summaries only.

**Examples:**
```
feat(core): add capability system interfaces for phase-0.1.1

Why:
- establish typed capability contracts for authorization boundaries

What changed:
- implement `ICapabilityInterface` marker
- add `CapabilityTier` enum with four tiers
- create public, restricted, and privileged interfaces

Tests:
- `dotnet test`

Docs:
- update `docs/IMPLEMENTATION_CHECKLIST.md`

Deployment/Ops:
- none

Breaking changes:
- none

Refs:
- Implements phase-0.1.1 from `docs/MASTER_PROJECT_PLAN.md`
- Fixes #45

docs(architecture): update capability system documentation

Why:
- keep architecture docs aligned with shipped abstractions

What changed:
- add capability system design patterns
- include tier hierarchy diagram

Tests:
- not run (docs-only)

Docs:
- update `docs/architecture/ARCHITECTURE.md`

Deployment/Ops:
- none

Breaking changes:
- none

Refs:
- none
```

## Documentation

When contributing code, also update documentation:

1. **Inline XML Documentation** (`///`):
   ```csharp
   /// <summary>
   /// Description of what the member does.
   /// </summary>
   /// <param name="param1">Description of parameter</param>
   /// <returns>Description of return value</returns>
   /// <exception cref="ArgumentNullException">Thrown when param1 is null</exception>
   public string MyMethod(string param1)
   ```

2. **Architecture Documentation**: Update relevant `.md` files in `docs/architecture/`

3. **IMPLEMENTATION_CHECKLIST.md**: Mark tasks as complete as you finish them

4. **README.md**: Update if changing public APIs or significant features

## Troubleshooting

### Build Issues

- Ensure .NET 10 is installed: `dotnet --version`
- Clean and rebuild: `dotnet clean && dotnet build`
- Clear NuGet cache: `dotnet nuget locals all --clear`

### Test Failures

- Run tests in verbose mode: `dotnet test -v d`
- Run specific test: `dotnet test --filter "TestName"`
- Check test output logs

### Database Issues

- Restart Docker: `docker-compose down && docker-compose up -d`
- Remove and recreate database:
  ```bash
  docker-compose down -v
  docker-compose up -d
  ```

## Questions?

- Check existing documentation in `docs/`
- Review MASTER_PROJECT_PLAN.md for context
- Open a GitHub discussion for questions
- Ask in issue comments if related to work

## License

By contributing, you agree that your contributions will be licensed under the same AGPL-3.0 license as the project.

---

Thank you for contributing to DotNetCloud! 🎉
