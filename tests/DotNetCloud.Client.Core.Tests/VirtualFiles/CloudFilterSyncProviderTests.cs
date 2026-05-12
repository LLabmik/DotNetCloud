using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform.Windows;
using DotNetCloud.Client.Core.Transfer;
using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.Core.Tests.VirtualFiles;

/// <summary>
/// Helper assertion class for async exception testing.
/// </summary>
internal static class ExceptionAssert
{
    public static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        var threw = false;
        try
        {
            await action();
        }
        catch (TException)
        {
            threw = true;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }

        Assert.IsTrue(threw, $"Expected {typeof(TException).Name} but no exception was thrown.");
    }
}

[TestClass]
public sealed class CloudFilterSyncProviderTests
{
    private Mock<IChunkedTransferClient> _chunkedMock = null!;
    private Mock<ILocalStateDb> _stateDbMock = null!;
    private VirtualFileSettings _settings = null!;
    private CloudFilterSyncProvider _provider = null!;

    [TestInitialize]
    public void Initialize()
    {
        _chunkedMock = new Mock<IChunkedTransferClient>();
        _stateDbMock = new Mock<ILocalStateDb>();
        _settings = new VirtualFileSettings();

        _provider = new CloudFilterSyncProvider(
            _chunkedMock.Object,
            _stateDbMock.Object,
            _settings,
            NullLogger<CloudFilterSyncProvider>.Instance,
            NullLogger<CloudFilterCallbacks>.Instance);
    }

    // ── Constructor / Dispose ──────────────────────────────────────────

    [TestMethod]
    public void Constructor_DoesNotThrow()
    {
        Assert.IsNotNull(_provider);
    }

    [TestMethod]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        await _provider.DisposeAsync();
        await _provider.DisposeAsync(); // Should not throw
    }

    // ── Dispose guards ─────────────────────────────────────────────────

    [TestMethod]
    public async Task HydrateFileAsync_WhenDisposed_Throws()
    {
        await _provider.DisposeAsync();

        await ExceptionAssert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.HydrateFileAsync("/path/to/file", Guid.NewGuid()));
    }

    [TestMethod]
    public async Task CreatePlaceholdersAsync_WhenDisposed_Throws()
    {
        await _provider.DisposeAsync();

        await ExceptionAssert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.CreatePlaceholdersAsync(new DotNetCloud.Client.Core.Api.SyncTreeNodeResponse
            {
                Name = "root",
                NodeType = "Folder",
            }));
    }

    [TestMethod]
    public async Task DehydrateFileAsync_WhenDisposed_Throws()
    {
        await _provider.DisposeAsync();

        await ExceptionAssert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.DehydrateFileAsync("/path/to/file"));
    }

    [TestMethod]
    public async Task PinFileAsync_WhenDisposed_Throws()
    {
        await _provider.DisposeAsync();

        await ExceptionAssert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.PinFileAsync("/path/to/file"));
    }

    [TestMethod]
    public async Task UnpinFileAsync_WhenDisposed_Throws()
    {
        await _provider.DisposeAsync();

        await ExceptionAssert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.UnpinFileAsync("/path/to/file"));
    }

    [TestMethod]
    public async Task ShutdownAsync_WhenDisposed_Throws()
    {
        await _provider.DisposeAsync();

        await ExceptionAssert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.ShutdownAsync());
    }
}
