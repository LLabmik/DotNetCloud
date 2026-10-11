using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.Android.Tests.ViewModels;

/// <summary>
/// Covers the editor's failure/retry state machine (the WebView load result is reported by the page,
/// which platform tests cannot drive).
/// </summary>
[TestClass]
public sealed class DocumentEditorViewModelTests
{
    private Mock<IWopiRestClient> _wopiApi = null!;
    private Mock<IServerConnectionStore> _serverStore = null!;
    private Mock<ISecureTokenStore> _tokenStore = null!;
    private DocumentEditorViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        _wopiApi = new Mock<IWopiRestClient>(MockBehavior.Strict);
        _serverStore = new Mock<IServerConnectionStore>(MockBehavior.Strict);
        _tokenStore = new Mock<ISecureTokenStore>(MockBehavior.Strict);

        _vm = new DocumentEditorViewModel(
            _wopiApi.Object,
            _serverStore.Object,
            _tokenStore.Object,
            NullLogger<DocumentEditorViewModel>.Instance);
    }

    [TestMethod]
    public void InitialState_IsLoadingWithoutError()
    {
        Assert.IsTrue(_vm.IsLoading);
        Assert.IsFalse(_vm.ShowError);
    }

    [TestMethod]
    public void NotifyEditorLoaded_ClearsTheBusyStateAndAnyError()
    {
        _vm.NotifyEditorLoadFailed(502);

        _vm.NotifyEditorLoaded();

        Assert.IsFalse(_vm.IsLoading);
        Assert.IsNull(_vm.ErrorMessage);
        Assert.IsFalse(_vm.ShowError);
    }

    [TestMethod]
    public void NotifyEditorLoadFailed_WithoutStatus_ReportsAConnectionProblem()
    {
        _vm.NotifyEditorLoadFailed();

        Assert.IsFalse(_vm.IsLoading);
        Assert.IsTrue(_vm.ShowError);
        StringAssert.Contains(_vm.ErrorMessage, "Check your connection");
    }

    [TestMethod]
    public void NotifyEditorLoadFailed_ServerError_MentionsTheStatusAndCollabora()
    {
        _vm.NotifyEditorLoadFailed(502);

        Assert.IsTrue(_vm.ShowError);
        StringAssert.Contains(_vm.ErrorMessage, "HTTP 502");
        StringAssert.Contains(_vm.ErrorMessage, "Collabora may be down");
    }

    [TestMethod]
    public void NotifyEditorLoadFailed_Forbidden_ReportsLostAccess()
    {
        _vm.NotifyEditorLoadFailed(403);

        Assert.IsTrue(_vm.ShowError);
        StringAssert.Contains(_vm.ErrorMessage, "no longer have access");
    }

    [TestMethod]
    public void NotifyEditorLoadFailed_NotFound_ReportsAMissingDocument()
    {
        _vm.NotifyEditorLoadFailed(404);

        Assert.IsTrue(_vm.ShowError);
        StringAssert.Contains(_vm.ErrorMessage, "could not be found");
    }

    [TestMethod]
    public void NotifyEditorLoadFailed_OtherStatus_IncludesTheStatusCode()
    {
        _vm.NotifyEditorLoadFailed(418);

        Assert.IsTrue(_vm.ShowError);
        StringAssert.Contains(_vm.ErrorMessage, "418");
    }

    [TestMethod]
    public async Task EndSessionAsync_WithoutAnOpenedDocument_DoesNotCallTheApi()
    {
        // No document id was applied, so there is no session to end and no credentials to fetch —
        // the strict mocks fail the test on any interaction.
        await _vm.EndSessionAsync();
        await _vm.EndSessionAsync();

        _wopiApi.VerifyNoOtherCalls();
        _serverStore.VerifyNoOtherCalls();
        _tokenStore.VerifyNoOtherCalls();
    }
}
