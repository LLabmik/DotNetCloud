using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// The JSON convention every module host's HTTP API shares.
/// </summary>
/// <remarks>
/// A module's only HTTP client is its own module-hosted UI, which is JavaScript: it sends enum values
/// as their <b>names</b> (<c>"operationType": "Rotate"</c>) because a numeric literal means nothing in
/// a browser script. Without a converter MVC cannot bind those names and answers
/// <c>400 "The JSON value could not be converted to …"</c> — a defect the compiler cannot see and the
/// core never hits, because core-to-module traffic is gRPC.
/// <para>
/// Integer values stay accepted (<c>allowIntegerValues</c>), so an existing client that sends numbers
/// keeps working.
/// </para>
/// </remarks>
public static class ModuleJsonOptionsExtensions
{
    /// <summary>
    /// Makes the module's controllers bind and emit enums as names instead of integers.
    /// </summary>
    /// <param name="builder">The MVC builder returned by <c>AddControllers</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IMvcBuilder AddModuleJsonOptions(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(allowIntegerValues: true)));
    }
}
