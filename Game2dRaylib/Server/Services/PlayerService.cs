using Server.ECS;
using Server.ECS.Components;
using Shared;
using Shared.Packets;

namespace Server.Services;

public class PlayerService
{
    /// <summary>
    /// Aplica un MoveRequest: encola la dirección solicitada.
    /// </summary>
    public void ApplyMoveRequest(int networkId, MoveRequestPacket request, World world)
    {
        foreach (var entity in world.GetEntitiesWith<NetworkIdComponent>())
        {
            if (entity.GetComponent<NetworkIdComponent>().Id != networkId) continue;

            var queue = entity.GetComponent<MovementQueueComponent>();
            var dir   = (Direction)request.Direction;

            if (dir == Direction.None) 
            {
                queue.QueuedDirection = null;
            }
            else
            {
                queue.QueuedDirection = dir;
            }
            break;
        }
    }

    public void RemovePlayer(int networkId, World world)
    {
        var entity = world.GetEntitiesWith<NetworkIdComponent>()
            .FirstOrDefault(e => e.GetComponent<NetworkIdComponent>().Id == networkId);
        if (entity != null) world.RemoveEntity(entity);
    }
}
