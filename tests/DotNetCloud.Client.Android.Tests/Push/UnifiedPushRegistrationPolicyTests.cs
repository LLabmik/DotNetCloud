using System.Net;
using DotNetCloud.Client.Android.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Push.Tests;

/// <summary>Covers the UnifiedPush registration state machine and its retry policy.</summary>
[TestClass]
public sealed class UnifiedPushRegistrationPolicyTests
{
    private const string ServerUrl = "https://cloud.example.com";
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static UnifiedPushRegistration NewRegistration() => new()
    {
        ServerBaseUrl = ServerUrl,
        Token = UnifiedPushProtocol.CreateToken(),
    };

    private static UnifiedPushRegistration RegisteredRegistration() => NewRegistration() with
    {
        Endpoint = "https://cloud.example.com/push/upTopic",
        State = UnifiedPushRegistrationState.Registered,
    };

    [TestMethod]
    public void OnRegisterRequested_WithoutToken_MintsOneAndAsksToRegister()
    {
        // Arrange
        var registration = NewRegistration() with { Token = string.Empty };

        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegisterRequested(registration, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.SendRegister, decision.Action);
        Assert.IsTrue(Guid.TryParse(decision.Registration.Token, out _));
        Assert.AreEqual(UnifiedPushRegistrationState.Pending, decision.Registration.State);
    }

    [TestMethod]
    public void OnRegisterRequested_WithKnownEndpoint_ReportsItToTheServerInsteadOfReRegistering()
    {
        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegisterRequested(RegisteredRegistration(), Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.ReportToServer, decision.Action);
    }

    [TestMethod]
    public void OnRegisterRequested_WithinBackoffWindow_DefersTheAttempt()
    {
        // Arrange
        var registration = NewRegistration().WithFailure(UnifiedPushProtocol.ReasonNetwork, Now);

        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegisterRequested(registration, Now.AddSeconds(5));

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.RetryLater, decision.Action);
    }

    [TestMethod]
    public void OnRegisterRequested_AfterBackoffWindow_AttemptsAgain()
    {
        // Arrange
        var registration = NewRegistration().WithFailure(UnifiedPushProtocol.ReasonNetwork, Now);

        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegisterRequested(
            registration, Now.Add(UnifiedPushRetry.NextDelay(1)).AddSeconds(1));

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.SendRegister, decision.Action);
    }

    [TestMethod]
    public void OnRegisterRequested_AfterExhaustingRetries_RequiresTheUser()
    {
        // Arrange
        var registration = NewRegistration() with
        {
            State = UnifiedPushRegistrationState.Failed,
            FailedAttempts = UnifiedPushRetry.MaxAttempts,
        };

        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegisterRequested(registration, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.NeedsUserAction, decision.Action);
    }

    [TestMethod]
    public void OnEndpointReceived_StoresEndpointAndAsksToReportIt()
    {
        // Act
        var decision = UnifiedPushRegistrationPolicy.OnEndpointReceived(
            NewRegistration(), "https://cloud.example.com/push/upTopic", "io.heckel.ntfy", Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.ReportToServer, decision.Action);
        Assert.AreEqual(UnifiedPushRegistrationState.Registered, decision.Registration.State);
        Assert.AreEqual("https://cloud.example.com/push/upTopic", decision.Registration.Endpoint);
        Assert.AreEqual("io.heckel.ntfy", decision.Registration.DistributorPackage);
        Assert.IsNull(decision.Registration.LastReason);
        Assert.AreEqual(0, decision.Registration.FailedAttempts);
    }

    [TestMethod]
    public void ShouldIgnoreFailure_AfterEndpointArrived_IsTrue()
    {
        // Act + Assert
        Assert.IsTrue(UnifiedPushRegistrationPolicy.ShouldIgnoreFailure(RegisteredRegistration()));
        Assert.IsFalse(UnifiedPushRegistrationPolicy.ShouldIgnoreFailure(NewRegistration()));
    }

    [TestMethod]
    public void OnRegistrationFailed_AfterEndpointArrived_IsIgnored()
    {
        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            RegisteredRegistration(), UnifiedPushProtocol.ReasonNetwork, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.None, decision.Action);
        Assert.AreEqual(UnifiedPushRegistrationState.Registered, decision.Registration.State);
    }

    [TestMethod]
    public void OnRegistrationFailed_InternalError_RotatesTokenAndRetriesImmediatelyOnce()
    {
        // Act
        var first = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            NewRegistration(), UnifiedPushProtocol.ReasonInternalError, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.RetryNow, first.Action);
        Assert.AreNotEqual(NewRegistration().Token, first.Registration.Token);
        Assert.IsTrue(Guid.TryParse(first.Registration.Token, out _));
        Assert.AreEqual(UnifiedPushProtocol.ReasonInternalError, first.Registration.LastReason);
        Assert.IsNotNull(first.Registration.NextAttemptAt);

        // A second consecutive failure must not retry immediately again (no ping-pong loop).
        var second = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            first.Registration, UnifiedPushProtocol.ReasonInternalError, Now.AddSeconds(1));

        Assert.AreEqual(UnifiedPushRegistrationAction.RetryLater, second.Action);
        Assert.AreEqual(2, second.Registration.FailedAttempts);
    }

    [TestMethod]
    public void OnRegistrationFailed_NetworkError_WaitsForTheBackoff()
    {
        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            NewRegistration(), UnifiedPushProtocol.ReasonNetwork, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.RetryLater, decision.Action);
        Assert.AreEqual(UnifiedPushRetry.NextDelay(1), decision.Registration.NextAttemptAt - Now);
    }

    [TestMethod]
    public void OnRegistrationFailed_NonRetryableReason_RequiresTheUser()
    {
        // Act
        var actionRequired = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            NewRegistration(), UnifiedPushProtocol.ReasonActionRequired, Now);
        var vapidRequired = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            NewRegistration(), UnifiedPushProtocol.ReasonVapidRequired, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.NeedsUserAction, actionRequired.Action);
        Assert.AreEqual(UnifiedPushProtocol.ReasonActionRequired, actionRequired.Registration.LastReason);
        Assert.AreEqual(UnifiedPushRegistrationAction.NeedsUserAction, vapidRequired.Action);
    }

    [TestMethod]
    public void OnRegistrationFailed_AfterMaxAttempts_RequiresTheUser()
    {
        // Arrange
        var registration = NewRegistration() with
        {
            FailedAttempts = UnifiedPushRetry.MaxAttempts - 1,
            State = UnifiedPushRegistrationState.Failed,
        };

        // Act
        var decision = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
            registration, UnifiedPushProtocol.ReasonNetwork, Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.NeedsUserAction, decision.Action);
        Assert.IsNull(decision.Registration.NextAttemptAt);
    }

    [TestMethod]
    public void OnUnregistered_DropsTheRegistration()
    {
        // Act
        var decision = UnifiedPushRegistrationPolicy.OnUnregistered(RegisteredRegistration(), Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.Drop, decision.Action);
        Assert.AreEqual(UnifiedPushRegistrationState.Unregistered, decision.Registration.State);
        Assert.IsFalse(decision.Registration.HasEndpoint);
        Assert.AreEqual(0, decision.Registration.FailedAttempts);
    }

    [TestMethod]
    public void OnTempUnavailable_RecordsDegradedState()
    {
        // Act
        var decision = UnifiedPushRegistrationPolicy.OnTempUnavailable(RegisteredRegistration(), Now);

        // Assert
        Assert.AreEqual(UnifiedPushRegistrationAction.None, decision.Action);
        Assert.AreEqual(UnifiedPushRegistrationState.TempUnavailable, decision.Registration.State);
    }

    [TestMethod]
    public void RetryPolicy_ClassifiesReasons()
    {
        // Assert
        Assert.IsTrue(UnifiedPushRetry.IsRetryable(null));
        Assert.IsTrue(UnifiedPushRetry.IsRetryable(UnifiedPushProtocol.ReasonNetwork));
        Assert.IsTrue(UnifiedPushRetry.IsRetryable(UnifiedPushProtocol.ReasonInternalError));
        Assert.IsFalse(UnifiedPushRetry.IsRetryable(UnifiedPushProtocol.ReasonActionRequired));

        Assert.IsTrue(UnifiedPushRetry.RequiresUserAction(UnifiedPushProtocol.ReasonActionRequired));
        Assert.IsTrue(UnifiedPushRetry.RequiresUserAction(UnifiedPushProtocol.ReasonVapidRequired));
        Assert.IsFalse(UnifiedPushRetry.RequiresUserAction(UnifiedPushProtocol.ReasonNetwork));
    }

    [TestMethod]
    public void RetryPolicy_BackoffGrowsAndIsCapped()
    {
        // Act + Assert
        Assert.AreEqual(TimeSpan.FromSeconds(30), UnifiedPushRetry.NextDelay(1));
        Assert.IsTrue(UnifiedPushRetry.NextDelay(3) > UnifiedPushRetry.NextDelay(2));
        Assert.AreEqual(TimeSpan.FromMinutes(60), UnifiedPushRetry.NextDelay(99));
        Assert.IsNull(UnifiedPushRetry.NextAttemptAt(UnifiedPushRetry.MaxAttempts, Now));
    }

    [TestMethod]
    public void RetryPolicy_ShouldAttempt_RespectsTheWindowAndTheAttemptCap()
    {
        // Assert
        Assert.IsTrue(UnifiedPushRetry.ShouldAttempt(0, null, Now));
        Assert.IsFalse(UnifiedPushRetry.ShouldAttempt(0, Now.AddMinutes(1), Now));
        Assert.IsTrue(UnifiedPushRetry.ShouldAttempt(0, Now.AddMinutes(-1), Now));
        Assert.IsFalse(UnifiedPushRetry.ShouldAttempt(UnifiedPushRetry.MaxAttempts, null, Now));
    }
}
