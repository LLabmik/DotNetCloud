using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Grpc.Core;

namespace DotNetCloud.Core.Server.Grpc.ModuleAccess;

/// <summary>
/// Why a user-scoped module call could not be bound to the subject it may act for.
/// </summary>
internal enum SubjectBindingOutcome
{
    /// <summary>The call may act for the resolved user.</summary>
    Allowed,

    /// <summary>The payload named a user other than the verified caller.</summary>
    Mismatch,

    /// <summary>No verified caller identity was present on the call.</summary>
    MissingCaller,
}

/// <summary>
/// Result of binding a user-scoped call to the subject it may act for.
/// </summary>
/// <param name="Outcome">Whether the call may proceed.</param>
/// <param name="UserId">
/// The user the call acts for. <see cref="Guid.Empty"/> means the payload carried no usable user
/// id, which callers treat as an empty answer rather than an error.
/// </param>
/// <param name="IsModuleScope">
/// <see langword="true"/> when the call carries no end-user subject, i.e. the module is acting for
/// itself (background work) rather than on behalf of a signed-in user.
/// </param>
internal readonly record struct SubjectBinding(
    SubjectBindingOutcome Outcome,
    Guid UserId,
    bool IsModuleScope)
{
    /// <summary>Gets a value indicating whether the call may proceed.</summary>
    public bool IsAllowed => Outcome is SubjectBindingOutcome.Allowed;
}

/// <summary>
/// Binds the user a module call acts for to the verified caller identity.
/// </summary>
/// <remarks>
/// <para>
/// The end-user identity on a module call comes from the core-signed <c>dnc-user-assertion</c>,
/// which <c>CallerContextInterceptor</c> re-validates into <see cref="CallerContext"/>. A
/// user-scoped RPC must never let the payload name a different person: doing so turns every
/// directory RPC into a cross-user read/de-enumeration primitive for any module that holds the
/// capability grant.
/// </para>
/// <para>
/// The check is deliberately scoped: a <em>user-scoped</em> call (one carrying an assertion) may
/// only act for that user, while a module-scope call (no assertion, e.g. a background sweep) is
/// still subject to the capability grant. Module-scope reads are reported separately so they stay
/// observable; per-user consent (phase A3) narrows them further.
/// </para>
/// </remarks>
internal static class CallerSubjectGuard
{
    /// <summary>Gets the verified caller identity for a call, if one is present.</summary>
    /// <param name="context">The call context.</param>
    /// <returns>The caller context, or <see langword="null"/> when the chain did not supply one.</returns>
    public static CallerContext? GetCaller(ServerCallContext context) =>
        context.UserState.TryGetValue("CallerContext", out var state) && state is CallerContext caller
            ? caller
            : null;

    /// <summary>
    /// Binds a user-scoped call to the subject it may act for.
    /// </summary>
    /// <param name="context">The call context (carries the verified caller identity).</param>
    /// <param name="requestedUserId">The user id supplied in the request payload.</param>
    /// <returns>The binding decision.</returns>
    public static SubjectBinding Bind(ServerCallContext context, string? requestedUserId)
    {
        var caller = GetCaller(context);
        if (caller is null)
        {
            // Reaching here means the interceptor chain was misconfigured. Fail closed rather
            // than guess whose data was asked for.
            return new SubjectBinding(SubjectBindingOutcome.MissingCaller, Guid.Empty, false);
        }

        _ = Guid.TryParse(requestedUserId, out var requested);

        if (caller.Type is CallerType.User)
        {
            // An absent/empty payload id means "me". Anything else must be the caller.
            return requested == Guid.Empty || requested == caller.UserId
                ? new SubjectBinding(SubjectBindingOutcome.Allowed, caller.UserId, false)
                : new SubjectBinding(SubjectBindingOutcome.Mismatch, Guid.Empty, false);
        }

        return new SubjectBinding(SubjectBindingOutcome.Allowed, requested, IsModuleScope: true);
    }

    /// <summary>
    /// Creates the RPC exception a denied binding is reported with.
    /// </summary>
    /// <param name="outcome">The denial outcome.</param>
    /// <returns>The exception to throw.</returns>
    public static RpcException CreateDeniedException(SubjectBindingOutcome outcome) =>
        outcome switch
        {
            SubjectBindingOutcome.MissingCaller => new RpcException(new Status(
                StatusCode.Unauthenticated,
                "No verified caller identity is available for this call")),

            _ => new RpcException(
                new Status(
                    StatusCode.PermissionDenied,
                    "[AUTH_SUBJECT_MISMATCH] The request names a different user than the " +
                    "authenticated caller."),
                new Metadata { { "error-code", ErrorCodes.SubjectMismatch } }),
        };
}
