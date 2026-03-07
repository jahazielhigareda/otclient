using System.IO;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── MinimapTileFlags ─────────────────────────────────────────────────────────

/// <summary>
/// Bitflags stored in every <see cref="MinimapTileData"/> cell.
/// Mirrors <c>MinimapTileFlags</c> in <c>minimap.h</c>.
/// Task T18.
/// </summary>
[Flags]
public enum MinimapTileFlags : byte
{
    None          = 0,
    WasSeen       = 1,
    NotPathable   = 2,
    NotWalkable   = 4,
    Empty         = 8,
}

// ─── MinimapTileData ──────────────────────────────────────────────────────────

/// <summary>
/// Compact data stored for every visited tile (3 bytes, matching the C++ layout).
/// <c>Color</c> = 8-bit palette index (255 = transparent/unknown).
/// <c>Speed</c> = ground speed / 10, clamped to byte.
/// Mirrors the C++ <c>MinimapTile</c> struct.
/// Task T18.
/// </summary>
public struct MinimapTileData : IEquatable<MinimapTileData>
{
    /// <summary>8-bit palette colour index. 255 means "unseen" (transparent).</summary>
    public byte Color = 255;
    /// <summary>Bitflags — combination of <see cref="MinimapTileFlags"/>.</summary>
    public MinimapTileFlags Flags = MinimapTileFlags.None;
    /// <summary>Ground walk speed ÷ 10, clamped to [0,255].</summary>
    public byte Speed = 10;

    public MinimapTileData() { }

    public readonly bool HasFlag(MinimapTileFlags flag) => (Flags & flag) != 0;
    public readonly int  GetSpeed() => Speed * 10;

    public readonly bool Equals(MinimapTileData other)
        => Color == other.Color && Flags == other.Flags && Speed == other.Speed;
    public override readonly bool Equals(object? obj) => obj is MinimapTileData m && Equals(m);
    public override readonly int GetHashCode() => HashCode.Combine(Color, Flags, Speed);
    public static bool operator ==(MinimapTileData a, MinimapTileData b) => a.Equals(b);
    public static bool operator !=(MinimapTileData a, MinimapTileData b) => !a.Equals(b);
}

// ─── MinimapBlock ─────────────────────────────────────────────────────────────

/// <summary>
/// A 64×64 grid of <see cref="MinimapTileData"/> cells.
/// Matches <c>MMBLOCK_SIZE = 64</c> and the C++ <c>MinimapBlock</c> class.
/// Task T18.
/// </summary>
public sealed class MinimapBlock
{
    public const int BlockSize = 64;

    private readonly MinimapTileData[] _tiles = new MinimapTileData[BlockSize * BlockSize];
    private bool _wasSeen;
    private bool _isDirty = true;

    public bool WasSeen => _wasSeen;
    public bool IsDirty => _isDirty;

    private static int Index(int localX, int localY)
        => (localY % BlockSize) * BlockSize + (localX % BlockSize);

    /// <summary>Returns the tile at block-local coordinates.</summary>
    public ref MinimapTileData GetTile(int localX, int localY)
        => ref _tiles[Index(localX, localY)];

    /// <summary>Updates the tile at block-local coordinates, marking dirty when colour changes.</summary>
    public void UpdateTile(int localX, int localY, MinimapTileData tile)
    {
        int idx = Index(localX, localY);
        if (_tiles[idx].Color != tile.Color) _isDirty = true;
        _tiles[idx] = tile;
    }

    /// <summary>Resets the tile at block-local coordinates to default.</summary>
    public void ResetTile(int localX, int localY) => _tiles[Index(localX, localY)] = new MinimapTileData();

    /// <summary>Marks this block as having been visited.</summary>
    public void MarkSeen() { _wasSeen = true; }

    /// <summary>Signals that a render update is needed.</summary>
    public void MarkDirty() => _isDirty = true;

    /// <summary>Clears the dirty flag after a render flush.</summary>
    public void ClearDirty() => _isDirty = false;

    /// <summary>Clears all tile data and resets state.</summary>
    public void Clean()
    {
        Array.Fill(_tiles, new MinimapTileData());
        _isDirty  = false;
        _wasSeen  = false;
    }

    /// <summary>Returns a read-only span over the raw tile array (for binary I/O).</summary>
    internal ReadOnlySpan<MinimapTileData> GetRawSpan() => _tiles;

    /// <summary>Copies raw bytes over the tile array (for binary I/O).</summary>
    internal void CopyFromBytes(byte[] src)
    {
        // Each MinimapTileData = 3 bytes: Color, Flags, Speed
        for (int i = 0; i < _tiles.Length && i * 3 + 2 < src.Length; i++)
        {
            _tiles[i].Color = src[i * 3];
            _tiles[i].Flags = (MinimapTileFlags)src[i * 3 + 1];
            _tiles[i].Speed = src[i * 3 + 2];
        }
    }

    /// <summary>Serialises the tile array to a byte array (for binary I/O).</summary>
    internal byte[] ToBytes()
    {
        var buf = new byte[_tiles.Length * 3];
        for (int i = 0; i < _tiles.Length; i++)
        {
            buf[i * 3]     = _tiles[i].Color;
            buf[i * 3 + 1] = (byte)_tiles[i].Flags;
            buf[i * 3 + 2] = _tiles[i].Speed;
        }
        return buf;
    }
}

// ─── Minimap ──────────────────────────────────────────────────────────────────

/// <summary>
/// Records visited tile colours (and pathability metadata) across all floors,
/// organised into 64×64 <see cref="MinimapBlock"/> cells for efficient I/O and
/// rendering.  Provides OTMM binary load/save and coordinate-mapping helpers
/// that mirror the C++ <c>Minimap</c> class.
/// Maps to <c>src/client/minimap.h</c> and <c>src/client/minimap.cpp</c>.
/// Task T18.
/// </summary>
public sealed class Minimap
{
    // ─── Constants ────────────────────────────────────────────────────────────

    public  const int    MaxFloors         = 16;            // floors 0–15
    private const int    MapAxisSize       = 65536;          // total map size in tiles per axis
    private const int    BlocksPerAxis     = MapAxisSize / MinimapBlock.BlockSize; // 1024
    private const uint   OtmmSignature     = 0x4D4d544F;   // "OTMm"
    private const ushort OtmmVersion       = 1;
    private const ushort OtmmBlockSentinel = 65535;          // signals end-of-file in OTMM stream
    private const int    SpeedDivisor      = 10;             // C++ MinimapTile::getSpeed() divisor
    private const int    MaxSpeedValue     = 255;            // max byte value for speed field

    // ─── Storage: _blocks[floor][blockIndex] ──────────────────────────────────

    // Outer array is indexed by floor (0-15), inner dictionary by block index.
    private readonly Dictionary<uint, MinimapBlock>[] _blocks =
        Enumerable.Range(0, MaxFloors)
                  .Select(_ => new Dictionary<uint, MinimapBlock>())
                  .ToArray();

    // ─── Legacy colour-only API (kept for backwards compat) ──────────────────

    /// <summary>Total number of recorded tiles (all floors, legacy path only).</summary>
    public int Count
    {
        get
        {
            int n = 0;
            for (int z = 0; z < MaxFloors; z++)
                foreach (var (_, b) in _blocks[z])
                    foreach (var t in b.GetRawSpan())
                        if (t.HasFlag(MinimapTileFlags.WasSeen)) n++;
            return n;
        }
    }

    /// <summary>Records the colour of a tile at <paramref name="pos"/> (legacy API).</summary>
    public void Record(Position pos, Color color)
    {
        var td = new MinimapTileData
        {
            Color = RaylibColorTo8Bit(color),
            Flags = MinimapTileFlags.WasSeen,
            Speed = 10,
        };
        var (block, lx, ly) = GetOrCreateBlock(pos);
        block.UpdateTile(lx, ly, td);
        block.MarkSeen();
    }

    /// <summary>Returns the recorded colour for <paramref name="pos"/>, or Black (legacy API).</summary>
    public Color GetColor(Position pos)
    {
        var (block, lx, ly) = TryGetBlock(pos);
        if (block is null) return Color.Black;
        ref var t = ref block.GetTile(lx, ly);
        return t.Color == 255 ? Color.Black : EightBitToRaylibColor(t.Color);
    }

    /// <summary>Returns <c>true</c> when the tile has been visited.</summary>
    public bool IsKnown(Position pos)
    {
        var (block, lx, ly) = TryGetBlock(pos);
        if (block is null) return false;
        return block.GetTile(lx, ly).HasFlag(MinimapTileFlags.WasSeen);
    }

    /// <summary>Clears all recorded tiles.</summary>
    public void Clear()
    {
        for (int z = 0; z < MaxFloors; z++) _blocks[z].Clear();
    }

    // ─── Tile update from map ─────────────────────────────────────────────────

    /// <summary>
    /// Updates the minimap tile at <paramref name="pos"/> from a map <see cref="Tile"/>.
    /// Passing <c>null</c> marks the position as not-walkable / not-pathable.
    /// Maps to <c>Minimap::updateTile</c>.
    /// </summary>
    public void UpdateTile(Position pos, Tile? tile)
    {
        var td = new MinimapTileData();
        if (tile is not null)
        {
            td.Color  = RaylibColorTo8Bit(tile.MinimapColor);
            td.Flags  = MinimapTileFlags.WasSeen;
            td.Speed  = (byte)Math.Min(Math.Max(1, (tile.GetGroundSpeed() + SpeedDivisor - 1) / SpeedDivisor), MaxSpeedValue);
        }
        else
        {
            td.Flags  = MinimapTileFlags.NotWalkable | MinimapTileFlags.NotPathable;
        }

        var (block, lx, ly) = GetOrCreateBlock(pos);
        block.UpdateTile(lx, ly, td);
        block.MarkSeen();
    }

    /// <summary>
    /// Bulk-imports tile colours from all known tiles in <paramref name="map"/> (legacy helper).
    /// </summary>
    public void Update(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        foreach (var t in map.GetViewport(0, 0, Position.GroundFloor, int.MaxValue, int.MaxValue))
            Record(t.Position, t.MinimapColor);
    }

    // ─── Tile access ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="MinimapTileData"/> for the given map position.
    /// Returns a default (unseen) tile when the position is not known.
    /// </summary>
    public MinimapTileData GetTile(Position pos)
    {
        var (block, lx, ly) = TryGetBlock(pos);
        return block is null ? new MinimapTileData() : block.GetTile(lx, ly);
    }

    // ─── Coordinate mapping ───────────────────────────────────────────────────

    /// <summary>
    /// Maps a world <see cref="Position"/> to a pixel point inside
    /// <paramref name="screenRect"/>, given that <paramref name="mapCenter"/>
    /// is drawn at the centre of the rect at <paramref name="scale"/> px/tile.
    /// Returns <c>(-1,-1)</c> when the position is on a different floor.
    /// Mirrors <c>Minimap::getTilePoint</c>.
    /// </summary>
    public static Vector2 GetTilePoint(
        Position pos,
        System.Drawing.Rectangle screenRect,
        Position mapCenter,
        float scale)
    {
        if (pos.Z != mapCenter.Z) return new Vector2(-1, -1);

        var mapRect  = CalcMapRect(screenRect, mapCenter, scale);
        var offX     = (mapRect.Width  * scale - screenRect.Width)  / 2f;
        var offY     = (mapRect.Height * scale - screenRect.Height) / 2f;
        var posOffX  = (pos.X - mapRect.X) * scale;
        var posOffY  = (pos.Y - mapRect.Y) * scale;
        return new Vector2(
            posOffX + screenRect.X - offX + scale / 2f,
            posOffY + screenRect.Y - offY + scale / 2f);
    }

    /// <summary>
    /// Converts a pixel <paramref name="point"/> inside <paramref name="screenRect"/>
    /// back to a world <see cref="Position"/> on the same floor as
    /// <paramref name="mapCenter"/>.
    /// Mirrors <c>Minimap::getTilePosition</c>.
    /// </summary>
    public static Position GetTilePosition(
        Vector2 point,
        System.Drawing.Rectangle screenRect,
        Position mapCenter,
        float scale)
    {
        var mapRect = CalcMapRect(screenRect, mapCenter, scale);
        var offX    = (mapRect.Width  * scale - screenRect.Width)  / 2f;
        var offY    = (mapRect.Height * scale - screenRect.Height) / 2f;
        int x       = (int)((point.X - screenRect.X + offX) / scale) + mapRect.X;
        int y       = (int)((point.Y - screenRect.Y + offY) / scale) + mapRect.Y;
        return new Position(x, y, mapCenter.Z);
    }

    /// <summary>
    /// Returns the screen-space rectangle that the tile at <paramref name="pos"/>
    /// occupies on the minimap display.
    /// Mirrors <c>Minimap::getTileRect</c>.
    /// </summary>
    public static System.Drawing.Rectangle GetTileRect(
        Position pos,
        System.Drawing.Rectangle screenRect,
        Position mapCenter,
        float scale)
    {
        if (pos.Z != mapCenter.Z) return System.Drawing.Rectangle.Empty;
        var center = GetTilePoint(pos, screenRect, mapCenter, scale);
        int tileSize = Math.Max(1, (int)scale);
        return new System.Drawing.Rectangle(
            (int)(center.X - tileSize / 2f),
            (int)(center.Y - tileSize / 2f),
            tileSize, tileSize);
    }

    // ─── OTMM binary I/O ─────────────────────────────────────────────────────

    /// <summary>
    /// Loads a binary <c>.otmm</c> minimap file into this instance.
    /// The OTMM format: U32 signature, U16 data-start offset, U16 version,
    /// U32 flags, then version-specific header, then block records until
    /// an invalid position (x=65535 or z≥MaxFloors) is encountered.
    /// Each block record: U16 x, U16 y, U8 z, then raw tile bytes
    /// (64×64×3 = 12288 bytes, uncompressed for portability).
    /// Mirrors <c>Minimap::loadOtmm</c>.
    /// </summary>
    public bool LoadOtmm(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            uint sig = r.ReadUInt32();
            if (sig != OtmmSignature) return false;

            ushort dataStart = r.ReadUInt16();
            ushort version   = r.ReadUInt16();
            _ = r.ReadUInt32(); // flags (reserved)

            if (version != 1) return false;

            _ = r.ReadString(); // description

            stream.Seek(dataStart, SeekOrigin.Begin);

            while (stream.Position < stream.Length - 4)
            {
                ushort px = r.ReadUInt16();
                ushort py = r.ReadUInt16();
                byte   pz = r.ReadByte();

                if (px >= OtmmBlockSentinel || pz >= MaxFloors) break; // sentinel

                ushort len  = r.ReadUInt16();
                byte[] data = r.ReadBytes(len);

                var pos   = new Position(px, py, pz);
                var (block, _, _) = GetOrCreateBlock(pos);
                block.CopyFromBytes(data);
                block.MarkDirty();
                block.MarkSeen();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves all visited minimap blocks to <paramref name="stream"/> in OTMM format.
    /// Mirrors <c>Minimap::saveOtmm</c>.
    /// </summary>
    public void SaveOtmm(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Header
        w.Write(OtmmSignature);          // U32 signature
        long startFieldPos = stream.Position;
        w.Write((ushort)0);              // U16 data-start (placeholder)
        w.Write(OtmmVersion);            // U16 version
        w.Write((uint)0);               // U32 flags (reserved)
        w.Write("OTMM 1.0");            // description

        // Record actual start and rewrite placeholder
        uint dataStart = (uint)stream.Position;
        long savedPos  = stream.Position;
        stream.Seek(startFieldPos, SeekOrigin.Begin);
        w.Write((ushort)dataStart);
        stream.Seek(savedPos, SeekOrigin.Begin);

        // Block records
        for (int z = 0; z < MaxFloors; z++)
        {
            foreach (var (blockIndex, block) in _blocks[z])
            {
                if (!block.WasSeen) continue;

                var pos = IndexToPosition(blockIndex, z);
                w.Write((ushort)pos.X);
                w.Write((ushort)pos.Y);
                w.Write((byte)pos.Z);

                byte[] data = block.ToBytes();
                w.Write((ushort)data.Length);
                w.Write(data);
            }
        }

        // Sentinel: position with x=OtmmBlockSentinel signals end-of-file
        w.Write(OtmmBlockSentinel);
        w.Write((ushort)0);
        w.Write((byte)0);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static uint BlockIndex(Position pos)
        => (uint)((pos.Y / MinimapBlock.BlockSize) * BlocksPerAxis
                + (pos.X / MinimapBlock.BlockSize));

    private static Position IndexToPosition(uint index, int z)
    {
        int bx = (int)(index % BlocksPerAxis) * MinimapBlock.BlockSize;
        int by = (int)(index / BlocksPerAxis) * MinimapBlock.BlockSize;
        return new Position(bx, by, z);
    }

    private (MinimapBlock block, int localX, int localY) GetOrCreateBlock(Position pos)
    {
        int z   = pos.Z;
        uint bi = BlockIndex(pos);
        if (!_blocks[z].TryGetValue(bi, out var block))
        {
            block = new MinimapBlock();
            _blocks[z][bi] = block;
        }
        return (block, pos.X % MinimapBlock.BlockSize, pos.Y % MinimapBlock.BlockSize);
    }

    private (MinimapBlock? block, int localX, int localY) TryGetBlock(Position pos)
    {
        int z   = pos.Z;
        uint bi = BlockIndex(pos);
        if (!_blocks[z].TryGetValue(bi, out var block)) return (null, 0, 0);
        return (block, pos.X % MinimapBlock.BlockSize, pos.Y % MinimapBlock.BlockSize);
    }

    private static System.Drawing.Rectangle CalcMapRect(
        System.Drawing.Rectangle screenRect,
        Position mapCenter,
        float scale)
    {
        int w = (int)(screenRect.Width  / scale);
        int h = (int)Math.Ceiling(screenRect.Height / scale);
        return new System.Drawing.Rectangle(
            mapCenter.X - w / 2,
            mapCenter.Y - h / 2,
            w, h);
    }

    // Very simple 8-bit colour approximation (matches C++ Color::to8bit / Color::from8bit).
    private static byte RaylibColorTo8Bit(Color c)
    {
        byte r = (byte)((c.R >> 5) & 0x7);
        byte g = (byte)((c.G >> 5) & 0x7);
        byte b = (byte)((c.B >> 6) & 0x3);
        return (byte)((r << 5) | (g << 2) | b);
    }

    private static Color EightBitToRaylibColor(byte c)
    {
        byte r = (byte)(((c >> 5) & 0x7) * 255 / 7);
        byte g = (byte)(((c >> 2) & 0x7) * 255 / 7);
        byte b = (byte)( (c       & 0x3) * 255 / 3);
        return new Color(r, g, b, (byte)255);
    }
}


// ─── InventorySlot ────────────────────────────────────────────────────────────

/// <summary>
/// Equipment slots on the player's paper-doll.  Wire values match the C++
/// <c>Otc::InventorySlot</c> enum (1-based).
/// Task T04.
/// </summary>
public enum InventorySlot : byte
{
    Head      = 1,
    Necklace  = 2,
    Backpack  = 3,
    Armor     = 4,
    Right     = 5,
    Left      = 6,
    Legs      = 7,
    Feet      = 8,
    Ring      = 9,
    Ammo      = 10,
    Purse     = 11,
    Ext1      = 12,
    Ext2      = 13,
    Ext3      = 14,
    Ext4      = 15,
    /// <summary>Sentinel value — not a real slot; equals the total slot count + 1.</summary>
    MaxValue  = 16,
}

// ─── Container ────────────────────────────────────────────────────────────────

/// <summary>
/// An ordered collection of <see cref="Item"/> objects (backpack / container item).
/// Mirrors <c>src/client/container.h</c>.
/// Task T08 / T39.
/// </summary>
[MoonSharpUserData]
public sealed class Container
{
    /// <summary>Wire container ID (0–63).</summary>
    public int    Id           { get; init; }

    /// <summary>Display name shown in the container window title.</summary>
    public string Name         { get; init; } = string.Empty;

    /// <summary>Maximum number of items this container can hold.</summary>
    public int    Capacity     { get; init; } = 20;

    /// <summary>
    /// The item that represents the container itself (e.g. a backpack item).
    /// Mirrors <c>m_containerItem</c>.
    /// </summary>
    public Item?  ContainerItem { get; init; }

    /// <summary>
    /// <c>true</c> when this container was opened from inside another container.
    /// Mirrors <c>m_hasParent</c>.
    /// </summary>
    public bool HasParent    { get; init; }

    /// <summary>
    /// <c>true</c> when items can be dragged into/out of this container.
    /// Mirrors <c>m_unlocked</c> (GameContainerPagination).
    /// </summary>
    public bool IsUnlocked   { get; init; } = true;

    /// <summary>
    /// <c>true</c> when the container supports pagination (large bags).
    /// Mirrors <c>m_hasPages</c>.
    /// </summary>
    public bool HasPages     { get; init; }

    /// <summary>
    /// Total number of slots in the container (may exceed <see cref="Capacity"/>
    /// for paginated bags).  Mirrors <c>m_size</c>.
    /// </summary>
    public int  Size         { get; init; }

    /// <summary>
    /// First visible slot index (non-zero for paginated bags scrolled down).
    /// Mirrors <c>m_firstIndex</c>.
    /// </summary>
    public int  FirstIndex   { get; init; }

    /// <summary><c>true</c> when the container has been closed by the server.</summary>
    public bool IsClosed     { get; private set; }

    // ─── Contents ─────────────────────────────────────────────────────────────

    private readonly List<Item> _contents = [];

    /// <summary>The ordered list of items currently in this container.</summary>
    public  IReadOnlyList<Item> Contents  => _contents;

    /// <summary>Number of items currently in this container.</summary>
    public  int                 Count     => _contents.Count;

    public  bool IsFull  => _contents.Count >= Capacity;
    public  bool IsEmpty => _contents.Count == 0;

    // ─── Mutation methods ─────────────────────────────────────────────────────

    /// <summary>
    /// Appends all items in <paramref name="items"/> to the container
    /// (used when the server sends the initial item list in <c>OpenContainer</c>).
    /// </summary>
    public void AddItems(IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            _contents.Add(item);
    }

    /// <summary>
    /// Inserts <paramref name="item"/> at position <paramref name="slot"/>,
    /// clamping to the end if <paramref name="slot"/> is out of range.
    /// Mirrors <c>Container::onAddItem</c> with the paginated slot.
    /// </summary>
    public void AddItem(Item item, int slot = -1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (slot < 0 || slot >= _contents.Count)
            _contents.Add(item);
        else
            _contents.Insert(slot, item);
    }

    /// <summary>
    /// Replaces the item at <paramref name="slot"/> with <paramref name="newItem"/>.
    /// Returns <c>true</c> on success, <c>false</c> when <paramref name="slot"/>
    /// is out of range.
    /// Mirrors <c>Container::onUpdateItem</c>.
    /// </summary>
    public bool UpdateAt(int slot, Item newItem)
    {
        ArgumentNullException.ThrowIfNull(newItem);
        if (slot < 0 || slot >= _contents.Count) return false;
        _contents[slot] = newItem;
        return true;
    }

    /// <summary>
    /// Removes the item at <paramref name="slot"/>, optionally appending
    /// <paramref name="lastItem"/> to the end (pagination: last page item
    /// moves forward after removal).
    /// Returns <c>false</c> when <paramref name="slot"/> is out of range.
    /// Mirrors <c>Container::onRemoveItem</c>.
    /// </summary>
    public bool RemoveAt(int slot, Item? lastItem = null)
    {
        if (slot < 0 || slot >= _contents.Count) return false;
        _contents.RemoveAt(slot);
        if (lastItem is not null)
            _contents.Add(lastItem);
        return true;
    }

    /// <summary>Returns the item at <paramref name="slot"/>, or <c>null</c>.</summary>
    public Item? GetAt(int slot)
        => slot >= 0 && slot < _contents.Count ? _contents[slot] : null;

    /// <summary>Marks the container as closed.</summary>
    public void Close() => IsClosed = true;

    // ─── Lua accessor methods (T39) ───────────────────────────────────────────

    /// <summary>Lua: <c>container:getId()</c></summary>
    public int                getId()            => Id;
    /// <summary>Lua: <c>container:getName()</c></summary>
    public string             getName()          => Name;
    /// <summary>Lua: <c>container:getCapacity()</c></summary>
    public int                getCapacity()      => Capacity;
    /// <summary>Lua: <c>container:getContainerItem()</c></summary>
    public Item?              getContainerItem() => ContainerItem;
    /// <summary>Lua: <c>container:hasParent()</c></summary>
    public bool               hasParent()        => HasParent;
    /// <summary>Lua: <c>container:isClosed()</c></summary>
    public bool               isClosed()         => IsClosed;
    /// <summary>Lua: <c>container:isUnlocked()</c></summary>
    public bool               isUnlocked()       => IsUnlocked;
    /// <summary>Lua: <c>container:hasPages()</c></summary>
    public bool               hasPages()         => HasPages;
    /// <summary>Lua: <c>container:getSize()</c></summary>
    public int                getSize()          => Size;
    /// <summary>Lua: <c>container:getFirstIndex()</c></summary>
    public int                getFirstIndex()    => FirstIndex;
    /// <summary>Lua: <c>container:getItemsCount()</c></summary>
    public int                getItemsCount()    => Count;
    /// <summary>Lua: <c>container:getItem(slot)</c> — 0-based slot.</summary>
    public Item?              getItem(int slot)  => GetAt(slot);
    /// <summary>Lua: <c>container:getItems()</c> — returns all items as a table.</summary>
    public IReadOnlyList<Item> getItems()        => _contents;
}

// ─── AttachedEffect ────────────────────────────────────────────────────────────

/// <summary>Draw-order layer for attached effects (mirrors C++ DrawOrder enum).</summary>
public enum DrawOrder : byte { First = 0, Second = 1, Third = 2, Fourth = 3 }

/// <summary>
/// Per-direction offset/onTop control for attached effects and paper dolls.
/// Maps to the <c>DirControl</c> inner struct in <c>attachedeffect.h</c>.
/// </summary>
public sealed class DirControl
{
    public bool  OnTop  { get; set; }
    public int   OffsetX { get; set; }
    public int   OffsetY { get; set; }
}

/// <summary>
/// Bounce / pulse / fade animation descriptor.
/// Maps to the <c>Bounce</c> struct in <c>attachedeffect.h</c>.
/// </summary>
public sealed class BounceControl
{
    public byte   MinHeight { get; set; }
    public byte   Height    { get; set; }
    public ushort Speed     { get; set; }
}

/// <summary>
/// A visual effect that can be attached to a creature or item at runtime
/// (e.g. wings, auras, shader overlays).
/// Maps to <c>src/client/attachedeffect.h</c>.
/// Task T34.
/// </summary>
public sealed class AttachedEffect
{
    // ── identity ──────────────────────────────────────────────────────────────
    public int    Id   { get; init; }
    public string Name { get; set; } = string.Empty;

    // ── animation ─────────────────────────────────────────────────────────────
    public Animator Animator  { get; } = new(1);
    public sbyte    Loop      { get; set; } = -1;   // -1 = infinite
    public ushort   Duration  { get; set; }          // 0 = no limit

    // ── visual properties ─────────────────────────────────────────────────────
    /// <summary>Playback speed multiplier × 100 (100 = 1×).</summary>
    public byte Speed   { get; set; } = 100;
    /// <summary>Opacity × 100 (100 = fully opaque).</summary>
    public byte Opacity { get; set; } = 100;

    /// <summary>Override render size (0 = natural size).</summary>
    public int SizeWidth  { get; set; }
    public int SizeHeight { get; set; }

    // ── behaviour flags ───────────────────────────────────────────────────────
    public bool Permanent             { get; set; } = true;
    public bool HideOwner             { get; set; }
    public bool Transform             { get; set; }
    public bool DisableWalkAnimation  { get; set; }
    public bool FollowOwner           { get; set; }
    public bool CanDrawOnUI           { get; set; } = true;

    // ── draw order / direction ────────────────────────────────────────────────
    public DrawOrder  DrawOrder { get; set; } = DrawOrder.Third;
    public Direction  Direction { get; set; } = Direction.North;

    // ── animation controls ────────────────────────────────────────────────────
    public BounceControl? Bounce { get; set; }
    public BounceControl? Pulse  { get; set; }
    public BounceControl? Fade   { get; set; }

    // ── per-direction offsets (indexed by Direction cast to int) ──────────────
    private readonly DirControl[] _dirControls = new DirControl[8];

    public AttachedEffect()
    {
        for (int i = 0; i < 8; i++)
            _dirControls[i] = new DirControl();
    }

    public DirControl GetDirControl(Direction dir) => _dirControls[(int)dir];

    public void SetOnTop(bool onTop)
    {
        foreach (var dc in _dirControls) dc.OnTop = onTop;
    }

    public void SetOffset(int x, int y)
    {
        foreach (var dc in _dirControls) { dc.OffsetX = x; dc.OffsetY = y; }
    }

    public void SetDirOffset(Direction dir, int x, int y, bool onTop = false)
    {
        var dc = _dirControls[(int)dir];
        dc.OnTop = onTop; dc.OffsetX = x; dc.OffsetY = y;
    }

    /// <summary>Deep-copies this effect (mirrors <c>clone()</c> in C++).</summary>
    public AttachedEffect Clone()
    {
        var c = new AttachedEffect
        {
            Id = Id, Name = Name,
            Loop = Loop, Duration = Duration, Speed = Speed, Opacity = Opacity,
            SizeWidth = SizeWidth, SizeHeight = SizeHeight,
            Permanent = Permanent, HideOwner = HideOwner, Transform = Transform,
            DisableWalkAnimation = DisableWalkAnimation, FollowOwner = FollowOwner,
            CanDrawOnUI = CanDrawOnUI, DrawOrder = DrawOrder, Direction = Direction,
            Bounce = Bounce is null ? null : new BounceControl { MinHeight = Bounce.MinHeight, Height = Bounce.Height, Speed = Bounce.Speed },
            Pulse  = Pulse  is null ? null : new BounceControl { MinHeight = Pulse.MinHeight,  Height = Pulse.Height,  Speed = Pulse.Speed  },
            Fade   = Fade   is null ? null : new BounceControl { MinHeight = Fade.MinHeight,   Height = Fade.Height,   Speed = Fade.Speed   },
        };
        for (int i = 0; i < 8; i++)
        {
            c._dirControls[i].OnTop   = _dirControls[i].OnTop;
            c._dirControls[i].OffsetX = _dirControls[i].OffsetX;
            c._dirControls[i].OffsetY = _dirControls[i].OffsetY;
        }
        return c;
    }
}

/// <summary>
/// Manages all <see cref="AttachedEffect"/> descriptors registered by the client.
/// Maps to <c>src/client/attachedeffectmanager.h</c>.
/// Task T34.
/// </summary>
public sealed class AttachedEffectManager
{
    private readonly Dictionary<int, AttachedEffect> _effects = [];

    // kept for backwards-compat with existing tests
    public void Register(AttachedEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effects[effect.Id] = effect;
    }

    /// <summary>Register an effect identified by a ThingType id + category string.</summary>
    public AttachedEffect RegisterByThing(int id, string name, int thingId, string category)
    {
        var effect = new AttachedEffect { Id = id, Name = name };
        _effects[id] = effect;
        return effect;
    }

    /// <summary>Register an effect identified by an image path.</summary>
    public AttachedEffect RegisterByImage(int id, string name, string imagePath, bool smooth = true)
    {
        var effect = new AttachedEffect { Id = id, Name = name };
        _effects[id] = effect;
        return effect;
    }

    public AttachedEffect? Get(int id)
        => _effects.TryGetValue(id, out var e) ? e : null;

    public void Remove(int id) => _effects.Remove(id);
    public void Clear()        => _effects.Clear();

    public IEnumerable<AttachedEffect> All => _effects.Values;
    public int Count => _effects.Count;
}

// ─── AttachableObject ─────────────────────────────────────────────────────────

/// <summary>
/// Base class for game objects (creatures, tiles, items) that support attached visual effects.
/// Maps to <c>src/client/attachableobject.h</c>.
/// Task T34.
/// </summary>
public abstract class AttachableObject
{
    private readonly List<AttachedEffect> _attachedEffects = [];

    /// <summary>All currently attached effects.</summary>
    public IReadOnlyList<AttachedEffect> AttachedEffects => _attachedEffects;

    /// <summary>True when at least one effect is attached.</summary>
    public bool HasAttachedEffects => _attachedEffects.Count > 0;

    /// <summary>Whether the owner sprite is hidden by any effect.</summary>
    public bool IsOwnerHidden => _attachedEffects.Any(e => e.HideOwner);

    /// <summary>Attach an effect to this object (clones it for independent lifetime).</summary>
    public void AttachEffect(AttachedEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _attachedEffects.Add(effect.Clone());
        OnAttachEffect(effect);
    }

    /// <summary>Remove a previously attached effect by its id.</summary>
    public bool DetachEffectById(int id)
    {
        var idx = _attachedEffects.FindIndex(e => e.Id == id);
        if (idx < 0) return false;
        var removed = _attachedEffects[idx];
        _attachedEffects.RemoveAt(idx);
        OnDetachEffect(removed);
        return true;
    }

    /// <summary>Remove all attached effects.</summary>
    public void ClearAttachedEffects()
    {
        foreach (var e in _attachedEffects) OnDetachEffect(e);
        _attachedEffects.Clear();
    }

    /// <summary>Remove only temporary (non-permanent) effects.</summary>
    public void ClearTemporaryAttachedEffects()
    {
        for (int i = _attachedEffects.Count - 1; i >= 0; i--)
        {
            if (!_attachedEffects[i].Permanent)
            {
                OnDetachEffect(_attachedEffects[i]);
                _attachedEffects.RemoveAt(i);
            }
        }
    }

    /// <summary>Remove only permanent effects.</summary>
    public void ClearPermanentAttachedEffects()
    {
        for (int i = _attachedEffects.Count - 1; i >= 0; i--)
        {
            if (_attachedEffects[i].Permanent)
            {
                OnDetachEffect(_attachedEffects[i]);
                _attachedEffects.RemoveAt(i);
            }
        }
    }

    public AttachedEffect? GetAttachedEffectById(int id)
        => _attachedEffects.FirstOrDefault(e => e.Id == id);

    protected virtual void OnAttachEffect(AttachedEffect effect) { }
    protected virtual void OnDetachEffect(AttachedEffect effect) { }
}

// ─── NpcTradeItem ─────────────────────────────────────────────────────────────

/// <summary>
/// A single entry in an NPC's trade list as received from the server.
/// Carries the item prototype, its display name, weight, and buy/sell prices.
/// Maps to the tuple elements in <c>parseOpenNpcTrade</c> /
/// <c>Game::processOpenNpcTrade</c>.
/// Task T15.
/// </summary>
public sealed record NpcTradeItem(
    Item   Item,
    string Name,
    uint   Weight,
    uint   BuyPrice,
    uint   SellPrice);

// ─── Quest records (T23) ──────────────────────────────────────────────────────

/// <summary>
/// One entry in the quest log as received from the server.
/// Maps to the per-quest tuple in <c>ProtocolGame::parseQuestLog</c>.
/// Task T23.
/// </summary>
public sealed record QuestEntry(ushort Id, string Name, bool Completed);

/// <summary>
/// A single mission within a quest, as received from the server.
/// Maps to the per-mission tuple in <c>ProtocolGame::parseQuestLine</c>.
/// Task T23.
/// </summary>
public sealed record QuestMission(string Name, string Description, ushort MissionId);

// ─── Modal dialog records (T23) ───────────────────────────────────────────────

/// <summary>A button inside a modal dialog window.</summary>
public sealed record ModalButton(byte Id, string Label);

/// <summary>A selectable choice inside a modal dialog window.</summary>
public sealed record ModalChoice(byte Id, string Label);

/// <summary>
/// A modal dialog window pushed by the server.
/// Maps to <c>ProtocolGame::parseModalDialog</c> /
/// <c>Game::processModalDialog</c>.
/// Task T23.
/// </summary>
public sealed record ModalDialog(
    uint                       WindowId,
    string                     Title,
    string                     Message,
    IReadOnlyList<ModalButton> Buttons,
    byte                       EnterButton,
    byte                       EscapeButton,
    IReadOnlyList<ModalChoice> Choices,
    bool                       Priority);

// ─── Market records (T26) ─────────────────────────────────────────────────────

/// <summary>
/// Item in the player's depot as reported by <c>parseMarketEnter</c>.
/// Maps to the per-item triple in <c>ProtocolGame::parseMarketEnter</c>.
/// Task T26.
/// </summary>
public sealed record MarketDepotItem(ushort ItemId, byte Tier, ushort Count);

/// <summary>
/// A single buy or sell offer on the market, as decoded by <c>readMarketOffer</c>.
/// Maps to <c>MarketOffer</c> struct in <c>src/client/protocolgameparse.cpp</c>.
/// Task T26.
/// </summary>
public sealed record MarketOffer(
    uint   Timestamp,
    ushort Counter,
    byte   Action,       // 0 = buy, 1 = sell
    ushort ItemId,
    byte   ItemTier,
    ushort Amount,
    ulong  Price,
    string PlayerName,
    byte   State,        // MarketOfferState
    ushort Var);         // browse var / request type

/// <summary>
/// One row in the daily price-statistics list returned by <c>parseMarketDetail</c>.
/// Maps to the inner vector in <c>readMarketStatsList</c>.
/// Task T26.
/// </summary>
public sealed record MarketStatEntry(
    ulong Day,
    byte  Action,
    uint  Transactions,
    ulong TotalPrice,
    ulong HighestPrice,
    ulong LowestPrice);

// ─── GameConfig ────────────────────────────────────────────────────────────────

/// <summary>
/// Server-side feature flags and protocol configuration negotiated during login.
/// Maps to <c>src/client/gameconfig.h</c>.
/// Task 8.24.
/// </summary>
public sealed class GameConfig
{
    // ─── Protocol version ─────────────────────────────────────────────────────

    public int  ClientVersion  { get; set; } = 1281;
    public int  ProtocolVersion { get; set; } = 1281;

    // ─── Feature flags ────────────────────────────────────────────────────────

    public bool AttackCooldown     { get; set; }
    public bool ItemInspection     { get; set; }
    public bool Podium             { get; set; }
    public bool MarketStats        { get; set; }
    public bool HasTransparency    { get; set; }
    public bool HasContainerOpen   { get; set; }
    public bool NewProtocol        { get; set; } = true;

    // ─── Map dimensions ───────────────────────────────────────────────────────

    public int MapWidth  { get; set; } = 18;
    public int MapHeight { get; set; } = 14;
}

// ─── T27: Prey data ───────────────────────────────────────────────────────────

/// <summary>Creature listed inside a Prey slot.</summary>
public sealed record PreyMonster(string Name);

/// <summary>State of a single Prey slot.</summary>
public enum PreyState : byte
{
    Locked   = 0,
    Inactive = 1,
    Active   = 2,
    Selection         = 3,
    SelectionChangeMonster = 4,
    ListSelection     = 5,
}

/// <summary>Data for one Prey slot as sent by <c>parsePreyData</c>.</summary>
public sealed class PreyData
{
    public byte         Slot          { get; set; }
    public PreyState    State         { get; set; }
    public uint         NextFreeReroll{ get; set; }
    public byte         Wildcards     { get; set; }

    // Active state extra fields
    public PreyMonster? ActiveMonster { get; set; }
    public byte         BonusType     { get; set; }
    public ushort       BonusValue    { get; set; }
    public byte         BonusGrade    { get; set; }
    public ushort       TimeLeft      { get; set; }

    // Monster list (selection states)
    public IReadOnlyList<PreyMonster> Monsters { get; set; } = [];
}

// ─── T27: Forge result ────────────────────────────────────────────────────────

/// <summary>Result data for one forge action as sent by <c>parseForgeResult</c>.</summary>
public sealed class ForgeResult
{
    public byte   ActionType   { get; set; }
    public bool   Convergence  { get; set; }
    public bool   Success      { get; set; }
    public ushort LeftItemId   { get; set; }
    public byte   LeftTier     { get; set; }
    public ushort RightItemId  { get; set; }
    public byte   RightTier    { get; set; }
    public byte   Bonus        { get; set; }
    public byte   CoreCount    { get; set; }
}

// ─── T27: Bestiary data ───────────────────────────────────────────────────────

/// <summary>One race row in the bestiary race list.</summary>
public sealed record BestiaryRace(int Race, string ClassName, ushort Count, ushort UnlockedCount);

/// <summary>One monster summary row in a bestiary overview.</summary>
public sealed record BestiaryMonster(ushort Id, byte CurrentLevel, byte Occurrence, ushort AnimusMasteryBonus);

/// <summary>Loot item entry inside <see cref="BestiaryMonsterData"/>.</summary>
public sealed record BestiaryLootItem(ushort ItemId, byte Difficulty, byte SpecialEvent, string Name, byte Amount);

/// <summary>Full monster data sheet received from <c>parseBestiaryMonsterData</c>.</summary>
public sealed class BestiaryMonsterData
{
    public ushort  Id             { get; set; }
    public string  ClassName      { get; set; } = "";
    public byte    CurrentLevel   { get; set; }
    public ushort  AnimusMasteryBonus  { get; set; }
    public ushort  AnimusMasteryPoints { get; set; }
    public uint    KillCounter    { get; set; }
    public ushort  ThirdDifficulty{ get; set; }
    public ushort  SecondUnlock   { get; set; }
    public ushort  LastProgressKillCount { get; set; }
    public byte    Difficulty     { get; set; }
    public byte    Occurrence     { get; set; }
    public IReadOnlyList<BestiaryLootItem> Loot { get; set; } = [];
    // Level 2+ fields
    public ushort  CharmValue     { get; set; }
    public byte    AttackMode     { get; set; }
    public uint    MaxHealth      { get; set; }
    public uint    Experience     { get; set; }
    public ushort  Speed          { get; set; }
    public ushort  Armor          { get; set; }
    // Level 3+ fields
    public IReadOnlyDictionary<byte, ushort> Combat { get; set; }
        = new Dictionary<byte, ushort>();
    public string  Location       { get; set; } = "";
}

// ─── T27: Imbuement durations ────────────────────────────────────────────────

/// <summary>One filled imbuement slot on an item.</summary>
public sealed record ImbuementSlot(
    byte   SlotIndex,
    string Name,
    ushort IconId,
    uint   Duration,
    byte   State);

/// <summary>One item with its imbuement slots as tracked in the HUD.</summary>
public sealed class ImbuementTrackerItem
{
    public byte   TrackSlot  { get; set; }
    public Item?  TrackedItem{ get; set; }
    public byte   TotalSlots { get; set; }
    public IReadOnlyList<ImbuementSlot> Slots { get; set; } = [];
}

// ─── T27: Wheel of Destiny ────────────────────────────────────────────────────

/// <summary>Data received from <c>parseOpenWheelWindow</c>.</summary>
public sealed class WheelData
{
    public uint   PlayerId      { get; set; }
    public bool   CanView       { get; set; }
    public byte   ChangeState   { get; set; }
    public byte   VocationId    { get; set; }
    public ushort Points        { get; set; }
    public ushort ExtraPoints   { get; set; }
}

// ─── T28: In-game Store data ──────────────────────────────────────────────────

/// <summary>One sub-offer nested inside a <see cref="StoreOffer"/>.</summary>
public sealed class StoreSubOffer
{
    public string            Name        { get; set; } = string.Empty;
    public string            Description { get; set; } = string.Empty;
    public List<string>      Icons       { get; set; } = [];
    public string            ServiceType { get; set; } = string.Empty;
}

/// <summary>One product offer in the in-game store.</summary>
public sealed class StoreOffer
{
    public uint              Id                { get; set; }
    public string            Name              { get; set; } = string.Empty;
    public string            Description       { get; set; } = string.Empty;
    public uint              Price             { get; set; }
    public byte              State             { get; set; }    // 0=Normal, 1=New, 2=Sale
    public bool              Disabled          { get; set; }
    public string            DisabledReason    { get; set; } = string.Empty;
    public string            Icon              { get; set; } = string.Empty;
    public uint              SaleValidUntil    { get; set; }
    public uint              BasePrice         { get; set; }
    public List<StoreSubOffer> SubOffers       { get; set; } = [];
}

/// <summary>One top-level category as sent by <c>parseStore</c>.</summary>
public sealed class StoreCategory
{
    public string            Name          { get; set; } = string.Empty;
    public string            Description   { get; set; } = string.Empty;
    public byte              State         { get; set; }
    public List<string>      Icons         { get; set; } = [];
    public string            Parent        { get; set; } = string.Empty;
}

/// <summary>Coin balance data sent by <c>parseCoinBalance</c>.</summary>
public sealed class CoinBalance
{
    public bool   IsUpdated          { get; set; }
    public uint   Coins              { get; set; }
    public uint   TransferableCoins  { get; set; }
    public uint   AuctionCoins       { get; set; }
}

/// <summary>Result of a completed store purchase from <c>parseCompleteStorePurchase</c>.</summary>
public sealed class StorePurchaseResult
{
    public string Message           { get; set; } = string.Empty;
    public uint   RemainingCoins    { get; set; }
    public uint   TransferableCoins { get; set; }
}

// ─── VipEntry (T43) ───────────────────────────────────────────────────────────

/// <summary>
/// One entry in the player's VIP (friends) list as maintained by the game client.
/// Populated by <c>ProtocolGame.ParseVipAdd</c> and updated by
/// <c>ProtocolGame.ParseVipState</c>.
/// Maps to the <c>Vip</c> tuple in <c>src/client/staticdata.h</c>:
/// <c>using Vip = std::tuple&lt;string, uint32_t, string, int, bool, vector&lt;uint8_t&gt;&gt;</c>.
/// Task T43.
/// </summary>
[MoonSharpUserData]
public sealed class VipEntry
{
    /// <summary>Server-assigned creature ID for the VIP player.</summary>
    public uint   Id           { get; set; }
    /// <summary>Display name of the VIP player.</summary>
    public string Name         { get; set; } = string.Empty;
    /// <summary>Online status received from the server: 0 = offline, 1 = online.
    /// Only values 0 and 1 are currently defined in the Tibia 12.x protocol.</summary>
    public uint   Status       { get; set; }
    /// <summary>Optional description text set by the player.</summary>
    public string Description  { get; set; } = string.Empty;
    /// <summary>Icon identifier chosen for this VIP entry.</summary>
    public uint   IconId       { get; set; }
    /// <summary>Whether the client should notify when this player logs in.</summary>
    public bool   NotifyLogin  { get; set; }

    // ── Lua accessor methods ────────────────────────────────────────────────
    /// <summary>Lua: <c>entry:getId()</c></summary>
    public uint   getId()          => Id;
    /// <summary>Lua: <c>entry:getName()</c></summary>
    public string getName()        => Name;
    /// <summary>Lua: <c>entry:getStatus()</c> — 0=offline, 1=online</summary>
    public uint   getStatus()      => Status;
    /// <summary>Lua: <c>entry:getDescription()</c></summary>
    public string getDescription() => Description;
    /// <summary>Lua: <c>entry:getIconId()</c></summary>
    public uint   getIconId()      => IconId;
    /// <summary>Lua: <c>entry:getNotifyLogin()</c></summary>
    public bool   getNotifyLogin() => NotifyLogin;
    /// <summary>Lua: <c>entry:isOnline()</c></summary>
    public bool   isOnline()       => Status != 0;
}

