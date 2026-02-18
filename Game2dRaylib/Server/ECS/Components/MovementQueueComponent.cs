using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Arch struct component – Tibia-style movement queue (max 1 ahead).
/// </summary>
public struct MovementQueueComponent
{
    // 255 = no queued direction (Direction.None)
    public byte QueuedDirection;
    public byte LastDirection;

    public MovementQueueComponent()
    {
        QueuedDirection = (byte)Direction.None;
        LastDirection   = (byte)Direction.South;
    }

    public readonly bool HasQueued => QueuedDirection != (byte)Direction.None;
}
