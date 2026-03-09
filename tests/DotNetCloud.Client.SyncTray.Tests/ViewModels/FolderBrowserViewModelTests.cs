using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.SyncService.Ipc;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.ViewModels;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class FolderBrowserViewModelTests
{
    private readonly Guid _contextId = Guid.NewGuid();

    [TestMethod]
    public async Task Load_BuildsTreeFromApiResponse()
    {
        // Arrange: two-level tree.
        var tree = new SyncTreeNodeResponse
        {
            NodeId = Guid.NewGuid(),
            Name = "root",
            NodeType = "Folder",
            Children =
            [
                new SyncTreeNodeResponse
                {
                    NodeId = Guid.NewGuid(),
                    Name = "Documents",
                    NodeType = "Folder",
                    Children =
                    [
                        new SyncTreeNodeResponse
                        {
                            NodeId = Guid.NewGuid(),
                            Name = "Projects",
                            NodeType = "Folder",
                        },
                    ],
                },
                new SyncTreeNodeResponse
                {
                    NodeId = Guid.NewGuid(),
                    Name = "Photos",
                    NodeType = "Folder",
                },
                new SyncTreeNodeResponse
                {
                    NodeId = Guid.NewGuid(),
                    Name = "readme.txt",
                    NodeType = "File", // Should be excluded from tree
                },
            ],
        };

        var ipc = new Mock<IIpcClient>();
        ipc.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var vm = new FolderBrowserViewModel(ipc.Object, _contextId, selectiveSync, "test-config.json");

        // Act
        await vm.LoadTreeAsync();

        // Assert
        Assert.AreEqual(2, vm.RootItems.Count, "Should have 2 folders (Documents, Photos — no files).");
        Assert.AreEqual("Documents", vm.RootItems[0].Name);
        Assert.AreEqual("Photos", vm.RootItems[1].Name);
        Assert.AreEqual(1, vm.RootItems[0].Children.Count);
        Assert.AreEqual("Projects", vm.RootItems[0].Children[0].Name);
        Assert.AreEqual("Documents/Projects", vm.RootItems[0].Children[0].RelativePath);
        Assert.IsFalse(vm.IsLoading);
        Assert.IsNull(vm.ErrorMessage);
    }

    [TestMethod]
    public async Task Save_ExcludesUncheckedFolders()
    {
        // Arrange: build a tree and uncheck one folder.
        var tree = new SyncTreeNodeResponse
        {
            NodeId = Guid.NewGuid(),
            Name = "root",
            NodeType = "Folder",
            Children =
            [
                new SyncTreeNodeResponse
                {
                    NodeId = Guid.NewGuid(),
                    Name = "Documents",
                    NodeType = "Folder",
                },
                new SyncTreeNodeResponse
                {
                    NodeId = Guid.NewGuid(),
                    Name = "Trash",
                    NodeType = "Folder",
                },
            ],
        };

        var ipc = new Mock<IIpcClient>();
        ipc.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var tempFile = Path.GetTempFileName();
        try
        {
            var vm = new FolderBrowserViewModel(ipc.Object, _contextId, selectiveSync, tempFile);
            await vm.LoadTreeAsync();

            // Act: uncheck "Trash"
            vm.RootItems[1].IsChecked = false;
            await vm.SaveAsync();

            // Assert
            var rules = selectiveSync.GetRules(_contextId);
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual("Trash", rules[0].FolderPath);
            Assert.IsFalse(rules[0].IsInclude);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

[TestClass]
public sealed class FolderBrowserItemViewModelTests
{
    [TestMethod]
    public void CheckParent_PropagatesChildren()
    {
        // Arrange
        var parent = new FolderBrowserItemViewModel(Guid.NewGuid(), "Root", "Root");
        var child1 = new FolderBrowserItemViewModel(Guid.NewGuid(), "A", "Root/A") { Parent = parent };
        var child2 = new FolderBrowserItemViewModel(Guid.NewGuid(), "B", "Root/B") { Parent = parent };
        parent.Children.Add(child1);
        parent.Children.Add(child2);

        // Act: uncheck parent
        parent.IsChecked = false;

        // Assert: all children should be unchecked.
        Assert.AreEqual(false, child1.IsChecked);
        Assert.AreEqual(false, child2.IsChecked);

        // Act: re-check parent
        parent.IsChecked = true;

        // Assert: all children should be checked.
        Assert.AreEqual(true, child1.IsChecked);
        Assert.AreEqual(true, child2.IsChecked);
    }

    [TestMethod]
    public void MixedChildren_ParentIndeterminate()
    {
        // Arrange
        var parent = new FolderBrowserItemViewModel(Guid.NewGuid(), "Root", "Root");
        var child1 = new FolderBrowserItemViewModel(Guid.NewGuid(), "A", "Root/A") { Parent = parent };
        var child2 = new FolderBrowserItemViewModel(Guid.NewGuid(), "B", "Root/B") { Parent = parent };
        parent.Children.Add(child1);
        parent.Children.Add(child2);

        // Act: check one, uncheck the other
        child1.IsChecked = true;
        child2.IsChecked = false;

        // Assert: parent should be indeterminate (null).
        Assert.IsNull(parent.IsChecked);
    }
}
