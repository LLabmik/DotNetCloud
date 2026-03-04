using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for adding and removing emoji reactions on messages.
/// </summary>
public interface IReactionService
{
    /// <summary>Adds a reaction to a message.</summary>
    Task AddReactionAsync(Guid messageId, string emoji, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a reaction from a message.</summary>
    Task RemoveReactionAsync(Guid messageId, string emoji, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all reactions on a message, grouped by emoji.</summary>
    Task<IReadOnlyList<MessageReactionDto>> GetReactionsAsync(Guid messageId, CancellationToken cancellationToken = default);
}
