using Shared.Packets;

namespace Server.Network;

/// <summary>
/// Per-peer server-side session state.
/// Aggregates rate limiting and per-stream sequence number tracking.
/// </summary>
public sealed class PeerSessionState
{
    // ── Rate limiting ─────────────────────────────────────────────────────

    public PeerRateLimiter RateLimiter;

    // ── Sequence number tracking ──────────────────────────────────────────

    /// <summary>Last accepted sequence number per stream id.</summary>
    private readonly Dictionary<byte, ushort> _lastSeq = new();

    /// <summary>
    /// Accept sequences within this many steps ahead of the last-accepted value.
    /// Handles minor UDP reordering while blocking replays.
    /// </summary>
    public const ushort ForwardWindow = 128;

    // ── WorldState delta baseline ─────────────────────────────────────────

    /// <summary>
    /// Last WorldState snapshot sent to this client.
    /// Null until the first full snapshot has been sent.
    /// </summary>
    public WorldStatePacket? LastSentSnapshot { get; set; }

    /// <summary>Server tick at which LastSentSnapshot was generated.</summary>
    public int LastSentTick { get; set; } = -1;

    // ── Identity ──────────────────────────────────────────────────────────

    public int NetworkId { get; }

    public PeerSessionState(int networkId,
                             int maxPacketsPerSecond = 120,
                             int abuseThreshold      = 5)
    {
        NetworkId   = networkId;
        RateLimiter = new PeerRateLimiter(maxPacketsPerSecond, abuseThreshold);
    }

    // ── Sequence helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="incoming"/> is new for
    /// <paramref name="streamId"/> and advances the counter.
    ///
    /// Wrap-around at 65535 to 0 is handled via signed modular distance:
    ///   dist = (short)(incoming - last)
    /// dist in [1, ForwardWindow] => new packet.
    /// dist == 0 or negative      => duplicate / replay => drop.
    /// dist > ForwardWindow       => too far ahead / gap attack => drop.
    /// </summary>
    public bool IsSequenceAcceptable(byte streamId, ushort incoming)
    {
        if (!_lastSeq.TryGetValue(streamId, out ushort last))
        {
            _lastSeq[streamId] = incoming;
            return true;
        }

        int dist = (short)(incoming - last);  // signed cast handles wrap-around correctly

        if (dist <= 0)            return false; // duplicate or replay
        if (dist > ForwardWindow) return false; // too far ahead

        _lastSeq[streamId] = incoming;
        return true;
    }

    public void ResetStream(byte streamId) => _lastSeq.Remove(streamId);
}

/// <summary>Stream id constants for sequence tracking.</summary>
public static class StreamId
{
    public const byte Movement = 0;
    public const byte Chat     = 1;
}
