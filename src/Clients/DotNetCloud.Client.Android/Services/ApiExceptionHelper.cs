using System.Net;
using System.Net.Http;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Maps raw exceptions from API calls to user-friendly error messages.
/// </summary>
internal static class ApiExceptionHelper
{
    /// <summary>
    /// Returns a user-friendly error message for the given exception.
    /// </summary>
    public static string GetUserFriendlyMessage(Exception ex) => ex switch
    {
        TaskCanceledException or OperationCanceledException =>
            "Unable to reach the server. Please check your connection and try again.",

        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            "Your session has expired. Please log in again from Settings.",

        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            "The requested item was not found on the server.",

        HttpRequestException { StatusCode: HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable } =>
            "The server is temporarily unavailable. Please try again later.",

        HttpRequestException =>
            "A connection error occurred. The server may be temporarily unavailable.",

        InvalidOperationException ioe when ioe.Message.Contains("No active server", StringComparison.OrdinalIgnoreCase) =>
            "Not connected to a server. Please log in first.",

        InvalidOperationException ioe when ioe.Message.Contains("No access token", StringComparison.OrdinalIgnoreCase) =>
            "Your session has expired. Please log in again from Settings.",

        _ => "An unexpected error occurred. Please try again later."
    };
}
