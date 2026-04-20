using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="WebRtcInteropService"/>.
/// Covers initialization validation, SDP/ICE payload validation, peer ID validation,
/// element ID validation, stream type validation, JS interop delegation, and error handling.
/// </summary>
[TestClass]
public class WebRtcInteropServiceTests
{
    private Mock<IJSRuntime> _jsMock = null!;
    private WebRtcInteropService _service = null!;

    private static readonly WebRtcCallConfig ValidConfig = new()
    {
        CallId = "call-123",
        IceServers =
        [
            new IceServerDto { Urls = ["stun:stun.l.google.com:19302"] }
        ]
    };

    [TestInitialize]
    public void Setup()
    {
        _jsMock = new Mock<IJSRuntime>();
        _service = new WebRtcInteropService(_jsMock.Object, NullLogger<WebRtcInteropService>.Instance);
    }

    // ═══════════════════════════════════════════════════════════
    // Constructor validation
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void Constructor_NullJsRuntime_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new WebRtcInteropService(null!, NullLogger<WebRtcInteropService>.Instance));
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new WebRtcInteropService(_jsMock.Object, null!));
    }

    // ═══════════════════════════════════════════════════════════
    // InitializeCallAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitializeCallAsync_NullDotNetRef_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await _service.InitializeCallAsync<object>(null!, ValidConfig));
    }

    [TestMethod]
    public async Task InitializeCallAsync_NullConfig_ThrowsArgumentNullException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, null!));
    }

    [TestMethod]
    public async Task InitializeCallAsync_EmptyCallId_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "",
            IceServers = [new IceServerDto { Urls = ["stun:stun.example.com"] }]
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_WhitespaceCallId_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "   ",
            IceServers = [new IceServerDto { Urls = ["stun:stun.example.com"] }]
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_NullIceServers_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = null!
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_EmptyIceServers_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = []
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_IceServerWithNoUrls_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = [new IceServerDto { Urls = [] }]
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_IceServerWithNullUrls_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = [new IceServerDto { Urls = null! }]
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_InvalidIceTransportPolicy_ThrowsArgumentException()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = [new IceServerDto { Urls = ["stun:stun.example.com"] }],
            IceTransportPolicy = "invalid"
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.InitializeCallAsync(dotNetRef, config));
    }

    [TestMethod]
    public async Task InitializeCallAsync_ValidConfig_InvokesJsInitializeCall()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.initializeCall", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.InitializeCallAsync(dotNetRef, ValidConfig);

        Assert.IsTrue(result);
        _jsMock.Verify(js => js.InvokeAsync<bool>(
            "dotnetcloudVideoCall.initializeCall",
            It.Is<object?[]>(args => args.Length == 2)), Times.Once);
    }

    [TestMethod]
    public async Task InitializeCallAsync_AllTransportPolicy_Succeeds()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = [new IceServerDto { Urls = ["stun:stun.example.com"] }],
            IceTransportPolicy = "all"
        };

        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.initializeCall", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.InitializeCallAsync(dotNetRef, config);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task InitializeCallAsync_RelayTransportPolicy_Succeeds()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = [new IceServerDto { Urls = ["turn:turn.example.com:3478"] }],
            IceTransportPolicy = "relay"
        };

        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.initializeCall", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.InitializeCallAsync(dotNetRef, config);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task InitializeCallAsync_MultipleIceServers_Succeeds()
    {
        using var dotNetRef = DotNetObjectReference.Create(new object());
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers =
            [
                new IceServerDto { Urls = ["stun:stun.l.google.com:19302"] },
                new IceServerDto { Urls = ["turn:turn.example.com:3478"], Username = "user", Credential = "pass" }
            ]
        };

        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.initializeCall", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.InitializeCallAsync(dotNetRef, config);

        Assert.IsTrue(result);
    }

    // ═══════════════════════════════════════════════════════════
    // Peer ID validation (static, internal)
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void ValidatePeerId_Null_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidatePeerId(null!));
    }

    [TestMethod]
    public void ValidatePeerId_Empty_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidatePeerId(""));
    }

    [TestMethod]
    public void ValidatePeerId_Whitespace_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidatePeerId("   "));
    }

    [TestMethod]
    public void ValidatePeerId_TooLong_ThrowsArgumentException()
    {
        var longId = new string('a', 101);
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidatePeerId(longId));
    }

    [TestMethod]
    public void ValidatePeerId_ValidGuid_Succeeds()
    {
        WebRtcInteropService.ValidatePeerId(Guid.NewGuid().ToString());
    }

    [TestMethod]
    public void ValidatePeerId_MaxLength_Succeeds()
    {
        var id = new string('x', 100);
        WebRtcInteropService.ValidatePeerId(id);
    }

    // ═══════════════════════════════════════════════════════════
    // SDP payload validation (static, internal)
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void ValidateSdpPayload_Null_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateSdpPayload(null!));
    }

    [TestMethod]
    public void ValidateSdpPayload_Empty_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateSdpPayload(""));
    }

    [TestMethod]
    public void ValidateSdpPayload_Whitespace_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateSdpPayload("   "));
    }

    [TestMethod]
    public void ValidateSdpPayload_ExceedsMaxSize_ThrowsArgumentException()
    {
        var oversized = new string('x', 65_537); // > 64 KB
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateSdpPayload(oversized));
    }

    [TestMethod]
    public void ValidateSdpPayload_AtMaxSize_Succeeds()
    {
        var atLimit = new string('x', 65_536); // exactly 64 KB
        WebRtcInteropService.ValidateSdpPayload(atLimit);
    }

    [TestMethod]
    public void ValidateSdpPayload_ValidSdp_Succeeds()
    {
        WebRtcInteropService.ValidateSdpPayload("{\"type\":\"offer\",\"sdp\":\"v=0\\r\\n\"}");
    }

    [TestMethod]
    public void ValidateSdpPayload_MultiByte_ExceedsMaxSize_ThrowsArgumentException()
    {
        // Multi-byte UTF-8 characters: each '日' is 3 bytes
        var multiByteOversize = new string('日', 21_846); // 21846 * 3 = 65538 > 64 KB
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateSdpPayload(multiByteOversize));
    }

    // ═══════════════════════════════════════════════════════════
    // ICE candidate validation (static, internal)
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void ValidateIceCandidate_Null_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateIceCandidate(null!));
    }

    [TestMethod]
    public void ValidateIceCandidate_Empty_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateIceCandidate(""));
    }

    [TestMethod]
    public void ValidateIceCandidate_Whitespace_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateIceCandidate("  "));
    }

    [TestMethod]
    public void ValidateIceCandidate_ExceedsMaxSize_ThrowsArgumentException()
    {
        var oversized = new string('c', 4_097); // > 4 KB
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateIceCandidate(oversized));
    }

    [TestMethod]
    public void ValidateIceCandidate_AtMaxSize_Succeeds()
    {
        var atLimit = new string('c', 4_096); // exactly 4 KB
        WebRtcInteropService.ValidateIceCandidate(atLimit);
    }

    [TestMethod]
    public void ValidateIceCandidate_ValidCandidate_Succeeds()
    {
        WebRtcInteropService.ValidateIceCandidate("{\"candidate\":\"candidate:1 1 UDP 2130706431 192.168.1.1 5000 typ host\"}");
    }

    // ═══════════════════════════════════════════════════════════
    // Element ID validation (static, internal)
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void ValidateElementId_Null_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateElementId(null!));
    }

    [TestMethod]
    public void ValidateElementId_Empty_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateElementId(""));
    }

    [TestMethod]
    public void ValidateElementId_TooLong_ThrowsArgumentException()
    {
        var longId = new string('e', 201);
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateElementId(longId));
    }

    [TestMethod]
    public void ValidateElementId_AtMaxLength_Succeeds()
    {
        WebRtcInteropService.ValidateElementId(new string('e', 200));
    }

    [TestMethod]
    public void ValidateElementId_ValidId_Succeeds()
    {
        WebRtcInteropService.ValidateElementId("video-local-preview");
    }

    // ═══════════════════════════════════════════════════════════
    // Stream type validation (static, internal)
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void ValidateStreamType_Null_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateStreamType(null!));
    }

    [TestMethod]
    public void ValidateStreamType_Empty_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateStreamType(""));
    }

    [TestMethod]
    public void ValidateStreamType_Local_Succeeds()
    {
        WebRtcInteropService.ValidateStreamType("local");
    }

    [TestMethod]
    public void ValidateStreamType_Screen_Succeeds()
    {
        WebRtcInteropService.ValidateStreamType("screen");
    }

    [TestMethod]
    public void ValidateStreamType_ValidGuid_Succeeds()
    {
        WebRtcInteropService.ValidateStreamType(Guid.NewGuid().ToString());
    }

    [TestMethod]
    public void ValidateStreamType_InvalidString_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateStreamType("invalid-type"));
    }

    [TestMethod]
    public void ValidateStreamType_RandomText_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            WebRtcInteropService.ValidateStreamType("foobar"));
    }

    // ═══════════════════════════════════════════════════════════
    // CreateOfferAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateOfferAsync_NullPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.CreateOfferAsync(null!));
    }

    [TestMethod]
    public async Task CreateOfferAsync_EmptyPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.CreateOfferAsync(""));
    }

    [TestMethod]
    public async Task CreateOfferAsync_ValidPeerId_InvokesJsCreateOffer()
    {
        var peerId = Guid.NewGuid().ToString();
        var expectedSdp = "{\"type\":\"offer\",\"sdp\":\"v=0\"}";

        _jsMock.Setup(js => js.InvokeAsync<string?>("dotnetcloudVideoCall.createOffer", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedSdp);

        var result = await _service.CreateOfferAsync(peerId);

        Assert.AreEqual(expectedSdp, result);
        _jsMock.Verify(js => js.InvokeAsync<string?>(
            "dotnetcloudVideoCall.createOffer",
            It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == peerId)), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // HandleOfferAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HandleOfferAsync_NullPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.HandleOfferAsync(null!, "{\"sdp\":\"valid\"}"));
    }

    [TestMethod]
    public async Task HandleOfferAsync_NullSdp_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.HandleOfferAsync("peer1", null!));
    }

    [TestMethod]
    public async Task HandleOfferAsync_EmptySdp_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.HandleOfferAsync("peer1", ""));
    }

    [TestMethod]
    public async Task HandleOfferAsync_OversizedSdp_ThrowsArgumentException()
    {
        var oversizedSdp = new string('x', 65_537);
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.HandleOfferAsync("peer1", oversizedSdp));
    }

    [TestMethod]
    public async Task HandleOfferAsync_ValidInput_InvokesJsHandleOffer()
    {
        var peerId = "peer-1";
        var sdp = "{\"type\":\"offer\",\"sdp\":\"v=0\"}";
        var expectedAnswer = "{\"type\":\"answer\",\"sdp\":\"v=0\"}";

        _jsMock.Setup(js => js.InvokeAsync<string?>("dotnetcloudVideoCall.handleOffer", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedAnswer);

        var result = await _service.HandleOfferAsync(peerId, sdp);

        Assert.AreEqual(expectedAnswer, result);
    }

    // ═══════════════════════════════════════════════════════════
    // HandleAnswerAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HandleAnswerAsync_NullPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.HandleAnswerAsync(null!, "{\"sdp\":\"valid\"}"));
    }

    [TestMethod]
    public async Task HandleAnswerAsync_NullSdp_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.HandleAnswerAsync("peer1", null!));
    }

    [TestMethod]
    public async Task HandleAnswerAsync_ValidInput_InvokesJsHandleAnswer()
    {
        var peerId = "peer-1";
        var sdp = "{\"type\":\"answer\",\"sdp\":\"v=0\"}";

        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.handleAnswer", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.HandleAnswerAsync(peerId, sdp);

        Assert.IsTrue(result);
    }

    // ═══════════════════════════════════════════════════════════
    // AddIceCandidateAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AddIceCandidateAsync_NullPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AddIceCandidateAsync(null!, "{\"candidate\":\"x\"}"));
    }

    [TestMethod]
    public async Task AddIceCandidateAsync_NullCandidate_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AddIceCandidateAsync("peer1", null!));
    }

    [TestMethod]
    public async Task AddIceCandidateAsync_EmptyCandidate_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AddIceCandidateAsync("peer1", ""));
    }

    [TestMethod]
    public async Task AddIceCandidateAsync_OversizedCandidate_ThrowsArgumentException()
    {
        var oversized = new string('c', 4_097);
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AddIceCandidateAsync("peer1", oversized));
    }

    [TestMethod]
    public async Task AddIceCandidateAsync_ValidInput_InvokesJsAddIceCandidate()
    {
        var peerId = "peer-1";
        var candidate = "{\"candidate\":\"candidate:1 1 UDP 2130706431 192.168.1.1 5000 typ host\"}";

        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.addIceCandidate", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.AddIceCandidateAsync(peerId, candidate);

        Assert.IsTrue(result);
    }

    // ═══════════════════════════════════════════════════════════
    // ToggleAudioAsync / ToggleVideoAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ToggleAudioAsync_True_InvokesJsToggleAudio()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleAudio", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.ToggleAudioAsync(true);

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleAudio",
            It.Is<object?[]>(args => args.Length == 1 && (bool)args[0]! == true)), Times.Once);
    }

    [TestMethod]
    public async Task ToggleAudioAsync_False_InvokesJsToggleAudio()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleAudio", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.ToggleAudioAsync(false);

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleAudio",
            It.Is<object?[]>(args => args.Length == 1 && (bool)args[0]! == false)), Times.Once);
    }

    [TestMethod]
    public async Task ToggleVideoAsync_True_InvokesJsToggleVideo()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleVideo", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.ToggleVideoAsync(true);

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleVideo",
            It.Is<object?[]>(args => args.Length == 1 && (bool)args[0]! == true)), Times.Once);
    }

    [TestMethod]
    public async Task ToggleVideoAsync_False_InvokesJsToggleVideo()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleVideo", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.ToggleVideoAsync(false);

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.toggleVideo",
            It.Is<object?[]>(args => args.Length == 1 && (bool)args[0]! == false)), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // AttachStreamToElementAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AttachStreamToElementAsync_NullElementId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AttachStreamToElementAsync(null!, "local"));
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_EmptyElementId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AttachStreamToElementAsync("", "local"));
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_NullStreamType_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AttachStreamToElementAsync("video-el", null!));
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_InvalidStreamType_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AttachStreamToElementAsync("video-el", "invalid"));
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_LocalStream_InvokesJs()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.attachStreamToElement", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.AttachStreamToElementAsync("video-el", "local");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_ScreenStream_InvokesJs()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.attachStreamToElement", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.AttachStreamToElementAsync("video-el", "screen");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_PeerGuid_InvokesJs()
    {
        var peerId = Guid.NewGuid().ToString();
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.attachStreamToElement", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.AttachStreamToElementAsync("video-el", peerId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task AttachStreamToElementAsync_TooLongElementId_ThrowsArgumentException()
    {
        var longId = new string('x', 201);
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.AttachStreamToElementAsync(longId, "local"));
    }

    // ═══════════════════════════════════════════════════════════
    // DetachStreamFromElementAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DetachStreamFromElementAsync_NullElementId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.DetachStreamFromElementAsync(null!));
    }

    [TestMethod]
    public async Task DetachStreamFromElementAsync_EmptyElementId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.DetachStreamFromElementAsync(""));
    }

    [TestMethod]
    public async Task DetachStreamFromElementAsync_ValidElementId_InvokesJs()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.detachStreamFromElement", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.DetachStreamFromElementAsync("video-el");

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.detachStreamFromElement",
            It.Is<object?[]>(args => args.Length == 1)), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // ClosePeerConnectionAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ClosePeerConnectionAsync_NullPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.ClosePeerConnectionAsync(null!));
    }

    [TestMethod]
    public async Task ClosePeerConnectionAsync_EmptyPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.ClosePeerConnectionAsync(""));
    }

    [TestMethod]
    public async Task ClosePeerConnectionAsync_ValidPeerId_InvokesJs()
    {
        var peerId = "peer-1";
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.closePeerConnection", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.ClosePeerConnectionAsync(peerId);

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.closePeerConnection",
            It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == peerId)), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // HangupAsync / DisposeAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HangupAsync_InvokesJsHangup()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.hangup", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.HangupAsync();

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.hangup",
            It.Is<object?[]>(args => args.Length == 0)), Times.Once);
    }

    [TestMethod]
    public async Task DisposeAsync_InvokesJsDispose()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.dispose", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.DisposeAsync();

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.dispose",
            It.Is<object?[]>(args => args.Length == 0)), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // StartLocalMediaAsync / StartScreenShareAsync / StopScreenShareAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task StartLocalMediaAsync_InvokesJsStartLocalMedia()
    {
        var expectedStreamId = "stream-abc-123";
        _jsMock.Setup(js => js.InvokeAsync<string?>("dotnetcloudVideoCall.startLocalMedia", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedStreamId);

        var result = await _service.StartLocalMediaAsync();

        Assert.AreEqual(expectedStreamId, result);
    }

    [TestMethod]
    public async Task StartLocalMediaAsync_ReturnsNull_WhenJsFails()
    {
        _jsMock.Setup(js => js.InvokeAsync<string?>("dotnetcloudVideoCall.startLocalMedia", It.IsAny<object?[]>()))
            .ReturnsAsync((string?)null);

        var result = await _service.StartLocalMediaAsync();

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task StartScreenShareAsync_InvokesJsStartScreenShare()
    {
        var expectedStreamId = "screen-stream-456";
        _jsMock.Setup(js => js.InvokeAsync<string?>("dotnetcloudVideoCall.startScreenShare", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedStreamId);

        var result = await _service.StartScreenShareAsync();

        Assert.AreEqual(expectedStreamId, result);
    }

    [TestMethod]
    public async Task StopScreenShareAsync_InvokesJsStopScreenShare()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.stopScreenShare", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.StopScreenShareAsync();

        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.stopScreenShare",
            It.Is<object?[]>(args => args.Length == 0)), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // GetCallStateAsync / GetPeerStateAsync / GetMediaStateAsync
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetCallStateAsync_ReturnsCallState()
    {
        var expectedState = new WebRtcCallState
        {
            CallId = "call-1",
            PeerCount = 2,
            IsScreenSharing = false,
            HasLocalMedia = true,
            Peers = ["peer-1", "peer-2"]
        };

        _jsMock.Setup(js => js.InvokeAsync<WebRtcCallState?>("dotnetcloudVideoCall.getCallState", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedState);

        var result = await _service.GetCallStateAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual("call-1", result.CallId);
        Assert.AreEqual(2, result.PeerCount);
        Assert.IsTrue(result.HasLocalMedia);
        Assert.IsFalse(result.IsScreenSharing);
    }

    [TestMethod]
    public async Task GetCallStateAsync_ReturnsNull_WhenNoActiveCall()
    {
        _jsMock.Setup(js => js.InvokeAsync<WebRtcCallState?>("dotnetcloudVideoCall.getCallState", It.IsAny<object?[]>()))
            .ReturnsAsync((WebRtcCallState?)null);

        var result = await _service.GetCallStateAsync();

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPeerStateAsync_NullPeerId_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await _service.GetPeerStateAsync(null!));
    }

    [TestMethod]
    public async Task GetPeerStateAsync_ValidPeerId_ReturnsPeerState()
    {
        var peerId = "peer-1";
        var expectedState = new WebRtcPeerState
        {
            Exists = true,
            ConnectionState = "connected",
            IceConnectionState = "connected",
            IceGatheringState = "complete"
        };

        _jsMock.Setup(js => js.InvokeAsync<WebRtcPeerState?>("dotnetcloudVideoCall.getPeerState", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedState);

        var result = await _service.GetPeerStateAsync(peerId);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Exists);
        Assert.AreEqual("connected", result.ConnectionState);
    }

    [TestMethod]
    public async Task GetPeerStateAsync_NonExistentPeer_ReturnsNonExistent()
    {
        var expectedState = new WebRtcPeerState
        {
            Exists = false,
            ConnectionState = null,
            IceConnectionState = null,
            IceGatheringState = null
        };

        _jsMock.Setup(js => js.InvokeAsync<WebRtcPeerState?>("dotnetcloudVideoCall.getPeerState", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedState);

        var result = await _service.GetPeerStateAsync("nonexistent");

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Exists);
    }

    [TestMethod]
    public async Task GetMediaStateAsync_ReturnsMediaState()
    {
        var expectedState = new WebRtcMediaState
        {
            HasAudio = true,
            AudioEnabled = true,
            HasVideo = true,
            VideoEnabled = false,
            IsScreenSharing = false
        };

        _jsMock.Setup(js => js.InvokeAsync<WebRtcMediaState?>("dotnetcloudVideoCall.getMediaState", It.IsAny<object?[]>()))
            .ReturnsAsync(expectedState);

        var result = await _service.GetMediaStateAsync();

        Assert.IsNotNull(result);
        Assert.IsTrue(result.HasAudio);
        Assert.IsTrue(result.AudioEnabled);
        Assert.IsTrue(result.HasVideo);
        Assert.IsFalse(result.VideoEnabled);
        Assert.IsFalse(result.IsScreenSharing);
    }

    [TestMethod]
    public async Task GetMediaStateAsync_ReturnsNull_WhenNoMedia()
    {
        _jsMock.Setup(js => js.InvokeAsync<WebRtcMediaState?>("dotnetcloudVideoCall.getMediaState", It.IsAny<object?[]>()))
            .ReturnsAsync((WebRtcMediaState?)null);

        var result = await _service.GetMediaStateAsync();

        Assert.IsNull(result);
    }

    // ── SetBackgroundBlurAsync Tests ────────────────────────────────

    [TestMethod]
    public async Task SetBackgroundBlurAsync_Enable_InvokesJsEnableBackgroundBlur()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.enableBackgroundBlur", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.SetBackgroundBlurAsync(true);

        Assert.IsTrue(result);
        _jsMock.Verify(js => js.InvokeAsync<bool>(
            "dotnetcloudVideoCall.enableBackgroundBlur",
            It.IsAny<object?[]>()), Times.Once);
    }

    [TestMethod]
    public async Task SetBackgroundBlurAsync_Enable_ReturnsFalse_WhenJsReturnsFalse()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoCall.enableBackgroundBlur", It.IsAny<object?[]>()))
            .ReturnsAsync(false);

        var result = await _service.SetBackgroundBlurAsync(true);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SetBackgroundBlurAsync_Disable_InvokesJsDisableBackgroundBlur()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.disableBackgroundBlur", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        var result = await _service.SetBackgroundBlurAsync(false);

        Assert.IsTrue(result);
        _jsMock.Verify(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.disableBackgroundBlur",
            It.IsAny<object?[]>()), Times.Once);
    }

    [TestMethod]
    public async Task SetBackgroundBlurAsync_Disable_DoesNotCallEnable()
    {
        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "dotnetcloudVideoCall.disableBackgroundBlur", It.IsAny<object?[]>()))
            .ReturnsAsync((Microsoft.JSInterop.Infrastructure.IJSVoidResult)null!);

        await _service.SetBackgroundBlurAsync(false);

        _jsMock.Verify(js => js.InvokeAsync<bool>(
            "dotnetcloudVideoCall.enableBackgroundBlur",
            It.IsAny<object?[]>()), Times.Never);
    }

    // ── IsBackgroundBlurSupportedAsync Tests ────────────────────────

    [TestMethod]
    public async Task IsBackgroundBlurSupportedAsync_ReturnsTrue_WhenJsReturnsTrue()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoEffects.isSupported", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var result = await _service.IsBackgroundBlurSupportedAsync();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsBackgroundBlurSupportedAsync_ReturnsFalse_WhenJsReturnsFalse()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoEffects.isSupported", It.IsAny<object?[]>()))
            .ReturnsAsync(false);

        var result = await _service.IsBackgroundBlurSupportedAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsBackgroundBlurSupportedAsync_InvokesCorrectJsFunction()
    {
        _jsMock.Setup(js => js.InvokeAsync<bool>("dotnetcloudVideoEffects.isSupported", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        await _service.IsBackgroundBlurSupportedAsync();

        _jsMock.Verify(js => js.InvokeAsync<bool>(
            "dotnetcloudVideoEffects.isSupported",
            It.IsAny<object?[]>()), Times.Once);
    }
}
