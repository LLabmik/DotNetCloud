using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

[TestClass]
public class UnifiedPushProviderTests
{
    [TestMethod]
    public async Task SendAsync_WhenTransientFailuresThenSuccess_ThenRetriesUntilDelivered()
    {
        var userId = Guid.NewGuid();
        var endpoint = "https://up.example/device";
        var transport = new SequenceUnifiedPushTransport([
            new UnifiedPushSendResult { IsSuccess = false, IsTransientFailure = true, Error = "timeout_1" },
            new UnifiedPushSendResult { IsSuccess = false, IsTransientFailure = true, Error = "timeout_2" },
            UnifiedPushSendResult.Success
        ]);

        var provider = new UnifiedPushProvider(transport, NullLogger<UnifiedPushProvider>.Instance);
        await provider.RegisterDeviceAsync(userId, new DeviceRegistration
        {
            Token = "token-1",
            Provider = PushProvider.UnifiedPush,
            Endpoint = endpoint
        });

        await provider.SendAsync(userId, new PushNotification { Title = "t", Body = "b" });

        Assert.AreEqual(3, transport.CallCount);
    }

    [TestMethod]
    public async Task SendAsync_WhenNonTransientFailure_ThenDoesNotRetry()
    {
        var userId = Guid.NewGuid();
        var endpoint = "https://up.example/device";
        var transport = new SequenceUnifiedPushTransport([
            new UnifiedPushSendResult { IsSuccess = false, IsTransientFailure = false, Error = "bad_request" },
            UnifiedPushSendResult.Success
        ]);

        var provider = new UnifiedPushProvider(transport, NullLogger<UnifiedPushProvider>.Instance);
        await provider.RegisterDeviceAsync(userId, new DeviceRegistration
        {
            Token = "token-1",
            Provider = PushProvider.UnifiedPush,
            Endpoint = endpoint
        });

        await provider.SendAsync(userId, new PushNotification { Title = "t", Body = "b" });

        Assert.AreEqual(1, transport.CallCount);
    }

    private sealed class SequenceUnifiedPushTransport : IUnifiedPushTransport
    {
        private readonly Queue<UnifiedPushSendResult> _results;

        public SequenceUnifiedPushTransport(IEnumerable<UnifiedPushSendResult> results)
        {
            _results = new Queue<UnifiedPushSendResult>(results);
        }

        public int CallCount { get; private set; }

        public Task<UnifiedPushSendResult> SendAsync(string endpoint, PushNotification notification, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_results.Count == 0)
            {
                return Task.FromResult(UnifiedPushSendResult.Success);
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}
