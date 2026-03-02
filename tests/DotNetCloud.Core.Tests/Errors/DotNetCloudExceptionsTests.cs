namespace DotNetCloud.Core.Tests.Errors;

using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for DotNetCloud exception classes.
/// </summary>
[TestClass]
public class DotNetCloudExceptionsTests
{
    [TestMethod]
    [ExpectedException(typeof(CapabilityNotGrantedException))]
    public void CapabilityNotGrantedException_CanBeThrown()
    {
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);
        throw new CapabilityNotGrantedException("TestCapability", context);
    }

    [TestMethod]
    public void CapabilityNotGrantedException_StoresCapabilityName()
    {
        // Arrange
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);
        var capabilityName = "TestCapability";

        // Act
        var exception = new CapabilityNotGrantedException(capabilityName, context);

        // Assert
        Assert.AreEqual(capabilityName, exception.CapabilityName);
    }

    [TestMethod]
    public void CapabilityNotGrantedException_StoresCallerContext()
    {
        // Arrange
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);

        // Act
        var exception = new CapabilityNotGrantedException("TestCapability", context);

        // Assert
        Assert.AreEqual(context, exception.CallerContext);
    }

    [TestMethod]
    public void CapabilityNotGrantedException_HasCorrectErrorCode()
    {
        // Arrange
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);

        // Act
        var exception = new CapabilityNotGrantedException("TestCapability", context);

        // Assert
        Assert.AreEqual(ErrorCodes.CapabilityNotGranted, exception.ErrorCode);
    }

    [TestMethod]
    public void CapabilityNotGrantedException_HasCorrectMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var context = new CallerContext(userId, Array.Empty<string>(), CallerType.User);

        // Act
        var exception = new CapabilityNotGrantedException("TestCapability", context);

        // Assert
        Assert.IsTrue(exception.Message.Contains("TestCapability"));
        Assert.IsTrue(exception.Message.Contains(userId.ToString()));
    }

    [TestMethod]
    public void ModuleNotFoundException_StoresModuleId()
    {
        // Arrange
        var moduleId = "test.module";

        // Act
        var exception = new ModuleNotFoundException(moduleId);

        // Assert
        Assert.AreEqual(moduleId, exception.ModuleId);
    }

    [TestMethod]
    public void ModuleNotFoundException_HasCorrectErrorCode()
    {
        // Arrange & Act
        var exception = new ModuleNotFoundException("test.module");

        // Assert
        Assert.AreEqual(ErrorCodes.ModuleNotFound, exception.ErrorCode);
    }

    [TestMethod]
    public void ModuleNotFoundException_HasCorrectMessage()
    {
        // Arrange
        var moduleId = "test.module";

        // Act
        var exception = new ModuleNotFoundException(moduleId);

        // Assert
        Assert.IsTrue(exception.Message.Contains(moduleId));
    }

    [TestMethod]
    public void UnauthorizedException_CreatedWithoutResource()
    {
        // Act
        var exception = new UnauthorizedException("Unauthorized access");

        // Assert
        Assert.AreEqual(ErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.IsNull(exception.Resource);
    }

    [TestMethod]
    public void UnauthorizedException_CreatedWithResource()
    {
        // Arrange
        var resource = "AdminPanel";

        // Act
        var exception = new UnauthorizedException("Unauthorized access", resource);

        // Assert
        Assert.AreEqual(ErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.AreEqual(resource, exception.Resource);
    }

    [TestMethod]
    public void UnauthorizedException_HasCorrectMessage()
    {
        // Act
        var exception = new UnauthorizedException("Custom message");

        // Assert
        Assert.AreEqual("Custom message", exception.Message);
    }

    [TestMethod]
    public void DotNetCloudException_Subclasses_InheritBaseProperties()
    {
        // Arrange
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);
        var exception = new CapabilityNotGrantedException("TestCapability", context);

        // Act & Assert
        Assert.IsInstanceOfType(exception, typeof(Exception));
        Assert.IsNotNull(exception.ErrorCode);
        Assert.IsNotNull(exception.Message);
    }

    [TestMethod]
    public void ValidationException_WithDictionary()
    {
        // Arrange
        var errors = new Dictionary<string, IList<string>>
        {
            { "Email", new[] { "Invalid format", "Already in use" } },
            { "Password", new[] { "Too weak" } }
        };

        // Act
        var exception = new ValidationException("Validation failed", errors);

        // Assert
        Assert.AreEqual(2, exception.Errors.Count);
        Assert.IsTrue(exception.Errors.ContainsKey("Email"));
    }

    [TestMethod]
    public void ValidationException_HasCorrectErrorCode()
    {
        // Arrange
        var errors = new Dictionary<string, IList<string>>
        {
            { "Email", new[] { "Invalid" } }
        };

        // Act
        var exception = new ValidationException("Validation failed", errors);

        // Assert
        Assert.AreEqual(ErrorCodes.ValidationError, exception.ErrorCode);
    }

    [TestMethod]
    public void CapabilityNotGrantedException_WithInnerException()
    {
        // Arrange
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);

        // Act
        var exception = new CapabilityNotGrantedException("TestCapability", context);

        // Assert
        Assert.AreEqual(ErrorCodes.CapabilityNotGranted, exception.ErrorCode);
        Assert.AreEqual("TestCapability", exception.CapabilityName);
    }
}
