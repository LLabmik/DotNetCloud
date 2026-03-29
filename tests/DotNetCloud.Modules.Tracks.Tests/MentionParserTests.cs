using DotNetCloud.Modules.Tracks.Services;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class MentionParserTests
{
    [TestMethod]
    public void ParseMentions_NullText_ReturnsEmpty()
    {
        var result = MentionParser.ParseMentions(null);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseMentions_EmptyString_ReturnsEmpty()
    {
        var result = MentionParser.ParseMentions("");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseMentions_WhitespaceOnly_ReturnsEmpty()
    {
        var result = MentionParser.ParseMentions("   ");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseMentions_NoMentions_ReturnsEmpty()
    {
        var result = MentionParser.ParseMentions("This is a regular comment with no mentions.");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseMentions_SingleMention_ReturnsSingle()
    {
        var result = MentionParser.ParseMentions("Hey @alice, check this out.");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("alice", result[0]);
    }

    [TestMethod]
    public void ParseMentions_MentionAtStart_Parsed()
    {
        var result = MentionParser.ParseMentions("@bob please review");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("bob", result[0]);
    }

    [TestMethod]
    public void ParseMentions_MultipleMentions_ReturnsAll()
    {
        var result = MentionParser.ParseMentions("@alice and @bob should review this with @charlie");
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("alice", result[0]);
        Assert.AreEqual("bob", result[1]);
        Assert.AreEqual("charlie", result[2]);
    }

    [TestMethod]
    public void ParseMentions_DuplicateMentions_ReturnsDistinct()
    {
        var result = MentionParser.ParseMentions("@alice and @alice again");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("alice", result[0]);
    }

    [TestMethod]
    public void ParseMentions_CaseInsensitiveDedupe_PreservesFirst()
    {
        var result = MentionParser.ParseMentions("@Alice and @alice");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alice", result[0]);
    }

    [TestMethod]
    public void ParseMentions_UsernameWithDots_Parsed()
    {
        var result = MentionParser.ParseMentions("Assigned to @john.doe");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("john.doe", result[0]);
    }

    [TestMethod]
    public void ParseMentions_UsernameWithHyphens_Parsed()
    {
        var result = MentionParser.ParseMentions("CC @mary-jane");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mary-jane", result[0]);
    }

    [TestMethod]
    public void ParseMentions_UsernameWithUnderscores_Parsed()
    {
        var result = MentionParser.ParseMentions("Ask @dev_user");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("dev_user", result[0]);
    }

    [TestMethod]
    public void ParseMentions_MentionInParentheses_Parsed()
    {
        var result = MentionParser.ParseMentions("(cc @alice)");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("alice", result[0]);
    }

    [TestMethod]
    public void ParseMentions_EmailAddress_NotParsedAsMention()
    {
        // "user@example" — the @ is preceded by a non-whitespace character
        var result = MentionParser.ParseMentions("Send email to user@example.com");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseMentions_SingleCharUsername_Parsed()
    {
        var result = MentionParser.ParseMentions("Hey @X what do you think?");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("X", result[0]);
    }
}
