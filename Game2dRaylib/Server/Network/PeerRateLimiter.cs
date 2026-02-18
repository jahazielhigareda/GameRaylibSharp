namespace Server.Network;

/// <summary>
/// Per-peer, per-window packet rate limiter.
///
/// Thread safety: mutated only on the LiteNetLib receive thread (single-threaded
/// inside PollEvents()), so no locks are needed.
///
/// Abuse tracking: after <see cref="AbuseThreshold"/> consecutive windows of
/// over-limit traffic, <see cref="ShouldDisconnect"/> becomes true.
/// </summary>
public struct PeerRateLimiter
{
    // ── Configuration (set once at construction) ──────────────────────────

    /// <summary>Maximum packets accepted per 1-second window.</summary>
    public readonly int MaxPacketsPerSecond;

    /// <summary>Consecutive over-limit windows before flagging for disconnect.</summary>
    public readonly int AbuseThreshold;

    // ── Mutable state ─────────────────────────────────────────────────────

    private int  _packetCount;
    private long _windowStartTicks;   // Environment.TickCount64 (milliseconds)
    private int  _abuseCounter;

    private const long WindowMs = 1_000; // 1-second window

    public PeerRateLimiter(int maxPacketsPerSecond = 60, int abuseThreshold = 5)
    {
        MaxPacketsPerSecond = maxPacketsPerSecond;
        AbuseThreshold      = abuseThreshold;
        _packetCount        = 0;
        _windowStartTicks   = Environment.TickCount64;
        _abuseCounter       = 0;
    }

    /// <summary>
    /// Call once per received packet.
    /// Returns <c>true</c> if the packet should be accepted (within limit).
    /// Returns <c>false</c> if the packet should be dropped.
    /// Also updates <see cref="ShouldDisconnect"/>.
    /// </summary>
    public bool TryAccept()
    {
        long now = Environment.TickCount64;

        if (now - _windowStartTicks >= WindowMs)
        {
            if (_packetCount > MaxPacketsPerSecond)
                _abuseCounter++;
            else
                _abuseCounter = 0;  // good behaviour resets the counter

            _packetCount      = 0;
            _windowStartTicks = now;
        }

        _packetCount++;
        return _packetCount <= MaxPacketsPerSecond;
    }

    /// <summary>True when the peer should be kicked due to repeated abuse.</summary>
    public readonly bool ShouldDisconnect => _abuseCounter >= AbuseThreshold;

    /// <summary>Packets counted in the current window.</summary>
    public readonly int PacketCountThisWindow => _packetCount;
}
