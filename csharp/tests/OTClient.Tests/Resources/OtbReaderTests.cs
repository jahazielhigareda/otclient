using System.IO;
using System.Text;
using OTClient.Framework.Resources;
using Xunit;

namespace OTClient.Tests.Resources;

/// <summary>
/// Tests for <see cref="OtbReader"/> and <see cref="OtbFile"/>.
/// Task 9.8.
/// </summary>
public sealed class OtbReaderTests
{
    // ─── Minimal OTB builder ──────────────────────────────────────────────────

    // OTB format (escape-encoded):
    //   4-byte identifier (zeros)
    //   NodeStart(0xFE)  root node
    //     byte rootType(0)
    //     uint32 rootFlags(0)
    //     byte AttrDescr(0x13)
    //     uint16 len(12)
    //     uint32 majorVersion
    //     uint32 minorVersion
    //     uint32 buildNumber
    //     [item nodes omitted in minimal file]
    //   NodeEnd(0xFF)
    //
    // Because Unescape(stream) is called first, we build the RAW byte array
    // (without escape-coding) and pass it directly as the "raw" buffer.
    // But ReadFromBytes → Read → Unescape, so we need to emit the bytes
    // the way the actual OTB file looks: escape-coded.
    //
    // To keep tests simple we build files that have no control bytes in the
    // content, so no escaping is required.

    private const byte NodeStart  = 0xFE;
    private const byte NodeEnd    = 0xFF;
    private const byte AttrDescr  = 0x13;

    private static byte[] BuildMinimalOtb(uint major = 1, uint minor = 2, uint build = 100)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.Latin1, leaveOpen: true);

        // 4-byte file identifier
        bw.Write(0u);

        // Root NodeStart
        bw.Write(NodeStart);

        // Root type byte
        bw.Write((byte)0);

        // Root flags
        bw.Write(0u);

        // Version attribute (type = AttrDescr = 0x13)
        bw.Write(AttrDescr);
        bw.Write((ushort)12);    // length = 3 × uint32
        bw.Write(major);
        bw.Write(minor);
        bw.Write(build);

        // Root NodeEnd
        bw.Write(NodeEnd);

        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] BuildOtbWithItem(uint major = 1, int serverId = 100, int clientId = 200)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.Latin1, leaveOpen: true);

        bw.Write(0u);               // identifier
        bw.Write(NodeStart);        // root start

        // root node body
        bw.Write((byte)0);          // root type
        bw.Write(0u);               // root flags
        bw.Write(AttrDescr);        // version attr
        bw.Write((ushort)12);
        bw.Write(major);            // majorVersion
        bw.Write(1u);               // minorVersion
        bw.Write(0u);               // buildNumber

        // Item NodeStart
        bw.Write(NodeStart);
        bw.Write((byte)OtbItemGroup.Ground);  // group byte
        bw.Write(0u);                          // flags

        // ServerId attribute (0x10)
        bw.Write((byte)0x10);
        bw.Write((ushort)2);
        bw.Write((ushort)serverId);

        // ClientId attribute (0x11)
        bw.Write((byte)0x11);
        bw.Write((ushort)2);
        bw.Write((ushort)clientId);

        bw.Write(NodeEnd);          // item end
        bw.Write(NodeEnd);          // root end

        bw.Flush();
        return ms.ToArray();
    }

    // ─── Basic parsing ────────────────────────────────────────────────────────

    [Fact]
    public void ReadFromBytes_MinimalOtb_ReturnsFile()
    {
        var otb = BuildMinimalOtb(major: 3, minor: 57, build: 999);
        var file = OtbReader.ReadFromBytes(otb);
        Assert.NotNull(file);
    }

    [Fact]
    public void ReadFromBytes_CorrectMajorVersion()
    {
        var file = OtbReader.ReadFromBytes(BuildMinimalOtb(major: 5));
        Assert.Equal(5, file.MajorVersion);
    }

    [Fact]
    public void ReadFromBytes_CorrectMinorVersion()
    {
        var file = OtbReader.ReadFromBytes(BuildMinimalOtb(minor: 42));
        Assert.Equal(42, file.MinorVersion);
    }

    [Fact]
    public void ReadFromBytes_CorrectBuildNumber()
    {
        var file = OtbReader.ReadFromBytes(BuildMinimalOtb(build: 777));
        Assert.Equal(777, file.BuildNumber);
    }

    [Fact]
    public void ReadFromBytes_NoItems_ItemsEmpty()
    {
        var file = OtbReader.ReadFromBytes(BuildMinimalOtb());
        Assert.Empty(file.Items);
    }

    [Fact]
    public void ReadFromBytes_WithItem_ItemCount()
    {
        var file = OtbReader.ReadFromBytes(BuildOtbWithItem(serverId: 100));
        Assert.Single(file.Items);
    }

    [Fact]
    public void ReadFromBytes_Item_HasCorrectServerId()
    {
        var file = OtbReader.ReadFromBytes(BuildOtbWithItem(serverId: 123));
        Assert.Equal(123, file.Items[0].ServerId);
    }

    [Fact]
    public void ReadFromBytes_Item_HasCorrectClientId()
    {
        var file = OtbReader.ReadFromBytes(BuildOtbWithItem(clientId: 456));
        Assert.Equal(456, file.Items[0].ClientId);
    }

    [Fact]
    public void ReadFromBytes_Item_GroupIsGround()
    {
        var file = OtbReader.ReadFromBytes(BuildOtbWithItem());
        Assert.Equal(OtbItemGroup.Ground, file.Items[0].Group);
    }

    [Fact]
    public void ReadFromBytes_EmptyData_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => OtbReader.ReadFromBytes([]));
    }

    [Fact]
    public void Read_Stream_SameAsReadFromBytes()
    {
        byte[] data = BuildMinimalOtb(major: 2, minor: 10, build: 5);
        var    a    = OtbReader.ReadFromBytes(data);
        using var ms = new MemoryStream(data);
        var    b    = OtbReader.Read(ms);
        Assert.Equal(a.MajorVersion, b.MajorVersion);
        Assert.Equal(a.MinorVersion, b.MinorVersion);
    }
}
