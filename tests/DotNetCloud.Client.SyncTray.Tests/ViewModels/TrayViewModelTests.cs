using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Sync;
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

    // ── SyncProgress event ────────────────────────────────────────────────

    [TestMethod]
    public async Task OnSyncProgress_ForKnownContext_UpdatesAccountState()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Idle");

        // Raise a sync-progress event.
        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs { ContextId = contextId, Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 2, PendingDownloads = 1 } });

        var account = vm.Accounts.FirstOrDefault(a => a.ContextId == contextId);
        Assert.IsNotNull(account);
        Assert.AreEqual("Syncing", account!.State);
        Assert.AreEqual(2, account.PendingUploads);
    }

    // ── SyncComplete event ────────────────────────────────────────────────

    [TestMethod]
    public async Task OnSyncComplete_ResetsAccountToIdle()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { LastSyncedAt = DateTime.UtcNow, Conflicts = 0 } });

        var account = vm.Accounts.FirstOrDefault(a => a.ContextId == contextId);
        Assert.AreEqual("Idle", account?.State);
    }

    [TestMethod]
    public async Task OnSyncComplete_WithConflicts_ShowsNotification()
    {
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 3, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.SyncError += null,
            syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Network timeout" });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.SyncError += null,
            syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Network timeout" });

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 0, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Error A" });
        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Error B" });
        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Error C" });

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 0, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        // Two uploads and one download.
        syncMock.Raise(i => i.TransferComplete += null, syncMock.Object,
            new ContextTransferCompleteEventArgs { ContextId = contextId, FileName = "a.txt", Direction = "upload", TotalBytes = 100 });
        syncMock.Raise(i => i.TransferComplete += null, syncMock.Object,
            new ContextTransferCompleteEventArgs { ContextId = contextId, FileName = "b.txt", Direction = "upload", TotalBytes = 200 });
        syncMock.Raise(i => i.TransferComplete += null, syncMock.Object,
            new ContextTransferCompleteEventArgs { ContextId = contextId, FileName = "c.txt", Direction = "download", TotalBytes = 300 });

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 0, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 0, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Syncing");

        syncMock.Raise(i => i.TransferComplete += null, syncMock.Object,
            new ContextTransferCompleteEventArgs { ContextId = contextId, FileName = "ok.txt", Direction = "upload", TotalBytes = 100 });
        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Upload failed: permission denied" });

        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 0, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Idle");

        // First cycle: triggers an error.
        syncMock.Raise(i => i.SyncProgress += null, syncMock.Object,
            new SyncProgressEventArgs { ContextId = contextId, Status = new SyncStatus { State = SyncState.Syncing } });
        syncMock.Raise(i => i.SyncError += null, syncMock.Object,
            new SyncErrorEventArgs { ContextId = contextId, ErrorMessage = "Old error" });

        // Second cycle starts — should clear the pending error.
        syncMock.Raise(i => i.SyncProgress += null, syncMock.Object,
            new SyncProgressEventArgs { ContextId = contextId, Status = new SyncStatus { State = SyncState.Idle } });
        syncMock.Raise(i => i.SyncProgress += null, syncMock.Object,
            new SyncProgressEventArgs { ContextId = contextId, Status = new SyncStatus { State = SyncState.Syncing } });

        // Cycle completes with no new errors and no transfers.
        syncMock.Raise(
            i => i.SyncComplete += null,
            syncMock.Object,
            new SyncCompleteEventArgs { ContextId = contextId, Status = new SyncStatus { Conflicts = 0, LastSyncedAt = DateTime.UtcNow } });

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
        var (vm, syncMock, _, _) = BuildVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, id1, "Idle");
        await SeedAccountAsync(vm, syncMock, id2, "Idle");

        Assert.AreEqual(TrayState.Idle, vm.OverallState);
    }

    [TestMethod]
    public async Task OverallState_IsSyncing_WhenAnyAccountSyncing()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, id1, "Idle");
        await SeedAccountAsync(vm, syncMock, id2, "Syncing");

        // Trigger a SyncProgress event so state recomputes.
        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs { ContextId = id2, Status = new SyncStatus { State = SyncState.Syncing, PendingUploads = 1 } });

        Assert.AreEqual(TrayState.Syncing, vm.OverallState);
    }

    [TestMethod]
    public async Task OverallState_IsError_WhenAnyAccountInError()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var id = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, id, "Idle");

        // Trigger a SyncError event which sets state to Error.
        syncMock.Raise(
            i => i.SyncError += null,
            syncMock.Object,
            new SyncErrorEventArgs { ContextId = id, ErrorMessage = "Boom" });

        Assert.AreEqual(TrayState.Error, vm.OverallState);
    }

    [TestMethod]
    public async Task OverallState_IsPaused_WhenAllAccountsPaused()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var id = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, id, "Idle");

        // Simulate a progress event with Paused state.
        syncMock.Raise(
            i => i.SyncProgress += null,
            syncMock.Object,
            new SyncProgressEventArgs { ContextId = id, Status = new SyncStatus { State = SyncState.Paused } });

        Assert.AreEqual(TrayState.Paused, vm.OverallState);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task OnUnreadCountUpdated_AggregatesAcrossChannels_AndTracksMentions()
    {
        var (vm, syncMock, chatMock, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Idle");

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
        var (vm, syncMock, chatMock, _) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Idle");

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
        var (vm, syncMock, chatMock, notifMock) = BuildVm();
        var contextId = Guid.NewGuid();

        await SeedAccountAsync(vm, syncMock, contextId, "Idle");

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

    private static (TrayViewModel vm, Mock<ISyncContextManager> syncMock, Mock<IChatSignalRClient> chatMock, Mock<INotificationService> notifMock) BuildVm()
    {
        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync(new List<SyncContextRegistration>());

        var chatMock = new Mock<IChatSignalRClient>();
        chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var notifMock = new Mock<INotificationService>();
        var vm = new TrayViewModel(syncMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);

        return (vm, syncMock, chatMock, notifMock);
    }

    /// <summary>
    /// Injects a fake account into the view-model by:
    /// 1. Wiring the mock to return a context from <c>GetContextsAsync</c>.
    /// 2. Wiring <c>GetStatusAsync</c> to return the desired state.
    /// 3. Calling <c>RefreshAccountsAsync</c> directly.
    /// 4. Awaiting until the account appears in <c>vm.Accounts</c>.
    /// </summary>
    private static async Task SeedAccountAsync(
        TrayViewModel vm, Mock<ISyncContextManager> syncMock, Guid contextId, string state)
    {
        var existing = syncMock.Object.GetContextsAsync().GetAwaiter().GetResult();

        var contexts = new List<SyncContextRegistration>(existing)
        {
            new SyncContextRegistration
            {
                Id = contextId,
                DisplayName = $"TestAccount-{contextId}",
                ServerBaseUrl = "https://cloud.example.com",
                LocalFolderPath = "/sync",
                UserId = Guid.NewGuid(),
                AccountKey = $"test-{contextId}",
                OsUserName = "testuser",
                DataDirectory = "/tmp/data",
            },
        };

        syncMock
            .Setup(s => s.GetContextsAsync())
            .ReturnsAsync(contexts);

        // Parse the state string into a SyncState enum value.
        var syncState = Enum.TryParse<SyncState>(state, ignoreCase: true, out var parsed)
            ? parsed
            : SyncState.Idle;

        syncMock
            .Setup(s => s.GetStatusAsync(contextId))
            .ReturnsAsync(new SyncStatus { State = syncState });

        await vm.RefreshAccountsAsync();
    }
}
