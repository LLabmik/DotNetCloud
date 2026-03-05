using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

[TestClass]
public class PasswordValidatorTests
{
    [TestMethod]
    public void WhenPasswordIsNull_ThenReturnsEmptyError()
    {
        var result = PasswordValidator.Validate(null!);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordIsEmpty_ThenReturnsEmptyError()
    {
        var result = PasswordValidator.Validate("");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordIsWhitespace_ThenReturnsEmptyError()
    {
        var result = PasswordValidator.Validate("   ");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordTooShort_ThenReturnsLengthError()
    {
        var result = PasswordValidator.Validate("Ab1!xyz");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("10", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordExactlyMinLength_ThenPassesLengthCheck()
    {
        // 10 chars, has upper + lower + digit = 3 categories
        var result = PasswordValidator.Validate("Abcdefgh1x");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenPasswordOnlyLowercase_ThenReturnsCategoryError()
    {
        var result = PasswordValidator.Validate("abcdefghijklmn");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("uppercase", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordOnlyUpperAndLower_ThenReturnsCategoryError()
    {
        var result = PasswordValidator.Validate("AbCdEfGhIjKl");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("3 of", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordHasUpperLowerDigit_ThenPasses()
    {
        var result = PasswordValidator.Validate("MySecret99xx");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenPasswordHasUpperLowerSpecial_ThenPasses()
    {
        var result = PasswordValidator.Validate("MySecret!!xx");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenPasswordHasLowerDigitSpecial_ThenPasses()
    {
        var result = PasswordValidator.Validate("mysecret9!!x");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenPasswordHasAllFourCategories_ThenPasses()
    {
        var result = PasswordValidator.Validate("MySecret9!xx");
        Assert.IsNull(result);
    }

    [TestMethod]
    [DataRow("password1234X!")]
    [DataRow("Password123!!")]
    [DataRow("Xqwerty12345")]
    [DataRow("letmein!!ABC")]
    [DataRow("Xdotnetcloud1!")]
    [DataRow("mypostgres!!X")]
    public void WhenPasswordContainsCommonWord_ThenReturnsCommonError(string password)
    {
        var result = PasswordValidator.Validate(password);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("common", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordMatchesForbidden_ThenTellsUserCannotReuse()
    {
        var dbPassword = "MyDbPass99!x";
        var result = PasswordValidator.Validate(dbPassword, dbPassword);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("cannot use the same password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenPasswordDoesNotMatchForbidden_ThenPasses()
    {
        var result = PasswordValidator.Validate("GoodSecure1!x", "DifferentPass1!");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenForbiddenIsEmpty_ThenIgnoresForbiddenCheck()
    {
        var result = PasswordValidator.Validate("MySecret99!x", "");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenMultipleForbiddenPasswords_ThenRejectsAnyMatch()
    {
        var password = "MySecret99!x";
        var result = PasswordValidator.Validate(password, "other1", password, "other2");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("cannot use the same password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WhenNoForbiddenPasswords_ThenStrongPasswordPasses()
    {
        var result = PasswordValidator.Validate("Str0ngP@ssw0rd!");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenForbiddenMatchDiffersInCase_ThenStillRejects()
    {
        // Forbidden check is case-insensitive — same password with different casing is still reuse
        var result = PasswordValidator.Validate("MyDbPass99!x", "mydbpass99!x");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("cannot use the same password", StringComparison.OrdinalIgnoreCase));
    }
}
