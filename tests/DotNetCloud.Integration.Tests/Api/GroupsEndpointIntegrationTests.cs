using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Integration.Tests.Api;

[TestClass]
[TestCategory("Integration")]
public sealed class GroupsEndpointIntegrationTests
{
    [TestMethod]
    public async Task GroupCrudAndMembershipFlow_WorksOverCoreHost()
    {
        var adminUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        using var factory = new DotNetCloudWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Acme Integration",
                CreatedAt = DateTime.UtcNow,
            });
            db.Users.AddRange(
                CreateUser(adminUserId, "admin@example.com", "Admin User"),
                CreateUser(memberUserId, "member@example.com", "Member User"));
            await db.SaveChangesAsync();
        }

        using var adminClient = factory.CreateAdminApiClient(adminUserId);

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/core/admin/groups",
            new CreateGroupDto
            {
                OrganizationId = organizationId,
                Name = "Editors",
                Description = "Editorial team",
            });

        var createdRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        var createdGroup = DataOrRoot(createdRoot);
        var groupId = createdGroup.GetProperty("id").GetGuid();

        Assert.AreEqual("Editors", createdGroup.GetProperty("name").GetString());
        Assert.AreEqual(organizationId, createdGroup.GetProperty("organizationId").GetGuid());
        Assert.AreEqual(0, createdGroup.GetProperty("memberCount").GetInt32());

        var listResponse = await adminClient.GetAsync($"/api/v1/core/admin/groups?organizationId={organizationId}");
        var listedRoot = await ApiAssert.SuccessAsync(listResponse, HttpStatusCode.OK);
        var listedGroups = DataOrRoot(listedRoot);

        Assert.AreEqual(1, listedGroups.GetArrayLength());
        Assert.AreEqual(groupId, listedGroups[0].GetProperty("id").GetGuid());

        var addMemberResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/core/admin/groups/{groupId}/members",
            new AddGroupMemberDto
            {
                UserId = memberUserId,
            });

        var addedMemberRoot = await ApiAssert.SuccessAsync(addMemberResponse, HttpStatusCode.OK);
        var addedMember = DataOrRoot(addedMemberRoot);

        Assert.AreEqual(memberUserId, addedMember.GetProperty("userId").GetGuid());
        Assert.AreEqual("member@example.com", addedMember.GetProperty("userEmail").GetString());
        Assert.AreEqual(adminUserId, addedMember.GetProperty("addedByUserId").GetGuid());

        var listMembersResponse = await adminClient.GetAsync($"/api/v1/core/admin/groups/{groupId}/members");
        var membersRoot = await ApiAssert.SuccessAsync(listMembersResponse, HttpStatusCode.OK);
        var members = DataOrRoot(membersRoot);

        Assert.AreEqual(1, members.GetArrayLength());
        Assert.AreEqual("Member User", members[0].GetProperty("userDisplayName").GetString());

        var removeMemberResponse = await adminClient.DeleteAsync($"/api/v1/core/admin/groups/{groupId}/members/{memberUserId}");
        await ApiAssert.SuccessAsync(removeMemberResponse, HttpStatusCode.OK);

        var membersAfterRemoveResponse = await adminClient.GetAsync($"/api/v1/core/admin/groups/{groupId}/members");
        var membersAfterRemoveRoot = await ApiAssert.SuccessAsync(membersAfterRemoveResponse, HttpStatusCode.OK);
        Assert.AreEqual(0, DataOrRoot(membersAfterRemoveRoot).GetArrayLength());

        var deleteResponse = await adminClient.DeleteAsync($"/api/v1/core/admin/groups/{groupId}");
        await ApiAssert.SuccessAsync(deleteResponse, HttpStatusCode.OK);
    }

    private static ApplicationUser CreateUser(Guid userId, string email, string displayName)
    {
        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            DisplayName = displayName,
            IsActive = true,
        };
    }

    private static JsonElement DataOrRoot(JsonElement root)
    {
        var current = root;

        while (current.ValueKind == JsonValueKind.Object &&
               current.TryGetProperty("data", out var nested))
        {
            current = nested;
        }

        return current;
    }
}