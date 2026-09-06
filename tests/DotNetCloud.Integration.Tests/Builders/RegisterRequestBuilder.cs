using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Integration.Tests.Builders;

/// <summary>
/// Fluent builder for creating <see cref="RegisterRequest"/> test instances.
/// </summary>
internal sealed class RegisterRequestBuilder
{
    private string _username = $"user-{Guid.CreateVersion7():N}";
    private string _email = $"user-{Guid.CreateVersion7():N}@test.local";
    private string _password = "TestP@ssw0rd!";
    // Display names must be unique; default to a unique value so multiple
    // registrations in one test class don't trip the duplicate check.
    private string _displayName = $"Integration User {Guid.CreateVersion7():N}";
    private string _locale = "en-US";
    private string _timezone = "UTC";
    private bool _passwordChangeRequired;

    public RegisterRequestBuilder WithUsername(string username) { _username = username; return this; }
    public RegisterRequestBuilder WithEmail(string email) { _email = email; return this; }
    public RegisterRequestBuilder WithPassword(string password) { _password = password; return this; }
    public RegisterRequestBuilder WithDisplayName(string name) { _displayName = name; return this; }
    public RegisterRequestBuilder WithLocale(string locale) { _locale = locale; return this; }
    public RegisterRequestBuilder WithTimezone(string tz) { _timezone = tz; return this; }
    public RegisterRequestBuilder WithPasswordChangeRequired(bool required) { _passwordChangeRequired = required; return this; }

    public RegisterRequest Build()
    {
        return new RegisterRequest
        {
            Username = _username,
            Email = _email,
            Password = _password,
            DisplayName = _displayName,
            Locale = _locale,
            Timezone = _timezone,
            PasswordChangeRequired = _passwordChangeRequired,
        };
    }
}
