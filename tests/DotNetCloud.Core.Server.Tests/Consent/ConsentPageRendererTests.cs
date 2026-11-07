using DotNetCloud.Core.Server.Consent;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for the consent prompt markup: every dynamic value must be encoded, the page must be a
/// script-free POST form, and it must carry the anti-forgery token.
/// </summary>
[TestClass]
public class ConsentPageRendererTests
{
    private static PendingConsentRequest CreateRequest(params string[] scopes) => new(
        "request-id-123",
        Guid.CreateVersion7(),
        "dnc-module-vendor-receipts",
        "vendor.receipts",
        "Receipt Scanner",
        scopes.Length == 0 ? ["files:read"] : scopes,
        "?client_id=dnc-module-vendor-receipts",
        DateTimeOffset.UtcNow,
        ConsentDecision.Pending);

    [TestMethod]
    public void Render_IncludesEveryScopeDescriptionAndName()
    {
        var html = ConsentPageRenderer.Render(
            CreateRequest("files:read", "profile"),
            "__RequestVerificationToken",
            "token-value",
            "/connect/consent");

        StringAssert.Contains(html, "Read your files and folders.");
        StringAssert.Contains(html, "files:read");
        StringAssert.Contains(html, "Read your name, email address and profile picture.");
        StringAssert.Contains(html, "profile");
    }

    [TestMethod]
    public void Render_EncodesTheClientDisplayName()
    {
        var request = CreateRequest() with { ClientDisplayName = "<script>alert('x')</script>" };

        var html = ConsentPageRenderer.Render(request, "field", "token", "/connect/consent");

        Assert.IsFalse(html.Contains("<script>alert"), "Module-supplied text must never reach the page unencoded.");
        StringAssert.Contains(html, "&lt;script&gt;");
    }

    [TestMethod]
    public void Render_IsAPostFormWithTheAntiforgeryTokenAndPromptId()
    {
        var html = ConsentPageRenderer.Render(CreateRequest(), "__RequestVerificationToken", "token-value", "/connect/consent");

        StringAssert.Contains(html, "method=\"post\"");
        StringAssert.Contains(html, "action=\"/connect/consent\"");
        StringAssert.Contains(html, "name=\"__RequestVerificationToken\" value=\"token-value\"");
        StringAssert.Contains(html, "name=\"rid\" value=\"request-id-123\"");
        StringAssert.Contains(html, "name=\"decision\" value=\"allow\"");
        StringAssert.Contains(html, "name=\"decision\" value=\"deny\"");
    }

    [TestMethod]
    public void Render_ContainsNoScript()
    {
        var html = ConsentPageRenderer.Render(CreateRequest(), "field", "token", "/connect/consent");

        Assert.IsFalse(html.Contains("<script", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("javascript:", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("onclick", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RenderError_EncodesTheMessage()
    {
        var html = ConsentPageRenderer.RenderError("<img src=x onerror=alert(1)>");

        Assert.IsFalse(html.Contains("<img src=x"));
        StringAssert.Contains(html, "&lt;img");
    }
}
