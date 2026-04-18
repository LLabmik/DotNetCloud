using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Data.Services;
using DotNetCloud.Modules.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="AiChatService"/>.
/// </summary>
[TestClass]
public class AiChatServiceTests
{
    private AiDbContext _db;
    private AiChatService _service;
    private Mock<IOllamaClient> _ollamaMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AiDbContext(options);
        _ollamaMock = new Mock<IOllamaClient>();
        _service = new AiChatService(_db, _ollamaMock.Object, NullLogger<AiChatService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), new[] { "user" }, CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateConversation_ValidInput_ReturnsConversation()
    {
        var conversation = await _service.CreateConversationAsync(_caller, "Test Chat", "gpt-oss:20b", null);

        Assert.IsNotNull(conversation);
        Assert.AreEqual("Test Chat", conversation.Title);
        Assert.AreEqual("gpt-oss:20b", conversation.Model);
        Assert.AreEqual(_caller.UserId, conversation.OwnerId);
    }

    [TestMethod]
    public async Task CreateConversation_NullTitle_DefaultsToNewConversation()
    {
        var conversation = await _service.CreateConversationAsync(_caller, null, "gpt-oss:20b", null);

        Assert.AreEqual("New Conversation", conversation.Title);
    }

    [TestMethod]
    public async Task CreateConversation_WithSystemPrompt_StoresPrompt()
    {
        var prompt = "You are a helpful coding assistant.";
        var conversation = await _service.CreateConversationAsync(_caller, "Code Help", "gpt-oss:20b", prompt);

        Assert.AreEqual(prompt, conversation.SystemPrompt);
    }

    [TestMethod]
    public async Task GetConversation_OwnedByUser_ReturnsConversation()
    {
        var created = await _service.CreateConversationAsync(_caller, "Test", "gpt-oss:20b", null);

        var result = await _service.GetConversationAsync(_caller, created.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task GetConversation_OwnedByOtherUser_ReturnsNull()
    {
        var created = await _service.CreateConversationAsync(_caller, "Test", "gpt-oss:20b", null);

        var otherCaller = new CallerContext(Guid.NewGuid(), new[] { "user" }, CallerType.User);
        var result = await _service.GetConversationAsync(otherCaller, created.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListConversations_ReturnsOnlyOwnedConversations()
    {
        await _service.CreateConversationAsync(_caller, "Chat 1", "gpt-oss:20b", null);
        await _service.CreateConversationAsync(_caller, "Chat 2", "gpt-oss:20b", null);

        var otherCaller = new CallerContext(Guid.NewGuid(), new[] { "user" }, CallerType.User);
        await _service.CreateConversationAsync(otherCaller, "Other Chat", "gpt-oss:20b", null);

        var conversations = await _service.ListConversationsAsync(_caller);

        Assert.AreEqual(2, conversations.Count);
    }

    [TestMethod]
    public async Task DeleteConversation_OwnedByUser_ReturnsTrue()
    {
        var created = await _service.CreateConversationAsync(_caller, "To Delete", "gpt-oss:20b", null);

        var result = await _service.DeleteConversationAsync(_caller, created.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteConversation_SoftDeleteHidesFromList()
    {
        var created = await _service.CreateConversationAsync(_caller, "To Delete", "gpt-oss:20b", null);

        await _service.DeleteConversationAsync(_caller, created.Id);

        var conversations = await _service.ListConversationsAsync(_caller);
        Assert.AreEqual(0, conversations.Count);
    }

    [TestMethod]
    public async Task DeleteConversation_NonExistent_ReturnsFalse()
    {
        var result = await _service.DeleteConversationAsync(_caller, Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendMessage_PersistsUserAndAssistantMessages()
    {
        var conversation = await _service.CreateConversationAsync(_caller, "Test", "gpt-oss:20b", null);

        _ollamaMock.Setup(o => o.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "gpt-oss:20b",
                Message = new LlmMessage("assistant", "Hello! How can I help?"),
                Done = true,
                EvalCount = 10,
                PromptEvalCount = 5
            });

        var response = await _service.SendMessageAsync(_caller, conversation.Id, "Hello");

        Assert.AreEqual("Hello! How can I help?", response.Message.Content);

        // Verify messages were persisted
        var loaded = await _service.GetConversationAsync(_caller, conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, loaded.Messages.Count); // user + assistant
    }

    [TestMethod]
    public async Task SendMessage_NonExistentConversation_ThrowsInvalidOperation()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendMessageAsync(_caller, Guid.NewGuid(), "Hello"));
    }

    [TestMethod]
    public async Task ListModels_DelegatesToOllamaClient()
    {
        var expected = new List<LlmModelInfo>
        {
            new() { Id = "gpt-oss:20b", Name = "gpt-oss:20b", Provider = "ollama" }
        };

        _ollamaMock.Setup(o => o.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var models = await _service.ListModelsAsync(_caller);

        Assert.AreEqual(1, models.Count);
        Assert.AreEqual("gpt-oss:20b", models[0].Id);
    }
}
