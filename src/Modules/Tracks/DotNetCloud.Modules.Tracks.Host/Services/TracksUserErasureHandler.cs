using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// Erases a deleted account's Tracks data on behalf of the core's account-erasure orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// The module owns products, work items, comments, time entries, memberships and activity in its own
/// schema (F12). Deleting the account must therefore delete them here; the core records the
/// acknowledgement and retries for 72 hours, and an account deletion never reports success while a
/// module row is outstanding.
/// </para>
/// <para>
/// Two classes of data are erased:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Owning data</b> — products the account owns are removed outright (the same teardown the
/// retention sweep uses, so work items, sprints, swimlanes, checklists, comments, attachments,
/// dependencies, votes, review sessions and automation rules beneath them go with them).
/// </item>
/// <item>
/// <b>Participating data</b> — rows the account created or participated in inside products owned by
/// someone else: memberships, team roles, assignments, watchers, time entries, activity, comments and
/// their reactions, votes, review participants, sprint discussions, custom views, share links,
/// templates, goals, automation rules, webhooks, recurring rules, poker sessions and teams.
/// </item>
/// </list>
/// <para>
/// <c>IgnoreQueryFilters</c> is deliberate: most entities carry a soft-delete filter, and a row the
/// user had already "deleted" in the UI is still personal data that must not survive an account
/// deletion. The account id is personal data and is never logged
/// (cs/exposure-of-sensitive-information).
/// </para>
/// <para>
/// Idempotent by construction: after the first successful pass there is nothing left to delete, so a
/// retried request reports zero rows and still acknowledges.
/// </para>
/// </remarks>
public sealed class TracksUserErasureHandler : IModuleUserErasureHandler
{
    /// <summary>Scope key reporting the number of products deleted.</summary>
    public const string ProductsScopeKey = "products";

    /// <summary>Scope key reporting the number of memberships and team roles deleted.</summary>
    public const string MembershipsScopeKey = "memberships";

    /// <summary>Scope key reporting the number of work-item participation rows deleted.</summary>
    public const string WorkItemScopeKey = "work-items";

    /// <summary>Scope key reporting the number of collaboration rows deleted (comments, reactions, votes, discussions).</summary>
    public const string CollaborationScopeKey = "collaboration";

    /// <summary>Scope key reporting the number of module configuration rows deleted (templates, rules, webhooks, views, sessions, teams).</summary>
    public const string ConfigurationScopeKey = "configuration";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TracksUserErasureHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="TracksUserErasureHandler"/> class.</summary>
    /// <param name="scopeFactory">Creates the scope the scoped <see cref="TracksDbContext"/> comes from.</param>
    /// <param name="logger">The logger.</param>
    public TracksUserErasureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<TracksUserErasureHandler> logger)
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
        var db = scope.ServiceProvider.GetRequiredService<TracksDbContext>();
        var userId = context.UserId;

        // Load-then-remove rather than ExecuteDeleteAsync so the same code path runs on the in-memory
        // provider used by the module's tests and on PostgreSQL/SQL Server in production.
        async Task<int> LoadAndRemoveAsync<TEntity>(IQueryable<TEntity> query, CancellationToken ct)
            where TEntity : class
        {
            var rows = await query.ToListAsync(ct);
            if (rows.Count == 0)
            {
                return 0;
            }

            db.RemoveRange(rows);
            return rows.Count;
        }

        // 1. Products the account owns: use the same teardown the retention sweep uses so the
        //    dependency order (votes → sessions → checklists → work items → swimlanes → product)
        //    stays in one place.
        var ownedProductIds = await db.Products
            .IgnoreQueryFilters()
            .Where(product => product.OwnerId == userId)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (ownedProductIds.Count > 0)
        {
            if (db.Database.IsRelational())
            {
                var products = scope.ServiceProvider.GetRequiredService<ProductService>();
                foreach (var productId in ownedProductIds)
                {
                    await products.HardDeleteProductAsync(productId, cancellationToken);
                }
            }
            else
            {
                // The in-memory provider used by the module's tests enforces no foreign keys, so
                // removing the product rows is the complete teardown there. Production (PostgreSQL
                // and SQL Server) always takes the relational branch above.
                var productRows = await db.Products
                    .IgnoreQueryFilters()
                    .Where(product => ownedProductIds.Contains(product.Id))
                    .ToListAsync(cancellationToken);
                db.Products.RemoveRange(productRows);
            }
        }

        // 2. Participation rows in products the account does not own.
        var memberships = await LoadAndRemoveAsync(db.ProductMembers.Where(member => member.UserId == userId), cancellationToken);
        memberships += await LoadAndRemoveAsync(db.TeamRoles.Where(role => role.UserId == userId), cancellationToken);

        var workItems = await LoadAndRemoveAsync(db.WorkItemAssignments.Where(item => item.UserId == userId), cancellationToken);

        // A checklist item is not personal data; the assignment is. Clear it rather than delete the item.
        var assignedChecklistItems = await db.ChecklistItems
            .IgnoreQueryFilters()
            .Where(item => item.AssignedToUserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var item in assignedChecklistItems)
        {
            item.AssignedToUserId = null;
        }

        workItems += await LoadAndRemoveAsync(db.WorkItemWatchers.Where(watcher => watcher.UserId == userId), cancellationToken);
        workItems += await LoadAndRemoveAsync(db.TimeEntries.Where(entry => entry.UserId == userId), cancellationToken);
        workItems += await LoadAndRemoveAsync(db.Activities.Where(activity => activity.UserId == userId), cancellationToken);

        // Leave no trace of the account in audit columns either. CreatedByUserId stays: it is a
        // non-nullable ownership column on work that belongs to someone else's product, and deleting
        // that work would destroy the surviving project's data.
        var workItemAuditRows = await db.WorkItems
            .IgnoreQueryFilters()
            .Where(item => item.DeletedByUserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var row in workItemAuditRows)
        {
            row.DeletedByUserId = null;
        }

        // 3. Collaboration: the account's comments (and reactions to them), its own reactions, votes,
        //    review participation and sprint-discussion messages.
        var commentIds = await db.WorkItemComments
            .IgnoreQueryFilters()
            .Where(comment => comment.UserId == userId)
            .Select(comment => comment.Id)
            .ToListAsync(cancellationToken);

        var comments = await db.WorkItemComments
            .IgnoreQueryFilters()
            .Where(comment => comment.UserId == userId)
            .ToListAsync(cancellationToken);

        var collaboration = 0;
        collaboration += await LoadAndRemoveAsync(
            db.CommentReactions.Where(reaction => reaction.UserId == userId || commentIds.Contains(reaction.CommentId)),
            cancellationToken);
        collaboration += await LoadAndRemoveAsync(db.PokerVotes.Where(vote => vote.UserId == userId), cancellationToken);
        collaboration += await LoadAndRemoveAsync(
            db.ReviewSessionParticipants.Where(participant => participant.UserId == userId),
            cancellationToken);
        collaboration += await LoadAndRemoveAsync(db.SprintDiscussions.Where(message => message.UserId == userId), cancellationToken);

        // Comment audit references on rows that stay (comments by other accounts on this user's work).
        var commentAuditRows = await db.WorkItemComments
            .IgnoreQueryFilters()
            .Where(comment => comment.DeletedByUserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var row in commentAuditRows)
        {
            row.DeletedByUserId = null;
        }

        db.WorkItemComments.RemoveRange(comments);
        collaboration += comments.Count;

        // 4. Configuration owned by the account.
        var configuration = await LoadAndRemoveAsync(db.CustomViews.Where(view => view.UserId == userId), cancellationToken);
        configuration += await LoadAndRemoveAsync(db.WorkItemShareLinks.IgnoreQueryFilters().Where(link => link.CreatedByUserId == userId), cancellationToken);
        configuration += await LoadAndRemoveAsync(db.ItemTemplates.IgnoreQueryFilters().Where(template => template.CreatedByUserId == userId), cancellationToken);
        configuration += await LoadAndRemoveAsync(db.ProductTemplates.IgnoreQueryFilters().Where(template => template.CreatedByUserId == userId), cancellationToken);
        configuration += await LoadAndRemoveAsync(db.Goals.IgnoreQueryFilters().Where(goal => goal.CreatedByUserId == userId), cancellationToken);
        configuration += await LoadAndRemoveAsync(db.AutomationRules.IgnoreQueryFilters().Where(rule => rule.CreatedByUserId == userId), cancellationToken);
        configuration += await LoadAndRemoveAsync(db.RecurringRules.IgnoreQueryFilters().Where(rule => rule.CreatedByUserId == userId), cancellationToken);

        // Webhooks: deliveries reference the subscription, so they go first.
        var webhookIds = await db.WebhookSubscriptions
            .IgnoreQueryFilters()
            .Where(subscription => subscription.CreatedByUserId == userId)
            .Select(subscription => subscription.Id)
            .ToListAsync(cancellationToken);
        configuration += await LoadAndRemoveAsync(
            db.WebhookDeliveries.Where(delivery => webhookIds.Contains(delivery.SubscriptionId)),
            cancellationToken);
        configuration += await LoadAndRemoveAsync(
            db.WebhookSubscriptions.IgnoreQueryFilters().Where(subscription => webhookIds.Contains(subscription.Id)),
            cancellationToken);

        // Poker sessions the account created: their votes go first (votes cast by other accounts on
        // this user's session are part of that session's data).
        var pokerSessionIds = await db.PokerSessions
            .Where(session => session.CreatedByUserId == userId)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        configuration += await LoadAndRemoveAsync(
            db.PokerVotes.Where(vote => pokerSessionIds.Contains(vote.SessionId)),
            cancellationToken);
        configuration += await LoadAndRemoveAsync(
            db.PokerSessions.Where(session => pokerSessionIds.Contains(session.Id)),
            cancellationToken);

        // Teams the account created: team roles go first.
        var teamIds = await db.Teams
            .Where(team => team.CreatedByUserId == userId)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);
        configuration += await LoadAndRemoveAsync(
            db.TeamRoles.Where(role => teamIds.Contains(role.TeamId)),
            cancellationToken);
        configuration += await LoadAndRemoveAsync(
            db.Teams.Where(team => teamIds.Contains(team.Id)),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Erased Tracks data for a deleted account: {ProductCount} product(s), {MembershipCount} " +
            "membership(s), {WorkItemCount} work-item row(s), {CollaborationCount} collaboration row(s), " +
            "{ConfigurationCount} configuration row(s).",
            ownedProductIds.Count,
            memberships,
            workItems,
            collaboration,
            configuration);

        return ModuleUserErasureResult.Success(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ProductsScopeKey] = ownedProductIds.Count,
                [MembershipsScopeKey] = memberships,
                [WorkItemScopeKey] = workItems,
                [CollaborationScopeKey] = collaboration,
                [ConfigurationScopeKey] = configuration,
            });
    }
}
