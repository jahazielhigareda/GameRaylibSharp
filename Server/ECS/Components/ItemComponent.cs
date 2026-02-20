namespace Server.ECS.Components;

/// <summary>Arch struct component – item data.</summary>
public struct ItemComponent
{
    public ushort ItemId;
    public byte   Count;
    public bool   IsPickable;
}
