using DotNetCloud.Core.Grpc.ModuleApi.V1;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Structural guards for the canonical module API contract (phase A2,
/// <c>docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md</c> §4.3).
/// </summary>
/// <remarks>
/// These assert the generated descriptors, not source text, so they fail if anyone re-adds a
/// caller-supplied identity field or a request-supplied module id — the two shapes the plan
/// forbids because the interceptor and evaluator would have to keep ignoring them forever.
/// </remarks>
[TestClass]
public class ModuleApiContractTests
{
    private static readonly string[] ExpectedRpcs =
    [
        "ReportHealth",
        "GetUser",
        "SearchUsers",
        "GetCurrentUserContext",
        "GetGroup",
        "GetGroupsForUser",
        "GetTeam",
        "GetTeamsForUser",
        "SendNotification",
        "PublishEvent",
        "GetSetting",
        "SetSetting",
        "LogAudit",
        "SubmitSearchIndex",
        "BroadcastRealtimeEvent",
        "CheckUserConsent",
        "GetGrantedScopes",
        "ListFiles",
        "DownloadFile",
        "UploadFile",
    ];

    [TestMethod]
    public void Service_UsesVersionedPackage()
    {
        StringAssert.EndsWith(ModuleApi.Descriptor.File.Name, "module_api.proto");
        Assert.AreEqual("dotnetcloud.moduleapi.v1", ModuleApi.Descriptor.File.Package);
    }

    [TestMethod]
    public void Service_ExposesExactlyTheExpectedRpcSet()
    {
        var actual = ModuleApi.Descriptor.Methods.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal);
        var expected = ExpectedRpcs.OrderBy(n => n, StringComparer.Ordinal);

        CollectionAssert.AreEqual(expected.ToList(), actual.ToList());
    }

    [TestMethod]
    public void Requests_NeverCarryCallerIdentityField()
    {
        foreach (var method in ModuleApi.Descriptor.Methods)
        {
            var input = method.InputType;
            var callerField = input.Fields.InDeclarationOrder()
                .FirstOrDefault(f => string.Equals(f.Name, "caller", StringComparison.OrdinalIgnoreCase));

            Assert.IsNull(callerField, $"{method.Name} request exposes a caller field.");
            Assert.IsFalse(
                input.Fields.InDeclarationOrder().Any(f => f.FieldNumber == 1),
                $"{method.Name} request uses field 1, which is reserved for the removed caller slot.");
        }
    }

    [TestMethod]
    public void Requests_NeverCarryModuleIdField()
    {
        // The module id always comes from the authenticated transport (dnc-module-id); a request
        // field would reintroduce the F7 settings/attribution spoofing class.
        foreach (var method in ModuleApi.Descriptor.Methods)
        {
            var moduleIdField = method.InputType.Fields.InDeclarationOrder()
                .FirstOrDefault(f => string.Equals(f.Name, "module_id", StringComparison.OrdinalIgnoreCase));

            Assert.IsNull(moduleIdField, $"{method.Name} request exposes a module_id field.");
        }
    }

    [TestMethod]
    public void UserScopedRequests_DocumentEmptyTargetAsActingUser()
    {
        // The contract's subject-binding rule: an empty user id means "the acting user", so a
        // module cannot silently address someone else by omitting the value.
        foreach (var messageName in new[] { "GetGroupsForUserRequest", "GetTeamsForUserRequest", "CheckUserConsentRequest", "GetGrantedScopesRequest" })
        {
            var message = FindMessage(messageName);
            Assert.IsNotNull(message, $"{messageName} is missing from the contract.");

            var userId = message.Fields.InDeclarationOrder()
                .FirstOrDefault(f => f.Name == "user_id");

            Assert.IsNotNull(userId, $"{messageName} must expose a user_id target field.");
            Assert.AreEqual(
                Google.Protobuf.Reflection.FieldType.String,
                userId.FieldType,
                $"{messageName}.user_id must be a string so 'empty = acting user' is expressible.");
        }
    }

    private static Google.Protobuf.Reflection.MessageDescriptor? FindMessage(string name) =>
        ModuleApi.Descriptor.File.MessageTypes
            .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));
}
