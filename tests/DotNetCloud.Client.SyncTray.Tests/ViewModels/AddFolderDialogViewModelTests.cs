using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.SyncTray.Views;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class AddFolderDialogViewModelTests
{
    private static AddFolderDialogViewModel CreateVm(
        Mock<ISyncContextManager> syncMock,
        IReadOnlyList<string>? existingRoots = null)
    {
        return new AddFolderDialogViewModel(null!, syncMock.Object, Guid.CreateVersion7(), existingRoots ?? []);
    }

    [TestMethod]
    public void DeriveRemoteFolderName_FromPath_ReturnsLeafName()
    {
        Assert.AreEqual("Docs", AddFolderDialogViewModel.DeriveRemoteFolderName("/home/user/Docs"));
        Assert.AreEqual("Docs", AddFolderDialogViewModel.DeriveRemoteFolderName("/home/user/Docs/"));
    }

    [TestMethod]
    public async Task Confirm_NoLocalFolder_SetsError()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Confirm_ExistingMode_NoSelection_SetsError()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = true;

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_EmptyName_SetsError()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "   ";

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_CreatesAtRoot_AndSetsResult()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var contextId = Guid.CreateVersion7();
        var vm = new AddFolderDialogViewModel(null!, syncMock.Object, contextId, []);

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "Docs";

        var created = new FileNodeResponse { Id = Guid.CreateVersion7(), Name = "Docs", NodeType = "Folder" };
        syncMock
            .Setup(i => i.CreateRemoteFolderAsync(contextId, "Docs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        await vm.ConfirmAsync();

        Assert.IsNotNull(vm.DialogResult);
        Assert.AreEqual(created.Id, vm.DialogResult!.RemoteFolderNodeId);
        Assert.AreEqual("/Docs", vm.DialogResult.RemoteFolderPath);
        Assert.AreEqual("/tmp/new-folder", vm.DialogResult.LocalFolderPath);
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(contextId, "Docs", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_WithParent_UsesParentAndNestedPath()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var contextId = Guid.CreateVersion7();
        var parentId = Guid.CreateVersion7();
        var vm = new AddFolderDialogViewModel(null!, syncMock.Object, contextId, []);

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "Docs";
        vm.SetParentFolder(parentId, "Work/Project");

        var created = new FileNodeResponse { Id = Guid.CreateVersion7(), Name = "Docs", NodeType = "Folder" };
        syncMock
            .Setup(i => i.CreateRemoteFolderAsync(contextId, "Docs", parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        await vm.ConfirmAsync();

        Assert.IsNotNull(vm.DialogResult);
        Assert.AreEqual("/Work/Project/Docs", vm.DialogResult!.RemoteFolderPath);
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(contextId, "Docs", parentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_DuplicateName_SetsError_KeepsDialogOpen()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var contextId = Guid.CreateVersion7();
        var vm = new AddFolderDialogViewModel(null!, syncMock.Object, contextId, []);

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "Docs";

        syncMock
            .Setup(i => i.CreateRemoteFolderAsync(contextId, "Docs", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A folder named Docs already exists."));

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [TestMethod]
    public async Task Confirm_ExistingMode_UsesSelectedNode()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        var existingId = Guid.CreateVersion7();

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = true;
        vm.SetExistingRemoteFolder(existingId, "Documents");

        await vm.ConfirmAsync();

        Assert.IsNotNull(vm.DialogResult);
        Assert.AreEqual(existingId, vm.DialogResult!.RemoteFolderNodeId);
        Assert.AreEqual("/Documents", vm.DialogResult.RemoteFolderPath);
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
