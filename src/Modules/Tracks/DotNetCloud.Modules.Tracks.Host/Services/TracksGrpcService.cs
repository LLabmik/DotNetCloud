using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Protos;
using DotNetCloud.Modules.Tracks.Models;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// gRPC service implementation for the Tracks module.
/// </summary>
public sealed class TracksGrpcService : Protos.TracksGrpcService.TracksGrpcServiceBase
{
    private readonly TracksDbContext _db;
    private readonly ProductService _productService;
    private readonly SwimlaneService _swimlaneService;
    private readonly WorkItemService _workItemService;
    private readonly PokerService _pokerService;
    private readonly SprintPlanningService _sprintPlanningService;
    private readonly ReviewSessionService _reviewSessionService;
    private readonly CommentService _commentService;
    private readonly ChecklistService _checklistService;
    private readonly AttachmentService _attachmentService;
    private readonly DependencyService _dependencyService;
    private readonly SprintService _sprintService;
    private readonly TimeTrackingService _timeTrackingService;
    private readonly AnalyticsService _analyticsService;
    private readonly ActivityService _activityService;
    private readonly CustomViewService _customViewService;
    private readonly AutomationRuleService _automationRuleService;
    private readonly GoalService _goalService;
    private readonly WebhookService _webhookService;
    private readonly SprintDiscussionService _discussionService;
    private readonly SwimlaneTransitionService _transitionService;
    private readonly IUserDirectory _userDirectory;
    private readonly ILogger<TracksGrpcService> _logger;

    public TracksGrpcService(
        TracksDbContext db,
        ProductService productService,
        SwimlaneService swimlaneService,
        WorkItemService workItemService,
        PokerService pokerService,
        SprintPlanningService sprintPlanningService,
        ReviewSessionService reviewSessionService,
        CommentService commentService,
        ChecklistService checklistService,
        AttachmentService attachmentService,
        DependencyService dependencyService,
        SprintService sprintService,
        TimeTrackingService timeTrackingService,
        AnalyticsService analyticsService,
        ActivityService activityService,
        CustomViewService customViewService,
        AutomationRuleService automationRuleService,
        GoalService goalService,
        WebhookService webhookService,
        SprintDiscussionService discussionService,
        SwimlaneTransitionService transitionService,
        IUserDirectory userDirectory,
        ILogger<TracksGrpcService> logger)
    {
        _db = db;
        _productService = productService;
        _swimlaneService = swimlaneService;
        _workItemService = workItemService;
        _pokerService = pokerService;
        _sprintPlanningService = sprintPlanningService;
        _reviewSessionService = reviewSessionService;
        _commentService = commentService;
        _checklistService = checklistService;
        _attachmentService = attachmentService;
        _dependencyService = dependencyService;
        _sprintService = sprintService;
        _timeTrackingService = timeTrackingService;
        _analyticsService = analyticsService;
        _activityService = activityService;
        _customViewService = customViewService;
        _automationRuleService = automationRuleService;
        _goalService = goalService;
        _webhookService = webhookService;
        _discussionService = discussionService;
        _transitionService = transitionService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ProductResponse> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateProduct called for user {UserId}", request.UserId);
        try
        {
            var ownerId = Guid.Parse(request.UserId);
            var organizationId = string.IsNullOrEmpty(request.OrganizationId) ? Guid.Empty : Guid.Parse(request.OrganizationId);
            var dto = new CreateProductDto
            {
                Name = request.Name,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color,
                SubItemsEnabled = request.SubItemsEnabled
            };
            var product = await _productService.CreateProductAsync(
                organizationId, ownerId, dto, context.CancellationToken);
            return new ProductResponse { Success = true, Product = MapProduct(product) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateProduct failed");
            return new ProductResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ProductResponse> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        try
        {
            var productId = Guid.Parse(request.ProductId);
            var product = await _productService.GetProductAsync(productId, context.CancellationToken);
            return new ProductResponse { Success = true, Product = MapProduct(product) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProduct failed");
            return new ProductResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListProductsResponse> ListProducts(ListProductsRequest request, ServerCallContext context)
    {
        try
        {
            var products = await _productService.ListProductsByOrganizationAsync(
                Guid.Parse(request.OrganizationId), context.CancellationToken);

            if (request.IncludeArchived == false)
                products = products.Where(p => !p.IsArchived).ToList();

            var response = new ListProductsResponse { Success = true };
            foreach (var product in products)
                response.Products.Add(MapProduct(product));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListProducts failed");
            return new ListProductsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SwimlaneResponse> CreateSwimlane(CreateSwimlaneRequest request, ServerCallContext context)
    {
        try
        {
            var containerId = Guid.Parse(request.ProductId);
            var dto = new CreateSwimlaneDto
            {
                Title = request.Title,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color
            };
            var swimlane = await _swimlaneService.CreateSwimlaneAsync(
                SwimlaneContainerType.Product, containerId, dto, context.CancellationToken);
            return new SwimlaneResponse { Success = true, Swimlane = MapSwimlane(swimlane) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSwimlane failed");
            return new SwimlaneResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<WorkItemResponse> CreateWorkItem(CreateWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var swimlaneId = Guid.Parse(request.SwimlaneId);
            var userId = Guid.Parse(request.UserId);
            var priority = Enum.TryParse<Priority>(request.Priority, true, out var p) ? p : Priority.None;
            DateTime? dueDate = DateTime.TryParse(request.DueDate, out var dd) ? dd : null;

            // Resolve the owning product from the swimlane (product-level or work-item level)
            var swimlane = await _db.Swimlanes.FirstOrDefaultAsync(s => s.Id == swimlaneId, context.CancellationToken);
            var productId = await ResolveProductIdAsync(swimlane, context.CancellationToken);

            var dto = new CreateWorkItemDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = priority,
                DueDate = dueDate,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = request.AssigneeIds?.Select(Guid.Parse).ToList() ?? [],
                LabelIds = request.LabelIds?.Select(Guid.Parse).ToList() ?? []
            };
            var workItem = await _workItemService.CreateWorkItemAsync(
                productId, swimlaneId, WorkItemType.Epic, userId, dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateWorkItem failed");
            return new WorkItemResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<WorkItemResponse> GetWorkItem(GetWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var workItemId = Guid.Parse(request.WorkItemId);
            var workItem = await _workItemService.GetWorkItemAsync(workItemId, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWorkItem failed");
            return new WorkItemResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<WorkItemResponse> MoveWorkItem(MoveWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var workItemId = Guid.Parse(request.WorkItemId);
            var dto = new MoveWorkItemDto
            {
                TargetSwimlaneId = Guid.Parse(request.TargetSwimlaneId),
                Position = (int)request.Position
            };
            var workItem = await _workItemService.MoveWorkItemAsync(
                workItemId, dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MoveWorkItem failed");
            return new WorkItemResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PokerSessionResponse> StartPokerSession(StartPokerSessionRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartPokerSession called for item {ItemId} by user {UserId}", request.ItemId, request.UserId);
        try
        {
            var userId = Guid.Parse(request.UserId);
            var dto = new CreatePokerSessionDto
            {
                ItemId = Guid.Parse(request.ItemId),
                Scale = Enum.TryParse<PokerScale>(request.Scale, true, out var scale) ? scale : PokerScale.Fibonacci,
                CustomScaleValues = string.IsNullOrEmpty(request.CustomScaleValues) ? null : request.CustomScaleValues
            };
            var session = await _pokerService.StartSessionAsync(
                Guid.Empty, userId, dto, context.CancellationToken);
            return new PokerSessionResponse { Success = true, Session = MapPokerSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartPokerSession failed");
            return new PokerSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PokerSessionResponse> SubmitPokerVote(SubmitPokerVoteRequest request, ServerCallContext context)
    {
        try
        {
            var userId = Guid.Parse(request.UserId);
            var dto = new SubmitPokerVoteDto { Estimate = request.Estimate };
            var session = await _pokerService.SubmitVoteAsync(
                Guid.Parse(request.SessionId), userId, dto, context.CancellationToken);
            return new PokerSessionResponse { Success = true, Session = MapPokerSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitPokerVote failed");
            return new PokerSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PokerSessionResponse> RevealPokerSession(RevealPokerSessionRequest request, ServerCallContext context)
    {
        try
        {
            var session = await _pokerService.RevealVotesAsync(
                Guid.Parse(request.SessionId), context.CancellationToken);
            return new PokerSessionResponse { Success = true, Session = MapPokerSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RevealPokerSession failed");
            return new PokerSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PokerSessionResponse> AcceptPokerEstimate(AcceptPokerEstimateRequest request, ServerCallContext context)
    {
        try
        {
            var session = await _pokerService.AcceptEstimateAsync(
                Guid.Parse(request.SessionId), request.AcceptedEstimate, context.CancellationToken);
            return new PokerSessionResponse { Success = true, Session = MapPokerSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptPokerEstimate failed");
            return new PokerSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Sprint Plan RPCs ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<SprintPlanResponse> CreateSprintPlan(CreateSprintPlanRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateSprintPlanDto
            {
                StartDate = DateTime.Parse(request.StartDate),
                NumberOfSprints = request.NumberOfSprints,
                SprintDurationWeeks = request.SprintDurationWeeks
            };
            var sprints = await _sprintPlanningService.CreateSprintPlanAsync(
                Guid.Parse(request.EpicId), dto, context.CancellationToken);
            var response = new SprintPlanResponse { Success = true };
            response.Sprints.AddRange(MapSprints(sprints));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSprintPlan failed");
            return new SprintPlanResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SprintPlanResponse> GetSprintPlan(GetSprintPlanRequest request, ServerCallContext context)
    {
        try
        {
            var epicId = Guid.Parse(request.EpicId);
            var sprints = await _sprintPlanningService.GetSprintPlanAsync(epicId, context.CancellationToken);
            var response = new SprintPlanResponse { Success = true };
            response.Sprints.AddRange(MapSprints(sprints));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSprintPlan failed");
            return new SprintPlanResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SprintPlanResponse> AdjustSprint(AdjustSprintRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new AdjustSprintDto
            {
                DurationWeeks = request.DurationWeeks,
                StartDate = string.IsNullOrEmpty(request.StartDate) ? null : DateTime.Parse(request.StartDate)
            };
            var sprint = await _sprintPlanningService.AdjustSprintDatesAsync(
                Guid.Parse(request.SprintId), dto, context.CancellationToken);
            var sprints = await _sprintPlanningService.GetSprintPlanAsync(
                sprint.EpicId, context.CancellationToken);
            var response = new SprintPlanResponse { Success = true };
            response.Sprints.AddRange(MapSprints(sprints));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdjustSprint failed");
            return new SprintPlanResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Review Session RPCs ──────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> StartReviewSession(StartReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            var hostUserId = Guid.Parse(request.UserId);
            var session = await _reviewSessionService.StartReviewSessionAsync(
                Guid.Parse(request.EpicId), hostUserId, context.CancellationToken);
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartReviewSession failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> GetReviewSession(GetReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            var session = await _reviewSessionService.GetReviewSessionAsync(
                Guid.Parse(request.SessionId), context.CancellationToken);
            if (session is null)
                return new ReviewSessionResponse { Success = false, ErrorMessage = "Review session not found." };
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetReviewSession failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> JoinReviewSession(JoinReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            var userId = Guid.Parse(request.UserId);
            var participant = await _reviewSessionService.JoinSessionAsync(
                Guid.Parse(request.SessionId), userId, context.CancellationToken);

            var session = await _reviewSessionService.GetReviewSessionAsync(
                Guid.Parse(request.SessionId), context.CancellationToken);
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session!) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JoinReviewSession failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> SetReviewCurrentItem(SetReviewCurrentItemRequest request, ServerCallContext context)
    {
        try
        {
            var session = await _reviewSessionService.SetCurrentItemAsync(
                Guid.Parse(request.SessionId), Guid.Parse(request.ItemId), context.CancellationToken);
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetReviewCurrentItem failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> EndReviewSession(EndReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            var session = await _reviewSessionService.EndSessionAsync(
                Guid.Parse(request.SessionId), context.CancellationToken);
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EndReviewSession failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PokerVoteStatusResponse> GetPokerVoteStatus(GetPokerVoteStatusRequest request, ServerCallContext context)
    {
        try
        {
            var statuses = await _pokerService.GetVoteStatusAsync(
                Guid.Parse(request.SessionId), context.CancellationToken);
            var response = new PokerVoteStatusResponse { Success = true };
            foreach (var s in statuses)
                response.Statuses.Add(new PokerVoteStatusItem { UserId = "", HasVoted = s.HasVoted });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPokerVoteStatus failed");
            return new PokerVoteStatusResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Mapping Helpers ──────────────────────────────────────────────────

    private static PokerSessionMessage MapPokerSession(PokerSessionDto dto)
    {
        var msg = new PokerSessionMessage
        {
            Id = dto.Id.ToString(),
            ItemId = dto.ItemId.ToString(),
            EpicId = dto.EpicId.ToString(),
            CreatedByUserId = dto.CreatedByUserId.ToString(),
            Scale = dto.Scale.ToString(),
            CustomScaleValues = dto.CustomScaleValues ?? "",
            Status = dto.Status.ToString(),
            AcceptedEstimate = dto.AcceptedEstimate ?? "",
            Round = dto.Round,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
        return msg;
    }

    private static ProductMessage MapProduct(ProductDto dto)
    {
        return new ProductMessage
        {
            Id = dto.Id.ToString(),
            OrganizationId = dto.OrganizationId.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? "",
            Color = dto.Color ?? "",
            IsArchived = dto.IsArchived,
            Etag = dto.ETag ?? "",
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            SwimlaneCount = dto.SwimlaneCount,
            EpicCount = dto.EpicCount,
            MemberCount = dto.MemberCount
        };
    }

    private static SwimlaneMessage MapSwimlane(SwimlaneDto dto)
    {
        return new SwimlaneMessage
        {
            Id = dto.Id.ToString(),
            ContainerType = dto.ContainerType.ToString(),
            ContainerId = dto.ContainerId.ToString(),
            Title = dto.Title,
            Position = dto.Position,
            Color = dto.Color ?? "",
            CardLimit = dto.CardLimit ?? 0,
            IsDone = dto.IsDone,
            IsArchived = dto.IsArchived,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            CardCount = dto.CardCount
        };
    }

    private static WorkItemMessage MapWorkItem(WorkItemDto dto)
    {
        var msg = new WorkItemMessage
        {
            Id = dto.Id.ToString(),
            ProductId = dto.ProductId.ToString(),
            Type = dto.Type.ToString(),
            ParentWorkItemId = dto.ParentWorkItemId?.ToString() ?? "",
            SwimlaneId = dto.SwimlaneId.ToString(),
            SwimlaneTitle = dto.SwimlaneTitle ?? "",
            ItemNumber = dto.ItemNumber,
            Title = dto.Title,
            Description = dto.Description ?? "",
            Position = dto.Position,
            Priority = dto.Priority.ToString(),
            StartDate = dto.StartDate?.ToString("O") ?? "",
            DueDate = dto.DueDate?.ToString("O") ?? "",
            StoryPoints = dto.StoryPoints ?? 0,
            IsArchived = dto.IsArchived,
            SprintId = dto.SprintId?.ToString() ?? "",
            SprintTitle = dto.SprintTitle ?? "",
            TotalTrackedMinutes = (int)(dto.TotalTrackedMinutes ?? 0),
            MilestoneId = dto.MilestoneId?.ToString() ?? "",
            MilestoneTitle = dto.MilestoneTitle ?? "",
            CreatedByUserId = "",
            Etag = dto.ETag,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            DeletedAt = dto.DeletedAt?.ToString("O") ?? "",
            DeletedByUserId = dto.DeletedByUserId?.ToString() ?? "",
            DeletedByDisplayName = dto.DeletedByDisplayName ?? "",
            CommentCount = dto.CommentCount,
            AttachmentCount = dto.AttachmentCount
        };
        foreach (var a in dto.Assignments)
            msg.Assignments.Add(new WorkItemAssignmentMessage
            {
                UserId = a.UserId.ToString(),
                DisplayName = a.DisplayName ?? "",
                AssignedAt = a.AssignedAt.ToString("O")
            });
        foreach (var l in dto.Labels)
            msg.Labels.Add(new LabelMessage
            {
                Id = l.Id.ToString(),
                ProductId = l.ProductId.ToString(),
                Title = l.Title,
                Color = l.Color,
                CreatedAt = l.CreatedAt.ToString("O")
            });
        return msg;
    }

    private static IEnumerable<SprintMessage> MapSprints(List<SprintDto> sprints)
    {
        return sprints.Select(s => new SprintMessage
        {
            Id = s.Id.ToString(),
            EpicId = s.EpicId.ToString(),
            Title = s.Title,
            Goal = s.Goal ?? "",
            StartDate = s.StartDate?.ToString("O") ?? "",
            EndDate = s.EndDate?.ToString("O") ?? "",
            Status = s.Status.ToString(),
            TargetStoryPoints = s.TargetStoryPoints ?? 0,
            DurationWeeks = s.DurationWeeks ?? 0,
            PlannedOrder = s.PlannedOrder ?? 0,
            ItemCount = s.ItemCount,
            TotalStoryPoints = s.TotalStoryPoints,
            CompletedStoryPoints = s.CompletedStoryPoints,
            CreatedAt = s.CreatedAt.ToString("O"),
            UpdatedAt = s.UpdatedAt.ToString("O")
        });
    }

    private static ReviewSessionMessage MapReviewSession(ReviewSessionDto dto)
    {
        var msg = new ReviewSessionMessage
        {
            Id = dto.Id.ToString(),
            EpicId = dto.EpicId.ToString(),
            HostUserId = dto.HostUserId.ToString(),
            CurrentItemId = dto.CurrentItemId?.ToString() ?? "",
            Status = dto.Status.ToString(),
            CreatedAt = dto.CreatedAt.ToString("O"),
            EndedAt = dto.EndedAt?.ToString("O") ?? ""
        };
        return msg;
    }

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        GetSearchableDocumentsRequest request,
        IServerStreamWriter<SearchableDocument> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out _))
            return;

        var products = await _productService.ListProductsByOrganizationAsync(
            Guid.Empty, context.CancellationToken);

        foreach (var product in products.Where(p => !p.IsArchived))
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(
                SwimlaneContainerType.Product, product.Id, context.CancellationToken);

            foreach (var swimlane in swimlanes)
            {
                var workItems = await _workItemService.GetWorkItemsBySwimlaneAsync(
                    swimlane.Id, context.CancellationToken);

                foreach (var workItem in workItems)
                {
                    var doc = MapWorkItemToSearchableDocument(workItem, product.Name);
                    await responseStream.WriteAsync(doc, context.CancellationToken);
                }
            }
        }
    }

    /// <inheritdoc />
    public override async Task<SearchableDocumentResponse> GetSearchableDocument(
        GetSearchableDocumentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EntityId, out var entityId))
            return new SearchableDocumentResponse { Found = false };

        try
        {
            var workItem = await _workItemService.GetWorkItemAsync(entityId, context.CancellationToken);
            return new SearchableDocumentResponse
            {
                Found = true,
                Document = MapWorkItemToSearchableDocument(workItem, null)
            };
        }
        catch
        {
            return new SearchableDocumentResponse { Found = false };
        }
    }

    private static SearchableDocument MapWorkItemToSearchableDocument(WorkItemDto workItem, string? productName)
    {
        var contentParts = new List<string>();
        if (!string.IsNullOrEmpty(workItem.Description))
            contentParts.Add(workItem.Description);
        foreach (var label in workItem.Labels)
            contentParts.Add(label.Title);

        var doc = new SearchableDocument
        {
            ModuleId = "tracks",
            EntityId = workItem.Id.ToString(),
            EntityType = workItem.Type.ToString(),
            Title = workItem.Title,
            Content = string.Join(" ", contentParts),
            Summary = workItem.Description?.Length > 200
                ? workItem.Description[..200] + "..."
                : workItem.Description ?? string.Empty,
            OwnerId = string.Empty,
            CreatedAt = workItem.CreatedAt.ToString("O"),
            UpdatedAt = workItem.UpdatedAt.ToString("O")
        };

        doc.Metadata["ProductId"] = workItem.ProductId.ToString();
        doc.Metadata["Priority"] = workItem.Priority.ToString();
        doc.Metadata["Type"] = workItem.Type.ToString();
        if (productName is not null)
            doc.Metadata["ProductName"] = productName;
        if (workItem.Labels.Count > 0)
            doc.Metadata["Labels"] = string.Join(",", workItem.Labels.Select(l => l.Title));

        return doc;
    }
    /// <inheritdoc />
    /// <inheritdoc />
    public override async Task<ProductResponse> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateProductDto
            {
                Name = string.IsNullOrEmpty(request.Name) ? null : request.Name,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color,
                SubItemsEnabled = request.SubItemsEnabled,
                ETag = string.IsNullOrEmpty(request.Etag) ? null : request.Etag
            };
            var product = await _productService.UpdateProductAsync(Guid.Parse(request.ProductId), dto, context.CancellationToken);
            return new ProductResponse { Success = true, Product = MapProduct(product) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProduct failed");
            return new ProductResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
    {
        try
        {
            await _productService.DeleteProductAsync(Guid.Parse(request.ProductId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteProduct failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListProductsResponse> ListDeletedProducts(ListDeletedProductsRequest request, ServerCallContext context)
    {
        try
        {
            var products = await _productService.ListDeletedProductsAsync(Guid.Parse(request.OrganizationId), context.CancellationToken);
            var response = new ListProductsResponse { Success = true };
            foreach (var p in products)
                response.Products.Add(MapProduct(p));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListDeletedProducts failed");
            return new ListProductsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ProductResponse> RestoreProduct(RestoreProductRequest request, ServerCallContext context)
    {
        try
        {
            var product = await _productService.UndeleteProductAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            return new ProductResponse { Success = true, Product = MapProduct(product) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RestoreProduct failed");
            return new ProductResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> PermanentDeleteProduct(PermanentDeleteProductRequest request, ServerCallContext context)
    {
        try
        {
            await _productService.HardDeleteProductAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PermanentDeleteProduct failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListProductMembersResponse> ListProductMembers(ListProductMembersRequest request, ServerCallContext context)
    {
        try
        {
            var productId = Guid.Parse(request.ProductId);
            var members = await _db.ProductMembers
                .Where(pm => pm.ProductId == productId)
                .ToListAsync(context.CancellationToken);

            // Batch-resolve display names via IUserDirectory
            var userIds = members.Select(m => m.UserId).Distinct().ToList();
            IReadOnlyDictionary<Guid, string> displayNames;
            try
            {
                displayNames = await _userDirectory.GetDisplayNamesAsync(userIds, context.CancellationToken);
            }
            catch
            {
                displayNames = new Dictionary<Guid, string>();
            }

            var response = new ListProductMembersResponse { Success = true };
            foreach (var m in members)
                response.Members.Add(new ProductMemberMessage
                {
                    UserId = m.UserId.ToString(),
                    DisplayName = displayNames.TryGetValue(m.UserId, out var name) ? name : "",
                    Role = m.Role.ToString(),
                    JoinedAt = m.JoinedAt.ToString("O")
                });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListProductMembers failed");
            return new ListProductMembersResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ProductMemberResponse> AddProductMember(AddProductMemberRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new AddProductMemberDto
            {
                UserId = Guid.Parse(request.MemberUserId),
                Role = Enum.TryParse<ProductMemberRole>(request.Role, true, out var r) ? r : ProductMemberRole.Viewer
            };
            var member = await _productService.AddMemberAsync(Guid.Parse(request.ProductId), dto, context.CancellationToken);
            return new ProductMemberResponse
            {
                Success = true,
                Member = new ProductMemberMessage
                {
                    UserId = member.UserId.ToString(),
                    DisplayName = "",
                    Role = member.Role.ToString(),
                    JoinedAt = member.JoinedAt.ToString("O")
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddProductMember failed");
            return new ProductMemberResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> RemoveProductMember(RemoveProductMemberRequest request, ServerCallContext context)
    {
        try
        {
            await _productService.RemoveMemberAsync(
                Guid.Parse(request.ProductId), Guid.Parse(request.MemberUserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveProductMember failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ProductMemberResponse> UpdateProductMemberRole(UpdateProductMemberRoleRequest request, ServerCallContext context)
    {
        try
        {
            var member = await _productService.UpdateMemberRoleAsync(
                Guid.Parse(request.ProductId),
                Guid.Parse(request.MemberUserId),
                Enum.TryParse<ProductMemberRole>(request.Role, true, out var r) ? r : ProductMemberRole.Viewer,
                context.CancellationToken);
            return new ProductMemberResponse
            {
                Success = true,
                Member = new ProductMemberMessage
                {
                    UserId = member.UserId.ToString(),
                    DisplayName = "",
                    Role = member.Role.ToString(),
                    JoinedAt = member.JoinedAt.ToString("O")
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProductMemberRole failed");
            return new ProductMemberResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListLabelsResponse> ListLabels(ListLabelsRequest request, ServerCallContext context)
    {
        try
        {
            var productId = Guid.Parse(request.ProductId);
            var labels = await _db.Labels
                .Where(l => l.ProductId == productId)
                .ToListAsync(context.CancellationToken);
            var response = new ListLabelsResponse { Success = true };
            foreach (var l in labels)
                response.Labels.Add(new LabelMessage
                {
                    Id = l.Id.ToString(),
                    ProductId = l.ProductId.ToString(),
                    Title = l.Title,
                    Color = l.Color,
                    CreatedAt = l.CreatedAt.ToString("O")
                });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListLabels failed");
            return new ListLabelsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<LabelResponse> CreateLabel(CreateLabelRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateLabelDto
            {
                Title = request.Title,
                Color = request.Color
            };
            var label = await _productService.CreateLabelAsync(Guid.Parse(request.ProductId), dto, context.CancellationToken);
            return new LabelResponse
            {
                Success = true,
                Label = new LabelMessage
                {
                    Id = label.Id.ToString(),
                    ProductId = label.ProductId.ToString(),
                    Title = label.Title,
                    Color = label.Color,
                    CreatedAt = label.CreatedAt.ToString("O")
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateLabel failed");
            return new LabelResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<LabelResponse> UpdateLabel(UpdateLabelRequest request, ServerCallContext context)
    {
        try
        {
            var label = await _db.Labels.FirstOrDefaultAsync(
                l => l.Id == Guid.Parse(request.LabelId), context.CancellationToken);
            if (label is null)
                return new LabelResponse { Success = false, ErrorMessage = "Label not found" };
            if (!string.IsNullOrEmpty(request.Title))
                label.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Color))
                label.Color = request.Color;
            await _db.SaveChangesAsync(context.CancellationToken);
            return new LabelResponse
            {
                Success = true,
                Label = new LabelMessage
                {
                    Id = label.Id.ToString(),
                    ProductId = label.ProductId.ToString(),
                    Title = label.Title,
                    Color = label.Color,
                    CreatedAt = label.CreatedAt.ToString("O")
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateLabel failed");
            return new LabelResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteLabel(DeleteLabelRequest request, ServerCallContext context)
    {
        try
        {
            await _productService.DeleteLabelAsync(
                Guid.Parse(request.ProductId), Guid.Parse(request.LabelId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteLabel failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public override async Task<ListSwimlanesResponse> ListProductSwimlanes(ListProductSwimlanesRequest request, ServerCallContext context)
    {
        try
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(
                SwimlaneContainerType.Product, Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new ListSwimlanesResponse { Success = true };
            foreach (var s in swimlanes)
                response.Swimlanes.Add(MapSwimlane(s));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListProductSwimlanes failed");
            return new ListSwimlanesResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListSwimlanesResponse> ListWorkItemSwimlanes(ListWorkItemSwimlanesRequest request, ServerCallContext context)
    {
        try
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(
                SwimlaneContainerType.WorkItem, Guid.Parse(request.WorkItemId), context.CancellationToken);
            var response = new ListSwimlanesResponse { Success = true };
            foreach (var s in swimlanes)
                response.Swimlanes.Add(MapSwimlane(s));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListWorkItemSwimlanes failed");
            return new ListSwimlanesResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SwimlaneResponse> CreateWorkItemSwimlane(CreateWorkItemSwimlaneRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateSwimlaneDto
            {
                Title = request.Title,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color,
                CardLimit = request.CardLimit > 0 ? request.CardLimit : null,
                IsDone = request.IsDone
            };
            var swimlane = await _swimlaneService.CreateSwimlaneAsync(
                SwimlaneContainerType.WorkItem, Guid.Parse(request.WorkItemId), dto, context.CancellationToken);
            return new SwimlaneResponse { Success = true, Swimlane = MapSwimlane(swimlane) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateWorkItemSwimlane failed");
            return new SwimlaneResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SwimlaneResponse> UpdateSwimlane(UpdateSwimlaneRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateSwimlaneDto
            {
                Title = string.IsNullOrEmpty(request.Title) ? null : request.Title,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color,
                CardLimit = request.CardLimit > 0 ? request.CardLimit : null,
                IsDone = request.IsDone
            };
            var swimlane = await _swimlaneService.UpdateSwimlaneAsync(
                Guid.Parse(request.SwimlaneId), dto, context.CancellationToken);
            return new SwimlaneResponse { Success = true, Swimlane = MapSwimlane(swimlane) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateSwimlane failed");
            return new SwimlaneResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteSwimlane(DeleteSwimlaneRequest request, ServerCallContext context)
    {
        try
        {
            await _swimlaneService.DeleteSwimlaneAsync(
                Guid.Parse(request.SwimlaneId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSwimlane failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> ReorderSwimlanes(ReorderSwimlanesRequest request, ServerCallContext context)
    {
        try
        {
            var orderedIds = request.OrderedIds.Select(Guid.Parse).ToList();
            await _swimlaneService.ReorderSwimlanesAsync(orderedIds, context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReorderSwimlanes failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GetSwimlaneTransitionMatrixResponse> GetSwimlaneTransitionMatrix(
        GetSwimlaneTransitionMatrixRequest request, ServerCallContext context)
    {
        try
        {
            var productId = Guid.Parse(request.ProductId);
            var rules = await _db.SwimlaneTransitionRules
                .Where(r => r.ProductId == productId)
                .ToListAsync(context.CancellationToken);
            var response = new GetSwimlaneTransitionMatrixResponse { Success = true };
            foreach (var r in rules)
                response.Rules.Add(new SwimlaneTransitionRuleMessage
                {
                    Id = r.Id.ToString(),
                    ProductId = r.ProductId.ToString(),
                    FromSwimlaneId = r.FromSwimlaneId.ToString(),
                    ToSwimlaneId = r.ToSwimlaneId.ToString(),
                    IsAllowed = r.IsAllowed,
                    CreatedAt = r.CreatedAt.ToString("O")
                });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSwimlaneTransitionMatrix failed");
            return new GetSwimlaneTransitionMatrixResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SetSwimlaneTransitionMatrixResponse> SetSwimlaneTransitionMatrix(
        SetSwimlaneTransitionMatrixRequest request, ServerCallContext context)
    {
        try
        {
            var productId = Guid.Parse(request.ProductId);
            var existing = await _db.SwimlaneTransitionRules
                .Where(r => r.ProductId == productId)
                .ToListAsync(context.CancellationToken);
            _db.SwimlaneTransitionRules.RemoveRange(existing);
            foreach (var rule in request.Rules)
                _db.SwimlaneTransitionRules.Add(new SwimlaneTransitionRule
                {
                    ProductId = productId,
                    FromSwimlaneId = Guid.Parse(rule.FromSwimlaneId),
                    ToSwimlaneId = Guid.Parse(rule.ToSwimlaneId),
                    IsAllowed = rule.IsAllowed
                });
            await _db.SaveChangesAsync(context.CancellationToken);
            return new SetSwimlaneTransitionMatrixResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetSwimlaneTransitionMatrix failed");
            return new SetSwimlaneTransitionMatrixResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListWorkItemsResponse> ListWorkItems(ListWorkItemsRequest request, ServerCallContext context)
    {
        try
        {
            var workItems = await _workItemService.GetWorkItemsBySwimlaneAsync(Guid.Parse(request.SwimlaneId), context.CancellationToken);
            var response = new ListWorkItemsResponse { Success = true };
            foreach (var wi in workItems)
                response.WorkItems.Add(MapWorkItem(wi));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListWorkItems failed");
            return new ListWorkItemsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WorkItemResponse> GetWorkItemByNumber(GetWorkItemByNumberRequest request, ServerCallContext context)
    {
        try
        {
            var workItem = await _workItemService.GetWorkItemByNumberAsync(Guid.Parse(request.ProductId), request.ItemNumber, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWorkItemByNumber failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WorkItemResponse> CreateEpic(CreateEpicRequest request, ServerCallContext context)
    {
        try
        {
            var swimlaneId = Guid.Parse(request.SwimlaneId);
            var swimlane = await _db.Swimlanes.FirstOrDefaultAsync(s => s.Id == swimlaneId, context.CancellationToken);
            var productId = await ResolveProductIdAsync(swimlane, context.CancellationToken);
            var dto = new CreateWorkItemDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = Enum.TryParse<Priority>(request.Priority, true, out var ep) ? ep : Priority.None,
                DueDate = DateTime.TryParse(request.DueDate, out var edd) ? edd : null,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = request.AssigneeIds?.Select(Guid.Parse).ToList() ?? [],
                LabelIds = request.LabelIds?.Select(Guid.Parse).ToList() ?? []
            };
            var workItem = await _workItemService.CreateWorkItemAsync(productId, swimlaneId, WorkItemType.Epic, Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateEpic failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WorkItemResponse> CreateFeature(CreateFeatureRequest request, ServerCallContext context)
    {
        try
        {
            var swimlaneId = Guid.Parse(request.SwimlaneId);
            var swimlane = await _db.Swimlanes.FirstOrDefaultAsync(s => s.Id == swimlaneId, context.CancellationToken);
            var productId = await ResolveProductIdAsync(swimlane, context.CancellationToken);
            var dto = new CreateWorkItemDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = Enum.TryParse<Priority>(request.Priority, true, out var fp) ? fp : Priority.None,
                DueDate = DateTime.TryParse(request.DueDate, out var fdd) ? fdd : null,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = request.AssigneeIds?.Select(Guid.Parse).ToList() ?? [],
                LabelIds = request.LabelIds?.Select(Guid.Parse).ToList() ?? []
            };
            var workItem = await _workItemService.CreateWorkItemAsync(productId, swimlaneId, WorkItemType.Feature, Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateFeature failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WorkItemResponse> CreateItem(CreateItemRequest request, ServerCallContext context)
    {
        try
        {
            var swimlaneId = Guid.Parse(request.SwimlaneId);
            var swimlane = await _db.Swimlanes.FirstOrDefaultAsync(s => s.Id == swimlaneId, context.CancellationToken);
            var productId = await ResolveProductIdAsync(swimlane, context.CancellationToken);
            var dto = new CreateWorkItemDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = Enum.TryParse<Priority>(request.Priority, true, out var ip) ? ip : Priority.None,
                DueDate = DateTime.TryParse(request.DueDate, out var idd) ? idd : null,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = request.AssigneeIds?.Select(Guid.Parse).ToList() ?? [],
                LabelIds = request.LabelIds?.Select(Guid.Parse).ToList() ?? []
            };
            var workItem = await _workItemService.CreateWorkItemAsync(productId, swimlaneId, WorkItemType.Item, Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateItem failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WorkItemResponse> CreateSubItem(CreateSubItemRequest request, ServerCallContext context)
    {
        try
        {
            var parentId = Guid.Parse(request.ParentItemId);
            var parent = await _db.WorkItems.FirstOrDefaultAsync(wi => wi.Id == parentId, context.CancellationToken);
            if (parent is null)
                return new WorkItemResponse { Success = false, ErrorMessage = "Parent work item not found" };

            // Find the first child swimlane of the parent (SubItems must be created in WorkItem-type swimlanes)
            var childSwimlane = await _db.Swimlanes
                .Where(s => s.ContainerType == SwimlaneContainerType.WorkItem && s.ContainerId == parentId && !s.IsArchived)
                .OrderBy(s => s.Position)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (childSwimlane is null)
                return new WorkItemResponse { Success = false, ErrorMessage = "Parent work item has no child swimlanes. Create swimlanes first." };

            var dto = new CreateWorkItemDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = Enum.TryParse<Priority>(request.Priority, true, out var p) ? p : Priority.None,
                DueDate = DateTime.TryParse(request.DueDate, out var dd) ? dd : null,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = request.AssigneeIds?.Select(Guid.Parse).ToList() ?? [],
                LabelIds = request.LabelIds?.Select(Guid.Parse).ToList() ?? []
            };
            var workItem = await _workItemService.CreateWorkItemAsync(
                parent.ProductId, childSwimlane.Id, WorkItemType.SubItem, Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSubItem failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Resolves the owning product ID for a swimlane. Product-level swimlanes own the product
    /// directly; work-item swimlanes (epic/feature boards) belong to the parent work item, so the
    /// parent's product ID is returned.
    /// </summary>
    private async Task<Guid> ResolveProductIdAsync(Swimlane? swimlane, CancellationToken ct)
    {
        if (swimlane is null)
            throw new InvalidOperationException("Swimlane not found.");

        if (swimlane.ContainerType == SwimlaneContainerType.Product)
            return swimlane.ContainerId;

        var parent = await _db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(wi => wi.Id == swimlane.ContainerId && !wi.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Parent work item {swimlane.ContainerId} not found.");

        return parent.ProductId;
    }

    /// <inheritdoc />
    public override async Task<WorkItemResponse> UpdateWorkItem(UpdateWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateWorkItemDto { Title = string.IsNullOrEmpty(request.Title) ? null : request.Title, Description = string.IsNullOrEmpty(request.Description) ? null : request.Description, Priority = string.IsNullOrEmpty(request.Priority) ? null : Enum.TryParse<Priority>(request.Priority, true, out var p) ? p : null, DueDate = string.IsNullOrEmpty(request.DueDate) ? null : DateTime.TryParse(request.DueDate, out var dd) ? dd : null, StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null, ETag = string.IsNullOrEmpty(request.Etag) ? null : request.Etag };
            var workItem = await _workItemService.UpdateWorkItemAsync(Guid.Parse(request.WorkItemId), dto, context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateWorkItem failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteWorkItem(DeleteWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            await _workItemService.DeleteWorkItemAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteWorkItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListWorkItemsResponse> GetChildWorkItems(GetChildWorkItemsRequest request, ServerCallContext context)
    {
        try
        {
            var children = await _workItemService.GetChildWorkItemsAsync(Guid.Parse(request.ParentWorkItemId), context.CancellationToken);
            var response = new ListWorkItemsResponse { Success = true };
            foreach (var wi in children)
                response.WorkItems.Add(MapWorkItem(wi));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetChildWorkItems failed");
            return new ListWorkItemsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListWorkItemsResponse> ListDeletedWorkItems(ListDeletedWorkItemsRequest request, ServerCallContext context)
    {
        try
        {
            var items = await _workItemService.ListDeletedWorkItemsAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new ListWorkItemsResponse { Success = true };
            foreach (var wi in items)
                response.WorkItems.Add(MapWorkItem(wi));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListDeletedWorkItems failed");
            return new ListWorkItemsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WorkItemResponse> RestoreWorkItem(RestoreWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var workItem = await _workItemService.RestoreWorkItemAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), context.CancellationToken);
            return new WorkItemResponse { Success = true, WorkItem = MapWorkItem(workItem) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RestoreWorkItem failed");
            return new WorkItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> PermanentDeleteWorkItem(PermanentDeleteWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            await _workItemService.HardDeleteWorkItemAsync(Guid.Parse(request.WorkItemId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PermanentDeleteWorkItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<EmptyWorkItemTrashResponse> EmptyWorkItemTrash(EmptyWorkItemTrashRequest request, ServerCallContext context)
    {
        try
        {
            var deletedItems = await _db.WorkItems.IgnoreQueryFilters().Where(wi => wi.ProductId == Guid.Parse(request.ProductId) && wi.IsDeleted).ToListAsync(context.CancellationToken);
            int count = deletedItems.Count;
            foreach (var item in deletedItems)
                await _workItemService.HardDeleteWorkItemAsync(item.Id, context.CancellationToken);
            return new EmptyWorkItemTrashResponse { Success = true, DeletedCount = count };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmptyWorkItemTrash failed");
            return new EmptyWorkItemTrashResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ExportWorkItemsCsvResponse> ExportWorkItemsCsv(ExportWorkItemsCsvRequest request, ServerCallContext context)
    {
        try
        {
            var workItems = await _db.WorkItems.Where(wi => wi.ProductId == Guid.Parse(request.ProductId) && !wi.IsDeleted).OrderBy(wi => wi.ItemNumber).ToListAsync(context.CancellationToken);
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Number,Type,Title,Priority,StoryPoints");
            foreach (var wi in workItems)
            {
                var title = (wi.Title ?? "").Replace("\"", "\"\"");
                csv.AppendLine(string.Format("{0},{1},\"{2}\",{3},{4}", wi.ItemNumber, wi.Type, title, wi.Priority, wi.StoryPoints));
            }
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return new ExportWorkItemsCsvResponse { Success = true, CsvData = Google.Protobuf.ByteString.CopyFrom(csvBytes) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExportWorkItemsCsv failed");
            return new ExportWorkItemsCsvResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GetWatchersResponse> GetWatchers(GetWatchersRequest request, ServerCallContext context)
    {
        try
        {
            var watchers = await _db.WorkItemWatchers.Where(w => w.WorkItemId == Guid.Parse(request.WorkItemId)).ToListAsync(context.CancellationToken);
            var response = new GetWatchersResponse { Success = true };
            foreach (var w in watchers)
                response.WatcherUserIds.Add(w.UserId.ToString());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWatchers failed");
            return new GetWatchersResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WatchCountResponse> WatchWorkItem(WatchWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var wid = Guid.Parse(request.WorkItemId);
            var uid = Guid.Parse(request.UserId);
            if (!await _db.WorkItemWatchers.AnyAsync(w => w.WorkItemId == wid && w.UserId == uid, context.CancellationToken))
            {
                _db.WorkItemWatchers.Add(new WorkItemWatcher { WorkItemId = wid, UserId = uid, SubscribedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync(context.CancellationToken);
            }
            var count = await _db.WorkItemWatchers.CountAsync(w => w.WorkItemId == wid, context.CancellationToken);
            return new WatchCountResponse { Success = true, Count = count };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WatchWorkItem failed");
            return new WatchCountResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WatchCountResponse> UnwatchWorkItem(UnwatchWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var wid = Guid.Parse(request.WorkItemId);
            var uid = Guid.Parse(request.UserId);
            var watcher = await _db.WorkItemWatchers.FirstOrDefaultAsync(w => w.WorkItemId == wid && w.UserId == uid, context.CancellationToken);
            if (watcher is not null)
            {
                _db.WorkItemWatchers.Remove(watcher);
                await _db.SaveChangesAsync(context.CancellationToken);
            }
            var count = await _db.WorkItemWatchers.CountAsync(w => w.WorkItemId == wid, context.CancellationToken);
            return new WatchCountResponse { Success = true, Count = count };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnwatchWorkItem failed");
            return new WatchCountResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> AssignUser(AssignUserRequest request, ServerCallContext context)
    {
        try
        {
            await _workItemService.AssignUserAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.AssigneeUserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AssignUser failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> UnassignUser(UnassignUserRequest request, ServerCallContext context)
    {
        try
        {
            await _workItemService.RemoveAssignmentAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.AssigneeUserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnassignUser failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> AddLabelToWorkItem(AddLabelToWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            await _workItemService.AddLabelAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.LabelId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddLabelToWorkItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> RemoveLabelFromWorkItem(RemoveLabelFromWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            await _workItemService.RemoveLabelAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.LabelId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveLabelFromWorkItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListCommentsResponse> ListComments(ListCommentsRequest request, ServerCallContext context)
    {
        try
        {
            var comments = await _commentService.GetCommentsByWorkItemAsync(Guid.Parse(request.WorkItemId), request.Skip, request.Take > 0 ? request.Take : 50, context.CancellationToken);
            var response = new ListCommentsResponse { Success = true };
            foreach (var c in comments)
                response.Comments.Add(MapComment(c));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListComments failed");
            return new ListCommentsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<CommentResponse> CreateComment(CreateCommentRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new AddWorkItemCommentDto { Content = request.Content };
            var comment = await _commentService.CreateCommentAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new CommentResponse { Success = true, Comment = MapComment(comment) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateComment failed");
            return new CommentResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<CommentResponse> UpdateComment(UpdateCommentRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateWorkItemCommentDto { Content = request.Content };
            var comment = await _commentService.UpdateCommentAsync(Guid.Parse(request.CommentId), Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new CommentResponse { Success = true, Comment = MapComment(comment) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateComment failed");
            return new CommentResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteComment(DeleteCommentRequest request, ServerCallContext context)
    {
        try
        {
            await _commentService.DeleteCommentAsync(Guid.Parse(request.CommentId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteComment failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListCommentsResponse> ListDeletedComments(ListDeletedCommentsRequest request, ServerCallContext context)
    {
        try
        {
            var comments = await _commentService.ListDeletedCommentsAsync(Guid.Parse(request.WorkItemId), context.CancellationToken);
            var response = new ListCommentsResponse { Success = true };
            foreach (var c in comments)
                response.Comments.Add(MapComment(c));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListDeletedComments failed");
            return new ListCommentsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> RestoreComment(RestoreCommentRequest request, ServerCallContext context)
    {
        try
        {
            await _commentService.RestoreCommentAsync(Guid.Parse(request.CommentId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RestoreComment failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> PermanentDeleteComment(PermanentDeleteCommentRequest request, ServerCallContext context)
    {
        try
        {
            await _commentService.PermanentDeleteCommentAsync(Guid.Parse(request.CommentId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PermanentDeleteComment failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListChecklistsResponse> ListChecklists(ListChecklistsRequest request, ServerCallContext context)
    {
        try
        {
            var checklists = await _checklistService.GetChecklistsByItemAsync(Guid.Parse(request.ItemId), context.CancellationToken);
            var response = new ListChecklistsResponse { Success = true };
            foreach (var c in checklists)
                response.Checklists.Add(MapChecklist(c));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListChecklists failed");
            return new ListChecklistsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ChecklistResponse> CreateChecklist(CreateChecklistRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateChecklistDto { Title = request.Title };
            var checklist = await _checklistService.CreateChecklistAsync(Guid.Parse(request.ItemId), dto, context.CancellationToken);
            return new ChecklistResponse { Success = true, Checklist = MapChecklist(checklist) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateChecklist failed");
            return new ChecklistResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteChecklist(DeleteChecklistRequest request, ServerCallContext context)
    {
        try
        {
            await _checklistService.DeleteChecklistAsync(Guid.Parse(request.ChecklistId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteChecklist failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ChecklistItemResponse> AddChecklistItem(AddChecklistItemRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new AddChecklistItemDto { Title = request.Title };
            var item = await _checklistService.AddChecklistItemAsync(Guid.Parse(request.ChecklistId), dto, context.CancellationToken);
            return new ChecklistItemResponse { Success = true, ChecklistItem = new ChecklistItemMessage { Id = item.Id.ToString(), ChecklistId = item.ChecklistId.ToString(), Title = item.Title ?? "", IsCompleted = item.IsCompleted, Position = item.Position, AssignedToUserId = item.AssignedToUserId?.ToString() ?? "", CreatedAt = item.CreatedAt.ToString("O"), UpdatedAt = item.UpdatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddChecklistItem failed");
            return new ChecklistItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ChecklistItemResponse> ToggleChecklistItem(ToggleChecklistItemRequest request, ServerCallContext context)
    {
        try
        {
            await _checklistService.ToggleChecklistItemAsync(Guid.Parse(request.ChecklistItemId), context.CancellationToken);
            var item = await _db.ChecklistItems.FirstOrDefaultAsync(ci => ci.Id == Guid.Parse(request.ChecklistItemId), context.CancellationToken);
            if (item is null)
                return new ChecklistItemResponse { Success = false, ErrorMessage = "Checklist item not found" };
            return new ChecklistItemResponse { Success = true, ChecklistItem = new ChecklistItemMessage { Id = item.Id.ToString(), ChecklistId = item.ChecklistId.ToString(), Title = item.Title ?? "", IsCompleted = item.IsCompleted, Position = item.Position, AssignedToUserId = item.AssignedToUserId?.ToString() ?? "", CreatedAt = item.CreatedAt.ToString("O"), UpdatedAt = item.UpdatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ToggleChecklistItem failed");
            return new ChecklistItemResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteChecklistItem(DeleteChecklistItemRequest request, ServerCallContext context)
    {
        try
        {
            await _checklistService.DeleteChecklistItemAsync(Guid.Parse(request.ChecklistItemId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteChecklistItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListAttachmentsResponse> ListAttachments(ListAttachmentsRequest request, ServerCallContext context)
    {
        try
        {
            var attachments = await _attachmentService.GetAttachmentsByWorkItemAsync(Guid.Parse(request.WorkItemId), context.CancellationToken);
            var response = new ListAttachmentsResponse { Success = true };
            foreach (var a in attachments)
                response.Attachments.Add(new WorkItemAttachmentMessage { Id = a.Id.ToString(), WorkItemId = a.WorkItemId.ToString(), FileName = a.FileName ?? "", FileSize = a.FileSize ?? 0, MimeType = a.MimeType ?? "", FileNodeId = a.FileNodeId?.ToString() ?? "", Url = a.Url ?? "", UploadedByUserId = a.UploadedByUserId.ToString(), CreatedAt = a.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListAttachments failed");
            return new ListAttachmentsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<AttachmentResponse> AddAttachment(AddAttachmentRequest request, ServerCallContext context)
    {
        try
        {
            var fileNodeId = string.IsNullOrEmpty(request.FileNodeId) ? (Guid?)null : Guid.Parse(request.FileNodeId);
            var attachment = await _attachmentService.AddAttachmentAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), request.FileName, 0, null, fileNodeId, string.IsNullOrEmpty(request.Url) ? null : request.Url, context.CancellationToken);
            return new AttachmentResponse { Success = true, Attachment = new WorkItemAttachmentMessage { Id = attachment.Id.ToString(), WorkItemId = attachment.WorkItemId.ToString(), FileName = attachment.FileName ?? "", FileSize = attachment.FileSize ?? 0, MimeType = attachment.MimeType ?? "", FileNodeId = attachment.FileNodeId?.ToString() ?? "", Url = attachment.Url ?? "", UploadedByUserId = attachment.UploadedByUserId.ToString(), CreatedAt = attachment.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddAttachment failed");
            return new AttachmentResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> RemoveAttachment(RemoveAttachmentRequest request, ServerCallContext context)
    {
        try
        {
            await _attachmentService.RemoveAttachmentAsync(Guid.Parse(request.AttachmentId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveAttachment failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListDependenciesResponse> ListDependencies(ListDependenciesRequest request, ServerCallContext context)
    {
        try
        {
            var deps = await _dependencyService.GetDependenciesByWorkItemAsync(Guid.Parse(request.WorkItemId), context.CancellationToken);
            var response = new ListDependenciesResponse { Success = true };
            foreach (var d in deps)
                response.Dependencies.Add(MapDependency(d));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListDependencies failed");
            return new ListDependenciesResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<DependencyResponse> AddDependency(AddDependencyRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new AddWorkItemDependencyDto { DependsOnWorkItemId = Guid.Parse(request.DependsOnWorkItemId), Type = Enum.TryParse<DependencyType>(request.Type, true, out var dt) ? dt : DependencyType.BlockedBy };
            var dep = await _dependencyService.AddDependencyAsync(Guid.Parse(request.WorkItemId), dto, context.CancellationToken);
            return new DependencyResponse { Success = true, Dependency = MapDependency(dep) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddDependency failed");
            return new DependencyResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> RemoveDependency(RemoveDependencyRequest request, ServerCallContext context)
    {
        try
        {
            await _dependencyService.RemoveDependencyAsync(Guid.Parse(request.DependencyId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveDependency failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListSprintsResponse> ListSprints(ListSprintsRequest request, ServerCallContext context)
    {
        try
        {
            var sprints = await _sprintService.GetSprintsByEpicAsync(Guid.Parse(request.EpicId), context.CancellationToken);
            var response = new ListSprintsResponse { Success = true };
            foreach (var s in sprints)
                response.Sprints.Add(MapSprint(s));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListSprints failed");
            return new ListSprintsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintResponse> GetSprint(GetSprintRequest request, ServerCallContext context)
    {
        try
        {
            var sprint = await _sprintService.GetSprintAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            if (sprint is null)
                return new SprintResponse { Success = false, ErrorMessage = "Sprint not found" };
            return new SprintResponse { Success = true, Sprint = MapSprint(sprint) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSprint failed");
            return new SprintResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintResponse> CreateSprint(CreateSprintRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateSprintDto
            {
                Title = request.Title,
                Goal = string.IsNullOrEmpty(request.Goal) ? null : request.Goal,
                StartDate = string.IsNullOrEmpty(request.StartDate) ? null : DateTime.Parse(request.StartDate),
                EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate),
                DurationWeeks = request.DurationWeeks > 0 ? request.DurationWeeks : null,
                TargetStoryPoints = request.TargetStoryPoints > 0 ? request.TargetStoryPoints : null
            };
            var sprint = await _sprintService.CreateSprintAsync(Guid.Parse(request.EpicId), dto, context.CancellationToken);
            return new SprintResponse { Success = true, Sprint = MapSprint(sprint) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSprint failed");
            return new SprintResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintResponse> UpdateSprint(UpdateSprintRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateSprintDto
            {
                Title = string.IsNullOrEmpty(request.Title) ? null : request.Title,
                Goal = string.IsNullOrEmpty(request.Goal) ? null : request.Goal,
                StartDate = string.IsNullOrEmpty(request.StartDate) ? null : DateTime.Parse(request.StartDate),
                EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate),
                TargetStoryPoints = request.TargetStoryPoints > 0 ? request.TargetStoryPoints : null
            };
            var sprint = await _sprintService.UpdateSprintAsync(Guid.Parse(request.SprintId), dto, context.CancellationToken);
            return new SprintResponse { Success = true, Sprint = MapSprint(sprint) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateSprint failed");
            return new SprintResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteSprint(DeleteSprintRequest request, ServerCallContext context)
    {
        try
        {
            await _sprintService.DeleteSprintAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSprint failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintResponse> StartSprint(StartSprintRequest request, ServerCallContext context)
    {
        try
        {
            var sprint = await _sprintService.StartSprintAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            return new SprintResponse { Success = true, Sprint = MapSprint(sprint) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartSprint failed");
            return new SprintResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintResponse> CompleteSprint(CompleteSprintRequest request, ServerCallContext context)
    {
        try
        {
            var sprint = await _sprintService.CompleteSprintAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            return new SprintResponse { Success = true, Sprint = MapSprint(sprint) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompleteSprint failed");
            return new SprintResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> AddItemToSprint(AddItemToSprintRequest request, ServerCallContext context)
    {
        try
        {
            await _sprintService.AddItemToSprintAsync(Guid.Parse(request.SprintId), Guid.Parse(request.ItemId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddItemToSprint failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> RemoveItemFromSprint(RemoveItemFromSprintRequest request, ServerCallContext context)
    {
        try
        {
            await _sprintService.RemoveItemFromSprintAsync(Guid.Parse(request.SprintId), Guid.Parse(request.ItemId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveItemFromSprint failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListWorkItemsResponse> GetBacklogItems(GetBacklogItemsRequest request, ServerCallContext context)
    {
        try
        {
            var items = await _sprintService.GetBacklogItemsAsync(Guid.Parse(request.EpicId), context.CancellationToken);
            var response = new ListWorkItemsResponse { Success = true };
            foreach (var wi in items)
                response.WorkItems.Add(MapWorkItem(wi));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBacklogItems failed");
            return new ListWorkItemsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListTimeEntriesResponse> ListTimeEntries(ListTimeEntriesRequest request, ServerCallContext context)
    {
        try
        {
            var entries = await _timeTrackingService.GetTimeEntriesByWorkItemAsync(Guid.Parse(request.WorkItemId), context.CancellationToken);
            var response = new ListTimeEntriesResponse { Success = true };
            foreach (var e in entries)
                response.TimeEntries.Add(MapTimeEntry(e));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListTimeEntries failed");
            return new ListTimeEntriesResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TimeEntryResponse> CreateTimeEntry(CreateTimeEntryRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateTimeEntryDto
            {
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                StartTime = DateTime.TryParse(request.StartTime, out var st) ? st : null,
                DurationMinutes = request.DurationMinutes
            };
            var entry = await _timeTrackingService.AddManualEntryAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), dto, context.CancellationToken);
            return new TimeEntryResponse { Success = true, TimeEntry = MapTimeEntry(entry) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateTimeEntry failed");
            return new TimeEntryResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteTimeEntry(DeleteTimeEntryRequest request, ServerCallContext context)
    {
        try
        {
            await _timeTrackingService.DeleteEntryAsync(Guid.Parse(request.EntryId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteTimeEntry failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TimeEntryResponse> StartTimer(StartTimerRequest request, ServerCallContext context)
    {
        try
        {
            var entry = await _timeTrackingService.StartTimerAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), context.CancellationToken);
            return new TimeEntryResponse { Success = true, TimeEntry = MapTimeEntry(entry) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartTimer failed");
            return new TimeEntryResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TimeEntryResponse> StopTimer(StopTimerRequest request, ServerCallContext context)
    {
        try
        {
            var entry = await _timeTrackingService.StopTimerAsync(Guid.Parse(request.WorkItemId), Guid.Parse(request.UserId), context.CancellationToken);
            return new TimeEntryResponse { Success = true, TimeEntry = MapTimeEntry(entry) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StopTimer failed");
            return new TimeEntryResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListActivityResponse> GetProductActivity(GetProductActivityRequest request, ServerCallContext context)
    {
        try
        {
            var activities = await _activityService.GetActivitiesByProductAsync(Guid.Parse(request.ProductId), request.Skip, request.Take > 0 ? request.Take : 50, context.CancellationToken);
            var response = new ListActivityResponse { Success = true };
            foreach (var a in activities)
                response.Activities.Add(new ActivityMessage { Id = a.Id.ToString(), ProductId = a.ProductId.ToString(), UserId = a.UserId.ToString(), DisplayName = a.DisplayName ?? "", Action = a.Action ?? "", EntityType = a.EntityType ?? "", EntityId = a.EntityId.ToString(), Details = a.Details ?? "", CreatedAt = a.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProductActivity failed");
            return new ListActivityResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListActivityResponse> GetWorkItemActivity(GetWorkItemActivityRequest request, ServerCallContext context)
    {
        try
        {
            var activities = await _activityService.GetActivitiesByWorkItemAsync(Guid.Parse(request.WorkItemId), request.Skip, request.Take > 0 ? request.Take : 50, context.CancellationToken);
            var response = new ListActivityResponse { Success = true };
            foreach (var a in activities)
                response.Activities.Add(new ActivityMessage { Id = a.Id.ToString(), ProductId = a.ProductId.ToString(), UserId = a.UserId.ToString(), DisplayName = a.DisplayName ?? "", Action = a.Action ?? "", EntityType = a.EntityType ?? "", EntityId = a.EntityId.ToString(), Details = a.Details ?? "", CreatedAt = a.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWorkItemActivity failed");
            return new ListActivityResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ProductAnalyticsResponse> GetProductAnalytics(GetProductAnalyticsRequest request, ServerCallContext context)
    {
        try
        {
            var a = await _analyticsService.GetProductAnalyticsAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var analytics = new ProductAnalyticsMessage { TotalItems = a.TotalItems, TotalEpics = a.TotalEpics, TotalFeatures = a.TotalFeatures, ItemsCompletedThisWeek = a.ItemsCompletedThisWeek, ActiveSprints = a.ActiveSprints, AvgCycleTimeDays = a.AvgCycleTimeDays };
            foreach (var dc in a.DailyCompletions)
                analytics.DailyCompletions.Add(new DailyCompletionMessage { Date = dc.Date.ToString("O"), CompletedCount = dc.CompletedCount });
            return new ProductAnalyticsResponse { Success = true, Analytics = analytics };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProductAnalytics failed");
            return new ProductAnalyticsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GetVelocityDataResponse> GetVelocityData(GetVelocityDataRequest request, ServerCallContext context)
    {
        try
        {
            var data = await _analyticsService.GetVelocityDataAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new GetVelocityDataResponse { Success = true };
            foreach (var v in data)
                response.Velocities.Add(new SprintVelocityMessage { SprintId = v.SprintId.ToString(), SprintTitle = v.SprintTitle ?? "", CompletedStoryPoints = v.CompletedStoryPoints, TotalStoryPoints = v.TotalStoryPoints });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetVelocityData failed");
            return new GetVelocityDataResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintReportResponse> GetSprintReport(GetSprintReportRequest request, ServerCallContext context)
    {
        try
        {
            var r = await _analyticsService.GetSprintReportAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            return new SprintReportResponse { Success = true, Report = new SprintReportMessage { Sprint = MapSprint(r.Sprint), CompletedItems = r.CompletedItems, IncompleteItems = r.IncompleteItems, CompletedStoryPoints = r.CompletedStoryPoints, TotalStoryPoints = r.TotalStoryPoints } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSprintReport failed");
            return new SprintReportResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<BurndownDataResponse> GetBurndownData(GetBurndownDataRequest request, ServerCallContext context)
    {
        try
        {
            var data = await _analyticsService.GetBurndownDataAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            var response = new BurndownDataResponse { Success = true, Burndown = new BurndownMessage { TotalStoryPoints = data.TotalStoryPoints } };
            foreach (var dp in data.Points)
                response.Burndown.Points.Add(new BurndownPointMessage { Date = dp.Date.ToString("O"), RemainingStoryPoints = dp.RemainingStoryPoints });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBurndownData failed");
            return new BurndownDataResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ProductDashboardResponse> GetProductDashboard(GetProductDashboardRequest request, ServerCallContext context)
    {
        try
        {
            var d = await _analyticsService.GetProductDashboardAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var dashboard = new ProductDashboardMessage { ProductId = d.ProductId.ToString(), ProductName = d.ProductName ?? "", TotalItems = d.TotalItems, TotalEpics = d.TotalEpics, TotalFeatures = d.TotalFeatures, ActiveSprints = d.ActiveSprints, AvgCycleTimeDays = d.AvgCycleTimeDays, ItemsCompletedThisWeek = d.ItemsCompletedThisWeek, UnassignedItems = d.UnassignedItems };
            foreach (var sb in d.StatusBreakdown)
                dashboard.StatusBreakdown.Add(new StatusBreakdownMessage { SwimlaneId = sb.SwimlaneId.ToString(), SwimlaneTitle = sb.SwimlaneTitle, Color = sb.Color ?? "", Count = sb.Count });
            foreach (var pb in d.PriorityBreakdown)
                dashboard.PriorityBreakdown.Add(new PriorityBreakdownMessage { Priority = pb.Priority.ToString(), Count = pb.Count });
            foreach (var wl in d.Workload)
                dashboard.Workload.Add(new WorkloadMessage { UserId = wl.UserId.ToString(), DisplayName = wl.DisplayName ?? "", AssignedItems = wl.AssignedItems, TotalStoryPoints = wl.TotalStoryPoints });
            foreach (var ru in d.RecentlyUpdated)
                dashboard.RecentlyUpdated.Add(new RecentlyUpdatedItemMessage { Id = ru.Id.ToString(), ItemNumber = ru.ItemNumber, Title = ru.Title, Type = ru.Type.ToString(), Priority = ru.Priority.ToString(), SwimlaneTitle = ru.SwimlaneTitle ?? "", SprintId = ru.SprintId?.ToString() ?? "", SprintTitle = ru.SprintTitle ?? "", UpdatedAt = ru.UpdatedAt.ToString("O") });
            foreach (var ud in d.UpcomingDueDates)
                dashboard.UpcomingDueDates.Add(new UpcomingDueDateMessage { Id = ud.Id.ToString(), ItemNumber = ud.ItemNumber, Title = ud.Title, Type = ud.Type.ToString(), Priority = ud.Priority.ToString(), SwimlaneTitle = ud.SwimlaneTitle ?? "", DueDate = ud.DueDate.ToString("O") });
            return new ProductDashboardResponse { Success = true, Dashboard = dashboard };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProductDashboard failed");
            return new ProductDashboardResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<BulkWorkItemActionResponse> BulkWorkItemAction(BulkWorkItemActionRequest request, ServerCallContext context)
    {
        try
        {
            var userId = Guid.Parse(request.UserId);
            int count = 0;
            foreach (var itemId in request.WorkItemIds)
            {
                var wid = Guid.Parse(itemId);
                switch (request.Action)
                {
                    case "delete":
                        await _workItemService.DeleteWorkItemAsync(wid, userId, context.CancellationToken);
                        count++;
                        break;
                    case "archive":
                    {
                        var adto = new UpdateWorkItemDto { IsArchived = true };
                        await _workItemService.UpdateWorkItemAsync(wid, adto, context.CancellationToken);
                        count++;
                        break;
                    }
                    case "move":
                        if (!string.IsNullOrEmpty(request.TargetSwimlaneId))
                        {
                            var mdto = new MoveWorkItemDto { TargetSwimlaneId = Guid.Parse(request.TargetSwimlaneId), Position = 0 };
                            await _workItemService.MoveWorkItemAsync(wid, mdto, context.CancellationToken);
                            count++;
                        }
                        break;
                    case "add-label":
                        if (!string.IsNullOrEmpty(request.LabelId))
                        {
                            await _workItemService.AddLabelAsync(wid, Guid.Parse(request.LabelId), context.CancellationToken);
                            count++;
                        }
                        break;
                    case "assign":
                        if (!string.IsNullOrEmpty(request.AssigneeUserId))
                        {
                            await _workItemService.AssignUserAsync(wid, Guid.Parse(request.AssigneeUserId), context.CancellationToken);
                            count++;
                        }
                        break;
                    case "set-priority":
                        if (!string.IsNullOrEmpty(request.Priority) && Enum.TryParse<Priority>(request.Priority, true, out var pr))
                        {
                            var udto = new UpdateWorkItemDto { Priority = pr };
                            await _workItemService.UpdateWorkItemAsync(wid, udto, context.CancellationToken);
                            count++;
                        }
                        break;
                    case "assign-to-sprint":
                        if (!string.IsNullOrEmpty(request.SprintId))
                        {
                            await _sprintService.AddItemToSprintAsync(Guid.Parse(request.SprintId), wid, context.CancellationToken);
                            count++;
                        }
                        break;
                }
            }
            return new BulkWorkItemActionResponse { Success = true, AffectedCount = count };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BulkWorkItemAction failed");
            return new BulkWorkItemActionResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListWorkItemsResponse> ListProductWorkItems(ListProductWorkItemsRequest request, ServerCallContext context)
    {
        try
        {
            var productId = Guid.Parse(request.ProductId);
            var query = _db.WorkItems.Where(wi => wi.ProductId == productId && !wi.IsDeleted);
            if (!string.IsNullOrEmpty(request.SwimlaneId))
                query = query.Where(wi => wi.SwimlaneId == Guid.Parse(request.SwimlaneId));
            var workItems = await query.Include(wi => wi.Swimlane).Include(wi => wi.Assignments).Include(wi => wi.WorkItemLabels).ThenInclude(wl => wl.Label).OrderBy(wi => wi.ItemNumber).ToListAsync(context.CancellationToken);
            var response = new ListWorkItemsResponse { Success = true };
            foreach (var wi in workItems)
            {
                var dto = await _workItemService.GetWorkItemAsync(wi.Id, context.CancellationToken);
                response.WorkItems.Add(MapWorkItem(dto));
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListProductWorkItems failed");
            return new ListWorkItemsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListTeamsResponse> ListTeams(ListTeamsRequest request, ServerCallContext context)
    {
        try
        {
            var teams = await _db.Teams.ToListAsync(context.CancellationToken);
            var response = new ListTeamsResponse { Success = true };
            foreach (var t in teams)
            {
                var memberCount = await _db.TeamRoles.CountAsync(tr => tr.TeamId == t.Id, context.CancellationToken);
                response.Teams.Add(new TracksTeamMessage { Id = t.Id.ToString(), TeamId = t.Id.ToString(), Name = t.Name, Description = t.Description ?? "", MemberCount = memberCount, CreatedAt = t.CreatedAt.ToString("O") });
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListTeams failed");
            return new ListTeamsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TeamResponse> GetTeam(GetTeamRequest request, ServerCallContext context)
    {
        try
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == Guid.Parse(request.TeamId), context.CancellationToken);
            if (team is null)
                return new TeamResponse { Success = false, ErrorMessage = "Team not found" };
            var memberCount = await _db.TeamRoles.CountAsync(tr => tr.TeamId == team.Id, context.CancellationToken);
            return new TeamResponse { Success = true, Team = new TracksTeamMessage { Id = team.Id.ToString(), TeamId = team.Id.ToString(), Name = team.Name, Description = team.Description ?? "", MemberCount = memberCount, CreatedAt = team.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTeam failed");
            return new TeamResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TeamResponse> CreateTeam(CreateTeamRequest request, ServerCallContext context)
    {
        try
        {
            var team = new Team { Name = request.Name, Description = string.IsNullOrEmpty(request.Description) ? null : request.Description, CreatedByUserId = Guid.Parse(request.UserId) };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(context.CancellationToken);
            return new TeamResponse { Success = true, Team = new TracksTeamMessage { Id = team.Id.ToString(), TeamId = team.Id.ToString(), Name = team.Name, Description = team.Description ?? "", MemberCount = 0, CreatedAt = team.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateTeam failed");
            return new TeamResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TeamResponse> UpdateTeam(UpdateTeamRequest request, ServerCallContext context)
    {
        try
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == Guid.Parse(request.TeamId), context.CancellationToken);
            if (team is null)
                return new TeamResponse { Success = false, ErrorMessage = "Team not found" };
            if (!string.IsNullOrEmpty(request.Name))
                team.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description))
                team.Description = request.Description;
            await _db.SaveChangesAsync(context.CancellationToken);
            var memberCount = await _db.TeamRoles.CountAsync(tr => tr.TeamId == team.Id, context.CancellationToken);
            return new TeamResponse { Success = true, Team = new TracksTeamMessage { Id = team.Id.ToString(), TeamId = team.Id.ToString(), Name = team.Name, Description = team.Description ?? "", MemberCount = memberCount, CreatedAt = team.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateTeam failed");
            return new TeamResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteTeam(DeleteTeamRequest request, ServerCallContext context)
    {
        try
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == Guid.Parse(request.TeamId), context.CancellationToken);
            if (team is not null)
            {
                var roles = await _db.TeamRoles.Where(tr => tr.TeamId == team.Id).ToListAsync(context.CancellationToken);
                _db.TeamRoles.RemoveRange(roles);
                _db.Teams.Remove(team);
                await _db.SaveChangesAsync(context.CancellationToken);
            }
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteTeam failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListTeamMembersResponse> ListTeamMembers(ListTeamMembersRequest request, ServerCallContext context)
    {
        try
        {
            var members = await _db.TeamRoles.Where(tr => tr.TeamId == Guid.Parse(request.TeamId)).ToListAsync(context.CancellationToken);
            var response = new ListTeamMembersResponse { Success = true };
            foreach (var m in members)
                response.Members.Add(new TracksTeamMemberMessage { UserId = m.UserId.ToString(), DisplayName = "", Role = m.Role.ToString(), AssignedAt = m.AssignedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListTeamMembers failed");
            return new ListTeamMembersResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TeamMemberResponse> AddTeamMember(AddTeamMemberRequest request, ServerCallContext context)
    {
        try
        {
            var role = Enum.TryParse<TracksTeamMemberRole>(request.Role, true, out var r) ? r : TracksTeamMemberRole.Member;
            var member = new TeamRole { TeamId = Guid.Parse(request.TeamId), UserId = Guid.Parse(request.MemberUserId), Role = role };
            _db.TeamRoles.Add(member);
            await _db.SaveChangesAsync(context.CancellationToken);
            return new TeamMemberResponse { Success = true, Member = new TracksTeamMemberMessage { UserId = member.UserId.ToString(), DisplayName = "", Role = member.Role.ToString(), AssignedAt = member.AssignedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddTeamMember failed");
            return new TeamMemberResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> RemoveTeamMember(RemoveTeamMemberRequest request, ServerCallContext context)
    {
        try
        {
            var member = await _db.TeamRoles.FirstOrDefaultAsync(tr => tr.TeamId == Guid.Parse(request.TeamId) && tr.UserId == Guid.Parse(request.MemberUserId), context.CancellationToken);
            if (member is not null)
            {
                _db.TeamRoles.Remove(member);
                await _db.SaveChangesAsync(context.CancellationToken);
            }
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveTeamMember failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TeamMemberResponse> UpdateTeamMemberRole(UpdateTeamMemberRoleRequest request, ServerCallContext context)
    {
        try
        {
            var member = await _db.TeamRoles.FirstOrDefaultAsync(tr => tr.TeamId == Guid.Parse(request.TeamId) && tr.UserId == Guid.Parse(request.MemberUserId), context.CancellationToken);
            if (member is null)
                return new TeamMemberResponse { Success = false, ErrorMessage = "Team member not found" };
            member.Role = Enum.TryParse<TracksTeamMemberRole>(request.Role, true, out var r) ? r : TracksTeamMemberRole.Member;
            await _db.SaveChangesAsync(context.CancellationToken);
            return new TeamMemberResponse { Success = true, Member = new TracksTeamMemberMessage { UserId = member.UserId.ToString(), DisplayName = "", Role = member.Role.ToString(), AssignedAt = member.AssignedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateTeamMemberRole failed");
            return new TeamMemberResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> LeaveReviewSession(LeaveReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            await _reviewSessionService.LeaveSessionAsync(Guid.Parse(request.SessionId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LeaveReviewSession failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<PokerSessionResponse> GetPokerSession(GetPokerSessionRequest request, ServerCallContext context)
    {
        try
        {
            var session = await _pokerService.GetSessionAsync(Guid.Parse(request.SessionId), context.CancellationToken);
            if (session is null)
                return new PokerSessionResponse { Success = false, ErrorMessage = "Poker session not found" };
            return new PokerSessionResponse { Success = true, Session = MapPokerSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPokerSession failed");
            return new PokerSessionResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<PokerSessionResponse> GetActivePokerSessionByReviewSession(GetActivePokerSessionByReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            var reviewSessionId = Guid.Parse(request.ReviewSessionId);
            var sessionEntity = await _db.PokerSessions.FirstOrDefaultAsync(ps => ps.ReviewSessionId == reviewSessionId && ps.Status != PokerSessionStatus.Completed, context.CancellationToken);
            if (sessionEntity is null)
                return new PokerSessionResponse { Success = false, ErrorMessage = "No active poker session found" };
            var dto = new PokerSessionDto { Id = sessionEntity.Id, ItemId = sessionEntity.ItemId, EpicId = sessionEntity.EpicId, CreatedByUserId = sessionEntity.CreatedByUserId, Scale = sessionEntity.Scale, CustomScaleValues = sessionEntity.CustomScaleValues, Status = sessionEntity.Status, AcceptedEstimate = sessionEntity.AcceptedEstimate, Round = sessionEntity.Round, CreatedAt = sessionEntity.CreatedAt, UpdatedAt = sessionEntity.UpdatedAt };
            return new PokerSessionResponse { Success = true, Session = MapPokerSession(dto) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActivePokerSessionByReviewSession failed");
            return new PokerSessionResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SearchUsersResponse> SearchUsers(SearchUsersRequest request, ServerCallContext context)
    {
        try
        {
            var searchTerm = request.SearchTerm?.Trim() ?? string.Empty;
            var maxResults = request.MaxResults > 0 ? request.MaxResults : 8;
            var response = new SearchUsersResponse { Success = true };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                try
                {
                    var results = await _userDirectory.SearchUsersAsync(searchTerm, maxResults, context.CancellationToken);
                    foreach (var r in results.Take(maxResults))
                    {
                        response.Results.Add(new UserSearchResultMessage
                        {
                            Id = r.Id.ToString(),
                            DisplayName = r.DisplayName ?? string.Empty,
                            Email = r.Email ?? string.Empty
                        });
                    }
                }
                catch
                {
                    // IUserDirectory search not available — return empty results gracefully
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchUsers failed");
            return new SearchUsersResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListCustomViewsResponse> ListCustomViews(ListCustomViewsRequest request, ServerCallContext context)
    {
        try
        {
            var views = await _customViewService.GetViewsForProductAsync(Guid.Parse(request.ProductId), Guid.Parse(request.UserId), context.CancellationToken);
            var response = new ListCustomViewsResponse { Success = true };
            foreach (var v in views)
                response.Views.Add(new CustomViewMessage { Id = v.Id.ToString(), ProductId = v.ProductId.ToString(), UserId = v.UserId.ToString(), Name = v.Name, FilterJson = v.FilterJson ?? "", SortJson = v.SortJson ?? "", GroupBy = v.GroupBy ?? "", Layout = v.Layout ?? "", IsShared = v.IsShared, CreatedAt = v.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListCustomViews failed");
            return new ListCustomViewsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<CustomViewResponse> CreateCustomView(CreateCustomViewRequest request, ServerCallContext context)
    {
        try
        {
            var view = await _customViewService.CreateViewAsync(Guid.Parse(request.ProductId), Guid.Parse(request.UserId), request.Name, request.FilterJson ?? "", request.SortJson ?? "", string.IsNullOrEmpty(request.GroupBy) ? null : request.GroupBy, string.IsNullOrEmpty(request.Layout) ? "board" : request.Layout, request.IsShared, context.CancellationToken);
            return new CustomViewResponse { Success = true, View = new CustomViewMessage { Id = view.Id.ToString(), ProductId = view.ProductId.ToString(), UserId = view.UserId.ToString(), Name = view.Name, FilterJson = view.FilterJson ?? "", SortJson = view.SortJson ?? "", GroupBy = view.GroupBy ?? "", Layout = view.Layout ?? "", IsShared = view.IsShared, CreatedAt = view.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCustomView failed");
            return new CustomViewResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<CustomViewResponse> UpdateCustomView(UpdateCustomViewRequest request, ServerCallContext context)
    {
        try
        {
            var view = await _customViewService.UpdateViewAsync(Guid.Parse(request.ViewId), Guid.Parse(request.UserId), string.IsNullOrEmpty(request.Name) ? null : request.Name, string.IsNullOrEmpty(request.FilterJson) ? null : request.FilterJson, string.IsNullOrEmpty(request.SortJson) ? null : request.SortJson, string.IsNullOrEmpty(request.GroupBy) ? null : request.GroupBy, string.IsNullOrEmpty(request.Layout) ? null : request.Layout, request.IsShared, context.CancellationToken);
            if (view is null)
                return new CustomViewResponse { Success = false, ErrorMessage = "Custom view not found" };
            return new CustomViewResponse { Success = true, View = new CustomViewMessage { Id = view.Id.ToString(), ProductId = view.ProductId.ToString(), UserId = view.UserId.ToString(), Name = view.Name, FilterJson = view.FilterJson ?? "", SortJson = view.SortJson ?? "", GroupBy = view.GroupBy ?? "", Layout = view.Layout ?? "", IsShared = view.IsShared, CreatedAt = view.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateCustomView failed");
            return new CustomViewResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteCustomView(DeleteCustomViewRequest request, ServerCallContext context)
    {
        try
        {
            var deleted = await _customViewService.DeleteViewAsync(Guid.Parse(request.ViewId), Guid.Parse(request.UserId), context.CancellationToken);
            return new GenericResponse { Success = deleted };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteCustomView failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListProductWebhooksResponse> ListProductWebhooks(ListProductWebhooksRequest request, ServerCallContext context)
    {
        try
        {
            var subs = await _webhookService.GetSubscriptionsAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new ListProductWebhooksResponse { Success = true };
            foreach (var s in subs)
                response.Subscriptions.Add(new WebhookSubscriptionMessage { Id = s.Id.ToString(), ProductId = s.ProductId.ToString(), Url = s.Url, EventsJson = s.EventsJson ?? "", IsActive = s.IsActive, CreatedByUserId = s.CreatedByUserId.ToString(), CreatedAt = s.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListProductWebhooks failed");
            return new ListProductWebhooksResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WebhookSubscriptionResponse> CreateProductWebhook(CreateProductWebhookRequest request, ServerCallContext context)
    {
        try
        {
            var eventTypesList = request.EventTypes != null && request.EventTypes.Count > 0 ? new List<string>(request.EventTypes) : new List<string>();
            var sub = await _webhookService.CreateSubscriptionAsync(Guid.Parse(request.ProductId), Guid.Parse(request.UserId), request.Url, eventTypesList, context.CancellationToken);
            return new WebhookSubscriptionResponse { Success = true, Subscription = new WebhookSubscriptionMessage { Id = sub.Id.ToString(), ProductId = sub.ProductId.ToString(), Url = sub.Url, EventsJson = sub.EventsJson ?? "", IsActive = sub.IsActive, CreatedByUserId = sub.CreatedByUserId.ToString(), CreatedAt = sub.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateProductWebhook failed");
            return new WebhookSubscriptionResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<WebhookSubscriptionResponse> UpdateProductWebhook(UpdateProductWebhookRequest request, ServerCallContext context)
    {
        try
        {
            var eventTypesList = request.EventTypes != null && request.EventTypes.Count > 0 ? new List<string>(request.EventTypes) : null;
            var sub = await _webhookService.UpdateSubscriptionAsync(Guid.Parse(request.SubscriptionId), request.Url ?? "", eventTypesList ?? new List<string>(), request.IsActive, context.CancellationToken);
            if (sub is null)
                return new WebhookSubscriptionResponse { Success = false, ErrorMessage = "Webhook not found" };
            return new WebhookSubscriptionResponse { Success = true, Subscription = new WebhookSubscriptionMessage { Id = sub.Id.ToString(), ProductId = sub.ProductId.ToString(), Url = sub.Url, EventsJson = sub.EventsJson ?? "", IsActive = sub.IsActive, CreatedByUserId = sub.CreatedByUserId.ToString(), CreatedAt = sub.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProductWebhook failed");
            return new WebhookSubscriptionResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteProductWebhook(DeleteProductWebhookRequest request, ServerCallContext context)
    {
        try
        {
            var deleted = await _webhookService.DeleteSubscriptionAsync(Guid.Parse(request.SubscriptionId), context.CancellationToken);
            return new GenericResponse { Success = deleted };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteProductWebhook failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<TestProductWebhookResponse> TestProductWebhook(TestProductWebhookRequest request, ServerCallContext context)
    {
        try
        {
            var subscription = await _webhookService.GetSubscriptionAsync(
                Guid.Parse(request.SubscriptionId), context.CancellationToken);

            if (subscription is null)
                return new TestProductWebhookResponse { Success = false, ErrorMessage = "Webhook subscription not found" };

            // Send a test ping to the webhook URL
            try
            {
                var httpClient = context.GetHttpContext().RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("WebhookClient");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                var httpResponse = await httpClient.PostAsync(subscription.Url,
                    new StringContent("{\"type\":\"ping\",\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\"}",
                        System.Text.Encoding.UTF8, "application/json"), cts.Token);
                sw.Stop();

                return new TestProductWebhookResponse
                {
                    Success = true,
                    DeliverySuccess = httpResponse.IsSuccessStatusCode,
                    StatusCode = (int)httpResponse.StatusCode,
                    DurationMs = sw.ElapsedMilliseconds,
                    Error = httpResponse.IsSuccessStatusCode ? string.Empty : $"HTTP {(int)httpResponse.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new TestProductWebhookResponse
                {
                    Success = true,
                    DeliverySuccess = false,
                    StatusCode = 0,
                    DurationMs = 0,
                    Error = ex.Message
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TestProductWebhook failed");
            return new TestProductWebhookResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<RoadmapDataResponse> GetRoadmapData(GetRoadmapDataRequest request, ServerCallContext context)
    {
        try
        {
            var data = await _analyticsService.GetRoadmapDataAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var roadmap = new RoadmapDataMessage { ProductId = data.ProductId.ToString(), ProductName = data.ProductName ?? "" };
            foreach (var item in data.Items)
            {
                var msg = new RoadmapItemMessage
                {
                    Id = item.Id.ToString(),
                    ItemNumber = item.ItemNumber,
                    Title = item.Title,
                    Type = item.Type.ToString(),
                    Priority = item.Priority.ToString(),
                    SwimlaneTitle = item.SwimlaneTitle ?? "",
                    SwimlaneColor = item.SwimlaneColor ?? "",
                    StartDate = item.StartDate?.ToString("O") ?? "",
                    DueDate = item.DueDate?.ToString("O") ?? "",
                    MilestoneId = item.MilestoneId?.ToString() ?? "",
                    MilestoneTitle = item.MilestoneTitle ?? "",
                    AssigneeUserId = item.AssigneeUserId?.ToString() ?? "",
                    AssigneeDisplayName = item.AssigneeDisplayName ?? ""
                };
                msg.DependencyIds.AddRange(item.DependencyIds.Select(id => id.ToString()));
                roadmap.Items.Add(msg);
            }
            foreach (var ml in data.Milestones)
                roadmap.Milestones.Add(new MilestoneMessage
                {
                    Id = ml.Id.ToString(),
                    ProductId = ml.ProductId.ToString(),
                    Title = ml.Title,
                    Description = ml.Description ?? "",
                    DueDate = ml.DueDate?.ToString("O") ?? "",
                    Status = ml.Status.ToString(),
                    Color = ml.Color ?? "",
                    WorkItemCount = ml.WorkItemCount,
                    CompletedWorkItemCount = ml.CompletedWorkItemCount,
                    CreatedAt = ml.CreatedAt.ToString("O"),
                    UpdatedAt = ml.UpdatedAt.ToString("O")
                });
            return new RoadmapDataResponse { Success = true, Roadmap = roadmap };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRoadmapData failed");
            return new RoadmapDataResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListAutomationRulesResponse> ListAutomationRules(ListAutomationRulesRequest request, ServerCallContext context)
    {
        try
        {
            var rules = await _automationRuleService.ListAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new ListAutomationRulesResponse { Success = true };
            foreach (var r in rules)
                response.Rules.Add(new AutomationRuleMessage { Id = r.Id.ToString(), ProductId = r.ProductId.ToString(), Name = r.Name, Trigger = r.Trigger, IsActive = r.IsActive, CreatedAt = r.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListAutomationRules failed");
            return new ListAutomationRulesResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<AutomationRuleResponse> CreateAutomationRule(CreateAutomationRuleRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateAutomationRuleDto { Name = request.Name, Trigger = request.Trigger, ConditionsJson = request.ConditionsJson, ActionsJson = request.ActionsJson };
            var rule = await _automationRuleService.CreateAsync(Guid.Parse(request.ProductId), dto, Guid.Parse(request.UserId), context.CancellationToken);
            return new AutomationRuleResponse { Success = true, Rule = new AutomationRuleMessage { Id = rule.Id.ToString(), ProductId = rule.ProductId.ToString(), Name = rule.Name, Trigger = rule.Trigger, IsActive = rule.IsActive, CreatedAt = rule.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateAutomationRule failed");
            return new AutomationRuleResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<AutomationRuleResponse> UpdateAutomationRule(UpdateAutomationRuleRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateAutomationRuleDto { Name = string.IsNullOrEmpty(request.Name) ? null : request.Name, Trigger = string.IsNullOrEmpty(request.Trigger) ? null : request.Trigger, ConditionsJson = string.IsNullOrEmpty(request.ConditionsJson) ? null : request.ConditionsJson, ActionsJson = string.IsNullOrEmpty(request.ActionsJson) ? null : request.ActionsJson, IsActive = request.IsActive };
            var rule = await _automationRuleService.UpdateAsync(Guid.Parse(request.RuleId), dto, context.CancellationToken);
            if (rule is null)
                return new AutomationRuleResponse { Success = false, ErrorMessage = "Automation rule not found" };
            return new AutomationRuleResponse { Success = true, Rule = new AutomationRuleMessage { Id = rule.Id.ToString(), ProductId = rule.ProductId.ToString(), Name = rule.Name, Trigger = rule.Trigger, IsActive = rule.IsActive, CreatedAt = rule.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAutomationRule failed");
            return new AutomationRuleResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteAutomationRule(DeleteAutomationRuleRequest request, ServerCallContext context)
    {
        try
        {
            var deleted = await _automationRuleService.DeleteAsync(Guid.Parse(request.RuleId), context.CancellationToken);
            return new GenericResponse { Success = deleted };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAutomationRule failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListGoalsResponse> ListGoals(ListGoalsRequest request, ServerCallContext context)
    {
        try
        {
            var goals = await _goalService.ListAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new ListGoalsResponse { Success = true };
            foreach (var g in goals)
                response.Goals.Add(new GoalMessage { Id = g.Id.ToString(), ProductId = g.ProductId.ToString(), Title = g.Title, Description = g.Description ?? "", Type = g.Type ?? "", Status = g.Status ?? "", ProgressPercent = g.TargetValue > 0 && g.CurrentValue > 0 ? ((g.CurrentValue ?? 0) / (g.TargetValue ?? 1)) * 100 : 0, CreatedAt = g.CreatedAt.ToString("O") });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListGoals failed");
            return new ListGoalsResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GoalResponse> GetGoal(GetGoalRequest request, ServerCallContext context)
    {
        try
        {
            var goal = await _goalService.GetAsync(Guid.Parse(request.GoalId), context.CancellationToken);
            if (goal is null)
                return new GoalResponse { Success = false, ErrorMessage = "Goal not found" };
            return new GoalResponse { Success = true, Goal = new GoalMessage { Id = goal.Id.ToString(), ProductId = goal.ProductId.ToString(), Title = goal.Title, Description = goal.Description ?? "", Type = goal.Type ?? "", Status = goal.Status ?? "", ProgressPercent = goal.TargetValue > 0 && goal.CurrentValue > 0 ? ((goal.CurrentValue ?? 0) / (goal.TargetValue ?? 1)) * 100 : 0, CreatedAt = goal.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGoal failed");
            return new GoalResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GoalResponse> CreateGoal(CreateGoalRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateGoalDto { Title = request.Title, Description = string.IsNullOrEmpty(request.Description) ? null : request.Description, Type = !string.IsNullOrEmpty(request.Type) ? request.Type : "objective" };
            var goal = await _goalService.CreateAsync(Guid.Parse(request.ProductId), dto, Guid.Parse(request.UserId), context.CancellationToken);
            return new GoalResponse { Success = true, Goal = new GoalMessage { Id = goal.Id.ToString(), ProductId = goal.ProductId.ToString(), Title = goal.Title, Description = goal.Description ?? "", Type = goal.Type ?? "", Status = goal.Status ?? "", ProgressPercent = goal.TargetValue > 0 && goal.CurrentValue > 0 ? ((goal.CurrentValue ?? 0) / (goal.TargetValue ?? 1)) * 100 : 0, CreatedAt = goal.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateGoal failed");
            return new GoalResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GoalResponse> UpdateGoal(UpdateGoalRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new UpdateGoalDto { Title = string.IsNullOrEmpty(request.Title) ? null : request.Title, Description = string.IsNullOrEmpty(request.Description) ? null : request.Description, Status = string.IsNullOrEmpty(request.Status) ? null : request.Status };
            var goal = await _goalService.UpdateAsync(Guid.Parse(request.GoalId), dto, context.CancellationToken);
            if (goal is null)
                return new GoalResponse { Success = false, ErrorMessage = "Goal not found" };
            return new GoalResponse { Success = true, Goal = new GoalMessage { Id = goal.Id.ToString(), ProductId = goal.ProductId.ToString(), Title = goal.Title, Description = goal.Description ?? "", Type = goal.Type ?? "", Status = goal.Status ?? "", ProgressPercent = goal.TargetValue > 0 && goal.CurrentValue > 0 ? ((goal.CurrentValue ?? 0) / (goal.TargetValue ?? 1)) * 100 : 0, CreatedAt = goal.CreatedAt.ToString("O") } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGoal failed");
            return new GoalResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteGoal(DeleteGoalRequest request, ServerCallContext context)
    {
        try
        {
            var deleted = await _goalService.DeleteAsync(Guid.Parse(request.GoalId), context.CancellationToken);
            return new GenericResponse { Success = deleted };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteGoal failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> LinkGoalWorkItem(LinkGoalWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var linked = await _goalService.LinkWorkItemAsync(Guid.Parse(request.GoalId), Guid.Parse(request.WorkItemId), context.CancellationToken);
            return new GenericResponse { Success = linked };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LinkGoalWorkItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<GenericResponse> UnlinkGoalWorkItem(UnlinkGoalWorkItemRequest request, ServerCallContext context)
    {
        try
        {
            var unlinked = await _goalService.UnlinkWorkItemAsync(Guid.Parse(request.GoalId), Guid.Parse(request.WorkItemId), context.CancellationToken);
            return new GenericResponse { Success = unlinked };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnlinkGoalWorkItem failed");
            return new GenericResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ProductCapacityResponse> GetProductCapacity(GetProductCapacityRequest request, ServerCallContext context)
    {
        try
        {
            var capacity = await _analyticsService.GetProductCapacityAsync(Guid.Parse(request.ProductId), context.CancellationToken);
            var response = new ProductCapacityResponse { Success = true, Capacity = new ProductCapacityMessage { ProductId = capacity.ProductId.ToString(), TotalAssignedStoryPoints = capacity.TotalAssignedStoryPoints, TotalMembers = capacity.TotalMembers, OverloadedMembers = capacity.OverloadedMembers } };
            foreach (var m in capacity.Members)
            {
                var mc = new MemberCapacityMessage { UserId = m.UserId.ToString(), DisplayName = m.DisplayName ?? "", AssignedStoryPoints = m.AssignedStoryPoints, AssignedItemCount = m.AssignedItemCount, CapacityPercent = m.CapacityPercent };
                mc.SprintTitles.AddRange(m.SprintTitles);
                response.Capacity.Members.Add(mc);
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProductCapacity failed");
            return new ProductCapacityResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<SprintCapacityResponse> GetSprintCapacity(GetSprintCapacityRequest request, ServerCallContext context)
    {
        try
        {
            var capacity = await _analyticsService.GetSprintCapacityAsync(Guid.Parse(request.SprintId), context.CancellationToken);
            return new SprintCapacityResponse { Success = true, Capacity = new SprintCapacityMessage { SprintId = capacity.SprintId.ToString(), SprintTitle = capacity.SprintTitle ?? "", TotalStoryPoints = capacity.TotalStoryPoints, TargetStoryPoints = capacity.TargetStoryPoints, CompletedStoryPoints = capacity.CompletedStoryPoints } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSprintCapacity failed");
            return new SprintCapacityResponse() { Success = false, ErrorMessage = ex.Message };
        }
    }
    /// <inheritdoc />
    public override async Task<ListSprintDiscussionsResponse> ListSprintDiscussions(ListSprintDiscussionsRequest request, ServerCallContext context)
    {
        try
        {
            var messages = await _discussionService.GetSprintMessagesAsync(Guid.Parse(request.SprintId), skip: request.Skip, take: request.Take > 0 ? request.Take : 50, ct: context.CancellationToken);
            var response = new ListSprintDiscussionsResponse() { Success = true };
            foreach (var m in messages)
                response.Messages.Add(MapDiscussionMessage(new DotNetCloud.Modules.Tracks.Models.SprintDiscussionDto(m.Id, m.SprintId, m.ReviewSessionId, m.UserId, m.UserDisplayName, m.Content, m.CreatedAt)));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListSprintDiscussions failed");
            return new ListSprintDiscussionsResponse();
        }
    }
    /// <inheritdoc />
    public override async Task<SprintDiscussionMessage> SendSprintDiscussion(SendSprintDiscussionRequest request, ServerCallContext context)
    {
        try
        {
            var msg = await _discussionService.SendSprintMessageAsync(Guid.Parse(request.SprintId), Guid.Parse(request.UserId), "", request.Content ?? "", context.CancellationToken);
            return MapDiscussionMessage(new DotNetCloud.Modules.Tracks.Models.SprintDiscussionDto(msg.Id, msg.SprintId, msg.ReviewSessionId, msg.UserId, msg.UserDisplayName, msg.Content, msg.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendSprintDiscussion failed");
            return new SprintDiscussionMessage();
        }
    }
    /// <inheritdoc />
    public override async Task<ListReviewDiscussionsResponse> ListReviewDiscussions(ListReviewDiscussionsRequest request, ServerCallContext context)
    {
        try
        {
            var messages = await _discussionService.GetReviewSessionMessagesAsync(Guid.Parse(request.ReviewSessionId), skip: request.Skip, take: request.Take > 0 ? request.Take : 50, ct: context.CancellationToken);
            var response = new ListReviewDiscussionsResponse() { Success = true };
            foreach (var m in messages)
                response.Messages.Add(MapDiscussionMessage(new DotNetCloud.Modules.Tracks.Models.SprintDiscussionDto(m.Id, m.SprintId, m.ReviewSessionId, m.UserId, m.UserDisplayName, m.Content, m.CreatedAt)));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListReviewDiscussions failed");
            return new ListReviewDiscussionsResponse();
        }
    }
    /// <inheritdoc />
    public override async Task<SprintDiscussionMessage> SendReviewDiscussion(SendReviewDiscussionRequest request, ServerCallContext context)
    {
        try
        {
            var msg = await _discussionService.SendReviewSessionMessageAsync(Guid.Parse(request.ReviewSessionId), Guid.Parse(request.UserId), "", request.Content ?? "", context.CancellationToken);
            return MapDiscussionMessage(new DotNetCloud.Modules.Tracks.Models.SprintDiscussionDto(msg.Id, msg.SprintId, msg.ReviewSessionId, msg.UserId, msg.UserDisplayName, msg.Content, msg.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendReviewDiscussion failed");
            return new SprintDiscussionMessage();
        }
    }
    // ─── Mapper stubs ────────────────────────────────────────────────────

    private static SprintMessage MapSprint(SprintDto dto) => MapSprints([dto]).First();
    private static SprintDiscussionMessage MapSprintDiscussion(DotNetCloud.Modules.Tracks.Models.SprintDiscussionDto dto) => MapDiscussionMessage(dto);
    private static WorkItemCommentMessage MapComment(WorkItemCommentDto dto) => new() { Id = dto.Id.ToString(), WorkItemId = dto.WorkItemId.ToString(), UserId = dto.UserId.ToString(), DisplayName = dto.DisplayName ?? "", Content = dto.Content ?? "", IsEdited = dto.IsEdited, IsDeleted = dto.IsDeleted, DeletedAt = dto.DeletedAt?.ToString("O") ?? "", CreatedAt = dto.CreatedAt.ToString("O"), UpdatedAt = dto.UpdatedAt.ToString("O") };
    private static ChecklistMessage MapChecklist(ChecklistDto dto) { var m = new ChecklistMessage { Id = dto.Id.ToString(), ItemId = dto.ItemId.ToString(), Title = dto.Title ?? "", Position = dto.Position, CreatedAt = dto.CreatedAt.ToString("O") }; if (dto.Items is not null) foreach (var i in dto.Items) m.Items.Add(new ChecklistItemMessage { Id = i.Id.ToString(), ChecklistId = i.ChecklistId.ToString(), Title = i.Title ?? "", IsCompleted = i.IsCompleted, Position = i.Position, AssignedToUserId = i.AssignedToUserId?.ToString() ?? "", CreatedAt = i.CreatedAt.ToString("O"), UpdatedAt = i.UpdatedAt.ToString("O") }); return m; }
    private static TimeEntryMessage MapTimeEntry(TimeEntryDto dto) => new() { Id = dto.Id.ToString(), WorkItemId = dto.WorkItemId.ToString(), UserId = dto.UserId.ToString(), Description = dto.Description ?? "", StartTime = dto.StartTime?.ToString("O") ?? "", EndTime = dto.EndTime?.ToString("O") ?? "", DurationMinutes = dto.DurationMinutes, CreatedAt = dto.CreatedAt.ToString("O"), UpdatedAt = dto.UpdatedAt.ToString("O") };
    private static WorkItemDependencyMessage MapDependency(WorkItemDependencyDto dto) => new() { Id = dto.Id.ToString(), WorkItemId = dto.WorkItemId.ToString(), DependsOnWorkItemId = dto.DependsOnWorkItemId.ToString(), DependsOnTitle = dto.DependsOnTitle ?? "", Type = dto.Type.ToString(), CreatedAt = dto.CreatedAt.ToString("O") };
    private static SprintDiscussionMessage MapDiscussionMessage(DotNetCloud.Modules.Tracks.Models.SprintDiscussionDto dto) => new() { Id = dto.Id.ToString(), SprintId = dto.SprintId?.ToString() ?? "", ReviewSessionId = dto.ReviewSessionId?.ToString() ?? "", UserId = dto.UserId.ToString(), UserDisplayName = dto.UserDisplayName ?? "", Content = dto.Content ?? "", CreatedAt = dto.CreatedAt.ToString("O") };

}
