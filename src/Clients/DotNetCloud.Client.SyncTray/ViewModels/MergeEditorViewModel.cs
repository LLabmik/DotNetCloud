using System.Collections.ObjectModel;
using System.Windows.Input;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DotNetCloud.Client.Core.Conflict;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the three-pane merge editor window.
/// Left pane: local version (read-only), Right pane: server version (read-only),
/// Bottom pane: merged result (editable).
/// </summary>
public sealed class MergeEditorViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly Action<string?>? _onCompleted;

    private string _localContent = string.Empty;
    private string _serverContent = string.Empty;
    private string _mergedContent = string.Empty;
    private string _filePath = string.Empty;
    private string _fileName = string.Empty;
    private bool _isXml;
    private bool _isBinary;
    private string _statusText = string.Empty;
    private int _localChangeCount;
    private int _serverChangeCount;
    private int _conflictCount;
    private bool _hasConflicts;

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Display-friendly file name.</summary>
    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    /// <summary>Full path being merged.</summary>
    public string FilePath
    {
        get => _filePath;
        private set => SetProperty(ref _filePath, value);
    }

    /// <summary>Merged result text (editable by user).</summary>
    public string MergedContent
    {
        get => _mergedContent;
        set => SetProperty(ref _mergedContent, value);
    }

    /// <summary>Status bar text.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Number of changes on the local side.</summary>
    public int LocalChangeCount
    {
        get => _localChangeCount;
        private set => SetProperty(ref _localChangeCount, value);
    }

    /// <summary>Number of changes on the server side.</summary>
    public int ServerChangeCount
    {
        get => _serverChangeCount;
        private set => SetProperty(ref _serverChangeCount, value);
    }

    /// <summary>Number of conflicting hunks.</summary>
    public int ConflictCount
    {
        get => _conflictCount;
        private set => SetProperty(ref _conflictCount, value);
    }

    /// <summary>Whether there are overlapping (conflicting) changes.</summary>
    public bool HasConflicts
    {
        get => _hasConflicts;
        private set => SetProperty(ref _hasConflicts, value);
    }

    /// <summary>Whether this is an XML file.</summary>
    public bool IsXml
    {
        get => _isXml;
        private set => SetProperty(ref _isXml, value);
    }

    /// <summary>Whether file is binary (merge not possible).</summary>
    public bool IsBinary
    {
        get => _isBinary;
        private set => SetProperty(ref _isBinary, value);
    }

    /// <summary>Lines for the left (local) diff pane.</summary>
    public ObservableCollection<DiffLineViewModel> LocalLines { get; } = [];

    /// <summary>Lines for the right (server) diff pane.</summary>
    public ObservableCollection<DiffLineViewModel> ServerLines { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Saves the merged content and closes the window.</summary>
    public ICommand SaveAndResolveCommand { get; }

    /// <summary>Accepts all local changes into the merge result.</summary>
    public ICommand AcceptAllLocalCommand { get; }

    /// <summary>Accepts all server changes into the merge result.</summary>
    public ICommand AcceptAllServerCommand { get; }

    /// <summary>Resets the merge result to the auto-merged content.</summary>
    public ICommand ResetMergeCommand { get; }

    /// <summary>Cancels without saving.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Raised when the window should close.</summary>
    public event EventHandler? CloseRequested;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new merge editor view-model.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="onCompleted">
    /// Callback invoked when the user completes or cancels.
    /// Receives the merged content string on save, or null on cancel.
    /// </param>
    public MergeEditorViewModel(ILogger logger, Action<string?>? onCompleted = null)
    {
        _logger = logger;
        _onCompleted = onCompleted;

        SaveAndResolveCommand = new AsyncRelayCommand(SaveAndResolveAsync);
        AcceptAllLocalCommand = new RelayCommand(AcceptAllLocal);
        AcceptAllServerCommand = new RelayCommand(AcceptAllServer);
        ResetMergeCommand = new RelayCommand(ResetMerge);
        CancelCommand = new RelayCommand(Cancel);
    }

    // ── Initialization ────────────────────────────────────────────────────

    /// <summary>
    /// Loads conflict file contents and computes diffs.
    /// </summary>
    /// <param name="originalPath">Path to the server (current) version of the file.</param>
    /// <param name="conflictCopyPath">Path to the local (conflict copy) version.</param>
    public void LoadConflict(string originalPath, string conflictCopyPath)
    {
        FilePath = originalPath;
        FileName = Path.GetFileName(originalPath);

        var mergeMode = FileTypeClassifier.GetMergeMode(originalPath);
        IsXml = mergeMode == FileMergeMode.Xml;
        IsBinary = mergeMode == FileMergeMode.Binary;

        if (IsBinary)
        {
            StatusText = "Binary file — merge not available. Use Keep Local / Keep Server.";
            return;
        }

        try
        {
            _serverContent = File.Exists(originalPath) ? File.ReadAllText(originalPath) : string.Empty;
            _localContent = File.Exists(conflictCopyPath) ? File.ReadAllText(conflictCopyPath) : string.Empty;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read conflict files for merge editor.");
            StatusText = $"Error reading files: {ex.Message}";
            return;
        }

        ComputeDiffs();
        ComputeInitialMerge();
    }

    // ── Diff computation ──────────────────────────────────────────────────

    private void ComputeDiffs()
    {
        LocalLines.Clear();
        ServerLines.Clear();

        var diffBuilder = new SideBySideDiffBuilder(new Differ());
        var model = diffBuilder.BuildDiffModel(_localContent, _serverContent, ignoreWhitespace: false);

        int localChanges = 0;
        int serverChanges = 0;

        for (int i = 0; i < Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count); i++)
        {
            var oldLine = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var newLine = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            LocalLines.Add(new DiffLineViewModel
            {
                LineNumber = oldLine?.Position,
                Text = oldLine?.Text ?? string.Empty,
                Type = MapChangeType(oldLine?.Type),
            });

            ServerLines.Add(new DiffLineViewModel
            {
                LineNumber = newLine?.Position,
                Text = newLine?.Text ?? string.Empty,
                Type = MapChangeType(newLine?.Type),
            });

            if (oldLine?.Type is ChangeType.Deleted or ChangeType.Modified or ChangeType.Inserted)
                localChanges++;
            if (newLine?.Type is ChangeType.Deleted or ChangeType.Modified or ChangeType.Inserted)
                serverChanges++;
        }

        LocalChangeCount = localChanges;
        ServerChangeCount = serverChanges;
        StatusText = $"{localChanges} local change(s), {serverChanges} server change(s)";
    }

    private void ComputeInitialMerge()
    {
        // Attempt auto-merge: apply non-overlapping changes from both sides.
        // We use a simple line-based approach: if only one side changed a line, use that version.
        // If both sides changed the same line, insert conflict markers.

        var diffBuilder = new SideBySideDiffBuilder(new Differ());
        var model = diffBuilder.BuildDiffModel(_localContent, _serverContent, ignoreWhitespace: false);

        var merged = new List<string>();
        int conflicts = 0;

        for (int i = 0; i < Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count); i++)
        {
            var localLine = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var serverLine = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            var localType = localLine?.Type ?? ChangeType.Imaginary;
            var serverType = serverLine?.Type ?? ChangeType.Imaginary;

            if (localType == ChangeType.Unchanged && serverType == ChangeType.Unchanged)
            {
                // Both sides agree.
                merged.Add(localLine?.Text ?? string.Empty);
            }
            else if (localType == ChangeType.Unchanged || localType == ChangeType.Imaginary)
            {
                // Only server changed — accept server.
                if (serverType != ChangeType.Deleted)
                    merged.Add(serverLine?.Text ?? string.Empty);
            }
            else if (serverType == ChangeType.Unchanged || serverType == ChangeType.Imaginary)
            {
                // Only local changed — accept local.
                if (localType != ChangeType.Deleted)
                    merged.Add(localLine?.Text ?? string.Empty);
            }
            else
            {
                // Both sides changed — conflict.
                conflicts++;
                merged.Add($"<<<<<<< LOCAL");
                if (localType != ChangeType.Deleted)
                    merged.Add(localLine?.Text ?? string.Empty);
                merged.Add("=======");
                if (serverType != ChangeType.Deleted)
                    merged.Add(serverLine?.Text ?? string.Empty);
                merged.Add(">>>>>>> SERVER");
            }
        }

        ConflictCount = conflicts;
        HasConflicts = conflicts > 0;
        MergedContent = string.Join(Environment.NewLine, merged);

        if (conflicts > 0)
            StatusText += $" — {conflicts} conflict(s) require manual resolution";
    }

    private static DiffLineType MapChangeType(ChangeType? type) => type switch
    {
        ChangeType.Unchanged => DiffLineType.Unchanged,
        ChangeType.Inserted => DiffLineType.Inserted,
        ChangeType.Deleted => DiffLineType.Deleted,
        ChangeType.Modified => DiffLineType.Modified,
        ChangeType.Imaginary => DiffLineType.Filler,
        _ => DiffLineType.Unchanged,
    };

    // ── Command handlers ──────────────────────────────────────────────────

    private Task SaveAndResolveAsync()
    {
        _onCompleted?.Invoke(MergedContent);
        CloseRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void AcceptAllLocal()
    {
        MergedContent = _localContent;
        StatusText = "Accepted all local changes.";
    }

    private void AcceptAllServer()
    {
        MergedContent = _serverContent;
        StatusText = "Accepted all server changes.";
    }

    private void ResetMerge()
    {
        ComputeInitialMerge();
    }

    private void Cancel()
    {
        _onCompleted?.Invoke(null);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
