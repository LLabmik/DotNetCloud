using Microsoft.AspNetCore.Components.Web;

using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="MessageComposer"/> mention-autocomplete state management.
/// </summary>
[TestClass]
public class MessageComposerTests
{
    [TestMethod]
    public void WhenAtSymbolTypedThenAllMentionSuggestionsAreVisible()
    {
        var composer = CreateComposer();

        composer.SetMessageText("Hello @");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);
        Assert.AreEqual(3, composer.TestVisibleMentionSuggestions.Count);
    }

    [TestMethod]
    public void WhenMentionQueryTypedThenSuggestionsAreFilteredByDisplayNameAndUsername()
    {
        var composer = CreateComposer();

        composer.SetMessageText("Hello @bea");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);
        CollectionAssert.AreEqual(new[] { "Beatrice Kim" }, composer.TestVisibleMentionSuggestions.Select(member => member.DisplayName).ToArray());

        composer.SetMessageText("Hello @cst");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);
        CollectionAssert.AreEqual(new[] { "Charlie Stone" }, composer.TestVisibleMentionSuggestions.Select(member => member.DisplayName).ToArray());
    }

    [TestMethod]
    public async Task WhenMentionSelectedThenDisplayNameIsInsertedAndDropdownCloses()
    {
        var composer = CreateComposer();
        var targetMember = composer.AllMentionSuggestions[1];

        composer.SetMessageText("Hello @bea");
        await composer.ChooseMentionAsync(targetMember);

        Assert.AreEqual("Hello @Beatrice Kim ", composer.TestMessageText);
        Assert.IsFalse(composer.TestIsMentionDropdownVisible);
    }

    [TestMethod]
    public async Task WhenEscapePressedThenMentionDropdownCloses()
    {
        var composer = CreateComposer();

        composer.SetMessageText("Hello @al");
        await composer.HandleKeyAsync("Escape");

        Assert.IsFalse(composer.TestIsMentionDropdownVisible);
    }

    [TestMethod]
    public void WhenMentionTokenEndsThenDropdownCloses()
    {
        var composer = CreateComposer();

        composer.SetMessageText("Hello @alex hi");

        Assert.IsFalse(composer.TestIsMentionDropdownVisible);
    }

    private static TestableComposer CreateComposer()
    {
        return new TestableComposer
        {
            MentionSuggestions =
            [
                new MemberViewModel { UserId = Guid.NewGuid(), DisplayName = "Alex Carter", Username = "acarter" },
                new MemberViewModel { UserId = Guid.NewGuid(), DisplayName = "Beatrice Kim", Username = "bea" },
                new MemberViewModel { UserId = Guid.NewGuid(), DisplayName = "Charlie Stone", Username = "cstone" }
            ]
        };
    }

    /// <summary>
    /// Test accessor subclass that exposes protected mention state.
    /// </summary>
    private sealed class TestableComposer : MessageComposer
    {
        public bool TestIsMentionDropdownVisible => IsMentionDropdownVisible;

        public IReadOnlyList<MemberViewModel> TestVisibleMentionSuggestions => VisibleMentionSuggestions;

        public IReadOnlyList<MemberViewModel> AllMentionSuggestions => MentionSuggestions;

        public string TestMessageText => MessageText;

        public void SetMessageText(string value)
        {
            MessageText = value;
        }

        public Task ChooseMentionAsync(MemberViewModel member)
        {
            return SelectMentionAsync(member);
        }

        public Task HandleKeyAsync(string key)
        {
            return HandleKeyDown(new KeyboardEventArgs { Key = key });
        }
    }
}