using DotNetCloud.Modules.Chat.Data.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Buffers.Binary;
using System.Net;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="StunServer"/> packet building (RFC 5389).
/// Tests the static/internal STUN protocol methods without requiring a live UDP socket.
/// </summary>
[TestClass]
public sealed class StunServerTests
{
    private static readonly byte[] TestTransactionId = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C];

    // ── BuildBindingResponse ─────────────────────────────────────

    [TestMethod]
    public void BuildBindingResponse_IPv4_ReturnsValidStunPacket()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 12345);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);

        // Minimum: 20 header + XOR-MAPPED-ADDRESS(12) + SOFTWARE
        Assert.IsTrue(response.Length >= 20);

        // Check message type: Binding Response (0x0101)
        var messageType = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        Assert.AreEqual(0x0101, messageType);

        // Check magic cookie
        var cookie = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(4, 4));
        Assert.AreEqual(0x2112A442u, cookie);

        // Check transaction ID
        CollectionAssert.AreEqual(TestTransactionId, response[8..20]);
    }

    [TestMethod]
    public void BuildBindingResponse_IPv4_MessageLengthMatchesAttributes()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 8080);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);

        var messageLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
        Assert.AreEqual(response.Length - 20, messageLength); // total - header = attributes
    }

    [TestMethod]
    public void BuildBindingResponse_IPv6_ReturnsValidStunPacket()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("2001:db8::1"), 54321);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);

        Assert.IsTrue(response.Length >= 20);
        var messageType = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        Assert.AreEqual(0x0101, messageType);
    }

    // ── BuildXorMappedAddress ────────────────────────────────────

    [TestMethod]
    public void BuildXorMappedAddress_IPv4_CorrectAttributeType()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 12345);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        var attrType = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(0, 2));
        Assert.AreEqual(0x0020, attrType); // XOR-MAPPED-ADDRESS
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv4_ValueLength8()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 12345);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        var valueLength = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(2, 2));
        Assert.AreEqual(8, valueLength); // 1(reserved) + 1(family) + 2(port) + 4(IPv4 address)
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv4_Family01()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 3478);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        Assert.AreEqual(0x00, attr[4]); // reserved
        Assert.AreEqual(0x01, attr[5]); // IPv4 family
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv6_Family02()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("2001:db8::1"), 3478);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        Assert.AreEqual(0x00, attr[4]); // reserved
        Assert.AreEqual(0x02, attr[5]); // IPv6 family
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv6_ValueLength20()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("2001:db8::1"), 3478);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        var valueLength = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(2, 2));
        Assert.AreEqual(20, valueLength); // 1(reserved) + 1(family) + 2(port) + 16(IPv6 address)
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv4_XorPortIsCorrect()
    {
        var port = 12345;
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), port);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        var xorPort = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(6, 2));
        var expectedXorPort = (ushort)(port ^ (0x2112A442 >> 16)); // XOR with top 16 bits of magic cookie
        Assert.AreEqual(expectedXorPort, xorPort);
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv4_XorAddressIsCorrect()
    {
        var ip = IPAddress.Parse("192.168.1.100");
        var endpoint = new IPEndPoint(ip, 8080);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        // XOR address with magic cookie bytes
        var ipBytes = ip.GetAddressBytes();
        var cookieBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(cookieBytes, 0x2112A442);

        var expectedXorAddress = new byte[4];
        for (var i = 0; i < 4; i++)
            expectedXorAddress[i] = (byte)(ipBytes[i] ^ cookieBytes[i]);

        CollectionAssert.AreEqual(expectedXorAddress, attr[8..12]);
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv6_XorAddressUsesTransactionId()
    {
        var ip = IPAddress.Parse("2001:0db8:0000:0000:0000:0000:0000:0001");
        var endpoint = new IPEndPoint(ip, 3478);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        // XOR key = magic cookie (4 bytes) + transaction ID (12 bytes)
        var xorKey = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(xorKey, 0x2112A442);
        TestTransactionId.CopyTo(xorKey.AsSpan(4));

        var ipBytes = ip.GetAddressBytes();
        var expectedXorAddress = new byte[16];
        for (var i = 0; i < 16; i++)
            expectedXorAddress[i] = (byte)(ipBytes[i] ^ xorKey[i]);

        CollectionAssert.AreEqual(expectedXorAddress, attr[8..24]);
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv4Mapped_TreatedAsIPv4()
    {
        // IPv4-mapped IPv6: ::ffff:192.168.1.1
        var ip = IPAddress.Parse("192.168.1.1").MapToIPv6();
        var endpoint = new IPEndPoint(ip, 3478);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        Assert.AreEqual(0x01, attr[5]); // Should be IPv4 family
        var valueLength = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(2, 2));
        Assert.AreEqual(8, valueLength); // IPv4 size
    }

    // ── Round-trip decode verification ───────────────────────────

    [TestMethod]
    public void BuildXorMappedAddress_IPv4_RoundTripDecode()
    {
        var originalIp = IPAddress.Parse("203.0.113.42");
        var originalPort = 54321;
        var endpoint = new IPEndPoint(originalIp, originalPort);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        // Decode: un-XOR the port
        var xorPort = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(6, 2));
        var decodedPort = xorPort ^ (0x2112A442 >> 16);
        Assert.AreEqual(originalPort, decodedPort);

        // Decode: un-XOR the address
        var cookieBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(cookieBytes, 0x2112A442);
        var decodedIpBytes = new byte[4];
        for (var i = 0; i < 4; i++)
            decodedIpBytes[i] = (byte)(attr[8 + i] ^ cookieBytes[i]);

        var decodedIp = new IPAddress(decodedIpBytes);
        Assert.AreEqual(originalIp, decodedIp);
    }

    [TestMethod]
    public void BuildXorMappedAddress_IPv6_RoundTripDecode()
    {
        var originalIp = IPAddress.Parse("2001:db8:85a3::8a2e:370:7334");
        var originalPort = 9999;
        var endpoint = new IPEndPoint(originalIp, originalPort);
        var attr = StunServer.BuildXorMappedAddress(endpoint, TestTransactionId);

        // Decode port
        var xorPort = BinaryPrimitives.ReadUInt16BigEndian(attr.AsSpan(6, 2));
        var decodedPort = xorPort ^ (0x2112A442 >> 16);
        Assert.AreEqual(originalPort, decodedPort);

        // Decode address
        var xorKey = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(xorKey, 0x2112A442);
        TestTransactionId.CopyTo(xorKey.AsSpan(4));

        var decodedIpBytes = new byte[16];
        for (var i = 0; i < 16; i++)
            decodedIpBytes[i] = (byte)(attr[8 + i] ^ xorKey[i]);

        var decodedIp = new IPAddress(decodedIpBytes);
        Assert.AreEqual(originalIp, decodedIp);
    }

    // ── Edge cases ───────────────────────────────────────────────

    [TestMethod]
    public void BuildBindingResponse_Port0_HandlesCorrectly()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);
        Assert.IsTrue(response.Length >= 20);
    }

    [TestMethod]
    public void BuildBindingResponse_Port65535_HandlesCorrectly()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 65535);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);
        Assert.IsTrue(response.Length >= 20);
    }

    [TestMethod]
    public void BuildBindingResponse_LoopbackIPv4_HandlesCorrectly()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 3478);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);

        var messageType = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        Assert.AreEqual(0x0101, messageType);
    }

    [TestMethod]
    public void BuildBindingResponse_LoopbackIPv6_HandlesCorrectly()
    {
        var endpoint = new IPEndPoint(IPAddress.IPv6Loopback, 3478);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);

        var messageType = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        Assert.AreEqual(0x0101, messageType);
    }

    [TestMethod]
    public void BuildBindingResponse_ContainsSoftwareAttribute()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 3478);
        var response = StunServer.BuildBindingResponse(endpoint, TestTransactionId);

        // Search for SOFTWARE attribute type (0x8022) in the response after header
        var found = false;
        var pos = 20; // skip header
        while (pos + 4 <= response.Length)
        {
            var attrType = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(pos, 2));
            var attrLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(pos + 2, 2));

            if (attrType == 0x8022)
            {
                found = true;
                var software = System.Text.Encoding.UTF8.GetString(response, pos + 4, attrLength);
                Assert.AreEqual("DotNetCloud STUN/1.0", software);
                break;
            }

            pos += 4 + ((attrLength + 3) & ~3); // skip to next attribute (padded)
        }

        Assert.IsTrue(found, "SOFTWARE attribute not found in response");
    }
}
