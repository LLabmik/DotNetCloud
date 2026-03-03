namespace DotNetCloud.Core.Errors;

/// <summary>
/// Standard error codes used throughout the DotNetCloud application.
/// </summary>
public static class ErrorCodes
{
    // Authentication & Authorization
    /// <summary>Error code for invalid credentials.</summary>
    public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";

    /// <summary>Error code for unauthorized access.</summary>
    public const string Unauthorized = "AUTH_UNAUTHORIZED";

    /// <summary>Error code for forbidden access.</summary>
    public const string Forbidden = "AUTH_FORBIDDEN";

    /// <summary>Error code for token expired.</summary>
    public const string TokenExpired = "AUTH_TOKEN_EXPIRED";

    /// <summary>Error code for invalid token.</summary>
    public const string InvalidToken = "AUTH_INVALID_TOKEN";

    /// <summary>Error code for MFA required.</summary>
    public const string MfaRequired = "AUTH_MFA_REQUIRED";

    /// <summary>Error code for invalid MFA code.</summary>
    public const string InvalidMfaCode = "AUTH_INVALID_MFA_CODE";

    /// <summary>Error code for account locked.</summary>
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";

    /// <summary>Error code for email not confirmed.</summary>
    public const string EmailNotConfirmed = "AUTH_EMAIL_NOT_CONFIRMED";

    // User & Identity
    /// <summary>Error code for user not found.</summary>
    public const string UserNotFound = "USER_NOT_FOUND";

    /// <summary>Error code for user already exists.</summary>
    public const string UserAlreadyExists = "USER_ALREADY_EXISTS";

    /// <summary>Error code for email already in use.</summary>
    public const string EmailAlreadyInUse = "USER_EMAIL_ALREADY_IN_USE";

    /// <summary>Error code for invalid email format.</summary>
    public const string InvalidEmailFormat = "USER_INVALID_EMAIL_FORMAT";

    /// <summary>Error code for weak password.</summary>
    public const string WeakPassword = "USER_WEAK_PASSWORD";

    /// <summary>Error code for password mismatch.</summary>
    public const string PasswordMismatch = "USER_PASSWORD_MISMATCH";

    // Organization & Teams
    /// <summary>Error code for organization not found.</summary>
    public const string OrganizationNotFound = "ORG_NOT_FOUND";

    /// <summary>Error code for organization already exists.</summary>
    public const string OrganizationAlreadyExists = "ORG_ALREADY_EXISTS";

    /// <summary>Error code for team not found.</summary>
    public const string TeamNotFound = "TEAM_NOT_FOUND";

    /// <summary>Error code for team already exists.</summary>
    public const string TeamAlreadyExists = "TEAM_ALREADY_EXISTS";

    /// <summary>Error code for user not a member of team.</summary>
    public const string NotTeamMember = "TEAM_NOT_MEMBER";

    // Capabilities & Permissions
    /// <summary>Error code for capability not granted.</summary>
    public const string CapabilityNotGranted = "CAP_NOT_GRANTED";

    /// <summary>Error code for capability not found.</summary>
    public const string CapabilityNotFound = "CAP_NOT_FOUND";

    /// <summary>Error code for forbidden capability.</summary>
    public const string ForbiddenCapability = "CAP_FORBIDDEN";

    /// <summary>Error code for permission not found.</summary>
    public const string PermissionNotFound = "PERM_NOT_FOUND";

    /// <summary>Error code for role not found.</summary>
    public const string RoleNotFound = "ROLE_NOT_FOUND";

    /// <summary>Error code for role already exists.</summary>
    public const string RoleAlreadyExists = "ROLE_ALREADY_EXISTS";

    /// <summary>Error code for cannot delete system role.</summary>
    public const string CannotDeleteSystemRole = "ROLE_CANNOT_DELETE_SYSTEM";

    // Modules
    /// <summary>Error code for module not found.</summary>
    public const string ModuleNotFound = "MODULE_NOT_FOUND";

    /// <summary>Error code for module already installed.</summary>
    public const string ModuleAlreadyInstalled = "MODULE_ALREADY_INSTALLED";

    /// <summary>Error code for module failed to load.</summary>
    public const string ModuleLoadFailed = "MODULE_LOAD_FAILED";

    /// <summary>Error code for module failed to initialize.</summary>
    public const string ModuleInitFailed = "MODULE_INIT_FAILED";

    /// <summary>Error code for module dependency not satisfied.</summary>
    public const string ModuleDependencyNotSatisfied = "MODULE_DEPENDENCY_NOT_SATISFIED";

    /// <summary>Error code for module version mismatch.</summary>
    public const string ModuleVersionMismatch = "MODULE_VERSION_MISMATCH";

    /// <summary>Error code for invalid module manifest.</summary>
    public const string InvalidModuleManifest = "MODULE_INVALID_MANIFEST";

    // Events
    /// <summary>Error code for event bus error.</summary>
    public const string EventBusError = "EVENT_BUS_ERROR";

    /// <summary>Error code for event handler error.</summary>
    public const string EventHandlerError = "EVENT_HANDLER_ERROR";

    /// <summary>Error code for event subscription failed.</summary>
    public const string EventSubscriptionFailed = "EVENT_SUBSCRIPTION_FAILED";

    // Database & Data
    /// <summary>Error code for database connection failed.</summary>
    public const string DatabaseConnectionFailed = "DB_CONNECTION_FAILED";

    /// <summary>Error code for database error.</summary>
    public const string DatabaseError = "DB_ERROR";

    /// <summary>Error code for entity not found.</summary>
    public const string EntityNotFound = "DB_ENTITY_NOT_FOUND";

    /// <summary>Error code for entity already exists.</summary>
    public const string EntityAlreadyExists = "DB_ENTITY_ALREADY_EXISTS";

    /// <summary>Error code for concurrency conflict.</summary>
    public const string ConcurrencyConflict = "DB_CONCURRENCY_CONFLICT";

    /// <summary>Error code for invalid operation.</summary>
    public const string InvalidOperation = "DB_INVALID_OPERATION";

    // Validation
    /// <summary>Error code for validation error.</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>Error code for required field missing.</summary>
    public const string RequiredFieldMissing = "VALIDATION_REQUIRED_FIELD";

    /// <summary>Error code for invalid format.</summary>
    public const string InvalidFormat = "VALIDATION_INVALID_FORMAT";

    /// <summary>Error code for value out of range.</summary>
    public const string ValueOutOfRange = "VALIDATION_OUT_OF_RANGE";

    // API & HTTP
    /// <summary>Error code for bad request.</summary>
    public const string BadRequest = "HTTP_BAD_REQUEST";

    /// <summary>Error code for not found.</summary>
    public const string NotFound = "HTTP_NOT_FOUND";

    /// <summary>Error code for method not allowed.</summary>
    public const string MethodNotAllowed = "HTTP_METHOD_NOT_ALLOWED";

    /// <summary>Error code for conflict.</summary>
    public const string Conflict = "HTTP_CONFLICT";

    /// <summary>Error code for unsupported media type.</summary>
    public const string UnsupportedMediaType = "HTTP_UNSUPPORTED_MEDIA_TYPE";

    /// <summary>Error code for rate limit exceeded.</summary>
    public const string RateLimitExceeded = "HTTP_RATE_LIMIT_EXCEEDED";

    /// <summary>Error code for internal server error.</summary>
    public const string InternalServerError = "HTTP_INTERNAL_SERVER_ERROR";

    /// <summary>Error code for service unavailable.</summary>
    public const string ServiceUnavailable = "HTTP_SERVICE_UNAVAILABLE";

    /// <summary>Error code for request timeout.</summary>
    public const string RequestTimeout = "HTTP_REQUEST_TIMEOUT";

    // Files & Storage
    /// <summary>Error code for file not found.</summary>
    public const string FileNotFound = "FILE_NOT_FOUND";

    /// <summary>Error code for insufficient storage space.</summary>
    public const string InsufficientStorage = "FILE_INSUFFICIENT_STORAGE";

    /// <summary>Error code for file upload failed.</summary>
    public const string FileUploadFailed = "FILE_UPLOAD_FAILED";

    /// <summary>Error code for file download failed.</summary>
    public const string FileDownloadFailed = "FILE_DOWNLOAD_FAILED";

    /// <summary>Error code for invalid file type.</summary>
    public const string InvalidFileType = "FILE_INVALID_TYPE";

    // Settings
    /// <summary>Error code for setting not found.</summary>
    public const string SettingNotFound = "SETTING_NOT_FOUND";

    /// <summary>Error code for invalid setting value.</summary>
    public const string InvalidSettingValue = "SETTING_INVALID_VALUE";

    // Admin
    /// <summary>Error code for admin password reset failed.</summary>
    public const string AdminPasswordResetFailed = "ADMIN_PASSWORD_RESET_FAILED";

    /// <summary>Error code for user already disabled.</summary>
    public const string UserAlreadyDisabled = "USER_ALREADY_DISABLED";

    /// <summary>Error code for user already enabled.</summary>
    public const string UserAlreadyEnabled = "USER_ALREADY_ENABLED";

    // General
    /// <summary>Error code for unknown error.</summary>
    public const string UnknownError = "UNKNOWN_ERROR";

    /// <summary>Error code for operation not supported.</summary>
    public const string NotSupported = "NOT_SUPPORTED";

    /// <summary>Error code for operation already in progress.</summary>
    public const string AlreadyInProgress = "ALREADY_IN_PROGRESS";

    /// <summary>Error code for operation timed out.</summary>
    public const string OperationTimeout = "OPERATION_TIMEOUT";

    /// <summary>Error code for operation cancelled.</summary>
    public const string OperationCancelled = "OPERATION_CANCELLED";
}
