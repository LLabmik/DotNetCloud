using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Creates short-lived <see cref="ChatDbContext"/> instances for singleton services
/// (e.g. <see cref="Services.DbNotificationPreferenceStore"/>).
/// </summary>
/// <remarks>
/// <see cref="ChatDbContext"/> has two applicable constructors (options-only, and
/// options + <see cref="ITableNamingStrategy"/>). When <see cref="ITableNamingStrategy"/>
/// is registered in DI, the built-in <c>AddDbContextFactory</c> activator cannot pick a
/// single constructor, so this factory constructs the context explicitly with the
/// two-argument constructor.
/// </remarks>
public sealed class ChatDbContextFactory : IDbContextFactory<ChatDbContext>
{
    private readonly DbContextOptions<ChatDbContext> _options;
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatDbContextFactory"/> class.
    /// </summary>
    /// <param name="options">Configured options for the chat context.</param>
    /// <param name="namingStrategy">Naming strategy matching the active database provider.</param>
    public ChatDbContextFactory(DbContextOptions<ChatDbContext> options, ITableNamingStrategy namingStrategy)
    {
        _options = options;
        _namingStrategy = namingStrategy;
    }

    /// <inheritdoc />
    public ChatDbContext CreateDbContext() => new(_options, _namingStrategy);

    /// <inheritdoc />
    public Task<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
