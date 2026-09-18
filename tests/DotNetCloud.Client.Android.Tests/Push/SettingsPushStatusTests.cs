using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Push.Tests;

/// <summary>
/// Covers the one-line push status shown on the Settings card (plan §9.5): every state must tell
/// the user what is happening and, where possible, what to do about it.
/// </summary>
[TestClass]
public sealed class SettingsPushStatusTests
{
    [TestMethod]
    public void DescribePushStatus_NoDistributorInstalled_ExplainsWhatToInstall()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Unregistered, null, null, null, null, InstalledDistributorCount: 0);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "No distributor app installed");
        StringAssert.Contains(text, "ntfy");
    }

    [TestMethod]
    public void DescribePushStatus_RegisteredWithLabel_NamesTheDistributor()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Registered, "io.heckel.ntfy", "ntfy", "cloud.example.com", null, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "On");
        StringAssert.Contains(text, "ntfy");
    }

    [TestMethod]
    public void DescribePushStatus_RegisteredWithoutLabel_StillReportsItIsOn()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Registered, "io.heckel.ntfy", null, "cloud.example.com", null, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "background");
    }

    [TestMethod]
    public void DescribePushStatus_Registered_NeverShowsTheEndpointOrTopic()
    {
        // Arrange: an endpoint is a capability URL, so neither its path nor its topic may be shown.
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Registered, "io.heckel.ntfy", "ntfy", "cloud.example.com", null, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        Assert.IsFalse(text.Contains("/push/", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("upCapabilityTopic", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("cloud.example.com", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DescribePushStatus_SeveralDistributorsAndNoneChosen_AsksTheUserToChoose()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Unregistered, null, null, null, null, InstalledDistributorCount: 2);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "Choose");
    }

    [TestMethod]
    public void DescribePushStatus_Pending_SaysItIsSettingUp()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Pending, "io.heckel.ntfy", "ntfy", null, null, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "Setting up");
    }

    [TestMethod]
    public void DescribePushStatus_TempUnavailable_ReassuresThatItRecovers()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.TempUnavailable, "io.heckel.ntfy", "ntfy", "cloud.example.com", null, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "temporarily unavailable");
    }

    [TestMethod]
    [DataRow(UnifiedPushProtocol.ReasonActionRequired, "attention")]
    [DataRow(UnifiedPushProtocol.ReasonVapidRequired, "choose another distributor")]
    [DataRow(UnifiedPushProtocol.ReasonNetwork, "network")]
    [DataRow(UnifiedPushProtocol.ReasonInternalError, "retry")]
    public void DescribePushStatus_Failures_ExplainTheReason(string reason, string expected)
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Failed, "io.heckel.ntfy", "ntfy", null, reason, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, expected);
    }

    [TestMethod]
    public void DescribePushStatus_UnknownFailureReason_FallsBackToANeutralLine()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Failed, "io.heckel.ntfy", "ntfy", null, "SOMETHING_NEW", 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "not set up");
    }

    [TestMethod]
    public void DescribePushStatus_UnregisteredWithADistributorInstalled_ExplainsRetrying()
    {
        // Arrange
        var status = new UnifiedPushStatus(
            UnifiedPushRegistrationState.Unregistered, "io.heckel.ntfy", "ntfy", null, null, 1);

        // Act
        var text = SettingsViewModel.DescribePushStatus(status);

        // Assert
        StringAssert.Contains(text, "open the app again");
    }

    [TestMethod]
    public void DescribePushStatus_NoStatus_ReportsUnavailableInsteadOfThrowing()
    {
        // Act
        var text = SettingsViewModel.DescribePushStatus(null);

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(text));
    }
}
