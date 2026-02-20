namespace Shared.Creatures;

public struct LootEntry
{
    /// <summary>Item template ID.</summary>
    public ushort ItemId;
    /// <summary>Drop chance 0.0-1.0 (e.g. 0.05 = 5%).</summary>
    public float  Chance;
    public byte   MinCount;
    public byte   MaxCount;
}
