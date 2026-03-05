using System.Text.Json;
using DotNetCloud.Client.SyncService.Ipc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.SyncService.Tests;

/// <summary>Tests for IPC protocol serialisation round-trips.</summary>
[TestClass]
public class IpcProtocolTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

    // ── IpcCommand ────────────────────────────────────────────────────────

    [TestMethod]
    public void IpcCommand_Serialise_CommandNamePreserved()
    {
        var command = new IpcCommand { Command = IpcCommands.ListContexts };
        var json = JsonSerializer.Serialize(command, JsonOptions);

        Assert.IsTrue(json.Contains("list-contexts"), "Command name should be preserved in JSON.");
    }

    [TestMethod]
    public void IpcCommand_Deserialise_CommandAndContextIdRestored()
    {
        var contextId = Guid.NewGuid();
        var json = $"{{\"command\":\"pause\",\"contextId\":\"{contextId}\"}}";

        var command = JsonSerializer.Deserialize<IpcCommand>(json, JsonOptions);

        Assert.IsNotNull(command);
        Assert.AreEqual(IpcCommands.Pause, command.Command);
        Assert.AreEqual(contextId, command.ContextId);
    }

    [TestMethod]
    public void IpcCommand_Deserialise_NullContextIdWhenAbsent()
    {
        var json = "{\"command\":\"list-contexts\"}";

        var command = JsonSerializer.Deserialize<IpcCommand>(json, JsonOptions);

        Assert.IsNotNull(command);
        Assert.IsNull(command.ContextId);
    }

    // ── IpcMessage ────────────────────────────────────────────────────────

    [TestMethod]
    public void IpcMessage_Response_SerialisedCorrectly()
    {
        var msg = new IpcMessage
        {
            Type = "response",
            Command = IpcCommands.GetStatus,
            Success = true,
            Data = new { state = "Idle" },
        };

        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.IsTrue(json.Contains("\"type\":\"response\""));
        Assert.IsTrue(json.Contains("\"success\":true"));
        Assert.IsTrue(json.Contains("get-status"));
    }

    [TestMethod]
    public void IpcMessage_ErrorResponse_ErrorFieldPresent()
    {
        var msg = new IpcMessage
        {
            Type = "response",
            Command = IpcCommands.Pause,
            Success = false,
            Error = "Context not found.",
        };

        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.IsTrue(json.Contains("\"success\":false"));
        Assert.IsTrue(json.Contains("Context not found"));
    }

    [TestMethod]
    public void IpcMessage_Event_EventFieldPresent()
    {
        var contextId = Guid.NewGuid();
        var msg = new IpcMessage
        {
            Type = "event",
            Event = IpcEvents.SyncComplete,
            ContextId = contextId,
            Success = true,
            Data = new { lastSyncedAt = DateTime.UtcNow },
        };

        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.IsTrue(json.Contains("\"type\":\"event\""));
        Assert.IsTrue(json.Contains("sync-complete"));
        Assert.IsTrue(json.Contains(contextId.ToString()));
    }

    // ── AddAccountData ────────────────────────────────────────────────────

    [TestMethod]
    public void AddAccountData_Deserialise_AllFieldsRestored()
    {
        var userId = Guid.NewGuid();
        var expiry = DateTime.UtcNow.AddHours(1);
        var json = JsonSerializer.Serialize(new AddAccountData
        {
            ServerUrl = "https://cloud.example.com",
            UserId = userId,
            LocalFolderPath = "/home/user/DotNetCloud",
            DisplayName = "User @ cloud.example.com",
            AccessToken = "tok_abc",
            RefreshToken = "ref_xyz",
            ExpiresAt = expiry,
        }, JsonOptions);

        var data = JsonSerializer.Deserialize<AddAccountData>(json, JsonOptions);

        Assert.IsNotNull(data);
        Assert.AreEqual("https://cloud.example.com", data.ServerUrl);
        Assert.AreEqual(userId, data.UserId);
        Assert.AreEqual("tok_abc", data.AccessToken);
    }

    // ── ContextInfo ───────────────────────────────────────────────────────

    [TestMethod]
    public void ContextInfo_Serialise_NullLastErrorOmitted()
    {
        var info = new ContextInfo
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            ServerBaseUrl = "https://cloud.example.com",
            LocalFolderPath = "/tmp/sync",
            State = "Idle",
        };

        var json = JsonSerializer.Serialize(info, JsonOptions);

        // lastError is null — should be omitted in WhenWritingNull mode
        Assert.IsFalse(json.Contains("lastError"), "Null lastError should be omitted.");
    }

    // ── Command constant values ───────────────────────────────────────────

    [TestMethod]
    public void IpcCommands_AllValues_AreKebabCase()
    {
        var commands = new[]
        {
            IpcCommands.ListContexts,
            IpcCommands.AddAccount,
            IpcCommands.RemoveAccount,
            IpcCommands.GetStatus,
            IpcCommands.Pause,
            IpcCommands.Resume,
            IpcCommands.SyncNow,
            IpcCommands.Subscribe,
            IpcCommands.Unsubscribe,
        };

        foreach (var cmd in commands)
        {
            Assert.IsFalse(cmd.Contains(' '), $"Command '{cmd}' must not contain spaces.");
            Assert.AreEqual(cmd.ToLowerInvariant(), cmd, $"Command '{cmd}' must be lower-case.");
        }
    }

    [TestMethod]
    public void IpcEvents_AllValues_AreKebabCase()
    {
        var events = new[]
        {
            IpcEvents.SyncProgress,
            IpcEvents.SyncComplete,
            IpcEvents.ConflictDetected,
            IpcEvents.Error,
        };

        foreach (var ev in events)
        {
            Assert.IsFalse(ev.Contains(' '), $"Event '{ev}' must not contain spaces.");
            Assert.AreEqual(ev.ToLowerInvariant(), ev, $"Event '{ev}' must be lower-case.");
        }
    }
}
