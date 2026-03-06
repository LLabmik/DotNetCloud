# Configuring Test Explorer to Skip Integration Tests

This repository includes both **unit tests** (fast, no external dependencies) and **integration tests** (slower, require Docker or real databases). By default, Test Explorer skips integration tests to speed up development workflows.

## How It Works

1. **Test Categories**: All integration test classes are marked with `[TestCategory("Integration")]`
2. **runsettings File**: The `.runsettings` file at the repository root filters out `TestCategory=Integration` by default
3. **Visual Studio Configuration**: Visual Studio automatically picks up `.runsettings` from the repository root

## Running Tests in Visual Studio

### Default Behavior (Unit Tests Only)

When you click **Run All Tests** or **Run Tests** in Test Explorer, only unit tests run. Integration tests are hidden/skipped.

### Manually Running Integration Tests

To run integration tests when needed:

#### Option 1: Via Test Explorer Filter

1. Open **Test Explorer** (Test → Test Explorer)
2. Click the **filter icon** (funnel) in the toolbar
3. Select **Traits** → **Integration**
4. Right-click on the filtered tests and select **Run**

#### Option 2: Via Command Line

Run all tests (including integration):
```powershell
dotnet test --filter "TestCategory!=Skipped"
```

Run ONLY integration tests:
```powershell
dotnet test --filter "TestCategory=Integration"
```

Run integration tests against Docker databases:
```powershell
dotnet test --filter "TestCategory=Integration&TestCategory=Docker"
```

#### Option 3: Temporarily Disable Filtering

1. Go to **Test → Configure Run Settings → Select Solution Wide runsettings File**
2. Select **Deselect Active RunSettings File**
3. Run tests normally (all tests will run)
4. Re-enable by selecting `.runsettings` again when done

#### Option 4: Create a Custom Test Playlist

1. In Test Explorer, filter to show integration tests (Traits → Integration)
2. Select all integration tests
3. Right-click → **Add to Playlist** → **New Playlist**
4. Name it "Integration Tests"
5. Use playlists to quickly run specific test groups

## CI/CD Behavior

In CI/CD pipelines (GitHub Actions, Gitea Actions), all tests run by default unless explicitly filtered:

```yaml
# Run only unit tests (fast feedback)
- name: Unit Tests
  run: dotnet test --filter "TestCategory!=Integration" --logger "trx;LogFileName=unit-tests.trx"

# Run integration tests (separate job, requires Docker)
- name: Integration Tests
  run: dotnet test --filter "TestCategory=Integration" --logger "trx;LogFileName=integration-tests.trx"
```

## Test Categories Reference

| Category | Description | Requires |
|----------|-------------|----------|
| `Integration` | All integration tests (API, database, gRPC) | May require Docker, SQL Server, etc. |
| `Docker` | Tests that require Docker containers | Docker daemon running |
| `(no category)` | Unit tests (default) | Nothing (in-memory, mocks) |

## Troubleshooting

### Integration Tests Not Showing Up

If integration tests never appear in Test Explorer:

1. Check that `[TestCategory("Integration")]` is present on test classes
2. Verify `.runsettings` is in the repository root
3. In Visual Studio: **Test → Configure Run Settings → Auto Detect runsettings Files** (should be checked)
4. Rebuild the solution (Test Explorer scans during build)

### Integration Tests Always Running

If integration tests run even with `.runsettings` active:

1. Check **Test → Configure Run Settings** — ensure `.runsettings` is selected (shows checkmark)
2. Verify `<TestCaseFilter>TestCategory!=Integration</TestCaseFilter>` is present in `.runsettings`
3. Close and reopen Visual Studio (settings sometimes cache)

### Can't Find .runsettings File

Visual Studio looks for `.runsettings` in:
1. Repository root (where `.sln` file is)
2. User profile directory: `%USERPROFILE%\.runsettings`
3. Manually specified via **Test → Configure Run Settings → Select Solution Wide runsettings File**

Our `.runsettings` is at the repository root, so it should auto-detect.

## Customizing Test Filters

You can create your own `.runsettings` with custom filters. For example, to exclude both integration AND Docker tests:

```xml
<RunConfiguration>
  <TestCaseFilter>TestCategory!=Integration&amp;TestCategory!=Docker</TestCaseFilter>
</RunConfiguration>
```

Or to run ONLY fast unit tests:

```xml
<RunConfiguration>
  <TestCaseFilter>TestCategory!=Integration&amp;TestCategory!=Slow</TestCaseFilter>
</RunConfiguration>
```

## Best Practices

1. **Run unit tests frequently** — they're fast, no setup required
2. **Run integration tests before PR** — ensures nothing breaks against real databases
3. **Use CI/CD for comprehensive testing** — both unit and integration tests run automatically
4. **Keep integration tests separate** — don't mix with unit tests in the same class
5. **Use descriptive test names** — integration test names should indicate what they test (e.g., `PostgreSqlCrudOperations`, `AuthEndpoint_LoginReturnsToken`)

## Further Reading

- [MSTest TestCategory documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=mstest)
- [.runsettings file reference](https://learn.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file)
- [Test Explorer filtering](https://learn.microsoft.com/en-us/visualstudio/test/run-unit-tests-with-test-explorer)
