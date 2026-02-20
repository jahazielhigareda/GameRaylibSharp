namespace Client.ECS.Components;

/// <summary>
/// Attached to every renderable creature/player entity.
/// YSortKey is the entity's pixel Y position used for painter's-algorithm
/// depth sorting (larger Y = drawn later = appears in front).
/// </summary>
public struct CreatureRenderOrder
{
    public float YSortKey;
}
