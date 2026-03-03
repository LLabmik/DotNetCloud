using DotNetCloud.Modules.Example.Models;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="ExampleNote"/> record model.
/// </summary>
[TestClass]
public class ExampleNoteTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var note = new ExampleNote { Title = "Test" };

        Assert.AreNotEqual(Guid.Empty, note.Id);
    }

    [TestMethod]
    public void WhenCreatedThenEachInstanceHasUniqueId()
    {
        var note1 = new ExampleNote { Title = "Note 1" };
        var note2 = new ExampleNote { Title = "Note 2" };

        Assert.AreNotEqual(note1.Id, note2.Id);
    }

    [TestMethod]
    public void WhenCreatedThenTitleIsSet()
    {
        var note = new ExampleNote { Title = "My Title" };

        Assert.AreEqual("My Title", note.Title);
    }

    [TestMethod]
    public void WhenCreatedThenContentDefaultsToEmpty()
    {
        var note = new ExampleNote { Title = "Test" };

        Assert.AreEqual(string.Empty, note.Content);
    }

    [TestMethod]
    public void WhenCreatedWithContentThenContentIsSet()
    {
        var note = new ExampleNote { Title = "Test", Content = "Body text" };

        Assert.AreEqual("Body text", note.Content);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow;
        var note = new ExampleNote { Title = "Test" };
        var after = DateTime.UtcNow;

        Assert.IsTrue(note.CreatedAt >= before && note.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenUpdatedAtIsNull()
    {
        var note = new ExampleNote { Title = "Test" };

        Assert.IsNull(note.UpdatedAt);
    }

    [TestMethod]
    public void WhenCreatedWithUserIdThenCreatedByUserIdIsSet()
    {
        var userId = Guid.NewGuid();
        var note = new ExampleNote { Title = "Test", CreatedByUserId = userId };

        Assert.AreEqual(userId, note.CreatedByUserId);
    }

    [TestMethod]
    public void WhenCreatedThenIsRecord()
    {
        var note1 = new ExampleNote { Title = "Test", Content = "Body" };
        var note2 = note1 with { Title = "Updated" };

        Assert.AreEqual("Test", note1.Title);
        Assert.AreEqual("Updated", note2.Title);
        Assert.AreEqual(note1.Id, note2.Id);
        Assert.AreEqual(note1.Content, note2.Content);
    }

    [TestMethod]
    public void WhenCreatedWithUpdatedAtThenValueIsPreserved()
    {
        var updatedAt = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var note = new ExampleNote { Title = "Test", UpdatedAt = updatedAt };

        Assert.AreEqual(updatedAt, note.UpdatedAt);
    }
}
