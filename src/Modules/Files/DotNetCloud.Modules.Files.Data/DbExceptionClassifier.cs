using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Classifies provider-specific database exceptions in a provider-agnostic way.
/// </summary>
internal static class DbExceptionClassifier
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ex"/> represents a unique constraint violation.
    /// Supports PostgreSQL (23505), SQLite (19/2067), and SQL Server (2601/2627).
    /// </summary>
    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (TryGetSqlState(ex, out var sqlState) && sqlState == "23505")
            return true;

        if (TryGetErrorCode(ex, out var errorCode) && (errorCode == 19 || errorCode == 2067 || errorCode == 2601 || errorCode == 2627))
            return true;

        return false;
    }

    private static bool TryGetSqlState(DbUpdateException ex, out string? sqlState)
    {
        sqlState = null;

        if (TryReadStringMember(ex.InnerException, "SqlState", out sqlState))
            return true;

        if (TryReadStringData(ex.InnerException, "SqlState", out sqlState))
            return true;

        return false;
    }

    private static bool TryGetErrorCode(DbUpdateException ex, out int errorCode)
    {
        errorCode = 0;

        if (TryReadIntMember(ex.InnerException, "SqliteErrorCode", out errorCode))
            return true;

        if (TryReadIntMember(ex.InnerException, "Number", out errorCode))
            return true;

        if (TryReadIntData(ex.InnerException, "SqliteErrorCode", out errorCode))
            return true;

        if (TryReadIntData(ex.InnerException, "Number", out errorCode))
            return true;

        return false;
    }

    private static bool TryReadStringMember(Exception? exception, string memberName, out string? value)
    {
        value = null;
        if (exception is null)
            return false;

        var property = exception.GetType().GetProperty(memberName);
        if (property?.GetValue(exception) is string stringValue)
        {
            value = stringValue;
            return true;
        }

        return false;
    }

    private static bool TryReadIntMember(Exception? exception, string memberName, out int value)
    {
        value = 0;
        if (exception is null)
            return false;

        var property = exception.GetType().GetProperty(memberName);
        if (property?.GetValue(exception) is int intValue)
        {
            value = intValue;
            return true;
        }

        return false;
    }

    private static bool TryReadStringData(Exception? exception, string key, out string? value)
    {
        value = null;
        if (exception?.Data[key] is string stringValue)
        {
            value = stringValue;
            return true;
        }

        return false;
    }

    private static bool TryReadIntData(Exception? exception, string key, out int value)
    {
        value = 0;
        if (exception?.Data[key] is int intValue)
        {
            value = intValue;
            return true;
        }

        return false;
    }
}