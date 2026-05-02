namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// A single condition within a rule condition group.
/// </summary>
public sealed class EmailRuleCondition
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent condition group.</summary>
    public Guid ConditionGroupId { get; set; }

    /// <summary>The field to check.</summary>
    public EmailRuleField Field { get; set; }

    /// <summary>The comparison operator.</summary>
    public EmailRuleOperator Operator { get; set; }

    /// <summary>The value to compare against.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>When the condition was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent condition group navigation property.</summary>
    public EmailRuleConditionGroup? ConditionGroup { get; set; }
}

/// <summary>
/// Fields that can be matched in an email rule.
/// </summary>
public enum EmailRuleField
{
    /// <summary>From address.</summary>
    From = 0,

    /// <summary>To address.</summary>
    To = 1,

    /// <summary>Cc address.</summary>
    Cc = 2,

    /// <summary>Subject line.</summary>
    Subject = 3,

    /// <summary>Body text preview.</summary>
    Body = 4,

    /// <summary>Whether the message has attachments.</summary>
    HasAttachment = 5,

    /// <summary>Message size in bytes.</summary>
    Size = 6,

    /// <summary>Mailbox/label name.</summary>
    Label = 7
}

/// <summary>
/// Comparison operators for rule conditions.
/// </summary>
public enum EmailRuleOperator
{
    /// <summary>Field contains the value (case-insensitive).</summary>
    Contains = 0,

    /// <summary>Field equals the value.</summary>
    Equals = 1,

    /// <summary>Field starts with the value.</summary>
    StartsWith = 2,

    /// <summary>Field ends with the value.</summary>
    EndsWith = 3,

    /// <summary>Field matches the regex pattern.</summary>
    Regex = 4,

    /// <summary>Numeric field is greater than the value.</summary>
    GreaterThan = 5,

    /// <summary>Numeric field is less than the value.</summary>
    LessThan = 6
}
