using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Integration.Tests.Api;

[TestClass]
[TestCategory("Integration")]
public sealed class AdminSharedFoldersEndpointTests
{
    [TestMethod]
    public async Task CreateListAndGet_SharedFolderDefinition_Works()
    {
        var adminUserId = Guid.NewGuid();
        var sharedRoot = Path.Combine(Path.GetTempPath(), $"dnc-shared-root-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(sharedRoot, "design-assets");
        Directory.CreateDirectory(sourceDirectory);

        using var baseFactory = new FilesHostWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Files:AdminSharedFolders:RootPath"] = sharedRoot,
                });
            });
        });
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        adminClient.DefaultRequestHeaders.Add("Accept", "application/json");
        adminClient.DefaultRequestHeaders.Add("x-test-user-id", adminUserId.ToString());
        adminClient.DefaultRequestHeaders.Add("x-test-user-roles", "Administrator");

        try
        {
            var createResponse = await adminClient.PostAsJsonAsync(
                "/api/v1/files/admin/shared-folders",
                new CreateAdminSharedFolderDto
                {
                    DisplayName = "Design Assets",
                    SourcePath = "design-assets",
                    IsEnabled = true,
                    AccessMode = "ReadOnly",
                    CrawlMode = "Scheduled",
                    GroupIds = [],
                });

            var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
            var created = DataOrRoot(createRoot);
            var sharedFolderId = created.GetProperty("id").GetGuid();

            Assert.AreEqual("Design Assets", created.GetProperty("displayName").GetString());
            Assert.AreEqual(Path.GetFullPath(sourceDirectory), created.GetProperty("sourcePath").GetString());
            Assert.AreEqual("ReadOnly", created.GetProperty("accessMode").GetString());
            Assert.AreEqual("Scheduled", created.GetProperty("crawlMode").GetString());
            Assert.AreEqual(JsonValueKind.String, created.GetProperty("nextScheduledScanAt").ValueKind);
            Assert.AreEqual(0, created.GetProperty("grantedGroups").GetArrayLength());

            var listResponse = await adminClient.GetAsync("/api/v1/files/admin/shared-folders");
            var listRoot = await ApiAssert.SuccessAsync(listResponse, HttpStatusCode.OK);
            var list = DataOrRoot(listRoot);

            Assert.AreEqual(1, list.GetArrayLength());
            Assert.AreEqual(sharedFolderId, list[0].GetProperty("id").GetGuid());
            Assert.AreEqual("Design Assets", list[0].GetProperty("displayName").GetString());

            var getResponse = await adminClient.GetAsync($"/api/v1/files/admin/shared-folders/{sharedFolderId}");
            var getRoot = await ApiAssert.SuccessAsync(getResponse, HttpStatusCode.OK);
            var fetched = DataOrRoot(getRoot);

            Assert.AreEqual(sharedFolderId, fetched.GetProperty("id").GetGuid());
            Assert.AreEqual("Design Assets", fetched.GetProperty("displayName").GetString());
            Assert.AreEqual(Path.GetFullPath(sourceDirectory), fetched.GetProperty("sourcePath").GetString());
        }
        finally
        {
            if (Directory.Exists(sharedRoot))
            {
                Directory.Delete(sharedRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task BrowseDirectories_ReturnsImmediateSubdirectoriesWithinConfiguredRoot()
    {
        var adminUserId = Guid.NewGuid();
        var sharedRoot = Path.Combine(Path.GetTempPath(), $"dnc-shared-browse-{Guid.NewGuid():N}");
        var designAssets = Directory.CreateDirectory(Path.Combine(sharedRoot, "design-assets")).FullName;
        var photos = Directory.CreateDirectory(Path.Combine(sharedRoot, "photos")).FullName;
        var raw = Directory.CreateDirectory(Path.Combine(photos, "raw")).FullName;

        using var baseFactory = new FilesHostWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Files:AdminSharedFolders:RootPath"] = sharedRoot,
                });
            });
        });
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        adminClient.DefaultRequestHeaders.Add("Accept", "application/json");
        adminClient.DefaultRequestHeaders.Add("x-test-user-id", adminUserId.ToString());
        adminClient.DefaultRequestHeaders.Add("x-test-user-roles", "Administrator");

        try
        {
            var rootBrowseResponse = await adminClient.GetAsync("/api/v1/files/admin/shared-folders/browse");
            var rootBrowse = DataOrRoot(await ApiAssert.SuccessAsync(rootBrowseResponse, HttpStatusCode.OK));

            var filesystemRoot = GetPlatformRootPath();
            Assert.AreEqual(filesystemRoot, rootBrowse.GetProperty("rootPath").GetString());
            Assert.AreEqual(filesystemRoot, rootBrowse.GetProperty("currentPath").GetString());

            var scopedBrowseResponse = await adminClient.GetAsync($"/api/v1/files/admin/shared-folders/browse?path={Uri.EscapeDataString(sharedRoot)}");
            var scopedBrowse = DataOrRoot(await ApiAssert.SuccessAsync(scopedBrowseResponse, HttpStatusCode.OK));

            Assert.AreEqual(filesystemRoot, scopedBrowse.GetProperty("rootPath").GetString());
            Assert.AreEqual(Path.GetFullPath(sharedRoot), scopedBrowse.GetProperty("currentPath").GetString());
            CollectionAssert.AreEqual(
                new[] { "design-assets", "photos" },
                scopedBrowse.GetProperty("directories")
                    .EnumerateArray()
                    .Select(directory => directory.GetProperty("name").GetString())
                    .ToArray());

            var nestedBrowseResponse = await adminClient.GetAsync($"/api/v1/files/admin/shared-folders/browse?path={Uri.EscapeDataString(photos)}");
            var nestedBrowse = DataOrRoot(await ApiAssert.SuccessAsync(nestedBrowseResponse, HttpStatusCode.OK));

            Assert.AreEqual(Path.GetFullPath(photos), nestedBrowse.GetProperty("currentPath").GetString());
            Assert.AreEqual(Path.GetFullPath(photos).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/'), nestedBrowse.GetProperty("relativePath").GetString());
            Assert.AreEqual("raw", nestedBrowse.GetProperty("directories")[0].GetProperty("name").GetString());
            Assert.AreEqual(Path.GetFullPath(raw), nestedBrowse.GetProperty("directories")[0].GetProperty("sourcePath").GetString());
            Assert.AreEqual(Path.GetFullPath(designAssets), scopedBrowse.GetProperty("directories")[0].GetProperty("sourcePath").GetString());
        }
        finally
        {
            if (Directory.Exists(sharedRoot))
            {
                Directory.Delete(sharedRoot, recursive: true);
            }
        }
    }

    private static string GetPlatformRootPath()
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.GetPathRoot(Path.GetTempPath()) ?? Path.DirectorySeparatorChar.ToString()));
    }

    [TestMethod]
    public async Task ListMountedSharedFolderAndRejectWrites_Works()
    {
        var adminUserId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var sharedRoot = Path.Combine(Path.GetTempPath(), $"dnc-mounted-root-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(sharedRoot, "team-handbook");
        var nestedDirectory = Path.Combine(sourceDirectory, "Policies");
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory, "leave-policy.md"), "Use paid leave responsibly.");

        using var baseFactory = new FilesHostWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Files:AdminSharedFolders:RootPath"] = sharedRoot,
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IShareAccessMembershipResolver>();
                services.AddScoped<IShareAccessMembershipResolver>(_ => new StubShareAccessMembershipResolver(groupId));
            });
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var sharedFolder = new AdminSharedFolderDefinition
        {
            DisplayName = "Team Handbook",
            SourcePath = Path.GetFullPath(sourceDirectory),
            IsEnabled = true,
            CreatedByUserId = adminUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Grants =
            {
                new AdminSharedFolderGrant
                {
                    GroupId = groupId,
                    CreatedAt = DateTime.UtcNow,
                },
            },
        };
        db.AdminSharedFolders.Add(sharedFolder);
        await db.SaveChangesAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("x-test-user-id", userId.ToString());

        try
        {
            var rootResponse = await client.GetAsync("/api/v1/files");
            var rootNodes = await ApiAssert.SuccessAsync(rootResponse, HttpStatusCode.OK);
            var dotNetCloudRoot = rootNodes
                .EnumerateArray()
                .Single(node => string.Equals(node.GetProperty("name").GetString(), "_DotNetCloud", StringComparison.Ordinal));
            var dotNetCloudRootId = dotNetCloudRoot.GetProperty("id").GetGuid();

            Assert.IsTrue(dotNetCloudRoot.GetProperty("isVirtual").GetBoolean());
            Assert.IsTrue(dotNetCloudRoot.GetProperty("isReadOnly").GetBoolean());

            var dotNetCloudChildrenResponse = await client.GetAsync($"/api/v1/files?parentId={dotNetCloudRootId}");
            var dotNetCloudChildren = await ApiAssert.SuccessAsync(dotNetCloudChildrenResponse, HttpStatusCode.OK);
            var mountedRoot = dotNetCloudChildren
                .EnumerateArray()
                .Single(node => string.Equals(node.GetProperty("name").GetString(), "Team Handbook", StringComparison.Ordinal));
            var mountedRootId = mountedRoot.GetProperty("id").GetGuid();

            Assert.AreEqual("AdminSharedFolder", mountedRoot.GetProperty("virtualSourceKind").GetString());
            Assert.IsTrue(mountedRoot.GetProperty("isReadOnly").GetBoolean());

            var mountedChildrenResponse = await client.GetAsync($"/api/v1/files?parentId={mountedRootId}");
            var mountedChildren = await ApiAssert.SuccessAsync(mountedChildrenResponse, HttpStatusCode.OK);
            var policies = mountedChildren
                .EnumerateArray()
                .Single(node => string.Equals(node.GetProperty("name").GetString(), "Policies", StringComparison.Ordinal));
            var policiesId = policies.GetProperty("id").GetGuid();

            Assert.IsTrue(policies.GetProperty("isReadOnly").GetBoolean());
            Assert.AreEqual("Folder", policies.GetProperty("nodeType").GetString());

            var nestedChildrenResponse = await client.GetAsync($"/api/v1/files?parentId={policiesId}");
            var nestedChildren = await ApiAssert.SuccessAsync(nestedChildrenResponse, HttpStatusCode.OK);
            var policyFile = nestedChildren
                .EnumerateArray()
                .Single(node => string.Equals(node.GetProperty("name").GetString(), "leave-policy.md", StringComparison.Ordinal));

            Assert.AreEqual("File", policyFile.GetProperty("nodeType").GetString());
            Assert.IsTrue(policyFile.GetProperty("isReadOnly").GetBoolean());

            var createFolderResponse = await client.PostAsJsonAsync(
                "/api/v1/files/folders",
                new CreateFolderDto
                {
                    Name = "Blocked",
                    ParentId = mountedRootId,
                });

            var errorRoot = await ApiAssert.ErrorAsync(createFolderResponse, HttpStatusCode.BadRequest, "DB_INVALID_OPERATION");
            Assert.AreEqual(
                "Mounted shared folders are read-only in this first delivery.",
                errorRoot.GetProperty("error").GetProperty("message").GetString());
        }
        finally
        {
            if (Directory.Exists(sharedRoot))
            {
                Directory.Delete(sharedRoot, recursive: true);
            }
        }
    }

    private static JsonElement DataOrRoot(JsonElement root)
    {
        var current = root;

        while (current.ValueKind == JsonValueKind.Object &&
               current.TryGetProperty("data", out var nested))
        {
            current = nested;
        }

        return current;
    }

    private sealed class StubShareAccessMembershipResolver(Guid groupId) : IShareAccessMembershipResolver
    {
        public Task<ShareAccessMembership> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShareAccessMembership
            {
                GroupIds = [groupId],
            });
        }
    }
}