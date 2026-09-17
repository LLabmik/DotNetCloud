using System.Text.RegularExpressions;

namespace DotNetCloud.UI.Shared.Components.Editors;

/// <summary>
/// A caret or selection range inside the editor text, expressed in UTF-16 code units.
/// </summary>
/// <param name="Start">Start of the range (inclusive).</param>
/// <param name="End">End of the range (exclusive); equal to <paramref name="Start"/> for a plain caret.</param>
public readonly record struct MarkdownTextRange(int Start, int End)
{
    /// <summary>Gets a value indicating whether the range is a plain caret with nothing highlighted.</summary>
    public bool IsCollapsed => Start == End;

    /// <summary>Gets the number of characters in the range.</summary>
    public int Length => End - Start;

    /// <summary>
    /// Clamps the range into <paramref name="length"/> characters, so a stale range reported by the
    /// browser can never throw or slice outside the text.
    /// </summary>
    /// <param name="length">Length of the text the range refers to.</param>
    /// <returns>The clamped range.</returns>
    public MarkdownTextRange Clamp(int length)
    {
        var max = Math.Max(0, length);
        var start = Math.Clamp(Start, 0, max);
        var end = Math.Clamp(End, start, max);
        return new MarkdownTextRange(start, end);
    }
}

/// <summary>
/// The text produced by a toolbar action, together with the selection the editor should restore
/// once the new text has been written back.
/// </summary>
/// <param name="Value">The full new text of the editor.</param>
/// <param name="Selection">The selection to restore after the edit.</param>
public readonly record struct MarkdownEditorEdit(string Value, MarkdownTextRange Selection);

/// <summary>
/// The pure text edits behind the Markdown editor's formatting toolbar.
/// <para>
/// Every action formats what the user has highlighted instead of replacing it: wrapping actions
/// (bold, italic, link…) surround the selection, line actions (lists, headings, blockquote…)
/// prefix each highlighted line, and block actions (table, rule…) insert after the selection. Only
/// a collapsed caret falls back to inserting a placeholder.
/// </para>
/// </summary>
public static class MarkdownTextFormatter
{
    /// <summary>Matches an existing Markdown heading marker at the start of a line.</summary>
    private static readonly Regex HeadingPattern = new(@"^[ \t]*#{1,6}[ \t]+", RegexOptions.Compiled);

    /// <summary>Matches the marker the list action writes, so numbering can be re-applied or removed.</summary>
    private static readonly Regex OrderedListPattern = new(@"^[ \t]*\d+\.[ \t]+", RegexOptions.Compiled);

    /// <summary>
    /// Wraps the highlighted text with <paramref name="prefix"/>/<paramref name="suffix"/>, or
    /// removes those markers again when the highlight (or the caret) is already wrapped by them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The actions toggle, so <em>Bold → Italic → Bold → Italic</em> walks back to plain text instead
    /// of stacking markers (<c>*******text*******</c>). After an action the text stays highlighted
    /// without its markers, so the next action applies to the text and not to the syntax.
    /// </para>
    /// <para>
    /// For markers made of one repeated character (<c>*</c>, <c>**</c>, <c>~~</c>) the length of the
    /// marker run says which formats are on — one asterisk is italic, two are bold, three are both —
    /// so Italic on <c>**bold**</c> adds emphasis instead of eating a bold marker, while Bold on
    /// <c>***both***</c> removes only the bold pair. Runs of another marker nested between the
    /// selection and the pair are skipped, so the strikethrough pair wrapping
    /// <c>~~**text**~~</c> is still found by the strikethrough action.
    /// </para>
    /// </remarks>
    /// <param name="value">The current text.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="prefix">Markdown opening marker (for example <c>**</c>).</param>
    /// <param name="suffix">Markdown closing marker.</param>
    /// <param name="placeholder">Text used when nothing is highlighted; it is inserted between the markers and left selected.</param>
    /// <returns>The formatted text and the selection to restore.</returns>
    public static MarkdownEditorEdit ToggleWrap(
        string value,
        MarkdownTextRange selection,
        string prefix,
        string suffix,
        string placeholder)
    {
        value ??= string.Empty;
        prefix ??= string.Empty;
        suffix ??= string.Empty;
        placeholder ??= string.Empty;

        var range = selection.Clamp(value.Length);
        if (prefix.Length == 0 && suffix.Length == 0)
        {
            return new MarkdownEditorEdit(value, range);
        }

        var uniformMarker = prefix == suffix && IsUniformRun(prefix);
        var marker = uniformMarker ? prefix[0] : default;

        // The highlighted text carries the markers itself (the user selected "**bold**").
        if (!range.IsCollapsed)
        {
            var strip = uniformMarker
                ? SelectedMarkerStrip(value, range, marker, prefix.Length)
                : value.AsSpan(range.Start, range.Length).StartsWith(prefix, StringComparison.Ordinal)
                    && value.AsSpan(range.Start, range.Length).EndsWith(suffix, StringComparison.Ordinal)
                    && range.Length >= prefix.Length + suffix.Length
                        ? prefix.Length
                        : 0;

            if (strip > 0)
            {
                var inner = value[(range.Start + strip)..(range.End - strip)];
                return new MarkdownEditorEdit(
                    string.Concat(value.AsSpan(0, range.Start), inner, value.AsSpan(range.End)),
                    new MarkdownTextRange(range.Start, range.Start + inner.Length));
            }
        }

        // The highlight (or the caret) already sits inside a matching pair of markers.
        if (uniformMarker)
        {
            if (TryFindWrappingRuns(value, range, marker, prefix.Length, out var beforeRunEnd, out var afterRunStart))
            {
                var kept = range.Length;
                var start = range.Start - prefix.Length;

                // Drop the marker run on each side and keep everything in between, including any
                // marker pair nested closer to the selection.
                var before = string.Concat(
                    value.AsSpan(0, beforeRunEnd - prefix.Length),
                    value.AsSpan(beforeRunEnd, range.Start - beforeRunEnd));
                var after = string.Concat(
                    value.AsSpan(range.End, afterRunStart - range.End),
                    value.AsSpan(afterRunStart + prefix.Length));
                var unwrapped = string.Concat(before, value.AsSpan(range.Start, kept), after);

                return new MarkdownEditorEdit(unwrapped, new MarkdownTextRange(start, start + kept));
            }
        }
        else if (value.AsSpan(0, range.Start).EndsWith(prefix, StringComparison.Ordinal)
            && value.AsSpan(range.End).StartsWith(suffix, StringComparison.Ordinal))
        {
            var kept = range.Length;
            var start = range.Start - prefix.Length;
            return new MarkdownEditorEdit(
                string.Concat(
                    value.AsSpan(0, start),
                    value.AsSpan(range.Start, kept),
                    value.AsSpan(range.End + suffix.Length)),
                new MarkdownTextRange(start, start + kept));
        }

        // Nothing to unwrap: add the markers, or the placeholder when there is nothing highlighted.
        if (range.IsCollapsed)
        {
            var inserted = prefix + placeholder + suffix;
            var innerStart = range.Start + prefix.Length;
            return new MarkdownEditorEdit(
                string.Concat(value.AsSpan(0, range.Start), inserted, value.AsSpan(range.Start)),
                new MarkdownTextRange(innerStart, innerStart + placeholder.Length));
        }

        var wrapped = prefix + value[range.Start..range.End] + suffix;
        return new MarkdownEditorEdit(
            string.Concat(value.AsSpan(0, range.Start), wrapped, value.AsSpan(range.End)),
            new MarkdownTextRange(range.Start + prefix.Length, range.End + prefix.Length));
    }

    /// <summary>
    /// Returns how many marker characters to strip from each side when the highlighted text is itself
    /// wrapped (the user highlighted <c>**bold**</c>), or zero when it is not wrapped by this marker.
    /// </summary>
    private static int SelectedMarkerStrip(string value, MarkdownTextRange range, char marker, int length)
    {
        var leading = 0;
        while (range.Start + leading < range.End && value[range.Start + leading] == marker)
        {
            leading++;
        }

        var trailing = 0;
        while (range.End - 1 - trailing >= range.Start + leading && value[range.End - 1 - trailing] == marker)
        {
            trailing++;
        }

        return RunCoversFormat(leading, length) && RunCoversFormat(trailing, length) ? length : 0;
    }

    /// <summary>
    /// Finds the innermost pair of <paramref name="marker"/> runs wrapping the range, skipping runs
    /// that belong to a nested pair of another marker (<c>~~**text**~~</c>).
    /// </summary>
    /// <param name="value">The current text.</param>
    /// <param name="range">The range to wrap, which may be a collapsed caret.</param>
    /// <param name="marker">The repeated marker character.</param>
    /// <param name="length">Marker length, for example 2 for <c>**</c>.</param>
    /// <param name="beforeRunEnd">End of the run before the range.</param>
    /// <param name="afterRunStart">Start of the run after the range.</param>
    /// <returns><see langword="true"/> when a matching pair exists.</returns>
    private static bool TryFindWrappingRuns(
        string value,
        MarkdownTextRange range,
        char marker,
        int length,
        out int beforeRunEnd,
        out int afterRunStart)
    {
        beforeRunEnd = -1;
        afterRunStart = -1;

        for (var i = range.Start - 1; i >= 0;)
        {
            if (value[i] != marker)
            {
                i--;
                continue;
            }

            var end = i + 1;
            var start = i;
            while (start > 0 && value[start - 1] == marker)
            {
                start--;
            }

            var run = end - start;
            if (RunCoversFormat(run, length) && IsMarkerGap(value, end, range.Start))
            {
                beforeRunEnd = end;
                break;
            }

            // A run of the same character nested closer to the selection is part of the gap.
            i = start - 1;
        }

        if (beforeRunEnd < 0)
        {
            return false;
        }

        for (var i = range.End; i < value.Length;)
        {
            if (value[i] != marker)
            {
                i++;
                continue;
            }

            var start = i;
            var end = i;
            while (end < value.Length && value[end] == marker)
            {
                end++;
            }

            if (RunCoversFormat(end - start, length) && IsMarkerGap(value, range.End, start))
            {
                afterRunStart = start;
                break;
            }

            i = end;
        }

        return afterRunStart >= 0;
    }

    /// <summary>
    /// Determines whether a marker run covers this format: the run must be long enough and — for a
    /// single-character marker — must not be an even one, because an even run of asterisks is bold
    /// rather than italic.
    /// </summary>
    private static bool RunCoversFormat(int run, int length)
        => run >= length && !(length == 1 && run % 2 == 0);

    /// <summary>
    /// Determines whether everything between a marker run and the selection is itself a marker, which
    /// is what makes the run a wrapping pair rather than two stray characters in prose (an asterisk
    /// used for multiplication, for instance).
    /// </summary>
    private static bool IsMarkerGap(string value, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            var c = value[i];
            if (c != '*' && c != '_' && c != '~' && c != '`')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every character of <paramref name="markers"/> is the same one.</summary>
    private static bool IsUniformRun(string markers)
        => markers.Length > 0 && markers.All(c => c == markers[0]);

    /// <summary>
    /// Adds <paramref name="prefix"/> to every highlighted line (or to the caret's line when
    /// nothing is highlighted), or removes it again when every line already carries it.
    /// </summary>
    /// <param name="value">The current text.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="prefix">Line prefix, for example <c>- </c> or <c>&gt; </c>.</param>
    /// <returns>The formatted text and the selection covering the edited lines.</returns>
    public static MarkdownEditorEdit ToggleLinePrefix(string value, MarkdownTextRange selection, string prefix)
    {
        value ??= string.Empty;
        prefix ??= string.Empty;

        var range = selection.Clamp(value.Length);
        if (prefix.Length == 0)
        {
            return new MarkdownEditorEdit(value, range);
        }

        var (block, lines) = SelectLines(value, range);
        var remove = lines.All(line => line.StartsWith(prefix, StringComparison.Ordinal));

        var updated = new string[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasPrefix = line.StartsWith(prefix, StringComparison.Ordinal);

            updated[i] = remove
                ? hasPrefix ? line[prefix.Length..] : line
                : hasPrefix || line.Length == 0 ? line : prefix + line;
        }

        return ReplaceBlock(value, block, string.Join('\n', updated));
    }

    /// <summary>
    /// Numbers every highlighted line (or the caret's line when nothing is highlighted), or removes
    /// the numbering again when every line is already numbered.
    /// </summary>
    /// <param name="value">The current text.</param>
    /// <param name="selection">The current selection.</param>
    /// <returns>The formatted text and the selection covering the edited lines.</returns>
    public static MarkdownEditorEdit NumberLines(string value, MarkdownTextRange selection)
    {
        value ??= string.Empty;
        var range = selection.Clamp(value.Length);
        var (block, lines) = SelectLines(value, range);

        var remove = lines.All(line => line.Length == 0 || OrderedListPattern.IsMatch(line));
        var updated = new string[lines.Length];
        var counter = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (remove)
            {
                updated[i] = OrderedListPattern.Replace(line, string.Empty);
                continue;
            }

            if (line.Length == 0)
            {
                updated[i] = line;
                continue;
            }

            counter++;
            updated[i] = OrderedListPattern.IsMatch(line)
                ? OrderedListPattern.Replace(line, counter + ". ")
                : counter + ". " + line;
        }

        return ReplaceBlock(value, block, string.Join('\n', updated));
    }

    /// <summary>
    /// Makes every highlighted line (or the caret's line when nothing is highlighted) a heading of
    /// the given level, or clears the heading when the lines are already at that level.
    /// </summary>
    /// <param name="value">The current text.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="level">Heading level, 1–6.</param>
    /// <returns>The formatted text and the selection covering the edited lines.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is outside 1–6.</exception>
    public static MarkdownEditorEdit SetHeading(string value, MarkdownTextRange selection, int level)
    {
        if (level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Heading level must be between 1 and 6.");
        }

        value ??= string.Empty;
        var range = selection.Clamp(value.Length);
        var (block, lines) = SelectLines(value, range);

        var marker = new string('#', level) + " ";
        var levelPattern = new Regex($@"^[ \t]*#{{{level}}}[ \t]+", RegexOptions.Compiled);
        var remove = lines.All(line => line.Length == 0 || levelPattern.IsMatch(line));

        var updated = new string[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var stripped = HeadingPattern.Replace(line, string.Empty);

            updated[i] = remove || line.Length == 0
                ? stripped
                : marker + stripped;
        }

        return ReplaceBlock(value, block, string.Join('\n', updated));
    }

    /// <summary>
    /// Inserts a block of Markdown (table, horizontal rule…) at the caret. When text is highlighted
    /// the block is inserted after it and the highlight is kept, so the selection is never replaced.
    /// </summary>
    /// <param name="value">The current text.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="block">The block to insert.</param>
    /// <returns>The new text and the selection to restore.</returns>
    public static MarkdownEditorEdit InsertBlock(string value, MarkdownTextRange selection, string block)
    {
        value ??= string.Empty;
        block ??= string.Empty;

        var range = selection.Clamp(value.Length);
        var insertAt = range.IsCollapsed ? range.Start : range.End;

        var newValue = string.Concat(value.AsSpan(0, insertAt), block, value.AsSpan(insertAt));
        var caret = range.IsCollapsed ? insertAt + block.Length : range.Start;
        var caretEnd = range.IsCollapsed ? caret : range.End;

        return new MarkdownEditorEdit(newValue, new MarkdownTextRange(caret, caretEnd));
    }

    /// <summary>
    /// Expands a range to the whole lines it touches: from the start of the first touched line to
    /// the end of the last one (excluding the line break).
    /// </summary>
    private static (MarkdownTextRange Block, string[] Lines) SelectLines(string value, MarkdownTextRange range)
    {
        var lineStart = range.Start == 0 ? 0 : value.LastIndexOf('\n', range.Start - 1) + 1;

        int lineEnd;
        if (!range.IsCollapsed && range.End > lineStart && value[range.End - 1] == '\n')
        {
            // The selection ends right after a line break, so the line below it is not selected.
            lineEnd = range.End - 1;
        }
        else
        {
            lineEnd = range.End >= value.Length ? value.Length : value.IndexOf('\n', range.End);
            if (lineEnd < 0)
            {
                lineEnd = value.Length;
            }
        }

        var block = new MarkdownTextRange(lineStart, lineEnd);
        return (block, value[block.Start..block.End].Split('\n'));
    }

    /// <summary>Replaces a range with new text and selects the replacement.</summary>
    private static MarkdownEditorEdit ReplaceBlock(string value, MarkdownTextRange block, string replacement)
        => new(
            string.Concat(value.AsSpan(0, block.Start), replacement, value.AsSpan(block.End)),
            new MarkdownTextRange(block.Start, block.Start + replacement.Length));
}
