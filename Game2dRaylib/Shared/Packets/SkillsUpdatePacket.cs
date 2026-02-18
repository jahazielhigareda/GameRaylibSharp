using MessagePack;

namespace Shared.Packets;

/// <summary>
/// Paquete con información de skills del jugador.
/// </summary>
[MessagePackObject]
public class SkillsUpdatePacket
{
    [Key(0)] public int PlayerId { get; set; }

    // Cada skill: [level, percent to next level]
    [Key(1)] public int FistLevel       { get; set; }
    [Key(2)] public int FistPercent     { get; set; }
    [Key(3)] public int ClubLevel       { get; set; }
    [Key(4)] public int ClubPercent     { get; set; }
    [Key(5)] public int SwordLevel      { get; set; }
    [Key(6)] public int SwordPercent    { get; set; }
    [Key(7)] public int AxeLevel        { get; set; }
    [Key(8)] public int AxePercent      { get; set; }
    [Key(9)] public int DistanceLevel   { get; set; }
    [Key(10)] public int DistancePercent { get; set; }
    [Key(11)] public int ShieldingLevel  { get; set; }
    [Key(12)] public int ShieldingPercent { get; set; }
    [Key(13)] public int FishingLevel    { get; set; }
    [Key(14)] public int FishingPercent  { get; set; }
    [Key(15)] public int MagicLevel      { get; set; }
    [Key(16)] public int MagicPercent    { get; set; }
}
