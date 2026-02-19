using Raylib_cs;

namespace Client.ECS.Components;

/// <summary>
/// Client-side layered tile data populated from MapData each frame.
/// Layers mirror Tibia render order:
///   0 = Ground   1 = Border   2 = BottomItem
///   3 = Creature (handled by CreatureRenderSystem)
///   4 = TopItem  5 = Effect
/// </summary>
public struct TileRenderComponent
{
    public int    TileX;
    public int    TileY;

    // Layer 0 – ground
    public ushort GroundId;
    public Color  GroundColor;

    // Layer 1 – border transition sprite id (0 = none)
    public ushort BorderId;

    // Layer 2 – bottom items (rocks, bushes, small decor)
    public ushort BottomItemId;

    // Layer 4 – top items (trees, walls, roofs)
    public ushort TopItemId;
    public Color  TopItemColor;

    // Layer 5 – built-in tile effect id (0 = none)
    public ushort EffectId;
}
