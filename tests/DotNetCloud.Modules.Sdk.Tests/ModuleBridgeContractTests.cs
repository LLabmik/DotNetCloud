using DotNetCloud.Core.Modules.Ui;

namespace DotNetCloud.Modules.Sdk.Tests;

/// <summary>
/// Drift guard for the module UI plane protocol: the C# contract
/// (<see cref="ModuleAppProtocol"/>) and the shipped <c>dncModuleBridge.js</c> must name exactly the
/// same messages. A rename in one place that misses the other silently breaks every module UI.
/// </summary>
[TestClass]
public class ModuleBridgeContractTests
{
    private const string ResourceName = "DotNetCloud.Modules.Sdk.wwwroot.dncModuleBridge.js";

    private static string ReadBridgeSource()
    {
        var assembly = typeof(ModuleAppProtocol).Assembly;

        // The bridge lives in the SDK assembly, so look it up there rather than by file path.
        var sdkAssembly = typeof(DotNetCloud.Modules.Sdk.Manifest.ModuleManifestV2).Assembly;
        using var stream = sdkAssembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing. Ensure wwwroot/dncModuleBridge.js is an EmbeddedResource.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [TestMethod]
    public void Bridge_DeclaresEveryFrameOriginatedMessageType()
    {
        var source = ReadBridgeSource();

        foreach (var type in ModuleAppProtocol.FromFrame)
        {
            Assert.IsTrue(
                source.Contains("\"" + type + "\"", StringComparison.Ordinal)
                || source.Contains("'" + type + "'", StringComparison.Ordinal),
                $"dncModuleBridge.js is missing frame-originated message type '{type}'.");
        }
    }

    [TestMethod]
    public void Bridge_DeclaresEveryHostOriginatedMessageType()
    {
        var source = ReadBridgeSource();

        foreach (var type in ModuleAppProtocol.ToFrame)
        {
            Assert.IsTrue(
                source.Contains("\"" + type + "\"", StringComparison.Ordinal)
                || source.Contains("'" + type + "'", StringComparison.Ordinal),
                $"dncModuleBridge.js does not accept host-originated message type '{type}'.");
        }
    }

    [TestMethod]
    public void Bridge_AuthenticatesTheParentByIdentity()
    {
        var source = ReadBridgeSource();

        Assert.IsTrue(
            source.Contains("event.source !== window.parent", StringComparison.Ordinal),
            "The bridge must validate event.source — origin is 'null' for every sandboxed frame.");
    }

    [TestMethod]
    public void Bridge_ExposesThePublicSurface()
    {
        var source = ReadBridgeSource();

        foreach (var member in new[] { "start:", "on:", "navigate:", "openExternal:", "requestConsent:", "notify:", "dispose:" })
        {
            Assert.IsTrue(
                source.Contains(member, StringComparison.Ordinal),
                $"dncModuleBridge.js is missing the public member '{member.TrimEnd(':')}'.");
        }
    }

    [TestMethod]
    public void Bridge_UsesAstrictModeIife()
    {
        var source = ReadBridgeSource();

        Assert.IsTrue(source.Contains("'use strict'", StringComparison.Ordinal));
        Assert.IsTrue(source.Contains("window.dncModuleBridge", StringComparison.Ordinal));
    }
}
