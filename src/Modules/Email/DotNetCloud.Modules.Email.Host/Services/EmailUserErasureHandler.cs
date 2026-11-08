using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Host.Services;

/// <summary>
/// Erases a deleted account's email accounts, mailboxes, threads, messages, attachments and rules on
/// behalf of the core's account-erasure orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// The module declares <c>dataStore: "isolated"</c>, so every mail row lives in a schema only this
/// module can reach. Deleting the account must therefore delete them here (F12); the core records the
/// acknowledgement and retries for 72 hours, and an account deletion never reports success while a
/// module row is outstanding.
/// </para>
/// <para>
/// Rows are deleted in dependency order and counted explicitly instead of relying on database
/// cascades, because attachments also own a blob on disk that must go with the row. Attachment
/// content is deduplicated by content hash, so a blob shared by two accounts is only removed once the
/// last referencing row is gone — the handler deletes the blob for each attachment it removes, and
/// <see cref="IAttachmentStorage.DeleteAsync"/> is idempotent.
/// </para>
/// <para>
/// <c>IgnoreQueryFilters</c> is deliberate: <c>EmailAccount</c> and <c>EmailMessage</c> carry a
/// soft-delete filter, and a row a user had already "deleted" in the UI is still personal data that
/// must not survive an account deletion.
/// </para>
/// </remarks>
public sealed class EmailUserErasureHandler : IModuleUserErasureHandler
{
    /// <summary>Scope key used when reporting how many rows were deleted.</summary>
    public const string AllScopeKey = "all";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailUserErasureHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="EmailUserErasureHandler"/> class.</summary>
    /// <param name="scopeFactory">Creates the scope the scoped <see cref="EmailDbContext"/> comes from.</param>
    /// <param name="logger">The logger.</param>
    public EmailUserErasureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailUserErasureHandler> logger)
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
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
        var storage = scope.ServiceProvider.GetService<IAttachmentStorage>();

        var accountIds = await db.EmailAccounts
            .IgnoreQueryFilters()
            .Where(account => account.OwnerId == context.UserId)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);

        var deleted = 0;

        var messageIds = accountIds.Count == 0
            ? []
            : await db.EmailMessages
                .IgnoreQueryFilters()
                .Where(message => accountIds.Contains(message.AccountId))
                .Select(message => message.Id)
                .ToListAsync(cancellationToken);

        if (messageIds.Count > 0)
        {
            var attachments = await db.EmailAttachments
                .Where(attachment => messageIds.Contains(attachment.MessageId))
                .ToListAsync(cancellationToken);

            foreach (var attachment in attachments)
            {
                if (storage is not null && !string.IsNullOrWhiteSpace(attachment.StorageKey))
                {
                    await storage.DeleteAsync(attachment.StorageKey, cancellationToken);
                }
            }

            db.EmailAttachments.RemoveRange(attachments);
            deleted += attachments.Count;

            var messages = await db.EmailMessages
                .IgnoreQueryFilters()
                .Where(message => messageIds.Contains(message.Id))
                .ToListAsync(cancellationToken);

            db.EmailMessages.RemoveRange(messages);
            deleted += messages.Count;
        }

        if (accountIds.Count > 0)
        {
            var threads = await db.EmailThreads
                .Where(thread => accountIds.Contains(thread.AccountId))
                .ToListAsync(cancellationToken);

            db.EmailThreads.RemoveRange(threads);
            deleted += threads.Count;

            var mailboxes = await db.EmailMailboxes
                .Where(mailbox => accountIds.Contains(mailbox.AccountId))
                .ToListAsync(cancellationToken);

            db.EmailMailboxes.RemoveRange(mailboxes);
            deleted += mailboxes.Count;

            var accounts = await db.EmailAccounts
                .IgnoreQueryFilters()
                .Where(account => accountIds.Contains(account.Id))
                .ToListAsync(cancellationToken);

            db.EmailAccounts.RemoveRange(accounts);
            deleted += accounts.Count;
        }

        // Rules are owned directly by the user, and may target one of the accounts just removed.
        var rules = await db.EmailRules
            .Where(rule => rule.OwnerId == context.UserId
                || (rule.AccountId != null && accountIds.Contains(rule.AccountId.Value)))
            .ToListAsync(cancellationToken);

        if (rules.Count > 0)
        {
            var ruleIds = rules.Select(rule => rule.Id).ToList();

            var actions = await db.EmailRuleActions
                .Where(action => ruleIds.Contains(action.RuleId))
                .ToListAsync(cancellationToken);

            db.EmailRuleActions.RemoveRange(actions);
            deleted += actions.Count;

            var groups = await db.EmailRuleConditionGroups
                .Where(group => ruleIds.Contains(group.RuleId))
                .ToListAsync(cancellationToken);

            if (groups.Count > 0)
            {
                var groupIds = groups.Select(group => group.Id).ToList();

                var conditions = await db.EmailRuleConditions
                    .Where(condition => groupIds.Contains(condition.ConditionGroupId))
                    .ToListAsync(cancellationToken);

                db.EmailRuleConditions.RemoveRange(conditions);
                deleted += conditions.Count;

                db.EmailRuleConditionGroups.RemoveRange(groups);
                deleted += groups.Count;
            }

            db.EmailRules.RemoveRange(rules);
            deleted += rules.Count;
        }

        if (deleted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Email erasure for request {RequestId} removed {Deleted} rows",
            context.RequestId,
            deleted);

        return ModuleUserErasureResult.Success(new Dictionary<string, int> { [AllScopeKey] = deleted });
    }
}
