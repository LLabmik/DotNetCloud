using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Shared helpers for Tracks service tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Creates a fresh InMemory TracksDbContext.</summary>
    public static TracksDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TracksDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    /// <summary>Seeds a board with the given owner as Owner member.</summary>
    public static async Task<Board> SeedBoardAsync(TracksDbContext db, Guid ownerId, string title = "Test Board")
    {
        var board = new Board { Title = title, OwnerId = ownerId };
        board.Members.Add(new BoardMember
        {
            BoardId = board.Id,
            UserId = ownerId,
            Role = BoardMemberRole.Owner,
            JoinedAt = DateTime.UtcNow
        });
        db.Boards.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    /// <summary>Adds a member to a board.</summary>
    public static async Task<BoardMember> AddMemberAsync(TracksDbContext db, Guid boardId, Guid userId, BoardMemberRole role = BoardMemberRole.Member)
    {
        var member = new BoardMember
        {
            BoardId = boardId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
        db.BoardMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    /// <summary>Seeds a list on a board.</summary>
    public static async Task<BoardSwimlane> SeedSwimlaneAsync(TracksDbContext db, Guid boardId, string title = "Test List", int? cardLimit = null)
    {
        var list = new BoardSwimlane
        {
            BoardId = boardId,
            Title = title,
            Position = 1000.0,
            CardLimit = cardLimit
        };
        db.BoardSwimlanes.Add(list);
        await db.SaveChangesAsync();
        return list;
    }

    /// <summary>Seeds a card in a swimlane.</summary>
    public static async Task<Card> SeedCardAsync(TracksDbContext db, Guid swimlaneId, Guid createdByUserId, string title = "Test Card")
    {
        var card = new Card
        {
            SwimlaneId = swimlaneId,
            Title = title,
            Position = 1000.0,
            CreatedByUserId = createdByUserId
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return card;
    }
}
