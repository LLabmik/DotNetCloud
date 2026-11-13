using System.IO.Compression;
using System.Security.Cryptography;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.ModulePackaging.Tests;

/// <summary>
/// Packing, verification, extraction and feed tests for `.dncpkg` (B1).
/// </summary>
[TestClass]
public class ModulePackageTests
{
    private PackageFixture _fixture = null!;

    [TestInitialize]
    public void Setup() => _fixture = new PackageFixture();

    [TestCleanup]
    public void Cleanup() => _fixture.Dispose();

    private static ModulePackageVerificationOptions Trusted(PackageFixture fixture, bool allowUnsigned = false) => new()
    {
        TrustedKeys = fixture.TrustedKeys,
        AllowUnsigned = allowUnsigned,
    };

    // -----------------------------------------------------------------------
    // Determinism
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task WriteAsync_SameInputs_ProducesByteIdenticalArchives()
    {
        var staging = _fixture.CreateStaging();

        var first = await _fixture.PackAsync(staging, sign: false);
        var second = await _fixture.PackAsync(staging, sign: false);

        var firstBytes = await File.ReadAllBytesAsync(first.PackagePath);
        var secondBytes = await File.ReadAllBytesAsync(second.PackagePath);

        CollectionAssert.AreEqual(firstBytes, secondBytes);
        Assert.AreEqual(first.Build.Sha256, second.Build.Sha256);
    }

    [TestMethod]
    public async Task WriteAsync_OrdersEntriesOrdinalAndUsesFixedTimestamps()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        using var archive = ZipFile.OpenRead(packagePath);
        var names = archive.Entries.Select(entry => entry.FullName).ToList();

        CollectionAssert.AreEqual(
            names.OrderBy(name => name, StringComparer.Ordinal).ToList(),
            names);

        foreach (var entry in archive.Entries)
        {
            // Zip stores a local wall-clock time (no offset), so compare the wall clock we wrote.
            Assert.AreEqual(ModulePackageFormat.EntryTimestamp.DateTime, entry.LastWriteTime.DateTime);
        }
    }

    [TestMethod]
    public async Task WriteAsync_EmptyEntrySet_Fails()
    {
        var output = Path.Combine(_fixture.Root, "empty.dncpkg");

        await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => DeterministicPackageWriter.WriteAsync(output, []));
    }

    [TestMethod]
    public async Task WriteAsync_MissingSourceFile_ReportsMissingFile()
    {
        var output = Path.Combine(_fixture.Root, "missing.dncpkg");

        var exception = await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => DeterministicPackageWriter.WriteAsync(
                output,
                [new PackageEntrySource("manifest.json", Path.Combine(_fixture.Root, "absent.json"))]));

        Assert.IsTrue(exception.Issues.Any(issue => issue.Code == PackageIssueCodes.SourceFileMissing));
    }

    // -----------------------------------------------------------------------
    // Signing and verification
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task VerifyAsync_SignedPackageWithTrustedKey_Succeeds()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, build) = await _fixture.PackAsync(staging);

        var verification = await ModulePackageVerifier.VerifyAsync(packagePath, options: Trusted(_fixture));

        Assert.IsTrue(verification.IsValid, string.Join("; ", verification.Issues));
        Assert.AreEqual(PackageFixture.DefaultModuleId, verification.ModuleId);
        Assert.AreEqual("1.0.0", verification.Version);
        Assert.AreEqual(_fixture.KeyPair.KeyId, verification.KeyId);
        Assert.AreEqual(build.Sha256, verification.Sha256);
    }

    [TestMethod]
    public async Task VerifyAsync_TamperedPackage_FailsHashVerification()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging);

        // Flip a byte inside the archive; the stored hash and signature no longer match.
        var bytes = await File.ReadAllBytesAsync(packagePath);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(packagePath, bytes);

        var verification = await ModulePackageVerifier.VerifyAsync(packagePath, options: Trusted(_fixture));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(
            verification.Issues.Any(issue =>
                issue.Code is PackageIssueCodes.HashMismatch or PackageIssueCodes.InvalidZip or PackageIssueCodes.SignatureInvalid),
            string.Join("; ", verification.Issues));
    }

    [TestMethod]
    public async Task VerifyAsync_UntrustedKeyId_Fails()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath,
            options: new ModulePackageVerificationOptions
            {
                TrustedKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["other-key"] = _fixture.KeyPair.PublicKey,
                },
            });

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureUntrustedKey));
    }

    [TestMethod]
    public async Task VerifyAsync_SignatureFromAnotherKey_FailsEd25519Check()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging);

        var otherKey = Ed25519Signature.GenerateKeyPair(_fixture.KeyPair.KeyId);
        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath,
            options: new ModulePackageVerificationOptions
            {
                TrustedKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [otherKey.KeyId] = otherKey.PublicKey,
                },
            });

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureInvalid));
    }

    [TestMethod]
    public async Task VerifyAsync_UnsignedPackage_RequiresAllowUnverified()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var strict = await ModulePackageVerifier.VerifyAsync(packagePath, options: Trusted(_fixture));
        var sideload = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(strict.IsValid);
        Assert.IsTrue(strict.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureMissing));
        Assert.IsTrue(sideload.IsValid, string.Join("; ", sideload.Issues));
    }

    [TestMethod]
    public async Task VerifyAsync_SignatureForDifferentFileName_Fails()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging);

        // Rename the package but keep the signature: the signature names the original file.
        var renamed = Path.Combine(_fixture.Root, "dotnetcloud.example-0.9.0.dncpkg");
        File.Move(packagePath, renamed);
        File.Move(ModulePackageFormat.GetSignaturePath(packagePath), ModulePackageFormat.GetSignaturePath(renamed));

        var verification = await ModulePackageVerifier.VerifyAsync(renamed, options: Trusted(_fixture));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureInvalid));
    }

    [TestMethod]
    public async Task VerifyAsync_NonEd25519Algorithm_Fails()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging);

        var signaturePath = ModulePackageFormat.GetSignaturePath(packagePath);
        var signature = ModulePackageSigner.Read(signaturePath);
        var tampered = new ModulePackageSignatureFile
        {
            KeyId = signature.KeyId,
            Algorithm = "rsa-pkcs1",
            File = signature.File,
            Sha256 = signature.Sha256,
            Signature = signature.Signature,
        };
        await File.WriteAllTextAsync(signaturePath, ModulePackageSigner.Serialize(tampered));

        var verification = await ModulePackageVerifier.VerifyAsync(packagePath, options: Trusted(_fixture));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureInvalid));
    }

    [TestMethod]
    public void Ed25519Signature_VerifyRejectsTamperedDigest()
    {
        var digest = RandomNumberGenerator.GetBytes(32);
        var signature = Ed25519Signature.Sign(digest, _fixture.KeyPair.PrivateKey);

        Assert.IsTrue(Ed25519Signature.Verify(digest, _fixture.KeyPair.PublicKey, signature));

        var tampered = (byte[])digest.Clone();
        tampered[0] ^= 0x01;

        Assert.IsFalse(Ed25519Signature.Verify(tampered, _fixture.KeyPair.PublicKey, signature));
    }

    // -----------------------------------------------------------------------
    // Manifest validation
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task VerifyAsync_ManifestIdDoesNotMatchFileName_Fails()
    {
        var staging = _fixture.CreateStaging();

        // Pack under the manifest id, then rename to a different id.
        var (packagePath, _) = await _fixture.PackAsync(staging);
        var renamed = Path.Combine(_fixture.Root, "org.example.other-1.0.0.dncpkg");
        File.Move(packagePath, renamed);
        File.Move(ModulePackageFormat.GetSignaturePath(packagePath), ModulePackageFormat.GetSignaturePath(renamed));

        var verification = await ModulePackageVerifier.VerifyAsync(
            renamed, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.ManifestMismatch));
    }

    [TestMethod]
    public async Task VerifyAsync_ManifestVersionDoesNotMatchFileName_Fails()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging);

        var renamed = Path.Combine(_fixture.Root, "dotnetcloud.example-2.0.0.dncpkg");
        File.Move(packagePath, renamed);

        var verification = await ModulePackageVerifier.VerifyAsync(
            renamed, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.ManifestMismatch));
    }

    [TestMethod]
    public async Task VerifyAsync_StrictV2ManifestWithUnknownField_Fails()
    {
        const string manifest = """
            {
              "manifestVersion": 2,
              "id": "dotnetcloud.example",
              "name": "Example",
              "version": "1.0.0",
              "minCoreVersion": "0.7.0",
              "dataStore": "none",
              "surprise": true
            }
            """;

        var staging = _fixture.CreateStaging(manifestOverride: manifest);
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.ManifestInvalid));
    }

    [TestMethod]
    public async Task VerifyAsync_InvalidDataStore_Fails()
    {
        const string manifest = """
            {
              "manifestVersion": 2,
              "id": "dotnetcloud.example",
              "name": "Example",
              "version": "1.0.0",
              "minCoreVersion": "0.7.0",
              "dataStore": "shared"
            }
            """;

        var staging = _fixture.CreateStaging(manifestOverride: manifest);
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.ManifestInvalid));
    }

    [TestMethod]
    public async Task VerifyAsync_MinCoreVersionAheadOfThisCore_Fails()
    {
        const string manifest = """
            {
              "manifestVersion": 2,
              "id": "dotnetcloud.example",
              "name": "Example",
              "version": "1.0.0",
              "minCoreVersion": "99.0.0",
              "dataStore": "none"
            }
            """;

        var staging = _fixture.CreateStaging(manifestOverride: manifest);
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.IncompatibleCore));
    }

    [TestMethod]
    public async Task VerifyAsync_UnparsableMinCoreVersion_Fails()
    {
        const string manifest = """
            {
              "manifestVersion": 2,
              "id": "dotnetcloud.example",
              "name": "Example",
              "version": "1.0.0",
              "minCoreVersion": "banana",
              "dataStore": "none"
            }
            """;

        var staging = _fixture.CreateStaging(manifestOverride: manifest);
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.ManifestInvalid));
    }

    [TestMethod]
    public async Task VerifyAsync_LegacyV1Manifest_IsValidWithWarning()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsTrue(verification.IsValid, string.Join("; ", verification.Issues));
        Assert.IsTrue(verification.Warnings.Any(warning => warning.Code == "MANIFEST_LEGACY_V1"));
    }

    // -----------------------------------------------------------------------
    // Required entries
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task VerifyAsync_MissingRequiredEntry_Fails()
    {
        var staging = _fixture.CreateStaging(skipEntries: [ModulePackageFormat.ChangelogEntryName]);
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue =>
            issue.Code == PackageIssueCodes.MissingEntry
            && issue.EntryName == ModulePackageFormat.ChangelogEntryName));
    }

    [TestMethod]
    public async Task VerifyAsync_NoRidBinaries_Fails()
    {
        var staging = _fixture.CreateStaging(rid: null);
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var verification = await ModulePackageVerifier.VerifyAsync(
            packagePath, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue =>
            issue.Code == PackageIssueCodes.MissingEntry
            && issue.EntryName == ModulePackageFormat.FilesDirectoryName));
    }

    [TestMethod]
    public async Task VerifyAsync_UnparseableFileName_Fails()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var renamed = Path.Combine(_fixture.Root, "package.dncpkg");
        File.Move(packagePath, renamed);

        var verification = await ModulePackageVerifier.VerifyAsync(
            renamed, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.FileNameInvalid));
    }

    // -----------------------------------------------------------------------
    // Hostile archives
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task VerifyAsync_TraversalEntry_Fails()
    {
        var path = _fixture.WriteRawArchive(
            "dotnetcloud.example-1.0.0.dncpkg",
            StandardRawEntries(("files/linux-x64/x.dll", "x"), ("../evil.txt", "pwned")));

        var verification = await ModulePackageVerifier.VerifyAsync(
            path, options: Trusted(_fixture, allowUnsigned: true));

        Assert.IsFalse(verification.IsValid);
        Assert.IsTrue(verification.Issues.Any(issue => issue.Code == PackageIssueCodes.PathTraversal));
    }

    [TestMethod]
    public async Task ExtractAsync_TraversalEntry_ThrowsAndWritesNothing()
    {
        var path = _fixture.WriteRawArchive(
            "dotnetcloud.example-1.0.0.dncpkg",
            StandardRawEntries(("files/linux-x64/x.dll", "x"), ("../escape.txt", "pwned")));

        var destination = Path.Combine(_fixture.Root, "extract");

        await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => ModulePackageVerifier.ExtractAsync(path, destination));

        Assert.IsFalse(File.Exists(Path.Combine(_fixture.Root, "escape.txt")));
    }

    [TestMethod]
    public async Task ExtractAsync_ValidPackage_WritesEntriesInsideRoot()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);

        var destination = Path.Combine(_fixture.Root, "extract-ok");
        var extracted = await ModulePackageVerifier.ExtractAsync(packagePath, destination);

        CollectionAssert.Contains(extracted.ToList(), "manifest.json");
        CollectionAssert.Contains(extracted.ToList(), "files/linux-x64/dotnetcloud.example.dll");
        Assert.IsTrue(File.Exists(Path.Combine(destination, "manifest.json")));
    }

    [TestMethod]
    public async Task ExtractAsync_SymlinkEntry_Throws()
    {
        var path = Path.Combine(_fixture.Root, "dotnetcloud.example-1.0.0.dncpkg");

        using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in StandardRawEntries(("files/linux-x64/x.dll", "x")))
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }

            var link = archive.CreateEntry("files/linux-x64/link");
            // unix S_IFLNK (0xA000) | 0777 in the high 16 bits.
            link.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var linkWriter = new StreamWriter(link.Open());
            linkWriter.Write("/etc/passwd");
        }

        await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => ModulePackageVerifier.ExtractAsync(path, Path.Combine(_fixture.Root, "extract-link")));
    }

    // -----------------------------------------------------------------------
    // Local feed
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task Feed_AddThenRead_RoundTripsPackageFacts()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, build) = await _fixture.PackAsync(staging);
        var feed = Path.Combine(_fixture.Root, "feed");

        var entry = await ModuleFeed.AddAsync(feed, packagePath, ModulePackageSigner.Read(
            ModulePackageFormat.GetSignaturePath(packagePath)));

        var index = await ModuleFeed.ReadAsync(feed);

        Assert.AreEqual(1, index.Packages.Count);
        Assert.AreEqual(PackageFixture.DefaultModuleId, entry.Id);
        Assert.AreEqual(build.Sha256, entry.Sha256);
        Assert.AreEqual(_fixture.KeyPair.KeyId, entry.KeyId);
        Assert.AreEqual("dotnetcloud.example-1.0.0.dncpkg", entry.File);
    }

    [TestMethod]
    public async Task Feed_AddReplacesSameVersion()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);
        var feed = Path.Combine(_fixture.Root, "feed");

        await ModuleFeed.AddAsync(feed, packagePath, null);
        await ModuleFeed.AddAsync(feed, packagePath, null);

        var index = await ModuleFeed.ReadAsync(feed);
        Assert.AreEqual(1, index.Packages.Count);
    }

    [TestMethod]
    public async Task Feed_VerifyEntry_UsesIndexSignatureWhenNoSigFile()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: true);
        var feed = Path.Combine(_fixture.Root, "feed");
        Directory.CreateDirectory(feed);

        var feedPackage = Path.Combine(feed, Path.GetFileName(packagePath));
        File.Copy(packagePath, feedPackage);

        var signature = ModulePackageSigner.Read(ModulePackageFormat.GetSignaturePath(packagePath));
        await ModuleFeed.AddAsync(feed, feedPackage, signature);

        var verification = await ModuleFeed.VerifyEntryAsync(
            feed,
            PackageFixture.DefaultModuleId,
            "1.0.0",
            new ModulePackageVerificationOptions { TrustedKeys = _fixture.TrustedKeys });

        Assert.IsTrue(verification.IsValid, string.Join("; ", verification.Issues));
        Assert.AreEqual(_fixture.KeyPair.KeyId, verification.KeyId);
    }

    [TestMethod]
    public async Task Feed_VerifyEntry_RejectsFileOutsideFeedDirectory()
    {
        var staging = _fixture.CreateStaging();
        var (packagePath, _) = await _fixture.PackAsync(staging, sign: false);
        var feed = Path.Combine(_fixture.Root, "feed");
        Directory.CreateDirectory(feed);

        await ModuleFeed.WriteAsync(
            feed,
            new ModuleFeedIndex
            {
                Packages =
                [
                    new ModuleFeedEntry(
                        PackageFixture.DefaultModuleId, "Example", "1.0.0",
                        "../" + Path.GetFileName(packagePath), 1, "00"),
                ],
            });

        await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => ModuleFeed.VerifyEntryAsync(
                feed, PackageFixture.DefaultModuleId, "1.0.0",
                new ModulePackageVerificationOptions { AllowUnsigned = true }));
    }

    [TestMethod]
    public void TryParseFileName_HandlesHyphenatedIds()
    {
        Assert.IsTrue(ModulePackageFormat.TryParseFileName(
            "org.example.receipt-scanner-1.2.3.dncpkg", out var moduleId, out var version));

        Assert.AreEqual("org.example.receipt-scanner", moduleId);
        Assert.AreEqual("1.2.3", version);
        Assert.IsFalse(ModulePackageFormat.TryParseFileName("not-a-package.zip", out _, out _));
    }

    private static (string Name, string Content)[] StandardRawEntries(params (string Name, string Content)[] extra)
    {
        var entries = new List<(string Name, string Content)>
        {
            ("manifest.json", """
                {
                  "manifestVersion": 2,
                  "id": "dotnetcloud.example",
                  "name": "Example",
                  "version": "1.0.0",
                  "minCoreVersion": "0.7.0",
                  "dataStore": "none"
                }
                """),
            ("LICENSE", "licence"),
            ("README.md", "# readme"),
            ("CHANGELOG.md", "# changelog"),
        };

        entries.AddRange(extra);
        return entries.ToArray();
    }
}
