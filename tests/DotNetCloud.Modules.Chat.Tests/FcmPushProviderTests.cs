using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Chat.Tests;

[TestClass]
public class FcmPushProviderTests
{
    [TestMethod]
    public async Task SendAsync_WhenTransportMarksInvalidToken_ThenTokenIsCleanedUp()
    {
        var userId = Guid.NewGuid();
        var badToken = "bad-token";
        var goodToken = "good-token";
        var transport = new TestFcmTransport(token =>
            token == badToken ? FcmSendResult.InvalidToken : FcmSendResult.Success);

        var provider = new FcmPushProvider(
            transport,
            Options.Create(new FcmPushOptions { Enabled = true }),
            NullLogger<FcmPushProvider>.Instance);

        await provider.RegisterDeviceAsync(userId, new DeviceRegistration { Token = badToken, Provider = PushProvider.FCM });
        await provider.RegisterDeviceAsync(userId, new DeviceRegistration { Token = goodToken, Provider = PushProvider.FCM });

        var notification = new PushNotification { Title = "t", Body = "b" };
        await provider.SendAsync(userId, notification);
        await provider.SendAsync(userId, notification);

        Assert.AreEqual(3, transport.Calls.Count);
        Assert.AreEqual(1, transport.Calls.Count(c => c == badToken));
        Assert.AreEqual(2, transport.Calls.Count(c => c == goodToken));
    }

    [TestMethod]
    public async Task SendAsync_WhenProviderDisabled_ThenTransportIsNotCalled()
    {
        var userId = Guid.NewGuid();
        var transport = new TestFcmTransport(_ => FcmSendResult.Success);
        var provider = new FcmPushProvider(
            transport,
            Options.Create(new FcmPushOptions { Enabled = false }),
            NullLogger<FcmPushProvider>.Instance);

        await provider.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token", Provider = PushProvider.FCM });
        await provider.SendAsync(userId, new PushNotification { Title = "t", Body = "b" });

        Assert.AreEqual(0, transport.Calls.Count);
    }

    private sealed class TestFcmTransport : IFcmTransport
    {
        private readonly Func<string, FcmSendResult> _resultFactory;

        public TestFcmTransport(Func<string, FcmSendResult> resultFactory)
        {
            _resultFactory = resultFactory;
        }

        public List<string> Calls { get; } = [];

        public Task<FcmSendResult> SendAsync(DeviceRegistration device, PushNotification notification, CancellationToken cancellationToken = default)
        {
            Calls.Add(device.Token);
            return Task.FromResult(_resultFactory(device.Token));
        }

        public Task<IReadOnlyList<FcmSendResult>> SendBatchAsync(IReadOnlyList<(DeviceRegistration Device, PushNotification Notification)> batch, CancellationToken cancellationToken = default)
        {
            var results = new List<FcmSendResult>();
            foreach (var (device, _) in batch)
            {
                Calls.Add(device.Token);
                results.Add(_resultFactory(device.Token));
            }
            return Task.FromResult<IReadOnlyList<FcmSendResult>>(results);
        }
    }
}
