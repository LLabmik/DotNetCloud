namespace DotNetCloud.Core.Tests.Localization;

using System.Reflection;
using DotNetCloud.Core.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="TranslationKeys"/> ensuring all key constants
/// are non-empty, unique, and properly categorized.
/// </summary>
[TestClass]
public class TranslationKeysTests
{
    [TestMethod]
    public void TranslationKeys_HasExpectedNestedClasses()
    {
        var nested = typeof(TranslationKeys).GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
        var names = nested.Select(t => t.Name).OrderBy(n => n).ToArray();

        CollectionAssert.Contains(names, nameof(TranslationKeys.Common));
        CollectionAssert.Contains(names, nameof(TranslationKeys.Auth));
        CollectionAssert.Contains(names, nameof(TranslationKeys.Errors));
        CollectionAssert.Contains(names, nameof(TranslationKeys.Validation));
        CollectionAssert.Contains(names, nameof(TranslationKeys.Admin));
    }

    [TestMethod]
    public void Common_AllKeysAreNonEmpty()
    {
        AssertAllConstantsNonEmpty(typeof(TranslationKeys.Common));
    }

    [TestMethod]
    public void Auth_AllKeysAreNonEmpty()
    {
        AssertAllConstantsNonEmpty(typeof(TranslationKeys.Auth));
    }

    [TestMethod]
    public void Errors_AllKeysAreNonEmpty()
    {
        AssertAllConstantsNonEmpty(typeof(TranslationKeys.Errors));
    }

    [TestMethod]
    public void Validation_AllKeysAreNonEmpty()
    {
        AssertAllConstantsNonEmpty(typeof(TranslationKeys.Validation));
    }

    [TestMethod]
    public void Admin_AllKeysAreNonEmpty()
    {
        AssertAllConstantsNonEmpty(typeof(TranslationKeys.Admin));
    }

    [TestMethod]
    public void AllKeys_AreGloballyUnique()
    {
        var allKeys = GetAllKeys();
        var duplicates = allKeys
            .GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.AreEqual(0, duplicates.Length,
            $"Duplicate translation keys found: {string.Join(", ", duplicates)}");
    }

    [TestMethod]
    public void Common_ContainsExpectedKeys()
    {
        Assert.AreEqual("AppName", TranslationKeys.Common.AppName);
        Assert.AreEqual("Save", TranslationKeys.Common.Save);
        Assert.AreEqual("Cancel", TranslationKeys.Common.Cancel);
        Assert.AreEqual("Dashboard", TranslationKeys.Common.Dashboard);
        Assert.AreEqual("Settings", TranslationKeys.Common.Settings);
        Assert.AreEqual("Logout", TranslationKeys.Common.Logout);
    }

    [TestMethod]
    public void Auth_ContainsExpectedKeys()
    {
        Assert.AreEqual("Auth_Login", TranslationKeys.Auth.Login);
        Assert.AreEqual("Auth_Register", TranslationKeys.Auth.Register);
        Assert.AreEqual("Auth_Email", TranslationKeys.Auth.Email);
        Assert.AreEqual("Auth_Password", TranslationKeys.Auth.Password);
    }

    [TestMethod]
    public void Errors_ContainsExpectedKeys()
    {
        Assert.AreEqual("Error_Unexpected", TranslationKeys.Errors.UnexpectedError);
        Assert.AreEqual("Error_NotFound", TranslationKeys.Errors.NotFound);
        Assert.AreEqual("Error_Unauthorized", TranslationKeys.Errors.Unauthorized);
    }

    [TestMethod]
    public void Validation_ContainsExpectedKeys()
    {
        Assert.AreEqual("Validation_Required", TranslationKeys.Validation.Required);
        Assert.AreEqual("Validation_InvalidEmail", TranslationKeys.Validation.InvalidEmail);
        Assert.AreEqual("Validation_PasswordMismatch", TranslationKeys.Validation.PasswordMismatch);
    }

    [TestMethod]
    public void Admin_ContainsExpectedKeys()
    {
        Assert.AreEqual("Admin_Users", TranslationKeys.Admin.Users);
        Assert.AreEqual("Admin_Modules", TranslationKeys.Admin.Modules);
        Assert.AreEqual("Admin_Health", TranslationKeys.Admin.Health);
    }

    [TestMethod]
    public void AllKeys_TotalCountIsConsistent()
    {
        var allKeys = GetAllKeys();

        // Ensure we have a reasonable number of keys (sanity check)
        Assert.IsTrue(allKeys.Count > 30,
            $"Expected more than 30 translation keys, but found {allKeys.Count}");
    }

    private static void AssertAllConstantsNonEmpty(Type type)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

        foreach (var field in fields)
        {
            var value = (string?)field.GetRawConstantValue();
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(value),
                $"{type.Name}.{field.Name} has a null or empty value");
        }
    }

    private static List<string> GetAllKeys()
    {
        var keys = new List<string>();
        var nestedTypes = typeof(TranslationKeys).GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

        foreach (var nested in nestedTypes)
        {
            var fields = nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

            foreach (var field in fields)
            {
                var value = (string?)field.GetRawConstantValue();
                if (value is not null)
                {
                    keys.Add(value);
                }
            }
        }

        return keys;
    }
}
