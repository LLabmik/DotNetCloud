using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ProductServiceTests
{
    private TracksDbContext _db = null!;
    private ProductService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new ProductService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateProductAsync_ValidInput_ReturnsProductDto()
    {
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var dto = new CreateProductDto { Name = "My Project" };

        var result = await _service.CreateProductAsync(orgId, ownerId, dto, CancellationToken.None);

        Assert.AreEqual("My Project", result.Name);
        Assert.AreEqual(orgId, result.OrganizationId);
        Assert.AreEqual(ownerId, result.OwnerId);
        Assert.IsTrue(result.MemberCount >= 1);
    }

    [TestMethod]
    public async Task GetProductAsync_ExistingProduct_ReturnsProduct()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.AreEqual(product.Id, result.Id);
        Assert.AreEqual(product.Name, result.Name);
    }

    [TestMethod]
    public async Task UpdateProductAsync_UpdatesName()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var dto = new UpdateProductDto { Name = "Updated Name" };

        var result = await _service.UpdateProductAsync(product.Id, dto, CancellationToken.None);

        Assert.AreEqual("Updated Name", result.Name);
    }

    // DeleteProductAsync and UndeleteProductAsync use ExecuteUpdateAsync which is
    // not supported by the InMemory database provider. Skip those tests.

    [TestMethod]
    public async Task ListProductsByOrganizationAsync_ReturnsProducts()
    {
        var orgId = Guid.NewGuid();
        await TestHelpers.SeedProductAsync(_db, orgId, Guid.NewGuid(), "A");
        await TestHelpers.SeedProductAsync(_db, orgId, Guid.NewGuid(), "B");

        var result = await _service.ListProductsByOrganizationAsync(orgId, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task AddMemberAsync_AddsUserToProduct()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var dto = new AddProductMemberDto { UserId = Guid.NewGuid(), Role = ProductMemberRole.Member };

        var result = await _service.AddMemberAsync(product.Id, dto, CancellationToken.None);

        Assert.AreEqual(dto.UserId, result.UserId);
        Assert.AreEqual(ProductMemberRole.Member, result.Role);
    }

    [TestMethod]
    public async Task GetUserProductRoleAsync_ReturnsMemberRole()
    {
        var ownerId = Guid.NewGuid();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), ownerId);

        var role = await _service.GetUserProductRoleAsync(product.Id, ownerId, CancellationToken.None);

        Assert.AreEqual(ProductMemberRole.Owner, role);
    }

    [TestMethod]
    public async Task RemoveMemberAsync_RemovesUserFromProduct()
    {
        var ownerId = Guid.NewGuid();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), ownerId);
        var memberId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, product.Id, memberId, ProductMemberRole.Member);

        await _service.RemoveMemberAsync(product.Id, memberId, CancellationToken.None);

        var role = await _service.GetUserProductRoleAsync(product.Id, memberId, CancellationToken.None);
        Assert.IsNull(role);
    }

    [TestMethod]
    public async Task CreateLabelAsync_CreatesLabel()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var dto = new CreateLabelDto { Title = "Bug", Color = "#ff0000" };

        var result = await _service.CreateLabelAsync(product.Id, dto, CancellationToken.None);

        Assert.AreEqual("Bug", result.Title);
        Assert.AreEqual("#ff0000", result.Color);
        Assert.AreEqual(product.Id, result.ProductId);
    }

    [TestMethod]
    public async Task DeleteLabelAsync_RemovesLabel()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var label = await _service.CreateLabelAsync(product.Id, new CreateLabelDto { Title = "Temp", Color = "#ccc" }, CancellationToken.None);

        await _service.DeleteLabelAsync(product.Id, label.Id, CancellationToken.None);

        var labels = _db.Labels.Where(l => l.ProductId == product.Id).ToList();
        Assert.AreEqual(0, labels.Count(l => l.Id == label.Id));
    }
}
