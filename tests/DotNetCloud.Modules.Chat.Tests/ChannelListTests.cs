using Microsoft.AspNetCore.Components;

using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelList"/> pinned-channel drag reorder behavior.
/// </summary>
[TestClass]
public class ChannelListTests
{
    [TestMethod]
    public async Task WhenPinnedChannelDroppedOnAnotherThenOrderIsUpdatedAndCallbackIsRaised()
    {
        var testList = CreateChannelList();
        var callbackReceiver = new object();
        IReadOnlyList<Guid>? callbackOrder = null;

        testList.OnChannelReordered = EventCallback.Factory.Create<IReadOnlyList<Guid>>(
            callbackReceiver,
            order => callbackOrder = order.ToList());

        var firstId = testList.PinnedOrder[0];
        var secondId = testList.PinnedOrder[1];

        testList.StartDrag(secondId);
        testList.DragOver(firstId);
        await testList.DropAsync(firstId);

        CollectionAssert.AreEqual(new[] { secondId, firstId }, testList.PinnedOrder.ToArray());
        CollectionAssert.AreEqual(new[] { secondId, firstId }, callbackOrder?.ToArray());
    }

    [TestMethod]
    public async Task WhenPinnedChannelDroppedOnItselfThenOrderDoesNotChange()
    {
        var testList = CreateChannelList();
        var originalOrder = testList.PinnedOrder.ToArray();

        testList.StartDrag(originalOrder[0]);
        await testList.DropAsync(originalOrder[0]);

        CollectionAssert.AreEqual(originalOrder, testList.PinnedOrder.ToArray());
    }

    [TestMethod]
    public void WhenDraggingPinnedChannelThenDragAndDropTargetCssClassesAreApplied()
    {
        var testList = CreateChannelList();
        var firstId = testList.PinnedOrder[0];
        var secondId = testList.PinnedOrder[1];

        testList.StartDrag(firstId);
        testList.DragOver(secondId);

        Assert.AreEqual("is-dragging", testList.DragClass(firstId));
        Assert.AreEqual("is-drop-target", testList.DragClass(secondId));
    }

    [TestMethod]
    public async Task HandleNewDmClick_InvokesOnNewDmCallback()
    {
        var testList = CreateChannelList();
        var receiver = new object();
        var invoked = false;
        testList.OnNewDm = EventCallback.Factory.Create(receiver, () => invoked = true);

        await testList.InvokeNewDmClickAsync();

        Assert.IsTrue(invoked);
    }

    private static TestableChannelList CreateChannelList()
    {
        var firstPinnedId = Guid.NewGuid();
        var secondPinnedId = Guid.NewGuid();

        var list = new TestableChannelList();
        list.SetChannels(
        [
            new ChannelViewModel { Id = firstPinnedId, Name = "alpha", Type = "Public", IsPinned = true },
            new ChannelViewModel { Id = secondPinnedId, Name = "beta", Type = "Public", IsPinned = true },
            new ChannelViewModel { Id = Guid.NewGuid(), Name = "general", Type = "Public", IsPinned = false }
        ]);

        return list;
    }

    private sealed class TestableChannelList : ChannelList
    {
        public IReadOnlyList<Guid> PinnedOrder => PinnedChannels.Select(channel => channel.Id).ToList();

        public void SetChannels(IReadOnlyList<ChannelViewModel> channels)
        {
            Channels = channels.ToList();
            base.OnParametersSet();
        }

        public void StartDrag(Guid channelId)
        {
            HandlePinnedDragStart(channelId);
        }

        public void DragOver(Guid channelId)
        {
            HandlePinnedDragOver(channelId);
        }

        public Task DropAsync(Guid targetChannelId)
        {
            return HandlePinnedDropAsync(targetChannelId);
        }

        public string DragClass(Guid channelId)
        {
            return GetPinnedDragClass(channelId);
        }

        public Task InvokeNewDmClickAsync()
        {
            return HandleNewDmClick();
        }
    }
}
