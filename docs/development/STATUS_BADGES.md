# Status Badges

> Reference for embedding CI/CD, build, and coverage status badges in project documentation.

---

## Available Badges

### CI Pipeline

The main CI workflow runs on every push to `main` and on pull requests.

```markdown
![CI](https://github.com/LLabmik/DotNetCloud/actions/workflows/ci.yml/badge.svg?branch=main)
```

**Preview:**
![CI](https://github.com/LLabmik/DotNetCloud/actions/workflows/ci.yml/badge.svg?branch=main)

### Build & Test Pipeline

Runs build, unit tests with coverage, and multi-database integration tests (PostgreSQL, SQL Server).

```markdown
![Build & Test](https://github.com/LLabmik/DotNetCloud/actions/workflows/build-test.yml/badge.svg?branch=main)
```

**Preview:**
![Build & Test](https://github.com/LLabmik/DotNetCloud/actions/workflows/build-test.yml/badge.svg?branch=main)

### Release Pipeline

Triggered on version tags (`v*`). Builds, tests, publishes self-contained binaries.

```markdown
![Release](https://github.com/LLabmik/DotNetCloud/actions/workflows/release.yml/badge.svg)
```

**Preview:**
![Release](https://github.com/LLabmik/DotNetCloud/actions/workflows/release.yml/badge.svg)

---

## Project Metadata Badges

### License

```markdown
![License](https://img.shields.io/github/license/LLabmik/DotNetCloud)
```

### .NET Version

```markdown
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
```

### Version

```markdown
![Version](https://img.shields.io/badge/version-0.1.0--alpha-blue)
```

---

## Usage in README

Add badges to the top of `README.md` inside the centered header block:

```markdown
<p align="center">
  <a href="https://github.com/LLabmik/DotNetCloud/actions/workflows/ci.yml">
    <img src="https://github.com/LLabmik/DotNetCloud/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI" />
  </a>
  <a href="https://github.com/LLabmik/DotNetCloud/actions/workflows/build-test.yml">
    <img src="https://github.com/LLabmik/DotNetCloud/actions/workflows/build-test.yml/badge.svg?branch=main" alt="Build & Test" />
  </a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/github/license/LLabmik/DotNetCloud" alt="License" />
  <img src="https://img.shields.io/badge/version-0.1.0--alpha-blue" alt="Version" />
</p>
```

---

## Badge Health Reference

| Badge | Source | Updates On |
|---|---|---|
| CI | GitHub Actions `ci.yml` | Push to main, PR |
| Build & Test | GitHub Actions `build-test.yml` | Push to main, PR |
| Release | GitHub Actions `release.yml` | Version tag push |
| License | GitHub repo metadata | Repo settings change |
| .NET version | Static (shields.io) | Manual update on .NET upgrade |
| Version | Static (shields.io) | Manual update on release |

---

## Adding Coverage Badge

When code coverage reporting is configured (e.g., via Codecov or Coveralls), add:

```markdown
![Coverage](https://codecov.io/gh/LLabmik/DotNetCloud/branch/main/graph/badge.svg)
```

This requires the Codecov GitHub App or a Coveralls integration to be configured in the CI pipeline.
