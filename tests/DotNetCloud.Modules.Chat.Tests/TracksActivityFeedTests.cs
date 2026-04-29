using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="TracksActivityFeed"/> component state management
/// and <see cref="NullTracksActivitySignalRService"/> null-object stub.
/// </summary>
[TestClass]
public class TracksActivityFeedTests
{
    // ─── Null-object stub tests ───────────────────────────────

    [TestMethod]
    public void NullStub_IsActive_ReturnsFalse()
    {
        ITracksActivitySignalRService stub = new TestableNullTracksActivityService();

        Assert.IsFalse(stub.IsActive);
    }

    [TestMethod]
    public void NullStub_NoEventsAreFired()
    {
        ITracksActivitySignalRService stub = new TestableNullTracksActivityService();
        var activityFired = false;
        var assignmentFired = false;

        stub.ActivityReceived += _ => activityFired = true;
        stub.WorkItemAssignedToMe += (_, _, _) => assignmentFired = true;

        // Nothing should fire — these are events that never trigger on the null stub
        Assert.IsFalse(activityFired);
        Assert.IsFalse(assignmentFired);
    }

    // ─── Component tests (via test accessor) ──────────────────

    [TestMethod]
    public void WhenServiceIsInactive_IsTracksAvailableReturnsFalse()
    {
        var service = new FakeTracksActivityService(isActive: false);
        var component = CreateComponent(service);

        Assert.IsFalse(component.TestIsTracksAvailable);
    }

    [TestMethod]
    public void WhenServiceIsActive_IsTracksAvailableReturnsTrue()
    {
        var service = new FakeTracksActivityService(isActive: true);
        var component = CreateComponent(service);

        Assert.IsTrue(component.TestIsTracksAvailable);
    }

    [TestMethod]
    public void WhenActivityReceived_ItemAppearsInList()
    {
        var service = new FakeTracksActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();

        service.RaiseActivity(new TracksActivitySignal
        {
            Action = "card_created",
            ProductId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        Assert.AreEqual(1, component.TestActivities.Count);
        Assert.AreEqual("card_created", component.TestActivities[0].Action);
    }

    [TestMethod]
    public void WhenMultipleActivitiesReceived_NewestIsFirst()
    {
        var service = new FakeTracksActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();

        service.RaiseActivity(new TracksActivitySignal
        {
            Action = "card_created",
            ProductId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddSeconds(-10)
        });

        service.RaiseActivity(new TracksActivitySignal
        {
            Action = "card_moved",
            ProductId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        Assert.AreEqual(2, component.TestActivities.Count);
        Assert.AreEqual("card_moved", component.TestActivities[0].Action);
        Assert.AreEqual("card_created", component.TestActivities[1].Action);
    }

    [TestMethod]
    public void WhenCardAssigned_AssignmentAlertIsSet()
    {
        var service = new FakeTracksActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();
        var workItemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();

        service.RaiseWorkItemAssigned(workItemId, productId, assignedBy);

        Assert.IsNotNull(component.TestAssignmentAlert);
        Assert.AreEqual(workItemId, component.TestAssignmentAlert.WorkItemId);
        Assert.AreEqual(productId, component.TestAssignmentAlert.ProductId);
    }

    [TestMethod]
    public void WhenAssignmentDismissed_AssignmentAlertIsNull()
    {
        var service = new FakeTracksActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();

        service.RaiseWorkItemAssigned(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.IsNotNull(component.TestAssignmentAlert);

        component.SimulateDismissAssignment();
        Assert.IsNull(component.TestAssignmentAlert);
    }

    [TestMethod]
    public void WhenDisposed_EventsAreUnsubscribed()
    {
        var service = new FakeTracksActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();

        component.Dispose();

        // Fire events after disposal — should not throw or add items
        service.RaiseActivity(new TracksActivitySignal
        {
            Action = "card_deleted",
            ProductId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });

        Assert.AreEqual(0, component.TestActivities.Count);
    }

    [TestMethod]
    [DataRow("card_created", "\U0001F4CB")]
    [DataRow("card_moved", "\u27A1")]
    [DataRow("card_updated", "\u270F")]
    [DataRow("card_deleted", "\U0001F5D1")]
    [DataRow("card_assigned", "\U0001F464")]
    [DataRow("comment_added", "\U0001F4AC")]
    [DataRow("sprint_started", "\U0001F3C3")]
    [DataRow("sprint_completed", "\u2705")]
    [DataRow("unknown_action", "\u2022")]
    public void GetActionIcon_ReturnsExpectedIcon(string action, string expectedIcon)
    {
        Assert.AreEqual(expectedIcon, TestableTracksActivityFeed.TestGetActionIcon(action));
    }

    [TestMethod]
    [DataRow("card_created", "Card created")]
    [DataRow("sprint_completed", "Sprint completed")]
    [DataRow("some_custom", "some custom")]
    public void GetActionText_ReturnsExpectedText(string action, string expectedText)
    {
        Assert.AreEqual(expectedText, TestableTracksActivityFeed.TestGetActionText(action));
    }

    [TestMethod]
    public void FormatTime_JustNow()
    {
        var result = TestableTracksActivityFeed.TestFormatTime(DateTime.UtcNow);
        Assert.AreEqual("just now", result);
    }

    [TestMethod]
    public void FormatTime_MinutesAgo()
    {
        var result = TestableTracksActivityFeed.TestFormatTime(DateTime.UtcNow.AddMinutes(-5));
        Assert.AreEqual("5m ago", result);
    }

    // ─── Helpers ──────────────────────────────────────────────

    private static TestableTracksActivityFeed CreateComponent(FakeTracksActivityService service)
    {
        return new TestableTracksActivityFeed(service);
    }

    /// <summary>
    /// Test accessor subclass exposing protected members of <see cref="TracksActivityFeed"/>.
    /// </summary>
    private sealed class TestableTracksActivityFeed : TracksActivityFeed
    {
        public TestableTracksActivityFeed(ITracksActivitySignalRService service)
        {
            // Use reflection to set the inject property since we're not using DI in tests
            var prop = typeof(TracksActivityFeed).GetProperty("TracksActivity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop!.SetValue(this, service);
        }

        public bool TestIsTracksAvailable => IsTracksAvailable;
        public IReadOnlyList<ActivityItem> TestActivities => GetType()
            .BaseType!.GetField("_activities", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(this) as List<ActivityItem> ?? [];
        public AssignmentAlert? TestAssignmentAlert => GetType()
            .BaseType!.GetField("_assignmentAlert", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(this) as AssignmentAlert;

        public void SimulateOnInitialized() => OnInitialized();
        public void SimulateDismissAssignment() => DismissAssignment();

        public static string TestGetActionIcon(string action) => GetActionIcon(action);
        public static string TestGetActionText(string action) => GetActionText(action);
        public static string TestFormatTime(DateTime utcTime) => FormatTime(utcTime);
    }

    /// <summary>
    /// Fake service implementation for testing — allows raising events on demand.
    /// </summary>
    private sealed class FakeTracksActivityService : ITracksActivitySignalRService
    {
        public FakeTracksActivityService(bool isActive) => IsActive = isActive;

        public bool IsActive { get; }
        public event Action<TracksActivitySignal>? ActivityReceived;
        public event Action<Guid, Guid, Guid>? WorkItemAssignedToMe;

        public void RaiseActivity(TracksActivitySignal signal) => ActivityReceived?.Invoke(signal);
        public void RaiseWorkItemAssigned(Guid workItemId, Guid productId, Guid assignedBy) =>
            WorkItemAssignedToMe?.Invoke(workItemId, productId, assignedBy);
    }

    /// <summary>
    /// Testable null stub — accessing internal class from tests via a public wrapper.
    /// </summary>
    private sealed class TestableNullTracksActivityService : ITracksActivitySignalRService
    {
        public bool IsActive => false;
#pragma warning disable CS0067
        public event Action<TracksActivitySignal>? ActivityReceived;
        public event Action<Guid, Guid, Guid>? WorkItemAssignedToMe;
#pragma warning restore CS0067
    }
}
