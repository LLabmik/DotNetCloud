using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using IAuditLogger = DotNetCloud.Core.Capabilities.IAuditLogger;
using AuditEntry = DotNetCloud.Core.Capabilities.AuditEntry;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// An <see cref="IEventBus"/> that records published events instead of dispatching them.
/// </summary>
internal sealed class RecordingEventBus : IEventBus
{
    /// <summary>Events published through this bus, in order.</summary>
    public List<object> Published { get; } = [];

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        Published.Add(@event!);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent
        => Task.CompletedTask;
}

/// <summary>
/// An <see cref="IAuditLogger"/> that records entries in memory.
/// </summary>
internal sealed class RecordingAuditLogger : IAuditLogger
{
    /// <summary>Audit entries recorded through this logger.</summary>
    public List<AuditEntry> Entries { get; } = [];

    /// <inheritdoc />
    public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>
/// An <see cref="IChatSettingsProvider"/> that always returns the same settings and counts reads,
/// so tests can drive the enforcement paths deterministically.
/// </summary>
internal sealed class FixedChatSettingsProvider : IChatSettingsProvider
{
    private ChatSettings _settings;

    /// <summary>Initializes a new instance of the <see cref="FixedChatSettingsProvider"/> class.</summary>
    /// <param name="settings">Settings to return.</param>
    public FixedChatSettingsProvider(ChatSettings settings)
    {
        _settings = settings.Normalized();
    }

    /// <summary>Number of times <see cref="GetSettingsAsync"/> was called.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Replaces the settings returned by subsequent reads.</summary>
    /// <param name="settings">The new settings.</param>
    public void Set(ChatSettings settings) => _settings = settings.Normalized();

    /// <inheritdoc />
    public Task<ChatSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(_settings);
    }

    /// <inheritdoc />
    public void Invalidate()
    {
    }
}

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> over a fixed set of options, mirroring
/// <c>ChatDbContextFactory</c> without a container.
/// </summary>
internal sealed class TestChatDbContextFactory : IDbContextFactory<ChatDbContext>
{
    private readonly DbContextOptions<ChatDbContext> _options;

    /// <summary>Initializes a new instance of the <see cref="TestChatDbContextFactory"/> class.</summary>
    /// <param name="options">Options used to build each context.</param>
    public TestChatDbContextFactory(DbContextOptions<ChatDbContext> options) => _options = options;

    /// <inheritdoc />
    public ChatDbContext CreateDbContext() => new(_options);

    /// <inheritdoc />
    public Task<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
