using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Integration.Tests.Builders;

/// <summary>
/// Fluent builder for creating <see cref="RegisterRequest"/> test instances.
/// </summary>
internal sealed class RegisterRequestBuilder
{
    private string _email = $"user-{Guid.NewGuid():N}@test.local";
    private string _password = "TestP@ssw0rd!";
    private string _displayName = "Integration Test User";
    private string _locale = "en-US";
    private string _timezone = "UTC";

    public RegisterRequestBuilder WithEmail(string email) { _email = email; return this; }
    public RegisterRequestBuilder WithPassword(string password) { _password = password; return this; }
    public RegisterRequestBuilder WithDisplayName(string name) { _displayName = name; return this; }
    public RegisterRequestBuilder WithLocale(string locale) { _locale = locale; return this; }
    public RegisterRequestBuilder WithTimezone(string tz) { _timezone = tz; return this; }

    public RegisterRequest Build()
    {
        return new RegisterRequest
        {
            Email = _email,
            Password = _password,
            DisplayName = _displayName,
            Locale = _locale,
            Timezone = _timezone,
        };
    }
}
