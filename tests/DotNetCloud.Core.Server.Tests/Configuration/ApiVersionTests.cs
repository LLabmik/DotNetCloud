using DotNetCloud.Core.Server.Configuration;

namespace DotNetCloud.Core.Server.Tests.Configuration;

[TestClass]
public class ApiVersionTests
{
    [TestMethod]
    public void Parse_ValidMajorVersion_ReturnsCorrectVersion()
    {
        var version = ApiVersion.Parse("1");

        Assert.AreEqual(1, version.Major);
        Assert.IsNull(version.Minor);
    }

    [TestMethod]
    public void Parse_ValidMajorMinorVersion_ReturnsCorrectVersion()
    {
        var version = ApiVersion.Parse("2.1");

        Assert.AreEqual(2, version.Major);
        Assert.AreEqual(1, version.Minor);
    }

    [TestMethod]
    public void Parse_EmptyString_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => ApiVersion.Parse(""));
    }

    [TestMethod]
    public void Parse_NegativeMajor_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => ApiVersion.Parse("-1"));
    }

    [TestMethod]
    public void Parse_NonNumeric_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => ApiVersion.Parse("abc"));
    }

    [TestMethod]
    public void Parse_InvalidMinor_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => ApiVersion.Parse("1.abc"));
    }

    [TestMethod]
    public void TryParse_ValidVersion_ReturnsTrueAndVersion()
    {
        var result = ApiVersion.TryParse("3", out var version);

        Assert.IsTrue(result);
        Assert.IsNotNull(version);
        Assert.AreEqual(3, version.Major);
    }

    [TestMethod]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = ApiVersion.TryParse(null, out var version);

        Assert.IsFalse(result);
        Assert.IsNull(version);
    }

    [TestMethod]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var result = ApiVersion.TryParse("not-a-version", out var version);

        Assert.IsFalse(result);
        Assert.IsNull(version);
    }

    [TestMethod]
    public void CompareTo_SameMajor_ReturnsZero()
    {
        var v1 = new ApiVersion(1);
        var v2 = new ApiVersion(1);

        Assert.AreEqual(0, v1.CompareTo(v2));
    }

    [TestMethod]
    public void CompareTo_HigherMajor_ReturnsPositive()
    {
        var v1 = new ApiVersion(2);
        var v2 = new ApiVersion(1);

        Assert.IsTrue(v1.CompareTo(v2) > 0);
    }

    [TestMethod]
    public void CompareTo_LowerMajor_ReturnsNegative()
    {
        var v1 = new ApiVersion(1);
        var v2 = new ApiVersion(2);

        Assert.IsTrue(v1.CompareTo(v2) < 0);
    }

    [TestMethod]
    public void CompareTo_SameMajorHigherMinor_ReturnsPositive()
    {
        var v1 = new ApiVersion(1, 2);
        var v2 = new ApiVersion(1, 1);

        Assert.IsTrue(v1.CompareTo(v2) > 0);
    }

    [TestMethod]
    public void CompareTo_Null_ReturnsPositive()
    {
        var v1 = new ApiVersion(1);

        Assert.IsTrue(v1.CompareTo(null) > 0);
    }

    [TestMethod]
    public void Equals_SameVersion_ReturnsTrue()
    {
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(1, 0);

        Assert.IsTrue(v1.Equals(v2));
    }

    [TestMethod]
    public void Equals_MajorOnlyMatchesMajorZero_ReturnsTrue()
    {
        var v1 = new ApiVersion(1);
        var v2 = new ApiVersion(1, 0);

        Assert.IsTrue(v1.Equals(v2));
    }

    [TestMethod]
    public void Equals_DifferentVersion_ReturnsFalse()
    {
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(2, 0);

        Assert.IsFalse(v1.Equals(v2));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        var v1 = new ApiVersion(1);

        Assert.IsFalse(v1.Equals(null));
    }

    [TestMethod]
    public void GetHashCode_EqualVersions_ReturnsSameHash()
    {
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(1);

        Assert.AreEqual(v1.GetHashCode(), v2.GetHashCode());
    }

    [TestMethod]
    public void ToString_MajorOnly_ReturnsMajor()
    {
        var version = new ApiVersion(1);

        Assert.AreEqual("1", version.ToString());
    }

    [TestMethod]
    public void ToString_MajorMinor_ReturnsMajorDotMinor()
    {
        var version = new ApiVersion(2, 1);

        Assert.AreEqual("2.1", version.ToString());
    }
}
