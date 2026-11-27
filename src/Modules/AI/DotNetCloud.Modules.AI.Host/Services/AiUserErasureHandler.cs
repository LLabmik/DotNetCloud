using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI.Host.Services;

/// <summary>
/// Erases a deleted account's conversations and their messages on behalf of the core's
/// account-erasure orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// The module declares <c>dataStore: "isolated"</c>, so every conversation lives in a schema only this
/// module can reach. Deleting the account must therefore delete them here (F12); the core records the
/// acknowledgement and retries for 72 hours, and an account deletion never reports success while a
/// module row is outstanding.
/// </para>
/// <para>
/// <c>IgnoreQueryFilters</c> is deliberate: <c>Conversation</c> carries a soft-delete filter, and a
/// conversation a user had already "deleted" in the UI is still personal data that must not survive an
/// account deletion.
/// </para>
/// <para>
/// Idempotent by construction: after the first successful pass there is nothing left to delete, so a
/// retried request reports zero rows and still acknowledges.
/// </para>
/// </remarks>
public sealed class AiUserErasureHandler : IModuleUserErasureHandler
{
    /// <summary>Scope key reporting the number of conversations deleted.</summary>
    public const string ConversationsScopeKey = "conversations";

    /// <summary>Scope key reporting the number of messages deleted.</summary>
    public const string MessagesScopeKey = "messages";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiUserErasureHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="AiUserErasureHandler"/> class.</summary>
    /// <param name="scopeFactory">Creates the scope the scoped <see cref="AiDbContext"/> comes from.</param>
    /// <param name="logger">The logger.</param>
    public AiUserErasureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<AiUserErasureHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleUserErasureResult> EraseAsync(
        ModuleUserErasureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var conversations = await db.Conversations
            .IgnoreQueryFilters()
            .Where(conversation => conversation.OwnerId == context.UserId)
            .ToListAsync(cancellationToken);

        var messages = new List<ConversationMessage>();

        if (conversations.Count > 0)
        {
            var conversationIds = conversations.Select(conversation => conversation.Id).ToList();

            messages = await db.ConversationMessages
                .Where(message => conversationIds.Contains(message.ConversationId))
                .ToListAsync(cancellationToken);

            db.ConversationMessages.RemoveRange(messages);
            db.Conversations.RemoveRange(conversations);
        }

        if (conversations.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // The account id is personal data: it is deliberately never logged
        // (cs/exposure-of-sensitive-information).
        _logger.LogInformation(
            "Erased {ConversationCount} conversation(s) and {MessageCount} message(s) for a deleted account.",
            conversations.Count,
            messages.Count);

        return ModuleUserErasureResult.Success(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ConversationsScopeKey] = conversations.Count,
                [MessagesScopeKey] = messages.Count,
            });
    }
}
