// Licensed under the Apache License, Version 2.0.

#if !WINDOWS_BUILD

#pragma warning disable CA1416 // Platform checks handled by conditional compilation

using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Platform.Linux;

/// <summary>
/// Adapts <see cref="Microsoft.Extensions.Logging.ILogger"/> to
/// <see cref="FuseDotNet.Logging.ILogger"/> for use with LTRData.FuseDotNet.
/// </summary>
internal sealed class FuseLoggerAdapter : FuseDotNet.Logging.ILogger
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FuseLoggerAdapter"/>.
    /// </summary>
    /// <param name="logger">The Microsoft.Extensions logger to wrap.</param>
    public FuseLoggerAdapter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool DebugEnabled => _logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug);

    /// <inheritdoc/>
    public void Debug(string message, params object[] args)
        => _logger.LogDebug(message, args);

    /// <inheritdoc/>
    public void Info(string message, params object[] args)
        => _logger.LogInformation(message, args);

    /// <inheritdoc/>
    public void Warn(string message, params object[] args)
        => _logger.LogWarning(message, args);

    /// <inheritdoc/>
    public void Error(string message, params object[] args)
        => _logger.LogError(message, args);

    /// <inheritdoc/>
    public void Fatal(string message, params object[] args)
        => _logger.LogCritical(message, args);
}

#endif
