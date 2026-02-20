namespace Shared.Creatures;

public struct SpellEntry
{
    public ushort SpellId;
    /// <summary>Chance this spell is chosen each attack tick (0.0-1.0).</summary>
    public float  Chance;
}
