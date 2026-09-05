using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// One-time, idempotent backfill that rewrites legacy accounts whose
/// <c>UserName</c> was set to their email address into a distinct username
/// derived from the email local-part. Collisions are resolved by appending
/// a numeric suffix.
/// </summary>
/// <remarks>
/// This directly edits <c>UserName</c>/<c>NormalizedUserName</c> via
/// <see cref="CoreDbContext"/> rather than <c>UserManager.SetUserNameAsync</c>
/// because it is a one-time data transformation and running Identity validators
/// on every startup would be unnecessary. It must run before <c>AdminSeeder</c>
/// so a migrated admin can be found by username.
/// </remarks>
internal sealed class LegacyUsernameMigration
{
    private const string AllowedChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";

    private readonly CoreDbContext _dbContext;
    private readonly ILogger<LegacyUsernameMigration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyUsernameMigration"/> class.
    /// </summary>
    /// <param name="dbContext">The core database context.</param>
    /// <param name="logger">The logger.</param>
    public LegacyUsernameMigration(CoreDbContext dbContext, ILogger<LegacyUsernameMigration> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Runs the migration. Safe to call on every startup: it is a no-op once
    /// no users have an <c>@</c> in <c>UserName</c>.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var legacyUsers = await _dbContext.Users
            .Where(u => u.UserName != null && u.UserName.Contains("@"))
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);

        if (legacyUsers.Count == 0)
        {
            return;
        }

        // Seed the taken set with all existing usernames that are NOT legacy
        // (case-insensitive, matching Identity's normalization).
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allUsers = await _dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var u in allUsers)
        {
            if (!string.IsNullOrWhiteSpace(u.UserName) && !u.UserName.Contains("@"))
            {
                taken.Add(u.UserName);
            }
        }

        foreach (var user in legacyUsers)
        {
            var oldUserName = user.UserName;
            var candidate = DeriveBaseUsername(user.Email ?? oldUserName!);

            // Resolve collisions against taken + already-assigned candidates.
            var final = candidate;
            var suffix = 2;
            while (taken.Contains(final))
            {
                final = $"{candidate}{suffix++}";
            }

            taken.Add(final);
            user.UserName = final;
            user.NormalizedUserName = final.ToUpperInvariant();

            _logger.LogInformation(
                "Migrated legacy username {OldUserName} -> {NewUserName} (user {UserId})",
                oldUserName ?? string.Empty,
                final,
                user.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string DeriveBaseUsername(string emailOrUsername)
    {
        var localPart = emailOrUsername.Split('@')[0];
        var sanitized = new string(localPart.Where(AllowedChars.Contains).ToArray());
        if (sanitized.Length == 0)
        {
            sanitized = "user";
        }

        return sanitized;
    }
}
