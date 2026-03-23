namespace DotNetCloud.Core.Tests.DTOs;

using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Note DTOs.
/// </summary>
[TestClass]
public class NoteDtosTests
{
    [TestMethod]
    public void NoteDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var note = new NoteDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "My First Note",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, note.Id);
        Assert.AreEqual("My First Note", note.Title);
        Assert.AreEqual(NoteContentFormat.Markdown, note.Format);
        Assert.AreEqual(1, note.Version);
        Assert.IsFalse(note.IsPinned);
        Assert.IsFalse(note.IsFavorite);
        Assert.IsFalse(note.IsDeleted);
    }

    [TestMethod]
    public void NoteDto_Collections_DefaultToEmpty()
    {
        // Arrange & Act
        var note = new NoteDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, note.Tags.Count);
        Assert.AreEqual(0, note.Links.Count);
    }

    [TestMethod]
    public void NoteContentFormat_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(NoteContentFormat));

        // Assert
        Assert.AreEqual(2, values.Length);
    }

    [TestMethod]
    public void NoteLinkType_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(NoteLinkType));

        // Assert
        Assert.AreEqual(4, values.Length);
    }

    [TestMethod]
    public void NoteLinkDto_HasRequiredProperties()
    {
        // Arrange & Act
        var link = new NoteLinkDto
        {
            LinkType = NoteLinkType.CalendarEvent,
            TargetId = Guid.NewGuid(),
            DisplayLabel = "Team Meeting"
        };

        // Assert
        Assert.AreEqual(NoteLinkType.CalendarEvent, link.LinkType);
        Assert.AreNotEqual(Guid.Empty, link.TargetId);
        Assert.AreEqual("Team Meeting", link.DisplayLabel);
    }

    [TestMethod]
    public void NoteFolderDto_HasRequiredProperties()
    {
        // Arrange & Act
        var folder = new NoteFolderDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Name = "Work Notes",
            NoteCount = 12,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Work Notes", folder.Name);
        Assert.AreEqual(12, folder.NoteCount);
        Assert.IsNull(folder.ParentId);
        Assert.AreEqual(0, folder.SortOrder);
    }

    [TestMethod]
    public void NoteVersionDto_HasRequiredProperties()
    {
        // Arrange & Act
        var version = new NoteVersionDto
        {
            Id = Guid.NewGuid(),
            NoteId = Guid.NewGuid(),
            VersionNumber = 3,
            Title = "Updated Title",
            Content = "# Updated Content",
            EditedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(3, version.VersionNumber);
        Assert.AreEqual("Updated Title", version.Title);
        Assert.AreEqual("# Updated Content", version.Content);
    }

    [TestMethod]
    public void CreateNoteDto_HasRequiredProperties()
    {
        // Arrange & Act
        var dto = new CreateNoteDto
        {
            Title = "New Note",
            Content = "# Hello"
        };

        // Assert
        Assert.AreEqual("New Note", dto.Title);
        Assert.AreEqual("# Hello", dto.Content);
        Assert.AreEqual(NoteContentFormat.Markdown, dto.Format);
        Assert.IsNull(dto.FolderId);
        Assert.AreEqual(0, dto.Tags.Count);
        Assert.AreEqual(0, dto.Links.Count);
    }

    [TestMethod]
    public void UpdateNoteDto_AllFields_AreNullable()
    {
        // Arrange & Act
        var dto = new UpdateNoteDto();

        // Assert
        Assert.IsNull(dto.Title);
        Assert.IsNull(dto.Content);
        Assert.IsNull(dto.Format);
        Assert.IsNull(dto.IsPinned);
        Assert.IsNull(dto.IsFavorite);
        Assert.IsNull(dto.Tags);
        Assert.IsNull(dto.Links);
        Assert.IsNull(dto.ExpectedVersion);
    }

    [TestMethod]
    public void CreateNoteFolderDto_HasRequiredProperties()
    {
        // Arrange & Act
        var dto = new CreateNoteFolderDto { Name = "Archive" };

        // Assert
        Assert.AreEqual("Archive", dto.Name);
        Assert.IsNull(dto.ParentId);
        Assert.IsNull(dto.Color);
    }

    [TestMethod]
    public void UpdateNoteFolderDto_AllFields_AreNullable()
    {
        // Arrange & Act
        var dto = new UpdateNoteFolderDto();

        // Assert
        Assert.IsNull(dto.Name);
        Assert.IsNull(dto.ParentId);
        Assert.IsNull(dto.Color);
        Assert.IsNull(dto.SortOrder);
    }

    [TestMethod]
    public void NoteDto_IsImmutableRecord()
    {
        // Arrange
        var note = new NoteDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Original",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var updated = note with { Title = "Updated", IsPinned = true };

        // Assert
        Assert.AreEqual("Original", note.Title);
        Assert.IsFalse(note.IsPinned);
        Assert.AreEqual("Updated", updated.Title);
        Assert.IsTrue(updated.IsPinned);
    }
}
