using DotNetCloud.Modules.Chat.Models;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="Channel"/> entity model defaults and properties.
/// </summary>
[TestClass]
public class ChannelTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var channel = new Channel { Name = "test" };
        Assert.AreNotEqual(Guid.Empty, channel.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultTypeIsPublic()
    {
        var channel = new Channel { Name = "test" };
        Assert.AreEqual(ChannelType.Public, channel.Type);
    }

    [TestMethod]
    public void WhenCreatedThenIsArchivedIsFalse()
    {
        var channel = new Channel { Name = "test" };
        Assert.IsFalse(channel.IsArchived);
    }

    [TestMethod]
    public void WhenCreatedThenIsDeletedIsFalse()
    {
        var channel = new Channel { Name = "test" };
        Assert.IsFalse(channel.IsDeleted);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var channel = new Channel { Name = "test" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(channel.CreatedAt >= before && channel.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenMembersCollectionIsEmpty()
    {
        var channel = new Channel { Name = "test" };
        Assert.AreEqual(0, channel.Members.Count);
    }

    [TestMethod]
    public void WhenCreatedThenMessagesCollectionIsEmpty()
    {
        var channel = new Channel { Name = "test" };
        Assert.AreEqual(0, channel.Messages.Count);
    }

    [TestMethod]
    public void WhenCreatedThenPinnedMessagesCollectionIsEmpty()
    {
        var channel = new Channel { Name = "test" };
        Assert.AreEqual(0, channel.PinnedMessages.Count);
    }

    [TestMethod]
    public void WhenCreatedThenLastActivityAtIsNull()
    {
        var channel = new Channel { Name = "test" };
        Assert.IsNull(channel.LastActivityAt);
    }

    [TestMethod]
    public void WhenCreatedThenDeletedAtIsNull()
    {
        var channel = new Channel { Name = "test" };
        Assert.IsNull(channel.DeletedAt);
    }
}

/// <summary>
/// Tests for <see cref="Message"/> entity model defaults and properties.
/// </summary>
[TestClass]
public class MessageTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var message = new Message { Content = "hello" };
        Assert.AreNotEqual(Guid.Empty, message.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultTypeIsText()
    {
        var message = new Message { Content = "hello" };
        Assert.AreEqual(MessageType.Text, message.Type);
    }

    [TestMethod]
    public void WhenCreatedThenIsEditedIsFalse()
    {
        var message = new Message { Content = "hello" };
        Assert.IsFalse(message.IsEdited);
    }

    [TestMethod]
    public void WhenCreatedThenIsDeletedIsFalse()
    {
        var message = new Message { Content = "hello" };
        Assert.IsFalse(message.IsDeleted);
    }

    [TestMethod]
    public void WhenCreatedThenSentAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var message = new Message { Content = "hello" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(message.SentAt >= before && message.SentAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenAttachmentsCollectionIsEmpty()
    {
        var message = new Message { Content = "hello" };
        Assert.AreEqual(0, message.Attachments.Count);
    }

    [TestMethod]
    public void WhenCreatedThenReactionsCollectionIsEmpty()
    {
        var message = new Message { Content = "hello" };
        Assert.AreEqual(0, message.Reactions.Count);
    }

    [TestMethod]
    public void WhenCreatedThenMentionsCollectionIsEmpty()
    {
        var message = new Message { Content = "hello" };
        Assert.AreEqual(0, message.Mentions.Count);
    }

    [TestMethod]
    public void WhenCreatedThenReplyToMessageIdIsNull()
    {
        var message = new Message { Content = "hello" };
        Assert.IsNull(message.ReplyToMessageId);
    }

    [TestMethod]
    public void WhenCreatedThenEditedAtIsNull()
    {
        var message = new Message { Content = "hello" };
        Assert.IsNull(message.EditedAt);
    }
}

/// <summary>
/// Tests for <see cref="ChannelMember"/> entity model defaults.
/// </summary>
[TestClass]
public class ChannelMemberTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var member = new ChannelMember();
        Assert.AreNotEqual(Guid.Empty, member.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultRoleIsMember()
    {
        var member = new ChannelMember();
        Assert.AreEqual(ChannelMemberRole.Member, member.Role);
    }

    [TestMethod]
    public void WhenCreatedThenIsMutedIsFalse()
    {
        var member = new ChannelMember();
        Assert.IsFalse(member.IsMuted);
    }

    [TestMethod]
    public void WhenCreatedThenIsPinnedIsFalse()
    {
        var member = new ChannelMember();
        Assert.IsFalse(member.IsPinned);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultNotificationPrefIsAll()
    {
        var member = new ChannelMember();
        Assert.AreEqual(NotificationPreference.All, member.NotificationPref);
    }

    [TestMethod]
    public void WhenCreatedThenLastReadAtIsNull()
    {
        var member = new ChannelMember();
        Assert.IsNull(member.LastReadAt);
    }

    [TestMethod]
    public void WhenCreatedThenLastReadMessageIdIsNull()
    {
        var member = new ChannelMember();
        Assert.IsNull(member.LastReadMessageId);
    }
}

/// <summary>
/// Tests for <see cref="MessageReaction"/> entity model defaults.
/// </summary>
[TestClass]
public class MessageReactionTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var reaction = new MessageReaction { Emoji = "👍" };
        Assert.AreNotEqual(Guid.Empty, reaction.Id);
    }

    [TestMethod]
    public void WhenCreatedThenReactedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var reaction = new MessageReaction { Emoji = "👍" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(reaction.ReactedAt >= before && reaction.ReactedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenEmojiIsSet()
    {
        var reaction = new MessageReaction { Emoji = "🎉" };
        Assert.AreEqual("🎉", reaction.Emoji);
    }
}

/// <summary>
/// Tests for <see cref="MessageMention"/> entity model defaults.
/// </summary>
[TestClass]
public class MessageMentionTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var mention = new MessageMention();
        Assert.AreNotEqual(Guid.Empty, mention.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultTypeIsUser()
    {
        var mention = new MessageMention();
        Assert.AreEqual(MentionType.User, mention.Type);
    }

    [TestMethod]
    public void WhenCreatedThenMentionedUserIdIsNull()
    {
        var mention = new MessageMention();
        Assert.IsNull(mention.MentionedUserId);
    }

    [TestMethod]
    public void WhenCreatedThenStartIndexIsZero()
    {
        var mention = new MessageMention();
        Assert.AreEqual(0, mention.StartIndex);
    }

    [TestMethod]
    public void WhenCreatedThenLengthIsZero()
    {
        var mention = new MessageMention();
        Assert.AreEqual(0, mention.Length);
    }
}
