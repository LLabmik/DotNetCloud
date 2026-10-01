using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Moq;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="ShareRecipientSearchService"/>, which backs the type-ahead
/// recipient search in the unified share dialog.
/// </summary>
[TestClass]
public class ShareRecipientSearchServiceTests
{
    private static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A user's email address must never be surfaced in share search results.</summary>
    [TestMethod]
    public async Task SearchAsync_WhenMatchesAUser_DoesNotExposeEmail()
    {
        var userDirectory = new Mock<IUserDirectory>();
        userDirectory
            .Setup(d => d.SearchUsersAsync("alice", 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserSearchResult(Guid.NewGuid(), "Alice Smith", "alice@example.com")]);

        var service = CreateService(userDirectory.Object, teams: []);

        var results = await service.SearchAsync("alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Smith", results[0].DisplayName);
        Assert.IsNull(results[0].SecondaryText, "The user's email must not be shown.");
        Assert.IsFalse(
            results[0].SecondaryText?.Contains("@", StringComparison.Ordinal) == true,
            "No email-like text may appear in share search results.");
    }

    /// <summary>Teams still show their member count as the secondary line.</summary>
    [TestMethod]
    public async Task SearchAsync_WhenMatchesATeam_StillShowsMemberCount()
    {
        var userDirectory = new Mock<IUserDirectory>();
        userDirectory
            .Setup(d => d.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService(userDirectory.Object, teams:
        [
            new TeamInfo
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                Name = "Design Team",
                MemberCount = 4,
                CreatedAt = DateTime.UtcNow,
            },
        ]);

        var results = await service.SearchAsync("design");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Team", results[0].RecipientType);
        Assert.AreEqual("4 members", results[0].SecondaryText);
    }

    /// <summary>Blank search terms return nothing instead of the whole directory.</summary>
    [TestMethod]
    public async Task SearchAsync_WhenTermIsBlank_ReturnsEmpty()
    {
        var userDirectory = new Mock<IUserDirectory>();
        var service = CreateService(userDirectory.Object, teams: []);

        var results = await service.SearchAsync("   ");

        Assert.AreEqual(0, results.Count);
        userDirectory.Verify(
            d => d.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ShareRecipientSearchService CreateService(IUserDirectory userDirectory, IReadOnlyList<TeamInfo> teams)
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(d => d.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);

        return new ShareRecipientSearchService(userDirectory, teamDirectory.Object, new FakeAuthenticationStateProvider());
    }

    /// <summary>Minimal authenticated <see cref="AuthenticationStateProvider"/> for the current user.</summary>
    private sealed class FakeAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, CurrentUserId.ToString())],
                authenticationType: "Test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
