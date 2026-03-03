namespace DotNetCloud.Core.Tests.Capabilities;

using DotNetCloud.Core.Capabilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for CapabilityTier enum.
/// </summary>
[TestClass]
public class CapabilityTierTests
{
    [TestMethod]
    public void CapabilityTier_Public_HasCorrectValue()
    {
        // Act
        var publicValue = (int)CapabilityTier.Public;

        // Assert
        Assert.AreEqual(0, publicValue);
    }

    [TestMethod]
    public void CapabilityTier_Restricted_HasCorrectValue()
    {
        // Act
        var restrictedValue = (int)CapabilityTier.Restricted;

        // Assert
        Assert.AreEqual(1, restrictedValue);
    }

    [TestMethod]
    public void CapabilityTier_Privileged_HasCorrectValue()
    {
        // Act
        var privilegedValue = (int)CapabilityTier.Privileged;

        // Assert
        Assert.AreEqual(2, privilegedValue);
    }

    [TestMethod]
    public void CapabilityTier_Forbidden_HasCorrectValue()
    {
        // Act
        var forbiddenValue = (int)CapabilityTier.Forbidden;

        // Assert
        Assert.AreEqual(3, forbiddenValue);
    }

    [TestMethod]
    public void CapabilityTier_AllValuesExist()
    {
        // Act
        var values = System.Enum.GetValues(typeof(CapabilityTier));

        // Assert
        Assert.AreEqual(4, values.Length);
    }

    [TestMethod]
    public void CapabilityTier_ComparePublicAndRestricted()
    {
        // Act
        var comparison = CapabilityTier.Public.CompareTo(CapabilityTier.Restricted);

        // Assert
        Assert.IsTrue(comparison < 0);
    }

    [TestMethod]
    public void CapabilityTier_CompareRestrictedAndPrivileged()
    {
        // Act
        var comparison = CapabilityTier.Restricted.CompareTo(CapabilityTier.Privileged);

        // Assert
        Assert.IsTrue(comparison < 0);
    }

    [TestMethod]
    public void CapabilityTier_ComparePrivilegedAndForbidden()
    {
        // Act
        var comparison = CapabilityTier.Privileged.CompareTo(CapabilityTier.Forbidden);

        // Assert
        Assert.IsTrue(comparison < 0);
    }

    [TestMethod]
    public void CapabilityTier_CanParse_FromString()
    {
        // Act
        var tier = System.Enum.Parse<CapabilityTier>("Public");

        // Assert
        Assert.AreEqual(CapabilityTier.Public, tier);
    }
}
