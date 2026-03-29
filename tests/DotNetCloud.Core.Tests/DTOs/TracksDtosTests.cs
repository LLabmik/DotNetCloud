namespace DotNetCloud.Core.Tests.DTOs;

using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Tracks (Project Management) DTOs.
/// </summary>
[TestClass]
public class TracksDtosTests
{
    // ── Board ──

    [TestMethod]
    public void BoardDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var board = new BoardDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Sprint Board",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, board.Id);
        Assert.AreEqual("Sprint Board", board.Title);
        Assert.IsFalse(board.IsArchived);
        Assert.IsFalse(board.IsDeleted);
    }

    [TestMethod]
    public void BoardDto_OptionalFields_DefaultToNull()
    {
        // Arrange & Act
        var board = new BoardDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(board.Description);
        Assert.IsNull(board.Color);
        Assert.IsNull(board.DeletedAt);
        Assert.IsNull(board.ETag);
    }

    [TestMethod]
    public void BoardDto_Collections_DefaultToEmpty()
    {
        // Arrange & Act
        var board = new BoardDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, board.Members.Count);
        Assert.AreEqual(0, board.Lists.Count);
        Assert.AreEqual(0, board.Labels.Count);
    }

    [TestMethod]
    public void BoardMemberRole_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(BoardMemberRole));

        // Assert
        Assert.AreEqual(4, values.Length);
    }

    [TestMethod]
    public void BoardMemberDto_CanBeCreated()
    {
        // Arrange & Act
        var member = new BoardMemberDto
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Alice",
            Role = BoardMemberRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Alice", member.DisplayName);
        Assert.AreEqual(BoardMemberRole.Admin, member.Role);
    }

    // ── BoardList ──

    [TestMethod]
    public void BoardListDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var list = new BoardListDto
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "To Do",
            Position = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("To Do", list.Title);
        Assert.AreEqual(0, list.Position);
        Assert.IsNull(list.CardLimit);
        Assert.AreEqual(0, list.CardCount);
    }

    // ── Card ──

    [TestMethod]
    public void CardDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var card = new CardDto
        {
            Id = Guid.NewGuid(),
            ListId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Implement auth",
            Position = 1000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Implement auth", card.Title);
        Assert.AreEqual(CardPriority.None, card.Priority);
        Assert.IsFalse(card.IsArchived);
        Assert.IsFalse(card.IsDeleted);
        Assert.AreEqual(0, card.CommentCount);
        Assert.AreEqual(0, card.AttachmentCount);
        Assert.AreEqual(0, card.TotalTrackedMinutes);
    }

    [TestMethod]
    public void CardDto_OptionalFields_DefaultToNull()
    {
        // Arrange & Act
        var card = new CardDto
        {
            Id = Guid.NewGuid(),
            ListId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Test",
            Position = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(card.Description);
        Assert.IsNull(card.DueDate);
        Assert.IsNull(card.StoryPoints);
        Assert.IsNull(card.DeletedAt);
        Assert.IsNull(card.ETag);
    }

    [TestMethod]
    public void CardDto_Collections_DefaultToEmpty()
    {
        // Arrange & Act
        var card = new CardDto
        {
            Id = Guid.NewGuid(),
            ListId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Test",
            Position = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, card.Assignments.Count);
        Assert.AreEqual(0, card.Labels.Count);
        Assert.AreEqual(0, card.Checklists.Count);
    }

    [TestMethod]
    public void CardPriority_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(CardPriority));

        // Assert
        Assert.AreEqual(5, values.Length);
    }

    // ── Assignment, Label, Comment ──

    [TestMethod]
    public void CardAssignmentDto_CanBeCreated()
    {
        // Arrange & Act
        var assignment = new CardAssignmentDto
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Bob",
            AssignedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Bob", assignment.DisplayName);
    }

    [TestMethod]
    public void LabelDto_CanBeCreated()
    {
        // Arrange & Act
        var label = new LabelDto
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Bug",
            Color = "#FF0000"
        };

        // Assert
        Assert.AreEqual("Bug", label.Title);
        Assert.AreEqual("#FF0000", label.Color);
    }

    [TestMethod]
    public void CardCommentDto_CanBeCreated()
    {
        // Arrange & Act
        var comment = new CardCommentDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Alice",
            Content = "Looks good!",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Looks good!", comment.Content);
    }

    // ── Attachment ──

    [TestMethod]
    public void CardAttachmentDto_CanBeCreated_WithFileNodeId()
    {
        // Arrange & Act
        var attachment = new CardAttachmentDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            FileNodeId = Guid.NewGuid(),
            FileName = "design.pdf",
            AddedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNotNull(attachment.FileNodeId);
        Assert.IsNull(attachment.Url);
    }

    [TestMethod]
    public void CardAttachmentDto_CanBeCreated_WithUrl()
    {
        // Arrange & Act
        var attachment = new CardAttachmentDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            FileName = "External Link",
            Url = "https://example.com/spec.pdf",
            AddedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(attachment.FileNodeId);
        Assert.IsNotNull(attachment.Url);
    }

    // ── Checklist ──

    [TestMethod]
    public void CardChecklistDto_CanBeCreated_WithItems()
    {
        // Arrange & Act
        var checklist = new CardChecklistDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            Title = "Acceptance Criteria",
            Position = 0,
            Items =
            [
                new ChecklistItemDto { Id = Guid.NewGuid(), Title = "Unit tests pass", IsCompleted = true, Position = 0 },
                new ChecklistItemDto { Id = Guid.NewGuid(), Title = "Code review done", IsCompleted = false, Position = 1 }
            ]
        };

        // Assert
        Assert.AreEqual("Acceptance Criteria", checklist.Title);
        Assert.AreEqual(2, checklist.Items.Count);
        Assert.IsTrue(checklist.Items[0].IsCompleted);
        Assert.IsFalse(checklist.Items[1].IsCompleted);
    }

    [TestMethod]
    public void CardChecklistDto_Items_DefaultToEmpty()
    {
        // Arrange & Act
        var checklist = new CardChecklistDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            Title = "Test",
            Position = 0
        };

        // Assert
        Assert.AreEqual(0, checklist.Items.Count);
    }

    // ── Dependency ──

    [TestMethod]
    public void CardDependencyType_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(CardDependencyType));

        // Assert
        Assert.AreEqual(2, values.Length);
    }

    [TestMethod]
    public void CardDependencyDto_CanBeCreated()
    {
        // Arrange & Act
        var dep = new CardDependencyDto
        {
            CardId = Guid.NewGuid(),
            DependsOnCardId = Guid.NewGuid(),
            DependsOnCardTitle = "Setup CI",
            Type = CardDependencyType.BlockedBy
        };

        // Assert
        Assert.AreEqual(CardDependencyType.BlockedBy, dep.Type);
        Assert.AreEqual("Setup CI", dep.DependsOnCardTitle);
    }

    // ── Sprint ──

    [TestMethod]
    public void SprintStatus_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(SprintStatus));

        // Assert
        Assert.AreEqual(3, values.Length);
    }

    [TestMethod]
    public void SprintDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var sprint = new SprintDto
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Sprint 1",
            Status = SprintStatus.Planning,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Sprint 1", sprint.Title);
        Assert.AreEqual(SprintStatus.Planning, sprint.Status);
        Assert.IsNull(sprint.Goal);
        Assert.IsNull(sprint.StartDate);
        Assert.IsNull(sprint.EndDate);
        Assert.AreEqual(0, sprint.CardCount);
    }

    // ── TimeEntry ──

    [TestMethod]
    public void TimeEntryDto_CanBeCreated()
    {
        // Arrange & Act
        var entry = new TimeEntryDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Alice",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 120,
            Description = "Working on auth",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(120, entry.DurationMinutes);
        Assert.IsNotNull(entry.EndTime);
    }

    [TestMethod]
    public void TimeEntryDto_RunningTimer_EndTimeIsNull()
    {
        // Arrange & Act
        var entry = new TimeEntryDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            DurationMinutes = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(entry.EndTime);
    }

    // ── BoardActivity ──

    [TestMethod]
    public void BoardActivityDto_CanBeCreated()
    {
        // Arrange & Act
        var activity = new BoardActivityDto
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Bob",
            Action = "card.created",
            EntityType = "Card",
            EntityId = Guid.NewGuid(),
            Details = "{\"title\":\"New task\"}",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("card.created", activity.Action);
        Assert.AreEqual("Card", activity.EntityType);
    }

    // ── Request DTOs ──

    [TestMethod]
    public void CreateBoardDto_HasRequiredTitle()
    {
        // Arrange & Act
        var dto = new CreateBoardDto
        {
            Title = "Project Alpha"
        };

        // Assert
        Assert.AreEqual("Project Alpha", dto.Title);
        Assert.IsNull(dto.Description);
        Assert.IsNull(dto.Color);
    }

    [TestMethod]
    public void UpdateBoardDto_AllFieldsNullable()
    {
        // Arrange & Act
        var dto = new UpdateBoardDto();

        // Assert
        Assert.IsNull(dto.Title);
        Assert.IsNull(dto.Description);
        Assert.IsNull(dto.Color);
        Assert.IsNull(dto.IsArchived);
    }

    [TestMethod]
    public void CreateCardDto_HasRequiredTitle()
    {
        // Arrange & Act
        var dto = new CreateCardDto
        {
            Title = "Fix login bug"
        };

        // Assert
        Assert.AreEqual("Fix login bug", dto.Title);
        Assert.AreEqual(CardPriority.None, dto.Priority);
        Assert.AreEqual(0, dto.AssigneeIds.Count);
        Assert.AreEqual(0, dto.LabelIds.Count);
    }

    [TestMethod]
    public void UpdateCardDto_AllFieldsNullable()
    {
        // Arrange & Act
        var dto = new UpdateCardDto();

        // Assert
        Assert.IsNull(dto.Title);
        Assert.IsNull(dto.Description);
        Assert.IsNull(dto.Priority);
        Assert.IsNull(dto.DueDate);
        Assert.IsNull(dto.StoryPoints);
        Assert.IsNull(dto.IsArchived);
    }

    [TestMethod]
    public void MoveCardDto_HasRequiredFields()
    {
        // Arrange & Act
        var dto = new MoveCardDto
        {
            TargetListId = Guid.NewGuid(),
            Position = 2000
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, dto.TargetListId);
        Assert.AreEqual(2000, dto.Position);
    }

    [TestMethod]
    public void CreateSprintDto_HasRequiredTitle()
    {
        // Arrange & Act
        var dto = new CreateSprintDto
        {
            Title = "Sprint 1"
        };

        // Assert
        Assert.AreEqual("Sprint 1", dto.Title);
        Assert.IsNull(dto.Goal);
        Assert.IsNull(dto.StartDate);
        Assert.IsNull(dto.EndDate);
    }

    [TestMethod]
    public void CreateTimeEntryDto_HasRequiredStartTime()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;
        var dto = new CreateTimeEntryDto
        {
            StartTime = now
        };

        // Assert
        Assert.AreEqual(now, dto.StartTime);
        Assert.IsNull(dto.EndTime);
        Assert.IsNull(dto.DurationMinutes);
        Assert.IsNull(dto.Description);
    }

    // ── Record immutability (with expressions) ──

    [TestMethod]
    public void BoardDto_SupportsWithExpression()
    {
        // Arrange
        var board = new BoardDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Original",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var updated = board with { Title = "Updated", IsArchived = true };

        // Assert
        Assert.AreEqual("Updated", updated.Title);
        Assert.IsTrue(updated.IsArchived);
        Assert.AreEqual("Original", board.Title); // original unchanged
    }

    [TestMethod]
    public void CardDto_SupportsWithExpression()
    {
        // Arrange
        var card = new CardDto
        {
            Id = Guid.NewGuid(),
            ListId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Original",
            Position = 1000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var moved = card with { ListId = Guid.NewGuid(), Position = 2000 };

        // Assert
        Assert.AreNotEqual(card.ListId, moved.ListId);
        Assert.AreEqual(2000, moved.Position);
        Assert.AreEqual("Original", moved.Title);
    }

    // ── Planning Poker ──

    [TestMethod]
    public void PokerSessionStatus_HasExpectedValues()
    {
        Assert.AreEqual(0, (int)PokerSessionStatus.Voting);
        Assert.AreEqual(1, (int)PokerSessionStatus.Revealed);
        Assert.AreEqual(2, (int)PokerSessionStatus.Completed);
        Assert.AreEqual(3, (int)PokerSessionStatus.Cancelled);
    }

    [TestMethod]
    public void PokerScale_HasExpectedValues()
    {
        Assert.AreEqual(0, (int)PokerScale.Fibonacci);
        Assert.AreEqual(1, (int)PokerScale.TShirt);
        Assert.AreEqual(2, (int)PokerScale.PowersOfTwo);
        Assert.AreEqual(3, (int)PokerScale.Custom);
    }

    [TestMethod]
    public void PokerSessionDto_CanBeCreated_WithRequiredProperties()
    {
        var session = new PokerSessionDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Scale = PokerScale.Fibonacci,
            Status = PokerSessionStatus.Voting,
            Round = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.AreNotEqual(Guid.Empty, session.Id);
        Assert.AreEqual(PokerScale.Fibonacci, session.Scale);
        Assert.AreEqual(PokerSessionStatus.Voting, session.Status);
        Assert.AreEqual(1, session.Round);
        Assert.IsNull(session.AcceptedEstimate);
        Assert.IsNull(session.CustomScaleValues);
    }

    [TestMethod]
    public void PokerSessionDto_Votes_DefaultToEmpty()
    {
        var session = new PokerSessionDto
        {
            Id = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Scale = PokerScale.Fibonacci,
            Status = PokerSessionStatus.Voting,
            Round = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.AreEqual(0, session.Votes.Count);
    }

    [TestMethod]
    public void PokerVoteDto_CanBeCreated_WithRequiredProperties()
    {
        var vote = new PokerVoteDto
        {
            UserId = Guid.NewGuid(),
            Estimate = "8",
            VotedAt = DateTime.UtcNow,
            Round = 1
        };

        Assert.AreEqual("8", vote.Estimate);
        Assert.AreEqual(1, vote.Round);
        Assert.IsNull(vote.DisplayName);
    }

    [TestMethod]
    public void CreatePokerSessionDto_CanBeCreated()
    {
        var dto = new CreatePokerSessionDto
        {
            Scale = PokerScale.TShirt,
            CustomScaleValues = null
        };

        Assert.AreEqual(PokerScale.TShirt, dto.Scale);
    }

    [TestMethod]
    public void CreatePokerSessionDto_CustomScale_IncludesValues()
    {
        var dto = new CreatePokerSessionDto
        {
            Scale = PokerScale.Custom,
            CustomScaleValues = "[\"XS\",\"S\",\"M\",\"L\",\"XL\"]"
        };

        Assert.AreEqual(PokerScale.Custom, dto.Scale);
        Assert.IsNotNull(dto.CustomScaleValues);
    }

    [TestMethod]
    public void SubmitPokerVoteDto_CanBeCreated()
    {
        var dto = new SubmitPokerVoteDto { Estimate = "13" };
        Assert.AreEqual("13", dto.Estimate);
    }

    [TestMethod]
    public void AcceptPokerEstimateDto_CanBeCreated()
    {
        var dto = new AcceptPokerEstimateDto
        {
            AcceptedEstimate = "8",
            StoryPoints = 8
        };

        Assert.AreEqual("8", dto.AcceptedEstimate);
        Assert.AreEqual(8, dto.StoryPoints);
    }

    [TestMethod]
    public void AcceptPokerEstimateDto_NonNumericScale_StoryPointsNull()
    {
        var dto = new AcceptPokerEstimateDto
        {
            AcceptedEstimate = "L",
            StoryPoints = null
        };

        Assert.AreEqual("L", dto.AcceptedEstimate);
        Assert.IsNull(dto.StoryPoints);
    }
}
