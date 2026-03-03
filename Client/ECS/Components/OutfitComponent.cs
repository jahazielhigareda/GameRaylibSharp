using Raylib_cs;

namespace Client.ECS.Components;

/// <summary>
/// Tibia-style outfit appearance for a creature or player entity.
/// The four color slots map to template regions on the sprite (head, body, legs, feet).
/// Used by CreatureRenderSystem to tint the base sprite at render time.
/// </summary>
public struct OutfitComponent
{
    /// <summary>Outfit ID – indexes the entity-type row in the sprite atlas.</summary>
    public ushort OutfitId;

    /// <summary>Skin / helmet color (topmost region of the sprite).</summary>
    public Color HeadColor;

    /// <summary>Torso / armour color (mid body region).</summary>
    public Color BodyColor;

    /// <summary>Trousers / skirt color (lower body region).</summary>
    public Color LegsColor;

    /// <summary>Boots / feet color (bottom region).</summary>
    public Color FeetColor;

    /// <summary>Addon bitmask: bit 0 = addon 1 active, bit 1 = addon 2 active.</summary>
    public byte Addons;

    // ── Factory helpers ───────────────────────────────────────────────────

    /// <summary>Default player outfit (outfit 0 – blue body, skin head).</summary>
    public static OutfitComponent DefaultPlayer() => new OutfitComponent
    {
        OutfitId  = 0,
        HeadColor = new Color(255, 200, 120, 255),   // skin tone
        BodyColor = new Color(50,  100, 200, 255),   // blue shirt
        LegsColor = new Color(40,  50,  130, 255),   // dark pants
        FeetColor = new Color(90,  55,  25,  255),   // brown boots
        Addons    = 0,
    };

    /// <summary>Default creature outfit (outfit 1 – red body, dark head).</summary>
    public static OutfitComponent DefaultCreature() => new OutfitComponent
    {
        OutfitId  = 1,
        HeadColor = new Color(180, 60,  60,  255),
        BodyColor = new Color(140, 40,  40,  255),
        LegsColor = new Color(100, 30,  30,  255),
        FeetColor = new Color(80,  20,  20,  255),
        Addons    = 0,
    };
}
