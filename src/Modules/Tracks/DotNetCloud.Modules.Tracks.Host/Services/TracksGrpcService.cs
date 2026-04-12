using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// gRPC service implementation for the Tracks module.
/// Exposes board, swimlane, and card operations over gRPC for the core server to invoke.
/// </summary>
public sealed class TracksGrpcService : Protos.TracksGrpcService.TracksGrpcServiceBase
{
    private readonly BoardService _boardService;
    private readonly SwimlaneService _swimlaneService;
    private readonly CardService _cardService;
    private readonly PokerService _pokerService;
    private readonly SprintPlanningService _sprintPlanningService;
    private readonly ReviewSessionService _reviewSessionService;
    private readonly ILogger<TracksGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksGrpcService"/> class.
    /// </summary>
    public TracksGrpcService(
        BoardService boardService,
        SwimlaneService swimlaneService,
        CardService cardService,
        PokerService pokerService,
        SprintPlanningService sprintPlanningService,
        ReviewSessionService reviewSessionService,
        ILogger<TracksGrpcService> logger)
    {
        _boardService = boardService;
        _swimlaneService = swimlaneService;
        _cardService = cardService;
        _pokerService = pokerService;
        _sprintPlanningService = sprintPlanningService;
        _reviewSessionService = reviewSessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<BoardResponse> CreateBoard(CreateBoardRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateBoard called for user {UserId}", request.UserId);
        try
        {
            var caller = ParseCaller(request.UserId);
            var dto = new CreateBoardDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color,
                Mode = Enum.TryParse<BoardMode>(request.Mode, true, out var mode) ? mode : BoardMode.Personal
            };
            var board = await _boardService.CreateBoardAsync(dto, caller, context.CancellationToken);
            return new BoardResponse { Success = true, Board = MapBoard(board) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateBoard failed");
            return new BoardResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<BoardResponse> GetBoard(GetBoardRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var boardId = Guid.Parse(request.BoardId);
            var board = await _boardService.GetBoardAsync(boardId, caller, context.CancellationToken);
            if (board is null)
                return new BoardResponse { Success = false, ErrorMessage = "Board not found." };
            return new BoardResponse { Success = true, Board = MapBoard(board) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBoard failed");
            return new BoardResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListBoardsResponse> ListBoards(ListBoardsRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var boards = await _boardService.ListBoardsAsync(caller, request.IncludeArchived, cancellationToken: context.CancellationToken);
            var response = new ListBoardsResponse { Success = true };
            foreach (var board in boards)
                response.Boards.Add(MapBoard(board));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListBoards failed");
            return new ListBoardsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SwimlaneResponse> CreateSwimlane(CreateSwimlaneRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var boardId = Guid.Parse(request.BoardId);
            var dto = new CreateBoardSwimlaneDto
            {
                Title = request.Title,
                Color = string.IsNullOrEmpty(request.Color) ? null : request.Color
            };
            var swimlane = await _swimlaneService.CreateSwimlaneAsync(boardId, dto, caller, context.CancellationToken);
            return new SwimlaneResponse { Success = true, Swimlane = MapSwimlane(swimlane) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSwimlane failed");
            return new SwimlaneResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<CardResponse> CreateCard(CreateCardRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var swimlaneId = Guid.Parse(request.SwimlaneId);
            var priority = Enum.TryParse<CardPriority>(request.Priority, true, out var p) ? p : CardPriority.None;
            DateTime? dueDate = DateTime.TryParse(request.DueDate, out var dd) ? dd : null;
            var dto = new CreateCardDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
                Priority = priority,
                DueDate = dueDate,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null,
                AssigneeIds = [],
                LabelIds = []
            };
            var card = await _cardService.CreateCardAsync(swimlaneId, dto, caller, context.CancellationToken);
            return new CardResponse { Success = true, Card = MapCard(card) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCard failed");
            return new CardResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<CardResponse> GetCard(GetCardRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var cardId = Guid.Parse(request.CardId);
            var card = await _cardService.GetCardAsync(cardId, caller, context.CancellationToken);
            if (card is null)
                return new CardResponse { Success = false, ErrorMessage = "Card not found." };
            return new CardResponse { Success = true, Card = MapCard(card) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCard failed");
            return new CardResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<CardResponse> MoveCard(MoveCardRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var cardId = Guid.Parse(request.CardId);
            var dto = new MoveCardDto
            {
                TargetSwimlaneId = Guid.Parse(request.TargetSwimlaneId),
                Position = (int)request.Position
            };
            var card = await _cardService.MoveCardAsync(cardId, dto, caller, context.CancellationToken);
            return new CardResponse { Success = true, Card = MapCard(card) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MoveCard failed");
            return new CardResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PokerSessionResponse> StartPokerSession(StartPokerSessionRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartPokerSession called for card {CardId} by user {UserId}", request.CardId, request.UserId);
        try
        {
            var caller = ParseCaller(request.UserId);
            var dto = new CreatePokerSessionDto
            {
                Scale = Enum.TryParse<PokerScale>(request.Scale, true, out var scale) ? scale : PokerScale.Fibonacci,
                CustomScaleValues = string.IsNullOrEmpty(request.CustomScaleValues) ? null : request.CustomScaleValues
            };
            var session = await _pokerService.StartSessionAsync(Guid.Parse(request.CardId), dto, caller, context.CancellationToken);
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
            var caller = ParseCaller(request.UserId);
            var dto = new SubmitPokerVoteDto { Estimate = request.Estimate };
            var session = await _pokerService.SubmitVoteAsync(Guid.Parse(request.SessionId), dto, caller, context.CancellationToken);
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
            var caller = ParseCaller(request.UserId);
            var session = await _pokerService.RevealSessionAsync(Guid.Parse(request.SessionId), caller, context.CancellationToken);
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
            var caller = ParseCaller(request.UserId);
            var dto = new AcceptPokerEstimateDto
            {
                AcceptedEstimate = request.AcceptedEstimate,
                StoryPoints = request.StoryPoints > 0 ? request.StoryPoints : null
            };
            var session = await _pokerService.AcceptEstimateAsync(Guid.Parse(request.SessionId), dto, caller, context.CancellationToken);
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
            var caller = ParseCaller(request.UserId);
            var dto = new CreateSprintPlanDto
            {
                StartDate = DateTime.Parse(request.StartDate),
                SprintCount = request.SprintCount,
                DefaultDurationWeeks = request.DefaultDurationWeeks
            };
            var overview = await _sprintPlanningService.CreateYearPlanAsync(Guid.Parse(request.BoardId), dto, caller, context.CancellationToken);
            return new SprintPlanResponse { Success = true, Overview = MapSprintPlanOverview(overview) };
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
            var caller = ParseCaller(request.UserId);
            var overview = await _sprintPlanningService.GetPlanOverviewAsync(Guid.Parse(request.BoardId), caller, context.CancellationToken);
            return new SprintPlanResponse { Success = true, Overview = MapSprintPlanOverview(overview) };
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
            var caller = ParseCaller(request.UserId);
            var dto = new AdjustSprintDto
            {
                DurationWeeks = request.DurationWeeks,
                StartDate = string.IsNullOrEmpty(request.StartDate) ? null : DateTime.Parse(request.StartDate)
            };
            var overview = await _sprintPlanningService.AdjustSprintAsync(Guid.Parse(request.SprintId), dto, caller, context.CancellationToken);
            return new SprintPlanResponse { Success = true, Overview = MapSprintPlanOverview(overview) };
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
            var caller = ParseCaller(request.UserId);
            var session = await _reviewSessionService.StartSessionAsync(Guid.Parse(request.BoardId), caller, context.CancellationToken);
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
            var caller = ParseCaller(request.UserId);
            var session = await _reviewSessionService.GetSessionStateAsync(Guid.Parse(request.SessionId), caller, context.CancellationToken);
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
            var caller = ParseCaller(request.UserId);
            var session = await _reviewSessionService.JoinSessionAsync(Guid.Parse(request.SessionId), caller, context.CancellationToken);
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JoinReviewSession failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> SetReviewCurrentCard(SetReviewCurrentCardRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var session = await _reviewSessionService.SetCurrentCardAsync(Guid.Parse(request.SessionId), Guid.Parse(request.CardId), caller, context.CancellationToken);
            return new ReviewSessionResponse { Success = true, Session = MapReviewSession(session) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetReviewCurrentCard failed");
            return new ReviewSessionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ReviewSessionResponse> EndReviewSession(EndReviewSessionRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            await _reviewSessionService.EndSessionAsync(Guid.Parse(request.SessionId), caller, context.CancellationToken);
            return new ReviewSessionResponse { Success = true };
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
            var caller = ParseCaller(request.UserId);
            var statuses = await _pokerService.GetVoteStatusAsync(Guid.Parse(request.SessionId), caller, context.CancellationToken);
            var response = new PokerVoteStatusResponse { Success = true };
            foreach (var s in statuses)
                response.Statuses.Add(new PokerVoteStatusItem { UserId = s.UserId.ToString(), HasVoted = s.HasVoted });
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
            CardId = dto.CardId.ToString(),
            BoardId = dto.BoardId.ToString(),
            CreatedByUserId = dto.CreatedByUserId.ToString(),
            Scale = dto.Scale.ToString(),
            CustomScaleValues = dto.CustomScaleValues ?? "",
            Status = dto.Status.ToString(),
            AcceptedEstimate = dto.AcceptedEstimate ?? "",
            Round = dto.Round,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
        foreach (var vote in dto.Votes)
            msg.Votes.Add(new PokerVoteMessage
            {
                UserId = vote.UserId.ToString(),
                Estimate = vote.Estimate,
                Round = vote.Round,
                VotedAt = vote.VotedAt.ToString("O")
            });
        return msg;
    }

    private static CallerContext ParseCaller(string userId)
    {
        return new CallerContext(Guid.Parse(userId), [], CallerType.Module);
    }

    private static BoardMessage MapBoard(BoardDto dto)
    {
        return new BoardMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            Title = dto.Title,
            Description = dto.Description ?? "",
            Color = dto.Color ?? "",
            IsArchived = dto.IsArchived,
            Etag = dto.ETag ?? "",
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            SwimlaneCount = dto.Swimlanes.Count,
            CardCount = dto.Swimlanes.Sum(l => l.CardCount),
            MemberCount = dto.Members.Count,
            Mode = dto.Mode.ToString()
        };
    }

    private static BoardSwimlaneMessage MapSwimlane(BoardSwimlaneDto dto)
    {
        return new BoardSwimlaneMessage
        {
            Id = dto.Id.ToString(),
            BoardId = dto.BoardId.ToString(),
            Title = dto.Title,
            Position = dto.Position,
            Color = dto.Color ?? "",
            CardLimit = dto.CardLimit ?? 0,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            CardCount = dto.CardCount
        };
    }

    private static CardMessage MapCard(CardDto dto)
    {
        var msg = new CardMessage
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
            Etag = "",
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O"),
            CommentCount = dto.CommentCount,
            AttachmentCount = dto.AttachmentCount
        };
        foreach (var a in dto.Assignments)
            msg.AssignedUserIds.Add(a.UserId.ToString());
        foreach (var l in dto.Labels)
            msg.LabelIds.Add(l.Id.ToString());
        return msg;
    }

    private static SprintPlanOverviewMessage MapSprintPlanOverview(SprintPlanOverviewDto dto)
    {
        var msg = new SprintPlanOverviewMessage
        {
            BoardId = dto.BoardId.ToString(),
            TotalWeeks = dto.TotalWeeks,
            PlanStartDate = dto.PlanStartDate?.ToString("O") ?? "",
            PlanEndDate = dto.PlanEndDate?.ToString("O") ?? ""
        };
        foreach (var s in dto.Sprints)
            msg.Sprints.Add(new SprintPlanItemMessage
            {
                Id = s.Id.ToString(),
                Title = s.Title,
                StartDate = s.StartDate?.ToString("O") ?? "",
                EndDate = s.EndDate?.ToString("O") ?? "",
                Status = s.Status.ToString(),
                DurationWeeks = s.DurationWeeks ?? 0,
                PlannedOrder = s.PlannedOrder ?? 0,
                CardCount = s.CardCount,
                TotalStoryPoints = s.TotalStoryPoints
            });
        return msg;
    }

    private static ReviewSessionMessage MapReviewSession(ReviewSessionDto dto)
    {
        var msg = new ReviewSessionMessage
        {
            Id = dto.Id.ToString(),
            BoardId = dto.BoardId.ToString(),
            HostUserId = dto.HostUserId.ToString(),
            CurrentCardId = dto.CurrentCardId?.ToString() ?? "",
            Status = dto.Status.ToString(),
            CreatedAt = dto.CreatedAt.ToString("O"),
            EndedAt = dto.EndedAt?.ToString("O") ?? ""
        };
        foreach (var p in dto.Participants)
            msg.Participants.Add(new ReviewParticipantMessage
            {
                Id = p.Id.ToString(),
                UserId = p.UserId.ToString(),
                JoinedAt = p.JoinedAt.ToString("O"),
                IsConnected = p.IsConnected
            });
        if (dto.ActivePokerSession is not null)
            msg.ActivePokerSession = MapPokerSession(dto.ActivePokerSession);
        return msg;
    }

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        GetSearchableDocumentsRequest request,
        IServerStreamWriter<SearchableDocument> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return;

        var caller = new CallerContext(userId, ["user"], CallerType.User);

        var boards = await _boardService.ListBoardsAsync(
            caller, includeArchived: false, cancellationToken: context.CancellationToken);

        foreach (var board in boards)
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(
                board.Id, caller, context.CancellationToken);

            foreach (var swimlane in swimlanes)
            {
                var cards = await _cardService.ListCardsAsync(
                    swimlane.Id, caller, cancellationToken: context.CancellationToken);

                foreach (var card in cards)
                {
                    var doc = MapCardToSearchableDocument(card, board.Title);
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

        var card = await _cardService.GetCardAsync(
            entityId,
            new CallerContext(Guid.Empty, ["system"], CallerType.System),
            context.CancellationToken);

        if (card is null)
            return new SearchableDocumentResponse { Found = false };

        return new SearchableDocumentResponse
        {
            Found = true,
            Document = MapCardToSearchableDocument(card, null)
        };
    }

    private static SearchableDocument MapCardToSearchableDocument(CardDto card, string? boardTitle)
    {
        var contentParts = new List<string>();
        if (!string.IsNullOrEmpty(card.Description)) contentParts.Add(card.Description);
        foreach (var label in card.Labels)
            contentParts.Add(label.Title);

        var doc = new SearchableDocument
        {
            ModuleId = "tracks",
            EntityId = card.Id.ToString(),
            EntityType = "Card",
            Title = card.Title,
            Content = string.Join(" ", contentParts),
            Summary = card.Description?.Length > 200
                ? card.Description[..200] + "..."
                : card.Description ?? string.Empty,
            OwnerId = string.Empty,
            CreatedAt = card.CreatedAt.ToString("O"),
            UpdatedAt = card.UpdatedAt.ToString("O")
        };

        doc.Metadata["BoardId"] = card.BoardId.ToString();
        doc.Metadata["Priority"] = card.Priority.ToString();
        if (boardTitle is not null)
            doc.Metadata["BoardTitle"] = boardTitle;
        if (card.Labels.Count > 0)
            doc.Metadata["Labels"] = string.Join(",", card.Labels.Select(l => l.Title));

        return doc;
    }
}
