# IDE Setup Guide

> **Purpose:** Configure your integrated development environment for DotNetCloud development  
> **Audience:** Developers (all skill levels)  
> **Last Updated:** 2026-03-02

---

## Table of Contents

1. [Visual Studio 2022](#visual-studio-2022)
2. [Visual Studio Code](#visual-studio-code)
3. [JetBrains Rider](#jetbrains-rider)
4. [EditorConfig & Code Style](#editorconfig--code-style)
5. [Common Extensions & Tools](#common-extensions--tools)
6. [Troubleshooting](#troubleshooting)

---

## Visual Studio 2022

### Installation

1. **Install Visual Studio 2022 Community or Professional**
   - Download from: [https://visualstudio.microsoft.com/vs/](https://visualstudio.microsoft.com/vs/)
   - Version: 2022 (17.8 or later)

2. **Select Workloads During Installation**
   - ✓ ASP.NET and web development
   - ✓ .NET 10 runtime/SDK (auto-included with latest installer)
   - ✓ Desktop development with C#
   - ✓ Azure development (recommended)

3. **Individual Components to Install**
   - Git for Windows
   - GitHub Desktop (optional)
   - SQL Server Express 2019 or later (for local SQL Server testing)

### Project Initialization

1. **Open DotNetCloud Solution**
   ```
   File → Open → Project/Solution
   Navigate to: D:\Repos\dotnetcloud\DotNetCloud.sln
   ```

2. **Verify .NET 10 SDK**
   - Open: Tools → Options → Projects and Solutions → .NET 10 SDK Configuration
   - Confirm SDK version is 10.0 or later
   - Or use command: `dotnet --version` (should be 10.x.x)

3. **Restore NuGet Packages**
   - Solution Explorer → Right-click solution → Restore NuGet Packages
   - Or: `dotnet restore` in terminal

### Code Style & EditorConfig

1. **Enable EditorConfig Support**
   - Tools → Options → Text Editor → C# → Code Style → Formatting
   - Check: "Follow project coding conventions"
   - Visual Studio automatically reads `.editorconfig`

2. **Code Analysis**
   - Tools → Options → Text Editor → C# → Advanced
   - Set "Analyze entire solution" to True (or on-demand)
   - Code analyzers use rules from `.editorconfig`

3. **Roslyn Analyzers**
   - Built-in analyzers are auto-enabled via `Directory.Build.props`
   - Warnings appear as squiggly underlines in editor

### Debugging

1. **Launch Configuration**
   - Debug → Start Debugging (F5)
   - Configures for first startup project
   - See `launchSettings.json` if custom setup needed

2. **Breakpoints & Watches**
   - Click left margin to set breakpoints
   - Debug → Windows → Watch (Ctrl + Alt + W) to inspect variables
   - Debug → Windows → Locals to see local scope

3. **Immediate Window**
   - Debug → Windows → Immediate (Ctrl + Alt + I)
   - Execute C# statements during debugging
   - Useful for testing expressions

### Testing in Visual Studio

1. **Open Test Explorer**
   - Test → Test Explorer (Ctrl + E, T)
   - Tests auto-discover from `[xUnit]`, `[Fact]`, `[Theory]` attributes

2. **Run Tests**
   - Run All Tests: Ctrl + R, A
   - Run Selected Tests: Ctrl + R, T
   - Debug Tests: Right-click test → Debug

3. **Test Settings**
   - Test → Configure Run Settings
   - Can specify multiple database targets for integration tests

### Recommended Extensions

1. **Essential Extensions**
   - Open Extensions → Manage Extensions (or Ctrl + Shift + X)
   - Search and install:
     - **Prettier Code Formatter**: C# and XAML formatting
     - **REST Client**: Make HTTP requests from editor
     - **GitLens**: Git integration and history
     - **SQL Server (mssql)**: SQL Server exploration

2. **Optional Extensions**
   - **Cascadia Code Font**: Modern terminal/editor font
   - **Thunder Client**: API testing (lightweight alternative to Postman)
   - **EditorConfig Language Support**: Enhanced .editorconfig editing

### Performance Tips

- Disable extensions you don't use: Extensions → Manage Extensions → Disable
- Exclude folders from IntelliSense: Tools → Options → Text Editor → C# → Advanced → Disable IntelliSense if needed
- Update Visual Studio regularly for performance improvements

---

## Visual Studio Code

### Installation

1. **Install VS Code**
   - Download from: [https://code.visualstudio.com/](https://code.visualstudio.com/)
   - Version: Latest Stable

2. **Verify .NET SDK**
   ```bash
   dotnet --version
   ```
   - Should output 10.x.x or later

### Initial Setup

1. **Open Workspace**
   ```bash
   code D:\Repos\dotnetcloud
   ```

2. **Install Recommended Extensions**
   - VS Code detects `.vscode/extensions.json` in the workspace (if present)
   - Prompts to install recommended extensions
   - Install:
     - **C# Dev Kit** (Microsoft)
     - **C#** (Microsoft) or **Omnisharp** (OmniSharp)
     - **.NET Install Tool** (Microsoft)
     - **Roslyn Analyzers** support

### Extensions for .NET Development

1. **Core Extensions**
   - **C# Dev Kit**: Unified C# experience (project explorer, test runner, debugging)
   - **C#**: Language support and IntelliSense
   - **Omnisharp** (alternative to C#): If using OmniSharp LSP

2. **Testing & Debugging**
   - **Test Explorer UI**: Better test discovery and execution
   - **Debugger for .NET**: Core debugging support

3. **Git & Version Control**
   - **GitLens**: Git blame, history, branch info
   - **GitHub Copilot** (optional): AI-assisted coding

4. **Productivity**
   - **REST Client**: Test API endpoints directly in editor
   - **Thunder Client**: Lightweight API testing
   - **Prettier**: Code formatting
   - **EditorConfig for VS Code**: Apply `.editorconfig` rules

### Configuration

1. **Create `.vscode/settings.json`** (if not present)
   ```json
   {
     "omnisharp.enableRoslynAnalyzers": true,
     "omnisharp.enableEditorConfigSupport": true,
     "[csharp]": {
       "editor.defaultFormatter": "ms-dotnettools.csharp",
       "editor.formatOnSave": true
     },
     "dotnet.unitTestDebuggingOptions": {
       "stopOnEntry": false,
       "console": "integratedTerminal"
     }
   }
   ```

2. **Restore & Build**
   ```bash
   dotnet restore
   dotnet build
   ```

### Running & Debugging

1. **Build Project**
   - Ctrl + Shift + B (or Tasks → Run Build Task)
   - Uses tasks from `.vscode/tasks.json`

2. **Debug**
   - Press F5 to start debugging (launches debugger configuration from `.vscode/launch.json`)
   - Set breakpoints by clicking line numbers
   - Use Debug sidebar to inspect variables

3. **Testing**
   - Open Test Explorer: Ctrl + Shift + D → Tests
   - Run/debug individual tests or test classes
   - View coverage if extension installed

### Terminal Integration

1. **Open Integrated Terminal**
   - Ctrl + ` (backtick)

2. **Common Commands**
   ```bash
   dotnet restore              # Restore NuGet packages
   dotnet build                # Build solution
   dotnet test                 # Run all tests
   dotnet test --filter "..."  # Run specific tests
   ```

### Tips for VS Code

- **Intellisense:** Trigger with Ctrl + Space
- **Go to Definition:** F12 or Ctrl + Click
- **Find All References:** Ctrl + F12 or Shift + F12
- **Rename Symbol:** F2
- **Quick Fix:** Ctrl + . (period)
- **Format Document:** Shift + Alt + F
- **Organize Usings:** Ctrl + Shift + O

---

## JetBrains Rider

### Installation

1. **Install Rider**
   - Download from: [https://www.jetbrains.com/rider/](https://www.jetbrains.com/rider/)
   - Version: Latest Stable (EAP also supported)
   - License: Community (free), Professional (paid), or with subscription

2. **Verify .NET SDK**
   - Rider auto-detects installed SDKs
   - Settings → Build, Execution, Deployment → .NET SDK
   - Confirm .NET 10 SDK is listed

### Project Setup

1. **Open Project**
   - File → Open → Select `D:\Repos\dotnetcloud`
   - Rider indexes the solution (may take 1-2 minutes)

2. **Sync Project**
   - File → Reload All Gradle Projects (or similar sync if using MSBuild)
   - Ensures all dependencies are recognized

3. **Configure Run/Debug**
   - Run → Edit Configurations
   - Add new configuration for the web/API project
   - Set environment variables, program arguments, etc.

### Code Style & EditorConfig

1. **EditorConfig Integration (Automatic)**
   - Rider respects `.editorconfig` by default
   - Settings → Editor → Code Style
   - Enable "Enable EditorConfig support" (usually on by default)

2. **Code Inspections**
   - Code → Run Inspection by Name
   - Search for specific issues
   - Rider highlights issues in the editor

3. **Reformat Code**
   - Select code → Code → Reformat Code (Ctrl + Alt + L)
   - Rider reformats according to `.editorconfig` rules

### Debugging

1. **Set Breakpoints**
   - Click in the gutter next to line numbers
   - Breakpoint is marked with a red circle

2. **Start Debugging**
   - Debug button in toolbar (Shift + F9 by default)
   - Rider pauses at breakpoints
   - Use Debug panel to step, inspect, and evaluate

3. **Conditional Breakpoints**
   - Right-click breakpoint → Edit Breakpoint
   - Add condition (e.g., `x > 5`)
   - Only breaks when condition is true

### Testing

1. **Run Tests**
   - View → Tool Windows → Unit Tests (or Ctrl + 8)
   - Rider auto-discovers tests
   - Right-click test → Run or Debug

2. **Code Coverage**
   - Run → Run with Coverage (or Ctrl + Alt + F6 without debug)
   - Rider shows which lines are covered

3. **Test Templates**
   - Rider provides test templates and can generate test stubs

### Rider Features

- **Database Tools:** Connect to SQL Server, PostgreSQL, MariaDB directly in IDE
- **Git Integration:** Built-in Git client with merge conflict resolution
- **REST Client:** Built-in for API testing
- **Profiling:** Integrated CPU and memory profilers
- **Docker Support:** Run/debug containers from IDE

### Keyboard Shortcuts (Common)

- **F2**: Navigate between usages
- **Ctrl + F12**: Show structure/members
- **Ctrl + Alt + V**: Introduce variable
- **Ctrl + Alt + T**: Wrap with statement (if, try, etc.)
- **Ctrl + Shift + R**: Replace (with regex)
- **Alt + Enter**: Quick fix/intention actions

### Plugins & Extensions

1. **Recommended Plugins**
   - File → Settings → Plugins → Browse Repositories
   - Markdown
   - YAML
   - Docker
   - GitHub Copilot (if desired)

---

## EditorConfig & Code Style

### EditorConfig File

The project includes `.editorconfig` at the root. It defines:

```ini
# Example rules (see actual file for all rules)
root = true

[*.cs]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = false

# C# specific
csharp_new_line_before_open_brace = all
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
```

### IDE-Specific Settings

1. **Visual Studio**
   - Tools → Options → Text Editor → C# → Code Style
   - Automatically applies `.editorconfig`

2. **VS Code**
   - Install "EditorConfig for VS Code"
   - Automatically applies `.editorconfig`

3. **Rider**
   - Settings → Editor → Code Style
   - "Enable EditorConfig support" (usually on by default)

### Validating Code Style

- Build the project: `dotnet build`
- Roslyn analyzers will report style violations
- Use `.editorconfig` rules and your IDE's formatting tools to fix

---

## Common Extensions & Tools

### All IDEs

1. **Roslyn Analyzers**
   - Enabled by default in all IDEs
   - Configured in `Directory.Build.props`
   - Reports code issues, warnings, and style violations

2. **NuGet Package Manager**
   - Visual Studio: Tools → NuGet Package Manager
   - VS Code: C# Dev Kit or `dotnet add package`
   - Rider: Tools → NuGet or Quick Fix (Ctrl + Alt + F)

3. **Git Integration**
   - All modern IDEs have built-in Git support
   - Commit, push, pull, merge directly from IDE

### Testing Tools

1. **xUnit.net Test Runner**
   - Integrated in all IDEs
   - Run tests with context menu → Run Tests

2. **Specflow** (for BDD, future phases)
   - Installed as NuGet package
   - Generate test stubs from `.feature` files

### Database Tools

1. **SQL Server Management Studio (SSMS)** - Optional
   - For detailed SQL Server management
   - Download from Microsoft

2. **pgAdmin** - Optional
   - For PostgreSQL management
   - Web-based interface

3. **DBeaver** - Recommended
   - Multi-database GUI tool
   - Supports PostgreSQL, SQL Server, MariaDB
   - Free and open-source
   - Download from [https://dbeaver.io/](https://dbeaver.io/)

### API Testing

1. **Postman** (standalone or in IDE)
   - [https://www.postman.com/](https://www.postman.com/)
   - Import OpenAPI specs automatically

2. **Thunder Client** (VS Code/Rider)
   - Lightweight API testing
   - No account required

3. **REST Client** (VS Code)
   - Lightweight and open-source
   - Use `.http` or `.rest` files

---

## Troubleshooting

### Common Issues

#### ".NET 10 SDK Not Found"
- **Solution:**
  - Download and install from [https://dot.net/download](https://dot.net/download)
  - Verify: `dotnet --version`
  - Restart IDE after installation

#### "Unable to Restore NuGet Packages"
- **Solution:**
  - Check internet connection
  - Clear NuGet cache: `nuget locals all -clear`
  - Check NuGet.config for correct sources
  - Try: `dotnet nuget locals all --clear` then `dotnet restore`

#### "Breakpoints Not Working"
- **Reason:** Debug symbols not generated
- **Solution:**
  - Ensure project built in Debug configuration
  - Clean and rebuild: `dotnet clean && dotnet build`
  - Restart debugging

#### "IntelliSense Not Working"
- **Solution (Visual Studio):**
  - Tools → Import and Export Settings → Reset
  - Or: File → Troubleshoot IDE Performance → Disable problematic extensions

- **Solution (VS Code):**
  - Restart OmniSharp: Ctrl + Shift + P → "Restart OmniSharp"
  - Ensure C# extension is installed and enabled

- **Solution (Rider):**
  - File → Invalidate Caches → Invalidate and Restart
  - Rider rebuilds indexes

#### "Tests Not Discovering"
- **Solution:**
  - Ensure test files end with `.Tests.cs` or are in `*Tests` namespace
  - Confirm xUnit is installed: `dotnet add package xunit`
  - Rebuild: `dotnet build`
  - Refresh test explorer

#### "Solution File Not Found"
- **Solution:**
  - Ensure `DotNetCloud.sln` exists in repository root
  - If missing, recreate: `dotnet new globaljson --sdk-version 10.x.x` then manually add projects

---

## Next Steps

- Refer to [DATABASE_SETUP.md](./DATABASE_SETUP.md) for database configuration
- See [DOCKER_SETUP.md](./DOCKER_SETUP.md) for containerized local testing
- Review [DEVELOPMENT_WORKFLOW.md](./DEVELOPMENT_WORKFLOW.md) for Git and PR guidelines
