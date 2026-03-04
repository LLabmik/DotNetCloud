namespace DotNetCloud.Core.Localization;

/// <summary>
/// Constants for translation keys used throughout the application.
/// Centralizes string keys to avoid magic strings in resource lookups.
/// Usage: <c>@Loc[TranslationKeys.Common.AppName]</c>
/// </summary>
public static class TranslationKeys
{
    /// <summary>Common/shared UI strings.</summary>
    public static class Common
    {
        /// <summary>Application name.</summary>
        public const string AppName = "AppName";
        /// <summary>Save button text.</summary>
        public const string Save = "Save";
        /// <summary>Cancel button text.</summary>
        public const string Cancel = "Cancel";
        /// <summary>Delete button text.</summary>
        public const string Delete = "Delete";
        /// <summary>Edit button text.</summary>
        public const string Edit = "Edit";
        /// <summary>Create button text.</summary>
        public const string Create = "Create";
        /// <summary>Close button text.</summary>
        public const string Close = "Close";
        /// <summary>Confirm button text.</summary>
        public const string Confirm = "Confirm";
        /// <summary>Search placeholder.</summary>
        public const string Search = "Search";
        /// <summary>Loading indicator text.</summary>
        public const string Loading = "Loading";
        /// <summary>No results found message.</summary>
        public const string NoResults = "NoResults";
        /// <summary>Yes text.</summary>
        public const string Yes = "Yes";
        /// <summary>No text.</summary>
        public const string No = "No";
        /// <summary>Submit button text.</summary>
        public const string Submit = "Submit";
        /// <summary>Back navigation text.</summary>
        public const string Back = "Back";
        /// <summary>Next navigation text.</summary>
        public const string Next = "Next";
        /// <summary>Previous navigation text.</summary>
        public const string Previous = "Previous";
        /// <summary>Refresh button text.</summary>
        public const string Refresh = "Refresh";
        /// <summary>Actions column/menu header.</summary>
        public const string Actions = "Actions";
        /// <summary>Settings page title.</summary>
        public const string Settings = "Settings";
        /// <summary>Dashboard page title.</summary>
        public const string Dashboard = "Dashboard";
        /// <summary>Logout action text.</summary>
        public const string Logout = "Logout";
        /// <summary>Select locale label.</summary>
        public const string SelectLocale = "SelectLocale";
    }

    /// <summary>Authentication-related strings.</summary>
    public static class Auth
    {
        /// <summary>Login page title / button.</summary>
        public const string Login = "Auth_Login";
        /// <summary>Register page title / button.</summary>
        public const string Register = "Auth_Register";
        /// <summary>Email label.</summary>
        public const string Email = "Auth_Email";
        /// <summary>Password label.</summary>
        public const string Password = "Auth_Password";
        /// <summary>Confirm password label.</summary>
        public const string ConfirmPassword = "Auth_ConfirmPassword";
        /// <summary>Display name label.</summary>
        public const string DisplayName = "Auth_DisplayName";
        /// <summary>Forgot password link.</summary>
        public const string ForgotPassword = "Auth_ForgotPassword";
        /// <summary>Reset password page title.</summary>
        public const string ResetPassword = "Auth_ResetPassword";
        /// <summary>MFA verification title.</summary>
        public const string MfaVerification = "Auth_MfaVerification";
        /// <summary>Remember me checkbox.</summary>
        public const string RememberMe = "Auth_RememberMe";
    }

    /// <summary>Error message strings.</summary>
    public static class Errors
    {
        /// <summary>Generic unexpected error.</summary>
        public const string UnexpectedError = "Error_Unexpected";
        /// <summary>Not found message.</summary>
        public const string NotFound = "Error_NotFound";
        /// <summary>Unauthorized access message.</summary>
        public const string Unauthorized = "Error_Unauthorized";
        /// <summary>Forbidden access message.</summary>
        public const string Forbidden = "Error_Forbidden";
        /// <summary>Validation failed message.</summary>
        public const string ValidationFailed = "Error_ValidationFailed";
        /// <summary>Connection error message.</summary>
        public const string ConnectionError = "Error_Connection";
        /// <summary>Session expired message.</summary>
        public const string SessionExpired = "Error_SessionExpired";
        /// <summary>Server error message.</summary>
        public const string ServerError = "Error_ServerError";
    }

    /// <summary>Validation message strings.</summary>
    public static class Validation
    {
        /// <summary>Required field message. {0} = field name.</summary>
        public const string Required = "Validation_Required";
        /// <summary>Invalid email format.</summary>
        public const string InvalidEmail = "Validation_InvalidEmail";
        /// <summary>Password too short. {0} = minimum length.</summary>
        public const string PasswordTooShort = "Validation_PasswordTooShort";
        /// <summary>Passwords do not match.</summary>
        public const string PasswordMismatch = "Validation_PasswordMismatch";
        /// <summary>Field exceeds maximum length. {0} = field, {1} = max.</summary>
        public const string MaxLength = "Validation_MaxLength";
        /// <summary>Field is below minimum length. {0} = field, {1} = min.</summary>
        public const string MinLength = "Validation_MinLength";
        /// <summary>Invalid format. {0} = field name.</summary>
        public const string InvalidFormat = "Validation_InvalidFormat";
    }

    /// <summary>Admin section strings.</summary>
    public static class Admin
    {
        /// <summary>Users list title.</summary>
        public const string Users = "Admin_Users";
        /// <summary>Modules list title.</summary>
        public const string Modules = "Admin_Modules";
        /// <summary>System settings title.</summary>
        public const string SystemSettings = "Admin_SystemSettings";
        /// <summary>Health monitoring title.</summary>
        public const string Health = "Admin_Health";
        /// <summary>Start module action.</summary>
        public const string StartModule = "Admin_StartModule";
        /// <summary>Stop module action.</summary>
        public const string StopModule = "Admin_StopModule";
        /// <summary>Restart module action.</summary>
        public const string RestartModule = "Admin_RestartModule";
    }
}
