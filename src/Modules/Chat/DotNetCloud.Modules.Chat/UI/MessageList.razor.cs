using Microsoft.AspNetCore.Components;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the message list component.
/// Displays messages with reactions, attachments, and typing indicators.
/// </summary>
public partial class MessageList : ComponentBase
{
    // Inline patterns (applied after HTML-encoding)
    private static readonly Regex InlineCodeRegex = new("`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex BoldItalicRegex = new("\\*\\*\\*([^*]+)\\*\\*\\*", RegexOptions.Compiled);
    private static readonly Regex BoldRegex = new("\\*\\*([^*]+)\\*\\*", RegexOptions.Compiled);
    private static readonly Regex ItalicAsteriskRegex = new("\\*([^*]+)\\*", RegexOptions.Compiled);
    private static readonly Regex ItalicUnderscoreRegex = new("_([^_]+)_", RegexOptions.Compiled);
    private static readonly Regex StrikethroughRegex = new("~~([^~]+)~~", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new("\\[([^\\]]+)\\]\\((https?://[^)]+)\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeadingRegex = new("^(#{1,6})\\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new("^\\d+\\.\\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex HorizontalRuleRegex = new("^(-{3,}|\\*{3,}|_{3,})$", RegexOptions.Compiled);

    private ElementReference _messageListRef;

    /// <summary>The list of messages to display.</summary>
    [Parameter]
    public List<MessageViewModel> Messages { get; set; } = [];

    /// <summary>Whether messages are currently loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Error message shown when message loading fails.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Whether there are more messages to load.</summary>
    [Parameter]
    public bool HasMoreMessages { get; set; }

    /// <summary>Whether a channel is currently selected.</summary>
    [Parameter]
    public bool ChannelSelected { get; set; }

    /// <summary>Users currently typing in the channel.</summary>
    [Parameter]
    public List<TypingUserViewModel> TypingUsers { get; set; } = [];

    /// <summary>Callback to load older messages.</summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>Callback when a reaction is toggled.</summary>
    [Parameter]
    public EventCallback<(Guid MessageId, string Emoji)> OnReactionToggle { get; set; }

    /// <summary>Callback when the user wants to edit a message.</summary>
    [Parameter]
    public EventCallback<MessageViewModel> OnEditMessage { get; set; }

    /// <summary>Callback when the user wants to delete a message.</summary>
    [Parameter]
    public EventCallback<Guid> OnDeleteMessage { get; set; }

    /// <summary>Callback when the user wants to reply to a message.</summary>
    [Parameter]
    public EventCallback<MessageViewModel> OnReplyToMessage { get; set; }

    /// <summary>The current user's ID (for showing edit/delete on own messages).</summary>
    [Parameter]
    public Guid CurrentUserId { get; set; }

    /// <summary>Currently editing message, if any.</summary>
    [Parameter]
    public MessageViewModel? EditingMessage { get; set; }

    /// <summary>
    /// Message ID where the unread/new messages divider should appear.
    /// </summary>
    [Parameter]
    public Guid? NewMessagesStartMessageId { get; set; }

    /// <summary>Reference to the message list element for scrolling.</summary>
    protected ElementReference MessageListRef
    {
        get => _messageListRef;
        set => _messageListRef = value;
    }

    /// <summary>Loads older messages.</summary>
    protected async Task LoadMoreMessages()
    {
        await OnLoadMore.InvokeAsync();
    }

    /// <summary>Toggles a reaction on a message.</summary>
    protected async Task ToggleReaction(Guid messageId, string emoji)
    {
        await OnReactionToggle.InvokeAsync((messageId, emoji));
    }

    /// <summary>Requests editing a message.</summary>
    protected async Task RequestEdit(MessageViewModel message)
    {
        await OnEditMessage.InvokeAsync(message);
    }

    /// <summary>Requests deleting a message.</summary>
    protected async Task RequestDelete(Guid messageId)
    {
        await OnDeleteMessage.InvokeAsync(messageId);
    }

    /// <summary>Requests replying to a message.</summary>
    protected async Task RequestReply(MessageViewModel message)
    {
        await OnReplyToMessage.InvokeAsync(message);
    }

    /// <summary>Gets the initials from a display name for the avatar placeholder.</summary>
    protected static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant()
        };
    }

    /// <summary>Formats a timestamp for display.</summary>
    protected static string FormatTime(DateTime sentAt)
    {
        var now = DateTime.UtcNow;
        var diff = now - sentAt;

        if (diff.TotalMinutes < 1)
        {
            return "just now";
        }

        if (diff.TotalHours < 1)
        {
            return $"{(int)diff.TotalMinutes}m ago";
        }

        if (sentAt.Date == now.Date)
        {
            return sentAt.ToString("HH:mm");
        }

        if (sentAt.Date == now.Date.AddDays(-1))
        {
            return $"Yesterday {sentAt:HH:mm}";
        }

        return sentAt.ToString("MMM d, HH:mm");
    }

    /// <summary>Formats a file size for display.</summary>
    protected static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
    }

    /// <summary>Gets the typing indicator text.</summary>
    protected string GetTypingText()
    {
        return TypingUsers.Count switch
        {
            0 => string.Empty,
            1 => $"{TypingUsers[0].DisplayName} is typing...",
            2 => $"{TypingUsers[0].DisplayName} and {TypingUsers[1].DisplayName} are typing...",
            _ => "Several people are typing..."
        };
    }

    /// <summary>
    /// Converts Markdown to safe HTML for message display.
    /// Supports bold, italic, strikethrough, inline code, code blocks,
    /// links, headings, blockquotes, lists, and horizontal rules.
    /// </summary>
    protected static MarkupString RenderMarkdown(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new MarkupString(string.Empty);
        }

        var encoded = HtmlEncoder.Default.Encode(content);
        var lines = encoded.Replace("\r\n", "\n", StringComparison.Ordinal)
                           .Split('\n');
        var result = new StringBuilder();
        var inCodeBlock = false;
        var codeBlockContent = new StringBuilder();
        var inList = false;
        var listTag = string.Empty;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Code fence toggle (``` after HTML-encoding is still ```)
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    result.Append("<pre><code>").Append(codeBlockContent).Append("</code></pre>");
                    codeBlockContent.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    CloseList(result, ref inList, ref listTag);
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                if (codeBlockContent.Length > 0)
                {
                    codeBlockContent.Append('\n');
                }
                codeBlockContent.Append(line);
                continue;
            }

            // Horizontal rule
            if (HorizontalRuleRegex.IsMatch(trimmed))
            {
                CloseList(result, ref inList, ref listTag);
                result.Append("<hr />");
                continue;
            }

            // Determine if current line is a list item
            var isUnordered = trimmed.StartsWith("- ", StringComparison.Ordinal)
                           || trimmed.StartsWith("* ", StringComparison.Ordinal);
            var isOrdered = !isUnordered && OrderedListRegex.IsMatch(trimmed);

            // Close list if this line is not a list item
            if (inList && !isUnordered && !isOrdered)
            {
                CloseList(result, ref inList, ref listTag);
            }

            // Heading
            var headingMatch = HeadingRegex.Match(trimmed);
            if (headingMatch.Success)
            {
                CloseList(result, ref inList, ref listTag);
                var level = headingMatch.Groups[1].Value.Length;
                result.Append("<h").Append(level).Append('>')
                      .Append(ApplyInlineFormatting(headingMatch.Groups[2].Value))
                      .Append("</h").Append(level).Append('>');
                continue;
            }

            // Blockquote (> becomes &gt; after HTML-encoding)
            if (trimmed.StartsWith("&gt; ", StringComparison.Ordinal))
            {
                CloseList(result, ref inList, ref listTag);
                var quoteContent = trimmed[5..];
                result.Append("<blockquote>").Append(ApplyInlineFormatting(quoteContent)).Append("</blockquote>");
                continue;
            }

            // Unordered list
            if (isUnordered)
            {
                if (!inList || listTag != "ul")
                {
                    CloseList(result, ref inList, ref listTag);
                    result.Append("<ul>");
                    inList = true;
                    listTag = "ul";
                }
                var itemContent = trimmed[2..];
                result.Append("<li>").Append(ApplyInlineFormatting(itemContent)).Append("</li>");
                continue;
            }

            // Ordered list
            if (isOrdered)
            {
                if (!inList || listTag != "ol")
                {
                    CloseList(result, ref inList, ref listTag);
                    result.Append("<ol>");
                    inList = true;
                    listTag = "ol";
                }
                var olMatch = OrderedListRegex.Match(trimmed);
                result.Append("<li>").Append(ApplyInlineFormatting(olMatch.Groups[1].Value)).Append("</li>");
                continue;
            }

            // Regular line
            result.Append(ApplyInlineFormatting(line));
            if (i < lines.Length - 1)
            {
                result.Append("<br />");
            }
        }

        CloseList(result, ref inList, ref listTag);

        if (inCodeBlock)
        {
            result.Append("<pre><code>").Append(codeBlockContent).Append("</code></pre>");
        }

        return new MarkupString(result.ToString());
    }

    /// <summary>Applies inline Markdown formatting to an already-HTML-encoded string.</summary>
    private static string ApplyInlineFormatting(string html)
    {
        // Inline code first (protect content from later patterns)
        html = InlineCodeRegex.Replace(html, "<code>$1</code>");
        // Links
        html = LinkRegex.Replace(html, "<a href=\"$2\" target=\"_blank\" rel=\"noopener noreferrer\">$1</a>");
        // Bold + italic
        html = BoldItalicRegex.Replace(html, "<strong><em>$1</em></strong>");
        // Bold
        html = BoldRegex.Replace(html, "<strong>$1</strong>");
        // Italic
        html = ItalicAsteriskRegex.Replace(html, "<em>$1</em>");
        html = ItalicUnderscoreRegex.Replace(html, "<em>$1</em>");
        // Strikethrough
        html = StrikethroughRegex.Replace(html, "<s>$1</s>");
        return html;
    }

    /// <summary>Closes an open list element in the output.</summary>
    private static void CloseList(StringBuilder sb, ref bool inList, ref string listTag)
    {
        if (!inList)
        {
            return;
        }
        sb.Append("</").Append(listTag).Append('>');
        inList = false;
        listTag = string.Empty;
    }

    /// <summary>
    /// Returns true when a message attachment should render an inline preview.
    /// </summary>
    protected static bool IsInlinePreviewAttachment(AttachmentViewModel attachment)
    {
        return attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || attachment.MimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            || attachment.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }
}
