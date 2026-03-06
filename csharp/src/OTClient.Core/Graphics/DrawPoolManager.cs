namespace OTClient.Framework.Graphics;

/// <summary>
/// Registry of <see cref="DrawPool"/> instances, one per <see cref="RenderLayer"/>.
/// <para>
/// Call <see cref="FlushAll"/> once per frame (inside <c>BeginDrawing/EndDrawing</c>)
/// to execute every pool in layer order (Ground → Items → Creatures → Effects → UI).
/// </para>
/// Maps to <c>src/framework/graphics/drawpoolmanager.{h,cpp}</c>.
/// </summary>
public sealed class DrawPoolManager
{
    private readonly Dictionary<RenderLayer, DrawPool> _pools;

    public DrawPoolManager()
    {
        // Pre-create one pool per layer
        _pools = [];
        foreach (RenderLayer layer in Enum.GetValues<RenderLayer>())
            _pools[layer] = new DrawPool(layer.ToString().ToLowerInvariant(), layer);
    }

    // ─── Pool access ──────────────────────────────────────────────────────────

    /// <summary>Returns the <see cref="DrawPool"/> for <paramref name="layer"/>.</summary>
    public DrawPool GetPool(RenderLayer layer) => _pools[layer];

    /// <summary>Shorthand for <see cref="GetPool"/>.</summary>
    public DrawPool this[RenderLayer layer] => _pools[layer];

    // ─── Batch operations ─────────────────────────────────────────────────────

    /// <summary>
    /// Flushes every pool in ascending layer order.
    /// Must be called inside <c>BeginDrawing / EndDrawing</c>.
    /// </summary>
    public void FlushAll()
    {
        foreach (RenderLayer layer in Enum.GetValues<RenderLayer>())
            _pools[layer].Flush();
    }

    /// <summary>Clears all pools without rendering.</summary>
    public void ClearAll()
    {
        foreach (var pool in _pools.Values)
            pool.Clear();
    }

    /// <summary>Total number of queued commands across all pools.</summary>
    public int TotalCommandCount
    {
        get
        {
            int total = 0;
            foreach (var p in _pools.Values)
                total += p.CommandCount;
            return total;
        }
    }
}
