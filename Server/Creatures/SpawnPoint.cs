namespace Server.Creatures;

/// <summary>
/// Defines where and how often a creature type spawns on the map.
/// </summary>
public sealed class SpawnPoint
{
    public int    CenterX      { get; init; }
    public int    CenterY      { get; init; }
    public byte   Floor        { get; init; }
    /// <summary>Radius around the center where the creature may be placed.</summary>
    public int    Radius       { get; init; }
    public string CreatureName { get; init; } = string.Empty;
    /// <summary>Maximum simultaneous alive instances of this spawn.</summary>
    public int    MaxCount     { get; init; } = 1;
    /// <summary>Seconds after death before a new creature is spawned.</summary>
    public int    RespawnTime  { get; init; } = 60;
}
