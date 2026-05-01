using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Chat.Data.Configuration;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Database context for the Chat module.
/// Manages all chat entities: channels, members, messages, attachments, reactions, mentions, and pins.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module DbContext Pattern:</b>
/// Each module owns its own DbContext, separate from the core <c>CoreDbContext</c>.
/// This provides schema isolation, independent migrations, and testability.
/// </para>
/// <para>
/// <b>Multi-Database Support:</b>
/// Works with PostgreSQL, SQL Server, and MariaDB through provider-specific configuration.
/// </para>
/// </remarks>
public class ChatDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatDbContext"/> class.
    /// </summary>
    public ChatDbContext(DbContextOptions<ChatDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatDbContext"/> class with a specific naming strategy.
    /// </summary>
    public ChatDbContext(DbContextOptions<ChatDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>Chat channels (public, private, DM, group).</summary>
    public DbSet<Channel> Channels => Set<Channel>();

    /// <summary>Channel memberships.</summary>
    public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();

    /// <summary>Chat messages.</summary>
    public DbSet<Message> Messages => Set<Message>();

    /// <summary>File attachments on messages.</summary>
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    /// <summary>Emoji reactions on messages.</summary>
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    /// <summary>@mentions in messages.</summary>
    public DbSet<MessageMention> MessageMentions => Set<MessageMention>();

    /// <summary>Messages pinned in channels.</summary>
    public DbSet<PinnedMessage> PinnedMessages => Set<PinnedMessage>();

    /// <summary>Organization-wide announcements.</summary>
    public DbSet<Announcement> Announcements => Set<Announcement>();

    /// <summary>Acknowledgements for announcements.</summary>
    public DbSet<AnnouncementAcknowledgement> AnnouncementAcknowledgements => Set<AnnouncementAcknowledgement>();

    /// <summary>Channel invitations.</summary>
    public DbSet<ChannelInvite> ChannelInvites => Set<ChannelInvite>();

    /// <summary>Video/audio calls.</summary>
    public DbSet<VideoCall> VideoCalls => Set<VideoCall>();

    /// <summary>Participants in video/audio calls.</summary>
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();

    /// <summary>Per-user call blocking records.</summary>
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("chat"));
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ChannelConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelMemberConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new MessageAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new MessageReactionConfiguration());
        modelBuilder.ApplyConfiguration(new MessageMentionConfiguration());
        modelBuilder.ApplyConfiguration(new PinnedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AnnouncementConfiguration());
        modelBuilder.ApplyConfiguration(new AnnouncementAcknowledgementConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelInviteConfiguration());
        modelBuilder.ApplyConfiguration(new VideoCallConfiguration());
        modelBuilder.ApplyConfiguration(new CallParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new BlockedUserConfiguration());
    }
}
