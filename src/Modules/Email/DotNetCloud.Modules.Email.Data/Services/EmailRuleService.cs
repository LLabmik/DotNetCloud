using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Email.Events;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Service implementation for managing and executing email rules/filters.
/// </summary>
public sealed class EmailRuleService : IEmailRuleService
{
    private readonly EmailDbContext _db;
    private readonly IEnumerable<IEmailProvider> _providers;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EmailRuleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailRuleService"/> class.
    /// </summary>
    public EmailRuleService(
        EmailDbContext db,
        IEnumerable<IEmailProvider> providers,
        IEventBus eventBus,
        ILogger<EmailRuleService> logger)
    {
        _db = db;
        _providers = providers;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailRule>> ListAsync(CallerContext caller, Guid? accountId, CancellationToken ct = default)
    {
        var query = _db.EmailRules.AsNoTracking()
            .Include(r => r.ConditionGroups).ThenInclude(g => g.Conditions)
            .Include(r => r.Actions)
            .Where(r => r.OwnerId == caller.UserId);

        if (accountId.HasValue)
            query = query.Where(r => r.AccountId == null || r.AccountId == accountId.Value);

        return await query.OrderBy(r => r.Priority).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<EmailRule?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        return await _db.EmailRules.AsNoTracking()
            .Include(r => r.ConditionGroups).ThenInclude(g => g.Conditions)
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == caller.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<EmailRule> CreateAsync(CreateEmailRuleRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var rule = new EmailRule
        {
            OwnerId = caller.UserId,
            AccountId = request.AccountId,
            Name = request.Name,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            StopProcessing = request.StopProcessing
        };

        foreach (var cg in request.ConditionGroups)
        {
            var group = new EmailRuleConditionGroup { MatchMode = cg.MatchMode };
            foreach (var c in cg.Conditions)
                group.Conditions.Add(new EmailRuleCondition { Field = c.Field, Operator = c.Operator, Value = c.Value });
            rule.ConditionGroups.Add(group);
        }

        foreach (var a in request.Actions)
            rule.Actions.Add(new EmailRuleAction { ActionType = a.ActionType, TargetValue = a.TargetValue });

        _db.EmailRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Email rule created: {RuleId} '{Name}'", rule.Id, rule.Name);
        return rule;
    }

    /// <inheritdoc />
    public async Task<EmailRule> UpdateAsync(Guid id, UpdateEmailRuleRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var rule = await _db.EmailRules
            .Include(r => r.ConditionGroups).ThenInclude(g => g.Conditions)
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.EmailRuleNotFound, "Email rule not found.");

        if (request.Name is not null) rule.Name = request.Name;
        if (request.IsEnabled.HasValue) rule.IsEnabled = request.IsEnabled.Value;
        if (request.Priority.HasValue) rule.Priority = request.Priority.Value;
        if (request.StopProcessing.HasValue) rule.StopProcessing = request.StopProcessing.Value;

        if (request.ConditionGroups is not null)
        {
            _db.EmailRuleConditionGroups.RemoveRange(rule.ConditionGroups);
            rule.ConditionGroups.Clear();
            foreach (var cg in request.ConditionGroups)
            {
                var group = new EmailRuleConditionGroup { MatchMode = cg.MatchMode };
                foreach (var c in cg.Conditions)
                    group.Conditions.Add(new EmailRuleCondition { Field = c.Field, Operator = c.Operator, Value = c.Value });
                rule.ConditionGroups.Add(group);
            }
        }

        if (request.Actions is not null)
        {
            _db.EmailRuleActions.RemoveRange(rule.Actions);
            rule.Actions.Clear();
            foreach (var a in request.Actions)
                rule.Actions.Add(new EmailRuleAction { ActionType = a.ActionType, TargetValue = a.TargetValue });
        }

        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Email rule updated: {RuleId}", id);
        return rule;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        var rule = await _db.EmailRules
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.EmailRuleNotFound, "Email rule not found.");

        _db.EmailRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Email rule deleted: {RuleId}", id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailRuleTriggeredEvent>> EvaluateForMessageAsync(EmailMessage message, EmailAccount account, CancellationToken ct = default)
    {
        var triggered = new List<EmailRuleTriggeredEvent>();

        var rules = await _db.EmailRules
            .Include(r => r.ConditionGroups).ThenInclude(g => g.Conditions)
            .Include(r => r.Actions)
            .Where(r => r.OwnerId == account.OwnerId && r.IsEnabled)
            .Where(r => r.AccountId == null || r.AccountId == account.Id)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        var appliedActions = new List<EmailAction>();

        foreach (var rule in rules)
        {
            if (EvaluateRule(rule, message))
            {
                var actions = rule.Actions
                    .Select(a => new EmailAction
                    {
                        ActionType = a.ActionType,
                        MessageProviderIds = new List<string> { message.ProviderMessageId },
                        TargetValue = a.TargetValue
                    })
                    .ToList();

                appliedActions.AddRange(actions);

                triggered.Add(new EmailRuleTriggeredEvent
                {
                    EventId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    RuleId = rule.Id,
                    MessageId = message.Id,
                    OwnerId = account.OwnerId,
                    ExecutedActions = actions
                        .Select(a => a.ActionType.ToString())
                        .ToList()
                });

                // Apply actions via provider
                if (appliedActions.Count > 0)
                {
                    var provider = _providers.FirstOrDefault(p => p.ProviderType == account.ProviderType);
                    if (provider is not null)
                    {
                        try
                        {
                            await provider.ApplyActionsAsync(account, appliedActions, ct);
                            _logger.LogInformation("Rule '{RuleName}' applied {ActionCount} actions to message {MessageId}",
                                rule.Name, appliedActions.Count, message.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Rule '{RuleName}' failed to apply actions to message {MessageId}",
                                rule.Name, message.Id);
                        }
                    }
                }

                if (rule.StopProcessing)
                    break;
            }
        }

        return triggered;
    }

    /// <inheritdoc />
    public async Task<int> RunRulesAsync(CallerContext caller, Guid? accountId, Guid? mailboxId, CancellationToken ct = default)
    {
        _logger.LogInformation("Manual rule run requested by user {UserId}", caller.UserId);

        var accountQuery = _db.EmailAccounts
            .Where(a => a.OwnerId == caller.UserId && a.IsEnabled && !a.IsDeleted);

        if (accountId.HasValue)
            accountQuery = accountQuery.Where(a => a.Id == accountId.Value);

        var accounts = await accountQuery.ToListAsync(ct);
        var matchCount = 0;

        foreach (var account in accounts)
        {
            // Get recent unread messages
            var messages = await _db.EmailMessages
                .Where(m => m.AccountId == account.Id && !m.IsRead && !m.IsDeleted)
                .OrderByDescending(m => m.DateReceived)
                .Take(200)
                .ToListAsync(ct);

            foreach (var message in messages)
            {
                var triggered = await EvaluateForMessageAsync(message, account, ct);
                if (triggered.Count > 0)
                {
                    matchCount++;

                    foreach (var t in triggered)
                    {
                        await _eventBus.PublishAsync(t, caller, ct);
                    }
                }
            }
        }

        _logger.LogInformation("Manual rule run completed: {MatchCount} messages matched", matchCount);
        return matchCount;
    }

    private static bool EvaluateRule(EmailRule rule, EmailMessage message)
    {
        if (rule.ConditionGroups.Count == 0)
            return false;

        foreach (var group in rule.ConditionGroups)
        {
            var matches = group.Conditions
                .Select(c => EvaluateCondition(c, message))
                .ToList();

            var groupMatches = group.MatchMode == ConditionMatchMode.All
                ? matches.All(m => m)
                : matches.Any(m => m);

            if (!groupMatches)
                return false;
        }

        return true;
    }

    private static bool EvaluateCondition(EmailRuleCondition condition, EmailMessage message)
    {
        if (condition.Field == EmailRuleField.HasAttachment)
        {
            var hasAttach = message.Attachments.Count > 0;
            return condition.Operator switch
            {
                EmailRuleOperator.Equals => hasAttach == bool.TryParse(condition.Value, out var b) && b,
                _ => hasAttach
            };
        }

        var value = GetFieldValue(condition.Field, message);
        if (value is null) return false;

        return condition.Operator switch
        {
            EmailRuleOperator.Contains =>
                value.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            EmailRuleOperator.Equals =>
                value.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            EmailRuleOperator.StartsWith =>
                value.StartsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
            EmailRuleOperator.EndsWith =>
                value.EndsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
            EmailRuleOperator.Regex =>
                System.Text.RegularExpressions.Regex.IsMatch(value, condition.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            _ => false
        };
    }

    private static string? GetFieldValue(EmailRuleField field, EmailMessage message)
    {
        return field switch
        {
            EmailRuleField.From => message.FromJson,
            EmailRuleField.To => message.ToJson,
            EmailRuleField.Cc => message.CcJson,
            EmailRuleField.Subject => message.Subject,
            EmailRuleField.Body => message.BodyPreview,
            _ => null
        };
    }
}
