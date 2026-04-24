using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class AdminSharedFolderPathValidatorTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static AdminSharedFolderPathValidator CreateValidator(FilesDbContext db, string rootPath = "")
    {
        return new AdminSharedFolderPathValidator(
            db,
            Microsoft.Extensions.Options.Options.Create(new AdminSharedFolderOptions
            {
                RootPath = rootPath,
            }));
    }

    [TestMethod]
    public async Task ValidateAsync_RelativePathWithinConfiguredRoot_ReturnsCanonicalPath()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            var nestedFolderPath = Directory.CreateDirectory(Path.Combine(rootPath, "media", "photos")).FullName;
            using var db = CreateContext();
            var validator = CreateValidator(db, rootPath);

            var result = await validator.ValidateAsync(Path.Combine("media", "photos"));

            Assert.AreEqual(Path.GetFullPath(nestedFolderPath), result.CanonicalPath);
            Assert.AreEqual("media/photos", result.RelativePath);
            Assert.AreEqual(Path.GetFullPath(rootPath), result.RootPath);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ValidateAsync_PathOutsideConfiguredRoot_ThrowsValidationException()
    {
        var rootPath = CreateTempDirectory();
        var outsideRootPath = CreateTempDirectory();

        try
        {
            var outsideFolderPath = Directory.CreateDirectory(Path.Combine(outsideRootPath, "music")).FullName;
            using var db = CreateContext();
            var validator = CreateValidator(db, rootPath);

            var result = await validator.ValidateAsync(outsideFolderPath);

            Assert.AreEqual(Path.GetFullPath(outsideFolderPath), result.CanonicalPath);
            Assert.AreEqual(GetPlatformRootPath(), result.RootPath);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
            Directory.Delete(outsideRootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ResolveDirectoryAsync_WithoutConfiguredRoot_UsesPlatformRoot()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            var nestedFolderPath = Directory.CreateDirectory(Path.Combine(rootPath, "media", "photos")).FullName;
            using var db = CreateContext();
            var validator = CreateValidator(db);

            var result = await validator.ResolveDirectoryAsync(nestedFolderPath);

            Assert.AreEqual(Path.GetFullPath(nestedFolderPath), result.CanonicalPath);
            Assert.AreEqual(GetPlatformRootPath(), result.RootPath);
            Assert.IsTrue(result.RelativePath.Length > 0);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ValidateAsync_OverlappingExistingDefinition_ThrowsValidationException()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            var existingFolderPath = Directory.CreateDirectory(Path.Combine(rootPath, "media")).FullName;
            Directory.CreateDirectory(Path.Combine(existingFolderPath, "albums"));
            using var db = CreateContext();
            db.AdminSharedFolders.Add(new AdminSharedFolderDefinition
            {
                DisplayName = "Media",
                SourcePath = Path.GetFullPath(existingFolderPath),
                CreatedByUserId = Guid.NewGuid(),
            });
            await db.SaveChangesAsync();

            var validator = CreateValidator(db, rootPath);

            await Assert.ThrowsExactlyAsync<ValidationException>(() => validator.ValidateAsync(Path.Combine("media", "albums")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ValidateAsync_PathThatIsNotDirectory_ThrowsValidationException()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(rootPath, "notes.txt");
            await File.WriteAllTextAsync(filePath, "hello");
            using var db = CreateContext();
            var validator = CreateValidator(db, rootPath);

            await Assert.ThrowsExactlyAsync<ValidationException>(() => validator.ValidateAsync("notes.txt"));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnetcloud-admin-share-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetPlatformRootPath()
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.GetPathRoot(Path.GetTempPath()) ?? Path.DirectorySeparatorChar.ToString()));
    }
}