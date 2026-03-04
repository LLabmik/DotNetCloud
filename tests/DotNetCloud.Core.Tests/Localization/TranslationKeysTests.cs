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
        AssertConstantValue(typeof(TranslationKeys.Common), nameof(TranslationKeys.Common.AppName), "AppName");
        AssertConstantValue(typeof(TranslationKeys.Common), nameof(TranslationKeys.Common.Save), "Save");
        AssertConstantValue(typeof(TranslationKeys.Common), nameof(TranslationKeys.Common.Cancel), "Cancel");
        AssertConstantValue(typeof(TranslationKeys.Common), nameof(TranslationKeys.Common.Dashboard), "Dashboard");
        AssertConstantValue(typeof(TranslationKeys.Common), nameof(TranslationKeys.Common.Settings), "Settings");
        AssertConstantValue(typeof(TranslationKeys.Common), nameof(TranslationKeys.Common.Logout), "Logout");
    }

    [TestMethod]
    public void Auth_ContainsExpectedKeys()
    {
        AssertConstantValue(typeof(TranslationKeys.Auth), nameof(TranslationKeys.Auth.Login), "Auth_Login");
        AssertConstantValue(typeof(TranslationKeys.Auth), nameof(TranslationKeys.Auth.Register), "Auth_Register");
        AssertConstantValue(typeof(TranslationKeys.Auth), nameof(TranslationKeys.Auth.Email), "Auth_Email");
        AssertConstantValue(typeof(TranslationKeys.Auth), nameof(TranslationKeys.Auth.Password), "Auth_Password");
    }

    [TestMethod]
    public void Errors_ContainsExpectedKeys()
    {
        AssertConstantValue(typeof(TranslationKeys.Errors), nameof(TranslationKeys.Errors.UnexpectedError), "Error_Unexpected");
        AssertConstantValue(typeof(TranslationKeys.Errors), nameof(TranslationKeys.Errors.NotFound), "Error_NotFound");
        AssertConstantValue(typeof(TranslationKeys.Errors), nameof(TranslationKeys.Errors.Unauthorized), "Error_Unauthorized");
    }

    [TestMethod]
    public void Validation_ContainsExpectedKeys()
    {
        AssertConstantValue(typeof(TranslationKeys.Validation), nameof(TranslationKeys.Validation.Required), "Validation_Required");
        AssertConstantValue(typeof(TranslationKeys.Validation), nameof(TranslationKeys.Validation.InvalidEmail), "Validation_InvalidEmail");
        AssertConstantValue(typeof(TranslationKeys.Validation), nameof(TranslationKeys.Validation.PasswordMismatch), "Validation_PasswordMismatch");
    }

    [TestMethod]
    public void Admin_ContainsExpectedKeys()
    {
        AssertConstantValue(typeof(TranslationKeys.Admin), nameof(TranslationKeys.Admin.Users), "Admin_Users");
        AssertConstantValue(typeof(TranslationKeys.Admin), nameof(TranslationKeys.Admin.Modules), "Admin_Modules");
        AssertConstantValue(typeof(TranslationKeys.Admin), nameof(TranslationKeys.Admin.Health), "Admin_Health");
    }

    private static void AssertConstantValue(Type type, string fieldName, string expectedValue)
    {
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, $"{type.Name}.{fieldName} field not found");
        var value = (string?)field.GetRawConstantValue();
        Assert.AreEqual(expectedValue, value, $"{type.Name}.{fieldName} has unexpected value");
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
