using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the <see cref="CoreCapabilitiesServiceImpl.GetTeamsForUser"/> and
/// <see cref="CoreCapabilitiesServiceImpl.GetTeam"/> RPC handlers.
/// </summary>
[TestClass]
public class CoreCapabilitiesTeamDirectoryTests
{
    private static CoreCapabilitiesServiceImpl CreateService(Mock<ITeamDirectory> teamDirectory)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => teamDirectory.Object);
        var provider = services.BuildServiceProvider();

        var indexingService = new SearchIndexingService(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);

        return new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            provider,
            indexingService);
    }

    private static GetTeamsForUserRequest CreateTeamsRequest(Guid? userId = null) => new()
    {
        UserId = (userId ?? Guid.CreateVersion7()).ToString(),
    };

    private static TeamInfo CreateTeam(string name = "Engineering") => new()
    {
        Id = Guid.CreateVersion7(),
        OrganizationId = Guid.CreateVersion7(),
        Name = name,
        Description = "desc",
        MemberCount = 3,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [TestMethod]
    public async Task GetTeamsForUser_ValidUser_ReturnsMappedTeams()
    {
        var team1 = CreateTeam("Engineering");
        var team2 = CreateTeam("Design");

        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { team1, team2 });

        var service = CreateService(teamDirectory);
        var userId = Guid.CreateVersion7();

        var response = await service.GetTeamsForUser(CreateTeamsRequest(userId), new TestServerCallContext());

        Assert.AreEqual(2, response.Teams.Count);
        Assert.AreEqual(team1.Id.ToString(), response.Teams[0].Id);
        Assert.AreEqual(team1.Name, response.Teams[0].Name);
        Assert.AreEqual(team1.OrganizationId.ToString(), response.Teams[0].OrganizationId);
        Assert.AreEqual(team1.MemberCount, response.Teams[0].MemberCount);
        Assert.AreEqual(team2.Name, response.Teams[1].Name);
    }

    [TestMethod]
    public async Task GetTeamsForUser_NoTeams_ReturnsEmpty()
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamInfo>());

        var service = CreateService(teamDirectory);

        var response = await service.GetTeamsForUser(CreateTeamsRequest(), new TestServerCallContext());

        Assert.IsNotNull(response);
        Assert.AreEqual(0, response.Teams.Count);
    }

    [TestMethod]
    public async Task GetTeamsForUser_InvalidUserId_ReturnsEmptyWithoutQuerying()
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        var service = CreateService(teamDirectory);

        var response = await service.GetTeamsForUser(new GetTeamsForUserRequest { UserId = "not-a-guid" }, new TestServerCallContext());

        Assert.AreEqual(0, response.Teams.Count);
        teamDirectory.Verify(
            x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetTeamsForUser_DirectoryThrows_ReturnsEmpty()
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var service = CreateService(teamDirectory);

        var response = await service.GetTeamsForUser(CreateTeamsRequest(), new TestServerCallContext());

        Assert.AreEqual(0, response.Teams.Count);
    }

    [TestMethod]
    public async Task GetTeam_Found_ReturnsMappedTeam()
    {
        var team = CreateTeam("Engineering");
        var teamId = team.Id;

        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var service = CreateService(teamDirectory);

        var response = await service.GetTeam(
            new GetTeamRequest { TeamId = teamId.ToString() },
            new TestServerCallContext());

        Assert.IsTrue(response.Found);
        Assert.IsNotNull(response.Team);
        Assert.AreEqual(team.Name, response.Team.Name);
        Assert.AreEqual(team.Id.ToString(), response.Team.Id);
    }

    [TestMethod]
    public async Task GetTeam_NotFound_ReturnsFoundFalse()
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamInfo?)null);

        var service = CreateService(teamDirectory);

        var response = await service.GetTeam(
            new GetTeamRequest { TeamId = Guid.CreateVersion7().ToString() },
            new TestServerCallContext());

        Assert.IsFalse(response.Found);
        Assert.IsNull(response.Team);
    }

    [TestMethod]
    public async Task GetTeam_InvalidTeamId_ReturnsFoundFalseWithoutQuerying()
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        var service = CreateService(teamDirectory);

        var response = await service.GetTeam(
            new GetTeamRequest { TeamId = "not-a-guid" },
            new TestServerCallContext());

        Assert.IsFalse(response.Found);
        teamDirectory.Verify(
            x => x.GetTeamAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
