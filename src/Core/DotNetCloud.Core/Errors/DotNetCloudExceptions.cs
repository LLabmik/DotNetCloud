namespace DotNetCloud.Core.Errors;

using DotNetCloud.Core.Authorization;

/// <summary>
/// Base exception class for DotNetCloud-specific exceptions.
/// </summary>
public abstract class DotNetCloudException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetCloudException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    protected DotNetCloudException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetCloudException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected DotNetCloudException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when a required capability is not granted to a module or user.
/// </summary>
public class CapabilityNotGrantedException : DotNetCloudException
{
    /// <summary>
    /// Gets the capability name that was not granted.
    /// </summary>
    public string CapabilityName { get; }

    /// <summary>
    /// Gets the caller context that attempted to use the capability.
    /// </summary>
    public CallerContext CallerContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityNotGrantedException"/> class.
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <param name="callerContext">The caller context.</param>
    public CapabilityNotGrantedException(string capabilityName, CallerContext callerContext)
        : base(
            ErrorCodes.CapabilityNotGranted,
            $"Capability '{capabilityName}' is not granted to {callerContext.Type} '{callerContext.UserId}'.")
    {
        CapabilityName = capabilityName;
        CallerContext = callerContext;
    }
}

/// <summary>
/// Exception thrown when a module cannot be found.
/// </summary>
public class ModuleNotFoundException : DotNetCloudException
{
    /// <summary>
    /// Gets the module ID that was not found.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleNotFoundException"/> class.
    /// </summary>
    /// <param name="moduleId">The module ID.</param>
    public ModuleNotFoundException(string moduleId)
        : base(
            ErrorCodes.ModuleNotFound,
            $"Module '{moduleId}' was not found.")
    {
        ModuleId = moduleId;
    }
}

/// <summary>
/// Exception thrown when an operation is unauthorized.
/// </summary>
public class UnauthorizedException : DotNetCloudException
{
    /// <summary>
    /// Gets the resource that required authorization.
    /// </summary>
    public string? Resource { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UnauthorizedException(string message)
        : base(ErrorCodes.Unauthorized, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resource">The resource that required authorization.</param>
    public UnauthorizedException(string message, string resource)
        : base(ErrorCodes.Unauthorized, message)
    {
        Resource = resource;
    }
}

/// <summary>
/// Exception thrown when a validation error occurs.
/// </summary>
public class ValidationException : DotNetCloudException
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyDictionary<string, IList<string>> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errors">The validation errors.</param>
    public ValidationException(string message, IReadOnlyDictionary<string, IList<string>> errors)
        : base(ErrorCodes.ValidationError, message)
    {
        Errors = errors;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationException(IReadOnlyDictionary<string, IList<string>> errors)
        : base(
            ErrorCodes.ValidationError,
            "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="fieldName">The field name that failed validation.</param>
    /// <param name="errorMessage">The validation error message.</param>
    public ValidationException(string fieldName, string errorMessage)
        : base(
            ErrorCodes.ValidationError,
            $"Validation failed: {fieldName}. {errorMessage}")
    {
        Errors = new Dictionary<string, IList<string>>
        {
            { fieldName, new List<string> { errorMessage } }
        };
    }
}

/// <summary>
/// Exception thrown when a forbidden operation is attempted.
/// </summary>
public class ForbiddenException : DotNetCloudException
{
    /// <summary>
    /// Gets the resource that was forbidden.
    /// </summary>
    public string? Resource { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ForbiddenException(string message)
        : base(ErrorCodes.Forbidden, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resource">The resource that was forbidden.</param>
    public ForbiddenException(string message, string resource)
        : base(ErrorCodes.Forbidden, message)
    {
        Resource = resource;
    }
}

/// <summary>
/// Exception thrown when a resource is not found.
/// </summary>
public class NotFoundException : DotNetCloudException
{
    /// <summary>
    /// Gets the resource type that was not found.
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Gets the resource identifier.
    /// </summary>
    public object? ResourceId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="resourceId">The resource identifier.</param>
    public NotFoundException(string resourceType, object resourceId)
        : base(
            ErrorCodes.NotFound,
            $"{resourceType} with ID '{resourceId}' was not found.")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NotFoundException(string message)
        : base(ErrorCodes.NotFound, message)
    {
        ResourceType = "Resource";
        ResourceId = null;
    }
}

/// <summary>
/// Exception thrown when a concurrency conflict occurs.
/// </summary>
public class ConcurrencyException : DotNetCloudException
{
    /// <summary>
    /// Gets the resource that had a concurrency conflict.
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    public ConcurrencyException(string resourceType)
        : base(
            ErrorCodes.ConcurrencyConflict,
            $"The {resourceType} was modified by another user or process. Please refresh and try again.")
    {
        ResourceType = resourceType;
    }
}

/// <summary>
/// Exception thrown when an invalid operation is attempted.
/// </summary>
public class InvalidOperationException : DotNetCloudException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidOperationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidOperationException(string message)
        : base(ErrorCodes.InvalidOperation, message)
    {
    }
}
