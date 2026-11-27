using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// The onboarding tour's state in a frame with no storage: per-page-session only, and never lying about
/// completion.
/// </summary>
[TestClass]
public sealed class BrowserOnboardingStateTests
{
    private const string UserId = "user-1";
    private const string TourId = "tracks";

    private static BrowserOnboardingState CreateState()
        => new(NullLogger<BrowserOnboardingState>.Instance);

    [TestMethod]
    public async Task IsCompletedAsync_FreshState_ReturnsFalse()
    {
        var state = CreateState();

        Assert.IsFalse(await state.IsCompletedAsync(UserId, TourId));
    }

    [TestMethod]
    public async Task MarkCompletedAsync_ThenIsCompleted_ReturnsTrue()
    {
        var state = CreateState();

        await state.MarkCompletedAsync(UserId, TourId);

        Assert.IsTrue(await state.IsCompletedAsync(UserId, TourId));
    }

    [TestMethod]
    public async Task MarkCompletedAsync_ClearsResumeStep()
    {
        var state = CreateState();
        await state.SetStepAsync(UserId, TourId, 4);

        await state.MarkCompletedAsync(UserId, TourId);

        Assert.AreEqual(0, await state.GetCurrentStepAsync(UserId, TourId));
    }

    [TestMethod]
    public async Task ResetAsync_AfterCompletion_ReportsNotCompleted()
    {
        var state = CreateState();
        await state.MarkCompletedAsync(UserId, TourId);

        await state.ResetAsync(UserId, TourId);

        Assert.IsFalse(await state.IsCompletedAsync(UserId, TourId));
    }

    [TestMethod]
    public async Task GetCurrentStepAsync_TracksEachUserSeparately()
    {
        var state = CreateState();

        await state.SetStepAsync(UserId, TourId, 3);

        Assert.AreEqual(3, await state.GetCurrentStepAsync(UserId, TourId));
        Assert.AreEqual(0, await state.GetCurrentStepAsync("other-user", TourId));
    }
}
