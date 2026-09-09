namespace DotNetCloud.Core.ServiceDefaults.Middleware;

/// <summary>
/// Builds the Content-Security-Policy header value for the DotNetCloud Blazor Web App.
/// </summary>
/// <remarks>
/// <para>
/// Blazor (server and WebAssembly render modes) requires <c>'wasm-unsafe-eval'</c> to run the
/// Mono WebAssembly runtime, but does <b>not</b> require <c>'unsafe-eval'</c> or <c>'unsafe-inline'</c>
/// on .NET 8+. The app's small set of static inline scripts are therefore allow-listed by SHA-256
/// hash so the policy stays strict:
/// </para>
/// <list type="bullet">
/// <item>Blazor culture bootstrap (<c>window.blazorCulture</c>) in <c>App.razor</c></item>
/// <item>Register page timezone/locale autofill script</item>
/// <item>Change-password success redirect script</item>
/// </list>
/// <para>
/// <c>object-src</c> is locked to <c>'none'</c> and <c>base-uri</c> to <c>'self'</c>. The dynamic
/// inline event handlers that previously existed (MFA shared-key copy, register timezone detect)
/// were converted to Blazor <c>@onclick</c> handlers backed by JS interop so no
/// <c>'unsafe-hashes'</c> allowance is required.
/// </para>
/// </remarks>
public static class CspPolicy
{
    // SHA-256 hashes of the static inline <script> blocks (content between <script> and </script>).
    private const string ScriptHashes =
        "'sha256-hTcoG55CxSil045VWrxfzU4efHtbQdijt+XlUjHFOa0=' " +
        "'sha256-RnuCcxxWUg+bO/ctB+WDXamgM0HHQPXLhk5J0IDXT54=' " +
        "'sha256-JqHvlAUKT6P5m4HK/7n20uRVrQkhMsVXaQ888/G9Qwo='";

    private const string Base =
        "default-src 'self'; " +
        $"script-src 'self' 'wasm-unsafe-eval' {ScriptHashes}; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' ws: wss:; " +
        "media-src 'self' blob:; " +
        "worker-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'; " +
        "upgrade-insecure-requests;";

    /// <summary>
    /// Gets the default policy used when no Collabora origin is configured.
    /// </summary>
    public static string Default => Base;

    /// <summary>
    /// Gets a policy that additionally permits the Collabora office origin to embed the app
    /// in an iframe (frame/child sources). All other directives are identical to <see cref="Default"/>.
    /// </summary>
    /// <param name="collaboraOrigin">The Collabora origin, e.g. <c>https://collabora.example.com</c>.</param>
    /// <returns>The full policy string.</returns>
    public static string WithCollabora(string collaboraOrigin) =>
        Base.Replace(
            "frame-ancestors 'self';",
            $"frame-src 'self' {collaboraOrigin}; child-src 'self' {collaboraOrigin}; frame-ancestors 'self';",
            StringComparison.Ordinal);
}
