using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class ActiveTransferViewModelTests
{
    [TestMethod]
    public void BytesLabel_WhenTotalBytesUnknown_ShowsUnknown()
    {
        var vm = new ActiveTransferViewModel(Guid.CreateVersion7(), "big.bin", "download");
        vm.Update(bytesTransferred: 0, totalBytes: 0, chunksCompleted: 0, chunksTotal: 0, percentComplete: 0);

        StringAssert.Contains(vm.BytesLabel, "unknown");
    }

    [TestMethod]
    public void BytesLabel_WhenTotalBytesKnown_ShowsRealSize()
    {
        var vm = new ActiveTransferViewModel(Guid.CreateVersion7(), "big.bin", "download");
        vm.Update(bytesTransferred: 1024, totalBytes: 2048, chunksCompleted: 1, chunksTotal: 2, percentComplete: 50);

        Assert.AreEqual("1 KB / 2 KB", vm.BytesLabel);
    }
}
