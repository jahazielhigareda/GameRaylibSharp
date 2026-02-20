namespace Server.ECS.Components;

/// <summary>Arch struct component – movement speed.</summary>
public struct SpeedComponent
{
    public float Speed;

    public SpeedComponent() { Speed = Shared.Constants.BaseSpeed; }
    public SpeedComponent(float speed) { Speed = speed; }
}
