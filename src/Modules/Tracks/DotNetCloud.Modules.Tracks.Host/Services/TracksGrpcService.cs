using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// gRPC service implementation for the Tracks module.
/// Exposes board, list, and card operations over gRPC for the core server to invoke.
/// </summary>
/// <remarks>
/// Full implementation will be added in Phase 4.3/4.4. This scaffold provides
/// the service structure and connection points.
/// </remarks>
public sealed class TracksGrpcService : Protos.TracksGrpcService.TracksGrpcServiceBase
{
    private readonly TracksDbContext _db;
    private readonly ILogger<TracksGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksGrpcService"/> class.
    /// </summary>
    public TracksGrpcService(TracksDbContext db, ILogger<TracksGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task<BoardResponse> CreateBoard(CreateBoardRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateBoard called for user {UserId}", request.UserId);
        return Task.FromResult(new BoardResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<BoardResponse> GetBoard(GetBoardRequest request, ServerCallContext context)
    {
        return Task.FromResult(new BoardResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<ListBoardsResponse> ListBoards(ListBoardsRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ListBoardsResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<ListResponse> CreateList(CreateListRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ListResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<CardResponse> CreateCard(CreateCardRequest request, ServerCallContext context)
    {
        return Task.FromResult(new CardResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<CardResponse> GetCard(GetCardRequest request, ServerCallContext context)
    {
        return Task.FromResult(new CardResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<CardResponse> MoveCard(MoveCardRequest request, ServerCallContext context)
    {
        return Task.FromResult(new CardResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<PokerSessionResponse> StartPokerSession(StartPokerSessionRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartPokerSession called for card {CardId} by user {UserId}", request.CardId, request.UserId);
        return Task.FromResult(new PokerSessionResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<PokerSessionResponse> SubmitPokerVote(SubmitPokerVoteRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PokerSessionResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<PokerSessionResponse> RevealPokerSession(RevealPokerSessionRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PokerSessionResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }

    /// <inheritdoc />
    public override Task<PokerSessionResponse> AcceptPokerEstimate(AcceptPokerEstimateRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PokerSessionResponse
        {
            Success = false,
            ErrorMessage = "Not implemented yet. Full implementation in Phase 4.3."
        });
    }
}
