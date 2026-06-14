using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Server.Grpc.Clients;
using Google.Protobuf.Collections;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using FilesProto = DotNetCloud.Modules.Files.Host.Protos;
using NotesProto = DotNetCloud.Modules.Notes.Host.Protos;
using CalendarProto = DotNetCloud.Modules.Calendar.Host.Protos;
using BookmarksProto = DotNetCloud.Modules.Bookmarks.Host.Protos;
using EmailProto = DotNetCloud.Modules.Email.Host.Protos;
using MusicProto = DotNetCloud.Modules.Music.Host.Protos;
using VideoProto = DotNetCloud.Modules.Video.Host.Protos;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Provides searchable documents from a process-isolated module via gRPC.
/// Replaces the old ISearchableModule DI pattern with gRPC-based document retrieval.
/// </summary>
internal interface IModuleSearchDocumentClient
{
    /// <summary>Gets the module identifier (e.g., "files", "notes").</summary>
    string ModuleId { get; }

    /// <summary>Returns all searchable documents from this module for a full reindex.</summary>
    Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken cancellationToken);

    /// <summary>Returns a single searchable document by entity ID.</summary>
    Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken cancellationToken);
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

internal static class SearchDocumentMapper
{
    public static SearchDocument ToSearchDocument(
        string moduleId,
        string entityId,
        string entityType,
        string title,
        string content,
        string summary,
        string ownerId,
        string createdAt,
        string updatedAt,
        MapField<string, string>? metadata = null)
    {
        return new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = entityId,
            EntityType = entityType,
            Title = title,
            Content = content,
            Summary = summary,
            OwnerId = Guid.TryParse(ownerId, out var oid) ? oid : Guid.Empty,
            CreatedAt = DateTimeOffset.Parse(createdAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTimeOffset.Parse(updatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Metadata = metadata is not null
                ? new Dictionary<string, string>(metadata)
                : new Dictionary<string, string>()
        };
    }
}

// ─── Files Module ────────────────────────────────────────────────────────────

internal sealed class FilesModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "files";

    private readonly Lazy<FilesProto.FilesService.FilesServiceClient> _client;
    private readonly ILogger<FilesModuleSearchClient> _logger;

    public FilesModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<FilesModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<FilesProto.FilesService.FilesServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.files");
            _logger.LogInformation("FilesModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new FilesProto.FilesService.FilesServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new FilesProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt, doc.Metadata));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "FilesModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new FilesProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt, d.Metadata);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "FilesModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}

// ─── Notes Module ────────────────────────────────────────────────────────────

internal sealed class NotesModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "notes";

    private readonly Lazy<NotesProto.NotesGrpcService.NotesGrpcServiceClient> _client;
    private readonly ILogger<NotesModuleSearchClient> _logger;

    public NotesModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<NotesModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<NotesProto.NotesGrpcService.NotesGrpcServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.notes");
            _logger.LogInformation("NotesModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new NotesProto.NotesGrpcService.NotesGrpcServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new NotesProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new NotesProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}

// ─── Calendar Module ─────────────────────────────────────────────────────────

internal sealed class CalendarModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "calendar";

    private readonly Lazy<CalendarProto.CalendarGrpcService.CalendarGrpcServiceClient> _client;
    private readonly ILogger<CalendarModuleSearchClient> _logger;

    public CalendarModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<CalendarModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<CalendarProto.CalendarGrpcService.CalendarGrpcServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.calendar");
            _logger.LogInformation("CalendarModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new CalendarProto.CalendarGrpcService.CalendarGrpcServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new CalendarProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "CalendarModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new CalendarProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "CalendarModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}

// ─── Bookmarks Module ────────────────────────────────────────────────────────

internal sealed class BookmarksModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "bookmarks";

    private readonly Lazy<BookmarksProto.BookmarksService.BookmarksServiceClient> _client;
    private readonly ILogger<BookmarksModuleSearchClient> _logger;

    public BookmarksModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<BookmarksModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<BookmarksProto.BookmarksService.BookmarksServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.bookmarks");
            _logger.LogInformation("BookmarksModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new BookmarksProto.BookmarksService.BookmarksServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new BookmarksProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new BookmarksProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "BookmarksModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}

// ─── Email Module ────────────────────────────────────────────────────────────

internal sealed class EmailModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "email";

    private readonly Lazy<EmailProto.EmailService.EmailServiceClient> _client;
    private readonly ILogger<EmailModuleSearchClient> _logger;

    public EmailModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<EmailModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<EmailProto.EmailService.EmailServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.email");
            _logger.LogInformation("EmailModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new EmailProto.EmailService.EmailServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new EmailProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "EmailModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new EmailProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "EmailModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}

// ─── Music Module ────────────────────────────────────────────────────────────

internal sealed class MusicModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "music";

    private readonly Lazy<MusicProto.MusicGrpcService.MusicGrpcServiceClient> _client;
    private readonly ILogger<MusicModuleSearchClient> _logger;

    public MusicModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<MusicModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<MusicProto.MusicGrpcService.MusicGrpcServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.music");
            _logger.LogInformation("MusicModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new MusicProto.MusicGrpcService.MusicGrpcServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new MusicProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt, doc.Metadata));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "MusicModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new MusicProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt, d.Metadata);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "MusicModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}

// ─── Video Module ────────────────────────────────────────────────────────────

internal sealed class VideoModuleSearchClient : IModuleSearchDocumentClient
{
    public string ModuleId => "video";

    private readonly Lazy<VideoProto.VideoGrpcService.VideoGrpcServiceClient> _client;
    private readonly ILogger<VideoModuleSearchClient> _logger;

    public VideoModuleSearchClient(ModuleEndpointProvider endpointProvider, ILogger<VideoModuleSearchClient> logger)
    {
        _logger = logger;
        _client = new Lazy<VideoProto.VideoGrpcService.VideoGrpcServiceClient>(() =>
        {
            var address = endpointProvider.GetEndpoint("dotnetcloud.video");
            _logger.LogInformation("VideoModuleSearchClient connecting to {Address}", address);
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) }
            });
            return new VideoProto.VideoGrpcService.VideoGrpcServiceClient(channel);
        });
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct)
    {
        var request = new VideoProto.GetSearchableDocumentsRequest();
        var results = new List<SearchDocument>();
        try
        {
            var call = _client.Value.GetSearchableDocuments(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            await foreach (var doc in call.ResponseStream.ReadAllAsync(ct))
            {
                results.Add(SearchDocumentMapper.ToSearchDocument(
                    doc.ModuleId, doc.EntityId, doc.EntityType, doc.Title,
                    doc.Content, doc.Summary, doc.OwnerId,
                    doc.CreatedAt, doc.UpdatedAt, doc.Metadata));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "VideoModuleSearchClient.GetAllSearchableDocumentsAsync failed");
        }
        return results;
    }

    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct)
    {
        var request = new VideoProto.GetSearchableDocumentRequest { EntityId = entityId };
        try
        {
            var response = await _client.Value.GetSearchableDocumentAsync(request, deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            if (!response.Found || response.Document is null)
                return null;
            var d = response.Document;
            return SearchDocumentMapper.ToSearchDocument(
                d.ModuleId, d.EntityId, d.EntityType, d.Title,
                d.Content, d.Summary, d.OwnerId,
                d.CreatedAt, d.UpdatedAt, d.Metadata);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "VideoModuleSearchClient.GetSearchableDocumentAsync({EntityId}) failed", entityId);
            return null;
        }
    }
}
