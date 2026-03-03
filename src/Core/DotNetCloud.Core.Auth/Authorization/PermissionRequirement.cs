using Microsoft.AspNetCore.Authorization;

namespace DotNetCloud.Core.Auth.Authorization;

/// <summary>
/// An authorization requirement that demands a specific permission claim on the principal.
/// </summary>
/// <param name="Permission">
/// The permission value that must be present in a <c>dnc:perm</c> claim.
/// </param>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
