using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Tests.Host;

/// <summary>
/// Verifies that <see cref="FilesControllerBase.ExecuteAsync"/> maps a
/// <see cref="ZipSizeLimitExceededException"/> to HTTP 413 with the correct
/// error envelope.
/// </summary>
[TestClass]
public class ZipSizeLimitExceededMappingTests
{
    /// <summary>Test subclass exposing the protected <see cref="FilesControllerBase.ExecuteAsync"/>.</summary>
    private sealed class TestFilesController : FilesControllerBase
    {
        public Task<IActionResult> CallExecuteAsync(Func<Task<IActionResult>> action)
            => ExecuteAsync(action);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZipSizeLimitExceeded_MapsTo413()
    {
        var controller = new TestFilesController();

        var result = await controller.CallExecuteAsync(() =>
            throw new ZipSizeLimitExceededException(4_294_967_296L));

        Assert.IsInstanceOfType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);

        // Envelope shape: { success = false, error = { code, message } }
        Assert.IsNotNull(objectResult.Value);
        var error = objectResult.Value!.GetType().GetProperty("error")?.GetValue(objectResult.Value);
        Assert.IsNotNull(error, "Error envelope must include an 'error' object");

        var code = error!.GetType().GetProperty("code")?.GetValue(error) as string;
        var message = error.GetType().GetProperty("message")?.GetValue(error) as string;

        Assert.AreEqual(ErrorCodes.ZipSizeLimitExceeded, code);
        Assert.IsFalse(string.IsNullOrWhiteSpace(message), "Error message must not be empty");
        StringAssert.Contains(message!, "4 GB", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZipSizeLimitExceeded_SmallLimitShowsBytes()
    {
        var controller = new TestFilesController();

        var result = await controller.CallExecuteAsync(() =>
            throw new ZipSizeLimitExceededException(128));

        Assert.IsInstanceOfType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);

        Assert.IsNotNull(objectResult.Value);
        var error = objectResult.Value!.GetType().GetProperty("error")?.GetValue(objectResult.Value);
        Assert.IsNotNull(error);

        var message = error!.GetType().GetProperty("message")?.GetValue(error) as string;
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "128 bytes", StringComparison.OrdinalIgnoreCase);
    }
}
