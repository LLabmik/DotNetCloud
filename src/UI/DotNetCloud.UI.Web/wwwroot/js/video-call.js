/**
 * DotNetCloud — Client-Side WebRTC Engine (JS Interop)
 * Phase 7.5: P2P mesh video calling with STUN/TURN support.
 *
 * Namespace: window.dotnetcloudVideoCall
 * Pattern: IIFE returning public API surface for Blazor JS interop.
 */
window.dotnetcloudVideoCall = window.dotnetcloudVideoCall || (function () {
    "use strict";

    // ── Constants ──────────────────────────────────────────────
    const MAX_PEERS = 3;
    const STATS_INTERVAL_MS = 5000;
    const DEFAULT_VIDEO_CONSTRAINTS = { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 30 } };
    const SCREEN_SHARE_CONSTRAINTS = { video: { cursor: "always", displaySurface: "monitor" }, audio: false };
    const BITRATE_THRESHOLDS = { good: 1500000, fair: 800000, poor: 400000 };

    // ── State ──────────────────────────────────────────────────
    /** @type {DotNet.DotNetObject|null} */
    let dotNetRef = null;
    /** @type {RTCConfiguration|null} */
    let rtcConfig = null;
    /** @type {MediaStream|null} */
    let localStream = null;
    /** @type {MediaStream|null} */
    let screenStream = null;
    /** @type {Map<string, RTCPeerConnection>} peerId → RTCPeerConnection */
    const peerConnections = new Map();
    /** @type {Map<string, MediaStream>} peerId → remote MediaStream */
    const remoteStreams = new Map();
    /** @type {Map<string, number>} peerId → stats interval timer */
    const statsTimers = new Map();
    /** @type {string|null} */
    let currentCallId = null;
    let isScreenSharing = false;
    let screenShareWasAdded = false; // true if screen track was added (not replaced)
    let isInitialized = false;

    // ── Initialization ─────────────────────────────────────────

    /**
     * Initialize the WebRTC engine with ICE server config and Blazor callback ref.
     * @param {DotNet.DotNetObject} blazorRef - Blazor DotNetObjectReference for callbacks.
     * @param {object} config - ICE server configuration.
     * @param {string} config.callId - The call identifier.
     * @param {Array<{urls: string|string[], username?: string, credential?: string}>} config.iceServers - ICE servers.
     * @param {string} [config.iceTransportPolicy] - "all" or "relay".
     */
    function initializeCall(blazorRef, config) {
        if (!blazorRef) {
            console.error("[VideoCall] blazorRef is required");
            return false;
        }
        if (!config || !config.callId) {
            console.error("[VideoCall] config.callId is required");
            return false;
        }

        dotNetRef = blazorRef;
        currentCallId = config.callId;

        rtcConfig = {
            iceServers: config.iceServers || [],
            iceCandidatePoolSize: 2,
            bundlePolicy: "max-bundle",
            rtcpMuxPolicy: "require"
        };
        if (config.iceTransportPolicy) {
            rtcConfig.iceTransportPolicy = config.iceTransportPolicy;
        }

        isInitialized = true;
        console.log("[VideoCall] Initialized for call:", currentCallId);
        return true;
    }

    // ── Local Media ────────────────────────────────────────────

    /**
     * Start capturing local media (camera + microphone).
     * @param {object} [constraints] - Optional getUserMedia constraints override.
     * @returns {Promise<string|null>} The local stream ID on success, null on failure.
     */
    async function startLocalMedia(constraints) {
        try {
            var mediaConstraints = constraints || {
                audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
                video: DEFAULT_VIDEO_CONSTRAINTS
            };

            localStream = await navigator.mediaDevices.getUserMedia(mediaConstraints);
            console.log("[VideoCall] Local media started, tracks:", localStream.getTracks().map(function (t) { return t.kind; }));
            return localStream.id;
        } catch (e) {
            console.error("[VideoCall] getUserMedia failed:", e.name, e.message);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnWebRtcError", "getUserMedia", e.name + ": " + e.message);
            }
            return null;
        }
    }

    /**
     * Start screen sharing via getDisplayMedia.
     * @returns {Promise<string|null>} The screen stream ID on success, null on failure.
     */
    async function startScreenShare() {
        if (isScreenSharing) {
            console.warn("[VideoCall] Screen share already active");
            return screenStream ? screenStream.id : null;
        }

        try {
            screenStream = await navigator.mediaDevices.getDisplayMedia(SCREEN_SHARE_CONSTRAINTS);

            // Listen for user stopping screen share via browser UI
            var videoTrack = screenStream.getVideoTracks()[0];
            if (videoTrack) {
                videoTrack.addEventListener("ended", function () {
                    handleScreenShareEnded();
                });
            }

            // Replace video track in all peer connections, or add if no camera track exists
            var screenTrack = screenStream.getVideoTracks()[0];
            if (screenTrack) {
                var cameraTrack = localStream ? localStream.getVideoTracks()[0] : null;
                if (cameraTrack) {
                    replaceTrackInAllPeers(cameraTrack, screenTrack);
                    screenShareWasAdded = false;
                } else {
                    // No camera track — add screen track directly to all peers
                    addTrackToAllPeers(screenTrack, screenStream);
                    screenShareWasAdded = true;
                }
            }

            isScreenSharing = true;
            console.log("[VideoCall] Screen share started");

            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnScreenShareStateChanged", true);
            }
            return screenStream.id;
        } catch (e) {
            console.error("[VideoCall] getDisplayMedia failed:", e.name, e.message);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnWebRtcError", "getDisplayMedia", e.name + ": " + e.message);
            }
            return null;
        }
    }

    /**
     * Stop screen sharing and revert to camera video.
     */
    function stopScreenShare() {
        if (!isScreenSharing || !screenStream) return;

        var screenTrack = screenStream.getVideoTracks()[0];
        var cameraTrack = localStream ? localStream.getVideoTracks()[0] : null;

        if (screenShareWasAdded) {
            // Screen track was added directly — remove it from all peers
            if (screenTrack) {
                removeTrackFromAllPeers(screenTrack);
            }
        } else if (cameraTrack && screenTrack) {
            // Screen track replaced camera — swap back
            replaceTrackInAllPeers(screenTrack, cameraTrack);
        }

        // Stop all screen share tracks
        screenStream.getTracks().forEach(function (t) { t.stop(); });
        screenStream = null;
        isScreenSharing = false;
        screenShareWasAdded = false;

        console.log("[VideoCall] Screen share stopped");
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnScreenShareStateChanged", false);
        }
    }

    /** Internal handler when user stops screen share via browser chrome. */
    function handleScreenShareEnded() {
        if (!isScreenSharing) return;
        var cameraTrack = localStream ? localStream.getVideoTracks()[0] : null;
        var screenTrack = screenStream ? screenStream.getVideoTracks()[0] : null;

        if (screenShareWasAdded) {
            if (screenTrack) {
                removeTrackFromAllPeers(screenTrack);
            }
        } else if (cameraTrack && screenTrack) {
            replaceTrackInAllPeers(screenTrack, cameraTrack);
        }

        if (screenStream) {
            screenStream.getTracks().forEach(function (t) { t.stop(); });
        }
        screenStream = null;
        isScreenSharing = false;
        screenShareWasAdded = false;

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnScreenShareStateChanged", false);
        }
    }

    // ── Peer Connection Management (P2P Mesh) ──────────────────

    /**
     * Create a new RTCPeerConnection for a remote peer.
     * @param {string} peerId - The remote user ID.
     * @returns {RTCPeerConnection|null} The connection, or null if max peers exceeded.
     */
    function createPeerConnection(peerId) {
        if (!isInitialized) {
            console.error("[VideoCall] Not initialized");
            return null;
        }
        if (peerConnections.has(peerId)) {
            console.warn("[VideoCall] Peer connection already exists for:", peerId);
            return peerConnections.get(peerId);
        }
        if (peerConnections.size >= MAX_PEERS) {
            console.error("[VideoCall] Max peers (" + MAX_PEERS + ") reached");
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnWebRtcError", "maxPeers", "Maximum peer limit reached (" + MAX_PEERS + ")");
            }
            return null;
        }

        var pc = new RTCPeerConnection(rtcConfig);
        peerConnections.set(peerId, pc);

        // Add local tracks to the connection
        if (localStream) {
            localStream.getTracks().forEach(function (track) {
                pc.addTrack(track, localStream);
            });
        }

        // ICE candidate handler
        pc.onicecandidate = function (event) {
            if (event.candidate && dotNetRef) {
                dotNetRef.invokeMethodAsync("OnIceCandidate", peerId, JSON.stringify(event.candidate));
            }
        };

        // ICE gathering state
        pc.onicegatheringstatechange = function () {
            console.log("[VideoCall] ICE gathering state (" + peerId + "):", pc.iceGatheringState);
        };

        // Connection state changes
        pc.onconnectionstatechange = function () {
            var state = pc.connectionState;
            console.log("[VideoCall] Connection state (" + peerId + "):", state);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnConnectionStateChanged", peerId, state);
            }
            if (state === "failed" || state === "disconnected") {
                handlePeerDisconnected(peerId, state);
            }
        };

        // ICE connection state
        pc.oniceconnectionstatechange = function () {
            console.log("[VideoCall] ICE connection state (" + peerId + "):", pc.iceConnectionState);
        };

        // Remote tracks
        pc.ontrack = function (event) {
            console.log("[VideoCall] Remote track received from", peerId, "kind:", event.track.kind);
            var stream = event.streams[0];
            if (stream) {
                remoteStreams.set(peerId, stream);
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync("OnRemoteStream", peerId, stream.id, event.track.kind);
                }

                // Track ended handler
                event.track.onended = function () {
                    console.log("[VideoCall] Remote track ended from", peerId, "kind:", event.track.kind);
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync("OnRemoteTrackEnded", peerId, event.track.kind);
                    }
                };
            }
        };

        // Negotiation needed (renegotiation after track changes)
        pc.onnegotiationneeded = function () {
            console.log("[VideoCall] Negotiation needed for peer:", peerId);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnNegotiationNeeded", peerId);
            }
        };

        // Start stats monitoring
        startStatsMonitoring(peerId, pc);

        console.log("[VideoCall] Peer connection created for:", peerId);
        return pc;
    }

    /**
     * Handle peer disconnection or failure.
     * @param {string} peerId
     * @param {string} state
     */
    function handlePeerDisconnected(peerId, state) {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnPeerDisconnected", peerId, state);
        }
    }

    // ── SDP Negotiation ────────────────────────────────────────

    /**
     * Create an SDP offer for a peer.
     * @param {string} peerId - The remote user ID.
     * @returns {Promise<string|null>} The SDP offer string, or null on failure.
     */
    async function createOffer(peerId) {
        var pc = peerConnections.get(peerId);
        if (!pc) {
            pc = createPeerConnection(peerId);
            if (!pc) return null;
        }

        try {
            var offer = await pc.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true
            });
            await pc.setLocalDescription(offer);
            console.log("[VideoCall] Offer created for:", peerId);
            return JSON.stringify(pc.localDescription);
        } catch (e) {
            console.error("[VideoCall] createOffer failed:", e.message);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnWebRtcError", "createOffer", e.message);
            }
            return null;
        }
    }

    /**
     * Handle an incoming SDP offer from a remote peer.
     * @param {string} peerId - The remote user ID.
     * @param {string} sdpJson - JSON-serialized RTCSessionDescription.
     * @returns {Promise<string|null>} The SDP answer string, or null on failure.
     */
    async function handleOffer(peerId, sdpJson) {
        var pc = peerConnections.get(peerId);
        if (!pc) {
            pc = createPeerConnection(peerId);
            if (!pc) return null;
        }

        try {
            var offer = JSON.parse(sdpJson);
            await pc.setRemoteDescription(new RTCSessionDescription(offer));
            var answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            console.log("[VideoCall] Offer handled, answer created for:", peerId);
            return JSON.stringify(pc.localDescription);
        } catch (e) {
            console.error("[VideoCall] handleOffer failed:", e.message);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnWebRtcError", "handleOffer", e.message);
            }
            return null;
        }
    }

    /**
     * Handle an incoming SDP answer from a remote peer.
     * @param {string} peerId - The remote user ID.
     * @param {string} sdpJson - JSON-serialized RTCSessionDescription.
     * @returns {Promise<boolean>} True on success.
     */
    async function handleAnswer(peerId, sdpJson) {
        var pc = peerConnections.get(peerId);
        if (!pc) {
            console.error("[VideoCall] No peer connection for:", peerId);
            return false;
        }

        try {
            var answer = JSON.parse(sdpJson);
            await pc.setRemoteDescription(new RTCSessionDescription(answer));
            console.log("[VideoCall] Answer handled for:", peerId);
            return true;
        } catch (e) {
            console.error("[VideoCall] handleAnswer failed:", e.message);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnWebRtcError", "handleAnswer", e.message);
            }
            return false;
        }
    }

    // ── ICE Candidate Handling ─────────────────────────────────

    /**
     * Add an ICE candidate received from a remote peer.
     * @param {string} peerId - The remote user ID.
     * @param {string} candidateJson - JSON-serialized RTCIceCandidate.
     * @returns {Promise<boolean>} True on success.
     */
    async function addIceCandidate(peerId, candidateJson) {
        var pc = peerConnections.get(peerId);
        if (!pc) {
            console.warn("[VideoCall] No peer connection for ICE candidate, peerId:", peerId);
            return false;
        }

        try {
            var candidate = JSON.parse(candidateJson);
            await pc.addIceCandidate(new RTCIceCandidate(candidate));
            return true;
        } catch (e) {
            // Suppress benign errors during early negotiation
            if (e.name !== "InvalidStateError") {
                console.error("[VideoCall] addIceCandidate failed:", e.message);
            }
            return false;
        }
    }

    // ── Track Controls ─────────────────────────────────────────

    /**
     * Toggle local audio track enabled/disabled.
     * @param {boolean} enabled
     */
    function toggleAudio(enabled) {
        if (!localStream) return;
        localStream.getAudioTracks().forEach(function (track) {
            track.enabled = !!enabled;
        });
        console.log("[VideoCall] Audio", enabled ? "unmuted" : "muted");
    }

    /**
     * Toggle local video track enabled/disabled.
     * @param {boolean} enabled
     */
    function toggleVideo(enabled) {
        if (!localStream) return;
        localStream.getVideoTracks().forEach(function (track) {
            track.enabled = !!enabled;
        });
        console.log("[VideoCall] Video", enabled ? "enabled" : "disabled");
    }

    // ── Adaptive Bitrate ───────────────────────────────────────

    /**
     * Start periodic stats monitoring for a peer connection.
     * @param {string} peerId
     * @param {RTCPeerConnection} pc
     */
    function startStatsMonitoring(peerId, pc) {
        if (statsTimers.has(peerId)) return;

        var timer = setInterval(async function () {
            if (pc.connectionState !== "connected") return;

            try {
                var stats = await pc.getStats();
                var report = processStats(stats, peerId);
                if (report) {
                    adjustBitrate(peerId, pc, report);
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync("OnConnectionQualityUpdate", peerId,
                            report.quality, report.roundTripTime, report.availableBandwidth);
                    }
                }
            } catch (e) {
                // Stats collection can fail during teardown; ignore
            }
        }, STATS_INTERVAL_MS);

        statsTimers.set(peerId, timer);
    }

    /**
     * Process WebRTC stats into a quality report.
     * @param {RTCStatsReport} stats
     * @param {string} peerId
     * @returns {{quality: string, roundTripTime: number, availableBandwidth: number, packetLoss: number}|null}
     */
    function processStats(stats, peerId) {
        var roundTripTime = 0;
        var availableBandwidth = 0;
        var packetsSent = 0;
        var packetsLost = 0;

        stats.forEach(function (report) {
            if (report.type === "candidate-pair" && report.state === "succeeded") {
                roundTripTime = report.currentRoundTripTime || 0;
                availableBandwidth = report.availableOutgoingBitrate || 0;
            }
            if (report.type === "outbound-rtp" && report.kind === "video") {
                packetsSent = report.packetsSent || 0;
            }
            if (report.type === "remote-inbound-rtp" && report.kind === "video") {
                packetsLost = report.packetsLost || 0;
            }
        });

        var packetLoss = packetsSent > 0 ? (packetsLost / packetsSent) * 100 : 0;
        var quality;
        if (availableBandwidth >= BITRATE_THRESHOLDS.good && roundTripTime < 0.15 && packetLoss < 2) {
            quality = "good";
        } else if (availableBandwidth >= BITRATE_THRESHOLDS.fair && roundTripTime < 0.3 && packetLoss < 5) {
            quality = "fair";
        } else {
            quality = "poor";
        }

        return { quality: quality, roundTripTime: roundTripTime, availableBandwidth: availableBandwidth, packetLoss: packetLoss };
    }

    /**
     * Adjust video bitrate based on connection quality.
     * @param {string} peerId
     * @param {RTCPeerConnection} pc
     * @param {{quality: string, availableBandwidth: number}} report
     */
    function adjustBitrate(peerId, pc, report) {
        var senders = pc.getSenders();
        var videoSender = null;
        for (var i = 0; i < senders.length; i++) {
            if (senders[i].track && senders[i].track.kind === "video") {
                videoSender = senders[i];
                break;
            }
        }
        if (!videoSender) return;

        var params = videoSender.getParameters();
        if (!params.encodings || params.encodings.length === 0) {
            params.encodings = [{}];
        }

        var targetBitrate;
        switch (report.quality) {
            case "good":
                targetBitrate = 1500000; // 1.5 Mbps
                break;
            case "fair":
                targetBitrate = 800000; // 800 Kbps
                break;
            case "poor":
                targetBitrate = 400000; // 400 Kbps
                break;
            default:
                targetBitrate = 800000;
        }

        var currentMax = params.encodings[0].maxBitrate;
        if (currentMax !== targetBitrate) {
            params.encodings[0].maxBitrate = targetBitrate;
            videoSender.setParameters(params).catch(function (e) {
                console.warn("[VideoCall] setParameters failed:", e.message);
            });
            console.log("[VideoCall] Bitrate adjusted for", peerId, "→", targetBitrate / 1000, "kbps (quality:", report.quality + ")");
        }
    }

    // ── Track Replacement (for screen share swap) ──────────────

    /**
     * Replace a track in all peer connections.
     * @param {MediaStreamTrack} oldTrack
     * @param {MediaStreamTrack} newTrack
     */
    function replaceTrackInAllPeers(oldTrack, newTrack) {
        peerConnections.forEach(function (pc, peerId) {
            var senders = pc.getSenders();
            for (var i = 0; i < senders.length; i++) {
                if (senders[i].track && senders[i].track.id === oldTrack.id) {
                    senders[i].replaceTrack(newTrack).catch(function (e) {
                        console.error("[VideoCall] replaceTrack failed for peer:", peerId, e.message);
                    });
                    break;
                }
            }
        });
    }

    /**
     * Add a track to all peer connections (triggers renegotiation).
     * Used when there's no existing track to replace (e.g., screen share without camera).
     * @param {MediaStreamTrack} track
     * @param {MediaStream} stream
     */
    function addTrackToAllPeers(track, stream) {
        peerConnections.forEach(function (pc, peerId) {
            try {
                pc.addTrack(track, stream);
                console.log("[VideoCall] Added track to peer:", peerId, "kind:", track.kind);
            } catch (e) {
                console.error("[VideoCall] addTrack failed for peer:", peerId, e.message);
            }
        });
    }

    /**
     * Remove a track from all peer connections (triggers renegotiation).
     * Used when stopping screen share that was added (not replaced).
     * @param {MediaStreamTrack} track
     */
    function removeTrackFromAllPeers(track) {
        peerConnections.forEach(function (pc, peerId) {
            var senders = pc.getSenders();
            for (var i = 0; i < senders.length; i++) {
                if (senders[i].track && senders[i].track.id === track.id) {
                    try {
                        pc.removeTrack(senders[i]);
                        console.log("[VideoCall] Removed track from peer:", peerId, "kind:", track.kind);
                    } catch (e) {
                        console.error("[VideoCall] removeTrack failed for peer:", peerId, e.message);
                    }
                    break;
                }
            }
        });
    }

    // ── Stream Access (for Blazor video element binding) ───────

    /**
     * Attach a stream to a video HTML element.
     * @param {string} elementId - The DOM element ID.
     * @param {string} streamType - "local", "screen", or a peerId for remote.
     * @returns {boolean} True if attached.
     */
    function attachStreamToElement(elementId, streamType) {
        var el = document.getElementById(elementId);
        if (!el) {
            console.error("[VideoCall] Element not found:", elementId);
            return false;
        }

        var stream = null;
        if (streamType === "local") {
            stream = localStream;
        } else if (streamType === "screen") {
            stream = screenStream;
        } else {
            stream = remoteStreams.get(streamType);
        }

        if (!stream) {
            console.warn("[VideoCall] No stream found for:", streamType);
            return false;
        }

        el.srcObject = stream;
        return true;
    }

    /**
     * Detach a stream from a video HTML element.
     * @param {string} elementId - The DOM element ID.
     */
    function detachStreamFromElement(elementId) {
        var el = document.getElementById(elementId);
        if (el) {
            el.srcObject = null;
        }
    }

    // ── Cleanup ────────────────────────────────────────────────

    /**
     * Close a specific peer connection.
     * @param {string} peerId
     */
    function closePeerConnection(peerId) {
        var timer = statsTimers.get(peerId);
        if (timer) {
            clearInterval(timer);
            statsTimers.delete(peerId);
        }

        var pc = peerConnections.get(peerId);
        if (pc) {
            pc.onicecandidate = null;
            pc.onconnectionstatechange = null;
            pc.oniceconnectionstatechange = null;
            pc.onicegatheringstatechange = null;
            pc.ontrack = null;
            pc.onnegotiationneeded = null;
            pc.close();
            peerConnections.delete(peerId);
        }

        remoteStreams.delete(peerId);
        console.log("[VideoCall] Peer connection closed:", peerId);
    }

    /**
     * Hang up: close all peer connections, stop all tracks, full cleanup.
     */
    function hangup() {
        console.log("[VideoCall] Hanging up call:", currentCallId);

        // Close all peer connections
        var peerIds = Array.from(peerConnections.keys());
        for (var i = 0; i < peerIds.length; i++) {
            closePeerConnection(peerIds[i]);
        }

        // Stop screen share tracks
        if (screenStream) {
            screenStream.getTracks().forEach(function (t) { t.stop(); });
            screenStream = null;
        }
        isScreenSharing = false;

        // Stop local media tracks
        if (localStream) {
            localStream.getTracks().forEach(function (t) { t.stop(); });
            localStream = null;
        }

        // Clear all state
        peerConnections.clear();
        remoteStreams.clear();
        statsTimers.forEach(function (t) { clearInterval(t); });
        statsTimers.clear();
        currentCallId = null;
        isInitialized = false;

        console.log("[VideoCall] Cleanup complete");
    }

    /**
     * Dispose: full teardown, release Blazor reference.
     */
    function dispose() {
        hangup();
        dotNetRef = null;
        rtcConfig = null;
    }

    // ── Query Methods ──────────────────────────────────────────

    /**
     * Get the current call state summary.
     * @returns {{callId: string|null, peerCount: number, isScreenSharing: boolean, hasLocalMedia: boolean, peers: string[]}}
     */
    function getCallState() {
        return {
            callId: currentCallId,
            peerCount: peerConnections.size,
            isScreenSharing: isScreenSharing,
            hasLocalMedia: localStream !== null,
            peers: Array.from(peerConnections.keys())
        };
    }

    /**
     * Check if a peer connection exists and its state.
     * @param {string} peerId
     * @returns {{exists: boolean, connectionState: string|null, iceConnectionState: string|null, iceGatheringState: string|null}}
     */
    function getPeerState(peerId) {
        var pc = peerConnections.get(peerId);
        if (!pc) {
            return { exists: false, connectionState: null, iceConnectionState: null, iceGatheringState: null };
        }
        return {
            exists: true,
            connectionState: pc.connectionState,
            iceConnectionState: pc.iceConnectionState,
            iceGatheringState: pc.iceGatheringState
        };
    }

    /**
     * Get local media track states.
     * @returns {{hasAudio: boolean, audioEnabled: boolean, hasVideo: boolean, videoEnabled: boolean, isScreenSharing: boolean}}
     */
    function getMediaState() {
        var audioTracks = localStream ? localStream.getAudioTracks() : [];
        var videoTracks = localStream ? localStream.getVideoTracks() : [];
        return {
            hasAudio: audioTracks.length > 0,
            audioEnabled: audioTracks.length > 0 && audioTracks[0].enabled,
            hasVideo: videoTracks.length > 0,
            videoEnabled: videoTracks.length > 0 && videoTracks[0].enabled,
            isScreenSharing: isScreenSharing
        };
    }

    // ── Fullscreen ─────────────────────────────────────────────

    function toggleFullscreen(elementId) {
        var el = document.getElementById(elementId);
        if (!el) return;

        if (document.fullscreenElement === el) {
            document.exitFullscreen();
        } else {
            el.requestFullscreen().catch(function () { /* best-effort */ });
        }
    }

    // ── Public API ─────────────────────────────────────────────

    return {
        // Initialization
        initializeCall: initializeCall,
        // Local media
        startLocalMedia: startLocalMedia,
        startScreenShare: startScreenShare,
        stopScreenShare: stopScreenShare,
        // Peer connections
        createPeerConnection: createPeerConnection,
        closePeerConnection: closePeerConnection,
        // SDP negotiation
        createOffer: createOffer,
        handleOffer: handleOffer,
        handleAnswer: handleAnswer,
        // ICE
        addIceCandidate: addIceCandidate,
        // Track controls
        toggleAudio: toggleAudio,
        toggleVideo: toggleVideo,
        // Stream binding
        attachStreamToElement: attachStreamToElement,
        detachStreamFromElement: detachStreamFromElement,
        // Lifecycle
        hangup: hangup,
        dispose: dispose,
        // Query
        getCallState: getCallState,
        getPeerState: getPeerState,
        getMediaState: getMediaState,
        // Fullscreen
        toggleFullscreen: toggleFullscreen
    };
})();
