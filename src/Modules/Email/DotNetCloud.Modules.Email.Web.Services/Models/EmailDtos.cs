namespace DotNetCloud.Modules.Email.Web.Services;

/// <summary>
/// An email account as the module's own REST API returns it.
/// </summary>
/// <remarks>
/// Enum-valued properties are kept as <see cref="int"/> on purpose: this host serialises and binds
/// enums as numbers (MVC defaults), so a client that writes enum names would be rejected.
/// </remarks>
public sealed record EmailAccountDto
{
    /// <summary>Gets the account id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the provider type as the host's numeric enum value.</summary>
    public int ProviderType { get; init; }

    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the email address.</summary>
    public string EmailAddress { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the account is enabled.</summary>
    public bool IsEnabled { get; init; }
}

/// <summary>
/// A mailbox (folder) of an email account.
/// </summary>
public sealed record EmailMailboxDto
{
    /// <summary>Gets the mailbox id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the account id.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the mailbox flags as the host's numeric enum value (Inbox = 1, Sent = 2, …).</summary>
    public int SyncFlags { get; init; }

    /// <summary>Gets the sort order within the account.</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets when the mailbox was last synced.</summary>
    public DateTime? LastSyncedAt { get; init; }
}

/// <summary>
/// A conversation thread in a mailbox.
/// </summary>
public sealed record EmailThreadDto
{
    /// <summary>Gets the thread id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the account id.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets the subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gets the participants as a JSON array of addresses.</summary>
    public string ParticipantsJson { get; init; } = "[]";

    /// <summary>Gets how many messages the thread holds.</summary>
    public int MessageCount { get; init; }

    /// <summary>Gets when the last message arrived.</summary>
    public DateTime? LastMessageAt { get; init; }
}

/// <summary>
/// One message in a thread.
/// </summary>
public sealed record EmailMessageDto
{
    /// <summary>Gets the message id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the thread id.</summary>
    public Guid ThreadId { get; init; }

    /// <summary>Gets the sender as a JSON array of addresses.</summary>
    public string? FromJson { get; init; }

    /// <summary>Gets the subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gets the plain-text preview of the body.</summary>
    public string? BodyPreview { get; init; }

    /// <summary>Gets when the message was received.</summary>
    public DateTime? DateReceived { get; init; }

    /// <summary>Gets when the record was created.</summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>Gets the message's attachments.</summary>
    public IReadOnlyList<EmailAttachmentDto> Attachments { get; init; } = [];
}

/// <summary>
/// An attachment of a message, or one staged for sending.
/// </summary>
public sealed record EmailAttachmentDto
{
    /// <summary>Gets the attachment id, when it belongs to a stored message.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the storage key, when the attachment is staged for sending.</summary>
    public string? StorageKey { get; init; }

    /// <summary>Gets the file name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Gets the content type.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Gets the size in bytes.</summary>
    public long Size { get; init; }
}

/// <summary>
/// An address as the host stores it inside the <c>*Json</c> columns.
/// </summary>
public sealed record EmailAddressDto
{
    /// <summary>Gets the display name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the address.</summary>
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// An email rule.
/// </summary>
public sealed record EmailRuleDto
{
    /// <summary>Gets the rule id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the account the rule applies to, or <see langword="null"/> for every account.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Gets the rule name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the rule is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Gets the priority; lower runs first.</summary>
    public int Priority { get; init; }

    /// <summary>Gets a value indicating whether later rules are skipped once this one matches.</summary>
    public bool StopProcessing { get; init; }

    /// <summary>Gets the condition groups.</summary>
    public IReadOnlyList<EmailRuleConditionGroupDto> ConditionGroups { get; init; } = [];

    /// <summary>Gets the actions.</summary>
    public IReadOnlyList<EmailRuleActionDto> Actions { get; init; } = [];
}

/// <summary>
/// A group of rule conditions combined with one match mode.
/// </summary>
public sealed record EmailRuleConditionGroupDto
{
    /// <summary>Gets the match mode (0 = all, 1 = any).</summary>
    public int MatchMode { get; init; }

    /// <summary>Gets the conditions.</summary>
    public IReadOnlyList<EmailRuleConditionDto> Conditions { get; init; } = [];
}

/// <summary>
/// One rule condition.
/// </summary>
public sealed record EmailRuleConditionDto
{
    /// <summary>Gets the field (0 = From, 1 = To, 2 = Cc, 3 = Subject, 4 = Body, 5 = Has attachment).</summary>
    public int Field { get; init; }

    /// <summary>Gets the operator (0 = contains, 1 = equals, 2 = starts with, 3 = ends with, 4 = regex).</summary>
    public int Operator { get; init; }

    /// <summary>Gets the comparison value.</summary>
    public string Value { get; init; } = string.Empty;
}

/// <summary>
/// One rule action.
/// </summary>
public sealed record EmailRuleActionDto
{
    /// <summary>Gets the action type (0 = mark read, 1 = mark unread, 2 = star, 3 = unstar, 4 = move, 5 = label, 6 = archive).</summary>
    public int ActionType { get; init; }

    /// <summary>Gets the target folder or label, for the actions that need one.</summary>
    public string? TargetValue { get; init; }
}

/// <summary>
/// A node of the acting user's cloud files, as the file browser lists it.
/// </summary>
public sealed record EmailFileNodeDto
{
    /// <summary>Gets the node id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the node type (<c>Folder</c>, <c>File</c> or <c>SymbolicLink</c>).</summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>Gets the MIME type.</summary>
    public string? MimeType { get; init; }

    /// <summary>Gets the size in bytes.</summary>
    public long Size { get; init; }
}

/// <summary>
/// The body of a message, fetched separately from the thread listing.
/// </summary>
public sealed record EmailMessageBodyDto
{
    /// <summary>Gets the HTML body.</summary>
    public string? BodyHtml { get; init; }
}

/// <summary>
/// The Gmail OAuth configuration state.
/// </summary>
public sealed record EmailGmailStatusDto
{
    /// <summary>Gets a value indicating whether an administrator configured Google OAuth.</summary>
    public bool Configured { get; init; }
}
