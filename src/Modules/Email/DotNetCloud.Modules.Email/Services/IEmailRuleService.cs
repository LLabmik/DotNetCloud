using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Email.Models;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Service for managing email rules/filters and executing them.
/// </summary>
public interface IEmailRuleService
{
    /// <summary>Lists rules for the caller.</summary>
    Task<IReadOnlyList<EmailRule>> ListAsync(CallerContext caller, Guid? accountId, CancellationToken ct = default);

    /// <summary>Gets a rule by ID.</summary>
    Task<EmailRule?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default);

    /// <summary>Creates a new rule.</summary>
    Task<EmailRule> CreateAsync(CreateEmailRuleRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Updates an existing rule.</summary>
    Task<EmailRule> UpdateAsync(Guid id, UpdateEmailRuleRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Deletes a rule.</summary>
    Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default);

    /// <summary>Evaluates all enabled rules for a given message.</summary>
    Task<IReadOnlyList<EmailRuleTriggeredEvent>> EvaluateForMessageAsync(EmailMessage message, EmailAccount account, CancellationToken ct = default);

    /// <summary>Manually runs rules for the caller, optionally scoped to an account or mailbox.</summary>
    Task<int> RunRulesAsync(CallerContext caller, Guid? accountId, Guid? mailboxId, CancellationToken ct = default);
}

/// <summary>Request DTO for creating an email rule.</summary>
public sealed record CreateEmailRuleRequest
{
    /// <summary>Rule display name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional: restrict to a specific account.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Whether the rule is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Execution priority.</summary>
    public int Priority { get; init; }

    /// <summary>Stop processing further rules if this matches.</summary>
    public bool StopProcessing { get; init; }

    /// <summary>Condition groups.</summary>
    public required IReadOnlyList<CreateConditionGroupRequest> ConditionGroups { get; init; }

    /// <summary>Actions to execute.</summary>
    public required IReadOnlyList<CreateRuleActionRequest> Actions { get; init; }
}

/// <summary>Request DTO for creating a condition group.</summary>
public sealed record CreateConditionGroupRequest
{
    /// <summary>Match mode.</summary>
    public ConditionMatchMode MatchMode { get; init; } = ConditionMatchMode.All;

    /// <summary>Conditions in this group.</summary>
    public required IReadOnlyList<CreateConditionRequest> Conditions { get; init; }
}

/// <summary>Request DTO for creating a condition.</summary>
public sealed record CreateConditionRequest
{
    /// <summary>The field to check.</summary>
    public required EmailRuleField Field { get; init; }

    /// <summary>The comparison operator.</summary>
    public required EmailRuleOperator Operator { get; init; }

    /// <summary>The value to compare against.</summary>
    public required string Value { get; init; }
}

/// <summary>Request DTO for creating a rule action.</summary>
public sealed record CreateRuleActionRequest
{
    /// <summary>The type of action.</summary>
    public required EmailRuleActionType ActionType { get; init; }

    /// <summary>Optional target value.</summary>
    public string? TargetValue { get; init; }
}

/// <summary>Request DTO for updating an email rule.</summary>
public sealed record UpdateEmailRuleRequest
{
    /// <summary>Updated name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated enabled state.</summary>
    public bool? IsEnabled { get; init; }

    /// <summary>Updated priority.</summary>
    public int? Priority { get; init; }

    /// <summary>Updated stop processing flag.</summary>
    public bool? StopProcessing { get; init; }

    /// <summary>Replacement condition groups.</summary>
    public IReadOnlyList<CreateConditionGroupRequest>? ConditionGroups { get; init; }

    /// <summary>Replacement actions.</summary>
    public IReadOnlyList<CreateRuleActionRequest>? Actions { get; init; }
}
