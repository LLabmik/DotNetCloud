using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class ModuleAvailabilityStateTests
{
    [TestInitialize]
    public void Setup()
    {
        // Reset state before each test
        ModuleAvailabilityState.SetMusicAvailable(false);
        ModuleAvailabilityState.SetAiAvailable(false);
    }

    [TestMethod]
    public void IsMusicModuleAvailable_DefaultsToFalse()
    {
        Assert.IsFalse(ModuleAvailabilityState.IsMusicModuleAvailable);
    }

    [TestMethod]
    public void IsMusicModuleAvailable_CanBeSetToTrue()
    {
        ModuleAvailabilityState.SetMusicAvailable(true);
        Assert.IsTrue(ModuleAvailabilityState.IsMusicModuleAvailable);
    }

    [TestMethod]
    public void IsMusicModuleAvailable_CanBeToggledBackToFalse()
    {
        ModuleAvailabilityState.SetMusicAvailable(true);
        ModuleAvailabilityState.SetMusicAvailable(false);
        Assert.IsFalse(ModuleAvailabilityState.IsMusicModuleAvailable);
    }

    [TestMethod]
    public void SetMusicAvailable_FiresEvent_WhenChanged()
    {
        var fired = false;
        ModuleAvailabilityState.MusicAvailabilityChanged += () => fired = true;

        ModuleAvailabilityState.SetMusicAvailable(true);

        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void SetMusicAvailable_DoesNotDoubleFire_WithoutSubscriber()
    {
        // Should not throw
        ModuleAvailabilityState.SetMusicAvailable(true);
    }

    // ── AI module ───────────────────────────────────────────────────

    [TestMethod]
    public void IsAiModuleAvailable_DefaultsToFalse()
    {
        Assert.IsFalse(ModuleAvailabilityState.IsAiModuleAvailable);
    }

    [TestMethod]
    public void IsAiModuleAvailable_CanBeSetToTrue()
    {
        ModuleAvailabilityState.SetAiAvailable(true);
        Assert.IsTrue(ModuleAvailabilityState.IsAiModuleAvailable);
    }

    [TestMethod]
    public void IsAiModuleAvailable_CanBeToggledBackToFalse()
    {
        ModuleAvailabilityState.SetAiAvailable(true);
        ModuleAvailabilityState.SetAiAvailable(false);
        Assert.IsFalse(ModuleAvailabilityState.IsAiModuleAvailable);
    }

    [TestMethod]
    public void SetAiAvailable_FiresEvent_WhenChanged()
    {
        var fired = false;
        ModuleAvailabilityState.AiAvailabilityChanged += () => fired = true;

        ModuleAvailabilityState.SetAiAvailable(true);

        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void SetAiAvailable_UsesAiModuleKey()
    {
        ModuleAvailabilityState.SetAiAvailable(true);
        Assert.IsTrue(ModuleAvailabilityState.IsModuleAvailable("AI"));
    }

    [TestMethod]
    public void SetAiAvailable_DoesNotAffectMusic()
    {
        ModuleAvailabilityState.SetAiAvailable(true);
        Assert.IsFalse(ModuleAvailabilityState.IsMusicModuleAvailable);
    }
}
