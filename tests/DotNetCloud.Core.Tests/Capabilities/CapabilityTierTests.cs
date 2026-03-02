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
        // Assert
        Assert.AreEqual(0, (int)CapabilityTier.Public);
    }

    [TestMethod]
    public void CapabilityTier_Restricted_HasCorrectValue()
    {
        // Assert
        Assert.AreEqual(1, (int)CapabilityTier.Restricted);
    }

    [TestMethod]
    public void CapabilityTier_Privileged_HasCorrectValue()
    {
        // Assert
        Assert.AreEqual(2, (int)CapabilityTier.Privileged);
    }

    [TestMethod]
    public void CapabilityTier_Forbidden_HasCorrectValue()
    {
        // Assert
        Assert.AreEqual(3, (int)CapabilityTier.Forbidden);
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
    public void CapabilityTier_CanCompare_Public_LessThan_Restricted()
    {
        // Assert
        Assert.IsTrue(CapabilityTier.Public < CapabilityTier.Restricted);
    }

    [TestMethod]
    public void CapabilityTier_CanCompare_Restricted_LessThan_Privileged()
    {
        // Assert
        Assert.IsTrue(CapabilityTier.Restricted < CapabilityTier.Privileged);
    }

    [TestMethod]
    public void CapabilityTier_CanCompare_Privileged_LessThan_Forbidden()
    {
        // Assert
        Assert.IsTrue(CapabilityTier.Privileged < CapabilityTier.Forbidden);
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
