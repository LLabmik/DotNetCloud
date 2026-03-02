# Development Setup & Workflow Guide

> **Quick Navigation:** Start here for all development-related setup and guidelines

## Getting Started (30 minutes)

Choose your development environment and follow the guides:

### 1. 🛠️ **IDE Setup** → [IDE_SETUP.md](./IDE_SETUP.md)
Install and configure your editor:
- Visual Studio 2022 (Windows)
- Visual Studio Code (any platform)
- JetBrains Rider (any platform)

**Time:** 15-20 minutes

### 2. 🗄️ **Database Setup** → [DATABASE_SETUP.md](./DATABASE_SETUP.md)
Choose and configure your database:
- PostgreSQL (recommended)
- SQL Server (Windows/Docker)
- MariaDB (MySQL-compatible)

**Time:** 10-20 minutes (depending on choice)

### 3. 🐳 **Docker Setup** (Optional) → [DOCKER_SETUP.md](./DOCKER_SETUP.md)
Run databases and services in containers:
- Docker Desktop installation
- docker-compose configuration
- Multi-database testing

**Time:** 15-30 minutes (if using Docker)

### 4. 📋 **Development Workflow** → [DEVELOPMENT_WORKFLOW.md](./DEVELOPMENT_WORKFLOW.md)
Learn team collaboration standards:
- Branching strategy
- Commit conventions
- Pull request process
- Code review standards

**Time:** 20-30 minutes (read before first PR)

---

## Quick Decision Tree

```
Are you starting development?
│
├─ YES → Read all 4 guides above (30-60 min)
│
└─ NO → Where are you contributing?
    │
    ├─ Bug fix → Read: DEVELOPMENT_WORKFLOW.md
    ├─ Feature → Read: DEVELOPMENT_WORKFLOW.md + relevant module guides
    ├─ Docs → Read: DEVELOPMENT_WORKFLOW.md
    └─ Infrastructure → Read: DOCKER_SETUP.md + DATABASE_SETUP.md
```

---

## Common Workflows

### Setting Up New Environment

```bash
# 1. Choose database (PostgreSQL recommended)
# 2. Follow DATABASE_SETUP.md for your OS
# 3. Follow IDE_SETUP.md for your editor
# 4. Clone and build:

git clone https://git.kimball.home/benk/dotnetcloud.git
cd dotnetcloud
dotnet restore
dotnet build
dotnet test
```

### Daily Development

```bash
# 1. Start databases
docker compose up -d postgres

# 2. Create feature branch (see DEVELOPMENT_WORKFLOW.md)
git checkout develop
git pull origin develop
git checkout -b feature/my-feature

# 3. Make changes and test
dotnet test

# 4. Commit with good messages (see DEVELOPMENT_WORKFLOW.md)
git commit -m "feat(scope): description"

# 5. Push and create PR
git push -u origin feature/my-feature
```

### Testing All Databases Locally

```bash
# Start all databases
docker compose up -d postgres sqlserver mariadb

# Test against each
dotnet test -- --Database:Provider=PostgreSQL
dotnet test -- --Database:Provider=SqlServer
dotnet test -- --Database:Provider=MariaDb

# Clean up
docker compose down
```

### Code Review Process

1. **For Authors:**
   - Read DEVELOPMENT_WORKFLOW.md → Pull Request Process
   - Ensure tests pass and code is formatted
   - Create PR with clear description

2. **For Reviewers:**
   - Read DEVELOPMENT_WORKFLOW.md → Code Review Standards
   - Follow review comment guidelines
   - Approve or request changes

---

## Troubleshooting

| Problem | Solution | Resource |
|---------|----------|----------|
| IDE not finding .NET 10 | Reinstall .NET 10 SDK, restart IDE | IDE_SETUP.md |
| Database won't start | Check port usage, service status | DATABASE_SETUP.md |
| Docker containers failing | Verify credentials, check logs | DOCKER_SETUP.md |
| Connection string errors | Check syntax in appsettings.json | DATABASE_SETUP.md |
| Tests not discovering | Rebuild project, restart IDE | IDE_SETUP.md |
| Merge conflicts | Understand both changes, test | DEVELOPMENT_WORKFLOW.md |

---

## Project Resources

- **Main Repository:** https://git.kimball.home/benk/dotnetcloud
- **Architecture Docs:** `/docs/architecture/`
- **Implementation Checklist:** `/docs/IMPLEMENTATION_CHECKLIST.md`
- **Master Project Plan:** `/docs/MASTER_PROJECT_PLAN.md`
- **Contributing Guidelines:** `/CONTRIBUTING.md`

---

## Key Information

### Technology Stack

- **.NET Version:** 10 (pinned in `global.json`)
- **Databases:** PostgreSQL, SQL Server, MariaDB (all supported)
- **Web Framework:** ASP.NET Core
- **Web UI:** Blazor
- **Testing:** xUnit
- **ORM:** Entity Framework Core
- **Authentication:** OpenIddict (OAuth2/OIDC)

### Code Standards

- **Language:** C# 13 (latest)
- **Style:** Enforced by `.editorconfig`
- **Format:** Run `dotnet format` before commits
- **Commits:** Follow Conventional Commits (see DEVELOPMENT_WORKFLOW.md)
- **Tests:** Minimum 80% coverage
- **Branches:** Git Flow model (feature/*, bugfix/*, release/*)

### Important Files

| File | Purpose |
|------|---------|
| `.editorconfig` | Code style rules (auto-enforced by IDEs) |
| `global.json` | .NET 10 version pinning |
| `Directory.Build.props` | Common project properties |
| `NuGet.config` | Package sources |
| `docker-compose.yml` | Local database containers |
| `.github/` | Workflows (CI/CD) |

---

## First-Time Setup Script

Save this as `setup.sh` (Linux/macOS) or adapt for PowerShell:

```bash
#!/bin/bash

echo "🚀 DotNetCloud Development Setup"
echo ""

# Verify .NET SDK
echo "1️⃣  Checking .NET SDK..."
dotnet --version

# Clone repository (if needed)
if [ ! -d ".git" ]; then
    echo "2️⃣  Cloning repository..."
    git clone https://git.kimball.home/benk/dotnetcloud.git
fi

# Restore and build
echo "2️⃣  Restoring dependencies..."
dotnet restore

echo "3️⃣  Building project..."
dotnet build

# Run tests
echo "4️⃣  Running tests..."
dotnet test

# Start databases
echo "5️⃣  Starting databases..."
docker compose up -d postgres

echo ""
echo "✅ Setup complete!"
echo "📖 Next: Read docs/development/DEVELOPMENT_WORKFLOW.md"
echo "🔗 Create feature branch: git checkout -b feature/my-feature"
echo "📝 Happy coding!"
```

---

## Getting Help

1. **Check the relevant guide** (IDE_SETUP.md, DATABASE_SETUP.md, etc.)
2. **Look for troubleshooting sections** in specific guides
3. **Search existing issues** in repository
4. **Ask in team chat** with specific error messages
5. **Check CONTRIBUTING.md** for additional guidelines

---

## Next Steps After Setup

1. ✅ Complete development environment setup
2. ✅ Read DEVELOPMENT_WORKFLOW.md (important for contributions)
3. 📖 Review architecture documentation (`/docs/architecture/`)
4. 🎯 Find your first task in IMPLEMENTATION_CHECKLIST.md
5. 🚀 Create a feature branch and start coding!

---

**Questions?** Refer to the specific guide or CONTRIBUTING.md
