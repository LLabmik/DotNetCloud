using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests.Host;

/// <summary>
/// Tests for <see cref="OrganizationsController"/> — the endpoint a sandboxed module UI uses to
/// discover the acting user's organizations through the <see cref="IOrganizationDirectory"/>
/// capability.
/// </summary>
[TestClass]
public class OrganizationsControllerTests
{
    private static readonly Guid Caller = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static OrganizationsController CreateController(
        IOrganizationDirectory directory,
        ProductService productService,
        Guid? callerId = null)
    {
        var controller = new OrganizationsController(
            directory,
            productService,
            NullLogger<OrganizationsController>.Instance);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, (callerId ?? Caller).ToString())],
            "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return controller;
    }

    private static OrganizationDto CreateOrganization(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        Description = $"{name} description",
        MemberCount = 3,
        TeamCount = 1,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [TestMethod]
    public async Task ListMyOrganizations_QueriesTheActingUser_NotARequestSuppliedId()
    {
        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(Caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrganizationDto>());

        await using var db = TestHelpers.CreateDb();
        var controller = CreateController(directory.Object, new ProductService(db));

        await controller.ListMyOrganizationsAsync(CancellationToken.None);

        directory.Verify(
            x => x.GetUserOrganizationsAsync(Caller, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ListMyOrganizations_ReturnsDirectoryOrganizationsWithProductCounts()
    {
        var withProducts = Guid.CreateVersion7();
        var empty = Guid.CreateVersion7();

        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateOrganization(withProducts, "Acme"),
                CreateOrganization(empty, "Globex"),
            });

        await using var db = TestHelpers.CreateDb();
        await TestHelpers.SeedProductAsync(db, withProducts, Caller, "Acme Board");
        await TestHelpers.SeedProductAsync(db, withProducts, Caller, "Acme Ops");

        var controller = CreateController(directory.Object, new ProductService(db));

        var result = await controller.ListMyOrganizationsAsync(CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok, "the endpoint should answer 200 with the envelope");

        var organizations = GetEnvelopeData(ok.Value);
        Assert.AreEqual(2, organizations.Count);
        Assert.AreEqual(withProducts, organizations[0].Id);
        Assert.AreEqual("Acme", organizations[0].Name);
        Assert.AreEqual(2, organizations[0].ProductCount);
        Assert.AreEqual(0, organizations[1].ProductCount);
    }

    [TestMethod]
    public async Task ListMyOrganizations_DirectoryFails_ReturnsServerErrorEnvelope()
    {
        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("core unreachable"));

        await using var db = TestHelpers.CreateDb();
        var controller = CreateController(directory.Object, new ProductService(db));

        var result = await controller.ListMyOrganizationsAsync(CancellationToken.None);

        var error = result as ObjectResult;
        Assert.IsNotNull(error);
        Assert.AreEqual(500, error.StatusCode);
    }

    [TestMethod]
    public async Task ListMyOrganizations_NoAssertedUser_Throws()
    {
        var directory = new Mock<IOrganizationDirectory>();
        await using var db = TestHelpers.CreateDb();
        var controller = new OrganizationsController(
            directory.Object,
            new ProductService(db),
            NullLogger<OrganizationsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
        };

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => controller.ListMyOrganizationsAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ListMyOrganizations_MissingProfileConsent_Answers403WithACodeTheUiCanActOn()
    {
        var metadata = new Metadata
        {
            { "error-code", "MODULE_CONSENT_REQUIRED" },
            { "consent-scopes", "profile" },
        };

        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RpcException(
                new Status(StatusCode.PermissionDenied, "[MODULE_CONSENT_REQUIRED] consent needed"),
                metadata));

        await using var db = TestHelpers.CreateDb();
        var controller = CreateController(directory.Object, new ProductService(db));

        var result = await controller.ListMyOrganizationsAsync(CancellationToken.None);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(403, objectResult.StatusCode, "a missing consent is not an empty listing: the UI must be able to prompt");

        var error = objectResult.Value!.GetType().GetProperty("error")!.GetValue(objectResult.Value)!;
        Assert.AreEqual("PROFILE_CONSENT_REQUIRED", error.GetType().GetProperty("code")!.GetValue(error));
    }

    /// <summary>
    /// The envelope is an anonymous object (<c>{ success, data }</c>); read <c>data</c> back through
    /// its runtime type.
    /// </summary>
    private static List<OrganizationsController.OrganizationSummary> GetEnvelopeData(object? envelope)
    {
        Assert.IsNotNull(envelope);

        var dataProperty = envelope.GetType().GetProperty("data");
        Assert.IsNotNull(dataProperty, "the success envelope must carry a 'data' property");

        var data = dataProperty.GetValue(envelope) as IEnumerable<OrganizationsController.OrganizationSummary>;
        Assert.IsNotNull(data);

        return data.ToList();
    }
}
