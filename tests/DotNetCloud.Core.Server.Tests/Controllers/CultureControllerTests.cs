using DotNetCloud.Core.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
public class CultureControllerTests
{
    private CultureController _controller = null!;
    private DefaultHttpContext _httpContext = null!;

    [TestInitialize]
    public void Setup()
    {
        _httpContext = new DefaultHttpContext();
        _controller = new CultureController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    [TestMethod]
    public void Set_WithValidCulture_SetsLocalizationCookie()
    {
        // Act
        _controller.Set("es-ES", "/dashboard");

        // Assert
        Assert.IsTrue(
            _httpContext.Response.Headers.SetCookie
                .Any(c => c != null && c.Contains(CookieRequestCultureProvider.DefaultCookieName)));
    }

    [TestMethod]
    public void Set_WithValidCulture_CookieContainsCultureValue()
    {
        // Act
        _controller.Set("de-DE", "/");

        // Assert
        var cookie = _httpContext.Response.Headers.SetCookie
            .FirstOrDefault(c => c != null && c.Contains(CookieRequestCultureProvider.DefaultCookieName));
        Assert.IsNotNull(cookie);
        Assert.IsTrue(cookie.Contains("de-DE"));
    }

    [TestMethod]
    public void Set_WithValidCulture_ReturnsLocalRedirect()
    {
        // Act
        var result = _controller.Set("fr-FR", "/settings");

        // Assert
        Assert.IsInstanceOfType<LocalRedirectResult>(result);
        var redirect = (LocalRedirectResult)result;
        Assert.AreEqual("/settings", redirect.Url);
    }

    [TestMethod]
    public void Set_WithDifferentRedirectUri_RedirectsToCorrectUri()
    {
        // Act
        var result = _controller.Set("en-US", "/admin/modules");

        // Assert
        var redirect = (LocalRedirectResult)result;
        Assert.AreEqual("/admin/modules", redirect.Url);
    }

    [TestMethod]
    public void Set_WithEmptyCulture_DoesNotSetCookie()
    {
        // Act
        _controller.Set("", "/dashboard");

        // Assert
        var cookies = _httpContext.Response.Headers.SetCookie;
        Assert.IsFalse(
            cookies.Any(c => c != null && c.Contains(CookieRequestCultureProvider.DefaultCookieName)),
            "Cookie should not be set for empty culture");
    }

    [TestMethod]
    public void Set_WithWhitespaceCulture_DoesNotSetCookie()
    {
        // Act
        _controller.Set("   ", "/dashboard");

        // Assert
        var cookies = _httpContext.Response.Headers.SetCookie;
        Assert.IsFalse(
            cookies.Any(c => c != null && c.Contains(CookieRequestCultureProvider.DefaultCookieName)),
            "Cookie should not be set for whitespace culture");
    }

    [TestMethod]
    public void Set_WithNullCulture_DoesNotSetCookie()
    {
        // Act
        _controller.Set(null!, "/dashboard");

        // Assert
        var cookies = _httpContext.Response.Headers.SetCookie;
        Assert.IsFalse(
            cookies.Any(c => c != null && c.Contains(CookieRequestCultureProvider.DefaultCookieName)),
            "Cookie should not be set for null culture");
    }

    [TestMethod]
    public void Set_WithEmptyCulture_StillRedirects()
    {
        // Act
        var result = _controller.Set("", "/dashboard");

        // Assert
        Assert.IsInstanceOfType<LocalRedirectResult>(result);
        var redirect = (LocalRedirectResult)result;
        Assert.AreEqual("/dashboard", redirect.Url);
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("es-ES")]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("pt-BR")]
    [DataRow("ja-JP")]
    [DataRow("zh-CN")]
    public void Set_EachSupportedCulture_SetsCookie(string culture)
    {
        // Act
        _controller.Set(culture, "/");

        // Assert
        var cookie = _httpContext.Response.Headers.SetCookie
            .FirstOrDefault(c => c != null && c.Contains(CookieRequestCultureProvider.DefaultCookieName));
        Assert.IsNotNull(cookie, $"Cookie not set for culture '{culture}'");
        Assert.IsTrue(cookie.Contains(culture), $"Cookie does not contain culture '{culture}'");
    }
}
