namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// An action to execute when a rule matches.
/// </summary>
public sealed class EmailRuleAction
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent rule.</summary>
    public Guid RuleId { get; set; }

    /// <summary>The type of action to perform.</summary>
    public EmailRuleActionType ActionType { get; set; }

    /// <summary>Optional target value (mailbox name, label, email address for forward, etc.).</summary>
    public string? TargetValue { get; set; }

    /// <summary>Sort order within the rule.</summary>
    public int SortOrder { get; set; }

    /// <summary>When the action was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent rule navigation property.</summary>
    public EmailRule? Rule { get; set; }
}

/// <summary>
/// Types of actions an email rule can perform.
/// </summary>
public enum EmailRuleActionType
{
    /// <summary>Mark the message as read.</summary>
    MarkRead = 0,

    /// <summary>Mark the message as unread.</summary>
    MarkUnread = 1,

    /// <summary>Star/flag the message.</summary>
    Star = 2,

    /// <summary>Unstar/unflag the message.</summary>
    Unstar = 3,

    /// <summary>Move to a specific mailbox.</summary>
    MoveToFolder = 4,

    /// <summary>Apply a label (Gmail only).</summary>
    ApplyLabel = 5,

    /// <summary>Archive the message (Gmail only).</summary>
    Archive = 6,

    /// <summary>Forward to an email address.</summary>
    ForwardTo = 7
}
