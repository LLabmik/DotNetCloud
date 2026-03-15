using DotNetCloud.Modules.Files.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Tests.Data;

[TestClass]
public class DbExceptionClassifierTests
{
    [TestMethod]
    public void IsUniqueConstraintViolation_PostgresSqlStateProperty_ReturnsTrue()
    {
        var ex = new DbUpdateException("duplicate", new FakePostgresException("23505"));

        var result = DbExceptionClassifier.IsUniqueConstraintViolation(ex);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUniqueConstraintViolation_PostgresSqlStateInData_ReturnsTrue()
    {
        var inner = new Exception("duplicate");
        inner.Data["SqlState"] = "23505";
        var ex = new DbUpdateException("duplicate", inner);

        var result = DbExceptionClassifier.IsUniqueConstraintViolation(ex);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUniqueConstraintViolation_SqlServerDuplicateKey_ReturnsTrue()
    {
        var ex = new DbUpdateException("duplicate", new FakeSqlServerException(2627));

        var result = DbExceptionClassifier.IsUniqueConstraintViolation(ex);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUniqueConstraintViolation_SqliteConstraintError_ReturnsTrue()
    {
        var ex = new DbUpdateException("duplicate", new FakeSqliteException(19));

        var result = DbExceptionClassifier.IsUniqueConstraintViolation(ex);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUniqueConstraintViolation_NonUniqueError_ReturnsFalse()
    {
        var ex = new DbUpdateException("other", new FakePostgresException("40001"));

        var result = DbExceptionClassifier.IsUniqueConstraintViolation(ex);

        Assert.IsFalse(result);
    }

    private sealed class FakePostgresException(string sqlState) : Exception
    {
        public string SqlState { get; } = sqlState;
    }

    private sealed class FakeSqlServerException(int number) : Exception
    {
        public int Number { get; } = number;
    }

    private sealed class FakeSqliteException(int sqliteErrorCode) : Exception
    {
        public int SqliteErrorCode { get; } = sqliteErrorCode;
    }
}
