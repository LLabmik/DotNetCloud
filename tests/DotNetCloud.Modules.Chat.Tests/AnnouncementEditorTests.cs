using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="AnnouncementEditor"/> preview toggle and field population logic.
/// </summary>
[TestClass]
public class AnnouncementEditorTests
{
    /// <summary>
    /// Test accessor subclass that exposes protected state and methods.
    /// </summary>
    private sealed class TestableAnnouncementEditor : AnnouncementEditor
    {
        public bool TestIsSaveDisabled => IsSaveDisabled;
        public string TestTitle { get => Title; set => Title = value; }
        public string TestContent { get => Content; set => Content = value; }
        public string TestPriority => Priority;
        public void TestOnParametersSet() => OnParametersSet();
    }

    [TestMethod]
    public void WhenTitleAndContentEmptyThenIsSaveDisabledIsTrue()
    {
        var editor = new TestableAnnouncementEditor();

        Assert.IsTrue(editor.TestIsSaveDisabled);
    }

    [TestMethod]
    public void WhenTitleAndContentSetThenIsSaveDisabledIsFalse()
    {
        var editor = new TestableAnnouncementEditor();

        editor.TestTitle = "Release Notes";
        editor.TestContent = "Version 2.0 released.";

        Assert.IsFalse(editor.TestIsSaveDisabled);
    }

    [TestMethod]
    public void WhenEditingAnnouncementSetThenFieldsArePopulated()
    {
        var announcement = new AnnouncementViewModel
        {
            Id = Guid.NewGuid(),
            Title = "Release Notes",
            Content = "Version 2.0 released.",
            Priority = "Urgent",
            ExpiresAt = new DateTime(2026, 12, 31),
            RequiresAcknowledgement = true
        };
        var editor = new TestableAnnouncementEditor
        {
            IsEditing = true,
            EditingAnnouncement = announcement
        };

        editor.TestOnParametersSet();

        Assert.AreEqual("Release Notes", editor.TestTitle);
        Assert.AreEqual("Version 2.0 released.", editor.TestContent);
        Assert.AreEqual("Urgent", editor.TestPriority);
    }

    [TestMethod]
    public void WhenNotEditingThenFieldsAreReset()
    {
        var editor = new TestableAnnouncementEditor
        {
            IsEditing = true,
            EditingAnnouncement = new AnnouncementViewModel
            {
                Title = "Old Title",
                Content = "Old Content",
                Priority = "Urgent"
            }
        };
        editor.TestOnParametersSet();

        editor.IsEditing = false;
        editor.TestOnParametersSet();

        Assert.AreEqual(string.Empty, editor.TestTitle);
        Assert.AreEqual(string.Empty, editor.TestContent);
        Assert.AreEqual("Normal", editor.TestPriority);
    }
}
