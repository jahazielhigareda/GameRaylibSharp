using MessagePack;

namespace Shared.Packets;

/// <summary>
/// Client to Server movement request.
///
/// <see cref="Sequence"/> is a monotonically-increasing ushort per-client.
/// The server tracks the last accepted sequence number and drops duplicates /
/// very old packets (see PeerSessionState.IsSequenceAcceptable).
///
/// Wrap-around: ushort wraps at 65535 to 0.  The server treats any incoming
/// sequence within a forward window of 128 of the last-accepted value as
/// "new", and anything outside that window as a duplicate or replay.
/// </summary>
[MessagePackObject]
public class MoveRequestPacket
{
    /// <summary>Per-client monotonic counter (wraps at ushort.MaxValue).</summary>
    [Key(0)] public ushort Sequence  { get; set; }

    /// <summary>Direction cast from <see cref="Shared.Direction"/>.</summary>
    [Key(1)] public byte   Direction { get; set; }

    [Key(2)] public int    Tick      { get; set; }
}
