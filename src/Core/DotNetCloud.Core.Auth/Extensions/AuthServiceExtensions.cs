using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Auth.Configuration;
using DotNetCloud.Core.Auth.Security;
using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Extensions;

/// <summary>
/// Extension methods for registering DotNetCloud authentication and authorization services.
/// </summary>
public static class AuthServiceExtensions
{
    /// <summary>
    /// Registers all DotNetCloud authentication and authorization services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">Application configuration used to bind <see cref="AuthOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    /// <item>ASP.NET Core Identity (custom password policy, lockout, user validation)</item>
    /// <item>OpenIddict core with EF Core store (custom entities, Guid key)</item>
    /// <item>OpenIddict server (token/authorize/logout/userinfo/introspect/revoke endpoints,
    ///       JWT access tokens, standard OIDC scopes, PKCE required for public clients)</item>
    /// <item>OpenIddict validation (local token validation)</item>
    /// <item><see cref="IAuthService"/> and <see cref="IMfaService"/> as scoped services</item>
    /// <item><see cref="IClaimsTransformation"/> for per-request claim enrichment</item>
    /// <item>Authorization policies via <see cref="AuthorizationPolicies"/></item>
    /// <item>Capability implementations: <see cref="IUserDirectory"/>, <see cref="IUserManager"/>,
    ///       <see cref="ICurrentUserContext"/></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddDotNetCloudAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        // -----------------------------------------------------------------
        // Bind options
        // -----------------------------------------------------------------
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));
        var authOptions = config.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? new AuthOptions();

        // -----------------------------------------------------------------
        // ASP.NET Core Identity
        // -----------------------------------------------------------------
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password requirements
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            // Lockout
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            // Email
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<CoreDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            // Blazor UI login route lives under /auth/login, not the Identity default /Account/Login.
            options.LoginPath = "/auth/login";
            options.AccessDeniedPath = "/auth/login";
            options.ReturnUrlParameter = "returnUrl";
        });

        // -----------------------------------------------------------------
        // OpenIddict
        // -----------------------------------------------------------------
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<CoreDbContext>()
                    .ReplaceDefaultEntities<
                        OpenIddictApplication,
                        OpenIddictAuthorization,
                        OpenIddictScope,
                        OpenIddictToken,
                        Guid>();
            })
            .AddServer(options =>
            {
                // Endpoints
                options.SetTokenEndpointUris("/connect/token");
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetUserInfoEndpointUris("/connect/userinfo");
                options.SetIntrospectionEndpointUris("/connect/introspect");
                options.SetRevocationEndpointUris("/connect/revoke");

                // Supported flows
                options.AllowAuthorizationCodeFlow();
                options.AllowRefreshTokenFlow();
                options.AllowClientCredentialsFlow();

                // PKCE required for public clients
                options.RequireProofKeyForCodeExchange();

                // Scopes
                options.RegisterScopes("openid", "profile", "email", "offline_access", "files:read", "files:write");

                // Token lifetimes
                options.SetAccessTokenLifetime(
                    TimeSpan.FromMinutes(authOptions.AccessTokenLifetimeMinutes));
                options.SetRefreshTokenLifetime(
                    TimeSpan.FromDays(authOptions.RefreshTokenLifetimeDays));

                // Persistent RSA keys for token signing and encryption.
                // Keys are stored as PEM files so they survive server restarts.
                var dataRoot = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
                var oidcKeysDir = Path.Combine(
                    !string.IsNullOrWhiteSpace(dataRoot) ? dataRoot : AppContext.BaseDirectory,
                    "oidc-keys");

                using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
                var keyLogger = loggerFactory.CreateLogger("DotNetCloud.OidcKeys");

                var signingKey = OidcKeyManager.LoadOrCreateKey(
                    Path.Combine(oidcKeysDir, "signing-key.pem"), keyLogger);
                var encryptionKey = OidcKeyManager.LoadOrCreateKey(
                    Path.Combine(oidcKeysDir, "encryption-key.pem"), keyLogger);

                options.AddSigningKey(signingKey);
                options.AddEncryptionKey(encryptionKey);

                // Disable access token encryption so clients can read JWT claims directly.
                // Without this, access tokens are JWE (encrypted) and clients cannot decode them.
                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        // -----------------------------------------------------------------
        // Claims transformation
        // -----------------------------------------------------------------
        services.AddMemoryCache();
        services.AddScoped<IClaimsTransformation, DotNetCloudClaimsTransformation>();

        // -----------------------------------------------------------------
        // Authorization policies + handler
        // -----------------------------------------------------------------
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireAuthenticated,
                policy => policy.RequireAuthenticatedUser())
            .AddPolicy(AuthorizationPolicies.RequireAdmin,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement("admin")))
            .AddPolicy(AuthorizationPolicies.RequireFilesRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement("files.read")))
            .AddPolicy(AuthorizationPolicies.RequireFilesWrite,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement("files.write")));

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // -----------------------------------------------------------------
        // Domain services
        // -----------------------------------------------------------------
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();

        // -----------------------------------------------------------------
        // Capability implementations
        // -----------------------------------------------------------------
        services.AddScoped<IUserDirectory, UserDirectoryService>();
        services.AddScoped<ITeamDirectory, TeamDirectoryService>();
        services.AddScoped<ITeamManager, TeamManagerService>();
        services.AddScoped<IUserManager, UserManagerService>();
        services.AddScoped<ICurrentUserContext, CurrentUserContextService>();
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Adds DotNetCloud authentication middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="WebApplication"/> for chaining.</returns>
    /// <remarks>
    /// Must be called after <c>app.UseRouting()</c> and before endpoint mapping.
    /// The actual OAuth2/OIDC endpoint controllers/handlers are registered in Phase 0.7.
    /// </remarks>
    public static WebApplication UseDotNetCloudAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
