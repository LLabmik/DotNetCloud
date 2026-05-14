using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// gRPC service implementation for the Tracks module.
/// Exposes product, swimlane, and work item operations over gRPC for the core server to invoke.
/// </summary>
public sealed class TracksGrpcService : Protos.TracksGrpcService.TracksGrpcServiceBase
{
    private readonly ProductService _productService;
    private readonly SwimlaneService _swimlaneService;
    private readonly WorkItemService _workItemService;
    private readonly PokerService _pokerService;
    private readonly SprintPlanningService _sprintPlanningService;
    private readonly ReviewSessionService _reviewSessionService;
    private readonly ILogger<TracksGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksGrpcService"/> class.
    /// </summary>
    public TracksGrpcService(
        ProductService productService,
        SwimlaneService swimlaneService,
        WorkItemService workItemService,
        PokerService pokerService,
        SprintPlanningService sprintPlanningService,
        ReviewSessionService reviewSessionService,
        ILogger<TracksGrpcService> logger)
    {
        _productService = productService;
        _swimlaneService = swimlaneService;
        _workItemService = workItemService;
        _pokerService = pokerService;
        _sprintPlanningService = sprintPlanningService;
        _reviewSessionService = reviewSessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ProductResponse> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateProduct called for user {UserId}", request.UserId);
        try
        {
            var ownerId = Guid.Parse(request.UserId);
            var dto = new CreateProductDto
            {
                Name = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color,
                SubItemsEnabled = false
            };
            var product = await _productService.CreateProductAsync(
                Guid.Empty, ownerId, dto, context.CancellationToken);
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
                Guid.Empty, context.CancellationToken);

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

            var dto = new CreateWorkItemDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = priority,
                DueDate = dueDate,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = [],
                LabelIds = []
            };
            var workItem = await _workItemService.CreateWorkItemAsync(
                Guid.Empty, swimlaneId, WorkItemType.Epic, userId, dto, context.CancellationToken);
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
        _logger.LogInformation("StartPokerSession called for item {ItemId} by user {UserId}", request.WorkItemId, request.UserId);
        try
        {
            var userId = Guid.Parse(request.UserId);
            var dto = new CreatePokerSessionDto
            {
                ItemId = Guid.Parse(request.WorkItemId),
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
                NumberOfSprints = request.SprintCount,
                SprintDurationWeeks = request.DefaultDurationWeeks
            };
            var sprints = await _sprintPlanningService.CreateSprintPlanAsync(
                Guid.Parse(request.ProductId), dto, context.CancellationToken);
            return new SprintPlanResponse { Success = true, Overview = MapSprintPlanOverview(sprints, Guid.Parse(request.ProductId)) };
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
            var epicId = Guid.Parse(request.ProductId);
            var sprints = await _sprintPlanningService.GetSprintPlanAsync(epicId, context.CancellationToken);
            return new SprintPlanResponse { Success = true, Overview = MapSprintPlanOverview(sprints, epicId) };
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
            return new SprintPlanResponse { Success = true, Overview = MapSprintPlanOverview(sprints, sprint.EpicId) };
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
                Guid.Parse(request.ProductId), hostUserId, context.CancellationToken);
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
                Guid.Parse(request.SessionId), Guid.Parse(request.WorkItemId), context.CancellationToken);
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
            WorkItemId = dto.ItemId.ToString(),
            ProductId = dto.EpicId.ToString(),
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
            OwnerId = dto.OwnerId.ToString(),
            Title = dto.Name,
            Description = dto.Description ?? "",
            Color = dto.Color ?? "",
            IsArchived = dto.IsArchived,
            Etag = dto.ETag ?? "",
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            SwimlaneCount = dto.SwimlaneCount,
            ItemCount = dto.EpicCount,
            MemberCount = dto.MemberCount,
            Mode = ""
        };
    }

    private static SwimlaneMessage MapSwimlane(SwimlaneDto dto)
    {
        return new SwimlaneMessage
        {
            Id = dto.Id.ToString(),
            ProductId = dto.ContainerId.ToString(),
            Title = dto.Title,
            Position = dto.Position,
            Color = dto.Color ?? "",
            ItemLimit = dto.CardLimit ?? 0,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            ItemCount = dto.CardCount
        };
    }

    private static WorkItemMessage MapWorkItem(WorkItemDto dto)
    {
        var msg = new WorkItemMessage
        {
            Id = dto.Id.ToString(),
            SwimlaneId = dto.SwimlaneId.ToString(),
            Title = dto.Title,
            Description = dto.Description ?? "",
            Position = dto.Position,
            DueDate = dto.DueDate?.ToString("O") ?? "",
            Priority = dto.Priority.ToString(),
            StoryPoints = dto.StoryPoints ?? 0,
            IsArchived = dto.IsArchived,
            CreatedByUserId = "",
            Etag = dto.ETag,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            CommentCount = dto.CommentCount,
            AttachmentCount = dto.AttachmentCount
        };
        foreach (var a in dto.Assignments)
            msg.AssigneeIds.Add(a.UserId.ToString());
        foreach (var l in dto.Labels)
            msg.LabelIds.Add(l.Id.ToString());
        return msg;
    }

    private static SprintPlanOverviewMessage MapSprintPlanOverview(List<SprintDto> sprints, Guid epicId)
    {
        var msg = new SprintPlanOverviewMessage
        {
            ProductId = epicId.ToString(),
            TotalWeeks = sprints.Sum(s => s.DurationWeeks ?? 0),
            PlanStartDate = sprints.FirstOrDefault()?.StartDate?.ToString("O") ?? "",
            PlanEndDate = sprints.LastOrDefault()?.EndDate?.ToString("O") ?? ""
        };
        foreach (var s in sprints)
        {
            msg.Sprints.Add(new SprintPlanItemMessage
            {
                Id = s.Id.ToString(),
                Title = s.Title,
                StartDate = s.StartDate?.ToString("O") ?? "",
                EndDate = s.EndDate?.ToString("O") ?? "",
                Status = s.Status.ToString(),
                DurationWeeks = s.DurationWeeks ?? 0,
                PlannedOrder = s.PlannedOrder ?? 0,
                ItemCount = s.ItemCount,
                TotalStoryPoints = s.TargetStoryPoints ?? 0
            });
        }
        return msg;
    }

    private static ReviewSessionMessage MapReviewSession(ReviewSessionDto dto)
    {
        var msg = new ReviewSessionMessage
        {
            Id = dto.Id.ToString(),
            ProductId = dto.EpicId.ToString(),
            HostUserId = dto.HostUserId.ToString(),
            CurrentWorkItemId = dto.CurrentItemId?.ToString() ?? "",
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
}
