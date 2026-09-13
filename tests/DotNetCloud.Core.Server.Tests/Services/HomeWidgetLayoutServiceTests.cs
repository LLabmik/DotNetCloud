using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Home;
using DotNetCloud.Core.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="HomeWidgetLayoutService"/>.
/// </summary>
[TestClass]
public sealed class HomeWidgetLayoutServiceTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [TestMethod]
    public async Task EnsureLoadedAsync_NoStoredPreferences_UsesRegistryOrderAndShowsAll()
    {
        using var service = CreateService(Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20)));

        await service.EnsureLoadedAsync();

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, service.StyleToken);
        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat" },
            service.Slots.Select(s => s.ModuleId).ToArray());
        Assert.IsTrue(service.Slots.All(s => s.Visible));
        Assert.AreEqual(2, service.VisibleSlots.Count);
    }

    [TestMethod]
    public async Task EnsureLoadedAsync_Unauthenticated_FallsBackToDefaultsWithoutError()
    {
        using var service = CreateService(
            Registry(("dotnetcloud.files", 10)),
            authStateProvider: AnonymousUser());

        await service.EnsureLoadedAsync();

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, service.StyleToken);
        Assert.AreEqual(1, service.VisibleSlots.Count);
        Assert.IsNull(service.LastError);
    }

    [TestMethod]
    public async Task SetStyleAsync_PersistsTokenAndAppliesIt()
    {
        var settings = new FakeSettingsService();
        using var service = CreateService(Registry(("dotnetcloud.files", 10)), settings);

        var saved = await service.SetStyleAsync(HomeWidgetStyles.HardCopy);

        Assert.IsTrue(saved);
        Assert.AreEqual(HomeWidgetStyles.HardCopy, service.StyleToken);
        StringAssert.Contains(settings.Stored, HomeWidgetStyles.HardCopy);
    }

    [TestMethod]
    public async Task SetStyleAsync_UnknownToken_NormalizesToDefault()
    {
        using var service = CreateService(Registry(("dotnetcloud.files", 10)));

        await service.SetStyleAsync("neon-chaos");

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, service.StyleToken);
    }

    [TestMethod]
    public async Task SetVisibilityAsync_HidesWidgetButKeepsItsSlot()
    {
        using var service = CreateService(Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20)));
        await service.EnsureLoadedAsync();

        await service.SetVisibilityAsync("dotnetcloud.chat", false);

        Assert.AreEqual(2, service.Slots.Count, "hidden widgets must keep their slot");
        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat" },
            service.Slots.Select(s => s.ModuleId).ToArray());
        Assert.IsTrue(service.Slots[0].Visible);
        Assert.IsFalse(service.Slots[1].Visible);
        Assert.AreEqual(1, service.VisibleSlots.Count);
        Assert.AreEqual("dotnetcloud.files", service.VisibleSlots[0].ModuleId);
    }

    [TestMethod]
    public async Task MoveToAsync_ReordersAndPersistsNewOrder()
    {
        var settings = new FakeSettingsService();
        using var service = CreateService(
            Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20), ("dotnetcloud.notes", 30)),
            settings);
        await service.EnsureLoadedAsync();

        await service.MoveToAsync(2, 0);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.notes", "dotnetcloud.files", "dotnetcloud.chat" },
            service.VisibleSlots.Select(s => s.ModuleId).ToArray());

        var moved = settings.Stored!.IndexOf("dotnetcloud.notes", StringComparison.Ordinal);
        var next = settings.Stored.IndexOf("dotnetcloud.files", StringComparison.Ordinal);
        Assert.IsTrue(moved < next, "the persisted payload should lead with the moved widget");
    }

    [TestMethod]
    public async Task MoveAsync_AtTopBoundary_DoesNotChangeOrder()
    {
        using var service = CreateService(Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20)));
        await service.EnsureLoadedAsync();

        await service.MoveAsync("dotnetcloud.files", -1);

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat" },
            service.Slots.Select(s => s.ModuleId).ToArray());
    }

    [TestMethod]
    public async Task ResetAsync_RestoresDefaultStyleAndFullVisibility()
    {
        using var service = CreateService(Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20)));
        await service.EnsureLoadedAsync();
        await service.SetStyleAsync(HomeWidgetStyles.StrictlyBusiness);
        await service.SetVisibilityAsync("dotnetcloud.files", false);

        await service.ResetAsync();

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, service.StyleToken);
        Assert.AreEqual(2, service.VisibleSlots.Count);
        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat" },
            service.VisibleSlots.Select(s => s.ModuleId).ToArray());
    }

    [TestMethod]
    public async Task SetStyleAsync_SaveFails_KeepsOptimisticStateAndReportsError()
    {
        var settings = new FakeSettingsService { FailWrites = true };
        using var service = CreateService(Registry(("dotnetcloud.files", 10)), settings);

        var saved = await service.SetStyleAsync(HomeWidgetStyles.HardCopy);

        Assert.IsFalse(saved);
        Assert.AreEqual(HomeWidgetStyles.HardCopy, service.StyleToken, "the change stays applied optimistically");
        Assert.IsNotNull(service.LastError);
    }

    [TestMethod]
    public async Task ClearError_AfterFailedSave_RemovesTheMessage()
    {
        var settings = new FakeSettingsService { FailWrites = true };
        using var service = CreateService(Registry(("dotnetcloud.files", 10)), settings);
        await service.SetStyleAsync(HomeWidgetStyles.HardCopy);

        service.ClearError();

        Assert.IsNull(service.LastError);
    }

    [TestMethod]
    public async Task RefreshFromRegistry_NewlyRegisteredWidget_AppearsAfterExistingOnes()
    {
        var registry = Registry(("dotnetcloud.files", 10));
        using var service = CreateService(registry);
        await service.EnsureLoadedAsync();

        registry.RegisterWidget("dotnetcloud.chat", "Chat", "widgets", "/apps/chat", typeof(string), 20);
        service.RefreshFromRegistry();

        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.files", "dotnetcloud.chat" },
            service.Slots.Select(s => s.ModuleId).ToArray());
    }

    [TestMethod]
    public async Task RefreshFromRegistry_UnregisteredWidget_Disappears()
    {
        var registry = Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20));
        using var service = CreateService(registry);
        await service.EnsureLoadedAsync();

        registry.UnregisterModule("dotnetcloud.chat");
        service.RefreshFromRegistry();

        Assert.AreEqual(1, service.Slots.Count);
        Assert.AreEqual("dotnetcloud.files", service.Slots[0].ModuleId);
    }

    [TestMethod]
    public async Task StyleRoundTrip_PersistedThenReloaded_KeepsStyleAndOrder()
    {
        var settings = new FakeSettingsService();
        using (var first = CreateService(Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20)), settings))
        {
            await first.EnsureLoadedAsync();
            await first.SetStyleAsync(HomeWidgetStyles.HardCopy);
            await first.MoveToAsync(1, 0);
            await first.SetVisibilityAsync("dotnetcloud.files", false);
        }

        // A fresh circuit (new service instance) must observe the persisted layout.
        using var second = CreateService(Registry(("dotnetcloud.files", 10), ("dotnetcloud.chat", 20)), settings);
        await second.EnsureLoadedAsync();

        Assert.AreEqual(HomeWidgetStyles.HardCopy, second.StyleToken);
        CollectionAssert.AreEqual(
            new[] { "dotnetcloud.chat", "dotnetcloud.files" },
            second.Slots.Select(s => s.ModuleId).ToArray());
        Assert.AreEqual(1, second.VisibleSlots.Count);
        Assert.AreEqual("dotnetcloud.chat", second.VisibleSlots[0].ModuleId);
    }

    // ---------- helpers ----------

    private static WidgetUiRegistry Registry(params (string ModuleId, int SortOrder)[] widgets)
    {
        var registry = new WidgetUiRegistry();
        foreach (var (moduleId, sortOrder) in widgets)
        {
            registry.RegisterWidget(moduleId, moduleId, "widgets", "/apps/" + moduleId, typeof(string), sortOrder);
        }

        return registry;
    }

    private static AuthenticationStateProvider SignedInUser()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "test");
        return new StubAuthenticationStateProvider(new ClaimsPrincipal(identity));
    }

    private static AuthenticationStateProvider AnonymousUser()
        => new StubAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));

    private static HomeWidgetLayoutService CreateService(
        WidgetUiRegistry registry,
        FakeSettingsService? settings = null,
        AuthenticationStateProvider? authStateProvider = null)
        => new(
            registry,
            settings ?? new FakeSettingsService(),
            authStateProvider ?? SignedInUser(),
            NullLogger<HomeWidgetLayoutService>.Instance);

    /// <summary>Minimal in-memory user settings store.</summary>
    private sealed class FakeSettingsService : IUserSettingsService
    {
        private string? _stored;

        /// <summary>Gets or sets a value indicating whether writes should throw.</summary>
        public bool FailWrites { get; set; }

        /// <summary>Gets the last written payload.</summary>
        public string? Stored => _stored;

        /// <inheritdoc />
        public Task<UserSettingDto?> GetSettingAsync(Guid userId, string module, string key)
            => Task.FromResult(_stored is null ? null : new UserSettingDto { Value = _stored });

        /// <inheritdoc />
        public Task<UserSettingDto> UpsertSettingAsync(Guid userId, string module, string key, UpsertUserSettingDto dto)
        {
            if (FailWrites)
            {
                throw new InvalidOperationException("database offline");
            }

            _stored = dto.Value;
            return Task.FromResult(new UserSettingDto { Value = dto.Value });
        }
    }

    private sealed class StubAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(user));
    }
}
