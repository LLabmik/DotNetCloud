using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Host.Protos;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Notes gRPC client used by the Core Server.
/// </summary>
public sealed class NotesGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "NotesGrpc";
    /// <summary>The gRPC address of the Notes module.</summary>
    public string NotesModuleAddress { get; set; } = "http://localhost:5010";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="INotesApiClient"/>.
/// Calls the Notes module's gRPC service.
/// </summary>
public sealed class NotesGrpcApiClient : INotesApiClient, IDisposable
{
    private readonly NotesGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<NotesGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<NotesGrpcService.NotesGrpcServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="NotesGrpcApiClient"/> class.</summary>
    public NotesGrpcApiClient(
        IOptions<NotesGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotesGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<NotesGrpcService.NotesGrpcServiceClient>(
            () => new NotesGrpcService.NotesGrpcServiceClient(_channel.Value));
    }

    private string GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) ? userId : Guid.Empty.ToString();
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.notes");
        _logger.LogInformation("NotesGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    // ─── Note CRUD ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteDto>> ListNotesAsync(Guid? folderId = null, CancellationToken cancellationToken = default)
    {
        var request = new ListNotesRequest
        {
            UserId = GetUserId(),
            FolderId = folderId?.ToString() ?? string.Empty,
            Skip = 0,
            Take = 50
        };
        try
        {
            var response = await _client.Value.ListNotesAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? [] : response.Notes.Select(ToNoteDto).Where(n => n is not null).Select(n => n!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.ListNotesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<NoteDto?> GetNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var request = new GetNoteRequest { NoteId = noteId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetNoteAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToNoteDto(response.Note) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.GetNoteAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<NoteDto?> CreateNoteAsync(CreateNoteDto dto, CancellationToken cancellationToken = default)
    {
        var request = new CreateNoteRequest
        {
            UserId = GetUserId(),
            FolderId = dto.FolderId?.ToString() ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Content = dto.Content ?? string.Empty,
            Format = dto.Format.ToString()
        };
        request.Tags.AddRange(dto.Tags ?? []);
        try
        {
            var response = await _client.Value.CreateNoteAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToNoteDto(response.Note) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.CreateNoteAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<NoteDto?> UpdateNoteAsync(Guid noteId, UpdateNoteDto dto, CancellationToken cancellationToken = default)
    {
        var request = new UpdateNoteRequest
        {
            NoteId = noteId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Content = dto.Content ?? string.Empty,
            Format = dto.Format?.ToString() ?? string.Empty,
            IsPinned = dto.IsPinned?.ToString().ToLowerInvariant() ?? string.Empty,
            IsFavorite = dto.IsFavorite?.ToString().ToLowerInvariant() ?? string.Empty,
            ExpectedVersion = dto.ExpectedVersion ?? 0
        };
        if (dto.Tags is not null)
        { request.UpdateTags = true; request.Tags.AddRange(dto.Tags); }
        try
        {
            var response = await _client.Value.UpdateNoteAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToNoteDto(response.Note) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.UpdateNoteAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var request = new DeleteNoteRequest { NoteId = noteId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteNoteAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.DeleteNoteAsync failed");
        }
    }

    // ─── Search ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string? query, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var request = new SearchNotesRequest
        {
            UserId = GetUserId(),
            Query = query ?? string.Empty,
            Skip = skip,
            Take = take
        };
        try
        {
            var response = await _client.Value.SearchNotesAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? [] : response.Notes.Select(ToNoteDto).Where(n => n is not null).Select(n => n!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.SearchNotesAsync failed");
            return [];
        }
    }

    // ─── Folders ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(CancellationToken cancellationToken = default)
    {
        var request = new ListFoldersRequest { UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListFoldersAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? [] : response.Folders.Select(ToFolderDto).Where(f => f is not null).Select(f => f!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.ListFoldersAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto?> CreateFolderAsync(CreateNoteFolderDto dto, CancellationToken cancellationToken = default)
    {
        var request = new CreateFolderRequest
        {
            UserId = GetUserId(),
            ParentId = dto.ParentId?.ToString() ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Color = dto.Color ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateFolderAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToFolderDto(response.Folder) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.CreateFolderAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto?> UpdateFolderAsync(Guid folderId, UpdateNoteFolderDto dto, CancellationToken cancellationToken = default)
    {
        var request = new UpdateFolderRequest
        {
            FolderId = folderId.ToString(),
            UserId = GetUserId(),
            Name = dto.Name ?? string.Empty,
            Color = dto.Color ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateFolderAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToFolderDto(response.Folder) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.UpdateFolderAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var request = new DeleteFolderRequest { FolderId = folderId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteFolderAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.DeleteFolderAsync failed");
        }
    }

    // ─── Markdown ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> RenderMarkdownAsync(string markdown, CancellationToken cancellationToken = default)
    {
        var request = new RenderMarkdownRequest { Markdown = markdown ?? string.Empty };
        try
        {
            var response = await _client.Value.RenderMarkdownAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Html;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.RenderMarkdownAsync failed");
            return markdown ?? string.Empty;
        }
    }

    // ─── Sharing ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteShareDto>> ListSharesAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var request = new ListSharesRequest { NoteId = noteId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListSharesAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? [] : response.Shares.Select(s => new NoteShareDto
            {
                Id = Guid.Parse(s.Id),
                NoteId = Guid.Parse(s.NoteId),
                SharedWithUserId = Guid.Parse(s.SharedWithUserId),
                Permission = Enum.TryParse<NoteSharePermission>(s.Permission, out var perm) ? perm : NoteSharePermission.ReadOnly,
                CreatedAt = DateTime.TryParse(s.CreatedAt, out var dt) ? dt : DateTime.MinValue
            }).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.ListSharesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<NoteShareDto?> ShareNoteAsync(Guid noteId, Guid userId, NoteSharePermission permission = NoteSharePermission.ReadOnly, CancellationToken cancellationToken = default)
    {
        var request = new ShareNoteRequest
        {
            NoteId = noteId.ToString(),
            UserId = GetUserId(),
            TargetUserId = userId.ToString(),
            Permission = permission.ToString()
        };
        try
        {
            var response = await _client.Value.ShareNoteAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            if (!response.Success || response.Share is null)
                return null;
            var s = response.Share;
            return new NoteShareDto
            {
                Id = Guid.Parse(s.Id),
                NoteId = Guid.Parse(s.NoteId),
                SharedWithUserId = Guid.Parse(s.SharedWithUserId),
                Permission = Enum.TryParse<NoteSharePermission>(s.Permission, out var perm) ? perm : NoteSharePermission.ReadOnly,
                CreatedAt = DateTime.TryParse(s.CreatedAt, out var dt) ? dt : DateTime.MinValue
            };
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.ShareNoteAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var request = new RevokeShareRequest { ShareId = shareId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.RevokeShareAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.RevokeShareAsync failed");
        }
    }

    // ─── Version History ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteVersionDto>> GetVersionHistoryAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var request = new GetVersionHistoryRequest { NoteId = noteId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetVersionHistoryAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? [] : response.Versions.Select(v => new NoteVersionDto
            {
                Id = Guid.Parse(v.Id),
                NoteId = Guid.Parse(v.NoteId),
                VersionNumber = v.VersionNumber,
                Title = v.Title,
                Content = v.Content,
                EditedByUserId = Guid.Parse(v.EditedByUserId),
                CreatedAt = DateTime.TryParse(v.CreatedAt, out var dt) ? dt : DateTime.MinValue
            }).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.GetVersionHistoryAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<NoteDto?> RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var request = new RestoreVersionRequest
        {
            NoteId = noteId.ToString(),
            VersionId = versionId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.RestoreVersionAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToNoteDto(response.Note) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "NotesGrpcApiClient.RestoreVersionAsync failed");
            return null;
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private Metadata DeadlineHeaders(CancellationToken ct)
    {
        var headers = new Metadata();
        if (_options.Timeout > TimeSpan.Zero)
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            headers.Add("deadline", deadline.ToString("O"));
        }
        return headers;
    }

    private static NoteDto? ToNoteDto(NoteMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new NoteDto
        {
            Id = Guid.Parse(m.Id),
            OwnerId = Guid.Parse(m.OwnerId),
            FolderId = string.IsNullOrEmpty(m.FolderId) ? null : Guid.Parse(m.FolderId),
            Title = m.Title,
            Content = m.Content,
            Format = Enum.TryParse<NoteContentFormat>(m.Format, out var fmt) ? fmt : NoteContentFormat.Markdown,
            IsPinned = m.IsPinned,
            IsFavorite = m.IsFavorite,
            Version = m.Version,
            ETag = m.Etag,
            ContentLength = m.ContentLength,
            CreatedAt = DateTime.TryParse(m.CreatedAt, out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(m.UpdatedAt, out var ua) ? ua : DateTime.MinValue,
            Tags = m.Tags.ToList(),
            Links = m.Links.Select(l => new NoteLinkDto
            {
                LinkType = Enum.TryParse<NoteLinkType>(l.LinkType, out var lt) ? lt : NoteLinkType.Note,
                TargetId = Guid.Parse(l.TargetId),
                DisplayLabel = l.DisplayLabel
            }).ToList()
        };
    }

    private static NoteFolderDto? ToFolderDto(FolderMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new NoteFolderDto
        {
            Id = Guid.Parse(m.Id),
            OwnerId = Guid.Parse(m.OwnerId),
            ParentId = string.IsNullOrEmpty(m.ParentId) ? null : Guid.Parse(m.ParentId),
            Name = m.Name,
            Color = m.Color,
            SortOrder = m.SortOrder,
            NoteCount = m.NoteCount,
            CreatedAt = DateTime.TryParse(m.CreatedAt, out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(m.UpdatedAt, out var ua) ? ua : DateTime.MinValue
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}
