using System.IO;
using System.Text;

namespace OTClient.Framework.Resources;

// ─── OtbItemGroup ─────────────────────────────────────────────────────────────

/// <summary>OTB item group (maps to the top-level group byte in the binary file).</summary>
public enum OtbItemGroup : byte
{
    None       = 0,
    Ground     = 1,
    Container  = 2,
    Weapon     = 3,
    Ammunition = 4,
    Armor      = 5,
    Changes    = 6,
    Teleport   = 7,
    MagicField = 8,
    Writable   = 9,
    Key        = 10,
    Splash     = 11,
    Combat     = 12,
    Door       = 13,
    Deprecated = 14,
    Last       = 15,
}

// ─── OtbItem ──────────────────────────────────────────────────────────────────

/// <summary>A single item loaded from an OTB file.</summary>
public sealed class OtbItem
{
    public int          ServerId  { get; internal set; }
    public int          ClientId  { get; internal set; }
    public OtbItemGroup Group     { get; internal set; }
    public uint         Flags     { get; internal set; }
    public string       Name      { get; internal set; } = string.Empty;
    public string       Article   { get; internal set; } = string.Empty;
    public string       PluralName { get; internal set; } = string.Empty;
    public int          GroundSpeed { get; internal set; }
    public int          LightLevel  { get; internal set; }
    public int          LightColor  { get; internal set; }
    public int          MaxReadChars  { get; internal set; }
    public int          MaxReadWriteChars { get; internal set; }
    public byte         AlwaysOnTopOrder { get; internal set; }
    public int          WareId        { get; internal set; }
}

// ─── OtbFile ──────────────────────────────────────────────────────────────────

/// <summary>
/// The result of reading a Tibia OTB (Open Tibia Binary) item database file.
/// </summary>
public sealed class OtbFile
{
    public int    MajorVersion  { get; internal set; }
    public int    MinorVersion  { get; internal set; }
    public int    BuildNumber   { get; internal set; }
    public string Description   { get; internal set; } = string.Empty;

    private readonly List<OtbItem> _items = [];
    public  IReadOnlyList<OtbItem> Items   => _items;
    internal void AddItem(OtbItem item) => _items.Add(item);
}

// ─── OtbReader ────────────────────────────────────────────────────────────────

/// <summary>
/// Reads the binary OTB (Open Tibia Binary) item database format.
/// <para>
/// OTB stores items as a tree of escape-coded nodes:
/// <list type="bullet">
///   <item><c>0xFE</c> = node start</item>
///   <item><c>0xFF</c> = node end</item>
///   <item><c>0xFD</c> = escape next byte</c></item>
/// </list>
/// </para>
/// Maps to the OTB reader logic in the original client.
/// Task 9.8.
/// </summary>
public static class OtbReader
{
    // ─── OTB binary constants ─────────────────────────────────────────────────

    private const byte NodeStart  = 0xFE;
    private const byte NodeEnd    = 0xFF;
    private const byte EscapeByte = 0xFD;

    // Attribute IDs
    private const byte AttrServerId   = 0x10;
    private const byte AttrClientId   = 0x11;
    private const byte AttrName       = 0x12;
    private const byte AttrDescr      = 0x13;
    private const byte AttrSpeed      = 0x14;
    private const byte AttrSlot       = 0x15;
    private const byte AttrMaxItems   = 0x16;
    private const byte AttrWeight     = 0x17;
    private const byte AttrWeapon     = 0x18;
    private const byte AttrAmmu       = 0x19;
    private const byte AttrArmor      = 0x1A;
    private const byte AttrMagicLevel = 0x1B;
    private const byte AttrMagicFieldType = 0x1C;
    private const byte AttrWritable   = 0x1D;
    private const byte AttrRotateTo   = 0x1F;
    private const byte AttrDecayTo    = 0x20;
    private const byte AttrFloorChange = 0x21;
    private const byte AttrCorpseType = 0x22;
    private const byte AttrContainerSize = 0x23;
    private const byte AttrFluidSource = 0x24;
    private const byte AttrWritableOnce = 0x25;
    private const byte AttrEmitEffect  = 0x26;
    private const byte AttrLight       = 0x27;
    private const byte AttrDecay       = 0x28;
    private const byte AttrCorpse      = 0x29;
    private const byte AttrHookSouth   = 0x2A;
    private const byte AttrHookEast    = 0x2B;
    private const byte AttrCharges     = 0x2C;
    private const byte AttrGroundSpeed = 0x2D;
    private const byte AttrAttribute   = 0x2E;
    private const byte AttrMapColor    = 0x2F;
    private const byte AttrPluralName  = 0x30;
    private const byte AttrArticle     = 0x31;
    private const byte AttrAlwaysOnTop = 0x32;
    private const byte AttrMaxReadChars = 0x33;
    private const byte AttrMaxReadWriteChars = 0x34;
    private const byte AttrWareId      = 0x35;

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Reads an OTB file from a byte array.</summary>
    public static OtbFile ReadFromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var ms = new MemoryStream(data, writable: false);
        return Read(ms);
    }

    /// <summary>Reads an OTB file from a stream.</summary>
    public static OtbFile Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Decode the escape-coded byte stream into a flat buffer
        var raw = Unescape(stream);
        using var br = new BinaryReader(new MemoryStream(raw, writable: false), Encoding.Latin1, leaveOpen: false);

        var result = new OtbFile();

        // Root node starts after a NodeStart byte
        // The OTB format:
        //   4 bytes: identifier (ignored / zero)
        //   NodeStart (0xFE) -- root node
        //     1 byte: type (0 = root)
        //     4 bytes flags
        //     attributes...
        //     NodeStart -- item nodes
        //     NodeEnd  (0xFF)
        //   NodeEnd

        // Skip 4-byte identifier
        if (raw.Length < 4)
            throw new InvalidDataException("OTB file too small.");

        br.BaseStream.Position = 4;

        // Expect root node start
        byte b = br.ReadByte();
        if (b != NodeStart)
            throw new InvalidDataException($"Expected OTB root NodeStart (0xFE), got 0x{b:X2}.");

        // Root node type byte (should be 0)
        byte rootType = br.ReadByte();

        // Root node flags (4 bytes)
        uint rootFlags = br.ReadUInt32();

        // Root node attributes: read version info attribute
        ReadVersionAttributes(br, result);

        // Child item nodes
        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            b = br.ReadByte();
            if (b == NodeEnd) break;
            if (b == NodeStart)
            {
                var item = ReadItem(br);
                if (item is not null)
                    result.AddItem(item);
            }
        }

        return result;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>Converts the OTB escape-coded stream into a plain byte array.</summary>
    private static byte[] Unescape(Stream stream)
    {
        var ms = new MemoryStream();
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == EscapeByte)
            {
                int next = stream.ReadByte();
                if (next == -1) break;
                ms.WriteByte((byte)next);
            }
            else
            {
                ms.WriteByte((byte)b);
            }
        }
        return ms.ToArray();
    }

    private static void ReadVersionAttributes(BinaryReader br, OtbFile result)
    {
        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            byte attrType;
            try { attrType = br.ReadByte(); }
            catch (EndOfStreamException) { return; }

            if (attrType == NodeStart || attrType == NodeEnd) break;
            if (attrType != AttrDescr) continue;

            ushort len = br.ReadUInt16();
            // Version attribute has 140 bytes: 4+4+4+128 description
            if (len >= 12)
            {
                result.MajorVersion  = (int)br.ReadUInt32();
                result.MinorVersion  = (int)br.ReadUInt32();
                result.BuildNumber   = (int)br.ReadUInt32();
                int descLen = len - 12;
                byte[] descBytes = br.ReadBytes(descLen);
                result.Description = Encoding.Latin1.GetString(descBytes).TrimEnd('\0');
            }
            else
            {
                br.ReadBytes(len);
            }
            break;
        }
    }

    private static OtbItem? ReadItem(BinaryReader br)
    {
        var item = new OtbItem();

        // Group byte
        byte groupByte = br.ReadByte();
        item.Group = (OtbItemGroup)groupByte;
        if (item.Group == OtbItemGroup.Deprecated) return null;

        // Flags (4 bytes)
        item.Flags = br.ReadUInt32();

        // Attributes
        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            byte attrType;
            try { attrType = br.ReadByte(); }
            catch (EndOfStreamException) { break; }

            if (attrType == NodeStart) { ReadNestedNode(br); break; }
            if (attrType == NodeEnd) break;

            ushort len = br.ReadUInt16();
            byte[] data = br.ReadBytes(len);

            switch (attrType)
            {
                case AttrServerId:
                    item.ServerId = BitConverter.ToUInt16(data, 0);
                    break;
                case AttrClientId:
                    item.ClientId = BitConverter.ToUInt16(data, 0);
                    break;
                case AttrName:
                    item.Name = Encoding.Latin1.GetString(data).TrimEnd('\0');
                    break;
                case AttrPluralName:
                    item.PluralName = Encoding.Latin1.GetString(data).TrimEnd('\0');
                    break;
                case AttrArticle:
                    item.Article = Encoding.Latin1.GetString(data).TrimEnd('\0');
                    break;
                case AttrLight:
                    if (data.Length >= 4)
                    {
                        item.LightLevel = BitConverter.ToUInt16(data, 0);
                        item.LightColor = BitConverter.ToUInt16(data, 2);
                    }
                    break;
                case AttrGroundSpeed:
                    if (data.Length >= 2)
                        item.GroundSpeed = BitConverter.ToUInt16(data, 0);
                    break;
                case AttrMaxReadChars:
                    if (data.Length >= 2)
                        item.MaxReadChars = BitConverter.ToUInt16(data, 0);
                    break;
                case AttrMaxReadWriteChars:
                    if (data.Length >= 2)
                        item.MaxReadWriteChars = BitConverter.ToUInt16(data, 0);
                    break;
                case AttrAlwaysOnTop:
                    if (data.Length >= 1)
                        item.AlwaysOnTopOrder = data[0];
                    break;
                case AttrWareId:
                    if (data.Length >= 2)
                        item.WareId = BitConverter.ToUInt16(data, 0);
                    break;
            }
        }

        return item;
    }

    /// <summary>Skips an already-opened nested node (consumes until matching NodeEnd).</summary>
    private static void ReadNestedNode(BinaryReader br)
    {
        int depth = 1;
        while (depth > 0 && br.BaseStream.Position < br.BaseStream.Length)
        {
            byte b = br.ReadByte();
            if (b == NodeStart) depth++;
            else if (b == NodeEnd) depth--;
        }
    }
}
