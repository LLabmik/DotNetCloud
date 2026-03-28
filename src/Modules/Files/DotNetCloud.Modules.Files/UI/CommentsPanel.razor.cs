using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the comments side panel.
/// </summary>
public partial class CommentsPanel : ComponentBase
{
    /// <summary>Name of the file whose comments are displayed.</summary>
    [Parameter] public string? FileName { get; set; }

    /// <summary>Current user ID, used to determine edit/delete permissions.</summary>
    [Parameter] public Guid CurrentUserId { get; set; }

    /// <summary>Raised when the panel is closed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Raised when a new top-level comment is submitted.</summary>
    [Parameter] public EventCallback<string> OnAddComment { get; set; }

    /// <summary>Raised when a reply is submitted (parentCommentId, content).</summary>
    [Parameter] public EventCallback<(Guid ParentCommentId, string Content)> OnReplyComment { get; set; }

    /// <summary>Raised when a comment is edited (commentId, newContent).</summary>
    [Parameter] public EventCallback<(Guid CommentId, string Content)> OnEditComment { get; set; }

    /// <summary>Raised when a comment is deleted.</summary>
    [Parameter] public EventCallback<Guid> OnDeleteComment { get; set; }

    /// <summary>Comment items to display, provided by the parent component.</summary>
    [Parameter] public IReadOnlyList<FileCommentViewModel>? InitialComments { get; set; }

    private List<FileCommentViewModel> _comments = [];
    private bool _isLoading;
    private bool _isSubmitting;
    private string _newCommentText = string.Empty;
    private string _replyText = string.Empty;
    private string _editCommentText = string.Empty;
    private Guid? _replyingToId;
    private Guid? _editingCommentId;
    private readonly HashSet<Guid> _expandedComments = [];

    /// <summary>Top-level comments (no parent), newest first.</summary>
    protected IReadOnlyList<FileCommentViewModel> TopLevelComments =>
        _comments.Where(c => c.ParentCommentId is null)
                 .OrderByDescending(c => c.CreatedAt)
                 .ToList();

    /// <summary>Whether comment data is being fetched.</summary>
    protected bool IsLoading => _isLoading;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (InitialComments is not null)
        {
            _comments = [.. InitialComments];
            _isLoading = false;

            // Auto-expand replies for comments that have them
            foreach (var comment in _comments.Where(c => c.ParentCommentId is null && c.ReplyCount > 0))
            {
                _expandedComments.Add(comment.Id);
            }
        }
    }

    /// <summary>Gets replies to a given comment, ordered oldest first.</summary>
    protected IReadOnlyList<FileCommentViewModel> GetReplies(Guid parentId) =>
        _comments.Where(c => c.ParentCommentId == parentId)
                 .OrderBy(c => c.CreatedAt)
                 .ToList();

    /// <summary>Whether replies for a comment are expanded.</summary>
    protected bool IsExpanded(Guid commentId) => _expandedComments.Contains(commentId);

    /// <summary>Toggles the expanded state for a comment's replies.</summary>
    protected void ToggleReplies(Guid commentId)
    {
        if (!_expandedComments.Remove(commentId))
            _expandedComments.Add(commentId);
    }

    /// <summary>Whether the current user can edit or delete a comment.</summary>
    protected bool CanEditOrDelete(FileCommentViewModel comment) =>
        comment.CreatedByUserId == CurrentUserId;

    /// <summary>Submits a new top-level comment.</summary>
    protected async Task SubmitNewComment()
    {
        if (string.IsNullOrWhiteSpace(_newCommentText) || _isSubmitting) return;

        _isSubmitting = true;
        StateHasChanged();

        try
        {
            await OnAddComment.InvokeAsync(_newCommentText.Trim());
            _newCommentText = string.Empty;
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>Starts replying to a comment.</summary>
    protected void StartReply(FileCommentViewModel comment)
    {
        _replyingToId = comment.Id;
        _replyText = string.Empty;
        _editingCommentId = null;
    }

    /// <summary>Cancels the reply form.</summary>
    protected void CancelReply()
    {
        _replyingToId = null;
        _replyText = string.Empty;
    }

    /// <summary>Submits a reply to a comment.</summary>
    protected async Task SubmitReply()
    {
        if (_replyingToId is null || string.IsNullOrWhiteSpace(_replyText) || _isSubmitting) return;

        _isSubmitting = true;
        StateHasChanged();

        try
        {
            await OnReplyComment.InvokeAsync((_replyingToId.Value, _replyText.Trim()));
            _replyingToId = null;
            _replyText = string.Empty;
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>Starts editing a comment.</summary>
    protected void StartEdit(FileCommentViewModel comment)
    {
        _editingCommentId = comment.Id;
        _editCommentText = comment.Content;
        _replyingToId = null;
    }

    /// <summary>Cancels editing.</summary>
    protected void CancelEdit()
    {
        _editingCommentId = null;
        _editCommentText = string.Empty;
    }

    /// <summary>Saves the edited comment.</summary>
    protected async Task SaveEdit()
    {
        if (_editingCommentId is null || string.IsNullOrWhiteSpace(_editCommentText) || _isSubmitting) return;

        _isSubmitting = true;
        StateHasChanged();

        try
        {
            await OnEditComment.InvokeAsync((_editingCommentId.Value, _editCommentText.Trim()));
            _editingCommentId = null;
            _editCommentText = string.Empty;
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>Deletes a comment.</summary>
    protected async Task DeleteComment(FileCommentViewModel comment)
    {
        _isSubmitting = true;
        StateHasChanged();

        try
        {
            await OnDeleteComment.InvokeAsync(comment.Id);
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>Handles Ctrl+Enter to submit in the new comment textarea.</summary>
    protected async Task HandleNewCommentKeyDown(KeyboardEventArgs e)
    {
        if (e is { Key: "Enter", CtrlKey: true })
            await SubmitNewComment();
    }

    /// <summary>Handles Ctrl+Enter to submit reply, Escape to cancel.</summary>
    protected async Task HandleReplyKeyDown(KeyboardEventArgs e)
    {
        if (e is { Key: "Enter", CtrlKey: true })
            await SubmitReply();
        else if (e.Key == "Escape")
            CancelReply();
    }

    /// <summary>Handles Ctrl+Enter to save edit, Escape to cancel.</summary>
    protected async Task HandleEditKeyDown(KeyboardEventArgs e)
    {
        if (e is { Key: "Enter", CtrlKey: true })
            await SaveEdit();
        else if (e.Key == "Escape")
            CancelEdit();
    }

    /// <summary>Formats a DateTime as a human-readable relative time string.</summary>
    protected static string FormatRelativeTime(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;

        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";

        return utcTime.ToString("MMM d, yyyy");
    }
}
