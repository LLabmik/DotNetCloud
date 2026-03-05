# Development Workflow Guidelines

> **Purpose:** Establish standards for branching, committing, PRs, code review, and collaboration  
> **Audience:** All developers, technical leads, maintainers  
> **Last Updated:** 2026-03-02

---

## Table of Contents

1. [Repository Overview](#repository-overview)
2. [Branching Strategy](#branching-strategy)
3. [Commit Guidelines](#commit-guidelines)
4. [Pull Request Process](#pull-request-process)
5. [Code Review Standards](#code-review-standards)
6. [Testing Requirements](#testing-requirements)
7. [Local Development Workflow](#local-development-workflow)
8. [Conflict Resolution](#conflict-resolution)
9. [Documentation Standards](#documentation-standards)
10. [Release Process](#release-process)

---

## Repository Overview

### Monorepo Structure

```
dotnetcloud/
├── src/
│   ├── Core/                    # Core platform services
│   ├── Modules/                 # Feature modules (Files, Chat, etc.)
│   ├── UI/                      # Web UI (Blazor)
│   └── Clients/                 # Desktop/mobile clients
├── tests/                       # Test projects
├── tools/                       # CLI and utilities
├── docs/                        # Documentation
├── .github/                     # GitHub Actions workflows
├── .editorconfig                # Code style configuration
├── Directory.Build.props        # Common build properties
├── Directory.Build.targets      # Common build targets
├── global.json                  # .NET version pinning
├── NuGet.config                 # Package sources
├── docker-compose.yml           # Local dev environment
└── DotNetCloud.sln              # Main solution
```

### Repository Access

- **Main Repository:** `https://github.com/LLabmik/DotNetCloud`
- **Hosting:** GitHub
- **Permissions:** Depends on role (see CONTRIBUTING.md)

---

## Branching Strategy

### Git Flow Model

We use a modified Git Flow approach for branch organization:

```
main (production-ready)
  ↓
release/* (release branches)
  ↓
develop (integration branch)
  ↓
feature/* (feature branches)
  ↓
bugfix/* (bug fix branches)
  ↓
refactor/* (refactoring branches)
```

### Branch Naming Convention

#### Main Branches

| Branch | Purpose | Protection | Merge Strategy |
|--------|---------|-----------|-----------------|
| `main` | Production-ready code | Strict | Squash merge from release/* |
| `develop` | Integration branch for next release | Moderate | Squash merge from feature/* |

#### Feature Branches

**Naming:** `feature/short-description` or `feature/PHASE-number`

Examples:
- `feature/files-upload-api`
- `feature/user-authentication`
- `feature/PHASE-1-core-abstractions`

Naming rules:
- Start with `feature/`
- Use kebab-case (lowercase, hyphens)
- Keep descriptions short (2-4 words)
- Include issue/ticket number if applicable: `feature/ISSUE-123-description`

#### Bugfix Branches

**Naming:** `bugfix/short-description` or `bugfix/ISSUE-number`

Examples:
- `bugfix/connection-timeout-issue`
- `bugfix/ISSUE-456-memory-leak`

Naming rules:
- Start with `bugfix/`
- Use kebab-case
- Reference issue number if available

#### Release Branches

**Naming:** `release/v{version}`

Examples:
- `release/v0.1.0-alpha`
- `release/v1.0.0`

Naming rules:
- Start with `release/v`
- Follow semantic versioning: `MAJOR.MINOR.PATCH[-PRERELEASE]`

#### Refactoring Branches

**Naming:** `refactor/short-description`

Examples:
- `refactor/extract-common-interfaces`
- `refactor/improve-error-handling`

### Branch Lifecycle

#### Creating a Feature Branch

```bash
# Update local develop
git checkout develop
git pull origin develop

# Create feature branch
git checkout -b feature/my-feature

# Push upstream
git push -u origin feature/my-feature
```

#### Keeping Branch Updated

```bash
# Fetch latest changes
git fetch origin

# Rebase on develop (preferred for cleaner history)
git rebase origin/develop

# Force push (only for non-shared branches)
git push -f origin feature/my-feature

# Alternatively, merge (if rebasing creates conflicts)
git merge origin/develop
git push origin feature/my-feature
```

#### Deleting Branch After Merge

```bash
# Delete local branch
git branch -d feature/my-feature

# Delete remote branch
git push origin --delete feature/my-feature
```

---

## Commit Guidelines

### Commit Message Format

We follow the **Conventional Commits** specification for consistency and automated changelog generation.

#### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

#### Type

- **feat:** New feature
- **fix:** Bug fix
- **docs:** Documentation changes
- **style:** Code style changes (formatting, missing semicolons, etc.)
- **refactor:** Code refactoring without feature/fix changes
- **perf:** Performance improvements
- **test:** Adding or updating tests
- **ci:** CI/CD configuration changes
- **build:** Build system or dependency changes
- **chore:** Other changes that don't fit above categories

#### Scope

Optional, but recommended. Specifies the area affected:

- `core` - Core platform
- `auth` - Authentication/authorization
- `files` - Files module
- `chat` - Chat module
- `ui` - User interface
- `docker` - Docker/container setup
- `cli` - CLI tool

#### Subject

- Imperative, present tense: "add" not "added" or "adds"
- Don't capitalize first letter
- No period (.) at the end
- Maximum 50 characters
- Reference issue if applicable: `fix(auth): resolve login timeout #123`

#### Body

- Optional but recommended for non-trivial changes
- Explain **what** and **why**, not **how**
- Wrap at 72 characters
- Separate from subject with blank line

#### Footer

- Reference issues/PRs: `Closes #123` or `Fixes #456`
- Breaking changes: `BREAKING CHANGE: description`

### Commit Examples

#### Simple Commit
```
feat(files): implement file upload API

Adds POST /api/v1/files/upload endpoint with multipart/form-data support.
Includes progress tracking via SignalR.

Closes #42
```

#### Bug Fix
```
fix(auth): prevent token refresh race condition

Uses mutex to ensure only one refresh happens concurrently.
Prevents 401 errors when multiple requests occur simultaneously.
```

#### Documentation
```
docs: update database setup guide for PostgreSQL

Clarifies connection string format and troubleshooting section.
```

#### Breaking Change
```
refactor(core)!: rename ICapabilityRequest to ICapabilityGrant

BREAKING CHANGE: ICapabilityRequest interface has been renamed to
ICapabilityGrant. Update all implementations accordingly.

Migration guide: Replace "ICapabilityRequest" with "ICapabilityGrant" in your code.
```

### Commit Best Practices

1. **Small, focused commits**
   - One logical change per commit
   - Easier to review, revert, and bisect
   - Target: 50-200 lines changed per commit

2. **Commit frequently**
   - Commit after each logical milestone
   - Not after each individual change
   - Catch mistakes early

3. **Write meaningful messages**
   - Future developers (including yourself) will thank you
   - Messages are searchable
   - Enables better code archaeology

4. **Never force-push to shared branches**
   - Only force-push to your feature branch
   - Never to `main`, `develop`, or shared branches
   - Discuss with team if you must rewrite shared history

---

## Pull Request Process

### Creating a Pull Request

#### Before Opening PR

1. **Ensure local branch is up-to-date**
   ```bash
   git fetch origin
   git rebase origin/develop  # or merge if you prefer
   ```

2. **Local build succeeds**
   ```bash
   dotnet clean
   dotnet build
   dotnet test
   ```

3. **Code follows style guidelines**
   - Run `dotnet format` if available
   - Check EditorConfig compliance

4. **Documentation is updated**
   - Inline code comments for complex logic
   - README/docs if API changes
   - Changelog entry if public-facing

#### Opening PR

1. **Push feature branch**
   ```bash
   git push origin feature/my-feature
   ```

2. **Navigate to repository**
   - Go to Gitea UI (or GitHub)
   - Create pull request

3. **Fill PR Template**

Use the template below (create `.github/pull_request_template.md`):

```markdown
## Description
Brief description of changes

## Related Issues
Closes #123

## Type of Change
- [ ] Bug fix (non-breaking)
- [ ] New feature (non-breaking)
- [ ] Breaking change
- [ ] Documentation update
- [ ] Refactoring/performance

## Changes Made
- Describe change 1
- Describe change 2
- Describe change 3

## Testing Done
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed
- [ ] Tested against all database providers (PostgreSQL, SQL Server, MariaDB)

## Screenshots/Evidence (if applicable)
<!-- Add screenshots or logs demonstrating the change -->

## Checklist
- [ ] Code follows style guidelines
- [ ] Comments added for complex logic
- [ ] Documentation updated
- [ ] No new compiler warnings
- [ ] Tests pass locally
- [ ] I have verified this works with Docker Compose setup
```

4. **Set reviewers**
   - At least 1 maintainer for bug fixes
   - At least 2 maintainers for features
   - Codeowners automatically assigned if configured

### PR Title Format

Follow Conventional Commits for PR titles:

```
[Phase X] type(scope): brief description
```

Examples:
- `[Phase 0] feat(core): add capability system interfaces`
- `[Phase 1] fix(files): prevent concurrent upload errors`
- `[Phase 0] docs: update IDE setup guide`

### PR Lifecycle

#### 1. Await Review
- Minimum 24 hours for community feedback
- Maintainers usually review within 48 hours
- Don't merge your own PRs

#### 2. Address Feedback
```bash
# Make requested changes
# Commit with descriptive message
git add .
git commit -m "refactor: address review feedback"

# Push (don't need --force-push unless rebasing)
git push origin feature/my-feature
```

#### 3. Approval & Merge
- Require minimum 1 approval from maintainer
- All checks must pass (tests, linting, coverage)
- Squash merge to develop branch

#### 4. Cleanup
```bash
# Delete local branch
git branch -d feature/my-feature

# Delete remote branch (Gitea does this automatically on merge)
git push origin --delete feature/my-feature
```

---

## Code Review Standards

### Reviewer Responsibilities

1. **Verify Understanding**
   - Do you understand what the code does?
   - Is the purpose clear?
   - Ask for clarification if needed

2. **Check Quality**
   - Does code follow project conventions?
   - Are variable names clear and descriptive?
   - Is the logic easy to follow?

3. **Assess Testing**
   - Are tests adequate?
   - Do tests cover edge cases?
   - Are performance implications tested?

4. **Verify Correctness**
   - Does code solve the stated problem?
   - Are there potential bugs?
   - Are edge cases handled?

5. **Consider Performance**
   - Are there obvious inefficiencies?
   - Could queries be optimized?
   - Are resources properly managed?

6. **Ensure Security**
   - Are inputs validated?
   - Are secrets/credentials handled safely?
   - Is authorization checked?

### Review Comment Types

Use comment categories for clarity:

#### 🔴 Blocker (Must Fix)
```
🔴 BLOCKER: Missing null check will cause NullReferenceException in production.

Please add:
```csharp
if (user == null)
    throw new ArgumentNullException(nameof(user));
```
```

#### 🟡 Issue (Should Fix)
```
🟡 ISSUE: This LINQ query will load entire table into memory.

Consider using .AsAsyncEnumerable() and paginating results.
```

#### 💡 Suggestion (Nice to Have)
```
💡 SUGGESTION: Consider extracting this method into a separate class for better testability.
```

#### 📝 Nit (Style)
```
📝 NIT: Formatting inconsistency. See .editorconfig for correct spacing.
```

### Giving Feedback

**Good feedback:**
- ✅ Specific and actionable
- ✅ References code lines
- ✅ Explains reasoning
- ✅ Respectful and constructive

**Poor feedback:**
- ❌ "This is wrong"
- ❌ "Bad code"
- ❌ Vague suggestions
- ❌ Personal attacks

### Approving PRs

Requirements for approval:

- [ ] Code builds locally
- [ ] Tests pass
- [ ] No security concerns
- [ ] Follows project conventions
- [ ] Properly documented
- [ ] Performance acceptable
- [ ] At least one other reviewer has approved (or author is lead)

### Dismissing Reviews

For significant changes to already-approved PR:
- Author should dismiss reviews and request fresh review
- Major logic changes warrant complete re-review
- Minor documentation fixes don't need re-review

---

## Testing Requirements

### Minimum Test Coverage

| Category | Coverage | Notes |
|----------|----------|-------|
| Core business logic | 90%+ | Critical path |
| API endpoints | 85%+ | Happy path + error cases |
| Database layer | 80%+ | CRUD operations |
| Utilities/helpers | 70%+ | Less critical |
| **Overall** | **80%+** | Enforced in CI/CD |

### Test Organization

```
tests/
├── DotNetCloud.Core.Tests/          # Unit tests for core
│   ├── Authorization/
│   ├── Capabilities/
│   └── Events/
├── DotNetCloud.Integration.Tests/   # Integration tests (database)
│   ├── Database/
│   ├── Api/
│   └── ModuleLoading/
└── DotNetCloud.E2E.Tests/          # End-to-end tests (full stack)
    ├── Authentication/
    ├── FileUpload/
    └── UserWorkflows/
```

### Pre-PR Testing Checklist

```bash
# 1. Run all tests
dotnet test

# 2. Run integration tests against all databases
dotnet test -- --Database:Provider=PostgreSQL
dotnet test -- --Database:Provider=SqlServer
dotnet test -- --Database:Provider=MariaDb

# 3. Build in Release mode
dotnet build -c Release

# 4. Check code coverage
dotnet test /p:CollectCoverage=true

# 5. Verify no warnings
dotnet build /nologo /v:m  # Should not show warnings
```

### Test Naming Convention

```csharp
// Pattern: [MethodName]_[Scenario]_[ExpectedResult]
public class UserAuthenticationTests
{
    [Fact]
    public async Task AuthenticateUser_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var credentials = new LoginRequest { Email = "user@example.com", Password = "correct" };
        
        // Act
        var token = await _authService.AuthenticateAsync(credentials);
        
        // Assert
        Assert.NotNull(token);
    }

    [Fact]
    public async Task AuthenticateUser_WithInvalidPassword_ThrowsException()
    {
        // Arrange
        var credentials = new LoginRequest { Email = "user@example.com", Password = "wrong" };
        
        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.AuthenticateAsync(credentials)
        );
    }
}
```

---

## Local Development Workflow

### Day-to-Day Development

#### Starting Work

```bash
# Update develop branch
git checkout develop
git pull origin develop

# Create feature branch
git checkout -b feature/my-feature

# Set up environment
docker compose up -d postgres  # Start local database

# Restore and build
dotnet restore
dotnet build
```

#### During Development

```bash
# Make changes, test locally
dotnet test  # Run tests frequently

# Commit frequently with meaningful messages
git add .
git commit -m "feat(scope): descriptive message"

# Keep branch updated
git fetch origin
git rebase origin/develop  # Or merge if conflicts

# Push regularly to backup
git push origin feature/my-feature
```

#### Before PR

```bash
# Final cleanup
dotnet format  # Auto-format code
dotnet build   # Verify build

# Run full test suite
dotnet test

# Verify against all databases (if applicable)
docker compose up -d postgres sqlserver mariadb
dotnet test -- --Database:Provider=PostgreSQL
dotnet test -- --Database:Provider=SqlServer
dotnet test -- --Database:Provider=MariaDb
docker compose down

# Check for uncommitted changes
git status

# Create pull request
git push origin feature/my-feature
```

### Working with Multiple Features

```bash
# Scenario: Working on feature-A, need to switch to hotfix

# Stash uncommitted changes
git stash

# Switch to develop
git checkout develop
git pull origin develop

# Create hotfix branch
git checkout -b bugfix/critical-issue

# Fix issue, test, commit, push, create PR

# Back to feature-A
git checkout feature/my-feature
git stash pop  # Restore stashed changes
```

---

## Conflict Resolution

### Preventing Conflicts

1. **Pull frequently**
   ```bash
   git pull origin develop
   ```

2. **Commit small, focused changes**
   - Reduces chance of overlapping changes

3. **Communicate with team**
   - Let others know what you're working on
   - Coordinate on high-touch areas

### Resolving Conflicts

#### Merge Conflicts

```bash
# When pulling or merging
git pull origin develop
# Conflict! CONFLICT (content merge): ...

# View conflicts
git status

# Edit conflicted files
code src/Core/MyFile.cs  # Look for <<<<<<, ======, >>>>>>

# After resolving manually:
git add src/Core/MyFile.cs
git commit -m "resolve merge conflict with develop"
git push origin feature/my-feature
```

#### Rebase Conflicts

```bash
# During rebase
git rebase origin/develop
# Conflict! ...

# View conflicts
git status

# Resolve files
code src/Core/MyFile.cs

# Continue rebase
git add src/Core/MyFile.cs
git rebase --continue

# If you want to abort
git rebase --abort
```

### Conflict Resolution Strategy

**For files you modified:**
- Keep your changes and integrate theirs
- Test after merge to ensure correctness

**For files others modified:**
- Understand both changes
- Ask original author if unclear
- Test thoroughly

**For generated files (csproj, lock files):**
- Use local version and rebuild
- Regenerate dependencies: `dotnet restore`

---

## Documentation Standards

### Code Documentation

#### XML Comments (C#)

```csharp
/// <summary>
/// Authenticates a user with the provided credentials.
/// </summary>
/// <param name="credentials">The login credentials.</param>
/// <returns>An authentication token if successful.</returns>
/// <exception cref="UnauthorizedException">Thrown when credentials are invalid.</exception>
public async Task<AuthenticationToken> AuthenticateAsync(LoginRequest credentials)
{
    // Implementation
}
```

### Markdown Documentation

#### README Standards
- What is this component?
- How to use it?
- Configuration options?
- Examples?
- Troubleshooting?

#### Inline Comments
```csharp
// Use for "why", not "what"
✅ GOOD:
// Cache results to avoid repeated database queries during single request
var cachedResult = _cache.Get(key);

❌ BAD:
// Get from cache
var cachedResult = _cache.Get(key);
```

---

## Release Process

### Version Numbering

Follow Semantic Versioning: `MAJOR.MINOR.PATCH[-PRERELEASE]`

- **MAJOR:** Incompatible API changes
- **MINOR:** New features (backward compatible)
- **PATCH:** Bug fixes
- **PRERELEASE:** alpha, beta, rc (e.g., `1.0.0-beta.1`)

### Release Steps

1. **Create release branch**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b release/v0.1.0
   ```

2. **Update version**
   - Update `Directory.Build.props`: `<Version>0.1.0</Version>`
   - Update CHANGELOG.md
   - Commit: `chore: bump version to 0.1.0`

3. **Test release build**
   ```bash
   dotnet build -c Release
   dotnet test -c Release
   ```

4. **Create pull request to main**
   - Title: `[RELEASE] v0.1.0`
   - Link to release notes in CHANGELOG

5. **Merge to main**
   - Use squash merge
   - Tag: `git tag v0.1.0`
   - Push tag: `git push origin v0.1.0`

6. **Merge release branch back to develop**
   - Ensures develop has version bump

7. **Create GitHub Release**
   - Use release notes from CHANGELOG
   - Attach artifacts if applicable

---

## Troubleshooting Workflow Issues

### "I committed to the wrong branch"

```bash
# Create new branch from current HEAD
git branch feature/correct-branch

# Reset original branch to previous commit
git reset --hard HEAD~1

# Switch to correct branch
git checkout feature/correct-branch
```

### "I need to change my last commit message"

```bash
# Amend last commit
git commit --amend -m "new message"

# Force push (only for your feature branch!)
git push -f origin feature/my-feature
```

### "I accidentally pushed secrets to repository"

⚠️ **Immediately notify maintainers and rotate secrets!**

```bash
# Option 1: Remove from history (complex, rewrite shared history)
git filter-branch --tree-filter 'rm -f secrets.config' HEAD

# Option 2: Mark as no longer valid
git commit --allow-empty -m "remove leaked credentials - revoke immediately"

# Better: Use git-secrets pre-commit hook (prevent in future)
```

### "I want to undo my last push"

```bash
# Review what you're undoing
git log -2

# If on your feature branch (not shared):
git reset --hard HEAD~1
git push -f origin feature/my-feature

# If you need to preserve history:
git revert HEAD
git push origin feature/my-feature
```

---

## Next Steps

- Review code review guidelines
- Set up pre-commit hooks (optional)
- Configure your IDE for the project standards
- Read phase-specific development guides in `/docs`
