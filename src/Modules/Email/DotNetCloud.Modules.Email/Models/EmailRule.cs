namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// A server-side email filter rule with conditions and actions.
/// </summary>
public sealed class EmailRule
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this rule.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Optional: restrict to a specific account.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Rule display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the rule is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Execution priority (lower runs first).</summary>
    public int Priority { get; set; }

    /// <summary>Whether to stop processing further rules after this one matches.</summary>
    public bool StopProcessing { get; set; }

    /// <summary>When the rule was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the rule was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Condition groups for this rule.</summary>
    public ICollection<EmailRuleConditionGroup> ConditionGroups { get; set; } = new List<EmailRuleConditionGroup>();

    /// <summary>Actions for this rule.</summary>
    public ICollection<EmailRuleAction> Actions { get; set; } = new List<EmailRuleAction>();
}
