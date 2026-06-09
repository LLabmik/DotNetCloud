using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// No-op implementation of <see cref="IVideoCallService"/> for the Core.Server process.
/// The Chat module runs process-isolated; this stub satisfies DI for global UI components
/// (e.g., <c>GlobalChatNotifications</c>) that inject the interface but only call it in
/// best-effort paths wrapped by try/catch.
/// </summary>
public sealed class NoOpVideoCallService : IVideoCallService
{
    public Task<VideoCallDto> InitiateCallAsync(Guid channelId, StartCallRequest request, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.FromResult<VideoCallDto>(null!);

    public Task<VideoCallDto> JoinCallAsync(Guid callId, JoinCallRequest request, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.FromResult<VideoCallDto>(null!);

    public Task LeaveCallAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EndCallAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RejectCallAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<CallHistoryDto>> GetCallHistoryAsync(Guid channelId, int skip, int take, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CallHistoryDto>>(Array.Empty<CallHistoryDto>());

    public Task<VideoCallDto?> GetActiveCallAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.FromResult<VideoCallDto?>(null);

    public Task<VideoCallDto?> GetCallByIdAsync(Guid callId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.FromResult<VideoCallDto?>(null);

    public Task<VideoCallDto> InitiateDirectCallAsync(Guid targetUserId, StartCallRequest request, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.FromResult<VideoCallDto>(null!);

    public Task InviteToCallAsync(Guid callId, Guid targetUserId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task TransferHostAsync(Guid callId, Guid newHostUserId, CallerContext caller, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
