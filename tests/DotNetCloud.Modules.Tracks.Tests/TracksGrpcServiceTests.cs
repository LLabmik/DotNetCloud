using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Protos;
using DotNetCloud.Modules.Tracks.Models;
using Grpc.Core;
using Grpc.Core.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

using HostSvc = DotNetCloud.Modules.Tracks.Host.Services;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class TracksGrpcServiceTests
{
    private TracksDbContext _db = null!;
    private HostSvc.TracksGrpcService _service = null!;
    private Mock<IUserDirectory> _userDirMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<ILogger<HostSvc.TracksGrpcService>> _loggerMock = null!;
    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _userDirMock = new Mock<IUserDirectory>();
        _userDirMock
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _userDirMock
            .Setup(x => x.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSearchResult>());
        _eventBusMock = new Mock<IEventBus>();
        _loggerMock = new Mock<ILogger<HostSvc.TracksGrpcService>>();

        var transitionService = new SwimlaneTransitionService(_db);
        var activityService = new ActivityService(_db, _userDirMock.Object);
        var productService = new ProductService(_db);
        var swimlaneService = new SwimlaneService(_db);
        var workItemService = new WorkItemService(_db, transitionService, _eventBusMock.Object, activityService);
        var commentService = new CommentService(_db, _eventBusMock.Object, activityService);
        var checklistService = new ChecklistService(_db);
        var attachmentService = new AttachmentService(_db);
        var dependencyService = new DependencyService(_db);
        var sprintService = new SprintService(_db);
        var timeTrackingService = new TimeTrackingService(_db);
        var analyticsService = new AnalyticsService(_db);
        var pokerService = new PokerService(_db);
        var sprintPlanningService = new SprintPlanningService(_db);
        var reviewSessionService = new ReviewSessionService(_db);
        var customViewService = new CustomViewService(_db);
        var automationRuleLogger = new Mock<ILogger<AutomationRuleService>>().Object;
        var automationRuleService = new AutomationRuleService(_db, automationRuleLogger);
        var goalLogger = new Mock<ILogger<GoalService>>().Object;
        var goalService = new GoalService(_db, goalLogger);
        var webhookLogger = new Mock<ILogger<WebhookService>>().Object;
        var webhookService = new WebhookService(_db, webhookLogger);
        var realtimeService = new NullTracksRealtimeService();
        var discussionLogger = new Mock<ILogger<SprintDiscussionService>>().Object;
        var discussionService = new SprintDiscussionService(_db, realtimeService, discussionLogger);

        _service = new HostSvc.TracksGrpcService(
            _db, productService, swimlaneService, workItemService,
            pokerService, sprintPlanningService, reviewSessionService,
            commentService, checklistService, attachmentService,
            dependencyService, sprintService, timeTrackingService,
            analyticsService, activityService, customViewService,
            automationRuleService, goalService, webhookService,
            discussionService, transitionService,
            _userDirMock.Object, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static ServerCallContext CreateContext() =>
        TestServerCallContext.Create("test", null, DateTime.UtcNow.AddSeconds(30),
            new Metadata(), CancellationToken.None, "127.0.0.1", null, null,
            m => TaskUtils.CompletedTask, () => new WriteOptions(), _ => { });

    // ═══════════════════════════════════════════════════════════════════════
    // PRODUCT TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateProduct_UsesOrganizationIdFromRequest()
    {
        var orgId = Guid.NewGuid();
        var request = new CreateProductRequest
        {
            UserId = _userId.ToString(),
            OrganizationId = orgId.ToString(),
            Name = "Test Org Product",
            SubItemsEnabled = true
        };

        var response = await _service.CreateProduct(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.IsNotNull(response.Product);
        Assert.AreEqual("Test Org Product", response.Product.Name);
        // Verify product was created with the org ID from request
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == Guid.Parse(response.Product.Id));
        Assert.IsNotNull(product);
        Assert.AreEqual(orgId, product!.OrganizationId);
    }

    [TestMethod]
    public async Task CreateProduct_EmptyOrganizationId_UsesGuidEmpty()
    {
        var request = new CreateProductRequest
        {
            UserId = _userId.ToString(),
            OrganizationId = "",
            Name = "No Org Product"
        };

        var response = await _service.CreateProduct(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == Guid.Parse(response.Product.Id));
        Assert.IsNotNull(product);
        Assert.AreEqual(Guid.Empty, product!.OrganizationId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORK ITEM TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateWorkItem_ResolvesProductIdFromSwimlane()
    {
        var orgId = Guid.NewGuid();
        var product = await TestHelpers.SeedProductAsync(_db, orgId, _userId, "WI Product");
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);

        var request = new CreateWorkItemRequest
        {
            SwimlaneId = swimlane.Id.ToString(),
            UserId = _userId.ToString(),
            Title = "Grocery shopping",
            Priority = "Medium",
            StoryPoints = 3,
            AssigneeIds = { _userId.ToString() },
            LabelIds = { }
        };

        var response = await _service.CreateWorkItem(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.IsNotNull(response.WorkItem);
        Assert.AreEqual("Grocery shopping", response.WorkItem.Title);
        // Verify the product was resolved from the swimlane
        var wi = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == Guid.Parse(response.WorkItem.Id));
        Assert.IsNotNull(wi);
        Assert.AreEqual(product.Id, wi!.ProductId);
    }

    [TestMethod]
    public async Task CreateEpic_ResolvesProductIdAndAssignees()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId, "Epic Product");
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var assigneeId = Guid.NewGuid();

        var request = new CreateEpicRequest
        {
            SwimlaneId = swimlane.Id.ToString(),
            UserId = _userId.ToString(),
            Title = "Big Epic",
            Priority = "High",
            AssigneeIds = { assigneeId.ToString() },
            LabelIds = { }
        };

        var response = await _service.CreateEpic(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual("Big Epic", response.WorkItem.Title);
        // Verify assignee was added
        var assignment = await _db.WorkItemAssignments
            .FirstOrDefaultAsync(a => a.WorkItemId == Guid.Parse(response.WorkItem.Id));
        Assert.IsNotNull(assignment);
        Assert.AreEqual(assigneeId, assignment!.UserId);
    }

    [TestMethod]
    public async Task CreateSubItem_UsesChildSwimlane_NotParentSwimlane()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId, "SubItem Product");

        // SubItems require SubItemsEnabled on the product
        var prod = await _db.Products.FirstAsync(p => p.Id == product.Id);
        prod.SubItemsEnabled = true;
        await _db.SaveChangesAsync();

        // SubItems require an Item-type parent (not Epic)
        var parentSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var parentItem = await TestHelpers.SeedWorkItemAsync(_db, product.Id, parentSwimlane.Id, _userId, "Parent Item", WorkItemType.Item);

        // Create child swimlanes for the parent item
        var childSwimlane = new Swimlane
        {
            ContainerType = SwimlaneContainerType.WorkItem,
            ContainerId = parentItem.Id,
            Title = "To Do",
            Position = 1000
        };
        _db.Swimlanes.Add(childSwimlane);
        await _db.SaveChangesAsync();

        var request = new CreateSubItemRequest
        {
            ParentItemId = parentItem.Id.ToString(),
            UserId = _userId.ToString(),
            Title = "Child Task"
        };

        var response = await _service.CreateSubItem(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual("Child Task", response.WorkItem.Title);
        // Verify it was created in the child swimlane, NOT the parent's swimlane
        var subItem = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == Guid.Parse(response.WorkItem.Id));
        Assert.IsNotNull(subItem);
        Assert.AreEqual(childSwimlane.Id, subItem!.SwimlaneId);
        Assert.AreNotEqual(parentSwimlane.Id, subItem.SwimlaneId);
    }

    [TestMethod]
    public async Task CreateSubItem_NoChildSwimlanes_ReturnsError()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var prod = await _db.Products.FirstAsync(p => p.Id == product.Id);
        prod.SubItemsEnabled = true;
        await _db.SaveChangesAsync();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var parentItem = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Parent Item", WorkItemType.Item);
        // Don't create any child swimlanes

        var request = new CreateSubItemRequest
        {
            ParentItemId = parentItem.Id.ToString(),
            UserId = _userId.ToString(),
            Title = "Orphan Child"
        };

        var response = await _service.CreateSubItem(request, CreateContext());

        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.ErrorMessage.Contains("swimlane", StringComparison.OrdinalIgnoreCase));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ASSIGNMENT TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task UnassignUser_UsesAssigneeUserId_NotRequestUserId()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Task to Unassign");

        var assigneeId = Guid.NewGuid();
        // First assign the user
        _db.WorkItemAssignments.Add(new WorkItemAssignment
        {
            WorkItemId = wi.Id,
            UserId = assigneeId
        });
        await _db.SaveChangesAsync();

        var request = new UnassignUserRequest
        {
            WorkItemId = wi.Id.ToString(),
            UserId = _userId.ToString(),           // requesting user
            AssigneeUserId = assigneeId.ToString()  // user to unassign
        };

        var response = await _service.UnassignUser(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        // Verify assignee was removed, not the requesting user
        var assignment = await _db.WorkItemAssignments
            .FirstOrDefaultAsync(a => a.WorkItemId == wi.Id && a.UserId == assigneeId);
        Assert.IsNull(assignment);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SWIMLANE TRANSITION MATRIX TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetSwimlaneTransitionMatrix_ReturnsActualIsAllowedValue()
    {
        var productId = Guid.NewGuid();
        var product = await TestHelpers.SeedProductAsync(_db, productId, _userId, "Matrix Product");

        // Seed rules - one allowed, one blocked
        _db.SwimlaneTransitionRules.Add(new SwimlaneTransitionRule
        {
            ProductId = product.Id,
            FromSwimlaneId = Guid.NewGuid(),
            ToSwimlaneId = Guid.NewGuid(),
            IsAllowed = true
        });
        _db.SwimlaneTransitionRules.Add(new SwimlaneTransitionRule
        {
            ProductId = product.Id,
            FromSwimlaneId = Guid.NewGuid(),
            ToSwimlaneId = Guid.NewGuid(),
            IsAllowed = false
        });
        await _db.SaveChangesAsync();

        var request = new GetSwimlaneTransitionMatrixRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.GetSwimlaneTransitionMatrix(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(2, response.Rules.Count);
        // Verify IsAllowed is NOT hardcoded to true
        Assert.IsTrue(response.Rules.Any(r => r.IsAllowed));
        Assert.IsTrue(response.Rules.Any(r => !r.IsAllowed));
    }

    [TestMethod]
    public async Task SetSwimlaneTransitionMatrix_SavesIsAllowedFromRequest()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId, "Set Matrix Product");

        var fromId = Guid.NewGuid().ToString();
        var toId = Guid.NewGuid().ToString();

        var request = new SetSwimlaneTransitionMatrixRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            Rules =
            {
                new SetTransitionRuleMessage
                {
                    FromSwimlaneId = fromId,
                    ToSwimlaneId = toId,
                    IsAllowed = false  // explicitly blocked
                }
            }
        };

        var response = await _service.SetSwimlaneTransitionMatrix(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        // Verify IsAllowed was saved from the request
        var rule = await _db.SwimlaneTransitionRules
            .FirstOrDefaultAsync(r => r.ProductId == product.Id);
        Assert.IsNotNull(rule);
        Assert.IsFalse(rule!.IsAllowed);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SEARCH USERS TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SearchUsers_ReturnsResultsFromUserDirectory()
    {
        var searchResults = new List<UserSearchResult>
        {
            new(Guid.NewGuid(), "Alice Smith", "alice@example.com"),
            new(Guid.NewGuid(), "Bob Jones", "bob@example.com")
        };
        _userDirMock
            .Setup(x => x.SearchUsersAsync("alice", 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var request = new SearchUsersRequest
        {
            SearchTerm = "alice",
            MaxResults = 8,
            UserId = _userId.ToString()
        };

        var response = await _service.SearchUsers(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(2, response.Results.Count);
        Assert.AreEqual("Alice Smith", response.Results[0].DisplayName);
        Assert.AreEqual("alice@example.com", response.Results[0].Email);
    }

    [TestMethod]
    public async Task SearchUsers_EmptySearchTerm_ReturnsEmpty()
    {
        var request = new SearchUsersRequest
        {
            SearchTerm = "",
            MaxResults = 8,
            UserId = _userId.ToString()
        };

        var response = await _service.SearchUsers(request, CreateContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(0, response.Results.Count);
        // Should NOT call the user directory with empty search
        _userDirMock.Verify(
            x => x.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SearchUsers_UserDirectoryThrows_ReturnsEmptyGracefully()
    {
        _userDirMock
            .Setup(x => x.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var request = new SearchUsersRequest
        {
            SearchTerm = "test",
            MaxResults = 5,
            UserId = _userId.ToString()
        };

        var response = await _service.SearchUsers(request, CreateContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(0, response.Results.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BULK WORK ITEM ACTION TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task BulkWorkItemAction_Delete_DeletesItems()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi1 = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Item 1");
        var wi2 = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Item 2");

        var request = new BulkWorkItemActionRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            WorkItemIds = { wi1.Id.ToString(), wi2.Id.ToString() },
            Action = "delete"
        };

        var response = await _service.BulkWorkItemAction(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(2, response.AffectedCount);
    }

    [TestMethod]
    public async Task BulkWorkItemAction_Archive_ArchivesItems()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Archive Me");

        var request = new BulkWorkItemActionRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            WorkItemIds = { wi.Id.ToString() },
            Action = "archive"
        };

        var response = await _service.BulkWorkItemAction(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(1, response.AffectedCount);
        // Verify item is archived
        var item = await _db.WorkItems.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == wi.Id);
        Assert.IsNotNull(item);
        Assert.IsTrue(item!.IsArchived);
    }

    [TestMethod]
    public async Task BulkWorkItemAction_AddLabel_AddsLabel()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Label Me");

        var label = new Label { ProductId = product.Id, Title = "Bug", Color = "#ff0000" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        var request = new BulkWorkItemActionRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            WorkItemIds = { wi.Id.ToString() },
            Action = "add-label",
            LabelId = label.Id.ToString()
        };

        var response = await _service.BulkWorkItemAction(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(1, response.AffectedCount);
        // Verify label was added
        var link = await _db.WorkItemLabels.FirstOrDefaultAsync(l => l.WorkItemId == wi.Id && l.LabelId == label.Id);
        Assert.IsNotNull(link);
    }

    [TestMethod]
    public async Task BulkWorkItemAction_Assign_AssignsUser()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Assign Me");
        var newAssignee = Guid.NewGuid();

        var request = new BulkWorkItemActionRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            WorkItemIds = { wi.Id.ToString() },
            Action = "assign",
            AssigneeUserId = newAssignee.ToString()
        };

        var response = await _service.BulkWorkItemAction(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(1, response.AffectedCount);
        var assignment = await _db.WorkItemAssignments.FirstOrDefaultAsync(a => a.WorkItemId == wi.Id && a.UserId == newAssignee);
        Assert.IsNotNull(assignment);
    }

    [TestMethod]
    public async Task BulkWorkItemAction_SetPriority_UpdatesPriority()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Prioritize Me");

        var request = new BulkWorkItemActionRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            WorkItemIds = { wi.Id.ToString() },
            Action = "set-priority",
            Priority = "Urgent"
        };

        var response = await _service.BulkWorkItemAction(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(1, response.AffectedCount);
        var item = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == wi.Id);
        Assert.IsNotNull(item);
        Assert.AreEqual(Priority.Urgent, item!.Priority);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ERROR HANDLING TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetWorkItem_NonExistent_ReturnsError()
    {
        var request = new GetWorkItemRequest
        {
            WorkItemId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.GetWorkItem(request, CreateContext());

        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public async Task GetProduct_NonExistent_ReturnsError()
    {
        var request = new GetProductRequest
        {
            ProductId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.GetProduct(request, CreateContext());

        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public async Task DeleteLabel_NonExistent_ReturnsError()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);

        var request = new DeleteLabelRequest
        {
            ProductId = product.Id.ToString(),
            LabelId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString()
        };

        // This should fail because the label doesn't exist
        var response = await _service.DeleteLabel(request, CreateContext());
        Assert.IsFalse(response.Success);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIST / QUERY TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ListProducts_ReturnsProductsForOrganization()
    {
        var orgId = Guid.NewGuid();
        await TestHelpers.SeedProductAsync(_db, orgId, _userId, "Product A");
        await TestHelpers.SeedProductAsync(_db, orgId, _userId, "Product B");

        var request = new ListProductsRequest
        {
            OrganizationId = orgId.ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.ListProducts(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(2, response.Products.Count);
    }

    [TestMethod]
    public async Task ListWorkItems_ReturnsItemsForSwimlane()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Task 1");
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Task 2");

        var request = new ListWorkItemsRequest
        {
            SwimlaneId = swimlane.Id.ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.ListWorkItems(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(2, response.WorkItems.Count);
    }

    [TestMethod]
    public async Task GetChildWorkItems_ReturnsChildren()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var epic = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId, "Epic", WorkItemType.Epic);

        // Create child swimlane + sub-items
        var childSwimlane = new Swimlane
        {
            ContainerType = SwimlaneContainerType.WorkItem,
            ContainerId = epic.Id,
            Title = "Child List",
            Position = 1000
        };
        _db.Swimlanes.Add(childSwimlane);
        await _db.SaveChangesAsync();

        var child1 = new WorkItem
        {
            ProductId = product.Id,
            ParentWorkItemId = epic.Id,
            SwimlaneId = childSwimlane.Id,
            Type = WorkItemType.Item,
            Title = "Child 1",
            Position = 1000,
            CreatedByUserId = _userId
        };
        var child2 = new WorkItem
        {
            ProductId = product.Id,
            ParentWorkItemId = epic.Id,
            SwimlaneId = childSwimlane.Id,
            Type = WorkItemType.Item,
            Title = "Child 2",
            Position = 2000,
            CreatedByUserId = _userId
        };
        _db.WorkItems.AddRange(child1, child2);
        await _db.SaveChangesAsync();

        var request = new GetChildWorkItemsRequest
        {
            ParentWorkItemId = epic.Id.ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.GetChildWorkItems(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual(2, response.WorkItems.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPRINT TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateSprint_WithDuration_Succeeds()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var epicSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var epic = await TestHelpers.SeedWorkItemAsync(_db, product.Id, epicSwimlane.Id, _userId, "Sprint Epic", WorkItemType.Epic);

        var request = new CreateSprintRequest
        {
            EpicId = epic.Id.ToString(),
            UserId = _userId.ToString(),
            Title = "Sprint 1",
            Goal = "Get things done",
            StartDate = DateTime.UtcNow.ToString("O"),
            EndDate = DateTime.UtcNow.AddDays(14).ToString("O"),
            DurationWeeks = 2,
            TargetStoryPoints = 20
        };

        var response = await _service.CreateSprint(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual("Sprint 1", response.Sprint.Title);
        Assert.AreEqual("Get things done", response.Sprint.Goal);
    }

    [TestMethod]
    public async Task AddItemToSprint_Succeeds()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var epicSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var epic = await TestHelpers.SeedWorkItemAsync(_db, product.Id, epicSwimlane.Id, _userId, "Epic", WorkItemType.Epic);

        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Sprint 1",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            Status = SprintStatus.Planning
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, epicSwimlane.Id, _userId, "Task Item");

        var request = new AddItemToSprintRequest
        {
            SprintId = sprint.Id.ToString(),
            ItemId = item.Id.ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.AddItemToSprint(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        var sprintItem = await _db.SprintItems.FirstOrDefaultAsync(si => si.SprintId == sprint.Id && si.ItemId == item.Id);
        Assert.IsNotNull(sprintItem);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMENT TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateAndListComments_Succeeds()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var wi = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, _userId);

        var createReq = new CreateCommentRequest
        {
            WorkItemId = wi.Id.ToString(),
            UserId = _userId.ToString(),
            Content = "Great work!"
        };
        var createRes = await _service.CreateComment(createReq, CreateContext());
        Assert.IsTrue(createRes.Success, createRes.ErrorMessage);
        Assert.AreEqual("Great work!", createRes.Comment.Content);

        var listReq = new ListCommentsRequest
        {
            WorkItemId = wi.Id.ToString(),
            UserId = _userId.ToString(),
            Skip = 0,
            Take = 10
        };
        var listRes = await _service.ListComments(listReq, CreateContext());
        Assert.IsTrue(listRes.Success, listRes.ErrorMessage);
        Assert.AreEqual(1, listRes.Comments.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WEBHOOK TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TestProductWebhook_NonExistentSubscription_ReturnsError()
    {
        var request = new TestProductWebhookRequest
        {
            ProductId = Guid.NewGuid().ToString(),
            SubscriptionId = Guid.NewGuid().ToString(),
            UserId = _userId.ToString()
        };

        var response = await _service.TestProductWebhook(request, CreateContext());

        Assert.IsFalse(response.Success);
        Assert.IsTrue(response.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SWIMLANE TESTS
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateSwimlane_WithCardLimit_Succeeds()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), _userId);

        var request = new CreateSwimlaneRequest
        {
            ProductId = product.Id.ToString(),
            UserId = _userId.ToString(),
            Title = "In Progress",
            Color = "#00ff00",
            CardLimit = 5,
            IsDone = false
        };

        var response = await _service.CreateSwimlane(request, CreateContext());

        Assert.IsTrue(response.Success, response.ErrorMessage);
        Assert.AreEqual("In Progress", response.Swimlane.Title);
    }
}

/// <summary>
/// Helper for Grpc.Core.Testing async call context.
/// </summary>
internal static class TaskUtils
{
    public static readonly Task CompletedTask = Task.CompletedTask;
}
