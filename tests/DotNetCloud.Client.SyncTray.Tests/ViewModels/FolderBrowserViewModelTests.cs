using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
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
        var tree = BuildTestTree();

        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var vm = new FolderBrowserViewModel(syncMock.Object, _contextId, selectiveSync, "test-config.json");

        // Act
        await vm.LoadTreeAsync();

        // Assert — top-level folders are built, sub-children are lazy (placeholder).
        Assert.AreEqual(2, vm.RootItems.Count, "Should have 2 folders (Documents, Photos — no files).");
        Assert.AreEqual("Documents", vm.RootItems[0].Name);
        Assert.AreEqual("Photos", vm.RootItems[1].Name);
        // Documents has a child folder, so lazy placeholder present.
        Assert.AreEqual(1, vm.RootItems[0].Children.Count);
        Assert.AreEqual(Guid.Empty, vm.RootItems[0].Children[0].NodeId, "Should be a lazy-load placeholder.");
        Assert.IsFalse(vm.IsLoading);
        Assert.IsNull(vm.ErrorMessage);
    }

    [TestMethod]
    public async Task LazyLoad_ChildrenPopulatedOnExpand()
    {
        // Arrange
        var tree = BuildTestTree();
        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var vm = new FolderBrowserViewModel(syncMock.Object, _contextId, selectiveSync, "test-config.json");
        await vm.LoadTreeAsync();

        // Verify placeholder before expand.
        var docsItem = vm.RootItems[0];
        Assert.AreEqual(1, docsItem.Children.Count);
        Assert.AreEqual(Guid.Empty, docsItem.Children[0].NodeId);

        // Act: expand the node to trigger lazy load.
        var sourceNode = tree.Children[0]; // Documents node with Projects child.
        await vm.LoadChildrenAsync(docsItem, sourceNode);

        // Assert: placeholder replaced by real child.
        Assert.AreEqual(1, docsItem.Children.Count);
        Assert.AreEqual("Projects", docsItem.Children[0].Name);
        Assert.AreEqual("Documents/Projects", docsItem.Children[0].RelativePath);
        Assert.AreNotEqual(Guid.Empty, docsItem.Children[0].NodeId);
    }

    [TestMethod]
    public async Task Save_ExcludesUncheckedFolders()
    {
        // Arrange: build a tree with two leaf folders (no sub-children = no placeholders).
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

        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var tempFile = Path.GetTempFileName();
        try
        {
            var vm = new FolderBrowserViewModel(syncMock.Object, _contextId, selectiveSync, tempFile);
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

    [TestMethod]
    public async Task Save_CleansUpNewlyExcludedLocalFolder()
    {
        // Arrange: a simple two-folder tree.
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
                    Name = "Keep",
                    NodeType = "Folder",
                },
                new SyncTreeNodeResponse
                {
                    NodeId = Guid.NewGuid(),
                    Name = "Remove",
                    NodeType = "Folder",
                },
            ],
        };

        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var tempConfigFile = Path.GetTempFileName();
        var tempSyncRoot = Path.Combine(Path.GetTempPath(), $"sync-test-{Guid.NewGuid():N}");
        var removeDir = Path.Combine(tempSyncRoot, "Remove");

        try
        {
            // Create a local folder that will be excluded (should be cleaned up).
            Directory.CreateDirectory(removeDir);
            File.WriteAllText(Path.Combine(removeDir, "file.txt"), "test");

            var vm = new FolderBrowserViewModel(syncMock.Object, _contextId, selectiveSync, tempConfigFile)
            {
                LocalSyncRoot = tempSyncRoot,
                ConfirmDeletionAsync = _ => Task.FromResult(true), // Auto-confirm.
            };

            await vm.LoadTreeAsync();

            // Act: uncheck "Remove" and save.
            vm.RootItems[1].IsChecked = false;
            await vm.SaveAsync();

            // Assert: local folder should be deleted.
            Assert.IsFalse(Directory.Exists(removeDir), "Newly excluded folder should be deleted locally.");
        }
        finally
        {
            File.Delete(tempConfigFile);
            if (Directory.Exists(tempSyncRoot))
                Directory.Delete(tempSyncRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task Save_SkipsCleanupWhenDeletionDeclined()
    {
        // Arrange
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
                    Name = "Protected",
                    NodeType = "Folder",
                },
            ],
        };

        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var tempConfigFile = Path.GetTempFileName();
        var tempSyncRoot = Path.Combine(Path.GetTempPath(), $"sync-test-{Guid.NewGuid():N}");
        var protectedDir = Path.Combine(tempSyncRoot, "Protected");

        try
        {
            Directory.CreateDirectory(protectedDir);

            var vm = new FolderBrowserViewModel(syncMock.Object, _contextId, selectiveSync, tempConfigFile)
            {
                LocalSyncRoot = tempSyncRoot,
                ConfirmDeletionAsync = _ => Task.FromResult(false), // Decline deletion.
            };

            await vm.LoadTreeAsync();

            // Act: uncheck and save.
            vm.RootItems[0].IsChecked = false;
            await vm.SaveAsync();

            // Assert: local folder should still exist (user declined deletion).
            Assert.IsTrue(Directory.Exists(protectedDir), "Folder should survive when user declines deletion.");
        }
        finally
        {
            File.Delete(tempConfigFile);
            if (Directory.Exists(tempSyncRoot))
                Directory.Delete(tempSyncRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadTreeAsync_DirectoryNodeType_IsTreatedAsFolder()
    {
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
                    Name = "Projects",
                    NodeType = "Directory",
                },
            ],
        };

        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(x => x.GetFolderTreeAsync(_contextId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(tree);

        var selectiveSync = new SelectiveSyncConfig();
        var vm = new FolderBrowserViewModel(syncMock.Object, _contextId, selectiveSync, "test-config.json");

        await vm.LoadTreeAsync();

        Assert.AreEqual(1, vm.RootItems.Count);
        Assert.AreEqual("Projects", vm.RootItems[0].Name);
        Assert.IsFalse(vm.NoFoldersFound);
    }

    /// <summary>Builds a standard two-level test tree with Documents/Projects, Photos, and a File.</summary>
    private static SyncTreeNodeResponse BuildTestTree()
    {
        return new SyncTreeNodeResponse
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
                    NodeType = "File",
                },
            ],
        };
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
