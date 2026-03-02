namespace DotNetCloud.Core.Errors;

/// <summary>
/// Standard API error response model.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the request was successful.
    /// </summary>
    public bool Success { get; set; } = false;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public object? Details { get; set; }

    /// <summary>
    /// Gets or sets the request path that caused the error.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the error.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the trace ID for debugging.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiErrorResponse"/> class.
    /// </summary>
    public ApiErrorResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiErrorResponse"/> class.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    public ApiErrorResponse(string code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiErrorResponse"/> class.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    public ApiErrorResponse(string code, string message, object? details)
        : this(code, message)
    {
        Details = details;
    }

    /// <summary>
    /// Creates an API error response from a <see cref="DotNetCloudException"/>.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="traceId">The request trace ID.</param>
    /// <returns>The API error response.</returns>
    public static ApiErrorResponse FromException(DotNetCloudException exception, string? traceId = null)
    {
        var response = new ApiErrorResponse(exception.ErrorCode, exception.Message)
        {
            TraceId = traceId
        };

        // Add specific details based on exception type
        if (exception is ValidationException validationEx)
        {
            response.Details = validationEx.Errors;
        }
        else if (exception is CapabilityNotGrantedException capabilityEx)
        {
            response.Details = new
            {
                capabilityEx.CapabilityName,
                CallerType = capabilityEx.CallerContext.Type.ToString(),
                capabilityEx.CallerContext.UserId
            };
        }
        else if (exception is ModuleNotFoundException moduleEx)
        {
            response.Details = new { moduleEx.ModuleId };
        }
        else if (exception is NotFoundException notFoundEx)
        {
            response.Details = new
            {
                notFoundEx.ResourceType,
                notFoundEx.ResourceId
            };
        }

        return response;
    }

    /// <summary>
    /// Creates an API error response from a general exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="traceId">The request trace ID.</param>
    /// <param name="includeStackTrace">Whether to include the stack trace.</param>
    /// <returns>The API error response.</returns>
    public static ApiErrorResponse FromException(Exception exception, string? traceId = null, bool includeStackTrace = false)
    {
        var response = new ApiErrorResponse(
            ErrorCodes.InternalServerError,
            exception.Message)
        {
            TraceId = traceId
        };

        if (includeStackTrace)
        {
            response.Details = new
            {
                exception.StackTrace,
                InnerException = exception.InnerException?.Message
            };
        }

        return response;
    }
}

/// <summary>
/// Standard API success response model.
/// </summary>
/// <typeparam name="T">The type of data in the response.</typeparam>
public class ApiSuccessResponse<T>
{
    /// <summary>
    /// Gets or sets a value indicating whether the request was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Gets or sets the response data.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets pagination information (if applicable).
    /// </summary>
    public PaginationInfo? Pagination { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the response.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiSuccessResponse{T}"/> class.
    /// </summary>
    public ApiSuccessResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiSuccessResponse{T}"/> class.
    /// </summary>
    /// <param name="data">The response data.</param>
    public ApiSuccessResponse(T? data)
    {
        Data = data;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiSuccessResponse{T}"/> class.
    /// </summary>
    /// <param name="data">The response data.</param>
    /// <param name="pagination">Pagination information.</param>
    public ApiSuccessResponse(T? data, PaginationInfo pagination)
    {
        Data = data;
        Pagination = pagination;
    }
}

/// <summary>
/// Pagination information for paginated responses.
/// </summary>
public class PaginationInfo
{
    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of items.
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaginationInfo"/> class.
    /// </summary>
    public PaginationInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaginationInfo"/> class.
    /// </summary>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalItems">The total number of items.</param>
    public PaginationInfo(int pageNumber, int pageSize, long totalItems)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = (int)Math.Ceiling((decimal)totalItems / pageSize);
        HasNextPage = pageNumber < TotalPages;
        HasPreviousPage = pageNumber > 1;
    }
}
