using System.Text.Json;
using DotNetCloud.Client.Android.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Push.Tests;

/// <summary>
/// Covers the ID-only notification payload contract and the generic notification mapping: both
/// delivery paths (in-app SignalR and the background alert poll) render through it, so the text a
/// user sees is always chosen on the device and never sent by the server.
/// </summary>
[TestClass]
public sealed class NotificationPayloadContractTests
{
    private const string ChannelId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string EventId = "12345678-1234-1234-1234-123456789012";

    [TestMethod]
    [DataRow(NotificationPayloadContract.PayloadTypeMessage, "New message", NotificationPayloadContract.ChannelMessages)]
    [DataRow(NotificationPayloadContract.PayloadTypeMention, "You were mentioned", NotificationPayloadContract.ChannelMentions)]
    [DataRow(NotificationPayloadContract.PayloadTypeAnnouncement, "New announcement", NotificationPayloadContract.ChannelAnnouncements)]
    [DataRow(NotificationPayloadContract.PayloadTypeDmChannelCreated, "New direct message", NotificationPayloadContract.ChannelDmNotifications)]
    public void MapToNotification_ChatTypes_RenderGenericTitleOnTheRightChannel(
        string type, string expectedTitle, string expectedChannel)
    {
        // Arrange
        var payload = new NotificationPayload { V = 1, Type = type, ChannelId = ChannelId };

        // Act
        var plan = NotificationPayloadContract.MapToNotification(payload);

        // Assert
        Assert.AreEqual(expectedTitle, plan.Title);
        Assert.AreEqual(string.Empty, plan.Body);
        Assert.AreEqual(expectedChannel, plan.NotificationChannelId);
        Assert.AreEqual(NotificationTarget.Channel, plan.Target);
        Assert.AreEqual(ChannelId, plan.TargetId);
        Assert.IsFalse(plan.IsSilent);
    }

    [TestMethod]
    public void MapToNotification_PayloadCarryingText_NeverSurfacesIt()
    {
        // Arrange: a hostile/regressed payload that still carries user-visible text. The contract
        // record deliberately has no such properties, so deserialization discards them.
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
        var plan = NotificationPayloadContract.MapToNotification(
            JsonSerializer.Deserialize<NotificationPayload>(json));

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
        var payload = new NotificationPayload
        {
            Type = NotificationPayloadContract.PayloadTypeCalendarReminder,
            EventId = EventId,
            ChannelId = ChannelId,
        };

        // Act
        var plan = NotificationPayloadContract.MapToNotification(payload);

        // Assert
        Assert.AreEqual("Calendar reminder", plan.Title);
        Assert.AreEqual(NotificationPayloadContract.ChannelCalendarReminders, plan.NotificationChannelId);
        Assert.AreEqual(NotificationTarget.CalendarEvent, plan.Target);
        Assert.AreEqual(EventId, plan.TargetId);
    }

    [TestMethod]
    public void MapToNotification_CalendarEventRefresh_IsSilent()
    {
        // Arrange
        var payload = new NotificationPayload
        {
            Type = NotificationPayloadContract.PayloadTypeCalendarEvent,
            EventId = EventId,
        };

        // Act
        var plan = NotificationPayloadContract.MapToNotification(payload);

        // Assert
        Assert.IsTrue(plan.IsSilent);
        Assert.AreEqual(NotificationKind.Silent, plan.Kind);
    }

    [TestMethod]
    public void MapToNotification_UnknownType_FallsBackToAGenericNotification()
    {
        // Arrange
        var payload = new NotificationPayload { Type = "something_new", ChannelId = ChannelId };

        // Act
        var plan = NotificationPayloadContract.MapToNotification(payload);

        // Assert
        Assert.AreEqual("New notification", plan.Title);
        Assert.AreEqual(NotificationKind.Generic, plan.Kind);
        Assert.AreEqual(NotificationTarget.Channel, plan.Target);
    }

    [TestMethod]
    public void MapToNotification_MessageWithoutAChannel_HasNoDeepLink()
    {
        // Arrange
        var payload = new NotificationPayload { Type = NotificationPayloadContract.PayloadTypeMessage };

        // Act
        var plan = NotificationPayloadContract.MapToNotification(payload);

        // Assert
        Assert.AreEqual(NotificationTarget.None, plan.Target);
        Assert.IsNull(plan.TargetId);
    }

    [TestMethod]
    public void MapToNotification_NonGuidChannel_IsDroppedRatherThanDeepLinked()
    {
        // Arrange
        var payload = new NotificationPayload
        {
            Type = NotificationPayloadContract.PayloadTypeMessage,
            ChannelId = "not-a-guid",
        };

        // Act
        var plan = NotificationPayloadContract.MapToNotification(payload);

        // Assert
        Assert.AreEqual(NotificationTarget.None, plan.Target);
    }

    [TestMethod]
    public void MapToNotification_NullPayload_RendersAGenericNotification()
    {
        // Act
        var plan = NotificationPayloadContract.MapToNotification(null);

        // Assert
        Assert.IsNotNull(plan.Title);
        Assert.AreEqual(string.Empty, plan.Body);
    }
}
