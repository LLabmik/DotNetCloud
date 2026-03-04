using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Assertion helpers for DotNetCloud API responses that follow the standard envelope format.
/// </summary>
internal static class ApiAssert
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Asserts that the response has the given status code and a success envelope.
    /// </summary>
    public static async Task<JsonElement> SuccessAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus = HttpStatusCode.OK)
    {
        Assert.AreEqual(expectedStatus, response.StatusCode,
            $"Expected {expectedStatus} but got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions);
        Assert.IsNotNull(doc, "Response body should be valid JSON");

        var root = doc.RootElement;

        if (root.TryGetProperty("success", out var successProp))
        {
            Assert.IsTrue(successProp.GetBoolean(), "Envelope 'success' should be true");
        }

        return root;
    }

    /// <summary>
    /// Asserts that the response has the given status code and an error envelope.
    /// </summary>
    public static async Task<JsonElement> ErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedCode = null)
    {
        Assert.AreEqual(expectedStatus, response.StatusCode,
            $"Expected {expectedStatus} but got {response.StatusCode}");

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions);
        Assert.IsNotNull(doc, "Response body should be valid JSON");

        var root = doc.RootElement;

        if (root.TryGetProperty("success", out var successProp))
        {
            Assert.IsFalse(successProp.GetBoolean(), "Envelope 'success' should be false for errors");
        }

        if (expectedCode is not null && root.TryGetProperty("error", out var errorObj))
        {
            if (errorObj.TryGetProperty("code", out var codeProp))
            {
                Assert.AreEqual(expectedCode, codeProp.GetString(),
                    $"Expected error code '{expectedCode}'");
            }
        }

        return root;
    }

    /// <summary>
    /// Asserts the response status code matches, regardless of body format.
    /// </summary>
    public static void StatusCode(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.AreEqual(expected, response.StatusCode,
            $"Expected {expected} but got {response.StatusCode}");
    }

    /// <summary>
    /// Reads the response body as the specified type.
    /// </summary>
    public static async Task<T> ReadAsAsync<T>(HttpResponseMessage response) where T : class
    {
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.IsNotNull(result, $"Failed to deserialize response to {typeof(T).Name}");
        return result;
    }

    /// <summary>
    /// Gets the "data" property from a success envelope and deserializes it.
    /// </summary>
    public static async Task<T> DataAsync<T>(HttpResponseMessage response) where T : class
    {
        var root = await SuccessAsync(response);

        Assert.IsTrue(root.TryGetProperty("data", out var dataProp),
            "Success envelope should contain 'data' property");

        var result = dataProp.Deserialize<T>(JsonOptions);
        Assert.IsNotNull(result, $"Failed to deserialize 'data' to {typeof(T).Name}");
        return result;
    }
}
