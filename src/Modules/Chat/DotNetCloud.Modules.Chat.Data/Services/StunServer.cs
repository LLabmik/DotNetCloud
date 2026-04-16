using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Embedded STUN server (RFC 5389) that responds to Binding Requests with
/// the client's reflexive transport address. Runs as a <see cref="BackgroundService"/>
/// on a configurable UDP port (default 3478).
/// <para>
/// This eliminates the dependency on third-party STUN servers (e.g., Google)
/// for WebRTC NAT traversal, keeping user traffic metadata private.
/// </para>
/// </summary>
/// <remarks>
/// Implements only STUN Binding (0x0001) — the minimum needed for WebRTC ICE.
/// Does not implement TURN relay, STUN long-term credentials, or TLS.
/// For TURN relay, configure an external coturn server.
/// </remarks>
public sealed class StunServer : BackgroundService
{
    // STUN constants (RFC 5389)
    private const int StunHeaderLength = 20;
    private const ushort BindingRequest = 0x0001;
    private const ushort BindingResponse = 0x0101;
    private const ushort AttrXorMappedAddress = 0x0020;
    private const ushort AttrSoftware = 0x8022;
    private const uint MagicCookie = 0x2112A442;

    private static readonly byte[] SoftwareValue = "DotNetCloud STUN/1.0"u8.ToArray();

    private readonly IceServerOptions _options;
    private readonly ILogger<StunServer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StunServer"/> class.
    /// </summary>
    public StunServer(
        IOptions<IceServerOptions> options,
        ILogger<StunServer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableBuiltInStun)
        {
            _logger.LogInformation("Built-in STUN server is disabled");
            return;
        }

        var port = _options.StunPort;
        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
        socket.DualMode = true;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));

        _logger.LogInformation("Built-in STUN server listening on UDP port {Port} (dual-stack IPv4/IPv6)", port);

        var buffer = new byte[576]; // RFC 5389 minimum MTU
        var remoteEp = new IPEndPoint(IPAddress.IPv6Any, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveFromAsync(
                    buffer, SocketFlags.None, remoteEp, stoppingToken);

                var received = result.ReceivedBytes;
                var sender = (IPEndPoint)result.RemoteEndPoint;

                if (received < StunHeaderLength)
                {
                    continue;
                }

                // Validate STUN header: first 2 bits must be 0 (RFC 5389 §6)
                if ((buffer[0] & 0xC0) != 0)
                {
                    continue;
                }

                var messageType = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(0, 2));
                if (messageType != BindingRequest)
                {
                    continue;
                }

                // Validate magic cookie
                var cookie = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4, 4));
                if (cookie != MagicCookie)
                {
                    continue;
                }

                // Extract 12-byte transaction ID
                var transactionId = buffer.AsSpan(8, 12).ToArray();

                var response = BuildBindingResponse(sender, transactionId);
                await socket.SendToAsync(response, SocketFlags.None, sender, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "STUN socket error: {Message}", ex.Message);
            }
        }

        _logger.LogInformation("Built-in STUN server stopped");
    }

    /// <summary>
    /// Builds a STUN Binding Response with XOR-MAPPED-ADDRESS and SOFTWARE attributes.
    /// </summary>
    internal static byte[] BuildBindingResponse(IPEndPoint clientEndpoint, byte[] transactionId)
    {
        // Build attributes first to calculate total length
        var xorMappedAddress = BuildXorMappedAddress(clientEndpoint, transactionId);
        var software = BuildSoftwareAttribute();
        var attributesLength = xorMappedAddress.Length + software.Length;

        var response = new byte[StunHeaderLength + attributesLength];
        var span = response.AsSpan();

        // Header: type (2) + length (2) + magic cookie (4) + transaction ID (12)
        BinaryPrimitives.WriteUInt16BigEndian(span[..2], BindingResponse);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..4], (ushort)attributesLength);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..8], MagicCookie);
        transactionId.CopyTo(span[8..20]);

        // Attributes
        xorMappedAddress.CopyTo(span[StunHeaderLength..]);
        software.CopyTo(span[(StunHeaderLength + xorMappedAddress.Length)..]);

        return response;
    }

    /// <summary>
    /// Builds an XOR-MAPPED-ADDRESS attribute (RFC 5389 §15.2).
    /// The address and port are XOR'd with the magic cookie (and transaction ID for IPv6).
    /// </summary>
    internal static byte[] BuildXorMappedAddress(IPEndPoint endpoint, byte[] transactionId)
    {
        // Resolve IPv4-mapped IPv6 addresses to their IPv4 form
        var address = endpoint.Address;
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var isIpv4 = address.AddressFamily == AddressFamily.InterNetwork;
        var addressBytes = address.GetAddressBytes();

        // XOR port with top 16 bits of magic cookie
        var xorPort = (ushort)(endpoint.Port ^ (MagicCookie >> 16));

        // XOR address with magic cookie (IPv4) or magic cookie + transaction ID (IPv6)
        byte[] xorAddress;
        if (isIpv4)
        {
            var cookieBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(cookieBytes, MagicCookie);
            xorAddress = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                xorAddress[i] = (byte)(addressBytes[i] ^ cookieBytes[i]);
            }
        }
        else
        {
            // IPv6: XOR with magic cookie (4 bytes) + transaction ID (12 bytes) = 16 bytes
            var xorKey = new byte[16];
            BinaryPrimitives.WriteUInt32BigEndian(xorKey, MagicCookie);
            transactionId.CopyTo(xorKey.AsSpan(4));
            xorAddress = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                xorAddress[i] = (byte)(addressBytes[i] ^ xorKey[i]);
            }
        }

        // Attribute: type (2) + length (2) + reserved (1) + family (1) + port (2) + address (4 or 16)
        var valueLength = 4 + xorAddress.Length; // reserved + family + port + address
        var paddedValueLength = (valueLength + 3) & ~3; // pad to 4-byte boundary
        var attr = new byte[4 + paddedValueLength]; // type + length + padded value

        BinaryPrimitives.WriteUInt16BigEndian(attr.AsSpan(0, 2), AttrXorMappedAddress);
        BinaryPrimitives.WriteUInt16BigEndian(attr.AsSpan(2, 2), (ushort)valueLength);
        attr[4] = 0; // reserved
        attr[5] = isIpv4 ? (byte)0x01 : (byte)0x02; // family
        BinaryPrimitives.WriteUInt16BigEndian(attr.AsSpan(6, 2), xorPort);
        xorAddress.CopyTo(attr.AsSpan(8));

        return attr;
    }

    /// <summary>
    /// Builds a SOFTWARE attribute (RFC 5389 §15.10).
    /// </summary>
    private static byte[] BuildSoftwareAttribute()
    {
        var valueLength = SoftwareValue.Length;
        var paddedLength = (valueLength + 3) & ~3;
        var attr = new byte[4 + paddedLength];

        BinaryPrimitives.WriteUInt16BigEndian(attr.AsSpan(0, 2), AttrSoftware);
        BinaryPrimitives.WriteUInt16BigEndian(attr.AsSpan(2, 2), (ushort)valueLength);
        SoftwareValue.CopyTo(attr.AsSpan(4));

        return attr;
    }
}
