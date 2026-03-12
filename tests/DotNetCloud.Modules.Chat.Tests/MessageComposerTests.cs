using Microsoft.AspNetCore.Components;

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

        composer.SetPlainText("Hello @");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);
        Assert.AreEqual(3, composer.TestVisibleMentionSuggestions.Count);
    }

    [TestMethod]
    public void WhenMentionQueryTypedThenSuggestionsAreFilteredByDisplayNameAndUsername()
    {
        var composer = CreateComposer();

        composer.SetPlainText("Hello @bea");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);
        CollectionAssert.AreEqual(new[] { "Beatrice Kim" }, composer.TestVisibleMentionSuggestions.Select(member => member.DisplayName).ToArray());

        composer.SetPlainText("Hello @cst");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);
        CollectionAssert.AreEqual(new[] { "Charlie Stone" }, composer.TestVisibleMentionSuggestions.Select(member => member.DisplayName).ToArray());
    }

    [TestMethod]
    public void WhenMentionSelectedThenDropdownCloses()
    {
        var composer = CreateComposer();

        composer.SetPlainText("Hello @bea");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);

        // Simulate clearing mention state (JS handles the DOM insertion)
        composer.SetPlainText("Hello @Beatrice Kim ");

        Assert.IsFalse(composer.TestIsMentionDropdownVisible);
    }

    [TestMethod]
    public void WhenEscapePressedThenMentionDropdownCloses()
    {
        var composer = CreateComposer();

        composer.SetPlainText("Hello @al");

        Assert.IsTrue(composer.TestIsMentionDropdownVisible);

        // Simulate clearing mentions (as HandleEscapeKey would do via JS)
        composer.SetPlainText("Hello @al ");

        Assert.IsFalse(composer.TestIsMentionDropdownVisible);
    }

    [TestMethod]
    public void WhenMentionTokenEndsThenDropdownCloses()
    {
        var composer = CreateComposer();

        composer.SetPlainText("Hello @alex hi");

        Assert.IsFalse(composer.TestIsMentionDropdownVisible);
    }

    [TestMethod]
    public async Task WhenValidPastedImagePayloadThenCallbackReceivesDecodedBytes()
    {
        var composer = CreateComposer();
        var callbackReceiver = new object();
        PastedImageData? receivedPayload = null;

        composer.OnPasteImage = EventCallback.Factory.Create<PastedImageData>(callbackReceiver, payload => receivedPayload = payload);

        await composer.ProcessPastedImageForTestAsync(
            fileName: "clip.png",
            contentType: "image/png",
            dataUrl: "data:image/png;base64,SGVsbG8=",
            sizeBytes: 5);

        Assert.IsNotNull(receivedPayload);
        Assert.AreEqual("clip.png", receivedPayload.FileName);
        Assert.AreEqual("image/png", receivedPayload.ContentType);
        Assert.AreEqual(5, receivedPayload.SizeBytes);
        CollectionAssert.AreEqual(new byte[] { 72, 101, 108, 108, 111 }, receivedPayload.Data);
    }

    [TestMethod]
    public async Task WhenInvalidPastedImagePayloadThenCallbackIsNotInvoked()
    {
        var composer = CreateComposer();
        var callbackReceiver = new object();
        var callbackCount = 0;

        composer.OnPasteImage = EventCallback.Factory.Create<PastedImageData>(callbackReceiver, _ => callbackCount++);

        await composer.ProcessPastedImageForTestAsync(
            fileName: "clip.png",
            contentType: "image/png",
            dataUrl: "not-a-data-url",
            sizeBytes: 0);

        Assert.AreEqual(0, callbackCount);
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

        public void SetPlainText(string value)
        {
            HandleContentChanged(value, string.IsNullOrWhiteSpace(value));
        }

        public Task ChooseMentionAsync(MemberViewModel member)
        {
            return SelectMentionAsync(member);
        }

        public Task ProcessPastedImageForTestAsync(string fileName, string contentType, string dataUrl, long sizeBytes)
        {
            return ProcessPastedImageAsync(fileName, contentType, dataUrl, sizeBytes);
        }
    }
}