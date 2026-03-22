using DotNetCloud.Client.SyncService.Ipc;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Notifications;
using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class TrayViewModelTests
{
    // ── Default state ─────────────────────────────────────────────────────

    [TestMethod]
    public void OverallState_DefaultsToOffline()
    {
        var (vm, _, _, _) = BuildVm();
        Assert.AreEqual(TrayState.Offline, vm.OverallState);
    }

    [TestMethod]
    public void Tooltip_DefaultIndicatesServiceNotRunning()
    {
        var (vm, _, _, _) = BuildVm();
        StringAssert.Contains(vm.Tooltip, "service not running");
    }

    // ── ConnectionStateChanged ────────────────────────────────────────────

    [TestMethod]
    public void OnDisconnect_SetsOfflineState()
    {
        var (vm, ipcMock, _, _) = BuildVm();

        // Simulate a disconnect.
        ipcMock.Raise(i => i.ConnectionStateChanged += null, ipcMock.Object, false);

        Assert.AreEqual(TrayState.Offline, vm.OverallState);
    }

    // ── SyncProgress event ────────────────────────────────────────────────

    [TestMethod]
    public async Task OnSyncProgress_ForKnownContext_UpdatesAccountState()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Idle");

        // Raise a sync-progress event.
        ipcMock.Raise(
            i => i.SyncProgressReceived += null,
            ipcMock.Object,
            new SyncProgressEventData { ContextId = contextId, State = "Syncing", PendingUploads = 2, PendingDownloads = 1 });

        var account = vm.Accounts.FirstOrDefault(a => a.ContextId == contextId);
        Assert.IsNotNull(account);
        Assert.AreEqual("Syncing", account!.State);
        Assert.AreEqual(2, account.PendingUploads);
    }

    // ── SyncComplete event ────────────────────────────────────────────────

    [TestMethod]
    public async Task OnSyncComplete_ResetsAccountToIdle()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, LastSyncedAt = DateTime.UtcNow, Conflicts = 0 });

        var account = vm.Accounts.FirstOrDefault(a => a.ContextId == contextId);
        Assert.AreEqual("Idle", account?.State);
    }

    [TestMethod]
    public async Task OnSyncComplete_WithConflicts_ShowsNotification()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 3, LastSyncedAt = DateTime.UtcNow });

        notifMock.Verify(
            n => n.ShowNotification(
                It.IsAny<string>(),
                It.Is<string>(b => b.Contains("conflict")),
                NotificationType.Warning,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    // ── SyncError event ───────────────────────────────────────────────────

    [TestMethod]
    public async Task OnSyncError_SetsErrorState_AndBuffersError_NoImmediateToast()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(
            i => i.SyncErrorReceived += null,
            ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Network timeout" });

        var account = vm.Accounts.FirstOrDefault(a => a.ContextId == contextId);
        Assert.AreEqual("Error", account?.State);
        Assert.AreEqual("Network timeout", account?.LastError);

        // No immediate toast — error is held until the sync cycle completes.
        notifMock.Verify(
            n => n.ShowNotification(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    // ── Toast coalescing: sync errors and successes ───────────────────────

    [TestMethod]
    public async Task OnSyncComplete_WithPriorError_ShowsSingleAggregatedErrorToast()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(
            i => i.SyncErrorReceived += null,
            ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Network timeout" });

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 0, LastSyncedAt = DateTime.UtcNow });

        notifMock.Verify(
            n => n.ShowNotification(
                "Sync failed",
                It.Is<string>(b => b.Contains("Network timeout")),
                NotificationType.Error,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string?>(r => r != null && r.StartsWith("sync-cycle-"))),
            Times.Once);

        // No success toast when there are errors.
        notifMock.Verify(
            n => n.ShowNotification(
                "Sync complete",
                It.IsAny<string>(),
                NotificationType.Info,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OnSyncComplete_WithMultipleErrors_ShowsSingleToastWithSummary()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(i => i.SyncErrorReceived += null, ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Error A" });
        ipcMock.Raise(i => i.SyncErrorReceived += null, ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Error B" });
        ipcMock.Raise(i => i.SyncErrorReceived += null, ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Error C" });

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 0, LastSyncedAt = DateTime.UtcNow });

        // Exactly one error toast containing the count.
        notifMock.Verify(
            n => n.ShowNotification(
                "Sync failed",
                It.Is<string>(b => b.Contains("3 error(s)")),
                NotificationType.Error,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task OnSyncComplete_WithTransfersNoErrors_ShowsSuccessToast()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        // Two uploads and one download.
        ipcMock.Raise(i => i.TransferCompleteReceived += null, ipcMock.Object,
            new TransferCompleteEventData { ContextId = contextId, FileName = "a.txt", Direction = "upload", TotalBytes = 100 });
        ipcMock.Raise(i => i.TransferCompleteReceived += null, ipcMock.Object,
            new TransferCompleteEventData { ContextId = contextId, FileName = "b.txt", Direction = "upload", TotalBytes = 200 });
        ipcMock.Raise(i => i.TransferCompleteReceived += null, ipcMock.Object,
            new TransferCompleteEventData { ContextId = contextId, FileName = "c.txt", Direction = "download", TotalBytes = 300 });

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 0, LastSyncedAt = DateTime.UtcNow });

        notifMock.Verify(
            n => n.ShowNotification(
                "Sync complete",
                It.Is<string>(b => b.Contains("2 uploaded") && b.Contains("1 downloaded")),
                NotificationType.Info,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string?>(r => r != null && r.StartsWith("sync-cycle-"))),
            Times.Once);
    }

    [TestMethod]
    public async Task OnSyncComplete_WithNoActivityNoErrors_ShowsNoToast()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 0, LastSyncedAt = DateTime.UtcNow });

        // Nothing synced, nothing failed — no toast.
        notifMock.Verify(
            n => n.ShowNotification(
                It.Is<string>(t => t == "Sync complete" || t == "Sync failed"),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OnSyncComplete_MixedSuccessAndErrors_ShowsOnlyErrorToast()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Syncing");

        ipcMock.Raise(i => i.TransferCompleteReceived += null, ipcMock.Object,
            new TransferCompleteEventData { ContextId = contextId, FileName = "ok.txt", Direction = "upload", TotalBytes = 100 });
        ipcMock.Raise(i => i.SyncErrorReceived += null, ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Upload failed: permission denied" });

        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 0, LastSyncedAt = DateTime.UtcNow });

        notifMock.Verify(
            n => n.ShowNotification(
                "Sync failed",
                It.Is<string>(b => b.Contains("permission denied")),
                NotificationType.Error,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Once);

        notifMock.Verify(
            n => n.ShowNotification(
                "Sync complete",
                It.IsAny<string>(),
                NotificationType.Info,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task NewSyncCycle_ResetsPriorCycleErrors()
    {
        var (vm, ipcMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Idle");

        // First cycle: triggers an error.
        ipcMock.Raise(i => i.SyncProgressReceived += null, ipcMock.Object,
            new SyncProgressEventData { ContextId = contextId, State = "Syncing" });
        ipcMock.Raise(i => i.SyncErrorReceived += null, ipcMock.Object,
            new SyncErrorEventData { ContextId = contextId, Error = "Old error" });

        // Second cycle starts — should clear the pending error.
        ipcMock.Raise(i => i.SyncProgressReceived += null, ipcMock.Object,
            new SyncProgressEventData { ContextId = contextId, State = "Idle" });
        ipcMock.Raise(i => i.SyncProgressReceived += null, ipcMock.Object,
            new SyncProgressEventData { ContextId = contextId, State = "Syncing" });

        // Cycle completes with no new errors and no transfers.
        ipcMock.Raise(
            i => i.SyncCompleteReceived += null,
            ipcMock.Object,
            new SyncCompleteEventData { ContextId = contextId, Conflicts = 0, LastSyncedAt = DateTime.UtcNow });

        // Old error was cleared — no error toast.
        notifMock.Verify(
            n => n.ShowNotification(
                "Sync failed",
                It.IsAny<string>(),
                NotificationType.Error,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    // ── Aggregate state ───────────────────────────────────────────────────

    [TestMethod]
    public async Task OverallState_IsIdle_WhenAllAccountsIdle()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, id1, "Idle");
        await SeedAccountAsync(vm, ipcMock, id2, "Idle");

        Assert.AreEqual(TrayState.Idle, vm.OverallState);
    }

    [TestMethod]
    public async Task OverallState_IsSyncing_WhenAnyAccountSyncing()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, id1, "Idle");
        await SeedAccountAsync(vm, ipcMock, id2, "Syncing");

        // Trigger a SyncProgress event so state recomputes.
        ipcMock.Raise(
            i => i.SyncProgressReceived += null,
            ipcMock.Object,
            new SyncProgressEventData { ContextId = id2, State = "Syncing", PendingUploads = 1 });

        Assert.AreEqual(TrayState.Syncing, vm.OverallState);
    }

    [TestMethod]
    public async Task OverallState_IsError_WhenAnyAccountInError()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var id = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, id, "Idle");

        // Trigger a SyncError event which sets state to Error.
        ipcMock.Raise(
            i => i.SyncErrorReceived += null,
            ipcMock.Object,
            new SyncErrorEventData { ContextId = id, Error = "Boom" });

        Assert.AreEqual(TrayState.Error, vm.OverallState);
    }

    [TestMethod]
    public async Task OverallState_IsPaused_WhenAllAccountsPaused()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var id = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, id, "Idle");

        // Simulate a progress event with Paused state.
        ipcMock.Raise(
            i => i.SyncProgressReceived += null,
            ipcMock.Object,
            new SyncProgressEventData { ContextId = id, State = "Paused" });

        Assert.AreEqual(TrayState.Paused, vm.OverallState);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task OnUnreadCountUpdated_AggregatesAcrossChannels_AndTracksMentions()
    {
        var (vm, ipcMock, chatMock, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Idle");

        chatMock.Raise(
            c => c.OnUnreadCountUpdated += null,
            chatMock.Object,
            new ChatUnreadCountUpdatedEventArgs("channel-a", 3, false));
        chatMock.Raise(
            c => c.OnUnreadCountUpdated += null,
            chatMock.Object,
            new ChatUnreadCountUpdatedEventArgs("channel-b", 2, true));

        Assert.AreEqual(5, vm.ChatUnreadCount);
        Assert.IsTrue(vm.ChatHasMentions);
    }

    [TestMethod]
    public async Task Tooltip_IncludesChatUnreadSummary_WhenUnreadExists()
    {
        var (vm, ipcMock, chatMock, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Idle");

        chatMock.Raise(
            c => c.OnUnreadCountUpdated += null,
            chatMock.Object,
            new ChatUnreadCountUpdatedEventArgs("channel-a", 4, true));

        StringAssert.Contains(vm.Tooltip, "chat: 4 unread");
        StringAssert.Contains(vm.Tooltip, "mentions");
    }

    [TestMethod]
    public void OnNewChatMessage_ShowsChatNotification_WithChannelAndPreview()
    {
        var (_, _, chatMock, notifMock) = BuildVm();

        chatMock.Raise(
            c => c.OnNewChatMessage += null,
            chatMock.Object,
            new ChatMessageReceivedEventArgs(
                "channel-a",
                "General",
                "Alice",
                "Hello from chat",
                Guid.NewGuid(),
                DateTime.UtcNow,
                false));

        notifMock.Verify(
            n => n.ShowNotification(
                It.Is<string>(t => t.Contains("General") && t.Contains("Alice")),
                It.Is<string>(b => b.Contains("Hello from chat")),
                NotificationType.Chat,
                It.IsAny<string?>(),
                It.Is<string?>(g => g == "chat-channel-channel-a"),
                It.Is<string?>(r => r == "chat-channel-channel-a")),
            Times.Once);
    }

    [TestMethod]
    public void OnNewChatMessage_WithMention_UsesMentionNotificationType()
    {
        var (_, _, chatMock, notifMock) = BuildVm();

        chatMock.Raise(
            c => c.OnNewChatMessage += null,
            chatMock.Object,
            new ChatMessageReceivedEventArgs(
                "channel-b",
                "Engineering",
                "Bob",
                "@you please check this",
                Guid.NewGuid(),
                DateTime.UtcNow,
                true));

        notifMock.Verify(
            n => n.ShowNotification(
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Mention,
                It.IsAny<string?>(),
                It.Is<string?>(g => g == "chat-channel-channel-b"),
                It.Is<string?>(r => r == "chat-channel-channel-b")),
            Times.Once);
    }

    [TestMethod]
    public void OnNewChatMessage_WhenMuted_DoesNotShowNotification()
    {
        var (vm, _, chatMock, notifMock) = BuildVm();
        vm.IsMuteChatNotifications = true;

        chatMock.Raise(
            c => c.OnNewChatMessage += null,
            chatMock.Object,
            new ChatMessageReceivedEventArgs(
                "channel-c",
                "General",
                "Carol",
                "Muted message",
                Guid.NewGuid(),
                DateTime.UtcNow,
                false));

        notifMock.Verify(
            n => n.ShowNotification(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OnNewChatMessage_WithKnownAccount_PassesChatAppActionUrl()
    {
        var (vm, ipcMock, chatMock, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, ipcMock, contextId, "Idle");

        chatMock.Raise(
            c => c.OnNewChatMessage += null,
            chatMock.Object,
            new ChatMessageReceivedEventArgs(
                "channel-d",
                "General",
                "Dana",
                "Click me",
                Guid.NewGuid(),
                DateTime.UtcNow,
                false));

        notifMock.Verify(
            n => n.ShowNotification(
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Chat,
                It.Is<string?>(u => u != null && u.Contains("/apps/chat", StringComparison.OrdinalIgnoreCase)
                                              && u.Contains("channelId=channel-d", StringComparison.OrdinalIgnoreCase)),
                It.Is<string?>(g => g == "chat-channel-channel-d"),
                It.Is<string?>(r => r == "chat-channel-channel-d")),
            Times.Once);
    }

    private static (TrayViewModel vm, Mock<IIpcClient> ipcMock, Mock<IChatSignalRClient> chatMock, Mock<INotificationService> notifMock) BuildVm()
    {
        var ipcMock = new Mock<IIpcClient>();
        ipcMock.SetupGet(i => i.IsConnected).Returns(false);
        ipcMock.SetupAdd(i => i.SyncProgressReceived += It.IsAny<EventHandler<SyncProgressEventData>>());
        ipcMock.SetupAdd(i => i.SyncCompleteReceived += It.IsAny<EventHandler<SyncCompleteEventData>>());
        ipcMock.SetupAdd(i => i.SyncErrorReceived += It.IsAny<EventHandler<SyncErrorEventData>>());
        ipcMock.SetupAdd(i => i.ConflictDetected += It.IsAny<EventHandler<SyncConflictEventData>>());
        ipcMock.SetupAdd(i => i.ConflictAutoResolved += It.IsAny<EventHandler<ConflictAutoResolvedEventData>>());
        ipcMock.SetupAdd(i => i.TransferProgressReceived += It.IsAny<EventHandler<TransferProgressEventData>>());
        ipcMock.SetupAdd(i => i.TransferCompleteReceived += It.IsAny<EventHandler<TransferCompleteEventData>>());
        ipcMock.SetupAdd(i => i.ConnectionStateChanged += It.IsAny<EventHandler<bool>>());

        var chatMock = new Mock<IChatSignalRClient>();
        chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var notifMock = new Mock<INotificationService>();
        var vm = new TrayViewModel(ipcMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);

        return (vm, ipcMock, chatMock, notifMock);
    }

    /// <summary>
    /// Injects a fake account into the view-model by:
    /// 1. Wiring the mock to return a context from <c>ListContextsAsync</c>.
    /// 2. Raising <c>ConnectionStateChanged(true)</c> which triggers <c>RefreshAccountsAsync</c>
    ///    via <c>Task.Run</c>.
    /// 3. Awaiting until the account appears in <c>vm.Accounts</c>.
    /// </summary>
    private static async Task SeedAccountAsync(
        TrayViewModel vm, Mock<IIpcClient> ipcMock, Guid contextId, string state)
    {
        ipcMock.SetupGet(i => i.IsConnected).Returns(true);

        var contexts = new List<ContextInfo>
        {
            new ContextInfo
            {
                Id = contextId,
                DisplayName = $"TestAccount-{contextId}",
                ServerBaseUrl = "https://cloud.example.com",
                LocalFolderPath = "/sync",
                State = state,
            },
        };

        ipcMock
            .Setup(i => i.ListContextsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(contexts);

        // Trigger refresh (fires Task.Run internally).
        ipcMock.Raise(i => i.ConnectionStateChanged += null, ipcMock.Object, true);

        // Wait until the account appears (Task.Run completes).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!vm.Accounts.Any(a => a.ContextId == contextId) && !cts.Token.IsCancellationRequested)
            await Task.Delay(10, cts.Token).ConfigureAwait(false);
    }
}
