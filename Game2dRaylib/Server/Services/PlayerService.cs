using Arch.Core.Extensions;
using Server.ECS;
using Server.ECS.Components;
using Shared;
using Shared.Packets;

namespace Server.Services;

public class PlayerService
{
    /// <summary>Applies a move request: queues the requested direction.</summary>
    public void ApplyMoveRequest(int networkId, MoveRequestPacket request, ServerWorld world)
    {
        var entity = world.FindPlayer(networkId);
        if (entity == Arch.Core.Entity.Null) return;

        ref var queue = ref entity.Get<MovementQueueComponent>();
        var dir = (Direction)request.Direction;
        queue.QueuedDirection = (byte)dir;
    }

    public void RemovePlayer(int networkId, ServerWorld world)
    {
        var entity = world.FindPlayer(networkId);
        if (entity != Arch.Core.Entity.Null)
            world.DestroyEntity(entity);
    }
}
