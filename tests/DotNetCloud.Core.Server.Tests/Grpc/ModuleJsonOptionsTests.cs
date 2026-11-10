using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Guards the JSON convention every module host shares (see <see cref="ModuleJsonOptionsExtensions"/>).
/// </summary>
/// <remarks>
/// A sandboxed module's only HTTP caller is its own JavaScript UI, which sends enum <b>names</b>. When a
/// host forgot to opt in, MVC answered
/// <c>400 "The JSON value could not be converted to DotNetCloud.Core.DTOs.PhotoEditType"</c> and the
/// photo edit feature was dead end-to-end — without a single compile error, and with unit tests green,
/// because they constructed the DTO in C# instead of crossing the wire.
/// </remarks>
[TestClass]
public class ModuleJsonOptionsTests
{
    [TestMethod]
    public void AddModuleJsonOptions_BindsEnumNames()
    {
        var options = BuildOptions();

        var operation = JsonSerializer.Deserialize<PhotoEditOperationDto>(
            """{"operationType":"Rotate","parameters":{"value":"270"}}""", options);

        Assert.IsNotNull(operation);
        Assert.AreEqual(PhotoEditType.Rotate, operation.OperationType);
        Assert.AreEqual("270", operation.Parameters["value"]);
    }

    [TestMethod]
    public void AddModuleJsonOptions_StillAcceptsNumericEnums()
    {
        var options = BuildOptions();

        var operation = JsonSerializer.Deserialize<PhotoEditOperationDto>(
            $"{{\"operationType\":{(int)PhotoEditType.Flip},\"parameters\":{{}}}}", options);

        Assert.IsNotNull(operation);
        Assert.AreEqual(PhotoEditType.Flip, operation.OperationType);
    }

    [TestMethod]
    public void AddModuleJsonOptions_WritesEnumNames()
    {
        var options = BuildOptions();

        var json = JsonSerializer.Serialize(new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Crop,
            Parameters = new Dictionary<string, string>(),
        }, options);

        StringAssert.Contains(json, "\"Crop\"");
    }

    [TestMethod]
    public void AddModuleJsonOptions_RejectsAnUnknownEnumName()
    {
        var options = BuildOptions();

        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PhotoEditOperationDto>(
            """{"operationType":"Spin","parameters":{}}""", options));
    }

    private static JsonSerializerOptions BuildOptions()
    {
        // The same wiring a module host performs: AddControllers().AddModuleJsonOptions().
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().AddModuleJsonOptions();
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }
}
