using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Tests.Entities.Identity;

/// <summary>
/// Unit tests for the ApplicationUser entity.
/// </summary>
[TestClass]
public class ApplicationUserTests
{
    [TestMethod]
    public void ApplicationUser_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Assert
        Assert.AreEqual("en-US", user.Locale, "Default locale should be en-US");
        Assert.AreEqual("UTC", user.Timezone, "Default timezone should be UTC");
        Assert.IsTrue(user.IsActive, "Default IsActive should be true");
        Assert.IsNotNull(user.CreatedAt, "CreatedAt should be set");
        Assert.IsNull(user.LastLoginAt, "LastLoginAt should be null by default");
        Assert.IsNull(user.AvatarUrl, "AvatarUrl should be null by default");
    }

    [TestMethod]
    public void ApplicationUser_PrimaryKey_IsGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            DisplayName = "Test User",
            UserName = "testuser"
        };

        // Act & Assert
        Assert.AreEqual(userId, user.Id);
        Assert.IsInstanceOfType(user.Id, typeof(Guid));
    }

    [TestMethod]
    public void ApplicationUser_DisplayName_CanBeSet()
    {
        // Arrange
        var displayName = "John Doe";
        var user = new ApplicationUser
        {
            DisplayName = displayName,
            UserName = "johndoe"
        };

        // Act & Assert
        Assert.AreEqual(displayName, user.DisplayName);
    }

    [TestMethod]
    public void ApplicationUser_AvatarUrl_CanBeSetAndCleared()
    {
        // Arrange
        var avatarUrl = "https://example.com/avatar.jpg";
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            AvatarUrl = avatarUrl
        };

        // Act & Assert
        Assert.AreEqual(avatarUrl, user.AvatarUrl);

        // Clear avatar
        user.AvatarUrl = null;
        Assert.IsNull(user.AvatarUrl);
    }

    [TestMethod]
    public void ApplicationUser_Locale_CanBeCustomized()
    {
        // Arrange
        var locale = "fr-FR";
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            Locale = locale
        };

        // Act & Assert
        Assert.AreEqual(locale, user.Locale);
    }

    [TestMethod]
    public void ApplicationUser_Timezone_CanBeCustomized()
    {
        // Arrange
        var timezone = "America/New_York";
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            Timezone = timezone
        };

        // Act & Assert
        Assert.AreEqual(timezone, user.Timezone);
    }

    [TestMethod]
    public void ApplicationUser_LastLoginAt_TracksLoginTime()
    {
        // Arrange
        var loginTime = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            LastLoginAt = loginTime
        };

        // Act & Assert
        Assert.AreEqual(loginTime, user.LastLoginAt);
    }

    [TestMethod]
    public void ApplicationUser_IsActive_CanBeDisabled()
    {
        // Arrange
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            IsActive = false
        };

        // Act & Assert
        Assert.IsFalse(user.IsActive);
    }

    [TestMethod]
    public void ApplicationUser_CreatedAt_TracksCreationTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser"
        };
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        // Act & Assert
        Assert.IsTrue(user.CreatedAt >= beforeCreation, "CreatedAt should be after or equal to beforeCreation");
        Assert.IsTrue(user.CreatedAt <= afterCreation, "CreatedAt should be before or equal to afterCreation");
    }

    [TestMethod]
    public void ApplicationUser_InheritsFromIdentityUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            DisplayName = "Test User",
            UserName = "testuser",
            Email = "test@example.com",
            PhoneNumber = "+1234567890",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };

        // Act & Assert - Check inherited properties
        Assert.AreEqual("testuser", user.UserName);
        Assert.AreEqual("test@example.com", user.Email);
        Assert.AreEqual("+1234567890", user.PhoneNumber);
        Assert.IsTrue(user.EmailConfirmed);
        Assert.IsTrue(user.PhoneNumberConfirmed);
    }

    [TestMethod]
    public void ApplicationUser_AllProperties_CanBeRoundTripped()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var displayName = "John Doe";
        var avatarUrl = "https://example.com/avatar.jpg";
        var locale = "de-DE";
        var timezone = "Europe/Berlin";
        var createdAt = DateTime.UtcNow.AddDays(-30);
        var lastLoginAt = DateTime.UtcNow.AddHours(-2);

        // Act
        var user = new ApplicationUser
        {
            Id = userId,
            DisplayName = displayName,
            UserName = "johndoe",
            Email = "john@example.com",
            AvatarUrl = avatarUrl,
            Locale = locale,
            Timezone = timezone,
            CreatedAt = createdAt,
            LastLoginAt = lastLoginAt,
            IsActive = true
        };

        // Assert
        Assert.AreEqual(userId, user.Id);
        Assert.AreEqual(displayName, user.DisplayName);
        Assert.AreEqual(avatarUrl, user.AvatarUrl);
        Assert.AreEqual(locale, user.Locale);
        Assert.AreEqual(timezone, user.Timezone);
        Assert.AreEqual(createdAt, user.CreatedAt);
        Assert.AreEqual(lastLoginAt, user.LastLoginAt);
        Assert.IsTrue(user.IsActive);
    }
}
