namespace Shared.Creatures;

/// <summary>
/// Immutable definition of a creature type loaded from data files.
/// </summary>
public sealed class CreatureTemplate
{
    public ushort           Id          { get; init; }
    public string           Name        { get; init; } = string.Empty;
    public int              MaxHP       { get; init; }
    public int              MaxMP       { get; init; }
    public int              Experience  { get; init; }
    /// <summary>Base speed in tiles/s (Tibia-style, e.g. 200).</summary>
    public int              Speed       { get; init; }
    public int              Armor       { get; init; }
    public int              Defense     { get; init; }
    public int              AttackMin   { get; init; }
    public int              AttackMax   { get; init; }
    /// <summary>Distance at which the creature detects players.</summary>
    public int              LookRange   { get; init; }
    /// <summary>Maximum tile distance before the creature gives up chasing.</summary>
    public int              ChaseRange  { get; init; }
    public CreatureBehavior Behavior    { get; init; }
    public IReadOnlyList<LootEntry>  LootTable   { get; init; } = Array.Empty<LootEntry>();
    public IReadOnlyList<SpellEntry> Spells      { get; init; } = Array.Empty<SpellEntry>();
    public IReadOnlyList<Element>    Immunities  { get; init; } = Array.Empty<Element>();
    public IReadOnlyList<Element>    Weaknesses  { get; init; } = Array.Empty<Element>();
}
