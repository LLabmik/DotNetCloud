// Markdown editor interop (window.dotnetcloudMarkdownEditor).
//
// The component (DotNetCloud.UI.Shared/Components/Editors/MarkdownEditor.razor) owns the
// formatting rules; this file only reads the textarea's text + selection and writes the
// formatted result back, so the text edits themselves stay unit-testable in C#.
window.dotnetcloudMarkdownEditor = (function () {
  "use strict";

  // Selection snapshot taken when the textarea loses focus. Clicking a toolbar button blurs the
  // textarea, and some browsers drop the live selection at that point — without the snapshot a
  // highlighted block would look unselected and the toolbar would insert its placeholder instead
  // of formatting the text the user highlighted.
  var SAVED_SELECTION = "__dncMarkdownSelection";
  var EDITOR_TEXTAREA_CLASS = "editor-textarea";

  function isEditorTextarea(element) {
    return (
      !!element &&
      element.tagName === "TEXTAREA" &&
      typeof element.classList !== "undefined" &&
      element.classList.contains(EDITOR_TEXTAREA_CLASS)
    );
  }

  // Capture phase: blur does not bubble, so listen on the document with capture enabled.
  document.addEventListener(
    "blur",
    function (event) {
      var element = event.target;
      if (!isEditorTextarea(element)) {
        return;
      }

      element[SAVED_SELECTION] = {
        start: element.selectionStart,
        end: element.selectionEnd,
      };
    },
    true,
  );

  function setSelection(textareaElement, start, end) {
    if (!textareaElement || !document.contains(textareaElement)) {
      return;
    }

    try {
      textareaElement.focus();
      textareaElement.setSelectionRange(start, end);
    } catch (error) {
      // Detached or unfocusable element — the text was still updated, so ignore.
    }
  }

  return {
    /**
     * Current text and selection of the editor textarea.
     * Uses the selection remembered on blur when the live one has collapsed, so a
     * highlighted block survives the toolbar button's focus change.
     * Returns null when the element is gone (stale Blazor element reference).
     */
    readState: function (textareaElement) {
      if (!isEditorTextarea(textareaElement)) {
        return null;
      }

      var value =
        typeof textareaElement.value === "string" ? textareaElement.value : "";
      var start = textareaElement.selectionStart;
      var end = textareaElement.selectionEnd;

      if (typeof start !== "number" || typeof end !== "number") {
        start = value.length;
        end = value.length;
      }

      var saved = textareaElement[SAVED_SELECTION];
      if (
        start === end &&
        saved &&
        saved.end > saved.start &&
        saved.end <= value.length
      ) {
        start = saved.start;
        end = saved.end;
      }

      return { value: value, selectionStart: start, selectionEnd: end };
    },

    /**
     * Writes the formatted text back into the textarea and restores the selection.
     * Blazor re-renders the bound `value` right after this returns (which collapses the caret
     * to the end of the text), so the selection is applied twice: now, and once the render
     * batch has been flushed.
     */
    writeState: function (
      textareaElement,
      value,
      selectionStart,
      selectionEnd,
    ) {
      if (!isEditorTextarea(textareaElement)) {
        return;
      }

      textareaElement.value =
        value === undefined || value === null ? "" : value;
      textareaElement[SAVED_SELECTION] = null;

      setSelection(textareaElement, selectionStart, selectionEnd);
      setTimeout(function () {
        setSelection(textareaElement, selectionStart, selectionEnd);
      }, 0);
    },
  };
})();
