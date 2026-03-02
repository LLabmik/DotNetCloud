namespace DotNetCloud.Core.Tests.Errors;

using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for ApiErrorResponse class.
/// </summary>
[TestClass]
public class ApiErrorResponseTests
{
    [TestMethod]
    public void ApiErrorResponse_DefaultConstructor_CreatesInstance()
    {
        // Act
        var response = new ApiErrorResponse();

        // Assert
        Assert.IsFalse(response.Success);
        Assert.IsNull(response.Code);
        Assert.IsNull(response.Message);
    }

    [TestMethod]
    public void ApiErrorResponse_ConstructorWithCodeAndMessage()
    {
        // Arrange
        var code = "TEST_ERROR";
        var message = "Test error message";

        // Act
        var response = new ApiErrorResponse(code, message);

        // Assert
        Assert.AreEqual(code, response.Code);
        Assert.AreEqual(message, response.Message);
        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public void ApiErrorResponse_ConstructorWithDetails()
    {
        // Arrange
        var code = "TEST_ERROR";
        var message = "Test error message";
        var details = new { Field = "Email", Error = "Invalid format" };

        // Act
        var response = new ApiErrorResponse(code, message, details);

        // Assert
        Assert.AreEqual(code, response.Code);
        Assert.AreEqual(message, response.Message);
        Assert.AreEqual(details, response.Details);
    }

    [TestMethod]
    public void ApiErrorResponse_Properties_CanBeSet()
    {
        // Arrange
        var response = new ApiErrorResponse();
        var timestamp = DateTime.UtcNow;
        var traceId = "trace-123";

        // Act
        response.Success = false;
        response.Code = "ERROR_CODE";
        response.Message = "Error message";
        response.Path = "/api/v1/test";
        response.Timestamp = timestamp;
        response.TraceId = traceId;

        // Assert
        Assert.IsFalse(response.Success);
        Assert.AreEqual("ERROR_CODE", response.Code);
        Assert.AreEqual("Error message", response.Message);
        Assert.AreEqual("/api/v1/test", response.Path);
        Assert.AreEqual(timestamp, response.Timestamp);
        Assert.AreEqual(traceId, response.TraceId);
    }

    [TestMethod]
    public void ApiErrorResponse_FromException_ConvertsCapabilityNotGrantedException()
    {
        // Arrange
        var context = new CallerContext(Guid.NewGuid(), Array.Empty<string>(), CallerType.User);
        var exception = new CapabilityNotGrantedException("TestCapability", context);
        var traceId = "trace-456";

        // Act
        var response = ApiErrorResponse.FromException(exception, traceId);

        // Assert
        Assert.AreEqual(ErrorCodes.CapabilityNotGranted, response.Code);
        Assert.AreEqual(exception.Message, response.Message);
        Assert.AreEqual(traceId, response.TraceId);
    }

    [TestMethod]
    public void ApiErrorResponse_FromException_ConvertsModuleNotFoundException()
    {
        // Arrange
        var exception = new ModuleNotFoundException("test.module");

        // Act
        var response = ApiErrorResponse.FromException(exception);

        // Assert
        Assert.AreEqual(ErrorCodes.ModuleNotFound, response.Code);
        Assert.AreEqual(exception.Message, response.Message);
    }

    [TestMethod]
    public void ApiErrorResponse_FromException_ConvertsUnauthorizedException()
    {
        // Arrange
        var exception = new UnauthorizedException("Access denied");

        // Act
        var response = ApiErrorResponse.FromException(exception);

        // Assert
        Assert.AreEqual(ErrorCodes.Unauthorized, response.Code);
        Assert.AreEqual("Access denied", response.Message);
    }

    [TestMethod]
    public void ApiErrorResponse_FromException_WithNullTraceId()
    {
        // Arrange
        var exception = new ModuleNotFoundException("test.module");

        // Act
        var response = ApiErrorResponse.FromException(exception, null);

        // Assert
        Assert.IsNull(response.TraceId);
    }

    [TestMethod]
    public void ApiErrorResponse_Timestamp_DefaultsToUtcNow()
    {
        // Arrange & Act
        var response = new ApiErrorResponse();
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.IsTrue(response.Timestamp >= beforeCreation && response.Timestamp <= afterCreation);
    }

    [TestMethod]
    public void ApiErrorResponse_Success_DefaultsToFalse()
    {
        // Arrange & Act
        var response = new ApiErrorResponse();

        // Assert
        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public void ApiErrorResponse_Details_CanBeNull()
    {
        // Arrange & Act
        var response = new ApiErrorResponse("CODE", "Message", null);

        // Assert
        Assert.IsNull(response.Details);
    }

    [TestMethod]
    public void ApiErrorResponse_Details_CanBeComplex()
    {
        // Arrange
        var details = new
        {
            Errors = new[] { "Error1", "Error2" },
            Field = "Username",
            Code = 400
        };

        // Act
        var response = new ApiErrorResponse("CODE", "Message", details);

        // Assert
        Assert.AreEqual(details, response.Details);
    }
}
