namespace DotNetCloud.Core.Tests.DTOs.Media;

using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="MediaType"/> enum.
/// </summary>
[TestClass]
public class MediaTypeTests
{
    [TestMethod]
    public void MediaType_HasExpectedValues()
    {
        // Assert
        Assert.AreEqual(0, (int)MediaType.Photo);
        Assert.AreEqual(1, (int)MediaType.Audio);
        Assert.AreEqual(2, (int)MediaType.Video);
    }

    [TestMethod]
    public void MediaType_HasExactlyThreeValues()
    {
        // Act
        var values = Enum.GetValues<MediaType>();

        // Assert
        Assert.AreEqual(3, values.Length);
    }

    [TestMethod]
    [DataRow(MediaType.Photo, "Photo")]
    [DataRow(MediaType.Audio, "Audio")]
    [DataRow(MediaType.Video, "Video")]
    public void MediaType_ToString_ReturnsExpectedName(MediaType mediaType, string expected)
    {
        // Act & Assert
        Assert.AreEqual(expected, mediaType.ToString());
    }
}
