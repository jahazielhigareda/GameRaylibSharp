namespace Server.ECS.Components;

/// <summary>
/// Gives a creature a stable network ID so clients can reference it in
/// TargetRequestPacket.  Assigned by ServerWorld.SpawnCreature at birth.
/// </summary>
public struct CreatureNetworkIdComponent
{
    public int Id;
}
