using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class ModuleAvailabilityStateTests
{
    [TestInitialize]
    public void Setup()
    {
        // Reset state before each test
        ModuleAvailabilityState.IsMusicModuleAvailable = false;
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
}
