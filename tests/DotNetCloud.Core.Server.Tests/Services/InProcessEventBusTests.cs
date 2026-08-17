using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class InProcessEventBusTests
{
    [TestMethod]
    public async Task PublishAsync_SingleHandlerSubscribedToMultipleTypes_FiresOncePerEvent()
    {
        // Regression test: a handler implementing several IEventHandler<T> interfaces
        // (e.g., NotificationProducer handling 8 event types) must only fire once per
        // published event. Previously each stored reference matched, producing N
        // duplicate invocations for N subscribed types.
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var handler = new MultiTypeHandler();

        await bus.SubscribeAsync<FileSharedEvent>(handler);
        await bus.SubscribeAsync<QuotaWarningEvent>(handler);

        await bus.PublishAsync(
            new FileSharedEvent
            {
                EventId = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                FileNodeId = Guid.CreateVersion7(),
                FileName = "report.pdf",
                ShareId = Guid.CreateVersion7(),
                ShareType = "User",
                SharedWithUserId = Guid.CreateVersion7(),
                SharedByUserId = Guid.CreateVersion7()
            },
            CallerContext.CreateSystemContext());

        Assert.AreEqual(1, handler.FileSharedHandled);
        Assert.AreEqual(0, handler.QuotaHandled);
    }

    [TestMethod]
    public async Task SubscribeAsync_SameInstanceMultipleTimes_Deduplicates()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var handler = new MultiTypeHandler();

        await bus.SubscribeAsync<FileSharedEvent>(handler);
        await bus.SubscribeAsync<FileSharedEvent>(handler); // duplicate subscription

        await bus.PublishAsync(
            new FileSharedEvent
            {
                EventId = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                FileNodeId = Guid.CreateVersion7(),
                FileName = "report.pdf",
                ShareId = Guid.CreateVersion7(),
                ShareType = "User",
                SharedWithUserId = Guid.CreateVersion7(),
                SharedByUserId = Guid.CreateVersion7()
            },
            CallerContext.CreateSystemContext());

        Assert.AreEqual(1, handler.FileSharedHandled);
    }

    [TestMethod]
    public async Task PublishAsync_DistinctHandlers_BothFire()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var handler1 = new MultiTypeHandler();
        var handler2 = new MultiTypeHandler();

        await bus.SubscribeAsync<FileSharedEvent>(handler1);
        await bus.SubscribeAsync<FileSharedEvent>(handler2);

        await bus.PublishAsync(
            new FileSharedEvent
            {
                EventId = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                FileNodeId = Guid.CreateVersion7(),
                FileName = "report.pdf",
                ShareId = Guid.CreateVersion7(),
                ShareType = "User",
                SharedWithUserId = Guid.CreateVersion7(),
                SharedByUserId = Guid.CreateVersion7()
            },
            CallerContext.CreateSystemContext());

        Assert.AreEqual(1, handler1.FileSharedHandled);
        Assert.AreEqual(1, handler2.FileSharedHandled);
    }

    private sealed class MultiTypeHandler :
        IEventHandler<FileSharedEvent>,
        IEventHandler<QuotaWarningEvent>
    {
        public int FileSharedHandled { get; private set; }
        public int QuotaHandled { get; private set; }

        /// <inheritdoc />
        public Task HandleAsync(FileSharedEvent @event, CancellationToken cancellationToken = default)
        {
            FileSharedHandled++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task HandleAsync(QuotaWarningEvent @event, CancellationToken cancellationToken = default)
        {
            QuotaHandled++;
            return Task.CompletedTask;
        }
    }
}
