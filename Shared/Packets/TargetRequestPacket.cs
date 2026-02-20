using MessagePack;

namespace Shared.Packets;

/// <summary>
/// Sent by the client when the player clicks on a creature to target it.
/// CreatureNetId == 0 means "clear target".
/// </summary>
[MessagePackObject]
public class TargetRequestPacket
{
    /// <summary>Network ID of the creature to target (0 = clear).</summary>
    [Key(0)] public int CreatureNetId { get; set; }
}
