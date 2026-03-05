using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Tests for <see cref="CapabilityValidationResult"/>.
/// </summary>
[TestClass]
public class CapabilityValidationResultTests
{
    [TestMethod]
    public void Success_WithNoForbiddenOrUnknown_IsValid()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: ["IUserDirectory", "IEventBus"],
            pending: [],
            forbidden: [],
            unknown: []);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(2, result.GrantedCapabilities.Count);
    }

    [TestMethod]
    public void Success_WithForbiddenCapabilities_IsNotValid()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: ["IUserDirectory"],
            pending: [],
            forbidden: ["CoreDbContext"],
            unknown: []);

        // Assert
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Success_WithUnknownCapabilities_IsNotValid()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: ["IUserDirectory"],
            pending: [],
            forbidden: [],
            unknown: ["ISomethingUndefined"]);

        // Assert
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void CanStart_WhenValidAndNoForbiddenAndNoPending_ReturnsTrue()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: ["IUserDirectory"],
            pending: [],
            forbidden: [],
            unknown: []);

        // Assert
        Assert.IsTrue(result.CanStart);
    }

    [TestMethod]
    public void CanStart_WhenPendingCapabilitiesExist_ReturnsFalse()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: ["IUserDirectory"],
            pending: ["IStorageProvider"],
            forbidden: [],
            unknown: []);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.CanStart);
    }

    [TestMethod]
    public void CanStart_WhenForbiddenCapabilitiesExist_ReturnsFalse()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: [],
            pending: [],
            forbidden: ["CoreDbContext"],
            unknown: []);

        // Assert
        Assert.IsFalse(result.CanStart);
    }

    [TestMethod]
    public void Failure_ReturnsInvalidWithErrors()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Failure("Module manifest missing", "Invalid module ID");

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.CanStart);
        Assert.AreEqual(2, result.Errors.Count);
        Assert.AreEqual("Module manifest missing", result.Errors[0]);
        Assert.AreEqual("Invalid module ID", result.Errors[1]);
    }

    [TestMethod]
    public void Failure_HasEmptyCollectionsForCapabilities()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Failure("Some error");

        // Assert
        Assert.AreEqual(0, result.GrantedCapabilities.Count);
        Assert.AreEqual(0, result.PendingCapabilities.Count);
        Assert.AreEqual(0, result.ForbiddenCapabilities.Count);
        Assert.AreEqual(0, result.UnknownCapabilities.Count);
    }

    [TestMethod]
    public void Success_WithMixedTiers_TracksAllCategories()
    {
        // Arrange & Act
        var result = CapabilityValidationResult.Success(
            granted: ["IUserDirectory", "IEventBus"],
            pending: ["IStorageProvider", "IModuleSettings"],
            forbidden: ["CoreDbContext"],
            unknown: ["ISomethingElse"]);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.CanStart);
        Assert.AreEqual(2, result.GrantedCapabilities.Count);
        Assert.AreEqual(2, result.PendingCapabilities.Count);
        Assert.AreEqual(1, result.ForbiddenCapabilities.Count);
        Assert.AreEqual(1, result.UnknownCapabilities.Count);
    }
}
