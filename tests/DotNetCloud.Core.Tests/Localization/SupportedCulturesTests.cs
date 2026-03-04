namespace DotNetCloud.Core.Tests.Localization;

using System.Globalization;
using DotNetCloud.Core.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="SupportedCultures"/>.
/// </summary>
[TestClass]
public class SupportedCulturesTests
{
    [TestMethod]
    public void DefaultCulture_IsEnUs()
    {
        Assert.AreEqual("en-US", SupportedCultures.DefaultCulture);
    }

    [TestMethod]
    public void All_ContainsDefaultCulture()
    {
        CollectionAssert.Contains(SupportedCultures.All, SupportedCultures.DefaultCulture);
    }

    [TestMethod]
    public void All_ContainsExpectedCultures()
    {
        var expected = new[] { "en-US", "es-ES", "de-DE", "fr-FR", "pt-BR", "ja-JP", "zh-CN" };
        CollectionAssert.AreEquivalent(expected, SupportedCultures.All);
    }

    [TestMethod]
    public void All_HasNoDuplicates()
    {
        var distinct = SupportedCultures.All.Distinct().ToArray();
        Assert.AreEqual(SupportedCultures.All.Length, distinct.Length);
    }

    [TestMethod]
    public void DisplayNames_HasEntryForEverySupportedCulture()
    {
        foreach (var culture in SupportedCultures.All)
        {
            Assert.IsTrue(
                SupportedCultures.DisplayNames.ContainsKey(culture),
                $"DisplayNames missing entry for '{culture}'");
        }
    }

    [TestMethod]
    public void DisplayNames_ValuesAreNotEmpty()
    {
        foreach (var kvp in SupportedCultures.DisplayNames)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(kvp.Value),
                $"DisplayName for '{kvp.Key}' is null or empty");
        }
    }

    [TestMethod]
    public void DisplayNames_IsCaseInsensitive()
    {
        Assert.IsTrue(SupportedCultures.DisplayNames.ContainsKey("EN-US"));
        Assert.IsTrue(SupportedCultures.DisplayNames.ContainsKey("en-us"));
    }

    [TestMethod]
    public void GetCultureInfos_ReturnsCorrectCount()
    {
        var infos = SupportedCultures.GetCultureInfos();
        Assert.AreEqual(SupportedCultures.All.Length, infos.Length);
    }

    [TestMethod]
    public void GetCultureInfos_ReturnsCultureInfoInstances()
    {
        var infos = SupportedCultures.GetCultureInfos();

        foreach (var info in infos)
        {
            Assert.IsInstanceOfType<CultureInfo>(info);
        }
    }

    [TestMethod]
    public void GetCultureInfos_MatchesAllTags()
    {
        var infos = SupportedCultures.GetCultureInfos();
        var names = infos.Select(ci => ci.Name).ToArray();

        CollectionAssert.AreEqual(SupportedCultures.All, names);
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("es-ES")]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("pt-BR")]
    [DataRow("ja-JP")]
    [DataRow("zh-CN")]
    public void All_ContainsValidBcp47Tag(string tag)
    {
        // Verifies that the tag resolves to a valid CultureInfo
        var culture = new CultureInfo(tag);
        Assert.AreEqual(tag, culture.Name);
    }
}
