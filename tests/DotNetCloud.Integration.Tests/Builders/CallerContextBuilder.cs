using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Integration.Tests.Builders;

/// <summary>
/// Fluent builder for creating <see cref="CallerContext"/> test instances.
/// </summary>
internal sealed class CallerContextBuilder
{
    private Guid _userId = Guid.NewGuid();
    private IReadOnlyList<string> _roles = ["User"];
    private CallerType _type = CallerType.User;

    public CallerContextBuilder WithUserId(Guid id) { _userId = id; return this; }
    public CallerContextBuilder WithRoles(params string[] roles) { _roles = roles; return this; }
    public CallerContextBuilder WithType(CallerType type) { _type = type; return this; }

    public CallerContext Build()
    {
        return new CallerContext(_userId, _roles, _type);
    }

    /// <summary>
    /// Creates a system caller context for integration tests.
    /// </summary>
    public static CallerContext CreateSystem() => CallerContext.CreateSystemContext();

    /// <summary>
    /// Creates an admin caller context for integration tests.
    /// </summary>
    public static CallerContext CreateAdmin(Guid? userId = null)
    {
        return new CallerContext(
            userId ?? Guid.NewGuid(),
            ["Administrator", "User"],
            CallerType.User);
    }
}
