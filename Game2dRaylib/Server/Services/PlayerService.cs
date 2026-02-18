using Server.ECS;
using Server.ECS.Components;
using Shared.Packets;

namespace Server.Services;

public class PlayerService
{
    public void ApplyInput(int networkId, InputPacket input, World world)
    {
        foreach (var entity in world.GetEntitiesWith<NetworkIdComponent>())
        {
            if (entity.GetComponent<NetworkIdComponent>().Id != networkId) continue;
            var ic    = entity.GetComponent<InputComponent>();
            ic.Up     = input.Up;
            ic.Down   = input.Down;
            ic.Left   = input.Left;
            ic.Right  = input.Right;
            ic.Tick   = input.Tick;
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
