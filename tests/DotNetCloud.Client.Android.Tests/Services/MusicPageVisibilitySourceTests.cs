using System.ComponentModel;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class MusicPageVisibilitySourceTests
{
    [TestInitialize]
    public void Setup()
    {
        ModuleAvailabilityState.SetMusicAvailable(false);
    }

    [TestMethod]
    public void IsMusicModuleAvailable_ReflectsStaticState_WhenFalse()
    {
        var source = new MusicPageVisibilitySource();
        Assert.IsFalse(source.IsMusicModuleAvailable);
    }

    [TestMethod]
    public void IsMusicModuleAvailable_ReflectsStaticState_WhenTrue()
    {
        ModuleAvailabilityState.SetMusicAvailable(true);
        var source = new MusicPageVisibilitySource();
        Assert.IsTrue(source.IsMusicModuleAvailable);
    }

    [TestMethod]
    public void Refresh_RaisesPropertyChanged()
    {
        var source = new MusicPageVisibilitySource();
        var raisedEvents = new List<string>();
        source.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName!);

        source.Refresh();

        Assert.AreEqual(1, raisedEvents.Count);
        Assert.AreEqual(nameof(MusicPageVisibilitySource.IsMusicModuleAvailable), raisedEvents[0]);
    }

    [TestMethod]
    public void PropertyChanged_NotRaisedWithoutSubscriber()
    {
        var source = new MusicPageVisibilitySource();
        // Should not throw
        source.Refresh();
    }
}
