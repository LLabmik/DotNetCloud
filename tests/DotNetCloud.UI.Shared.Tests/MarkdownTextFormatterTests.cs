using DotNetCloud.UI.Shared.Components.Editors;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Tests for <see cref="MarkdownTextFormatter"/> — the pure text edits behind the Markdown editor's
/// formatting toolbar. The behaviour under test is that a highlighted block is *formatted*: wrap
/// actions surround it, line actions prefix every highlighted line, and block actions insert after
/// it. Only a collapsed caret falls back to inserting a placeholder.
/// </summary>
[TestClass]
public sealed class MarkdownTextFormatterTests
{
    // ─── ToggleWrap (bold / italic / link / image / code) ───────────────

    [TestMethod]
    public void ToggleWrap_TextHighlighted_WrapsHighlightedText()
    {
        var result = MarkdownTextFormatter.ToggleWrap("hello world", new MarkdownTextRange(0, 5), "**", "**", "bold text");

        Assert.AreEqual("**hello** world", result.Value);
    }

    [TestMethod]
    public void ToggleWrap_TextHighlighted_KeepsTheHighlightedTextIntact()
    {
        var result = MarkdownTextFormatter.ToggleWrap("hello world", new MarkdownTextRange(0, 5), "**", "**", "bold text");

        StringAssert.Contains(result.Value, "**hello**");
        Assert.IsFalse(result.Value.Contains("bold text", StringComparison.Ordinal), "The highlighted text must not be replaced by the placeholder.");
    }

    /// <summary>
    /// The highlight must survive the edit without covering the markers, otherwise the next action
    /// wraps the markers too and the text ends up as <c>****text****</c>.
    /// </summary>
    [TestMethod]
    public void ToggleWrap_TextHighlighted_KeepsTheTextSelectedWithoutTheMarkers()
    {
        var result = MarkdownTextFormatter.ToggleWrap("hello world", new MarkdownTextRange(0, 5), "**", "**", "bold text");

        Assert.AreEqual(new MarkdownTextRange(2, 7), result.Selection);
        Assert.AreEqual("hello", result.Value[result.Selection.Start..result.Selection.End]);
    }

    [TestMethod]
    public void ToggleWrap_MidTextHighlight_LeavesSurroundingTextUntouched()
    {
        var result = MarkdownTextFormatter.ToggleWrap("the quick fox", new MarkdownTextRange(4, 9), "*", "*", "italic text");

        Assert.AreEqual("the *quick* fox", result.Value);
    }

    [TestMethod]
    public void ToggleWrap_NothingHighlighted_InsertsPlaceholderBetweenTheMarkers()
    {
        var result = MarkdownTextFormatter.ToggleWrap(string.Empty, new MarkdownTextRange(0, 0), "**", "**", "bold text");

        Assert.AreEqual("**bold text**", result.Value);
        Assert.AreEqual("bold text", result.Value[result.Selection.Start..result.Selection.End]);
    }

    [TestMethod]
    public void ToggleWrap_NothingHighlighted_SelectsThePlaceholderSoTypingReplacesIt()
    {
        var result = MarkdownTextFormatter.ToggleWrap("abc", new MarkdownTextRange(3, 3), "![", "](image-url)", "alt text");

        Assert.AreEqual("abc![alt text](image-url)", result.Value);
        Assert.AreEqual("alt text", result.Value[result.Selection.Start..result.Selection.End]);
    }

    [TestMethod]
    public void ToggleWrap_StaleSelection_ClampsInsteadOfThrowing()
    {
        var result = MarkdownTextFormatter.ToggleWrap("ab", new MarkdownTextRange(0, 99), "**", "**", "bold text");

        Assert.AreEqual("**ab**", result.Value);
    }

    // ─── ToggleWrap when the text is already formatted ──────────────────

    /// <summary>
    /// The reported bug: Bold, Italic, then Bold and Italic again stacked asterisks
    /// (<c>*******text*******</c>) instead of turning the formatting back off. Strikethrough had the
    /// same problem with tildes.
    /// </summary>
    [TestMethod]
    public void ToggleWrap_BoldThenItalicThenBoldThenItalic_ReturnsToPlainText()
    {
        var state = new MarkdownEditorState { Value = "Hi there!" };

        state = Apply(state, "**", "**");
        Assert.AreEqual("**Hi there!**", state.Value, "Bold must wrap the highlighted text once.");

        state = Apply(state, "*", "*");
        Assert.AreEqual("***Hi there!***", state.Value, "Italic must add emphasis, not eat a bold marker.");

        state = Apply(state, "**", "**");
        Assert.AreEqual("*Hi there!*", state.Value, "Bold must be removed and italic kept.");

        state = Apply(state, "*", "*");
        Assert.AreEqual("Hi there!", state.Value, "Italic must be removed, leaving plain text.");
    }

    [TestMethod]
    public void ToggleWrap_BoldAppliedTwice_RemovesTheMarkers()
    {
        var state = new MarkdownEditorState { Value = "text" };

        state = Apply(state, "**", "**");
        state = Apply(state, "**", "**");

        Assert.AreEqual("text", state.Value);
        Assert.AreEqual(new MarkdownTextRange(0, 4), state.Selection);
    }

    [TestMethod]
    public void ToggleWrap_ItalicAppliedTwiceOnBoldText_KeepsTheBoldMarkers()
    {
        var state = new MarkdownEditorState { Value = "text" };

        state = Apply(state, "**", "**");
        state = Apply(state, "*", "*");
        state = Apply(state, "*", "*");

        Assert.AreEqual("**text**", state.Value);
    }

    [TestMethod]
    public void ToggleWrap_StrikethroughAppliedTwice_RemovesTheMarkers()
    {
        var state = new MarkdownEditorState { Value = "text" };

        state = Apply(state, "~~", "~~");
        Assert.AreEqual("~~text~~", state.Value);

        state = Apply(state, "~~", "~~");

        Assert.AreEqual("text", state.Value);
        Assert.AreEqual(new MarkdownTextRange(0, 4), state.Selection);
    }

    [TestMethod]
    public void ToggleWrap_StrikethroughThenBoldThenStrikethrough_KeepsTheBoldMarkers()
    {
        var state = new MarkdownEditorState { Value = "text" };

        state = Apply(state, "~~", "~~");
        state = Apply(state, "**", "**");
        state = Apply(state, "~~", "~~");

        Assert.AreEqual("**text**", state.Value, "Removing the strikethrough must leave the bold text behind.");
    }

    [TestMethod]
    public void ToggleWrap_LinkAppliedTwice_RemovesTheLinkSyntax()
    {
        var state = new MarkdownEditorState { Value = "text" };

        state = Apply(state, "[", "](https://example.com)");
        Assert.AreEqual("[text](https://example.com)", state.Value);

        state = Apply(state, "[", "](https://example.com)");

        Assert.AreEqual("text", state.Value);
    }

    [TestMethod]
    public void ToggleWrap_CodeBlockAppliedTwice_RemovesTheFence()
    {
        var state = new MarkdownEditorState { Value = "code" };

        state = Apply(state, "\n```\n", "\n```\n");
        Assert.AreEqual("\n```\ncode\n```\n", state.Value);

        state = Apply(state, "\n```\n", "\n```\n");

        Assert.AreEqual("code", state.Value);
    }

    [TestMethod]
    public void ToggleWrap_HighlightIncludesTheMarkers_RemovesThem()
    {
        var result = MarkdownTextFormatter.ToggleWrap("**text**", new MarkdownTextRange(0, 8), "**", "**", "bold text");

        Assert.AreEqual("text", result.Value);
        Assert.AreEqual(new MarkdownTextRange(0, 4), result.Selection);
    }

    [TestMethod]
    public void ToggleWrap_CaretInsideEmptyMarkers_RemovesThem()
    {
        var result = MarkdownTextFormatter.ToggleWrap("****", new MarkdownTextRange(2, 2), "**", "**", "bold text");

        Assert.AreEqual(string.Empty, result.Value);
    }

    [TestMethod]
    public void ToggleWrap_HighlightOnlyAsteriskWrapped_AddsBoldInsideTheItalic()
    {
        // "*text*" is italic, so Bold must nest inside it instead of stripping a single asterisk.
        var result = MarkdownTextFormatter.ToggleWrap("*text*", new MarkdownTextRange(1, 5), "**", "**", "bold text");

        Assert.AreEqual("***text***", result.Value);
    }

    /// <summary>
    /// Applies an action the way the component does, carrying the selection forward. A state built
    /// without a selection has the whole text highlighted, which is the case these sequences start
    /// from ("highlight the text, then press the buttons").
    /// </summary>
    private static MarkdownEditorState Apply(MarkdownEditorState state, string prefix, string suffix)
    {
        var selection = state.Selection.IsCollapsed && state.Selection.Start == 0
            ? new MarkdownTextRange(0, state.Value.Length)
            : state.Selection;

        var edit = MarkdownTextFormatter.ToggleWrap(state.Value, selection, prefix, suffix, $"{prefix}placeholder{suffix}");
        return new MarkdownEditorState
        {
            Value = edit.Value,
            SelectionStart = edit.Selection.Start,
            SelectionEnd = edit.Selection.End
        };
    }

    // ─── ToggleLinePrefix (bullet list / task list / blockquote) ────────

    [TestMethod]
    public void ToggleLinePrefix_SingleHighlightedLine_PrefixesTheLine()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple", new MarkdownTextRange(0, 5), "- ");

        Assert.AreEqual("- apple", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_MultipleHighlightedLines_PrefixesEveryLine()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple\nbanana", new MarkdownTextRange(0, 12), "- ");

        Assert.AreEqual("- apple\n- banana", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_PartialLineHighlight_PrefixesTheWholeLine()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple pie", new MarkdownTextRange(6, 9), "- ");

        Assert.AreEqual("- apple pie", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_CaretOnly_PrefixesTheCurrentLine()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("one\ntwo", new MarkdownTextRange(5, 5), "> ");

        Assert.AreEqual("one\n> two", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_EveryLineAlreadyPrefixed_RemovesThePrefix()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("- apple\n- banana", new MarkdownTextRange(0, 16), "- ");

        Assert.AreEqual("apple\nbanana", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_MixedState_OnlyPrefixesTheUnprefixedLines()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("- apple\nbanana", new MarkdownTextRange(0, 14), "- ");

        Assert.AreEqual("- apple\n- banana", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_BlankLineInSelection_IsLeftBlank()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple\n\nbanana", new MarkdownTextRange(0, 14), "- ");

        Assert.AreEqual("- apple\n\n- banana", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_SelectionEndsOnLineBreak_DoesNotTouchTheNextLine()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple\nbanana", new MarkdownTextRange(0, 6), "- ");

        Assert.AreEqual("- apple\nbanana", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_EmptyPrefix_ReturnsTheTextUnchanged()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple", new MarkdownTextRange(0, 5), string.Empty);

        Assert.AreEqual("apple", result.Value);
    }

    [TestMethod]
    public void ToggleLinePrefix_LinesEdited_SelectsTheRewrittenLines()
    {
        var result = MarkdownTextFormatter.ToggleLinePrefix("apple\nbanana", new MarkdownTextRange(0, 12), "- ");

        Assert.AreEqual(new MarkdownTextRange(0, 16), result.Selection);
    }

    // ─── NumberLines (ordered list) ────────────────────────────────────

    [TestMethod]
    public void NumberLines_MultipleHighlightedLines_NumbersEveryLineInOrder()
    {
        var result = MarkdownTextFormatter.NumberLines("apple\nbanana\ncherry", new MarkdownTextRange(0, 19));

        Assert.AreEqual("1. apple\n2. banana\n3. cherry", result.Value);
    }

    [TestMethod]
    public void NumberLines_CaretOnly_NumbersTheCurrentLine()
    {
        var result = MarkdownTextFormatter.NumberLines("one\ntwo", new MarkdownTextRange(5, 5));

        Assert.AreEqual("one\n1. two", result.Value);
    }

    [TestMethod]
    public void NumberLines_NumberedAndPlainLinesMixed_RenumbersThemInOrder()
    {
        var result = MarkdownTextFormatter.NumberLines("1. apple\nbanana", new MarkdownTextRange(0, 15));

        Assert.AreEqual("1. apple\n2. banana", result.Value);
    }

    [TestMethod]
    public void NumberLines_LineNumberedOutOfOrder_RenumbersItInPlace()
    {
        var result = MarkdownTextFormatter.NumberLines("apple\n7. banana", new MarkdownTextRange(0, 15));

        Assert.AreEqual("1. apple\n2. banana", result.Value);
    }

    [TestMethod]
    public void NumberLines_EveryLineAlreadyNumbered_RemovesTheNumbering()
    {
        var result = MarkdownTextFormatter.NumberLines("1. apple\n2. banana", new MarkdownTextRange(0, 18));

        Assert.AreEqual("apple\nbanana", result.Value);
    }

    [TestMethod]
    public void NumberLines_BlankLineInSelection_IsNotNumbered()
    {
        var result = MarkdownTextFormatter.NumberLines("apple\n\nbanana", new MarkdownTextRange(0, 14));

        Assert.AreEqual("1. apple\n\n2. banana", result.Value);
    }

    // ─── SetHeading ────────────────────────────────────────────────────

    [TestMethod]
    public void SetHeading_PlainLine_AddsTheHeadingMarker()
    {
        var result = MarkdownTextFormatter.SetHeading("Title", new MarkdownTextRange(0, 5), 2);

        Assert.AreEqual("## Title", result.Value);
    }

    [TestMethod]
    public void SetHeading_MultipleHighlightedLines_HeadingsEveryLine()
    {
        var result = MarkdownTextFormatter.SetHeading("apple\nbanana", new MarkdownTextRange(0, 12), 1);

        Assert.AreEqual("# apple\n# banana", result.Value);
    }

    [TestMethod]
    public void SetHeading_LineAlreadyAtTheSameLevel_RemovesTheHeading()
    {
        var result = MarkdownTextFormatter.SetHeading("## Title", new MarkdownTextRange(0, 8), 2);

        Assert.AreEqual("Title", result.Value);
    }

    [TestMethod]
    public void SetHeading_LineAtADifferentLevel_ReplacesTheMarker()
    {
        var result = MarkdownTextFormatter.SetHeading("## Title", new MarkdownTextRange(0, 8), 3);

        Assert.AreEqual("### Title", result.Value);
    }

    [TestMethod]
    public void SetHeading_CaretOnly_HeadingsTheCurrentLine()
    {
        var result = MarkdownTextFormatter.SetHeading("one\ntwo", new MarkdownTextRange(5, 5), 2);

        Assert.AreEqual("one\n## two", result.Value);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void SetHeading_LevelOutsideOneToSix_Throws(int level)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => MarkdownTextFormatter.SetHeading("Title", new MarkdownTextRange(0, 5), level));
    }

    // ─── InsertBlock (table / horizontal rule) ─────────────────────────

    [TestMethod]
    public void InsertBlock_TextHighlighted_InsertsAfterTheSelectionInsteadOfOverIt()
    {
        var result = MarkdownTextFormatter.InsertBlock("apple", new MarkdownTextRange(0, 5), "\n---\n");

        Assert.AreEqual("apple\n---\n", result.Value);
    }

    [TestMethod]
    public void InsertBlock_TextHighlighted_KeepsTheHighlightedTextSelected()
    {
        var result = MarkdownTextFormatter.InsertBlock("apple", new MarkdownTextRange(0, 5), "\n---\n");

        Assert.AreEqual(new MarkdownTextRange(0, 5), result.Selection);
        Assert.AreEqual("apple", result.Value[result.Selection.Start..result.Selection.End]);
    }

    [TestMethod]
    public void InsertBlock_CaretOnly_InsertsAtTheCaretAndMovesPastTheBlock()
    {
        var result = MarkdownTextFormatter.InsertBlock("abc", new MarkdownTextRange(1, 1), "XY");

        Assert.AreEqual("aXYbc", result.Value);
        Assert.AreEqual(new MarkdownTextRange(3, 3), result.Selection);
    }

    // ─── Range clamping ────────────────────────────────────────────────

    [TestMethod]
    public void Clamp_RangePastTheEndOfTheText_ClampsToTheTextLength()
    {
        var clamped = new MarkdownTextRange(5, 99).Clamp(10);

        Assert.AreEqual(new MarkdownTextRange(5, 10), clamped);
    }

    [TestMethod]
    public void Clamp_EndBeforeStart_CollapsesOntoTheStart()
    {
        var clamped = new MarkdownTextRange(7, 2).Clamp(10);

        Assert.AreEqual(new MarkdownTextRange(7, 7), clamped);
        Assert.IsTrue(clamped.IsCollapsed);
    }

    [TestMethod]
    public void Clamp_NegativeRange_ClampsToZero()
    {
        var clamped = new MarkdownTextRange(-4, -1).Clamp(10);

        Assert.AreEqual(new MarkdownTextRange(0, 0), clamped);
    }
}
