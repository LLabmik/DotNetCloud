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

    /// <summary>Error code for case-insensitive name conflict (cross-platform safety).</summary>
    public const string NameConflict = "FILE_NAME_CONFLICT";

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

    // Contacts
    /// <summary>Error code for contact not found.</summary>
    public const string ContactNotFound = "CONTACT_NOT_FOUND";

    /// <summary>Error code for contact already exists (duplicate detection).</summary>
    public const string ContactAlreadyExists = "CONTACT_ALREADY_EXISTS";

    /// <summary>Error code for contact group not found.</summary>
    public const string ContactGroupNotFound = "CONTACT_GROUP_NOT_FOUND";

    /// <summary>Error code for contact group already exists.</summary>
    public const string ContactGroupAlreadyExists = "CONTACT_GROUP_ALREADY_EXISTS";

    /// <summary>Error code for invalid vCard data.</summary>
    public const string InvalidVCardData = "CONTACT_INVALID_VCARD";

    /// <summary>Error code for CardDAV sync token expired or invalid.</summary>
    public const string ContactSyncTokenInvalid = "CONTACT_SYNC_TOKEN_INVALID";

    // Calendar
    /// <summary>Error code for calendar not found.</summary>
    public const string CalendarNotFound = "CALENDAR_NOT_FOUND";

    /// <summary>Error code for calendar already exists.</summary>
    public const string CalendarAlreadyExists = "CALENDAR_ALREADY_EXISTS";

    /// <summary>Error code for calendar event not found.</summary>
    public const string CalendarEventNotFound = "CALENDAR_EVENT_NOT_FOUND";

    /// <summary>Error code for invalid recurrence rule.</summary>
    public const string InvalidRecurrenceRule = "CALENDAR_INVALID_RRULE";

    /// <summary>Error code for invalid event time range (end before start).</summary>
    public const string InvalidEventTimeRange = "CALENDAR_INVALID_TIME_RANGE";

    /// <summary>Error code for invalid iCalendar data.</summary>
    public const string InvalidICalendarData = "CALENDAR_INVALID_ICALENDAR";

    /// <summary>Error code for CalDAV sync token expired or invalid.</summary>
    public const string CalendarSyncTokenInvalid = "CALENDAR_SYNC_TOKEN_INVALID";

    /// <summary>Error code for attendee not found on event.</summary>
    public const string AttendeeNotFound = "CALENDAR_ATTENDEE_NOT_FOUND";

    // Notes
    /// <summary>Error code for note not found.</summary>
    public const string NoteNotFound = "NOTE_NOT_FOUND";

    /// <summary>Error code for note folder not found.</summary>
    public const string NoteFolderNotFound = "NOTE_FOLDER_NOT_FOUND";

    /// <summary>Error code for note folder already exists.</summary>
    public const string NoteFolderAlreadyExists = "NOTE_FOLDER_ALREADY_EXISTS";

    /// <summary>Error code for note version conflict (optimistic concurrency).</summary>
    public const string NoteVersionConflict = "NOTE_VERSION_CONFLICT";

    /// <summary>Error code for note version not found.</summary>
    public const string NoteVersionNotFound = "NOTE_VERSION_NOT_FOUND";

    /// <summary>Error code for unsafe Markdown content detected.</summary>
    public const string NoteUnsafeContent = "NOTE_UNSAFE_CONTENT";

    // Tracks (Project Management)
    /// <summary>Error code for board not found.</summary>
    public const string BoardNotFound = "TRACKS_BOARD_NOT_FOUND";

    /// <summary>Error code for board list not found.</summary>
    public const string BoardListNotFound = "TRACKS_LIST_NOT_FOUND";

    /// <summary>Error code for card not found.</summary>
    public const string CardNotFound = "TRACKS_CARD_NOT_FOUND";

    /// <summary>Error code for label not found.</summary>
    public const string LabelNotFound = "TRACKS_LABEL_NOT_FOUND";

    /// <summary>Error code for sprint not found.</summary>
    public const string SprintNotFound = "TRACKS_SPRINT_NOT_FOUND";

    /// <summary>Error code for comment not found.</summary>
    public const string CommentNotFound = "TRACKS_COMMENT_NOT_FOUND";

    /// <summary>Error code for checklist not found.</summary>
    public const string ChecklistNotFound = "TRACKS_CHECKLIST_NOT_FOUND";

    /// <summary>Error code for time entry not found.</summary>
    public const string TimeEntryNotFound = "TRACKS_TIME_ENTRY_NOT_FOUND";

    /// <summary>Error code for user not a member of the board.</summary>
    public const string NotBoardMember = "TRACKS_NOT_BOARD_MEMBER";

    /// <summary>Error code for insufficient board role.</summary>
    public const string InsufficientBoardRole = "TRACKS_INSUFFICIENT_ROLE";

    /// <summary>Error code for WIP limit exceeded on a list.</summary>
    public const string WipLimitExceeded = "TRACKS_WIP_LIMIT_EXCEEDED";

    /// <summary>Error code for card dependency cycle detected.</summary>
    public const string DependencyCycleDetected = "TRACKS_DEPENDENCY_CYCLE";

    /// <summary>Error code for board already has an active sprint.</summary>
    public const string ActiveSprintExists = "TRACKS_ACTIVE_SPRINT_EXISTS";

    /// <summary>Error code for invalid sprint state transition.</summary>
    public const string InvalidSprintTransition = "TRACKS_INVALID_SPRINT_TRANSITION";

    /// <summary>Error code for invalid time entry (end before start).</summary>
    public const string InvalidTimeEntry = "TRACKS_INVALID_TIME_ENTRY";

    /// <summary>Error code for poker session not found.</summary>
    public const string PokerSessionNotFound = "TRACKS_POKER_SESSION_NOT_FOUND";

    /// <summary>Error code for attempting to vote when session is not in Voting state.</summary>
    public const string PokerSessionNotVoting = "TRACKS_POKER_NOT_VOTING";

    /// <summary>Error code for card already having an active poker session.</summary>
    public const string PokerSessionAlreadyActive = "TRACKS_POKER_ALREADY_ACTIVE";

    /// <summary>Error code for invalid poker estimate value.</summary>
    public const string PokerInvalidEstimate = "TRACKS_POKER_INVALID_ESTIMATE";

    // Import / Migration
    /// <summary>Error code for unsupported import data type.</summary>
    public const string ImportUnsupportedDataType = "IMPORT_UNSUPPORTED_DATA_TYPE";

    /// <summary>Error code for invalid or unparseable import data.</summary>
    public const string ImportInvalidData = "IMPORT_INVALID_DATA";

    /// <summary>Error code for empty import payload.</summary>
    public const string ImportEmptyData = "IMPORT_EMPTY_DATA";

    /// <summary>Error code for import item validation failure.</summary>
    public const string ImportItemValidationFailed = "IMPORT_ITEM_VALIDATION_FAILED";

    /// <summary>Error code for import target container not found.</summary>
    public const string ImportTargetNotFound = "IMPORT_TARGET_NOT_FOUND";

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
