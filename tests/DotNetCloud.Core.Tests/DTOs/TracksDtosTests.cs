namespace DotNetCloud.Core.Tests.DTOs;

using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Tracks (Project Management) DTOs.
/// </summary>
[TestClass]
public class TracksDtosTests
{
    // -- Product --

    [TestMethod]
    public void ProductDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var product = new ProductDto
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Sprint Board",
            OwnerId = Guid.NewGuid(),
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, product.Id);
        Assert.AreEqual("Sprint Board", product.Name);
        Assert.IsFalse(product.IsArchived);
    }

    [TestMethod]
    public void ProductDto_OptionalFields_DefaultToNull()
    {
        // Arrange & Act
        var product = new ProductDto
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Test",
            OwnerId = Guid.NewGuid(),
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(product.Description);
        Assert.IsNull(product.Color);
    }

    [TestMethod]
    public void ProductDto_SwimlaneAndEpicCounts_DefaultToZero()
    {
        // Arrange & Act
        var product = new ProductDto
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Test",
            OwnerId = Guid.NewGuid(),
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, product.SwimlaneCount);
        Assert.AreEqual(0, product.EpicCount);
        Assert.AreEqual(0, product.MemberCount);
        Assert.AreEqual(0, product.LabelCount);
    }

    [TestMethod]
    public void ProductMemberRole_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(ProductMemberRole));

        // Assert
        Assert.AreEqual(4, values.Length);
    }

    [TestMethod]
    public void ProductMemberDto_CanBeCreated()
    {
        // Arrange & Act
        var member = new ProductMemberDto
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Alice",
            Role = ProductMemberRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Alice", member.DisplayName);
        Assert.AreEqual(ProductMemberRole.Admin, member.Role);
    }

    // -- Swimlane --

    [TestMethod]
    public void SwimlaneDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var swimlane = new SwimlaneDto
        {
            Id = Guid.NewGuid(),
            ContainerType = SwimlaneContainerType.Product,
            ContainerId = Guid.NewGuid(),
            Title = "To Do",
            Position = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("To Do", swimlane.Title);
        Assert.AreEqual(0, swimlane.Position);
        Assert.IsNull(swimlane.CardLimit);
        Assert.AreEqual(0, swimlane.CardCount);
    }

    // -- WorkItem --

    [TestMethod]
    public void WorkItemDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var item = new WorkItemDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ParentWorkItemId = null,
            Type = WorkItemType.Item,
            SwimlaneId = Guid.NewGuid(),
            ItemNumber = 42,
            Title = "Implement auth",
            Position = 1000,
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalTrackedMinutes = null
        };

        // Assert
        Assert.AreEqual("Implement auth", item.Title);
        Assert.AreEqual(Priority.None, item.Priority);
        Assert.IsFalse(item.IsArchived);
        Assert.AreEqual(0, item.CommentCount);
        Assert.AreEqual(0, item.AttachmentCount);
        Assert.IsNull(item.TotalTrackedMinutes);
    }

    [TestMethod]
    public void WorkItemDto_OptionalFields_DefaultToNull()
    {
        // Arrange & Act
        var item = new WorkItemDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ParentWorkItemId = null,
            Type = WorkItemType.Item,
            SwimlaneId = Guid.NewGuid(),
            ItemNumber = 1,
            Title = "Test",
            Position = 0,
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalTrackedMinutes = null
        };

        // Assert
        Assert.IsNull(item.Description);
        Assert.IsNull(item.DueDate);
        Assert.IsNull(item.StoryPoints);
        Assert.IsNull(item.SprintId);
        Assert.IsNull(item.SprintTitle);
    }

    [TestMethod]
    public void WorkItemDto_Collections_DefaultToEmpty()
    {
        // Arrange & Act
        var item = new WorkItemDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ParentWorkItemId = null,
            Type = WorkItemType.Item,
            SwimlaneId = Guid.NewGuid(),
            ItemNumber = 1,
            Title = "Test",
            Position = 0,
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalTrackedMinutes = null
        };

        // Assert
        Assert.AreEqual(0, item.Assignments.Count);
        Assert.AreEqual(0, item.Labels.Count);
        Assert.IsNull(item.Checklists);
    }

    [TestMethod]
    public void Priority_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(Priority));

        // Assert
        Assert.AreEqual(5, values.Length);
    }

    // -- Assignment, Label, Comment --

    [TestMethod]
    public void WorkItemAssignmentDto_CanBeCreated()
    {
        // Arrange & Act
        var assignment = new WorkItemAssignmentDto
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
            ProductId = Guid.NewGuid(),
            Title = "Bug",
            Color = "#FF0000",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Bug", label.Title);
        Assert.AreEqual("#FF0000", label.Color);
    }

    [TestMethod]
    public void WorkItemCommentDto_CanBeCreated()
    {
        // Arrange & Act
        var comment = new WorkItemCommentDto
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Alice",
            Content = "Looks good!",
            IsEdited = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Looks good!", comment.Content);
    }

    // -- Attachment --

    [TestMethod]
    public void WorkItemAttachmentDto_CanBeCreated_WithFileNodeId()
    {
        // Arrange & Act
        var attachment = new WorkItemAttachmentDto
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            FileNodeId = Guid.NewGuid(),
            FileName = "design.pdf",
            UploadedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNotNull(attachment.FileNodeId);
        Assert.IsNull(attachment.Url);
    }

    [TestMethod]
    public void WorkItemAttachmentDto_CanBeCreated_WithUrl()
    {
        // Arrange & Act
        var attachment = new WorkItemAttachmentDto
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            FileName = "External Link",
            Url = "https://example.com/spec.pdf",
            UploadedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(attachment.FileNodeId);
        Assert.IsNotNull(attachment.Url);
    }

    // -- Checklist --

    [TestMethod]
    public void ChecklistDto_CanBeCreated_WithItems()
    {
        // Arrange & Act
        var checklist = new ChecklistDto
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Title = "Acceptance Criteria",
            Position = 0,
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new ChecklistItemDto { Id = Guid.NewGuid(), ChecklistId = Guid.NewGuid(), Title = "Unit tests pass", IsCompleted = true, Position = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new ChecklistItemDto { Id = Guid.NewGuid(), ChecklistId = Guid.NewGuid(), Title = "Code review done", IsCompleted = false, Position = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            ]
        };

        // Assert
        Assert.AreEqual("Acceptance Criteria", checklist.Title);
        Assert.AreEqual(2, checklist.Items.Count);
        Assert.IsTrue(checklist.Items[0].IsCompleted);
        Assert.IsFalse(checklist.Items[1].IsCompleted);
    }

    [TestMethod]
    public void ChecklistDto_Items_DefaultToEmpty()
    {
        // Arrange & Act
        var checklist = new ChecklistDto
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            Title = "Test",
            Position = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, checklist.Items.Count);
    }

    // -- Dependency --

    [TestMethod]
    public void DependencyType_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(DependencyType));

        // Assert
        Assert.AreEqual(2, values.Length);
    }

    [TestMethod]
    public void WorkItemDependencyDto_CanBeCreated()
    {
        // Arrange & Act
        var dep = new WorkItemDependencyDto
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            DependsOnWorkItemId = Guid.NewGuid(),
            DependsOnTitle = "Setup CI",
            Type = DependencyType.BlockedBy,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(DependencyType.BlockedBy, dep.Type);
        Assert.AreEqual("Setup CI", dep.DependsOnTitle);
    }

    // -- Sprint --

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
            EpicId = Guid.NewGuid(),
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
        Assert.AreEqual(0, sprint.ItemCount);
    }

    // -- TimeEntry --

    [TestMethod]
    public void TimeEntryDto_CanBeCreated()
    {
        // Arrange & Act
        var entry = new TimeEntryDto
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 120,
            Description = "Working on auth",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
            WorkItemId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            DurationMinutes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(entry.EndTime);
    }

    // -- Activity --

    [TestMethod]
    public void ActivityDto_CanBeCreated()
    {
        // Arrange & Act
        var activity = new ActivityDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = "Bob",
            Action = "workitem.created",
            EntityType = "WorkItem",
            EntityId = Guid.NewGuid(),
            Details = "{\"title\":\"New task\"}",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("workitem.created", activity.Action);
        Assert.AreEqual("WorkItem", activity.EntityType);
    }

    // -- Request DTOs --

    [TestMethod]
    public void CreateProductDto_HasRequiredName()
    {
        // Arrange & Act
        var dto = new CreateProductDto
        {
            Name = "Project Alpha"
        };

        // Assert
        Assert.AreEqual("Project Alpha", dto.Name);
        Assert.IsNull(dto.Description);
        Assert.IsNull(dto.Color);
    }

    [TestMethod]
    public void UpdateProductDto_AllFieldsNullable()
    {
        // Arrange & Act
        var dto = new UpdateProductDto();

        // Assert
        Assert.IsNull(dto.Name);
        Assert.IsNull(dto.Description);
        Assert.IsNull(dto.Color);
        Assert.IsNull(dto.SubItemsEnabled);
    }

    [TestMethod]
    public void CreateWorkItemDto_HasRequiredTitle()
    {
        // Arrange & Act
        var dto = new CreateWorkItemDto
        {
            Title = "Fix login bug"
        };

        // Assert
        Assert.AreEqual("Fix login bug", dto.Title);
        Assert.AreEqual(Priority.None, dto.Priority);
        Assert.AreEqual(0, dto.AssigneeIds.Count);
        Assert.AreEqual(0, dto.LabelIds.Count);
    }

    [TestMethod]
    public void UpdateWorkItemDto_AllFieldsNullable()
    {
        // Arrange & Act
        var dto = new UpdateWorkItemDto();

        // Assert
        Assert.IsNull(dto.Title);
        Assert.IsNull(dto.Description);
        Assert.IsNull(dto.Priority);
        Assert.IsNull(dto.DueDate);
        Assert.IsNull(dto.StoryPoints);
        Assert.IsNull(dto.IsArchived);
    }

    [TestMethod]
    public void MoveWorkItemDto_HasRequiredFields()
    {
        // Arrange & Act
        var dto = new MoveWorkItemDto
        {
            TargetSwimlaneId = Guid.NewGuid(),
            Position = 2000
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, dto.TargetSwimlaneId);
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
    public void CreateTimeEntryDto_Defaults()
    {
        // Arrange & Act
        var dto = new CreateTimeEntryDto
        {
            DurationMinutes = 30
        };

        // Assert
        Assert.AreEqual(30, dto.DurationMinutes);
        Assert.IsNull(dto.StartTime);
        Assert.IsNull(dto.Description);
    }

    // -- Record immutability (with expressions) --

    [TestMethod]
    public void ProductDto_SupportsWithExpression()
    {
        // Arrange
        var product = new ProductDto
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Original",
            OwnerId = Guid.NewGuid(),
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var updated = product with { Name = "Updated", IsArchived = true };

        // Assert
        Assert.AreEqual("Updated", updated.Name);
        Assert.IsTrue(updated.IsArchived);
        Assert.AreEqual("Original", product.Name); // original unchanged
    }

    [TestMethod]
    public void WorkItemDto_SupportsWithExpression()
    {
        // Arrange
        var item = new WorkItemDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ParentWorkItemId = null,
            Type = WorkItemType.Item,
            SwimlaneId = Guid.NewGuid(),
            ItemNumber = 1,
            Title = "Original",
            Position = 1000,
            ETag = "etag-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalTrackedMinutes = null
        };

        // Act
        var moved = item with { SwimlaneId = Guid.NewGuid(), Position = 2000 };

        // Assert
        Assert.AreNotEqual(item.SwimlaneId, moved.SwimlaneId);
        Assert.AreEqual(2000, moved.Position);
        Assert.AreEqual("Original", moved.Title);
    }

    // -- Planning Poker --

    [TestMethod]
    public void PokerSessionStatus_HasExpectedValues()
    {
        Assert.AreEqual(0, Convert.ToInt32(PokerSessionStatus.Voting));
        Assert.AreEqual(1, Convert.ToInt32(PokerSessionStatus.Revealed));
        Assert.AreEqual(2, Convert.ToInt32(PokerSessionStatus.Completed));
        Assert.AreEqual(3, Convert.ToInt32(PokerSessionStatus.Cancelled));
    }

    [TestMethod]
    public void PokerScale_HasExpectedValues()
    {
        Assert.AreEqual(0, Convert.ToInt32(PokerScale.Fibonacci));
        Assert.AreEqual(1, Convert.ToInt32(PokerScale.TShirt));
        Assert.AreEqual(2, Convert.ToInt32(PokerScale.PowersOfTwo));
        Assert.AreEqual(3, Convert.ToInt32(PokerScale.Custom));
    }

    [TestMethod]
    public void PokerSessionDto_CanBeCreated_WithRequiredProperties()
    {
        var session = new PokerSessionDto
        {
            Id = Guid.NewGuid(),
            EpicId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
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
    public void PokerVoteDto_CanBeCreated_WithRequiredProperties()
    {
        var vote = new PokerVoteDto
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
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
            ItemId = Guid.NewGuid(),
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
            ItemId = Guid.NewGuid(),
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
}
