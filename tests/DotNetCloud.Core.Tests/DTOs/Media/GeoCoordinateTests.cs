namespace DotNetCloud.Core.Tests.DTOs.Media;

using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="GeoCoordinate"/> record.
/// </summary>
[TestClass]
public class GeoCoordinateTests
{
    [TestMethod]
    public void GeoCoordinate_RequiredProperties_AreSet()
    {
        // Arrange & Act
        var coord = new GeoCoordinate
        {
            Latitude = 48.8566,
            Longitude = 2.3522
        };

        // Assert
        Assert.AreEqual(48.8566, coord.Latitude);
        Assert.AreEqual(2.3522, coord.Longitude);
        Assert.IsNull(coord.AltitudeMetres);
    }

    [TestMethod]
    public void GeoCoordinate_WithAltitude_SetsAllProperties()
    {
        // Arrange & Act
        var coord = new GeoCoordinate
        {
            Latitude = -33.8688,
            Longitude = 151.2093,
            AltitudeMetres = 58.0
        };

        // Assert
        Assert.AreEqual(-33.8688, coord.Latitude);
        Assert.AreEqual(151.2093, coord.Longitude);
        Assert.AreEqual(58.0, coord.AltitudeMetres);
    }

    [TestMethod]
    public void GeoCoordinate_EqualsOperator_WorksForRecords()
    {
        // Arrange
        var coord1 = new GeoCoordinate { Latitude = 51.5074, Longitude = -0.1278 };
        var coord2 = new GeoCoordinate { Latitude = 51.5074, Longitude = -0.1278 };

        // Act & Assert
        Assert.AreEqual(coord1, coord2);
    }

    [TestMethod]
    public void GeoCoordinate_NotEqual_WhenDifferent()
    {
        // Arrange
        var coord1 = new GeoCoordinate { Latitude = 51.5074, Longitude = -0.1278 };
        var coord2 = new GeoCoordinate { Latitude = 40.7128, Longitude = -74.0060 };

        // Act & Assert
        Assert.AreNotEqual(coord1, coord2);
    }

    [TestMethod]
    public void GeoCoordinate_NegativeLatitude_SouthernHemisphere()
    {
        // Arrange & Act
        var coord = new GeoCoordinate { Latitude = -90.0, Longitude = 0.0 };

        // Assert
        Assert.AreEqual(-90.0, coord.Latitude);
    }

    [TestMethod]
    public void GeoCoordinate_NegativeLongitude_WesternHemisphere()
    {
        // Arrange & Act
        var coord = new GeoCoordinate { Latitude = 0.0, Longitude = -180.0 };

        // Assert
        Assert.AreEqual(-180.0, coord.Longitude);
    }

    [TestMethod]
    public void GeoCoordinate_NegativeAltitude_BelowSeaLevel()
    {
        // Arrange & Act (Dead Sea)
        var coord = new GeoCoordinate
        {
            Latitude = 31.5,
            Longitude = 35.5,
            AltitudeMetres = -430.0
        };

        // Assert
        Assert.AreEqual(-430.0, coord.AltitudeMetres);
    }
}
