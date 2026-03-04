using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Initializes the chat database with default channels and configuration.
/// </summary>
public static class ChatDbInitializer
{
    /// <summary>
    /// Seeds default system channels (e.g., #general, #announcements) if they don't already exist.
    /// </summary>
    /// <param name="db">The <see cref="ChatDbContext"/> instance.</param>
    /// <param name="organizationId">The organization to seed channels for.</param>
    /// <param name="systemUserId">The system user ID to use as the creator.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SeedDefaultChannelsAsync(
        ChatDbContext db,
        Guid organizationId,
        Guid systemUserId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var hasChannels = await db.Channels
            .AnyAsync(c => c.OrganizationId == organizationId, cancellationToken);

        if (hasChannels)
        {
            logger?.LogInformation("Chat channels already exist for organization {OrgId}; skipping seed", organizationId);
            return;
        }

        var generalChannel = new Channel
        {
            Name = "general",
            Description = "General discussion for everyone",
            Type = ChannelType.Public,
            OrganizationId = organizationId,
            CreatedByUserId = systemUserId,
            Topic = "Welcome to DotNetCloud!"
        };

        var announcementsChannel = new Channel
        {
            Name = "announcements",
            Description = "Organization-wide announcements",
            Type = ChannelType.Public,
            OrganizationId = organizationId,
            CreatedByUserId = systemUserId,
            Topic = "Important announcements"
        };

        var randomChannel = new Channel
        {
            Name = "random",
            Description = "Off-topic and fun conversations",
            Type = ChannelType.Public,
            OrganizationId = organizationId,
            CreatedByUserId = systemUserId,
            Topic = "Anything goes!"
        };

        db.Channels.AddRange(generalChannel, announcementsChannel, randomChannel);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Seeded {Count} default chat channels for organization {OrgId}: {Names}",
            3,
            organizationId,
            string.Join(", ", "#general", "#announcements", "#random"));
    }
}
