namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// A group of conditions evaluated together with All or Any match logic.
/// </summary>
public sealed class EmailRuleConditionGroup
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent rule.</summary>
    public Guid RuleId { get; set; }

    /// <summary>Match mode for conditions in this group.</summary>
    public ConditionMatchMode MatchMode { get; set; } = ConditionMatchMode.All;

    /// <summary>Sort order within the rule.</summary>
    public int SortOrder { get; set; }

    /// <summary>When the condition group was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent rule navigation property.</summary>
    public EmailRule? Rule { get; set; }

    /// <summary>Conditions in this group.</summary>
    public ICollection<EmailRuleCondition> Conditions { get; set; } = new List<EmailRuleCondition>();
}

/// <summary>
/// How conditions in a group are combined.
/// </summary>
public enum ConditionMatchMode
{
    /// <summary>All conditions must match.</summary>
    All = 0,

    /// <summary>Any condition must match.</summary>
    Any = 1
}
