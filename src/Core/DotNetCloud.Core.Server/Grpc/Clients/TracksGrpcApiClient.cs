using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Host.Protos;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Tracks gRPC client used by the Core Server.
/// </summary>
public sealed class TracksGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "TracksGrpc";
    /// <summary>The gRPC address of the Tracks module.</summary>
    public string TracksModuleAddress { get; set; } = "http://localhost:5011";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="ITracksApiClient"/>.
/// Calls the Tracks module's gRPC service.
/// </summary>
public sealed class TracksGrpcApiClient : ITracksApiClient, IDisposable
{
    private readonly TracksGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TracksGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<TracksGrpcService.TracksGrpcServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="TracksGrpcApiClient"/> class.</summary>
    public TracksGrpcApiClient(
        IOptions<TracksGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TracksGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<TracksGrpcService.TracksGrpcServiceClient>(
            () => new TracksGrpcService.TracksGrpcServiceClient(_channel.Value));
    }

    private string GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) ? userId : Guid.Empty.ToString();
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.tracks");
        _logger.LogInformation("TracksGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    // ─── Products ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> ListProductsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var request = new ListProductsRequest
        {
            OrganizationId = organizationId.ToString(),
            UserId = GetUserId(),
            Skip = 0,
            Take = 100,
            IncludeArchived = false
        };
        try
        {
            var response = await _client.Value.ListProductsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Products.Select(ToProductDto).Where(p => p is not null).Select(p => p!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListProductsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetProductRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetProductAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductDto(response.Product) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetProductAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProductDto?> CreateProductAsync(Guid organizationId, CreateProductDto dto, CancellationToken ct = default)
    {
        var request = new CreateProductRequest
        {
            UserId = GetUserId(),
            OrganizationId = organizationId.ToString(),
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Color = dto.Color ?? string.Empty,
            SubItemsEnabled = dto.SubItemsEnabled
        };
        try
        {
            var response = await _client.Value.CreateProductAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductDto(response.Product) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateProductAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProductDto?> UpdateProductAsync(Guid productId, UpdateProductDto dto, CancellationToken ct = default)
    {
        var request = new UpdateProductRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Color = dto.Color ?? string.Empty,
            SubItemsEnabled = dto.SubItemsEnabled ?? false,
            Etag = dto.ETag ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateProductAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductDto(response.Product) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateProductAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new DeleteProductRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteProductAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteProductAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> ListDeletedProductsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var request = new ListDeletedProductsRequest { OrganizationId = organizationId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListDeletedProductsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Products.Select(ToProductDto).Where(p => p is not null).Select(p => p!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListDeletedProductsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ProductDto?> RestoreProductAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new RestoreProductRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.RestoreProductAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductDto(response.Product) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RestoreProductAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task PermanentDeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new PermanentDeleteProductRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.PermanentDeleteProductAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.PermanentDeleteProductAsync failed");
        }
    }

    // ─── Product Members ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductMemberDto>> ListProductMembersAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListProductMembersRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListProductMembersAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Members.Select(ToProductMemberDto).Where(m => m is not null).Select(m => m!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListProductMembersAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task AddProductMemberAsync(Guid productId, AddProductMemberDto dto, CancellationToken ct = default)
    {
        var request = new AddProductMemberRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            MemberUserId = dto.UserId.ToString(),
            Role = dto.Role.ToString()
        };
        try
        {
            await _client.Value.AddProductMemberAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddProductMemberAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task RemoveProductMemberAsync(Guid productId, Guid userId, CancellationToken ct = default)
    {
        var request = new RemoveProductMemberRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            MemberUserId = userId.ToString()
        };
        try
        {
            await _client.Value.RemoveProductMemberAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RemoveProductMemberAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task UpdateProductMemberRoleAsync(Guid productId, Guid userId, ProductMemberRole role, CancellationToken ct = default)
    {
        var request = new UpdateProductMemberRoleRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            MemberUserId = userId.ToString(),
            Role = role.ToString()
        };
        try
        {
            await _client.Value.UpdateProductMemberRoleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateProductMemberRoleAsync failed");
        }
    }

    // ─── Labels ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<LabelDto>> ListLabelsAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListLabelsRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListLabelsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Labels.Select(ToLabelDto).Where(l => l is not null).Select(l => l!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListLabelsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<LabelDto?> CreateLabelAsync(Guid productId, CreateLabelDto dto, CancellationToken ct = default)
    {
        var request = new CreateLabelRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Color = dto.Color ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateLabelAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToLabelDto(response.Label) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateLabelAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<LabelDto?> UpdateLabelAsync(Guid productId, Guid labelId, UpdateLabelDto dto, CancellationToken ct = default)
    {
        var request = new UpdateLabelRequest
        {
            ProductId = productId.ToString(),
            LabelId = labelId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Color = dto.Color ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateLabelAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToLabelDto(response.Label) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateLabelAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteLabelAsync(Guid productId, Guid labelId, CancellationToken ct = default)
    {
        var request = new DeleteLabelRequest
        {
            ProductId = productId.ToString(),
            LabelId = labelId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteLabelAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteLabelAsync failed");
        }
    }

    // ─── Swimlanes ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SwimlaneDto>> ListProductSwimlanesAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListProductSwimlanesRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListProductSwimlanesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Swimlanes.Select(ToSwimlaneDto).Where(s => s is not null).Select(s => s!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListProductSwimlanesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<SwimlaneDto?> CreateProductSwimlaneAsync(Guid productId, CreateSwimlaneDto dto, CancellationToken ct = default)
    {
        var request = new CreateSwimlaneRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Color = dto.Color ?? string.Empty,
            IsDone = dto.IsDone,
            CardLimit = dto.CardLimit ?? 0
        };
        try
        {
            var response = await _client.Value.CreateSwimlaneAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSwimlaneDto(response.Swimlane) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateProductSwimlaneAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SwimlaneDto>> ListWorkItemSwimlanesAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new ListWorkItemSwimlanesRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListWorkItemSwimlanesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Swimlanes.Select(ToSwimlaneDto).Where(s => s is not null).Select(s => s!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListWorkItemSwimlanesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<SwimlaneDto?> CreateWorkItemSwimlaneAsync(Guid workItemId, CreateSwimlaneDto dto, CancellationToken ct = default)
    {
        var request = new CreateWorkItemSwimlaneRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Color = dto.Color ?? string.Empty,
            IsDone = dto.IsDone,
            CardLimit = dto.CardLimit ?? 0
        };
        try
        {
            var response = await _client.Value.CreateWorkItemSwimlaneAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSwimlaneDto(response.Swimlane) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateWorkItemSwimlaneAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SwimlaneDto?> UpdateSwimlaneAsync(Guid swimlaneId, UpdateSwimlaneDto dto, CancellationToken ct = default)
    {
        var request = new UpdateSwimlaneRequest
        {
            SwimlaneId = swimlaneId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Color = dto.Color ?? string.Empty,
            IsDone = dto.IsDone ?? false,
            CardLimit = dto.CardLimit ?? 0
        };
        try
        {
            var response = await _client.Value.UpdateSwimlaneAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSwimlaneDto(response.Swimlane) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateSwimlaneAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteSwimlaneAsync(Guid swimlaneId, CancellationToken ct = default)
    {
        var request = new DeleteSwimlaneRequest { SwimlaneId = swimlaneId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteSwimlaneAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteSwimlaneAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task ReorderSwimlanesAsync(IReadOnlyList<Guid> swimlaneIds, CancellationToken ct = default)
    {
        var request = new ReorderSwimlanesRequest
        {
            UserId = GetUserId()
        };
        request.OrderedIds.AddRange(swimlaneIds.Select(id => id.ToString()));
        try
        {
            await _client.Value.ReorderSwimlanesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ReorderSwimlanesAsync failed");
        }
    }

    // ─── Swimlane Transition Rules ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SwimlaneTransitionRuleDto>> GetSwimlaneTransitionMatrixAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetSwimlaneTransitionMatrixRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetSwimlaneTransitionMatrixAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Rules.Select(ToTransitionRuleDto).Where(r => r is not null).Select(r => r!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetSwimlaneTransitionMatrixAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SwimlaneTransitionRuleDto>> SetSwimlaneTransitionMatrixAsync(Guid productId, List<SetTransitionRuleDto> rules, CancellationToken ct = default)
    {
        var request = new SetSwimlaneTransitionMatrixRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId()
        };
        request.Rules.AddRange(rules.Select(r => new SetTransitionRuleMessage
        {
            FromSwimlaneId = r.FromSwimlaneId.ToString(),
            ToSwimlaneId = r.ToSwimlaneId.ToString(),
            IsAllowed = r.IsAllowed
        }));
        try
        {
            var response = await _client.Value.SetSwimlaneTransitionMatrixAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Rules.Select(ToTransitionRuleDto).Where(r => r is not null).Select(r => r!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.SetSwimlaneTransitionMatrixAsync failed");
            return [];
        }
    }

    // ─── Work Items ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemDto>> ListWorkItemsAsync(Guid swimlaneId, CancellationToken ct = default)
    {
        var request = new ListWorkItemsRequest { SwimlaneId = swimlaneId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListWorkItemsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.WorkItems.Select(ToWorkItemDto).Where(w => w is not null).Select(w => w!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListWorkItemsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> GetWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new GetWorkItemRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWorkItemDto(response.WorkItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetWorkItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> GetWorkItemByNumberAsync(Guid productId, int itemNumber, CancellationToken ct = default)
    {
        var request = new GetWorkItemByNumberRequest
        {
            ProductId = productId.ToString(),
            ItemNumber = itemNumber,
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.GetWorkItemByNumberAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWorkItemDto(response.WorkItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetWorkItemByNumberAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> CreateEpicAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new CreateEpicRequest
        {
            SwimlaneId = swimlaneId.ToString(),
            UserId = GetUserId()
        };
        PopulateCreateWorkItemRequest(dto, request);
        return await CreateWorkItemInternalAsync(
            () => _client.Value.CreateEpicAsync(request, DeadlineHeaders(ct)).ResponseAsync, ct, "CreateEpicAsync");
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> CreateFeatureAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new CreateFeatureRequest
        {
            SwimlaneId = swimlaneId.ToString(),
            UserId = GetUserId()
        };
        PopulateCreateWorkItemRequest(dto, request);
        return await CreateWorkItemInternalAsync(
            () => _client.Value.CreateFeatureAsync(request, DeadlineHeaders(ct)).ResponseAsync, ct, "CreateFeatureAsync");
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> CreateItemAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new CreateItemRequest
        {
            SwimlaneId = swimlaneId.ToString(),
            UserId = GetUserId()
        };
        PopulateCreateWorkItemRequest(dto, request);
        return await CreateWorkItemInternalAsync(
            () => _client.Value.CreateItemAsync(request, DeadlineHeaders(ct)).ResponseAsync, ct, "CreateItemAsync");
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> CreateSubItemAsync(Guid parentItemId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new CreateSubItemRequest
        {
            ParentItemId = parentItemId.ToString(),
            UserId = GetUserId()
        };
        PopulateCreateWorkItemRequest(dto, request);
        return await CreateWorkItemInternalAsync(
            () => _client.Value.CreateSubItemAsync(request, DeadlineHeaders(ct)).ResponseAsync, ct, "CreateSubItemAsync");
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> UpdateWorkItemAsync(Guid workItemId, UpdateWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new UpdateWorkItemRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Priority = dto.Priority?.ToString() ?? string.Empty,
            StartDate = FormatDateTime(dto.StartDate),
            DueDate = FormatDateTime(dto.DueDate),
            StoryPoints = dto.StoryPoints ?? 0,
            IsArchived = dto.IsArchived ?? false,
            MilestoneId = dto.MilestoneId?.ToString() ?? string.Empty,
            Etag = dto.ETag ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWorkItemDto(response.WorkItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateWorkItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new DeleteWorkItemRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteWorkItemAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> MoveWorkItemAsync(Guid workItemId, MoveWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new MoveWorkItemRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            TargetSwimlaneId = dto.TargetSwimlaneId.ToString(),
            Position = dto.Position ?? 0,
            EnforceWipLimit = dto.EnforceWipLimit ?? false
        };
        try
        {
            var response = await _client.Value.MoveWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWorkItemDto(response.WorkItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.MoveWorkItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemDto>> GetChildWorkItemsAsync(Guid parentWorkItemId, CancellationToken ct = default)
    {
        var request = new GetChildWorkItemsRequest { ParentWorkItemId = parentWorkItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetChildWorkItemsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.WorkItems.Select(ToWorkItemDto).Where(w => w is not null).Select(w => w!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetChildWorkItemsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemDto>> ListDeletedWorkItemsAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListDeletedWorkItemsRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListDeletedWorkItemsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.WorkItems.Select(ToWorkItemDto).Where(w => w is not null).Select(w => w!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListDeletedWorkItemsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemDto?> RestoreWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new RestoreWorkItemRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.RestoreWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWorkItemDto(response.WorkItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RestoreWorkItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task PermanentDeleteWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new PermanentDeleteWorkItemRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.PermanentDeleteWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.PermanentDeleteWorkItemAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<int> EmptyWorkItemTrashAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new EmptyWorkItemTrashRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.EmptyWorkItemTrashAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? response.DeletedCount : 0;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.EmptyWorkItemTrashAsync failed");
            return 0;
        }
    }

    // ─── Export ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]> ExportWorkItemsCsvAsync(Guid productId, Guid? swimlaneId = null, Guid? labelId = null, Priority? priority = null, CancellationToken ct = default)
    {
        var request = new ExportWorkItemsCsvRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            SwimlaneId = swimlaneId?.ToString() ?? string.Empty,
            LabelId = labelId?.ToString() ?? string.Empty,
            Priority = priority?.ToString() ?? string.Empty
        };
        try
        {
            var response = await _client.Value.ExportWorkItemsCsvAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? response.CsvData.ToByteArray() : [];
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ExportWorkItemsCsvAsync failed");
            return [];
        }
    }

    // ─── Watchers ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetWatchersAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new GetWatchersRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetWatchersAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.WatcherUserIds.Select(Guid.Parse).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetWatchersAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<int> WatchWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new WatchWorkItemRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.WatchWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? response.Count : 0;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.WatchWorkItemAsync failed");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<int> UnwatchWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new UnwatchWorkItemRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.UnwatchWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? response.Count : 0;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UnwatchWorkItemAsync failed");
            return 0;
        }
    }

    // ─── Assignments ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task AssignUserAsync(Guid workItemId, Guid userId, CancellationToken ct = default)
    {
        var request = new AssignUserRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            AssigneeUserId = userId.ToString()
        };
        try
        {
            await _client.Value.AssignUserAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AssignUserAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task UnassignUserAsync(Guid workItemId, Guid userId, CancellationToken ct = default)
    {
        var request = new UnassignUserRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            AssigneeUserId = userId.ToString()
        };
        try
        {
            await _client.Value.UnassignUserAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UnassignUserAsync failed");
        }
    }

    // ─── Work Item Labels ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task AddLabelToWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default)
    {
        var request = new AddLabelToWorkItemRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            LabelId = labelId.ToString()
        };
        try
        {
            await _client.Value.AddLabelToWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddLabelToWorkItemAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task RemoveLabelFromWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default)
    {
        var request = new RemoveLabelFromWorkItemRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            LabelId = labelId.ToString()
        };
        try
        {
            await _client.Value.RemoveLabelFromWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RemoveLabelFromWorkItemAsync failed");
        }
    }

    // ─── Comments ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemCommentDto>> ListCommentsAsync(Guid workItemId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var request = new ListCommentsRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            Skip = skip,
            Take = take
        };
        try
        {
            var response = await _client.Value.ListCommentsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Comments.Select(ToCommentDto).Where(c => c is not null).Select(c => c!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListCommentsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemCommentDto?> CreateCommentAsync(Guid workItemId, string content, CancellationToken ct = default)
    {
        var request = new CreateCommentRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            Content = content ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateCommentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToCommentDto(response.Comment) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateCommentAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemCommentDto?> UpdateCommentAsync(Guid workItemId, Guid commentId, string content, CancellationToken ct = default)
    {
        var request = new UpdateCommentRequest
        {
            WorkItemId = workItemId.ToString(),
            CommentId = commentId.ToString(),
            UserId = GetUserId(),
            Content = content ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateCommentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToCommentDto(response.Comment) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateCommentAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCommentAsync(Guid workItemId, Guid commentId, CancellationToken ct = default)
    {
        var request = new DeleteCommentRequest
        {
            WorkItemId = workItemId.ToString(),
            CommentId = commentId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteCommentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteCommentAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemCommentDto>> ListDeletedCommentsAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new ListDeletedCommentsRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListDeletedCommentsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Comments.Select(ToCommentDto).Where(c => c is not null).Select(c => c!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListDeletedCommentsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task RestoreCommentAsync(Guid workItemId, Guid commentId, CancellationToken ct = default)
    {
        var request = new RestoreCommentRequest
        {
            WorkItemId = workItemId.ToString(),
            CommentId = commentId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.RestoreCommentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RestoreCommentAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task PermanentDeleteCommentAsync(Guid workItemId, Guid commentId, CancellationToken ct = default)
    {
        var request = new PermanentDeleteCommentRequest
        {
            WorkItemId = workItemId.ToString(),
            CommentId = commentId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.PermanentDeleteCommentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.PermanentDeleteCommentAsync failed");
        }
    }

    // ─── Checklists ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChecklistDto>> ListChecklistsAsync(Guid itemId, CancellationToken ct = default)
    {
        var request = new ListChecklistsRequest { ItemId = itemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListChecklistsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Checklists.Select(ToChecklistDto).Where(c => c is not null).Select(c => c!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListChecklistsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ChecklistDto?> CreateChecklistAsync(Guid itemId, string title, CancellationToken ct = default)
    {
        var request = new CreateChecklistRequest
        {
            ItemId = itemId.ToString(),
            UserId = GetUserId(),
            Title = title ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateChecklistAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToChecklistDto(response.Checklist) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateChecklistAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteChecklistAsync(Guid itemId, Guid checklistId, CancellationToken ct = default)
    {
        var request = new DeleteChecklistRequest
        {
            ItemId = itemId.ToString(),
            ChecklistId = checklistId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteChecklistAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteChecklistAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<ChecklistItemDto?> AddChecklistItemAsync(Guid itemId, Guid checklistId, string title, CancellationToken ct = default)
    {
        var request = new AddChecklistItemRequest
        {
            ItemId = itemId.ToString(),
            ChecklistId = checklistId.ToString(),
            UserId = GetUserId(),
            Title = title ?? string.Empty
        };
        try
        {
            var response = await _client.Value.AddChecklistItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToChecklistItemDto(response.ChecklistItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddChecklistItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ChecklistItemDto?> ToggleChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct = default)
    {
        var request = new ToggleChecklistItemRequest
        {
            ItemId = itemId.ToString(),
            ChecklistId = checklistId.ToString(),
            ChecklistItemId = checklistItemId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.ToggleChecklistItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToChecklistItemDto(response.ChecklistItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ToggleChecklistItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct = default)
    {
        var request = new DeleteChecklistItemRequest
        {
            ItemId = itemId.ToString(),
            ChecklistId = checklistId.ToString(),
            ChecklistItemId = checklistItemId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteChecklistItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteChecklistItemAsync failed");
        }
    }

    // ─── Attachments ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemAttachmentDto>> ListAttachmentsAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new ListAttachmentsRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListAttachmentsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Attachments.Select(ToAttachmentDto).Where(a => a is not null).Select(a => a!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListAttachmentsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemAttachmentDto?> AddAttachmentAsync(Guid workItemId, string fileName, string? url, Guid? fileNodeId, CancellationToken ct = default)
    {
        var request = new AddAttachmentRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            FileName = fileName ?? string.Empty,
            Url = url ?? string.Empty,
            FileNodeId = fileNodeId?.ToString() ?? string.Empty
        };
        try
        {
            var response = await _client.Value.AddAttachmentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToAttachmentDto(response.Attachment) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddAttachmentAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAttachmentAsync(Guid workItemId, Guid attachmentId, CancellationToken ct = default)
    {
        var request = new RemoveAttachmentRequest
        {
            WorkItemId = workItemId.ToString(),
            AttachmentId = attachmentId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.RemoveAttachmentAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RemoveAttachmentAsync failed");
        }
    }

    // ─── Dependencies ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemDependencyDto>> ListDependenciesAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new ListDependenciesRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListDependenciesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Dependencies.Select(ToDependencyDto).Where(d => d is not null).Select(d => d!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListDependenciesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<WorkItemDependencyDto?> AddDependencyAsync(Guid workItemId, AddWorkItemDependencyDto dto, CancellationToken ct = default)
    {
        var request = new AddDependencyRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            DependsOnWorkItemId = dto.DependsOnWorkItemId.ToString(),
            Type = dto.Type.ToString()
        };
        try
        {
            var response = await _client.Value.AddDependencyAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToDependencyDto(response.Dependency) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddDependencyAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task RemoveDependencyAsync(Guid workItemId, Guid dependencyId, CancellationToken ct = default)
    {
        var request = new RemoveDependencyRequest
        {
            WorkItemId = workItemId.ToString(),
            DependencyId = dependencyId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.RemoveDependencyAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RemoveDependencyAsync failed");
        }
    }

    // ─── Sprints ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDto>> ListSprintsAsync(Guid epicId, CancellationToken ct = default)
    {
        var request = new ListSprintsRequest { EpicId = epicId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListSprintsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Sprints.Select(ToSprintDto).Where(s => s is not null).Select(s => s!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListSprintsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<SprintDto?> GetSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new GetSprintRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintDto(response.Sprint) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetSprintAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SprintDto?> CreateSprintAsync(Guid epicId, CreateSprintDto dto, CancellationToken ct = default)
    {
        var request = new CreateSprintRequest
        {
            EpicId = epicId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Goal = dto.Goal ?? string.Empty,
            StartDate = FormatDateTime(dto.StartDate),
            EndDate = FormatDateTime(dto.EndDate),
            TargetStoryPoints = dto.TargetStoryPoints ?? 0,
            DurationWeeks = dto.DurationWeeks ?? 0
        };
        try
        {
            var response = await _client.Value.CreateSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintDto(response.Sprint) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateSprintAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SprintDto?> UpdateSprintAsync(Guid sprintId, UpdateSprintDto dto, CancellationToken ct = default)
    {
        var request = new UpdateSprintRequest
        {
            SprintId = sprintId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Goal = dto.Goal ?? string.Empty,
            StartDate = FormatDateTime(dto.StartDate),
            EndDate = FormatDateTime(dto.EndDate),
            TargetStoryPoints = dto.TargetStoryPoints ?? 0
        };
        try
        {
            var response = await _client.Value.UpdateSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintDto(response.Sprint) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateSprintAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new DeleteSprintRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteSprintAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<SprintDto?> StartSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new StartSprintRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.StartSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintDto(response.Sprint) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.StartSprintAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SprintDto?> CompleteSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new CompleteSprintRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.CompleteSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintDto(response.Sprint) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CompleteSprintAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task AddItemToSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct = default)
    {
        var request = new AddItemToSprintRequest
        {
            SprintId = sprintId.ToString(),
            ItemId = itemId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.AddItemToSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddItemToSprintAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task RemoveItemFromSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct = default)
    {
        var request = new RemoveItemFromSprintRequest
        {
            SprintId = sprintId.ToString(),
            ItemId = itemId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.RemoveItemFromSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RemoveItemFromSprintAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemDto>> GetBacklogItemsAsync(Guid epicId, CancellationToken ct = default)
    {
        var request = new GetBacklogItemsRequest { EpicId = epicId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetBacklogItemsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.WorkItems.Select(ToWorkItemDto).Where(w => w is not null).Select(w => w!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetBacklogItemsAsync failed");
            return [];
        }
    }

    // ─── Sprint Planning ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDto>> CreateSprintPlanAsync(Guid epicId, CreateSprintPlanDto dto, CancellationToken ct = default)
    {
        var request = new CreateSprintPlanRequest
        {
            EpicId = epicId.ToString(),
            UserId = GetUserId(),
            NumberOfSprints = dto.NumberOfSprints,
            SprintDurationWeeks = dto.SprintDurationWeeks,
            StartDate = FormatDateTime(dto.StartDate)
        };
        try
        {
            var response = await _client.Value.CreateSprintPlanAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Sprints.Select(ToSprintDto).Where(s => s is not null).Select(s => s!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateSprintPlanAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDto>> GetSprintPlanAsync(Guid epicId, CancellationToken ct = default)
    {
        var request = new GetSprintPlanRequest { EpicId = epicId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetSprintPlanAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Sprints.Select(ToSprintDto).Where(s => s is not null).Select(s => s!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetSprintPlanAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDto>> AdjustSprintDatesAsync(Guid sprintId, AdjustSprintDto dto, CancellationToken ct = default)
    {
        var request = new AdjustSprintRequest
        {
            SprintId = sprintId.ToString(),
            UserId = GetUserId(),
            DurationWeeks = dto.DurationWeeks ?? 0,
            StartDate = FormatDateTime(dto.StartDate),
            EndDate = FormatDateTime(dto.EndDate)
        };
        try
        {
            var response = await _client.Value.AdjustSprintAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Sprints.Select(ToSprintDto).Where(s => s is not null).Select(s => s!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AdjustSprintDatesAsync failed");
            return [];
        }
    }

    // ─── Time Entries ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimeEntryDto>> ListTimeEntriesAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new ListTimeEntriesRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListTimeEntriesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.TimeEntries.Select(ToTimeEntryDto).Where(t => t is not null).Select(t => t!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListTimeEntriesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<TimeEntryDto?> CreateTimeEntryAsync(Guid workItemId, CreateTimeEntryDto dto, CancellationToken ct = default)
    {
        var request = new CreateTimeEntryRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            DurationMinutes = dto.DurationMinutes,
            Description = dto.Description ?? string.Empty,
            StartTime = FormatDateTime(dto.StartTime)
        };
        try
        {
            var response = await _client.Value.CreateTimeEntryAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToTimeEntryDto(response.TimeEntry) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateTimeEntryAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteTimeEntryAsync(Guid workItemId, Guid entryId, CancellationToken ct = default)
    {
        var request = new DeleteTimeEntryRequest
        {
            WorkItemId = workItemId.ToString(),
            EntryId = entryId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteTimeEntryAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteTimeEntryAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<TimeEntryDto?> StartTimerAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new StartTimerRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.StartTimerAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToTimeEntryDto(response.TimeEntry) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.StartTimerAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TimeEntryDto?> StopTimerAsync(Guid workItemId, CancellationToken ct = default)
    {
        var request = new StopTimerRequest { WorkItemId = workItemId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.StopTimerAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToTimeEntryDto(response.TimeEntry) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.StopTimerAsync failed");
            return null;
        }
    }

    // ─── Activity ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityDto>> GetProductActivityAsync(Guid productId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var request = new GetProductActivityRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Skip = skip,
            Take = take
        };
        try
        {
            var response = await _client.Value.GetProductActivityAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Activities.Select(ToActivityDto).Where(a => a is not null).Select(a => a!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetProductActivityAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityDto>> GetWorkItemActivityAsync(Guid workItemId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var request = new GetWorkItemActivityRequest
        {
            WorkItemId = workItemId.ToString(),
            UserId = GetUserId(),
            Skip = skip,
            Take = take
        };
        try
        {
            var response = await _client.Value.GetWorkItemActivityAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Activities.Select(ToActivityDto).Where(a => a is not null).Select(a => a!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetWorkItemActivityAsync failed");
            return [];
        }
    }

    // ─── Analytics ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ProductAnalyticsDto?> GetProductAnalyticsAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetProductAnalyticsRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetProductAnalyticsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductAnalyticsDto(response.Analytics) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetProductAnalyticsAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintVelocityDto>> GetVelocityDataAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetVelocityDataRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetVelocityDataAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Velocities.Select(ToSprintVelocityDto).Where(v => v is not null).Select(v => v!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetVelocityDataAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<SprintReportDto?> GetSprintReportAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new GetSprintReportRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetSprintReportAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintReportDto(response.Report) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetSprintReportAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SprintBurndownDto?> GetBurndownDataAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new GetBurndownDataRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetBurndownDataAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToBurndownDto(response.Burndown) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetBurndownDataAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProductDashboardDto?> GetProductDashboardAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetProductDashboardRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetProductDashboardAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductDashboardDto(response.Dashboard) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetProductDashboardAsync failed");
            return null;
        }
    }

    // ─── Bulk Actions ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<int> BulkWorkItemActionAsync(Guid productId, BulkWorkItemActionDto dto, CancellationToken ct = default)
    {
        var request = new BulkWorkItemActionRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Action = dto.Action ?? string.Empty,
            TargetSwimlaneId = dto.TargetSwimlaneId?.ToString() ?? string.Empty,
            LabelId = dto.LabelId?.ToString() ?? string.Empty,
            AssigneeUserId = dto.AssigneeUserId?.ToString() ?? string.Empty,
            Priority = dto.Priority?.ToString() ?? string.Empty,
            SprintId = dto.SprintId?.ToString() ?? string.Empty
        };
        request.WorkItemIds.AddRange(dto.WorkItemIds.Select(id => id.ToString()));
        try
        {
            var response = await _client.Value.BulkWorkItemActionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? response.AffectedCount : 0;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.BulkWorkItemActionAsync failed");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemDto>> ListProductWorkItemsAsync(Guid productId, Guid? swimlaneId = null, Guid? labelId = null, Priority? priority = null, CancellationToken ct = default)
    {
        var request = new ListProductWorkItemsRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            SwimlaneId = swimlaneId?.ToString() ?? string.Empty,
            LabelId = labelId?.ToString() ?? string.Empty,
            Priority = priority?.ToString() ?? string.Empty
        };
        try
        {
            var response = await _client.Value.ListProductWorkItemsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.WorkItems.Select(ToWorkItemDto).Where(w => w is not null).Select(w => w!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListProductWorkItemsAsync failed");
            return [];
        }
    }

    // ─── Teams ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<TracksTeamDto>> ListTeamsAsync(CancellationToken ct = default)
    {
        var request = new ListTeamsRequest { UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListTeamsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Teams.Select(ToTracksTeamDto).Where(t => t is not null).Select(t => t!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListTeamsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<TracksTeamDto?> GetTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var request = new GetTeamRequest { TeamId = teamId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetTeamAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToTracksTeamDto(response.Team) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetTeamAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TracksTeamDto?> CreateTeamAsync(CreateTracksTeamDto dto, CancellationToken ct = default)
    {
        var request = new CreateTeamRequest
        {
            UserId = GetUserId(),
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateTeamAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToTracksTeamDto(response.Team) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateTeamAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TracksTeamDto?> UpdateTeamAsync(Guid teamId, UpdateTracksTeamDto dto, CancellationToken ct = default)
    {
        var request = new UpdateTeamRequest
        {
            TeamId = teamId.ToString(),
            UserId = GetUserId(),
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty
        };
        try
        {
            var response = await _client.Value.UpdateTeamAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToTracksTeamDto(response.Team) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateTeamAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var request = new DeleteTeamRequest { TeamId = teamId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteTeamAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteTeamAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TracksTeamMemberDto>> ListTeamMembersAsync(Guid teamId, CancellationToken ct = default)
    {
        var request = new ListTeamMembersRequest { TeamId = teamId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListTeamMembersAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Members.Select(ToTracksTeamMemberDto).Where(m => m is not null).Select(m => m!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListTeamMembersAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task AddTeamMemberAsync(Guid teamId, AddTracksTeamMemberDto dto, CancellationToken ct = default)
    {
        var request = new AddTeamMemberRequest
        {
            TeamId = teamId.ToString(),
            UserId = GetUserId(),
            MemberUserId = dto.UserId.ToString(),
            Role = dto.Role.ToString()
        };
        try
        {
            await _client.Value.AddTeamMemberAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AddTeamMemberAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default)
    {
        var request = new RemoveTeamMemberRequest
        {
            TeamId = teamId.ToString(),
            UserId = GetUserId(),
            MemberUserId = userId.ToString()
        };
        try
        {
            await _client.Value.RemoveTeamMemberAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RemoveTeamMemberAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task UpdateTeamMemberRoleAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default)
    {
        var request = new UpdateTeamMemberRoleRequest
        {
            TeamId = teamId.ToString(),
            UserId = GetUserId(),
            MemberUserId = userId.ToString(),
            Role = role.ToString()
        };
        try
        {
            await _client.Value.UpdateTeamMemberRoleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateTeamMemberRoleAsync failed");
        }
    }

    // ─── Review Sessions ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ReviewSessionDto?> StartReviewSessionAsync(Guid epicId, CancellationToken ct = default)
    {
        var request = new StartReviewSessionRequest { EpicId = epicId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.StartReviewSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToReviewSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.StartReviewSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ReviewSessionDto?> GetReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new GetReviewSessionRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetReviewSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToReviewSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetReviewSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ReviewSessionDto?> JoinReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new JoinReviewSessionRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.JoinReviewSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToReviewSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.JoinReviewSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task LeaveReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new LeaveReviewSessionRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.LeaveReviewSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.LeaveReviewSessionAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<ReviewSessionDto?> SetReviewCurrentItemAsync(Guid sessionId, Guid itemId, CancellationToken ct = default)
    {
        var request = new SetReviewCurrentItemRequest
        {
            SessionId = sessionId.ToString(),
            UserId = GetUserId(),
            ItemId = itemId.ToString()
        };
        try
        {
            var response = await _client.Value.SetReviewCurrentItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToReviewSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.SetReviewCurrentItemAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task EndReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new EndReviewSessionRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.EndReviewSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.EndReviewSessionAsync failed");
        }
    }

    // ─── Planning Poker ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PokerSessionDto?> StartPokerSessionAsync(Guid epicId, CreatePokerSessionDto dto, CancellationToken ct = default)
    {
        var request = new StartPokerSessionRequest
        {
            EpicId = epicId.ToString(),
            UserId = GetUserId(),
            ItemId = dto.ItemId.ToString(),
            Scale = dto.Scale.ToString(),
            CustomScaleValues = dto.CustomScaleValues ?? string.Empty
        };
        try
        {
            var response = await _client.Value.StartPokerSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToPokerSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.StartPokerSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PokerSessionDto?> GetPokerSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new GetPokerSessionRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetPokerSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToPokerSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetPokerSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PokerSessionDto?> GetActivePokerSessionByReviewSessionAsync(Guid reviewSessionId, CancellationToken ct = default)
    {
        var request = new GetActivePokerSessionByReviewSessionRequest
        {
            ReviewSessionId = reviewSessionId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.GetActivePokerSessionByReviewSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToPokerSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetActivePokerSessionByReviewSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PokerSessionDto?> SubmitPokerVoteAsync(Guid sessionId, SubmitPokerVoteDto dto, CancellationToken ct = default)
    {
        var request = new SubmitPokerVoteRequest
        {
            SessionId = sessionId.ToString(),
            UserId = GetUserId(),
            Estimate = dto.Estimate ?? string.Empty
        };
        try
        {
            var response = await _client.Value.SubmitPokerVoteAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToPokerSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.SubmitPokerVoteAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PokerSessionDto?> RevealPokerSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new RevealPokerSessionRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.RevealPokerSessionAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToPokerSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.RevealPokerSessionAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PokerSessionDto?> AcceptPokerEstimateAsync(Guid sessionId, string estimate, CancellationToken ct = default)
    {
        var request = new AcceptPokerEstimateRequest
        {
            SessionId = sessionId.ToString(),
            UserId = GetUserId(),
            AcceptedEstimate = estimate ?? string.Empty
        };
        try
        {
            var response = await _client.Value.AcceptPokerEstimateAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToPokerSessionDto(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.AcceptPokerEstimateAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PokerVoteStatusDto>> GetPokerVoteStatusAsync(Guid sessionId, CancellationToken ct = default)
    {
        var request = new GetPokerVoteStatusRequest { SessionId = sessionId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetPokerVoteStatusAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Statuses.Select(s => new PokerVoteStatusDto
            {
                HasVoted = s.HasVoted,
                Estimate = s.Estimate
            }).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetPokerVoteStatusAsync failed");
            return [];
        }
    }

    // ─── User Search ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string searchTerm, int maxResults = 8, CancellationToken ct = default)
    {
        var request = new SearchUsersRequest
        {
            SearchTerm = searchTerm ?? string.Empty,
            MaxResults = maxResults,
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.SearchUsersAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Results.Select(r => new UserSearchResult(
                Guid.Parse(r.Id),
                r.DisplayName ?? string.Empty,
                r.Email ?? string.Empty
            )).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.SearchUsersAsync failed");
            return [];
        }
    }

    // ─── Custom Views ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomViewDto>> ListCustomViewsAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListCustomViewsRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListCustomViewsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Views.Select(ToCustomViewDto).Where(v => v is not null).Select(v => v!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListCustomViewsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<CustomViewDto?> CreateCustomViewAsync(Guid productId, string name, string filterJson, string sortJson, string? groupBy, string layout, bool isShared, CancellationToken ct = default)
    {
        var request = new CreateCustomViewRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Name = name ?? string.Empty,
            FilterJson = filterJson ?? string.Empty,
            SortJson = sortJson ?? string.Empty,
            GroupBy = groupBy ?? string.Empty,
            Layout = layout ?? string.Empty,
            IsShared = isShared
        };
        try
        {
            var response = await _client.Value.CreateCustomViewAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToCustomViewDto(response.View) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateCustomViewAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<CustomViewDto?> UpdateCustomViewAsync(Guid productId, Guid viewId, string? name, string? filterJson, string? sortJson, string? groupBy, string? layout, bool? isShared, CancellationToken ct = default)
    {
        var request = new UpdateCustomViewRequest
        {
            ProductId = productId.ToString(),
            ViewId = viewId.ToString(),
            UserId = GetUserId(),
            Name = name ?? string.Empty,
            FilterJson = filterJson ?? string.Empty,
            SortJson = sortJson ?? string.Empty,
            GroupBy = groupBy ?? string.Empty,
            Layout = layout ?? string.Empty,
            IsShared = isShared ?? false
        };
        try
        {
            var response = await _client.Value.UpdateCustomViewAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToCustomViewDto(response.View) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateCustomViewAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCustomViewAsync(Guid productId, Guid viewId, CancellationToken ct = default)
    {
        var request = new DeleteCustomViewRequest
        {
            ProductId = productId.ToString(),
            ViewId = viewId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteCustomViewAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteCustomViewAsync failed");
        }
    }

    // ─── Webhooks ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookSubscriptionDto>> ListProductWebhooksAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListProductWebhooksRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListProductWebhooksAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Subscriptions.Select(ToWebhookSubscription).Where(w => w is not null).Select(w => w!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListProductWebhooksAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<WebhookSubscriptionDto?> CreateProductWebhookAsync(Guid productId, string url, List<string> eventTypes, CancellationToken ct = default)
    {
        var request = new CreateProductWebhookRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Url = url ?? string.Empty
        };
        request.EventTypes.AddRange(eventTypes ?? []);
        try
        {
            var response = await _client.Value.CreateProductWebhookAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWebhookSubscription(response.Subscription) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateProductWebhookAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<WebhookSubscriptionDto?> UpdateProductWebhookAsync(Guid productId, Guid subscriptionId, string url, List<string> eventTypes, bool isActive, CancellationToken ct = default)
    {
        var request = new UpdateProductWebhookRequest
        {
            ProductId = productId.ToString(),
            SubscriptionId = subscriptionId.ToString(),
            UserId = GetUserId(),
            Url = url ?? string.Empty,
            IsActive = isActive
        };
        request.EventTypes.AddRange(eventTypes ?? []);
        try
        {
            var response = await _client.Value.UpdateProductWebhookAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToWebhookSubscription(response.Subscription) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateProductWebhookAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteProductWebhookAsync(Guid productId, Guid subscriptionId, CancellationToken ct = default)
    {
        var request = new DeleteProductWebhookRequest
        {
            ProductId = productId.ToString(),
            SubscriptionId = subscriptionId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            await _client.Value.DeleteProductWebhookAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteProductWebhookAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task<WebhookTestResult> TestProductWebhookAsync(Guid productId, Guid subscriptionId, CancellationToken ct = default)
    {
        var request = new TestProductWebhookRequest
        {
            ProductId = productId.ToString(),
            SubscriptionId = subscriptionId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.TestProductWebhookAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return new WebhookTestResult(
                response.Success,
                response.DeliverySuccess ? response.StatusCode : null,
                response.DurationMs,
                string.IsNullOrEmpty(response.Error) ? null : response.Error
            );
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.TestProductWebhookAsync failed");
            return new WebhookTestResult(false, null, 0, ex.Message);
        }
    }

    // ─── Roadmap ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<RoadmapDataDto?> GetRoadmapDataAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetRoadmapDataRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetRoadmapDataAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToRoadmapDataDto(response.Roadmap) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetRoadmapDataAsync failed");
            return null;
        }
    }

    // ─── Automation Rules ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<AutomationRuleDto>> ListAutomationRulesAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListAutomationRulesRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListAutomationRulesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Rules.Select(ToAutomationRuleDto).Where(r => r is not null).Select(r => r!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListAutomationRulesAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<AutomationRuleDto?> CreateAutomationRuleAsync(Guid productId, CreateAutomationRuleDto dto, CancellationToken ct = default)
    {
        var request = new CreateAutomationRuleRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Name = dto.Name ?? string.Empty,
            Trigger = dto.Trigger ?? string.Empty,
            ConditionsJson = dto.ConditionsJson ?? string.Empty,
            ActionsJson = dto.ActionsJson ?? string.Empty,
            IsActive = dto.IsActive
        };
        try
        {
            var response = await _client.Value.CreateAutomationRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToAutomationRuleDto(response.Rule) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateAutomationRuleAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AutomationRuleDto?> UpdateAutomationRuleAsync(Guid ruleId, UpdateAutomationRuleDto dto, CancellationToken ct = default)
    {
        var request = new UpdateAutomationRuleRequest
        {
            RuleId = ruleId.ToString(),
            UserId = GetUserId(),
            Name = dto.Name ?? string.Empty,
            Trigger = dto.Trigger ?? string.Empty,
            ConditionsJson = dto.ConditionsJson ?? string.Empty,
            ActionsJson = dto.ActionsJson ?? string.Empty,
            IsActive = dto.IsActive ?? true
        };
        try
        {
            var response = await _client.Value.UpdateAutomationRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToAutomationRuleDto(response.Rule) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateAutomationRuleAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAutomationRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var request = new DeleteAutomationRuleRequest { RuleId = ruleId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteAutomationRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteAutomationRuleAsync failed");
        }
    }

    // ─── Goals / OKRs ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<GoalDto>> ListGoalsAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new ListGoalsRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListGoalsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Goals.Select(ToGoalDto).Where(g => g is not null).Select(g => g!).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.ListGoalsAsync failed");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<GoalDto?> GetGoalAsync(Guid goalId, CancellationToken ct = default)
    {
        var request = new GetGoalRequest { GoalId = goalId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetGoalAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToGoalDto(response.Goal) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetGoalAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GoalDto?> CreateGoalAsync(Guid productId, CreateGoalDto dto, CancellationToken ct = default)
    {
        var request = new CreateGoalRequest
        {
            ProductId = productId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Type = dto.Type ?? string.Empty,
            ParentGoalId = dto.ParentGoalId?.ToString() ?? string.Empty,
            TargetValue = dto.TargetValue ?? 0,
            ProgressType = dto.ProgressType ?? string.Empty,
            DueDate = FormatDateTime(dto.DueDate)
        };
        try
        {
            var response = await _client.Value.CreateGoalAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToGoalDto(response.Goal) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.CreateGoalAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GoalDto?> UpdateGoalAsync(Guid goalId, UpdateGoalDto dto, CancellationToken ct = default)
    {
        var request = new UpdateGoalRequest
        {
            GoalId = goalId.ToString(),
            UserId = GetUserId(),
            Title = dto.Title ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            TargetValue = dto.TargetValue ?? 0,
            CurrentValue = dto.CurrentValue ?? 0,
            ProgressType = dto.ProgressType ?? string.Empty,
            Status = dto.Status ?? string.Empty,
            DueDate = FormatDateTime(dto.DueDate)
        };
        try
        {
            var response = await _client.Value.UpdateGoalAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToGoalDto(response.Goal) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UpdateGoalAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteGoalAsync(Guid goalId, CancellationToken ct = default)
    {
        var request = new DeleteGoalRequest { GoalId = goalId.ToString(), UserId = GetUserId() };
        try
        {
            await _client.Value.DeleteGoalAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.DeleteGoalAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task LinkGoalWorkItemAsync(Guid goalId, LinkGoalWorkItemDto dto, CancellationToken ct = default)
    {
        var request = new LinkGoalWorkItemRequest
        {
            GoalId = goalId.ToString(),
            UserId = GetUserId(),
            WorkItemId = dto.WorkItemId.ToString()
        };
        try
        {
            await _client.Value.LinkGoalWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.LinkGoalWorkItemAsync failed");
        }
    }

    /// <inheritdoc />
    public async Task UnlinkGoalWorkItemAsync(Guid goalId, Guid workItemId, CancellationToken ct = default)
    {
        var request = new UnlinkGoalWorkItemRequest
        {
            GoalId = goalId.ToString(),
            UserId = GetUserId(),
            WorkItemId = workItemId.ToString()
        };
        try
        {
            await _client.Value.UnlinkGoalWorkItemAsync(request, DeadlineHeaders(ct)).ResponseAsync;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.UnlinkGoalWorkItemAsync failed");
        }
    }

    // ─── Capacity Planning ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ProductCapacityDto?> GetProductCapacityAsync(Guid productId, CancellationToken ct = default)
    {
        var request = new GetProductCapacityRequest { ProductId = productId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetProductCapacityAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToProductCapacityDto(response.Capacity) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetProductCapacityAsync failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SprintCapacityDto?> GetSprintCapacityAsync(Guid sprintId, CancellationToken ct = default)
    {
        var request = new GetSprintCapacityRequest { SprintId = sprintId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetSprintCapacityAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToSprintCapacityDto(response.Capacity) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.GetSprintCapacityAsync failed");
            return null;
        }
    }

    // ─── Proto-to-DTO Mapping Helpers ──────────────────────────────────────

    private static string FormatDateTime(DateTime? dt) =>
        dt?.ToString("O") ?? string.Empty;

    private static DateTime ParseDateTime(string s) =>
        DateTime.TryParse(s, out var dt) ? dt : DateTime.MinValue;

    private static ProductDto? ToProductDto(ProductMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new ProductDto
        {
            Id = Guid.Parse(m.Id),
            OrganizationId = Guid.Parse(m.OrganizationId),
            OwnerId = Guid.Parse(m.OwnerId),
            Name = m.Name,
            Description = m.Description,
            Color = m.Color,
            SubItemsEnabled = m.SubItemsEnabled,
            IsArchived = m.IsArchived,
            SwimlaneCount = m.SwimlaneCount,
            EpicCount = m.EpicCount,
            MemberCount = m.MemberCount,
            LabelCount = m.LabelCount,
            ETag = m.Etag,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt),
            DeletedAt = string.IsNullOrEmpty(m.DeletedAt) ? null : ParseDateTime(m.DeletedAt),
            DeletedByUserId = string.IsNullOrEmpty(m.DeletedByUserId) ? null : Guid.Parse(m.DeletedByUserId),
            DeletedByDisplayName = string.IsNullOrEmpty(m.DeletedByDisplayName) ? null : m.DeletedByDisplayName
        };
    }

    private static ProductMemberDto? ToProductMemberDto(ProductMemberMessage? m)
    {
        if (m is null)
            return null;
        return new ProductMemberDto
        {
            UserId = Guid.Parse(m.UserId),
            DisplayName = m.DisplayName,
            Role = Enum.TryParse<ProductMemberRole>(m.Role, out var role) ? role : ProductMemberRole.Viewer,
            JoinedAt = ParseDateTime(m.JoinedAt)
        };
    }

    private static LabelDto? ToLabelDto(LabelMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new LabelDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            Title = m.Title,
            Color = m.Color,
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static SwimlaneDto? ToSwimlaneDto(SwimlaneMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new SwimlaneDto
        {
            Id = Guid.Parse(m.Id),
            ContainerType = Enum.TryParse<SwimlaneContainerType>(m.ContainerType, out var ct) ? ct : SwimlaneContainerType.Product,
            ContainerId = Guid.Parse(m.ContainerId),
            Title = m.Title,
            Color = m.Color,
            Position = m.Position,
            CardLimit = m.CardLimit == 0 ? null : m.CardLimit,
            IsDone = m.IsDone,
            IsArchived = m.IsArchived,
            CardCount = m.CardCount,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static SwimlaneTransitionRuleDto? ToTransitionRuleDto(SwimlaneTransitionRuleMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new SwimlaneTransitionRuleDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            FromSwimlaneId = Guid.Parse(m.FromSwimlaneId),
            ToSwimlaneId = Guid.Parse(m.ToSwimlaneId),
            IsAllowed = m.IsAllowed,
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static WorkItemDto? ToWorkItemDto(WorkItemMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new WorkItemDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            ParentWorkItemId = string.IsNullOrEmpty(m.ParentWorkItemId) ? null : Guid.Parse(m.ParentWorkItemId),
            Type = Enum.TryParse<WorkItemType>(m.Type, out var type) ? type : WorkItemType.Item,
            SwimlaneId = string.IsNullOrEmpty(m.SwimlaneId) ? null : Guid.Parse(m.SwimlaneId),
            SwimlaneTitle = string.IsNullOrEmpty(m.SwimlaneTitle) ? null : m.SwimlaneTitle,
            ItemNumber = m.ItemNumber,
            Title = m.Title,
            Description = m.Description,
            Position = m.Position,
            Priority = Enum.TryParse<Priority>(m.Priority, out var pri) ? pri : Priority.None,
            StartDate = string.IsNullOrEmpty(m.StartDate) ? null : ParseDateTime(m.StartDate),
            DueDate = string.IsNullOrEmpty(m.DueDate) ? null : ParseDateTime(m.DueDate),
            StoryPoints = m.StoryPoints == 0 ? null : m.StoryPoints,
            IsArchived = m.IsArchived,
            CommentCount = m.CommentCount,
            AttachmentCount = m.AttachmentCount,
            SprintId = string.IsNullOrEmpty(m.SprintId) ? null : Guid.Parse(m.SprintId),
            SprintTitle = string.IsNullOrEmpty(m.SprintTitle) ? null : m.SprintTitle,
            TotalTrackedMinutes = m.TotalTrackedMinutes == 0 ? null : m.TotalTrackedMinutes,
            MilestoneId = string.IsNullOrEmpty(m.MilestoneId) ? null : Guid.Parse(m.MilestoneId),
            MilestoneTitle = string.IsNullOrEmpty(m.MilestoneTitle) ? null : m.MilestoneTitle,
            ETag = m.Etag,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt),
            DeletedAt = string.IsNullOrEmpty(m.DeletedAt) ? null : ParseDateTime(m.DeletedAt),
            DeletedByUserId = string.IsNullOrEmpty(m.DeletedByUserId) ? null : Guid.Parse(m.DeletedByUserId),
            DeletedByDisplayName = string.IsNullOrEmpty(m.DeletedByDisplayName) ? null : m.DeletedByDisplayName,
            Assignments = m.Assignments.Select(a => new WorkItemAssignmentDto
            {
                UserId = Guid.Parse(a.UserId),
                DisplayName = a.DisplayName,
                AssignedAt = ParseDateTime(a.AssignedAt)
            }).ToList(),
            Labels = m.Labels.Select(l => new LabelDto
            {
                Id = Guid.Parse(l.Id),
                ProductId = Guid.Parse(l.ProductId),
                Title = l.Title,
                Color = l.Color,
                CreatedAt = ParseDateTime(l.CreatedAt)
            }).ToList(),
            ChildWorkItems = m.ChildWorkItems?.Select(c => ToWorkItemDto(c)!).Where(c => c is not null).ToList(),
            Checklists = m.Checklists?.Select(c => ToChecklistDto(c)!).Where(c => c is not null).ToList(),
            CustomFields = m.CustomFields.Select(cf => new WorkItemFieldValueDto
            {
                Id = Guid.Parse(cf.Id),
                WorkItemId = Guid.Parse(cf.WorkItemId),
                CustomFieldId = Guid.Parse(cf.CustomFieldId),
                CustomFieldName = cf.CustomFieldName,
                FieldType = Enum.TryParse<CustomFieldType>(cf.FieldType, out var cft) ? cft : CustomFieldType.Text,
                Value = cf.Value,
                UpdatedAt = ParseDateTime(cf.UpdatedAt)
            }).ToList()
        };
    }

    private static WorkItemCommentDto? ToCommentDto(WorkItemCommentMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new WorkItemCommentDto
        {
            Id = Guid.Parse(m.Id),
            WorkItemId = Guid.Parse(m.WorkItemId),
            UserId = Guid.Parse(m.UserId),
            DisplayName = m.DisplayName,
            Content = m.Content,
            IsEdited = m.IsEdited,
            IsDeleted = m.IsDeleted,
            DeletedAt = string.IsNullOrEmpty(m.DeletedAt) ? null : ParseDateTime(m.DeletedAt),
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static ChecklistDto? ToChecklistDto(ChecklistMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new ChecklistDto
        {
            Id = Guid.Parse(m.Id),
            ItemId = Guid.Parse(m.ItemId),
            Title = m.Title,
            Position = m.Position,
            Items = m.Items.Select(ToChecklistItemDto).Where(i => i is not null).Select(i => i!).ToList(),
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static ChecklistItemDto? ToChecklistItemDto(ChecklistItemMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new ChecklistItemDto
        {
            Id = Guid.Parse(m.Id),
            ChecklistId = Guid.Parse(m.ChecklistId),
            Title = m.Title,
            IsCompleted = m.IsCompleted,
            Position = m.Position,
            AssignedToUserId = string.IsNullOrEmpty(m.AssignedToUserId) ? null : Guid.Parse(m.AssignedToUserId),
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static WorkItemAttachmentDto? ToAttachmentDto(WorkItemAttachmentMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new WorkItemAttachmentDto
        {
            Id = Guid.Parse(m.Id),
            WorkItemId = Guid.Parse(m.WorkItemId),
            FileNodeId = string.IsNullOrEmpty(m.FileNodeId) ? null : Guid.Parse(m.FileNodeId),
            Url = string.IsNullOrEmpty(m.Url) ? null : m.Url,
            FileName = m.FileName,
            FileSize = m.FileSize,
            MimeType = string.IsNullOrEmpty(m.MimeType) ? null : m.MimeType,
            UploadedByUserId = Guid.Parse(m.UploadedByUserId),
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static WorkItemDependencyDto? ToDependencyDto(WorkItemDependencyMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new WorkItemDependencyDto
        {
            Id = Guid.Parse(m.Id),
            WorkItemId = Guid.Parse(m.WorkItemId),
            DependsOnWorkItemId = Guid.Parse(m.DependsOnWorkItemId),
            DependsOnTitle = m.DependsOnTitle,
            Type = Enum.TryParse<DependencyType>(m.Type, out var dt) ? dt : DependencyType.BlockedBy,
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static SprintDto? ToSprintDto(SprintMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new SprintDto
        {
            Id = Guid.Parse(m.Id),
            EpicId = Guid.Parse(m.EpicId),
            Title = m.Title,
            Goal = string.IsNullOrEmpty(m.Goal) ? null : m.Goal,
            StartDate = string.IsNullOrEmpty(m.StartDate) ? null : ParseDateTime(m.StartDate),
            EndDate = string.IsNullOrEmpty(m.EndDate) ? null : ParseDateTime(m.EndDate),
            Status = Enum.TryParse<SprintStatus>(m.Status, out var status) ? status : SprintStatus.Planning,
            TargetStoryPoints = m.TargetStoryPoints == 0 ? null : m.TargetStoryPoints,
            DurationWeeks = m.DurationWeeks == 0 ? null : m.DurationWeeks,
            PlannedOrder = m.PlannedOrder == 0 ? null : m.PlannedOrder,
            ItemCount = m.ItemCount,
            TotalStoryPoints = m.TotalStoryPoints,
            CompletedStoryPoints = m.CompletedStoryPoints,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static TimeEntryDto? ToTimeEntryDto(TimeEntryMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new TimeEntryDto
        {
            Id = Guid.Parse(m.Id),
            WorkItemId = Guid.Parse(m.WorkItemId),
            UserId = Guid.Parse(m.UserId),
            StartTime = string.IsNullOrEmpty(m.StartTime) ? null : ParseDateTime(m.StartTime),
            EndTime = string.IsNullOrEmpty(m.EndTime) ? null : ParseDateTime(m.EndTime),
            DurationMinutes = m.DurationMinutes,
            Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static ActivityDto? ToActivityDto(ActivityMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new ActivityDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            UserId = Guid.Parse(m.UserId),
            DisplayName = m.DisplayName,
            Action = m.Action,
            EntityType = m.EntityType,
            EntityId = Guid.Parse(m.EntityId),
            Details = string.IsNullOrEmpty(m.Details) ? null : m.Details,
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static ProductAnalyticsDto? ToProductAnalyticsDto(ProductAnalyticsMessage? m)
    {
        if (m is null)
            return null;
        return new ProductAnalyticsDto
        {
            TotalItems = m.TotalItems,
            TotalEpics = m.TotalEpics,
            TotalFeatures = m.TotalFeatures,
            ItemsCompletedThisWeek = m.ItemsCompletedThisWeek,
            ActiveSprints = m.ActiveSprints,
            AvgCycleTimeDays = m.AvgCycleTimeDays,
            DailyCompletions = m.DailyCompletions.Select(dc => new DailyCompletionDto
            {
                Date = ParseDateTime(dc.Date),
                CompletedCount = dc.CompletedCount
            }).ToList()
        };
    }

    private static SprintVelocityDto? ToSprintVelocityDto(SprintVelocityMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.SprintId))
            return null;
        return new SprintVelocityDto
        {
            SprintId = Guid.Parse(m.SprintId),
            SprintTitle = m.SprintTitle,
            CompletedStoryPoints = m.CompletedStoryPoints,
            TotalStoryPoints = m.TotalStoryPoints
        };
    }

    private static SprintReportDto? ToSprintReportDto(SprintReportMessage? m)
    {
        if (m is null || m.Sprint is null)
            return null;
        return new SprintReportDto
        {
            Sprint = ToSprintDto(m.Sprint)!,
            CompletedItems = m.CompletedItems,
            IncompleteItems = m.IncompleteItems,
            CompletedStoryPoints = m.CompletedStoryPoints,
            TotalStoryPoints = m.TotalStoryPoints
        };
    }

    private static SprintBurndownDto? ToBurndownDto(BurndownMessage? m)
    {
        if (m is null)
            return null;
        return new SprintBurndownDto
        {
            TotalStoryPoints = m.TotalStoryPoints,
            Points = m.Points.Select(p => new BurndownPointDto
            {
                Date = ParseDateTime(p.Date),
                RemainingStoryPoints = p.RemainingStoryPoints
            }).ToList()
        };
    }

    private static ProductDashboardDto? ToProductDashboardDto(ProductDashboardMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.ProductId))
            return null;
        return new ProductDashboardDto
        {
            ProductId = Guid.Parse(m.ProductId),
            ProductName = m.ProductName,
            TotalItems = m.TotalItems,
            TotalEpics = m.TotalEpics,
            TotalFeatures = m.TotalFeatures,
            ActiveSprints = m.ActiveSprints,
            AvgCycleTimeDays = m.AvgCycleTimeDays,
            ItemsCompletedThisWeek = m.ItemsCompletedThisWeek,
            UnassignedItems = m.UnassignedItems,
            StatusBreakdown = m.StatusBreakdown.Select(sb => new StatusBreakdownDto
            {
                SwimlaneId = Guid.Parse(sb.SwimlaneId),
                SwimlaneTitle = sb.SwimlaneTitle,
                Color = sb.Color,
                Count = sb.Count
            }).ToList(),
            PriorityBreakdown = m.PriorityBreakdown.Select(pb => new PriorityBreakdownDto
            {
                Priority = Enum.TryParse<Priority>(pb.Priority, out var pri) ? pri : Priority.None,
                Count = pb.Count
            }).ToList(),
            Workload = m.Workload.Select(w => new WorkloadDto
            {
                UserId = Guid.Parse(w.UserId),
                DisplayName = w.DisplayName,
                AssignedItems = w.AssignedItems,
                TotalStoryPoints = w.TotalStoryPoints
            }).ToList(),
            RecentlyUpdated = m.RecentlyUpdated.Select(ru => new RecentlyUpdatedItemDto
            {
                Id = Guid.Parse(ru.Id),
                ItemNumber = ru.ItemNumber,
                Title = ru.Title,
                Type = Enum.TryParse<WorkItemType>(ru.Type, out var wtype) ? wtype : WorkItemType.Item,
                Priority = Enum.TryParse<Priority>(ru.Priority, out var rpri) ? rpri : Priority.None,
                SwimlaneTitle = string.IsNullOrEmpty(ru.SwimlaneTitle) ? null : ru.SwimlaneTitle,
                SprintId = string.IsNullOrEmpty(ru.SprintId) ? null : Guid.Parse(ru.SprintId),
                SprintTitle = string.IsNullOrEmpty(ru.SprintTitle) ? null : ru.SprintTitle,
                UpdatedAt = ParseDateTime(ru.UpdatedAt)
            }).ToList(),
            UpcomingDueDates = m.UpcomingDueDates.Select(ud => new UpcomingDueDateDto
            {
                Id = Guid.Parse(ud.Id),
                ItemNumber = ud.ItemNumber,
                Title = ud.Title,
                Type = Enum.TryParse<WorkItemType>(ud.Type, out var utype) ? utype : WorkItemType.Item,
                Priority = Enum.TryParse<Priority>(ud.Priority, out var upri) ? upri : Priority.None,
                SwimlaneTitle = string.IsNullOrEmpty(ud.SwimlaneTitle) ? null : ud.SwimlaneTitle,
                DueDate = ParseDateTime(ud.DueDate)
            }).ToList()
        };
    }

    private static PokerSessionDto? ToPokerSessionDto(PokerSessionMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new PokerSessionDto
        {
            Id = Guid.Parse(m.Id),
            EpicId = Guid.Parse(m.EpicId),
            ItemId = Guid.Parse(m.ItemId),
            CreatedByUserId = Guid.Parse(m.CreatedByUserId),
            Scale = Enum.TryParse<PokerScale>(m.Scale, out var scale) ? scale : PokerScale.Fibonacci,
            CustomScaleValues = string.IsNullOrEmpty(m.CustomScaleValues) ? null : m.CustomScaleValues,
            Status = Enum.TryParse<PokerSessionStatus>(m.Status, out var status) ? status : PokerSessionStatus.Voting,
            AcceptedEstimate = string.IsNullOrEmpty(m.AcceptedEstimate) ? null : m.AcceptedEstimate,
            Round = m.Round,
            ReviewSessionId = string.IsNullOrEmpty(m.ReviewSessionId) ? null : Guid.Parse(m.ReviewSessionId),
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static ReviewSessionDto? ToReviewSessionDto(ReviewSessionMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new ReviewSessionDto
        {
            Id = Guid.Parse(m.Id),
            EpicId = Guid.Parse(m.EpicId),
            HostUserId = Guid.Parse(m.HostUserId),
            CurrentItemId = string.IsNullOrEmpty(m.CurrentItemId) ? null : Guid.Parse(m.CurrentItemId),
            Status = Enum.TryParse<ReviewSessionStatus>(m.Status, out var status) ? status : ReviewSessionStatus.Active,
            ParticipantCount = m.ParticipantCount,
            CreatedAt = ParseDateTime(m.CreatedAt),
            EndedAt = string.IsNullOrEmpty(m.EndedAt) ? null : ParseDateTime(m.EndedAt)
        };
    }

    private static TracksTeamDto? ToTracksTeamDto(TracksTeamMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new TracksTeamDto
        {
            Id = Guid.Parse(m.Id),
            TeamId = Guid.Parse(m.TeamId),
            Name = m.Name,
            Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
            MemberCount = m.MemberCount,
            CreatedAt = ParseDateTime(m.CreatedAt)
        };
    }

    private static TracksTeamMemberDto? ToTracksTeamMemberDto(TracksTeamMemberMessage? m)
    {
        if (m is null)
            return null;
        return new TracksTeamMemberDto
        {
            UserId = Guid.Parse(m.UserId),
            DisplayName = m.DisplayName,
            Role = Enum.TryParse<TracksTeamMemberRole>(m.Role, out var role) ? role : TracksTeamMemberRole.Member,
            AssignedAt = ParseDateTime(m.AssignedAt)
        };
    }

    private static CustomViewDto? ToCustomViewDto(CustomViewMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new CustomViewDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            UserId = Guid.Parse(m.UserId),
            Name = m.Name,
            FilterJson = m.FilterJson,
            SortJson = m.SortJson,
            GroupBy = string.IsNullOrEmpty(m.GroupBy) ? null : m.GroupBy,
            Layout = m.Layout,
            IsShared = m.IsShared,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static WebhookSubscriptionDto? ToWebhookSubscription(WebhookSubscriptionMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new WebhookSubscriptionDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            Url = m.Url,
            IsActive = m.IsActive,
            CreatedByUserId = Guid.Parse(m.CreatedByUserId),
            LastDeliveryAt = string.IsNullOrEmpty(m.LastDeliveryAt) ? null : ParseDateTime(m.LastDeliveryAt),
            FailedDeliveryCount = m.FailedDeliveryCount,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static RoadmapDataDto? ToRoadmapDataDto(RoadmapDataMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.ProductId))
            return null;
        return new RoadmapDataDto
        {
            ProductId = Guid.Parse(m.ProductId),
            ProductName = m.ProductName,
            Items = m.Items.Select(i => new RoadmapItemDto
            {
                Id = Guid.Parse(i.Id),
                ItemNumber = i.ItemNumber,
                Title = i.Title,
                Type = Enum.TryParse<WorkItemType>(i.Type, out var wtype) ? wtype : WorkItemType.Item,
                Priority = Enum.TryParse<Priority>(i.Priority, out var pri) ? pri : Priority.None,
                SwimlaneTitle = string.IsNullOrEmpty(i.SwimlaneTitle) ? null : i.SwimlaneTitle,
                SwimlaneColor = string.IsNullOrEmpty(i.SwimlaneColor) ? null : i.SwimlaneColor,
                StartDate = string.IsNullOrEmpty(i.StartDate) ? null : ParseDateTime(i.StartDate),
                DueDate = string.IsNullOrEmpty(i.DueDate) ? null : ParseDateTime(i.DueDate),
                MilestoneId = string.IsNullOrEmpty(i.MilestoneId) ? null : Guid.Parse(i.MilestoneId),
                MilestoneTitle = string.IsNullOrEmpty(i.MilestoneTitle) ? null : i.MilestoneTitle,
                DependencyIds = i.DependencyIds.Select(Guid.Parse).ToList(),
                AssigneeUserId = string.IsNullOrEmpty(i.AssigneeUserId) ? null : Guid.Parse(i.AssigneeUserId),
                AssigneeDisplayName = string.IsNullOrEmpty(i.AssigneeDisplayName) ? null : i.AssigneeDisplayName
            }).ToList(),
            Milestones = m.Milestones.Select(ml => new MilestoneDto
            {
                Id = Guid.Parse(ml.Id),
                ProductId = Guid.Parse(ml.ProductId),
                Title = ml.Title,
                Description = string.IsNullOrEmpty(ml.Description) ? null : ml.Description,
                DueDate = string.IsNullOrEmpty(ml.DueDate) ? null : ParseDateTime(ml.DueDate),
                Status = Enum.TryParse<MilestoneStatus>(ml.Status, out var mstat) ? mstat : MilestoneStatus.Upcoming,
                Color = string.IsNullOrEmpty(ml.Color) ? null : ml.Color,
                WorkItemCount = ml.WorkItemCount,
                CompletedWorkItemCount = ml.CompletedWorkItemCount,
                CreatedAt = ParseDateTime(ml.CreatedAt),
                UpdatedAt = ParseDateTime(ml.UpdatedAt)
            }).ToList()
        };
    }

    private static AutomationRuleDto? ToAutomationRuleDto(AutomationRuleMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new AutomationRuleDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            Name = m.Name,
            Trigger = m.Trigger,
            ConditionsJson = m.ConditionsJson,
            ActionsJson = m.ActionsJson,
            IsActive = m.IsActive,
            CreatedByUserId = Guid.Parse(m.CreatedByUserId),
            LastTriggeredAt = string.IsNullOrEmpty(m.LastTriggeredAt) ? null : ParseDateTime(m.LastTriggeredAt),
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static GoalDto? ToGoalDto(GoalMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new GoalDto
        {
            Id = Guid.Parse(m.Id),
            ProductId = Guid.Parse(m.ProductId),
            Title = m.Title,
            Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
            Type = m.Type,
            ParentGoalId = string.IsNullOrEmpty(m.ParentGoalId) ? null : Guid.Parse(m.ParentGoalId),
            TargetValue = m.TargetValue,
            CurrentValue = m.CurrentValue,
            ProgressType = m.ProgressType,
            Status = m.Status,
            DueDate = string.IsNullOrEmpty(m.DueDate) ? null : ParseDateTime(m.DueDate),
            CreatedByUserId = Guid.Parse(m.CreatedByUserId),
            LinkedWorkItemCount = m.LinkedWorkItemCount,
            CompletedLinkedWorkItemCount = m.CompletedLinkedWorkItemCount,
            ProgressPercent = m.ProgressPercent,
            CreatedAt = ParseDateTime(m.CreatedAt),
            UpdatedAt = ParseDateTime(m.UpdatedAt)
        };
    }

    private static ProductCapacityDto? ToProductCapacityDto(ProductCapacityMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.ProductId))
            return null;
        return new ProductCapacityDto
        {
            ProductId = Guid.Parse(m.ProductId),
            Members = m.Members.Select(mc => new MemberCapacityDto
            {
                UserId = Guid.Parse(mc.UserId),
                DisplayName = mc.DisplayName,
                AssignedStoryPoints = mc.AssignedStoryPoints,
                AssignedItemCount = mc.AssignedItemCount,
                CapacityPercent = mc.CapacityPercent,
                SprintTitles = mc.SprintTitles.ToList()
            }).ToList(),
            TotalAssignedStoryPoints = m.TotalAssignedStoryPoints,
            TotalMembers = m.TotalMembers,
            OverloadedMembers = m.OverloadedMembers
        };
    }

    private static SprintCapacityDto? ToSprintCapacityDto(SprintCapacityMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.SprintId))
            return null;
        return new SprintCapacityDto
        {
            SprintId = Guid.Parse(m.SprintId),
            SprintTitle = m.SprintTitle,
            TotalStoryPoints = m.TotalStoryPoints,
            TargetStoryPoints = m.TargetStoryPoints,
            CompletedStoryPoints = m.CompletedStoryPoints
        };
    }

    // ─── Work Item Create Helpers ──────────────────────────────────────────

    private static void PopulateCreateWorkItemRequest(CreateWorkItemDto dto, CreateEpicRequest request)
    {
        request.Title = dto.Title ?? string.Empty;
        request.Description = dto.Description ?? string.Empty;
        request.Priority = dto.Priority.ToString();
        request.StartDate = FormatDateTime(dto.StartDate);
        request.DueDate = FormatDateTime(dto.DueDate);
        request.StoryPoints = dto.StoryPoints ?? 0;
        request.AssigneeIds.AddRange(dto.AssigneeIds.Select(id => id.ToString()));
        request.LabelIds.AddRange(dto.LabelIds.Select(id => id.ToString()));
    }

    private static void PopulateCreateWorkItemRequest(CreateWorkItemDto dto, CreateFeatureRequest request)
    {
        request.Title = dto.Title ?? string.Empty;
        request.Description = dto.Description ?? string.Empty;
        request.Priority = dto.Priority.ToString();
        request.StartDate = FormatDateTime(dto.StartDate);
        request.DueDate = FormatDateTime(dto.DueDate);
        request.StoryPoints = dto.StoryPoints ?? 0;
        request.AssigneeIds.AddRange(dto.AssigneeIds.Select(id => id.ToString()));
        request.LabelIds.AddRange(dto.LabelIds.Select(id => id.ToString()));
    }

    private static void PopulateCreateWorkItemRequest(CreateWorkItemDto dto, CreateItemRequest request)
    {
        request.Title = dto.Title ?? string.Empty;
        request.Description = dto.Description ?? string.Empty;
        request.Priority = dto.Priority.ToString();
        request.StartDate = FormatDateTime(dto.StartDate);
        request.DueDate = FormatDateTime(dto.DueDate);
        request.StoryPoints = dto.StoryPoints ?? 0;
        request.AssigneeIds.AddRange(dto.AssigneeIds.Select(id => id.ToString()));
        request.LabelIds.AddRange(dto.LabelIds.Select(id => id.ToString()));
    }

    private static void PopulateCreateWorkItemRequest(CreateWorkItemDto dto, CreateSubItemRequest request)
    {
        request.Title = dto.Title ?? string.Empty;
        request.Description = dto.Description ?? string.Empty;
        request.Priority = dto.Priority.ToString();
        request.StartDate = FormatDateTime(dto.StartDate);
        request.DueDate = FormatDateTime(dto.DueDate);
        request.StoryPoints = dto.StoryPoints ?? 0;
        request.AssigneeIds.AddRange(dto.AssigneeIds.Select(id => id.ToString()));
        request.LabelIds.AddRange(dto.LabelIds.Select(id => id.ToString()));
    }

    private async Task<WorkItemDto?> CreateWorkItemInternalAsync(
        Func<Task<WorkItemResponse>> call,
        CancellationToken ct,
        string methodName)
    {
        try
        {
            var response = await call();
            return response.Success ? ToWorkItemDto(response.WorkItem) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "TracksGrpcApiClient.{MethodName} failed", methodName);
            return null;
        }
    }

    // ─── Deadline / Dispose Helpers ────────────────────────────────────────

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
