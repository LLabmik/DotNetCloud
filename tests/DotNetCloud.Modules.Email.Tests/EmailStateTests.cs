using DotNetCloud.Modules.Email.Web.Services;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Covers the pure helpers the Email module UI relies on.
/// </summary>
/// <remarks>
/// The host stores addresses as JSON columns and binds enums as numbers. These helpers are the only
/// place the UI turns those shapes into text, so they are pinned here rather than in the browser.
/// </remarks>
[TestClass]
public class EmailStateTests
{
    [TestMethod]
    public void ParseRecipients_NameAndAddress_ParsesBoth()
    {
        var recipients = EmailState.ParseRecipients("Ben Kimball <ben@example.com>, other@example.com");

        Assert.AreEqual(2, recipients.Count);
        StringAssert.Contains(recipients[0].ToString(), "ben@example.com");
        StringAssert.Contains(recipients[0].ToString(), "Ben Kimball");
        StringAssert.Contains(recipients[1].ToString(), "other@example.com");
    }

    [TestMethod]
    public void ParseRecipients_BlankInput_ReturnsNothing()
    {
        Assert.AreEqual(0, EmailState.ParseRecipients("  ,  ").Count);
        Assert.AreEqual(0, EmailState.ParseRecipients(null).Count);
    }

    [TestMethod]
    public void FormatFrom_UsesTheDisplayNameThenTheAddress()
    {
        Assert.AreEqual("Ben", EmailState.FormatFrom("""[{"name":"Ben","email":"ben@example.com"}]"""));
        Assert.AreEqual("ben@example.com", EmailState.FormatFrom("""[{"name":null,"email":"ben@example.com"}]"""));
        Assert.AreEqual("Unknown sender", EmailState.FormatFrom("[]"));
    }

    [TestMethod]
    public void FormatParticipants_LimitsToThreeAddresses()
    {
        var json = """
            [
              {"name":"A","email":"a@example.com"},
              {"name":"B","email":"b@example.com"},
              {"name":"C","email":"c@example.com"},
              {"name":"D","email":"d@example.com"}
            ]
            """;

        Assert.AreEqual("A, B, C", EmailState.FormatParticipants(json));
        Assert.AreEqual("Unknown", EmailState.FormatParticipants(null));
    }

    [TestMethod]
    public void ParseAddresses_MalformedJson_ReturnsEmptyInsteadOfThrowing()
    {
        Assert.AreEqual(0, EmailState.ParseAddresses("{not json").Count);
        Assert.AreEqual(0, EmailState.ParseAddresses(null).Count);
    }
}
