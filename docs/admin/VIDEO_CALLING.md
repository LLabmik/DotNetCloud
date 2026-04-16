# Video Calling — Admin Configuration Guide

> **Last Updated:** 2026-04-15

---

## Overview

DotNetCloud supports WebRTC-based video and audio calling from any Chat channel (Public, Private, DM, Group). Small calls (1–3 participants) use a peer-to-peer mesh. For larger groups (4+), an optional **LiveKit** SFU can be configured.

All signaling flows through the existing SignalR hub — no additional ports are required for signaling. Media traffic, however, requires UDP access for STUN/TURN.

---

## Architecture

```
Browser (WebRTC)
    │
    ├─ Signaling ──→ SignalR CoreHub (existing TCP port)
    │
    ├─ STUN ───────→ Built-in STUN server (UDP 3478)
    │
    ├─ TURN ───────→ External coturn (optional, UDP/TCP 3478+)
    │
    └─ Media ──────→ P2P mesh (1–3 users) or LiveKit SFU (4+ users)
```

---

## STUN Server Configuration

DotNetCloud includes a **built-in STUN server** (RFC 5389) that runs on UDP port 3478 by default. This is a privacy-first approach — no third-party STUN services are contacted.

### Configuration

Add to `appsettings.json`:

```json
{
  "Chat": {
    "IceServers": {
      "EnableBuiltInStun": true,
      "StunPort": 3478,
      "StunPublicHost": "stun.example.com",
      "AdditionalStunUrls": []
    }
  }
}
```

### Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `EnableBuiltInStun` | `true` | Enable the built-in STUN server |
| `StunPort` | `3478` | UDP port for the STUN server |
| `StunPublicHost` | `""` (auto) | Public hostname/IP returned to clients. If empty, uses the request's `Host` header |
| `AdditionalStunUrls` | `[]` | Optional additional STUN server URLs (e.g., `stun:stun.example.com:3478`) |

### Firewall Requirements

| Protocol | Port | Direction | Purpose |
|---|---|---|---|
| UDP | 3478 | Inbound | STUN binding requests |

Ensure your firewall allows **inbound UDP on port 3478** from client networks. If clients are behind symmetric NAT, STUN alone may not suffice — configure a TURN server (see below).

---

## TURN Server Configuration (Optional)

For clients behind restrictive NATs or firewalls that block direct P2P connections, configure a TURN relay server. DotNetCloud supports [coturn](https://github.com/coturn/coturn), the standard open-source TURN server.

### Static Credentials

For simple setups, configure static TURN credentials:

```json
{
  "Chat": {
    "IceServers": {
      "EnableTurn": true,
      "TurnUrls": [
        "turn:turn.example.com:3478",
        "turns:turn.example.com:5349"
      ],
      "TurnUsername": "dotnetcloud",
      "TurnCredential": "your-static-credential"
    }
  }
}
```

### Ephemeral Credentials (Recommended)

For production deployments, use time-limited HMAC-SHA1 credentials compatible with coturn's `use-auth-secret` mode. Credentials expire after a configurable TTL, reducing the risk if intercepted.

```json
{
  "Chat": {
    "IceServers": {
      "EnableTurn": true,
      "TurnUrls": ["turn:turn.example.com:3478"],
      "EnableEphemeralCredentials": true,
      "TurnSharedSecret": "same-secret-as-coturn",
      "CredentialTtlSeconds": 86400
    }
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `EnableTurn` | `false` | Enable TURN relay |
| `TurnUrls` | `[]` | TURN server URLs (`turn:` or `turns:` scheme) |
| `TurnUsername` | `""` | Static TURN username (ignored when ephemeral credentials are enabled) |
| `TurnCredential` | `""` | Static TURN credential (ignored when ephemeral credentials are enabled) |
| `EnableEphemeralCredentials` | `false` | Generate time-limited HMAC-SHA1 credentials (requires `TurnSharedSecret`) |
| `TurnSharedSecret` | `""` | Shared secret matching your coturn `static-auth-secret` |
| `CredentialTtlSeconds` | `86400` | Credential lifetime in seconds (default: 24 hours) |

### ICE Transport Policy

Control whether clients may use direct P2P connections or must relay all media through TURN:

```json
{
  "Chat": {
    "IceServers": {
      "IceTransportPolicy": "all"
    }
  }
}
```

| Value | Description |
|---|---|
| `all` (default) | Use the best available path — direct P2P if possible, TURN as fallback |
| `relay` | Force all media through TURN relay (useful for strict network policies) |

### coturn Setup Guide

1. **Install coturn:**

   ```bash
   sudo apt install coturn
   ```

2. **Configure `/etc/turnserver.conf`:**

   ```ini
   listening-port=3478
   tls-listening-port=5349
   realm=turn.example.com
   use-auth-secret
   static-auth-secret=same-secret-as-dotnetcloud
   total-quota=100
   max-bps=0
   stale-nonce=600
   cert=/etc/letsencrypt/live/turn.example.com/fullchain.pem
   pkey=/etc/letsencrypt/live/turn.example.com/privkey.pem
   no-multicast-peers
   ```

3. **Open firewall ports:**

   | Protocol | Port | Purpose |
   |---|---|---|
   | UDP/TCP | 3478 | TURN plain |
   | UDP/TCP | 5349 | TURN over TLS |
   | UDP | 49152–65535 | Media relay range |

4. **Start coturn:**

   ```bash
   sudo systemctl enable coturn
   sudo systemctl start coturn
   ```

5. **Verify:** Use a WebRTC tester (e.g., [Trickle ICE](https://webrtc.github.io/samples/src/content/peerconnection/trickle-ice/)) with the configured TURN URL and credentials.

---

## LiveKit SFU Configuration (Optional)

For group calls with 4+ participants, DotNetCloud can delegate media routing to a [LiveKit](https://livekit.io/) SFU server. When LiveKit is not configured, calls with 4+ participants fall back to a P2P mesh (which degrades quality at higher participant counts).

### Configuration

```json
{
  "Chat": {
    "LiveKit": {
      "Enabled": true,
      "ServerUrl": "wss://livekit.example.com",
      "ApiKey": "your-api-key",
      "ApiSecret": "your-api-secret",
      "DefaultMaxParticipants": 50,
      "TokenTtlSeconds": 3600,
      "MaxP2PParticipants": 3,
      "EmptyRoomTimeoutSeconds": 300
    }
  }
}
```

### Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `false` | Enable LiveKit SFU integration |
| `ServerUrl` | `""` | LiveKit server WebSocket URL (e.g., `wss://livekit.example.com`) |
| `ApiKey` | `""` | LiveKit API key |
| `ApiSecret` | `""` | LiveKit API secret |
| `DefaultMaxParticipants` | `50` | Max participants per LiveKit room |
| `TokenTtlSeconds` | `3600` | JWT token lifetime (seconds) |
| `MaxP2PParticipants` | `3` | Threshold for P2P → SFU escalation. Calls with more participants than this use LiveKit |
| `EmptyRoomTimeoutSeconds` | `300` | Seconds before an empty LiveKit room is automatically deleted |

### LiveKit Setup

1. **Install LiveKit** (Docker recommended):

   ```bash
   docker run -d --name livekit \
     -p 7880:7880 -p 7881:7881 -p 7882:7882/udp \
     -e LIVEKIT_KEYS="your-api-key: your-api-secret" \
     livekit/livekit-server
   ```

2. **Generate API key pair** (use the [LiveKit CLI](https://docs.livekit.io/home/cli/)):

   ```bash
   livekit-cli create-token --api-key your-api-key --api-secret your-api-secret
   ```

3. **Configure DotNetCloud** with the `ServerUrl`, `ApiKey`, and `ApiSecret` from above.

### How Escalation Works

1. Call starts → participant count ≤ `MaxP2PParticipants` → P2P mesh
2. New participant joins → count exceeds threshold → DotNetCloud creates a LiveKit room
3. All participants receive a LiveKit token and migrate from P2P mesh to the SFU
4. When all participants leave, the room is deleted after `EmptyRoomTimeoutSeconds`

### Without LiveKit

If LiveKit is not configured (`Enabled: false`), calls with 4+ participants still work via P2P mesh. Quality may degrade with many participants because each peer must send/receive streams to/from all other peers. For best experience with large groups, configure LiveKit.

---

## Call Behavior

| Setting | Value | Description |
|---|---|---|
| Calls per channel | 1 | Only one active call per channel at any time |
| Ring timeout | 30 seconds | Unanswered calls are marked as `Missed` after 30 seconds |
| Max participants (P2P) | 3 | P2P mesh supports up to 3 participants |
| Max participants (SFU) | 50 (configurable) | LiveKit rooms support up to `DefaultMaxParticipants` |

---

## Security Considerations

- **No third-party STUN by default.** The built-in STUN server prevents IP leaking to external services.
- **Ephemeral TURN credentials** rotate automatically and cannot be reused after expiry.
- **Call authorization** is enforced per-request — only channel members can initiate, join, or view calls.
- **Signaling payloads** (SDP offers/answers, ICE candidates) are validated for size limits before relay.
- **LiveKit tokens** are short-lived JWTs scoped to the specific room and participant.
- **Rate limiting** is applied to call initiation endpoints to prevent abuse.

---

## Troubleshooting

### Calls fail to connect

1. Verify STUN server is reachable: `stun:your-server:3478` (UDP)
2. Check firewall allows inbound UDP on port 3478
3. If clients are behind symmetric NAT, configure a TURN server
4. Check browser console for WebRTC ICE connection state errors

### Media quality degrades with many participants

- Configure LiveKit SFU for calls with 4+ participants
- Check network bandwidth — each P2P peer needs upload bandwidth for every other peer

### TURN credentials rejected

1. Verify `TurnSharedSecret` matches coturn's `static-auth-secret`
2. Check server time synchronization (ephemeral credentials are time-based)
3. Ensure `CredentialTtlSeconds` is long enough for clock skew between servers

### LiveKit rooms not created

1. Verify `ServerUrl`, `ApiKey`, and `ApiSecret` are correct
2. Check LiveKit server logs for authentication errors
3. Ensure the LiveKit server is reachable from the DotNetCloud host

---

## Related Documentation

- [Chat Module Overview](../modules/chat/README.md)
- [Chat REST API Reference](../modules/chat/API.md)
- [Chat Real-Time Events](../modules/chat/REALTIME.md)
- [Push Notifications](../modules/chat/PUSH.md)
