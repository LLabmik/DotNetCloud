using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Tests.Services;
using Grpc.Core;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for <see cref="CallerSubjectGuard"/>: a user-scoped module call may only act for the user
/// proven by the core-signed assertion, never for the user id in the payload.
/// </summary>
[TestClass]
public class CallerSubjectGuardTests
{
    private const string ModuleId = "dotnetcloud.files";

    [TestMethod]
    public void Bind_UserCallerNamesSelf_IsAllowed()
    {
        var userId = Guid.CreateVersion7();
        var context = CreateUserContext(userId);

        var binding = CallerSubjectGuard.Bind(context, userId.ToString());

        Assert.IsTrue(binding.IsAllowed);
        Assert.AreEqual(userId, binding.UserId);
        Assert.IsFalse(binding.IsModuleScope);
    }

    [TestMethod]
    public void Bind_UserCallerOmitsUserId_ActsForItself()
    {
        var userId = Guid.CreateVersion7();
        var context = CreateUserContext(userId);

        var binding = CallerSubjectGuard.Bind(context, requestedUserId: null);

        Assert.IsTrue(binding.IsAllowed);
        Assert.AreEqual(userId, binding.UserId);
    }

    [TestMethod]
    public void Bind_UserCallerNamesAnotherUser_IsDenied()
    {
        var context = CreateUserContext(Guid.CreateVersion7());

        var binding = CallerSubjectGuard.Bind(context, Guid.CreateVersion7().ToString());

        Assert.IsFalse(binding.IsAllowed);
        Assert.AreEqual(SubjectBindingOutcome.Mismatch, binding.Outcome);
        Assert.AreEqual(Guid.Empty, binding.UserId);
    }

    [TestMethod]
    public void Bind_UserCallerSuppliesUnparseableId_ActsForItself()
    {
        var userId = Guid.CreateVersion7();
        var context = CreateUserContext(userId);

        var binding = CallerSubjectGuard.Bind(context, "not-a-guid");

        Assert.IsTrue(binding.IsAllowed);
        Assert.AreEqual(userId, binding.UserId);
    }

    [TestMethod]
    public void Bind_ModuleScopeCall_IsAllowedAndFlagged()
    {
        var context = CreateModuleContext();
        var target = Guid.CreateVersion7();

        var binding = CallerSubjectGuard.Bind(context, target.ToString());

        Assert.IsTrue(binding.IsAllowed);
        Assert.IsTrue(binding.IsModuleScope);
        Assert.AreEqual(target, binding.UserId);
    }

    [TestMethod]
    public void Bind_NoCallerIdentity_FailsClosed()
    {
        var binding = CallerSubjectGuard.Bind(new TestServerCallContext(), Guid.CreateVersion7().ToString());

        Assert.IsFalse(binding.IsAllowed);
        Assert.AreEqual(SubjectBindingOutcome.MissingCaller, binding.Outcome);
    }

    [TestMethod]
    public void CreateDeniedException_Mismatch_CarriesStableErrorCode()
    {
        var ex = CallerSubjectGuard.CreateDeniedException(SubjectBindingOutcome.Mismatch);

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.AreEqual("AUTH_SUBJECT_MISMATCH", ex.Trailers.GetValue("error-code"));
    }

    private static TestServerCallContext CreateUserContext(Guid userId)
    {
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = new CallerContext(userId, ["User"], CallerType.User);
        return context;
    }

    private static TestServerCallContext CreateModuleContext()
    {
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = CallerContext.CreateModuleContext(Guid.CreateVersion7());
        return context;
    }
}
