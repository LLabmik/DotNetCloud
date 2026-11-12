namespace DotNetCloud.Modules.Email.Web.Services;

/// <summary>
/// The panels the Email module UI can show.
/// </summary>
public enum EmailView
{
    /// <summary>Nothing selected: welcome or "no folder selected".</summary>
    Welcome,

    /// <summary>The thread list of the selected mailbox.</summary>
    Mailbox,

    /// <summary>One thread with its messages.</summary>
    Thread,

    /// <summary>The compose form.</summary>
    Compose,

    /// <summary>The rule list.</summary>
    Rules,

    /// <summary>The rule editor.</summary>
    Rule,

    /// <summary>The add-account form.</summary>
    AddAccount,

    /// <summary>The help page.</summary>
    Help,
}

/// <summary>
/// What the file browser is choosing.
/// </summary>
public enum FileBrowserMode
{
    /// <summary>Pick a file to attach.</summary>
    File,

    /// <summary>Pick a folder to save into.</summary>
    Folder,
}

/// <summary>
/// One message of an open thread together with its separately fetched body.
/// </summary>
/// <param name="Message">The message.</param>
/// <param name="BodyHtml">The HTML body, when the host had one.</param>
public sealed record MessageEntry(EmailMessageDto Message, string? BodyHtml);

/// <summary>
/// The result of the file browser.
/// </summary>
/// <param name="FolderId">The chosen folder, or <see langword="null"/> for the files root.</param>
/// <param name="FolderName">The chosen folder's display name.</param>
/// <param name="Node">The chosen file, when a file was picked.</param>
public sealed record FileChoice(Guid? FolderId, string FolderName, EmailFileNodeDto? Node);

/// <summary>
/// A pending confirmation, rendered by the page's modal.
/// </summary>
/// <param name="Title">The dialog title.</param>
/// <param name="Message">The explanation.</param>
/// <param name="ConfirmLabel">The label of the accepting button.</param>
/// <param name="Danger">Whether the action is destructive.</param>
/// <param name="Accepted">Raised when the user accepts.</param>
public sealed record ConfirmRequest(string Title, string Message, string ConfirmLabel, bool Danger, Func<Task> Accepted);

/// <summary>
/// The state of the cloud-file browser overlay.
/// </summary>
public sealed class FileBrowserState
{
    /// <summary>Gets or sets what is being chosen.</summary>
    public FileBrowserMode Mode { get; set; }

    /// <summary>Gets the breadcrumb trail of the open folders.</summary>
    public List<(Guid Id, string Name)> Trail { get; } = [];

    /// <summary>Gets or sets the folder's children.</summary>
    public List<EmailFileNodeDto> Nodes { get; set; } = [];

    /// <summary>Gets or sets the selected file.</summary>
    public EmailFileNodeDto? Selected { get; set; }

    /// <summary>Gets or sets a value indicating whether the folder is loading.</summary>
    public bool Loading { get; set; }

    /// <summary>Gets or sets the failure explanation.</summary>
    public string? Error { get; set; }

    /// <summary>Gets a value indicating whether the accepting button is enabled.</summary>
    public bool CanConfirm => Mode == FileBrowserMode.Folder || Selected is not null;

    /// <summary>Gets the currently open folder.</summary>
    public (Guid Id, string Name)? Current => Trail.Count > 0 ? Trail[^1] : null;
}

/// <summary>
/// The in-progress compose form.
/// </summary>
public sealed class ComposeDraft
{
    /// <summary>Gets or sets the account the message is sent from.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the To field.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Gets or sets the Cc field.</summary>
    public string Cc { get; set; } = string.Empty;

    /// <summary>Gets or sets the Bcc field.</summary>
    public string Bcc { get; set; } = string.Empty;

    /// <summary>Gets or sets the subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain-text body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets the staged attachments.</summary>
    public List<EmailAttachmentDto> Attachments { get; } = [];

    /// <summary>Gets or sets how many uploads are in flight.</summary>
    public int Uploading { get; set; }
}

/// <summary>
/// The in-progress rule edit.
/// </summary>
public sealed class RuleDraft
{
    /// <summary>Gets or sets the rule id; <see langword="null"/> for a new rule.</summary>
    public Guid? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account the rule applies to.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Gets or sets a value indicating whether the rule is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets or sets the priority.</summary>
    public int Priority { get; set; }

    /// <summary>Gets or sets a value indicating whether later rules are skipped.</summary>
    public bool StopProcessing { get; set; }

    /// <summary>Gets the condition groups.</summary>
    public List<RuleGroupDraft> Groups { get; } = [];

    /// <summary>Gets the actions.</summary>
    public List<RuleActionDraft> Actions { get; } = [];
}

/// <summary>
/// A group of conditions in the rule editor.
/// </summary>
public sealed class RuleGroupDraft
{
    /// <summary>Gets or sets the match mode (0 = all, 1 = any).</summary>
    public int MatchMode { get; set; }

    /// <summary>Gets the conditions.</summary>
    public List<RuleConditionDraft> Conditions { get; } = [];
}

/// <summary>
/// A condition in the rule editor.
/// </summary>
public sealed class RuleConditionDraft
{
    /// <summary>Gets or sets the field.</summary>
    public int Field { get; set; } = 3;

    /// <summary>Gets or sets the operator.</summary>
    public int Operator { get; set; }

    /// <summary>Gets or sets the comparison value.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// An action in the rule editor.
/// </summary>
public sealed class RuleActionDraft
{
    /// <summary>Gets or sets the action type.</summary>
    public int ActionType { get; set; }

    /// <summary>Gets or sets the target folder or label.</summary>
    public string? TargetValue { get; set; }
}

/// <summary>
/// The options of the rule editor's dropdowns.
/// </summary>
public static class RuleCatalog
{
    /// <summary>Gets the selectable condition fields.</summary>
    public static IReadOnlyList<(int Value, string Label)> Fields { get; } =
    [
        (0, "From"), (1, "To"), (2, "Cc"), (3, "Subject"), (4, "Body"), (5, "Has attachment"),
    ];

    /// <summary>Gets the selectable operators.</summary>
    public static IReadOnlyList<(int Value, string Label)> Operators { get; } =
    [
        (0, "contains"), (1, "equals"), (2, "starts with"), (3, "ends with"), (4, "matches regex"),
    ];

    /// <summary>Gets the selectable actions.</summary>
    public static IReadOnlyList<(int Value, string Label)> Actions { get; } =
    [
        (0, "Mark as read"), (1, "Mark as unread"), (2, "Star"), (3, "Unstar"),
        (4, "Move to folder"), (5, "Apply label"), (6, "Archive"),
    ];
}
