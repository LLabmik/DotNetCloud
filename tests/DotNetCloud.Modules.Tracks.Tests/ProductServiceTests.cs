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
        var orgId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
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
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetProductAsync(product.Id, CancellationToken.None);

        Assert.AreEqual(product.Id, result.Id);
        Assert.AreEqual(product.Name, result.Name);
    }

    [TestMethod]
    public async Task UpdateProductAsync_UpdatesName()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var dto = new UpdateProductDto { Name = "Updated Name" };

        var result = await _service.UpdateProductAsync(product.Id, dto, CancellationToken.None);

        Assert.AreEqual("Updated Name", result.Name);
    }

    // DeleteProductAsync and UndeleteProductAsync use ExecuteUpdateAsync which is
    // not supported by the InMemory database provider. Skip those tests.

    [TestMethod]
    public async Task ListProductsByOrganizationAsync_ReturnsProducts()
    {
        var orgId = Guid.CreateVersion7();
        await TestHelpers.SeedProductAsync(_db, orgId, Guid.CreateVersion7(), "A");
        await TestHelpers.SeedProductAsync(_db, orgId, Guid.CreateVersion7(), "B");

        var result = await _service.ListProductsByOrganizationAsync(orgId, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task AddMemberAsync_AddsUserToProduct()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var dto = new AddProductMemberDto { UserId = Guid.CreateVersion7(), Role = ProductMemberRole.Member };

        var result = await _service.AddMemberAsync(product.Id, dto, CancellationToken.None);

        Assert.AreEqual(dto.UserId, result.UserId);
        Assert.AreEqual(ProductMemberRole.Member, result.Role);
    }

    [TestMethod]
    public async Task GetUserProductRoleAsync_ReturnsMemberRole()
    {
        var ownerId = Guid.CreateVersion7();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), ownerId);

        var role = await _service.GetUserProductRoleAsync(product.Id, ownerId, CancellationToken.None);

        Assert.AreEqual(ProductMemberRole.Owner, role);
    }

    [TestMethod]
    public async Task RemoveMemberAsync_RemovesUserFromProduct()
    {
        var ownerId = Guid.CreateVersion7();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), ownerId);
        var memberId = Guid.CreateVersion7();
        await TestHelpers.AddMemberAsync(_db, product.Id, memberId, ProductMemberRole.Member);

        await _service.RemoveMemberAsync(product.Id, memberId, CancellationToken.None);

        var role = await _service.GetUserProductRoleAsync(product.Id, memberId, CancellationToken.None);
        Assert.IsNull(role);
    }

    [TestMethod]
    public async Task CreateLabelAsync_CreatesLabel()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var dto = new CreateLabelDto { Title = "Bug", Color = "#ff0000" };

        var result = await _service.CreateLabelAsync(product.Id, dto, CancellationToken.None);

        Assert.AreEqual("Bug", result.Title);
        Assert.AreEqual("#ff0000", result.Color);
        Assert.AreEqual(product.Id, result.ProductId);
    }

    [TestMethod]
    public async Task DeleteLabelAsync_RemovesLabel()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var label = await _service.CreateLabelAsync(product.Id, new CreateLabelDto { Title = "Temp", Color = "#ccc" }, CancellationToken.None);

        await _service.DeleteLabelAsync(product.Id, label.Id, CancellationToken.None);

        var labels = _db.Labels.Where(l => l.ProductId == product.Id).ToList();
        Assert.AreEqual(0, labels.Count(l => l.Id == label.Id));
    }

    // ── Member Role Management ──────────────────────────────

    [TestMethod]
    public async Task UpdateMemberRoleAsync_ChangesMemberRole()
    {
        var ownerId = Guid.CreateVersion7();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), ownerId);
        var memberId = Guid.CreateVersion7();
        await TestHelpers.AddMemberAsync(_db, product.Id, memberId, ProductMemberRole.Viewer);

        var result = await _service.UpdateMemberRoleAsync(product.Id, memberId, ProductMemberRole.Admin, CancellationToken.None);

        Assert.AreEqual(ProductMemberRole.Admin, result.Role);
        Assert.AreEqual(memberId, result.UserId);
    }

    [TestMethod]
    public async Task UpdateMemberRoleAsync_DemotingLastOwner_ThrowsInvalidOperationException()
    {
        var ownerId = Guid.CreateVersion7();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), ownerId);

        try
        {
            await _service.UpdateMemberRoleAsync(product.Id, ownerId, ProductMemberRole.Member, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "last Owner");
        }
    }

    [TestMethod]
    public async Task UpdateMemberRoleAsync_NonExistentMember_ThrowsInvalidOperationException()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var nonMemberId = Guid.CreateVersion7();

        try
        {
            await _service.UpdateMemberRoleAsync(product.Id, nonMemberId, ProductMemberRole.Member, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "not a member");
        }
    }

    [TestMethod]
    public async Task AddMemberAsync_DuplicateMember_ThrowsInvalidOperationException()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var memberId = Guid.CreateVersion7();
        await TestHelpers.AddMemberAsync(_db, product.Id, memberId, ProductMemberRole.Member);

        var dto = new AddProductMemberDto { UserId = memberId, Role = ProductMemberRole.Viewer };

        try
        {
            await _service.AddMemberAsync(product.Id, dto, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "already a member");
        }
    }

    [TestMethod]
    public async Task RemoveMemberAsync_RemovingLastOwner_ThrowsInvalidOperationException()
    {
        var ownerId = Guid.CreateVersion7();
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), ownerId);

        try
        {
            await _service.RemoveMemberAsync(product.Id, ownerId, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "last Owner");
        }
    }

    [TestMethod]
    public async Task RemoveMemberAsync_NonExistentMember_ThrowsInvalidOperationException()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var nonMemberId = Guid.CreateVersion7();

        try
        {
            await _service.RemoveMemberAsync(product.Id, nonMemberId, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "not a member");
        }
    }

    [TestMethod]
    public async Task UpdateMemberRoleAsync_DemotingNonLastOwner_Succeeds()
    {
        var orgId = Guid.CreateVersion7();
        var owner1Id = Guid.CreateVersion7();
        var owner2Id = Guid.CreateVersion7();

        // Create product with owner1
        var product = await TestHelpers.SeedProductAsync(_db, orgId, owner1Id);
        // Add owner2 as second Owner
        await TestHelpers.AddMemberAsync(_db, product.Id, owner2Id, ProductMemberRole.Owner);

        // Demoting owner1 should succeed (owner2 remains)
        var result = await _service.UpdateMemberRoleAsync(product.Id, owner1Id, ProductMemberRole.Admin, CancellationToken.None);

        Assert.AreEqual(ProductMemberRole.Admin, result.Role);
    }
}
