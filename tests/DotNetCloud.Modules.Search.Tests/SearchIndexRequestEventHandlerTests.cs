using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Search.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SearchIndexRequestEventHandler"/>.
/// </summary>
[TestClass]
public class SearchIndexRequestEventHandlerTests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private SearchIndexRequestEventHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _searchProviderMock.Object);
    }

    [TestMethod]
    public async Task HandleAsync_RemoveAction_CallsRemoveDocument()
    {
        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Remove
        };

        await _handler.HandleAsync(@event);

        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync("files", "entity-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_IndexAction_DoesNotCallRemove()
    {
        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Index
        };

        await _handler.HandleAsync(@event);

        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NullSearchProvider_DoesNotThrow()
    {
        var handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            null);

        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Remove
        };

        await handler.HandleAsync(@event);
        // Should not throw
    }
}
