using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Example.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Example.Host.Services;

/// <summary>
/// Erases a deleted user's Example notes on behalf of the core's account-erasure orchestrator.
/// </summary>
/// <remarks>
/// The Example module declares <c>dataStore: "isolated"</c>, so its schema holds per-user rows that
/// no other component can reach. Deleting the account must therefore delete them here (F12): the
/// core records the acknowledgement and retries for 72 hours, and an account deletion never reports
/// success while a module row is outstanding.
/// </remarks>
public sealed class ExampleUserErasureHandler : IModuleUserErasureHandler
{
    /// <summary>Scope key used when reporting how many rows were deleted.</summary>
    public const string AllScopeKey = "all";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExampleUserErasureHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="ExampleUserErasureHandler"/> class.</summary>
    /// <param name="scopeFactory">
    /// Creates the scope the scoped <see cref="ExampleDbContext"/> is resolved from. The handler is
    /// registered as a singleton, so it must not hold a scoped context itself.
    /// </param>
    /// <param name="logger">The logger.</param>
    public ExampleUserErasureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<ExampleUserErasureHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleUserErasureResult> EraseAsync(
        ModuleUserErasureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();

        var notes = await db.Notes
            .Where(note => note.CreatedByUserId == context.UserId)
            .ToListAsync(cancellationToken);

        if (notes.Count > 0)
        {
            db.Notes.RemoveRange(notes);
            await db.SaveChangesAsync(cancellationToken);
        }

        // The account id is personal data: it is deliberately never logged (cs/exposure-of-sensitive-information).
        _logger.LogInformation(
            "Erased {NoteCount} example note(s) for a deleted account.",
            notes.Count);

        return ModuleUserErasureResult.Success(
            new Dictionary<string, int>(StringComparer.Ordinal) { [AllScopeKey] = notes.Count });
    }
}
