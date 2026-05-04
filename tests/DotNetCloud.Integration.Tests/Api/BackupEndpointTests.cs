using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the backup admin API endpoints.
/// Validates the backup/run, backup/status, and backup/restore endpoints.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class BackupEndpointTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static HttpClient _adminClient = null!;
    private static HttpClient _anonClient = null!;

    private static readonly Guid AdminUserId = Guid.NewGuid();

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
        _anonClient = _factory.CreateApiClient();
        _adminClient = _factory.CreateAdminApiClient(AdminUserId);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _anonClient?.Dispose();
        _adminClient?.Dispose();
        _factory?.Dispose();
    }

    // ---------------------------------------------------------------------------
    // Backup Status
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task GetBackupStatus_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.GetAsync("/api/v1/core/admin/backup/status");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to get backup status. Body: {await response.Content.ReadAsStringAsync()}");

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BackupStatusInfo>>();
        Assert.IsNotNull(envelope);
        Assert.IsTrue(envelope.Success);
        Assert.IsNotNull(envelope.Data);
        Assert.IsFalse(envelope.Data.IsRunning);
    }

    [TestMethod]
    public async Task GetBackupStatus_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/v1/core/admin/backup/status");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode,
            "Unauthenticated requests should be rejected.");
    }

    // ---------------------------------------------------------------------------
    // Run Backup
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task RunBackup_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await _adminClient.PostAsync("/api/v1/core/admin/backup/run", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            $"Admin should be able to trigger a backup. Body: {await response.Content.ReadAsStringAsync()}");

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BackupResult>>();
        Assert.IsNotNull(envelope);
        Assert.IsTrue(envelope.Success);
        Assert.IsNotNull(envelope.Data);
        Assert.IsTrue(envelope.Data.Success);
        Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.Data.FilePath));
        Assert.IsTrue(envelope.Data.FileCount > 0);
        Assert.IsTrue(envelope.Data.SizeBytes > 0);
    }

    [TestMethod]
    public async Task RunBackup_WithOutputPath_ReturnsOk()
    {
        // Arrange - use a temp path
        var tempDir = Path.Combine(Path.GetTempPath(), $"dnc-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "custom-backup.zip");

        try
        {
            // Act
            var response = await _adminClient.PostAsync(
                $"/api/v1/core/admin/backup/run?outputPath={Uri.EscapeDataString(outputPath)}", null);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"Backup with custom output path should succeed. Body: {await response.Content.ReadAsStringAsync()}");

            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BackupResult>>();
            Assert.IsNotNull(envelope);
            Assert.IsTrue(envelope.Success);
            Assert.IsTrue(envelope.Data?.Success ?? false);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task RunBackup_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.PostAsync("/api/v1/core/admin/backup/run", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode,
            "Unauthenticated backup trigger should be rejected.");
    }

    [TestMethod]
    public async Task RunBackup_AsRegularUser_ReturnsForbidden()
    {
        // Arrange
        var userClient = _factory.CreateAuthenticatedApiClient(Guid.NewGuid());

        // Act
        var response = await userClient.PostAsync("/api/v1/core/admin/backup/run", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
            "Non-admin users should not be able to trigger a backup.");
    }

    // ---------------------------------------------------------------------------
    // Restore Backup
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task RestoreBackup_MissingFile_ReturnsBadRequest()
    {
        // Act
        var response = await _adminClient.PostAsync(
            "/api/v1/core/admin/backup/restore", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "Missing file path should return BadRequest.");
    }

    [TestMethod]
    public async Task RestoreBackup_NonExistentFile_Returns500()
    {
        // Act
        var response = await _adminClient.PostAsync(
            "/api/v1/core/admin/backup/restore?filePath=/nonexistent/backup.zip", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode,
            "Restoring a non-existent file should fail.");
    }

    [TestMethod]
    public async Task RestoreBackup_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.PostAsync(
            "/api/v1/core/admin/backup/restore?filePath=test.zip", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode,
            "Unauthenticated restore should be rejected.");
    }

    // ---------------------------------------------------------------------------
    // Settings Integration (Backup settings CRUD)
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task BackupSettings_CRUD()
    {
        // Create
        var createResponse = await _adminClient.PutAsJsonAsync(
            "/api/v1/core/admin/settings/dotnetcloud.core/Backup:Enabled",
            new { Value = "true", Description = "Integration test" });
        Assert.AreEqual(HttpStatusCode.OK, createResponse.StatusCode);

        // Read
        var readResponse = await _adminClient.GetAsync(
            "/api/v1/core/admin/settings/dotnetcloud.core/Backup:Enabled");
        Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

        // Update
        var updateResponse = await _adminClient.PutAsJsonAsync(
            "/api/v1/core/admin/settings/dotnetcloud.core/Backup:Schedule",
            new { Value = "daily" });
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);

        // List
        var listResponse = await _adminClient.GetAsync(
            "/api/v1/core/admin/settings?module=dotnetcloud.core");
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Nested DTO for deserializing API envelope
    // ---------------------------------------------------------------------------

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public ApiError? Error { get; set; }
    }

    private sealed class ApiError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}
