using Xunit;

namespace DotNetCloud.Core.Server.Tests.Endpoints;

/// <summary>
/// Integration tests for OpenIddict protocol endpoints.
/// </summary>
public class OpenIddictEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task TokenEndpoint_IsAccessible()
    {
        // Act
        var response = await Client.PostAsync("/connect/token", null);

        // Assert
        // Endpoint should exist (may fail with bad request due to missing params)
        Assert.True(response.StatusCode != System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeEndpoint_IsAccessible()
    {
        // Act
        var response = await Client.PostAsync("/connect/authorize", null);

        // Assert
        // Endpoint should exist (may redirect to login)
        Assert.True(response.StatusCode != System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LogoutEndpoint_IsAccessible()
    {
        // Act
        var response = await Client.PostAsync("/connect/logout", null);

        // Assert
        // Endpoint should exist
        Assert.True(response.StatusCode != System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeEndpoint_IsAccessible()
    {
        // Act
        var response = await Client.PostAsync("/connect/revoke", null);

        // Assert
        // Endpoint should exist
        Assert.True(response.StatusCode != System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserInfoEndpoint_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/connect/userinfo");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IntrospectEndpoint_IsAccessible()
    {
        // Act
        var response = await Client.PostAsync("/connect/introspect", null);

        // Assert
        // Endpoint should exist (may fail with bad request due to missing params)
        Assert.True(response.StatusCode != System.Net.HttpStatusCode.NotFound);
    }
}
