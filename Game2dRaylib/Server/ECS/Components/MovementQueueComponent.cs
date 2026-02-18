using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Cola de movimiento estilo Tibia.
/// Solo permite encolar 1 movimiento adelante.
/// </summary>
public class MovementQueueComponent
{
    public Direction? QueuedDirection { get; set; }
    public Direction  LastDirection   { get; set; } = Direction.South;
}
