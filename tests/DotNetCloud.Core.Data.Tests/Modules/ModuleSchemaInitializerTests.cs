using DotNetCloud.Core.Data.Modules;

namespace DotNetCloud.Core.Data.Tests.Modules;

/// <summary>
/// Guards the error classification that lets a module baseline a schema which already holds its
/// tables (adopted installs) instead of dying on the first replayed migration.
/// </summary>
[TestClass]
public class ModuleSchemaInitializerTests
{
    /// <summary>
    /// Real provider messages observed when a module re-applies its initial migration against a
    /// schema that already contains the tables. All of them mean "this batch is already satisfied".
    /// </summary>
    [TestMethod]
    [DataRow("42P07: relation \"EmailAccounts\" already exists", true)]
    [DataRow("There is already an object named 'Bookmarks' in the database.", true)]
    [DataRow("23505: duplicate key value violates unique constraint \"PK_EmailAccounts\"", true)]
    [DataRow(
        "Introducing FOREIGN KEY constraint 'FK_BookmarkFolders_Bookmarks_BookmarkId' on table 'BookmarkFolders' " +
        "may cause cycles or multiple cascade paths.",
        true)]
    [DataRow(
        "INSERT failed because the following SET options have incorrect settings: 'QUOTED_IDENTIFIER'.",
        true)]
    [DataRow(
        "The filter expression 'X' for index 'IX_EmailMessages' on table 'EmailMessages' is incorrect. Incorrect WHERE clause.",
        true)]
    public void IsBenignCreateConflict_AlreadySatisfiedBatch_ReturnsTrue(string message, bool expected)
    {
        Assert.AreEqual(expected, ModuleSchemaInitializer.IsBenignCreateConflict(message));
    }

    /// <summary>
    /// Real failures must stay failures: a missing foreign-key target is retried on the next pass,
    /// and an unreachable object is not a reason to pretend the schema is complete.
    /// </summary>
    [TestMethod]
    [DataRow("23503: insert or update on table \"Bookmarks\" violates foreign key constraint", false)]
    [DataRow("42P01: relation \"Bookmarks\" does not exist", false)]
    [DataRow("28P01: password authentication failed for user \"dnc_mod_example_00000000\"", false)]
    public void IsBenignCreateConflict_RealFailure_ReturnsFalse(string message, bool expected)
    {
        Assert.AreEqual(expected, ModuleSchemaInitializer.IsBenignCreateConflict(message));
    }
}
