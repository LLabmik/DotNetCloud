using System.Text;
using DotNetCloud.Client.Android.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Push.Tests;

/// <summary>
/// Covers the Android-free UnifiedPush protocol: broadcast intents, the ID-only payload contract
/// and the generic notification mapping (plan §4.2/§4.4, §2.5).
/// </summary>
[TestClass]
public sealed class UnifiedPushProtocolTests
{
    private const string Token = "11111111-2222-3333-4444-555555555555";
    private const string ChannelId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string EventId = "12345678-1234-1234-1234-123456789012";

    [TestMethod]
    public void CreateToken_ReturnsUniqueLowercaseGuid()
    {
        // Act
        var first = UnifiedPushProtocol.CreateToken();
        var second = UnifiedPushProtocol.CreateToken();

        // Assert
        Assert.AreNotEqual(first, second);
        Assert.AreEqual(36, first.Length);
        Assert.IsTrue(Guid.TryParse(first, out _));
        Assert.AreEqual(first.ToLowerInvariant(), first);
    }

    [TestMethod]
    public void BuildRegisterIntent_CarriesTokenMessageAndShareIdentity()
    {
        // Act
        var descriptor = UnifiedPushProtocol.BuildRegisterIntent(Token, "DotNetCloud (ben@example.com)");

        // Assert
        Assert.AreEqual(UnifiedPushProtocol.ActionRegister, descriptor.Action);
        Assert.AreEqual(Token, descriptor.Extras[UnifiedPushProtocol.ExtraToken]);
        Assert.AreEqual(
            "DotNetCloud (ben@example.com)",
            descriptor.Extras[UnifiedPushProtocol.ExtraRegistrationMessage]);

        // targetSdk 35 must identify itself with FLAG_SHARE_IDENTITY, never a PendingIntent.
        Assert.IsTrue(descriptor.ShareIdentity);
        Assert.IsFalse(descriptor.Extras.ContainsKey(UnifiedPushProtocol.ExtraPendingIntent));
    }

    [TestMethod]
    public void BuildRegisterIntent_BlankDescription_OmitsMessageExtra()
    {
        // Act
        var descriptor = UnifiedPushProtocol.BuildRegisterIntent(Token, "   ");

        // Assert
        Assert.IsFalse(descriptor.Extras.ContainsKey(UnifiedPushProtocol.ExtraRegistrationMessage));
    }

    [TestMethod]
    public void BuildRegisterIntent_OverlongDescription_TruncatedToSpecLimit()
    {
        // Arrange
        var description = new string('x', 250);

        // Act
        var descriptor = UnifiedPushProtocol.BuildRegisterIntent(Token, description);

        // Assert
        var sent = descriptor.Extras[UnifiedPushProtocol.ExtraRegistrationMessage];
        Assert.AreEqual(UnifiedPushProtocol.MaxRegistrationMessageBytes, Encoding.UTF8.GetByteCount(sent));
    }

    [TestMethod]
    public void BuildRegisterIntent_MissingToken_Throws()
    {
        // Act + Assert
        Assert.ThrowsExactly<ArgumentException>(() => UnifiedPushProtocol.BuildRegisterIntent(" "));
    }

    [TestMethod]
    public void BuildUnregisterIntent_CarriesTokenAndShareIdentity()
    {
        // Act
        var descriptor = UnifiedPushProtocol.BuildUnregisterIntent(Token);

        // Assert
        Assert.AreEqual(UnifiedPushProtocol.ActionUnregister, descriptor.Action);
        Assert.AreEqual(Token, descriptor.Extras[UnifiedPushProtocol.ExtraToken]);
        Assert.IsTrue(descriptor.ShareIdentity);
    }

    [TestMethod]
    public void BuildAckIntent_WithoutId_ReturnsNull()
    {
        // Act + Assert
        Assert.IsNull(UnifiedPushProtocol.BuildAckIntent(Token, null));
        Assert.IsNull(UnifiedPushProtocol.BuildAckIntent(Token, "  "));
    }

    [TestMethod]
    public void BuildAckIntent_WithId_CarriesTokenAndIdWithoutShareIdentity()
    {
        // Act
        var descriptor = UnifiedPushProtocol.BuildAckIntent(Token, "msg-1");

        // Assert
        Assert.IsNotNull(descriptor);
        Assert.AreEqual(UnifiedPushProtocol.ActionMessageAck, descriptor.Action);
        Assert.AreEqual(Token, descriptor.Extras[UnifiedPushProtocol.ExtraToken]);
        Assert.AreEqual("msg-1", descriptor.Extras[UnifiedPushProtocol.ExtraId]);
        Assert.IsFalse(descriptor.ShareIdentity);
    }

    [TestMethod]
    public void ParsePayload_ContractJson_ReadsIdentifierFields()
    {
        // Arrange
        var json = $$"""
            {"v":1,"type":"message","channelId":"{{ChannelId}}","messageId":"m-1","eventId":null}
            """;

        // Act
        var payload = UnifiedPushProtocol.ParsePayload(Encoding.UTF8.GetBytes(json));

        // Assert
        Assert.IsNotNull(payload);
        Assert.AreEqual(1, payload.V);
        Assert.AreEqual(UnifiedPushProtocol.PayloadTypeMessage, payload.Type);
        Assert.AreEqual(ChannelId, payload.ChannelId);
        Assert.AreEqual("m-1", payload.MessageId);
    }

    [TestMethod]
    public void ParsePayload_EmptyOversizedOrInvalid_ReturnsNull()
    {
        // Arrange
        var oversized = new byte[UnifiedPushProtocol.MaxMessageBytes + 1];

        // Act + Assert
        Assert.IsNull(UnifiedPushProtocol.ParsePayload(null));
        Assert.IsNull(UnifiedPushProtocol.ParsePayload([]));
        Assert.IsNull(UnifiedPushProtocol.ParsePayload(oversized));
        Assert.IsNull(UnifiedPushProtocol.ParsePayload("not json"u8.ToArray()));
    }

    [TestMethod]
    [DataRow(UnifiedPushProtocol.PayloadTypeMessage, "New message", UnifiedPushProtocol.ChannelMessages)]
    [DataRow(UnifiedPushProtocol.PayloadTypeMention, "You were mentioned", UnifiedPushProtocol.ChannelMentions)]
    [DataRow(UnifiedPushProtocol.PayloadTypeAnnouncement, "New announcement", UnifiedPushProtocol.ChannelAnnouncements)]
    [DataRow(UnifiedPushProtocol.PayloadTypeDmChannelCreated, "New direct message", UnifiedPushProtocol.ChannelDmNotifications)]
    public void MapToNotification_ChatTypes_RenderGenericTitleOnTheRightChannel(
        string type, string expectedTitle, string expectedChannel)
    {
        // Arrange
        var payload = new UnifiedPushPayload { V = 1, Type = type, ChannelId = ChannelId };

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(payload);

        // Assert
        Assert.AreEqual(expectedTitle, plan.Title);
        Assert.AreEqual(string.Empty, plan.Body);
        Assert.AreEqual(expectedChannel, plan.NotificationChannelId);
        Assert.AreEqual(UnifiedPushNotificationTarget.Channel, plan.Target);
        Assert.AreEqual(ChannelId, plan.TargetId);
        Assert.IsFalse(plan.IsSilent);
    }

    [TestMethod]
    public void MapToNotification_PayloadCarryingText_NeverSurfacesIt()
    {
        // Arrange: a hostile/regressed payload that still carries user-visible text.
        var json = $$"""
            {
              "v": 1,
              "type": "message",
              "channelId": "{{ChannelId}}",
              "title": "Alice",
              "body": "meet me at the pier",
              "senderName": "Alice",
              "channelName": "#general",
              "data": { "title": "Alice", "body": "secret" }
            }
            """;

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(
            UnifiedPushProtocol.ParsePayload(Encoding.UTF8.GetBytes(json)));

        // Assert
        Assert.AreEqual("New message", plan.Title);
        Assert.AreEqual(string.Empty, plan.Body);
        Assert.IsFalse(plan.Title.Contains("Alice", StringComparison.Ordinal));
        Assert.IsFalse(plan.Body.Contains("pier", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MapToNotification_CalendarReminder_OpensTheEventAndStaysGeneric()
    {
        // Arrange
        var payload = new UnifiedPushPayload
        {
            Type = UnifiedPushProtocol.PayloadTypeCalendarReminder,
            EventId = EventId,
            ChannelId = ChannelId,
        };

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(payload);

        // Assert
        Assert.AreEqual("Calendar reminder", plan.Title);
        Assert.AreEqual(UnifiedPushProtocol.ChannelCalendarReminders, plan.NotificationChannelId);
        Assert.AreEqual(UnifiedPushNotificationTarget.CalendarEvent, plan.Target);
        Assert.AreEqual(EventId, plan.TargetId);
    }

    [TestMethod]
    public void MapToNotification_CalendarEventRefresh_IsSilent()
    {
        // Arrange
        var payload = new UnifiedPushPayload
        {
            Type = UnifiedPushProtocol.PayloadTypeCalendarEvent,
            EventId = EventId,
        };

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(payload);

        // Assert
        Assert.IsTrue(plan.IsSilent);
        Assert.AreEqual(UnifiedPushNotificationKind.Silent, plan.Kind);
    }

    [TestMethod]
    public void MapToNotification_UnknownType_FallsBackToAGenericNotification()
    {
        // Arrange
        var payload = new UnifiedPushPayload { Type = "something_new", ChannelId = ChannelId };

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(payload);

        // Assert
        Assert.AreEqual("New notification", plan.Title);
        Assert.AreEqual(UnifiedPushNotificationKind.Generic, plan.Kind);
        Assert.AreEqual(UnifiedPushNotificationTarget.Channel, plan.Target);
    }

    [TestMethod]
    public void MapToNotification_MessageWithoutAChannel_HasNoDeepLink()
    {
        // Arrange
        var payload = new UnifiedPushPayload { Type = UnifiedPushProtocol.PayloadTypeMessage };

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(payload);

        // Assert
        Assert.AreEqual(UnifiedPushNotificationTarget.None, plan.Target);
        Assert.IsNull(plan.TargetId);
    }

    [TestMethod]
    public void MapToNotification_NonGuidChannel_IsDroppedRatherThanDeepLinked()
    {
        // Arrange
        var payload = new UnifiedPushPayload
        {
            Type = UnifiedPushProtocol.PayloadTypeMessage,
            ChannelId = "not-a-guid",
        };

        // Act
        var plan = UnifiedPushProtocol.MapToNotification(payload);

        // Assert
        Assert.AreEqual(UnifiedPushNotificationTarget.None, plan.Target);
    }

    [TestMethod]
    public void MapToNotification_NullPayload_RendersAGenericNotification()
    {
        // Act
        var plan = UnifiedPushProtocol.MapToNotification(null);

        // Assert
        Assert.IsNotNull(plan.Title);
        Assert.AreEqual(string.Empty, plan.Body);
    }

    [TestMethod]
    public void SafeEndpointHost_ReturnsHostOnly_NeverTheTopic()
    {
        // Act + Assert
        Assert.AreEqual(
            "cloud.example.com",
            UnifiedPushProtocol.SafeEndpointHost("https://cloud.example.com/push/upSECRETtopic"));

        // The topic is a capability secret, so anything unparsable must yield nothing at all.
        Assert.IsNull(UnifiedPushProtocol.SafeEndpointHost("upSECRETtopic"));
        Assert.IsNull(UnifiedPushProtocol.SafeEndpointHost(null));
    }

    [TestMethod]
    public void Truncate_RespectsByteLimitWithoutSplittingCharacters()
    {
        // Arrange: four 3-byte characters, limit of 7 bytes.
        var value = "日本語です";

        // Act
        var truncated = UnifiedPushProtocol.Truncate(value, 7);

        // Assert
        Assert.AreEqual("日本", truncated);
        Assert.IsTrue(Encoding.UTF8.GetByteCount(truncated!) <= 7);
        Assert.IsNull(UnifiedPushProtocol.Truncate("  ", 10));
    }
}
